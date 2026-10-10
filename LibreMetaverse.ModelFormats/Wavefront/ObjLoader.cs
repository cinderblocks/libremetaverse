/*
 * Copyright (c) 2026, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LibreMetaverse.Imaging;
using LibreMetaverse.Rendering;
using Path = System.IO.Path;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Parses Wavefront <c>.obj</c> model files, and the <c>.mtl</c> material libraries they refer to,
    /// into <see cref="ModelPrim"/> structures for mesh upload.
    /// </summary>
    /// <remarks>
    /// Each <c>o</c> object becomes a prim (or each <c>g</c> group, in a file with no objects), and each
    /// material used by it becomes a face. Polygons are triangulated as a fan, which is only right for
    /// convex polygons. Points, lines, curves and surfaces are ignored. A material contributes its diffuse
    /// color, opacity and diffuse texture; everything else in a <c>.mtl</c> is ignored.
    /// The input is treated as untrusted: indices are range checked, the amount of geometry is capped and
    /// material and texture files are only read from the model's directory by default.
    /// </remarks>
    public class ObjLoader : IModelLoader
    {
        // ModelPrim.CreateAsset writes face indices as 16 bit values
        private const int MaxFaceVertices = ushort.MaxValue + 1;

        // A face statement lists one corner per vertex, so this also bounds how large one can get
        private const int MaxLineLength = 1 << 20;

        private const int MaxMaterials = 10_000;

        // OBJ has no up axis of its own; the common exporters write Y-up, and Second Life is Z-up
        private static readonly Matrix4 YUpToZUp = ModelAxes.YUpToZUp;

        private static readonly Regex UriScheme = new Regex(@"^[A-Za-z][A-Za-z0-9+.\-]+:", RegexOptions.Compiled);
        private static readonly char[] Whitespace = { ' ', '\t' };

        private readonly ITextureCodec? _textureCodec;
        private string _fileName = string.Empty;
        private long _budget;

        /// <summary>
        /// Only load material libraries and textures from the directory of the model file and its
        /// subdirectories. A model file can name any path, so without this an untrusted model can make
        /// the loader read (and a caller upload) files from anywhere the user can read. Set to false
        /// for models that keep their files elsewhere, such as in a sibling directory.
        /// </summary>
        public bool RestrictTexturesToModelDirectory { get; set; } = true;

        /// <summary>Largest model, material library or texture file that will be read, in bytes</summary>
        public long MaxFileSize { get; set; } = 256L * 1024 * 1024;

        /// <summary>Largest number of positions, texture coordinates, normals and triangle corners a model may contain in total</summary>
        public int MaxVertices { get; set; } = 4_000_000;

        /// <summary>
        /// True if the model's up axis is Y, which is what most exporters write, and it has to be turned
        /// to Second Life's Z-up. Set to false for a model that is already Z-up.
        /// </summary>
        public bool YUp { get; set; } = true;

        /// <summary>
        /// Creates a new OBJ loader
        /// </summary>
        /// <param name="textureCodec">Decodes the PNG and JPEG images OBJ materials normally use.
        /// Reference LibreMetaverse.Imaging.Skia for a working implementation, or provide your own.
        /// Not required for models that only reference .tga/.jp2/.j2c textures or load no images.</param>
        public ObjLoader(ITextureCodec? textureCodec = null)
        {
            _textureCodec = textureCodec;
        }

        /// <summary>
        /// Parses an OBJ file
        /// </summary>
        /// <param name="filename">Load the model from this <c>.obj</c> file</param>
        /// <param name="loadImages">Load and decode images for uploading with model</param>
        /// <returns>A list of mesh prims that were parsed from the file, or an empty list if it
        /// could not be read</returns>
        public List<ModelPrim> Load(string filename, bool loadImages)
        {
            try
            {
                _fileName = filename;
                _budget = MaxVertices;

                var model = ReadModel(filename);

                var materials = new Dictionary<string, ModelMaterial>(StringComparer.Ordinal);
                var textureFiles = new Dictionary<ModelMaterial, string>();
                foreach (var library in model.MaterialLibraries)
                {
                    LoadMaterialLibrary(library, materials, textureFiles);
                }

                var prims = BuildPrims(model, materials);

                if (loadImages)
                {
                    LoadImages(textureFiles);
                }
                return prims;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed parsing OBJ file: " + ex.Message, ex);
                return new List<ModelPrim>();
            }
        }

        #region Parsing

        /// <summary>One corner of a face: indices (zero based, -1 if the corner has none) into the shared lists</summary>
        private struct Corner
        {
            public int Position;
            public int TexCoord;
            public int Normal;
        }

        private sealed class MaterialGroup
        {
            public string Material = string.Empty;
            // Three corners per triangle
            public List<Corner> Corners = new List<Corner>();
        }

        private sealed class ObjectPart
        {
            public string Name = string.Empty;
            public bool IsObject;
            public List<MaterialGroup> Groups = new List<MaterialGroup>();
            public Dictionary<string, MaterialGroup> GroupsByMaterial = new Dictionary<string, MaterialGroup>(StringComparer.Ordinal);

            public MaterialGroup GroupFor(string material)
            {
                if (!GroupsByMaterial.TryGetValue(material, out var group))
                {
                    group = new MaterialGroup { Material = material };
                    GroupsByMaterial[material] = group;
                    Groups.Add(group);
                }
                return group;
            }
        }

        private sealed class ObjModel
        {
            public List<Vector3> Positions = new List<Vector3>();
            public List<Vector2> TexCoords = new List<Vector2>();
            public List<Vector3> Normals = new List<Vector3>();
            public List<ObjectPart> Parts = new List<ObjectPart>();
            public List<string> MaterialLibraries = new List<string>();
            public bool HasObjects;
        }

        private ObjModel ReadModel(string filename)
        {
            CheckFileSize(filename);

            var model = new ObjModel();
            var current = new ObjectPart();
            model.Parts.Add(current);
            string material = string.Empty;
            int skippedFaces = 0;

            using (var reader = new LineReader(File.OpenRead(filename)))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    int lineNumber = reader.LineNumber;
                    line = StripComment(line);
                    if (line.Length == 0) continue;

                    var tokens = line.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0) continue;

                    switch (tokens[0])
                    {
                        case "v":
                            Spend(1);
                            if (tokens.Length < 4) throw Bad(lineNumber, "a position needs three coordinates");
                            model.Positions.Add(new Vector3(
                                Number(tokens[1], lineNumber), Number(tokens[2], lineNumber), Number(tokens[3], lineNumber)));
                            break;

                        case "vt":
                            Spend(1);
                            if (tokens.Length < 2) throw Bad(lineNumber, "a texture coordinate needs a U value");
                            model.TexCoords.Add(new Vector2(
                                Number(tokens[1], lineNumber), tokens.Length > 2 ? Number(tokens[2], lineNumber) : 0f));
                            break;

                        case "vn":
                            Spend(1);
                            if (tokens.Length < 4) throw Bad(lineNumber, "a normal needs three components");
                            model.Normals.Add(new Vector3(
                                Number(tokens[1], lineNumber), Number(tokens[2], lineNumber), Number(tokens[3], lineNumber)));
                            break;

                        case "f":
                            if (!AddFace(model, current, material, tokens, lineNumber)) skippedFaces++;
                            break;

                        case "usemtl":
                            material = Rest(line);
                            break;

                        case "mtllib":
                            model.MaterialLibraries.Add(Rest(line));
                            break;

                        case "o":
                            model.HasObjects = true;
                            current = new ObjectPart { Name = Rest(line), IsObject = true };
                            model.Parts.Add(current);
                            break;

                        case "g":
                            current = new ObjectPart { Name = Rest(line) };
                            model.Parts.Add(current);
                            break;

                        // Smoothing groups, points, lines, curves, surfaces and the rest have no meaning here
                    }
                }
            }

            if (skippedFaces > 0)
                Logger.Warn($"Ignored {skippedFaces} OBJ faces that have fewer than three corners");

            return model;
        }

        private bool AddFace(ObjModel model, ObjectPart part, string material, string[] tokens, int lineNumber)
        {
            int count = tokens.Length - 1;
            if (count < 3) return false;

            var corners = new Corner[count];
            for (int i = 0; i < count; i++)
            {
                corners[i] = ParseCorner(tokens[i + 1], model, lineNumber);
            }

            // A fan around the first corner
            Spend(3L * (count - 2));
            var list = part.GroupFor(material).Corners;
            for (int i = 1; i < count - 1; i++)
            {
                list.Add(corners[0]);
                list.Add(corners[i]);
                list.Add(corners[i + 1]);
            }
            return true;
        }

        private static Corner ParseCorner(string token, ObjModel model, int lineNumber)
        {
            string position = token;
            string texCoord = string.Empty;
            string normal = string.Empty;

            int first = token.IndexOf('/');
            if (first >= 0)
            {
                position = token.Substring(0, first);
                int second = token.IndexOf('/', first + 1);
                if (second < 0)
                {
                    texCoord = token.Substring(first + 1);
                }
                else
                {
                    texCoord = token.Substring(first + 1, second - first - 1);
                    normal = token.Substring(second + 1);
                }
            }

            return new Corner
            {
                Position = ResolveIndex(position, model.Positions.Count, "position", lineNumber),
                TexCoord = texCoord.Length == 0 ? -1 : ResolveIndex(texCoord, model.TexCoords.Count, "texture coordinate", lineNumber),
                Normal = normal.Length == 0 ? -1 : ResolveIndex(normal, model.Normals.Count, "normal", lineNumber)
            };
        }

        /// <summary>
        /// Turns an OBJ index into a zero based one. Positive indices count from the start of the file's
        /// list; negative ones count back from the last entry defined so far. Whether a positive index
        /// lands inside the list is checked once the whole file has been read.
        /// </summary>
        private static int ResolveIndex(string text, int definedSoFar, string what, int lineNumber)
        {
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index) || index == 0)
                throw Bad(lineNumber, $"'{text}' is not a valid {what} index");

            if (index > 0) return index - 1;

            long resolved = (long)definedSoFar + index;
            if (resolved < 0)
                throw Bad(lineNumber, $"{what} index {index} reaches back past the first one");
            return (int)resolved;
        }

        private static float Number(string text, int lineNumber)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !Utils.IsFinite(value))
                throw Bad(lineNumber, $"'{text}' is not a finite number");
            return value;
        }

        private static InvalidDataException Bad(int lineNumber, string message)
        {
            return new InvalidDataException($"line {lineNumber}: {message}");
        }

        private void Spend(long amount)
        {
            _budget -= amount;
            if (_budget < 0)
                throw new InvalidDataException($"Model has more than {MaxVertices} vertices and triangle corners");
        }

        private static string StripComment(string line)
        {
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            return line.Trim();
        }

        /// <summary>Everything after the statement keyword, for names that may hold spaces</summary>
        private static string Rest(string line)
        {
            int space = line.IndexOfAny(Whitespace);
            return space < 0 ? string.Empty : line.Substring(space + 1).Trim();
        }

        private void CheckFileSize(string path)
        {
            if (new FileInfo(path).Length > MaxFileSize)
                throw new InvalidDataException($"File {Path.GetFileName(path)} is larger than {MaxFileSize} bytes");
        }

        /// <summary>
        /// Reads lines, joining the ones that end in a backslash, without ever holding more than
        /// <see cref="MaxLineLength"/> characters of a line. <c>StreamReader.ReadLine</c> would happily
        /// buffer a whole file that has no line breaks.
        /// </summary>
        private sealed class LineReader : IDisposable
        {
            private readonly StreamReader _reader;
            private readonly char[] _buffer = new char[4096];
            private int _length;
            private int _position;
            private bool _skipLineFeed;

            public int LineNumber { get; private set; }

            public LineReader(Stream stream)
            {
                _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            }

            public string? ReadLine()
            {
                var sb = new StringBuilder();
                bool any = false;

                while (true)
                {
                    int c = Next();
                    if (c < 0)
                    {
                        if (!any) return null;
                        LineNumber++;
                        return sb.ToString();
                    }
                    any = true;

                    if (c == '\n' || c == '\r')
                    {
                        if (c == '\r') _skipLineFeed = true;
                        LineNumber++;

                        // A trailing backslash continues the statement on the next line
                        int end = sb.Length;
                        while (end > 0 && (sb[end - 1] == ' ' || sb[end - 1] == '\t')) end--;
                        if (end > 0 && sb[end - 1] == '\\')
                        {
                            sb.Length = end - 1;
                            sb.Append(' ');
                            continue;
                        }
                        return sb.ToString();
                    }

                    if (sb.Length >= MaxLineLength)
                        throw new InvalidDataException($"line {LineNumber + 1}: longer than {MaxLineLength} characters");
                    sb.Append((char)c);
                }
            }

            private int Next()
            {
                while (true)
                {
                    if (_position >= _length)
                    {
                        _length = _reader.Read(_buffer, 0, _buffer.Length);
                        _position = 0;
                        if (_length <= 0)
                        {
                            _length = 0;
                            return -1;
                        }
                    }

                    char c = _buffer[_position++];
                    if (_skipLineFeed)
                    {
                        _skipLineFeed = false;
                        if (c == '\n') continue;
                    }
                    return c;
                }
            }

            public void Dispose()
            {
                _reader.Dispose();
            }
        }

        #endregion Parsing

        #region Materials

        private sealed class MaterialBuilder
        {
            public ModelMaterial Material = new ModelMaterial();
            public float R = 1f, G = 1f, B = 1f, A = 1f;
        }

        private void LoadMaterialLibrary(string names, Dictionary<string, ModelMaterial> materials,
            Dictionary<ModelMaterial, string> textureFiles)
        {
            // The statement can list several files, but a file name can also hold spaces
            var candidates = new List<string> { names };
            var split = names.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 1)
            {
                var whole = ResolveFile(names, quiet: true);
                if (whole == null || !File.Exists(whole))
                {
                    candidates.Clear();
                    candidates.AddRange(split);
                }
            }

            foreach (var name in candidates)
            {
                try
                {
                    var path = ResolveFile(name, quiet: false);
                    if (path == null) continue;
                    if (!File.Exists(path))
                    {
                        Logger.Warn($"OBJ material library {name} was not found");
                        continue;
                    }
                    CheckFileSize(path);
                    ReadMaterialLibrary(path, materials, textureFiles);
                }
                catch (Exception ex)
                {
                    // A broken library costs the model its materials, not its geometry
                    Logger.Warn($"Failed loading OBJ material library {name}: {ex.Message}");
                }
            }
        }

        private void ReadMaterialLibrary(string path, Dictionary<string, ModelMaterial> materials,
            Dictionary<ModelMaterial, string> textureFiles)
        {
            MaterialBuilder? current = null;
            var builders = new List<MaterialBuilder>();

            using (var reader = new LineReader(File.OpenRead(path)))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = StripComment(line);
                    if (line.Length == 0) continue;
                    var tokens = line.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0) continue;

                    string keyword = tokens[0].ToLowerInvariant();
                    if (keyword == "newmtl")
                    {
                        var name = Rest(line);
                        if (materials.Count + builders.Count >= MaxMaterials)
                            throw new InvalidDataException($"More than {MaxMaterials} materials");
                        current = new MaterialBuilder { Material = new ModelMaterial { ID = name } };
                        builders.Add(current);
                        continue;
                    }
                    if (current == null) continue;

                    switch (keyword)
                    {
                        case "kd":
                            if (tokens.Length >= 4 &&
                                TryNumber(tokens[1], out var r) && TryNumber(tokens[2], out var g) && TryNumber(tokens[3], out var b))
                            {
                                current.R = r; current.G = g; current.B = b;
                            }
                            break;

                        case "d":
                            if (tokens.Length >= 2 && TryNumber(tokens[1], out var d)) current.A = d;
                            break;

                        case "tr":
                            if (tokens.Length >= 2 && TryNumber(tokens[1], out var tr)) current.A = 1f - tr;
                            break;

                        case "map_kd":
                            var texture = TextureName(tokens);
                            if (texture.Length == 0) break;

                            var textureFile = ResolveFile(texture, quiet: false);
                            if (textureFile == null) break;

                            // The name the upload goes by is the one the file used, with / for separators
                            current.Material.Texture = texture.Replace('\\', '/');
                            textureFiles[current.Material] = textureFile;
                            break;
                    }
                }
            }

            foreach (var builder in builders)
            {
                builder.Material.DiffuseColor = new Color4(
                    Utils.Clamp(builder.R, 0f, 1f), Utils.Clamp(builder.G, 0f, 1f),
                    Utils.Clamp(builder.B, 0f, 1f), Utils.Clamp(builder.A, 0f, 1f));
                // A name defined twice, in one library or several, keeps its first definition
                if (!materials.ContainsKey(builder.Material.ID)) materials[builder.Material.ID] = builder.Material;
            }
        }

        private static bool TryNumber(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Utils.IsFinite(value);
        }

        /// <summary>
        /// The file named by a <c>map_Kd</c> statement, which may be preceded by options such as
        /// <c>-s 1 1 1</c>, and may itself hold spaces
        /// </summary>
        private static string TextureName(string[] tokens)
        {
            int i = 1;
            while (i < tokens.Length && tokens[i].Length > 1 && tokens[i][0] == '-')
            {
                string option = tokens[i].Substring(1).ToLowerInvariant();
                i++;

                switch (option)
                {
                    case "o":
                    case "s":
                    case "t":
                        // One to three numbers
                        for (int n = 0; n < 3 && i < tokens.Length && TryNumber(tokens[i], out _); n++) i++;
                        break;
                    case "mm":
                        i += 2;
                        break;
                    case "blendu":
                    case "blendv":
                    case "cc":
                    case "clamp":
                    case "texres":
                    case "bm":
                    case "imfchan":
                    case "type":
                        i++;
                        break;
                }
            }

            return i < tokens.Length ? string.Join(" ", tokens, i, tokens.Length - i) : string.Empty;
        }

        /// <summary>
        /// Finds the file a model names, or null if it cannot or may not be read. The name is relative
        /// to the model's directory, and uses either kind of path separator.
        /// </summary>
        private string? ResolveFile(string name, bool quiet)
        {
            try
            {
                name = name.Trim().Replace('\\', '/');
                if (name.Length == 0 || name.IndexOf('\0') >= 0 || UriScheme.IsMatch(name))
                {
                    if (!quiet) Logger.Warn($"Not loading OBJ file reference '{name}': it is not a local file");
                    return null;
                }

                string directory = Path.GetDirectoryName(Path.GetFullPath(_fileName)) ?? string.Empty;
                string path = Path.Combine(directory, name.Replace('/', Path.DirectorySeparatorChar));

                if (RestrictTexturesToModelDirectory && !ModelTextureLoader.IsInModelDirectory(_fileName, path))
                {
                    if (!quiet) Logger.Warn($"Not loading OBJ file reference '{name}': it is outside the model's directory");
                    return null;
                }
                return path;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                if (!quiet) Logger.Warn($"Not loading OBJ file reference '{name}': {ex.Message}");
                return null;
            }
        }

        private void LoadImages(Dictionary<ModelMaterial, string> textureFiles)
        {
            // Materials that name the same file share one decode and encode
            var loaded = new Dictionary<string, ModelMaterial>(StringComparer.Ordinal);
            foreach (var pair in textureFiles)
            {
                var material = pair.Key;
                var file = pair.Value;

                if (loaded.TryGetValue(file, out var first))
                {
                    material.TextureData = first.TextureData;
                    material.Width = first.Width;
                    material.Height = first.Height;
                    continue;
                }
                loaded[file] = material;

                try
                {
                    CheckFileSize(file);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed loading {file}: {ex.Message}");
                    continue;
                }
                ModelTextureLoader.LoadFile(file, material.Texture, material, _textureCodec);
            }
        }

        #endregion Materials

        #region Geometry

        private List<ModelPrim> BuildPrims(ObjModel model, Dictionary<string, ModelMaterial> materials)
        {
            // Rotate everything into Second Life's axes once, so bounds are worked out in the final space
            var positions = model.Positions.ToArray();
            var normals = model.Normals.ToArray();
            if (YUp)
            {
                for (int i = 0; i < positions.Length; i++)
                    positions[i] = Vector3.Transform(positions[i], YUpToZUp);
            }
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = SafeNormalize(YUp ? Vector3.TransformNormal(normals[i], YUpToZUp) : normals[i]);
            }

            var parts = model.HasObjects ? MergeGroupsIntoObjects(model.Parts) : model.Parts;

            string baseName = Path.GetFileNameWithoutExtension(_fileName);
            var defaultMaterial = new ModelMaterial { ID = "default" };
            var prims = new List<ModelPrim>();
            int unnamed = 0;

            foreach (var part in parts)
            {
                var prim = BuildPrim(part, model, positions, normals, materials, defaultMaterial);
                if (prim == null) continue;

                prim.ID = part.Name.Length > 0 ? part.Name : (unnamed++ == 0 ? baseName : baseName + "_" + unnamed);
                prims.Add(prim);
            }

            if (prims.Count == 0)
                Logger.Warn("OBJ file has no triangles to upload");
            return prims;
        }

        /// <summary>
        /// In a file that has objects, groups are parts of an object rather than objects of their own
        /// </summary>
        private static List<ObjectPart> MergeGroupsIntoObjects(List<ObjectPart> parts)
        {
            var merged = new List<ObjectPart>();
            foreach (var part in parts)
            {
                if (part.IsObject || merged.Count == 0)
                {
                    merged.Add(part);
                    continue;
                }

                var target = merged[merged.Count - 1];
                foreach (var group in part.Groups)
                {
                    target.GroupFor(group.Material).Corners.AddRange(group.Corners);
                }
            }
            return merged;
        }

        private ModelPrim? BuildPrim(ObjectPart part, ObjModel model, Vector3[] positions, Vector3[] normals,
            Dictionary<string, ModelMaterial> materials, ModelMaterial defaultMaterial)
        {
            // Check every corner against the lists, and find the bounds of what this prim actually uses
            var boundMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;
            foreach (var group in part.Groups)
            {
                foreach (var corner in group.Corners)
                {
                    if (corner.Position >= positions.Length)
                        throw new InvalidDataException($"Face refers to position {corner.Position + 1}, but the file defines {positions.Length}");
                    if (corner.TexCoord >= model.TexCoords.Count)
                        throw new InvalidDataException($"Face refers to texture coordinate {corner.TexCoord + 1}, but the file defines {model.TexCoords.Count}");
                    if (corner.Normal >= normals.Length)
                        throw new InvalidDataException($"Face refers to normal {corner.Normal + 1}, but the file defines {normals.Length}");

                    var pos = positions[corner.Position];
                    boundMin = new Vector3(Math.Min(boundMin.X, pos.X), Math.Min(boundMin.Y, pos.Y), Math.Min(boundMin.Z, pos.Z));
                    boundMax = new Vector3(Math.Max(boundMax.X, pos.X), Math.Max(boundMax.Y, pos.Y), Math.Max(boundMax.Z, pos.Z));
                    any = true;
                }
            }
            if (!any) return null;

            var assetScale = boundMax - boundMin;
            var assetOffset = boundMin + (assetScale / 2);

            // Fit the vertex positions into the identity cube -0.5 .. 0.5
            Vector3 Normalize(Vector3 pos) => new Vector3(
                assetScale.X == 0 ? 0 : ((pos.X - boundMin.X) / assetScale.X) - 0.5f,
                assetScale.Y == 0 ? 0 : ((pos.Y - boundMin.Y) / assetScale.Y) - 0.5f,
                assetScale.Z == 0 ? 0 : ((pos.Z - boundMin.Z) / assetScale.Z) - 0.5f);

            var prim = new ModelPrim { BoundMin = boundMin, BoundMax = boundMax };
            var seen = new HashSet<int>();

            foreach (var group in part.Groups)
            {
                if (group.Corners.Count == 0) continue;

                var material = ResolveMaterial(group.Material, materials, defaultMaterial);
                var face = new ModelFace { MaterialID = material.ID, Material = material };

                for (int t = 0; t < group.Corners.Count; t += 3)
                {
                    var a = group.Corners[t];
                    var b = group.Corners[t + 1];
                    var c = group.Corners[t + 2];

                    // A corner without a normal is shaded flat
                    var flat = SafeNormalize(Vector3.Cross(positions[b.Position] - positions[a.Position],
                        positions[c.Position] - positions[a.Position]));

                    for (int k = 0; k < 3; k++)
                    {
                        var corner = group.Corners[t + k];
                        var fitted = Normalize(positions[corner.Position]);
                        if (seen.Add(corner.Position)) prim.Positions.Add(fitted);

                        var vertex = new Vertex
                        {
                            Position = fitted,
                            Normal = UnitCubeFit.FitNormal(corner.Normal >= 0 ? normals[corner.Normal] : flat, assetScale)
                        };
                        // OBJ's UV origin is the bottom left, which is Second Life's as well
                        if (corner.TexCoord >= 0) vertex.TexCoord = model.TexCoords[corner.TexCoord];
                        face.AddVertex(vertex);
                    }
                }

                if (face.Vertices.Count > MaxFaceVertices)
                {
                    Logger.Warn($"Skipping the faces of OBJ object '{part.Name}' that use material '{group.Material}': " +
                                $"they have {face.Vertices.Count} distinct vertices, and a face holds at most {MaxFaceVertices}");
                    continue;
                }
                prim.Faces.Add(face);
            }

            if (prim.Faces.Count == 0) return null;
            if (prim.Faces.Count > 8)
            {
                Logger.Warn($"OBJ object '{part.Name}' uses {prim.Faces.Count} materials; Second Life allows 8 faces per mesh");
            }

            prim.Position = assetOffset;
            prim.Scale = assetScale;
            prim.CreateAsset(UUID.Zero);
            return prim;
        }

        private static ModelMaterial ResolveMaterial(string name, Dictionary<string, ModelMaterial> materials,
            ModelMaterial defaultMaterial)
        {
            if (name.Length == 0) return defaultMaterial;

            // A material the libraries do not define is plain white
            if (!materials.TryGetValue(name, out var material))
            {
                material = new ModelMaterial { ID = name };
                materials[name] = material;
            }
            return material;
        }

        private static Vector3 SafeNormalize(Vector3 v)
        {
            float length = v.Length();
            return length > 0f && !float.IsNaN(length) && !float.IsInfinity(length) ? v / length : Vector3.Zero;
        }

        #endregion Geometry
    }
}
