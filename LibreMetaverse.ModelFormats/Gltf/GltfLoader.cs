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
using System.IO;
using System.Text.RegularExpressions;
using LibreMetaverse.Assets.Gltf;
using LibreMetaverse.Imaging;
using LibreMetaverse.Rendering;
using Path = System.IO.Path;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Parses glTF 2.0 model files (<c>.gltf</c> with external or embedded buffers, and binary
    /// <c>.glb</c>) into <see cref="ModelPrim"/> structures for mesh upload.
    /// </summary>
    /// <remarks>
    /// Only static triangle meshes are read: skins, morph targets, animation, cameras and
    /// lights are ignored, as are primitives that are not plain triangle lists. Only the base
    /// color factor and base color texture of each material are used. Meshes compressed with
    /// Draco or meshopt, or quantized with KHR_mesh_quantization, are rejected.
    /// The input is treated as untrusted: accessors are bounds-checked before they are decoded
    /// and the amount of geometry is capped.
    /// </remarks>
    public class GltfLoader : IModelLoader
    {
        // ModelPrim.CreateAsset writes face indices as 16 bit values
        private const int MaxFaceVertices = ushort.MaxValue + 1;

        // glTF is Y-up, Second Life is Z-up
        private static readonly Matrix4 YUpToZUp = ModelAxes.YUpToZUp;
        private static readonly Matrix4 ZUpToYUp = ModelAxes.ZUpToYUp;

        private static readonly Regex UriScheme = new Regex(@"^[A-Za-z][A-Za-z0-9+.\-]+:", RegexOptions.Compiled);

        private static readonly string[] UnsupportedExtensions =
        {
            "KHR_draco_mesh_compression",
            "EXT_meshopt_compression",
            "KHR_mesh_quantization"
        };

        private readonly ITextureCodec? _textureCodec;
        private string _fileName = string.Empty;
        private long _vertexBudget;

        /// <summary>
        /// Only load external buffers and textures from the directory of the model file and its
        /// subdirectories. A model file can name any path, so without this an untrusted model can make
        /// the loader read (and a caller upload) files from anywhere the user can read. Set to false
        /// for models that keep their files elsewhere, such as in a sibling directory.
        /// </summary>
        public bool RestrictTexturesToModelDirectory { get; set; } = true;

        /// <summary>Largest model, buffer or texture file that will be read, in bytes</summary>
        public long MaxFileSize { get; set; } = 256L * 1024 * 1024;

        /// <summary>Largest number of vertex attribute and index elements a model may contain in total</summary>
        public int MaxVertices { get; set; } = 4_000_000;

        /// <summary>
        /// Creates a new glTF loader
        /// </summary>
        /// <param name="textureCodec">Decodes the PNG and JPEG images glTF models normally use.
        /// Reference LibreMetaverse.Imaging.Skia for a working implementation, or provide your own.
        /// Not required for models that only reference .tga/.jp2/.j2c textures or load no images.</param>
        public GltfLoader(ITextureCodec? textureCodec = null)
        {
            _textureCodec = textureCodec;
        }

        /// <summary>
        /// Parses a glTF or GLB file
        /// </summary>
        /// <param name="filename">Load the model from this <c>.gltf</c> or <c>.glb</c> file</param>
        /// <param name="loadImages">Load and decode images for uploading with model</param>
        /// <returns>A list of mesh prims that were parsed from the file, or an empty list if it
        /// could not be read</returns>
        public List<ModelPrim> Load(string filename, bool loadImages)
        {
            try
            {
                _fileName = filename;

                var info = new FileInfo(filename);
                if (info.Length > MaxFileSize)
                    throw new InvalidDataException($"Model file is larger than {MaxFileSize} bytes");

                byte[] data = File.ReadAllBytes(filename);
                CheckGlbStructure(data);

                var doc = GltfDocument.Load(data, LoadExternalBuffer);
                foreach (var required in doc.ExtensionsRequired)
                {
                    if (Array.IndexOf(UnsupportedExtensions, required) >= 0)
                        throw new NotSupportedException($"glTF extension {required} is not supported");
                }

                _vertexBudget = MaxVertices;
                var textureSources = new Dictionary<ModelMaterial, int>();
                var prims = Parse(doc, textureSources);
                if (loadImages)
                {
                    LoadImages(doc, textureSources);
                }
                return prims;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed parsing glTF file: " + ex.Message, ex);
                return new List<ModelPrim>();
            }
        }

        #region Scene

        private sealed class MeshInstance
        {
            public string Name = string.Empty;
            public int Mesh;
            public Matrix4 World;
        }

        private sealed class BuiltMesh
        {
            public ModelPrim Template = new ModelPrim();
            public Vector3 AssetScale;
            public Vector3 AssetOffset;
        }

        private List<ModelPrim> Parse(GltfDocument doc, Dictionary<ModelMaterial, int> textureSources)
        {
            var prims = new List<ModelPrim>();
            var built = new Dictionary<int, BuiltMesh?>();
            var materials = new Dictionary<int, ModelMaterial>();
            var defaultMaterial = new ModelMaterial { ID = "default" };

            foreach (var instance in FindMeshInstances(doc))
            {
                if (!built.TryGetValue(instance.Mesh, out var mesh))
                {
                    mesh = BuildMesh(doc, instance.Mesh, materials, defaultMaterial, textureSources);
                    built[instance.Mesh] = mesh;
                }
                if (mesh == null) continue;

                // Second Life is Z-up too, so move the node's world transform into that space
                var world = ZUpToYUp * instance.World * YUpToZUp;
                if (!world.Decompose(out var scale, out var rotation, out var position))
                {
                    Logger.Warn($"Skipping glTF node {instance.Name}: its transform has no volume");
                    continue;
                }

                var prim = new ModelPrim
                {
                    ID = instance.Name,
                    Asset = mesh.Template.Asset,
                    BoundMin = mesh.Template.BoundMin,
                    BoundMax = mesh.Template.BoundMax,
                    Positions = mesh.Template.Positions,
                    Faces = mesh.Template.Faces,
                    Rotation = rotation
                };

                // The mesh was fitted into the unit cube, so the offset that took out of the node's
                // frame has to be put back, rotated and scaled the same way the node is.
                var rot = Matrix4.CreateFromQuaternion(rotation);
                var offset = Vector3.Transform(mesh.AssetOffset * scale, rot);
                prim.Position = position + offset;
                prim.Scale = scale * mesh.AssetScale;

                prims.Add(prim);
            }

            return prims;
        }

        /// <summary>
        /// Walks the default scene (or the first one, or every node nothing else parents) and
        /// returns each node that holds a mesh, with its world transform in glTF's own space.
        /// </summary>
        private static List<MeshInstance> FindMeshInstances(GltfDocument doc)
        {
            var instances = new List<MeshInstance>();

            var roots = new List<int>();
            if (doc.DefaultScene >= 0 && doc.DefaultScene < doc.Scenes.Count)
            {
                roots.AddRange(doc.Scenes[doc.DefaultScene].Nodes);
            }
            else if (doc.Scenes.Count > 0)
            {
                roots.AddRange(doc.Scenes[0].Nodes);
            }
            else
            {
                var isChild = new bool[doc.Nodes.Count];
                foreach (var node in doc.Nodes)
                {
                    foreach (var child in node.Children)
                    {
                        if (child >= 0 && child < isChild.Length) isChild[child] = true;
                    }
                }
                for (int i = 0; i < isChild.Length; i++)
                {
                    if (!isChild[i]) roots.Add(i);
                }
            }

            // Walk without recursion, since the node graph comes from the file. A node can only be
            // visited once, which also keeps a malformed graph with a cycle from looping forever.
            var visited = new bool[doc.Nodes.Count];
            var pending = new Stack<KeyValuePair<int, Matrix4>>();
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                pending.Push(new KeyValuePair<int, Matrix4>(roots[i], Matrix4.Identity));
            }

            while (pending.Count > 0)
            {
                var item = pending.Pop();
                int index = item.Key;
                if (index < 0 || index >= doc.Nodes.Count)
                {
                    Logger.Warn($"Ignoring reference to missing glTF node {index}");
                    continue;
                }
                if (visited[index])
                {
                    Logger.Warn($"Ignoring glTF node {index}: it is reachable more than once");
                    continue;
                }
                visited[index] = true;

                var node = doc.Nodes[index];
                var world = LocalTransform(node) * item.Value;

                if (node.Mesh >= 0)
                {
                    instances.Add(new MeshInstance
                    {
                        Name = string.IsNullOrEmpty(node.Name) ? "node" + index : node.Name!,
                        Mesh = node.Mesh,
                        World = world
                    });
                }

                for (int i = node.Children.Count - 1; i >= 0; i--)
                {
                    pending.Push(new KeyValuePair<int, Matrix4>(node.Children[i], world));
                }
            }

            return instances;
        }

        private static Matrix4 LocalTransform(GltfNode node)
        {
            // GltfNode.Matrix keeps the column vector layout of the file, with the translation in
            // M14..M34. Matrix4 math is row vector, so it has to be flipped before it can be combined.
            if (node.Matrix.HasValue)
                return Matrix4.Transpose(node.Matrix.Value);

            return Matrix4.CreateScale(node.Scale)
                   * Matrix4.CreateFromQuaternion(node.Rotation)
                   * Matrix4.CreateTranslation(node.Translation);
        }

        #endregion Scene

        #region Geometry

        private sealed class PrimitiveData
        {
            public Vector3[] Positions = Array.Empty<Vector3>();
            public Vector3[]? Normals;
            public Vector2[]? TexCoords;
            public uint[]? Indices;
            public int TriangleCount;
            public ModelMaterial Material = new ModelMaterial();
        }

        private BuiltMesh? BuildMesh(GltfDocument doc, int meshIndex, Dictionary<int, ModelMaterial> materials,
            ModelMaterial defaultMaterial, Dictionary<ModelMaterial, int> textureSources)
        {
            if (meshIndex < 0 || meshIndex >= doc.Meshes.Count)
                throw new InvalidDataException($"Node refers to missing glTF mesh {meshIndex}");

            var gltfMesh = doc.Meshes[meshIndex];
            var primitives = new List<PrimitiveData>();

            foreach (var primitive in gltfMesh.Primitives)
            {
                if (primitive.Mode != GltfPrimitiveMode.Triangles)
                {
                    Logger.Warn($"Skipping glTF primitive of mesh {meshIndex}: only triangle lists are supported");
                    continue;
                }

                var data = ReadPrimitive(doc, primitive);
                if (data.TriangleCount == 0) continue;

                data.Material = GetMaterial(doc, primitive.Material, materials, defaultMaterial, textureSources);
                primitives.Add(data);
            }

            if (primitives.Count == 0)
            {
                Logger.Warn($"glTF mesh {meshIndex} has no triangles to upload");
                return null;
            }
            if (primitives.Count > 8)
            {
                Logger.Warn($"glTF mesh {meshIndex} has {primitives.Count} primitives; Second Life allows 8 faces per mesh");
            }

            // Fit the vertex positions of every primitive into the identity cube -0.5 .. 0.5
            var boundMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in primitives)
            {
                foreach (var pos in p.Positions)
                {
                    boundMin = new Vector3(Math.Min(boundMin.X, pos.X), Math.Min(boundMin.Y, pos.Y), Math.Min(boundMin.Z, pos.Z));
                    boundMax = new Vector3(Math.Max(boundMax.X, pos.X), Math.Max(boundMax.Y, pos.Y), Math.Max(boundMax.Z, pos.Z));
                }
            }

            var result = new BuiltMesh();
            result.AssetScale = boundMax - boundMin;
            result.AssetOffset = boundMin + (result.AssetScale / 2);
            var template = result.Template;
            template.BoundMin = boundMin;
            template.BoundMax = boundMax;

            foreach (var p in primitives)
            {
                var normalized = new Vector3[p.Positions.Length];
                for (int i = 0; i < normalized.Length; i++)
                {
                    var pos = p.Positions[i];
                    normalized[i] = new Vector3(
                        result.AssetScale.X == 0 ? 0 : ((pos.X - boundMin.X) / result.AssetScale.X) - 0.5f,
                        result.AssetScale.Y == 0 ? 0 : ((pos.Y - boundMin.Y) / result.AssetScale.Y) - 0.5f,
                        result.AssetScale.Z == 0 ? 0 : ((pos.Z - boundMin.Z) / result.AssetScale.Z) - 0.5f);
                }
                template.Positions.AddRange(normalized);

                var face = new ModelFace { MaterialID = p.Material.ID, Material = p.Material };
                var corners = new uint[3];
                for (int t = 0; t < p.TriangleCount; t++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        corners[c] = p.Indices == null ? (uint)(t * 3 + c) : p.Indices[t * 3 + c];
                    }

                    Vector3 flat = Vector3.Zero;
                    if (p.Normals == null)
                    {
                        // glTF says to shade a mesh without normals flat
                        flat = SafeNormalize(Vector3.Cross(
                            p.Positions[corners[1]] - p.Positions[corners[0]],
                            p.Positions[corners[2]] - p.Positions[corners[0]]));
                    }

                    foreach (var index in corners)
                    {
                        var vertex = new Vertex
                        {
                            Position = normalized[index],
                            Normal = p.Normals != null ? p.Normals[index] : flat
                        };
                        if (p.TexCoords != null)
                        {
                            // glTF's UV origin is the top left, Second Life's is the bottom left
                            var uv = p.TexCoords[index];
                            vertex.TexCoord = new Vector2(uv.X, 1f - uv.Y);
                        }
                        face.AddVertex(vertex);
                    }
                }

                if (face.Vertices.Count > MaxFaceVertices)
                {
                    Logger.Warn($"Skipping a glTF primitive of mesh {meshIndex}: it has {face.Vertices.Count} " +
                                $"distinct vertices, and a face holds at most {MaxFaceVertices}");
                    continue;
                }
                template.Faces.Add(face);
            }

            if (template.Faces.Count == 0) return null;

            template.CreateAsset(UUID.Zero);
            return result;
        }

        private PrimitiveData ReadPrimitive(GltfDocument doc, GltfPrimitive primitive)
        {
            if (!primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_POSITION, out var posAccessor))
                throw new InvalidDataException("glTF primitive has no POSITION attribute");

            int vertexCount = CheckAccessor(doc, posAccessor, GltfAccessorType.Vec3, "POSITION",
                c => c == GltfComponentType.Float, false);

            var data = new PrimitiveData();

            // Positions and normals are rotated into Second Life's axes here, so bounds can be
            // worked out in the final space
            var positions = doc.GetPositions(primitive);
            data.Positions = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                if (!positions[i].IsFinite())
                    throw new InvalidDataException("glTF POSITION data contains a value that is not a number");
                data.Positions[i] = Vector3.Transform(positions[i], YUpToZUp);
            }

            if (primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_NORMAL, out var norAccessor))
            {
                int count = CheckAccessor(doc, norAccessor, GltfAccessorType.Vec3, "NORMAL",
                    c => c == GltfComponentType.Float, false);
                if (count != vertexCount)
                    throw new InvalidDataException("glTF NORMAL and POSITION counts differ");

                var normals = doc.GetNormals(primitive);
                data.Normals = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    data.Normals[i] = SafeNormalize(Vector3.TransformNormal(normals[i], YUpToZUp));
                }
            }

            if (primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_TEXCOORD_0, out var uvAccessor))
            {
                int count = CheckAccessor(doc, uvAccessor, GltfAccessorType.Vec2, "TEXCOORD_0",
                    c => c == GltfComponentType.Float, true);
                if (count != vertexCount)
                    throw new InvalidDataException("glTF TEXCOORD_0 and POSITION counts differ");

                data.TexCoords = doc.GetTexCoords(primitive);
            }

            int indexCount = vertexCount;
            if (primitive.Indices >= 0)
            {
                indexCount = CheckAccessor(doc, primitive.Indices, GltfAccessorType.Scalar, "indices",
                    c => c == GltfComponentType.UnsignedByte || c == GltfComponentType.UnsignedShort ||
                         c == GltfComponentType.UnsignedInt, false);

                var indices = doc.GetIndices(primitive);
                foreach (var index in indices)
                {
                    if (index >= vertexCount)
                        throw new InvalidDataException("glTF index is past the end of the vertex data");
                }
                data.Indices = indices;
            }

            data.TriangleCount = indexCount / 3;
            return data;
        }

        private static Vector3 SafeNormalize(Vector3 v)
        {
            float length = v.Length();
            return length > 0f && !float.IsNaN(length) && !float.IsInfinity(length) ? v / length : Vector3.Zero;
        }

        /// <summary>
        /// Makes sure every element of an accessor lies inside its buffer, so the decoders in
        /// <see cref="GltfDocument"/> can be trusted not to read past the end of the data or
        /// allocate arrays sized by the file.
        /// </summary>
        /// <returns>The number of elements in the accessor</returns>
        private int CheckAccessor(GltfDocument doc, int index, GltfAccessorType type, string what,
            Func<GltfComponentType, bool> componentAllowed, bool allowNormalizedInteger)
        {
            if (index < 0 || index >= doc.Accessors.Count)
                throw new InvalidDataException($"glTF {what} refers to missing accessor {index}");

            var accessor = doc.Accessors[index];
            if (accessor.Type != type)
                throw new InvalidDataException($"glTF {what} accessor has the wrong element type");

            bool normalizedInteger = allowNormalizedInteger && accessor.Normalized &&
                (accessor.ComponentType == GltfComponentType.UnsignedByte ||
                 accessor.ComponentType == GltfComponentType.UnsignedShort);
            if (!componentAllowed(accessor.ComponentType) && !normalizedInteger)
                throw new InvalidDataException($"glTF {what} accessor has an unsupported component type");

            if (accessor.Count < 0)
                throw new InvalidDataException($"glTF {what} accessor has a negative count");
            if (accessor.Count > _vertexBudget)
                throw new InvalidDataException($"glTF model is larger than the {MaxVertices} vertices allowed");
            _vertexBudget -= accessor.Count;

            if (accessor.BufferView < 0 || accessor.BufferView >= doc.BufferViews.Count)
                throw new InvalidDataException($"glTF {what} accessor has no buffer view");
            var view = doc.BufferViews[accessor.BufferView];
            if (view.Buffer < 0 || view.Buffer >= doc.Buffers.Count)
                throw new InvalidDataException($"glTF {what} buffer view refers to a missing buffer");
            var buffer = doc.Buffers[view.Buffer].Data;
            if (buffer == null)
                throw new InvalidDataException($"glTF {what} buffer has no data");

            long elementSize = accessor.DefaultStride;
            long stride = view.ByteStride > 0 ? view.ByteStride : elementSize;
            if (stride < elementSize)
                throw new InvalidDataException($"glTF {what} buffer view stride is smaller than its elements");

            long start = (long)view.ByteOffset + accessor.ByteOffset;
            if (view.ByteOffset < 0 || accessor.ByteOffset < 0)
                throw new InvalidDataException($"glTF {what} accessor has a negative offset");

            long end = accessor.Count == 0 ? start : start + (accessor.Count - 1L) * stride + elementSize;
            if (end > buffer.Length)
                throw new InvalidDataException($"glTF {what} accessor reads past the end of its buffer");

            return accessor.Count;
        }

        #endregion Geometry

        #region Materials and images

        private ModelMaterial GetMaterial(GltfDocument doc, int index, Dictionary<int, ModelMaterial> materials,
            ModelMaterial defaultMaterial, Dictionary<ModelMaterial, int> textureSources)
        {
            if (index < 0) return defaultMaterial;
            if (index >= doc.Materials.Count)
                throw new InvalidDataException($"glTF primitive refers to missing material {index}");

            if (materials.TryGetValue(index, out var existing)) return existing;

            var source = doc.Materials[index];
            var material = new ModelMaterial
            {
                ID = string.IsNullOrEmpty(source.Name) ? "material" + index : source.Name!,
                DiffuseColor = new Color4(
                    Utils.Clamp(source.BaseColorFactor.R, 0f, 1f),
                    Utils.Clamp(source.BaseColorFactor.G, 0f, 1f),
                    Utils.Clamp(source.BaseColorFactor.B, 0f, 1f),
                    Utils.Clamp(source.BaseColorFactor.A, 0f, 1f))
            };

            int textureIndex = source.BaseColorTexture?.Index ?? -1;
            if (textureIndex >= 0)
            {
                if (textureIndex >= doc.Textures.Count ||
                    doc.Textures[textureIndex].Source < 0 || doc.Textures[textureIndex].Source >= doc.Images.Count)
                {
                    Logger.Warn($"glTF material {material.ID} refers to a missing texture");
                }
                else
                {
                    int imageIndex = doc.Textures[textureIndex].Source;
                    var image = doc.Images[imageIndex];

                    // A texture that cannot be used costs the material its texture, not the model its mesh
                    try
                    {
                        // The name is what ModelUploader uses to share one copy of a texture between faces
                        material.Texture = image.Uri != null && !image.Uri.StartsWith("data:", StringComparison.Ordinal)
                            ? DecodeUri(image.Uri)
                            : "gltf-image-" + imageIndex;
                        textureSources[material] = imageIndex;
                    }
                    catch (InvalidDataException ex)
                    {
                        Logger.Warn($"Not loading texture for glTF material {material.ID}: {ex.Message}");
                    }
                }
            }

            materials[index] = material;
            return material;
        }

        private void LoadImages(GltfDocument doc, Dictionary<ModelMaterial, int> textureSources)
        {
            // Materials that use the same image share one decode and encode
            var done = new Dictionary<int, ModelMaterial>();

            foreach (var entry in textureSources)
            {
                var material = entry.Key;
                int imageIndex = entry.Value;

                if (!done.TryGetValue(imageIndex, out var loaded))
                {
                    loaded = new ModelMaterial { Texture = material.Texture };
                    LoadImage(doc, doc.Images[imageIndex], loaded);
                    done[imageIndex] = loaded;
                }

                material.TextureData = loaded.TextureData;
                material.Width = loaded.Width;
                material.Height = loaded.Height;
            }
        }

        private void LoadImage(GltfDocument doc, GltfImage image, ModelMaterial material)
        {
            try
            {
                if (image.Uri != null && !image.Uri.StartsWith("data:", StringComparison.Ordinal))
                {
                    var relative = DecodeUri(image.Uri);
                    var fname = Path.Combine(Path.GetDirectoryName(_fileName) ?? string.Empty, relative);

                    if (RestrictTexturesToModelDirectory && !ModelTextureLoader.IsInModelDirectory(_fileName, fname))
                    {
                        Logger.Warn($"Not loading texture {relative}: it is outside the model's directory " +
                                    "(see GltfLoader.RestrictTexturesToModelDirectory)");
                        return;
                    }

                    CheckFileSize(fname);
                    ModelTextureLoader.LoadFile(fname, relative, material, _textureCodec);
                    return;
                }

                byte[] bytes;
                string extension = ExtensionForMimeType(image.MimeType);
                if (image.Uri != null)
                {
                    bytes = DecodeDataUri(image.Uri, out var mimeType);
                    if (image.MimeType == null) extension = ExtensionForMimeType(mimeType);
                }
                else
                {
                    bytes = ReadBufferView(doc, image.BufferView);
                }

                ModelTextureLoader.LoadBytes(bytes, extension, material.Texture, material, _textureCodec);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed loading glTF image {material.Texture}: {ex.Message}");
            }
        }

        private static string ExtensionForMimeType(string? mimeType)
        {
            switch (mimeType?.ToLowerInvariant())
            {
                case "image/png": return ".png";
                case "image/jpeg": return ".jpg";
                case "image/x-tga":
                case "image/tga": return ".tga";
                case "image/jp2":
                case "image/j2c": return ".j2c";
                default: return ".img";
            }
        }

        private byte[] ReadBufferView(GltfDocument doc, int viewIndex)
        {
            if (viewIndex < 0 || viewIndex >= doc.BufferViews.Count)
                throw new InvalidDataException("glTF image refers to a missing buffer view");

            var view = doc.BufferViews[viewIndex];
            if (view.Buffer < 0 || view.Buffer >= doc.Buffers.Count || doc.Buffers[view.Buffer].Data == null)
                throw new InvalidDataException("glTF image buffer view refers to a missing buffer");

            var buffer = doc.Buffers[view.Buffer].Data!;
            if (view.ByteOffset < 0 || view.ByteLength < 0 || (long)view.ByteOffset + view.ByteLength > buffer.Length)
                throw new InvalidDataException("glTF image buffer view reads past the end of its buffer");

            var bytes = new byte[view.ByteLength];
            Buffer.BlockCopy(buffer, view.ByteOffset, bytes, 0, bytes.Length);
            return bytes;
        }

        private byte[] DecodeDataUri(string uri, out string? mimeType)
        {
            int comma = uri.IndexOf(',');
            string header = comma < 0 ? uri : uri.Substring(0, comma);
            if (comma < 0 || !header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Only base64 data URIs are supported");

            mimeType = header.Substring("data:".Length, header.Length - "data:".Length - ";base64".Length);
            if (uri.Length - comma > MaxFileSize * 2)
                throw new InvalidDataException("Embedded data is too large");
            return Convert.FromBase64String(uri.Substring(comma + 1));
        }

        #endregion Materials and images

        #region Files

        /// <summary>
        /// Turns a URI from the file into a relative path. Only plain relative file references are
        /// allowed, never other kinds of URI such as http: or file:.
        /// </summary>
        private static string DecodeUri(string uri)
        {
            if (UriScheme.IsMatch(uri))
                throw new InvalidDataException($"Only files next to the model can be loaded, not {uri}");

            string path = Uri.UnescapeDataString(uri);
            if (path.IndexOf('\0') >= 0)
                throw new InvalidDataException("File name contains a null character");

            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        private byte[] LoadExternalBuffer(string uri)
        {
            var relative = DecodeUri(uri);
            var fname = Path.Combine(Path.GetDirectoryName(_fileName) ?? string.Empty, relative);

            if (RestrictTexturesToModelDirectory && !ModelTextureLoader.IsInModelDirectory(_fileName, fname))
                throw new InvalidDataException($"Buffer {relative} is outside the model's directory " +
                                               "(see GltfLoader.RestrictTexturesToModelDirectory)");

            CheckFileSize(fname);
            return File.ReadAllBytes(fname);
        }

        private void CheckFileSize(string fname)
        {
            if (new FileInfo(fname).Length > MaxFileSize)
                throw new InvalidDataException($"{fname} is larger than {MaxFileSize} bytes");
        }

        /// <summary>
        /// <see cref="GltfDocument.LoadGlb"/> allocates each chunk at the size its header claims, so a
        /// short file with a huge chunk length has to be refused before it gets there.
        /// </summary>
        private static void CheckGlbStructure(byte[] data)
        {
            if (data.Length < 4 || data[0] != 0x67 || data[1] != 0x6C || data[2] != 0x54 || data[3] != 0x46)
                return;

            if (data.Length < 12)
                throw new InvalidDataException("GLB file is shorter than its header");

            uint declared = BitConverter.ToUInt32(data, 8);
            if (!BitConverter.IsLittleEndian)
                declared = (declared >> 24) | ((declared >> 8) & 0xFF00) | ((declared << 8) & 0xFF0000) | (declared << 24);
            if (declared > data.Length)
                throw new InvalidDataException("GLB header claims more data than the file holds");

            long pos = 12;
            while (pos < declared)
            {
                if (pos + 8 > declared)
                    throw new InvalidDataException("GLB chunk header is cut off");
                uint chunkLength = BitConverter.ToUInt32(data, (int)pos);
                if (!BitConverter.IsLittleEndian)
                    chunkLength = (chunkLength >> 24) | ((chunkLength >> 8) & 0xFF00) | ((chunkLength << 8) & 0xFF0000) | (chunkLength << 24);
                if (chunkLength > declared - pos - 8)
                    throw new InvalidDataException("GLB chunk is longer than the file");
                pos += 8 + chunkLength;
            }
        }

        #endregion Files
    }
}
