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
using NumMatrix = System.Numerics.Matrix4x4;
using NumVector3 = System.Numerics.Vector3;
using Path = System.IO.Path;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Parses glTF 2.0 model files (<c>.gltf</c> with external or embedded buffers, and binary
    /// <c>.glb</c>) into <see cref="ModelPrim"/> structures for mesh upload.
    /// </summary>
    /// <remarks>
    /// Triangle meshes and skins are read; morph targets, animation, cameras and lights are
    /// ignored, as are primitives that are not plain triangle lists. A skinned mesh becomes a rigged prim:
    /// its weights go to the avatar joints the skin's nodes are named after (up to four a vertex, strongest
    /// first), its inverse bind matrices are carried over, and the node's own transform is ignored, as the
    /// glTF specification says. Joints whose names are not on the avatar skeleton are dropped, as are
    /// influences beyond the fourth. Joint position overrides are not imported. Only the base
    /// color factor and base color texture of each material are used. Meshes compressed with
    /// Draco or meshopt, or quantized with KHR_mesh_quantization, are rejected.
    /// The input is treated as untrusted: accessors are bounds-checked before they are decoded
    /// and the amount of geometry is capped.
    /// </remarks>
    public class GltfLoader : IDiagnosticModelLoader
    {
        // ModelPrim.CreateAsset writes face indices as 16 bit values
        private const int MaxFaceVertices = ModelPrim.MaxVerticesPerFace;

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
        private Dictionary<int, SkinInfo?> _skins = new Dictionary<int, SkinInfo?>();
        private int[]? _nodeParents;

        /// <summary>
        /// Only load external buffers and textures from the directory of the model file and its
        /// subdirectories. A model file can name any path, so without this an untrusted model can make
        /// the loader read (and a caller upload) files from anywhere the user can read. Set to false
        /// for models that keep their files elsewhere, such as in a sibling directory.
        /// </summary>
        public bool RestrictTexturesToModelDirectory { get; set; } = true;

        /// <summary>Largest model, buffer or texture file that will be read, in bytes</summary>
        public long MaxFileSize { get; set; } = 256L * 1024 * 1024;

        /// <summary>
        /// How the joints of a skin are read. The bones of the avatar skeleton have no rotation at rest, but the
        /// joints of a rig made in a modelling program often do (Blender turns a bone's axis to run along the
        /// bone), and a mesh bound to those axes comes out turned around each joint on the avatar. When true,
        /// the default, only each joint's position is taken from the file and its axes are the avatar's, which
        /// is right for both kinds of rig. When false the inverse bind matrices are carried over exactly as they
        /// are authored, which is right only for a rig whose joints have no rotation either.
        /// </summary>
        public bool UseAvatarJointAxes { get; set; } = true;

        /// <summary>Most joints a skinned mesh may be weighted to (after those the avatar skeleton does not
        /// have are dropped). A glTF skin usually lists a whole armature, and a mesh only counts the joints it has
        /// weight on. The reference viewer ignores any beyond 110.</summary>
        public int MaxJoints { get; set; } = ModelSkin.MaxJoints;

        /// <summary>Largest number of vertex attribute and index elements a model may contain in total</summary>
        public int MaxVertices { get; set; } = 4_000_000;

        /// <summary>
        /// Load the textures of each material's <see cref="ModelMaterial.Pbr"/> too (normal, metallic-roughness and emissive;
        /// the base color is loaded in any case), prepared the way the viewer prepares them when it imports a glTF
        /// material, and report what a Second Life material cannot hold (see <see cref="ModelDiagnosticCode.PbrApproximated"/>).
        /// Off by default, because it decodes and compresses extra images, and the mesh upload does not use them. The material's
        /// factors and the names of its textures are there either way.
        /// </summary>
        public bool LoadPbrTextures { get; set; }

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
            return new List<ModelPrim>(LoadWithDiagnostics(filename, loadImages).Prims);
        }

        /// <summary>
        /// Parses a glTF or GLB file, and reports what it could not use
        /// </summary>
        /// <param name="filename">Load the model from this <c>.gltf</c> or <c>.glb</c> file</param>
        /// <param name="loadImages">Load and decode images for uploading with model</param>
        public ModelLoadResult LoadWithDiagnostics(string filename, bool loadImages)
        {
            _diagnostics = new ModelDiagnosticCollector();
            var prims = new List<ModelPrim>();
            try
            {
                _fileName = filename;

                var info = new FileInfo(filename);
                if (info.Length > MaxFileSize)
                    throw new LimitExceededException($"Model file is larger than {MaxFileSize} bytes");

                byte[] data = File.ReadAllBytes(filename);
                CheckGlbStructure(data);

                var doc = GltfDocument.Load(data, LoadExternalBuffer);
                foreach (var required in doc.ExtensionsRequired)
                {
                    if (Array.IndexOf(UnsupportedExtensions, required) >= 0)
                        throw new NotSupportedException($"glTF extension {required} is not supported");
                }

                _vertexBudget = MaxVertices;
                _skins = new Dictionary<int, SkinInfo?>();
                _nodeParents = null;
                _pbrSources = new List<PbrSource>();
                var textureSources = new Dictionary<ModelMaterial, int>();
                prims = Parse(doc, textureSources);
                if (LoadPbrTextures) WarnAboutMaterialExtensions(doc);
                if (loadImages)
                {
                    LoadImages(doc, textureSources);
                    if (LoadPbrTextures) LoadPbrImages(doc);
                }
            }
            catch (Exception ex)
            {
                _diagnostics.ReportFailure(ex, "glTF");
                prims = new List<ModelPrim>();
            }
            return new ModelLoadResult(prims, _diagnostics.Diagnostics);
        }

        private ModelDiagnosticCollector _diagnostics = new ModelDiagnosticCollector();

        #region Scene

        private sealed class MeshInstance
        {
            public string Name = string.Empty;
            public int Mesh;
            public int Skin = -1;
            public Matrix4 World;
        }

        private sealed class BuiltMesh
        {
            public ModelPrim Template = new ModelPrim();
            public Vector3 AssetScale;
            public Vector3 AssetOffset;
            public bool Skinned;
        }

        private List<ModelPrim> Parse(GltfDocument doc, Dictionary<ModelMaterial, int> textureSources)
        {
            var prims = new List<ModelPrim>();
            var built = new Dictionary<long, BuiltMesh?>();
            var materials = new Dictionary<int, ModelMaterial>();
            var defaultMaterial = new ModelMaterial { ID = "default" };

            foreach (var instance in FindMeshInstances(doc))
            {
                // The same mesh skinned two ways is two different assets
                long key = instance.Mesh + ((instance.Skin + 1L) << 32);
                if (!built.TryGetValue(key, out var mesh))
                {
                    mesh = BuildMesh(doc, instance.Mesh, instance.Skin, materials, defaultMaterial, textureSources);
                    built[key] = mesh;
                }
                if (mesh == null) continue;

                if (mesh.Skinned)
                {
                    // A skinned mesh is placed by its joints, and its node's transform is not used. The prim
                    // is sized like any other, to match the unit cube it was fitted into.
                    prims.Add(new ModelPrim
                    {
                        ID = instance.Name,
                        Asset = mesh.Template.Asset,
                        BoundMin = mesh.Template.BoundMin,
                        BoundMax = mesh.Template.BoundMax,
                        Positions = mesh.Template.Positions,
                        Faces = mesh.Template.Faces,
                        Skin = mesh.Template.Skin,
                        Position = mesh.AssetOffset,
                        Scale = mesh.AssetScale
                    });
                    continue;
                }

                // Second Life is Z-up too, so move the node's world transform into that space
                var world = ZUpToYUp * instance.World * YUpToZUp;
                if (!world.Decompose(out var scale, out var rotation, out var position))
                {
                    _diagnostics.Warn(ModelDiagnosticCode.NodeSkipped, $"Skipping glTF node {instance.Name}: its transform has no volume", instance.Name);
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
        private List<MeshInstance> FindMeshInstances(GltfDocument doc)
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
                    _diagnostics.Warn(ModelDiagnosticCode.NodeSkipped, $"Ignoring reference to missing glTF node {index}", index.ToString());
                    continue;
                }
                if (visited[index])
                {
                    _diagnostics.Warn(ModelDiagnosticCode.NodeSkipped, $"Ignoring glTF node {index}: it is reachable more than once", doc.Nodes[index].Name);
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
                        Skin = node.Skin,
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
            public VertexWeight[]? Weights;
            public int TriangleCount;
            public ModelMaterial Material = new ModelMaterial();
        }

        private BuiltMesh? BuildMesh(GltfDocument doc, int meshIndex, int skinIndex, Dictionary<int, ModelMaterial> materials,
            ModelMaterial defaultMaterial, Dictionary<ModelMaterial, int> textureSources)
        {
            if (meshIndex < 0 || meshIndex >= doc.Meshes.Count)
                throw new InvalidDataException($"Node refers to missing glTF mesh {meshIndex}");

            // A skin none of whose joints the avatar has leaves a mesh that is just a mesh
            var skin = skinIndex >= 0 ? ReadSkin(doc, skinIndex) : null;

            var gltfMesh = doc.Meshes[meshIndex];
            var primitives = new List<PrimitiveData>();

            foreach (var primitive in gltfMesh.Primitives)
            {
                if (primitive.Mode != GltfPrimitiveMode.Triangles)
                {
                    _diagnostics.Warn(ModelDiagnosticCode.PrimitiveSkipped, $"Skipping glTF primitive of mesh {meshIndex}: only triangle lists are supported", doc.Meshes[meshIndex].Name);
                    continue;
                }

                var data = ReadPrimitive(doc, primitive, skin);
                if (data.TriangleCount == 0) continue;

                data.Material = GetMaterial(doc, primitive.Material, materials, defaultMaterial, textureSources);
                primitives.Add(data);
            }

            if (primitives.Count == 0)
            {
                _diagnostics.Warn(ModelDiagnosticCode.NoGeometry, $"glTF mesh {meshIndex} has no triangles to upload", doc.Meshes[meshIndex].Name);
                return null;
            }
            if (primitives.Count > ModelPrim.MaxFaces)
            {
                _diagnostics.Warn(ModelDiagnosticCode.TooManyFaces, $"glTF mesh {meshIndex} has {primitives.Count} primitives; Second Life allows 8 faces per mesh", doc.Meshes[meshIndex].Name, primitives.Count);
            }

            // A skin lists every joint of the file's armature, which is far more than any one mesh is weighted to. The
            // asset keeps just the ones this mesh uses, as a COLLADA controller does, and the limit applies to those.
            var meshJoints = skin != null ? UsedJoints(skin, primitives) : null;

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
            result.Skinned = skin != null;

            // The unit cube the vertices are fitted into has to be undone before the skin sees them, so the
            // bind shape matrix carries the scale and offset (a flat axis has nothing to scale). Normals
            // are put through its inverse transpose when drawn, so they are stored scaled the other way.
            var fit = new Vector3(
                result.AssetScale.X == 0 ? 1f : result.AssetScale.X,
                result.AssetScale.Y == 0 ? 1f : result.AssetScale.Y,
                result.AssetScale.Z == 0 ? 1f : result.AssetScale.Z);
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
                        var normal = p.Normals != null ? p.Normals[index] : flat;
                        // Fitting the mesh into the unit cube stretches it unevenly, which the stored normals
                        // have to allow for. A skin's bind shape is exact about the scale, so it gets that one.
                        normal = skin != null ? SafeNormalize(normal * fit) : UnitCubeFit.FitNormal(normal, result.AssetScale);
                        var vertex = new Vertex
                        {
                            Position = normalized[index],
                            Normal = normal
                        };
                        if (p.TexCoords != null)
                        {
                            // glTF's UV origin is the top left, Second Life's is the bottom left
                            var uv = p.TexCoords[index];
                            vertex.TexCoord = new Vector2(uv.X, 1f - uv.Y);
                        }
                        if (skin != null)
                            face.AddVertex(vertex, p.Weights![index]);
                        else
                            face.AddVertex(vertex);
                    }
                }

                if (face.Vertices.Count > MaxFaceVertices)
                {
                    _diagnostics.Warn(ModelDiagnosticCode.FaceSkipped, $"Skipping a glTF primitive of mesh {meshIndex}: it has {face.Vertices.Count} " +
                                $"distinct vertices, and a face holds at most {MaxFaceVertices}", doc.Meshes[meshIndex].Name);
                    continue;
                }
                template.Faces.Add(face);
            }

            if (template.Faces.Count == 0) return null;

            if (skin != null)
            {
                var bind = Matrix4.CreateScale(fit) * Matrix4.CreateTranslation(result.AssetOffset);
                template.Skin = new ModelSkin
                {
                    JointNames = meshJoints!.Names,
                    InverseBindMatrices = meshJoints.InverseBind,
                    BindShapeMatrix = new[]
                    {
                        bind.M11, bind.M12, bind.M13, bind.M14, bind.M21, bind.M22, bind.M23, bind.M24,
                        bind.M31, bind.M32, bind.M33, bind.M34, bind.M41, bind.M42, bind.M43, bind.M44
                    }
                };
            }

            template.CreateAsset(UUID.Zero);
            return result;
        }

        private PrimitiveData ReadPrimitive(GltfDocument doc, GltfPrimitive primitive, SkinInfo? skin)
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
                data.Positions[i] = Vector3.Transform(positions[i], skin != null ? skin.MeshToSl : YUpToZUp);
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
                    data.Normals[i] = SafeNormalize(Vector3.TransformNormal(normals[i], skin != null ? skin.NormalToSl : YUpToZUp));
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
            if (skin != null) data.Weights = ReadWeights(doc, primitive, vertexCount, skin);
            return data;
        }

        #region Skins

        private sealed class SkinInfo
        {
            /// <summary>Avatar joint names, each once, in the order the weights index them</summary>
            public List<string> Names = new List<string>();

            /// <summary>For each joint of the glTF skin, its index in <see cref="Names"/>, or -1 if the avatar has no such joint</summary>
            public int[] Map = Array.Empty<int>();

            /// <summary>One inverse bind matrix per entry of <see cref="Names"/>: 16 floats, row-major, row vector, in Second Life's axes</summary>
            public float[] InverseBind = Array.Empty<float>();

            /// <summary>Takes the mesh's vertices to Second Life's axes, as the bind shape sees them</summary>
            public Matrix4 MeshToSl = Matrix4.Identity;

            /// <summary>Takes the mesh's normals to Second Life's axes (the inverse transpose of <see cref="MeshToSl"/>)</summary>
            public Matrix4 NormalToSl = Matrix4.Identity;
        }

        /// <returns>The skin, or null if none of its joints are joints of the avatar</returns>
        private SkinInfo? ReadSkin(GltfDocument doc, int index)
        {
            if (_skins.TryGetValue(index, out var cached)) return cached;
            if (index < 0 || index >= doc.Skins.Count)
                throw new InvalidDataException($"Node refers to missing glTF skin {index}");

            var skin = doc.Skins[index];
            int count = skin.Joints.Count;
            if (count == 0)
                throw new InvalidDataException("glTF skin has no joints");
            _vertexBudget -= count;
            if (_vertexBudget < 0)
                throw new LimitExceededException($"glTF model is larger than the {MaxVertices} vertices allowed");

            // Without matrices every joint's is the identity
            float[]? matrices = null;
            if (skin.InverseBindMatrices >= 0)
            {
                int available = CheckAccessor(doc, skin.InverseBindMatrices, GltfAccessorType.Mat4, "inverse bind matrix",
                    c => c == GltfComponentType.Float, false);
                if (available < count)
                    throw new InvalidDataException("glTF skin has fewer inverse bind matrices than joints");
                matrices = ReadMatrices(doc, skin.InverseBindMatrices, count);
            }

            var avatar = AvatarSkeletonIndex.Default;
            if (avatar == null)
                _diagnostics.Warn(ModelDiagnosticCode.SkeletonUnavailable, "The avatar skeleton is not available, so joint names are not checked");

            var jointNames = new List<string>(count);
            for (int j = 0; j < count; j++)
            {
                int node = skin.Joints[j];
                if (node < 0 || node >= doc.Nodes.Count)
                    throw new InvalidDataException($"glTF skin refers to missing joint node {node}");
                jointNames.Add(doc.Nodes[node].Name ?? string.Empty);
            }

            var selection = RigImport.SelectJoints(jointNames, avatar);
            var info = new SkinInfo { Map = selection.Map, Names = selection.Names };
            var firstOf = selection.FirstOf; // the glTF joint each avatar joint was taken from

            if (selection.Dropped.Count > 0)
                _diagnostics.Warn(ModelDiagnosticCode.JointsIgnored, $"Ignoring glTF joints that are not on the avatar skeleton: {string.Join(", ", selection.Dropped)}", string.Join(", ", selection.Dropped), selection.Dropped.Count);

            if (info.Names.Count == 0)
            {
                _diagnostics.Warn(ModelDiagnosticCode.RigDropped, "None of the joints of the glTF skin are on the avatar skeleton, so the mesh is loaded without a rig");
                _skins[index] = null;
                return null;
            }
            // glTF's matrices are column vector and Second Life's row vector, which for these 16 floats in file order
            // is no more than reading them as rows
            Func<int, NumMatrix> fileMatrix = j => matrices == null
                ? NumMatrix.Identity
                : new NumMatrix(
                    matrices[j * 16], matrices[j * 16 + 1], matrices[j * 16 + 2], matrices[j * 16 + 3],
                    matrices[j * 16 + 4], matrices[j * 16 + 5], matrices[j * 16 + 6], matrices[j * 16 + 7],
                    matrices[j * 16 + 8], matrices[j * 16 + 9], matrices[j * 16 + 10], matrices[j * 16 + 11],
                    matrices[j * 16 + 12], matrices[j * 16 + 13], matrices[j * 16 + 14], matrices[j * 16 + 15]);

            var result = new List<NumMatrix>();
            if (!UseAvatarJointAxes || !TryAlignToAvatar(doc, skin, firstOf, fileMatrix, info, result))
            {
                // As authored. The file's axes are Y-up where the vertices are turned into Z-up, so each matrix
                // is conjugated by the same turn
                info.MeshToSl = YUpToZUp;
                info.NormalToSl = YUpToZUp;
                result.Clear();
                foreach (var j in firstOf)
                {
                    result.Add(ToNumerics(ZUpToYUp * ToLibre(fileMatrix(j)) * YUpToZUp));
                }
            }

            info.InverseBind = RigImport.Flatten(result, "glTF skin");
            _skins[index] = info;
            return info;
        }

        private static NumMatrix ToNumerics(Matrix4 m) => new NumMatrix(
            m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

        private static Matrix4 ToLibre(NumMatrix m) => new Matrix4(
            m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

        /// <summary>The joints and inverse bind matrices of a skin that a mesh actually uses</summary>
        private sealed class MeshJoints
        {
            public List<string> Names = new List<string>();
            public float[] InverseBind = Array.Empty<float>();
        }

        /// <summary>
        /// Narrows <paramref name="skin"/> to the joints that carry weight on <paramref name="primitives"/>, in the skin's
        /// order, and renumbers the weights to match.
        /// </summary>
        /// <exception cref="LimitExceededException">The mesh uses more than <see cref="MaxJoints"/> joints</exception>
        private MeshJoints UsedJoints(SkinInfo skin, List<PrimitiveData> primitives)
        {
            var used = new bool[skin.Names.Count];
            foreach (var p in primitives)
            {
                foreach (var w in p.Weights!)
                {
                    if (w.Weight0 > 0f) used[w.Joint0] = true;
                    if (w.Weight1 > 0f) used[w.Joint1] = true;
                    if (w.Weight2 > 0f) used[w.Joint2] = true;
                    if (w.Weight3 > 0f) used[w.Joint3] = true;
                }
            }

            var remap = new int[used.Length];
            var result = new MeshJoints();
            var matrices = new List<float>();
            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i]) continue;
                remap[i] = result.Names.Count;
                result.Names.Add(skin.Names[i]);
                for (int k = 0; k < 16; k++) matrices.Add(skin.InverseBind[i * 16 + k]);
            }
            if (result.Names.Count > MaxJoints)
                throw new LimitExceededException($"glTF mesh is weighted to {result.Names.Count} joints, and a mesh may use at most {MaxJoints}");
            result.InverseBind = matrices.ToArray();

            foreach (var p in primitives)
            {
                for (int v = 0; v < p.Weights!.Length; v++)
                {
                    var w = p.Weights[v];
                    w.Joint0 = w.Weight0 > 0f ? remap[w.Joint0] : 0;
                    w.Joint1 = w.Weight1 > 0f ? remap[w.Joint1] : 0;
                    w.Joint2 = w.Weight2 > 0f ? remap[w.Joint2] : 0;
                    w.Joint3 = w.Weight3 > 0f ? remap[w.Joint3] : 0;
                    p.Weights[v] = w;
                }
            }
            return result;
        }

        /// <summary>
        /// Rebuilds the inverse bind matrices with the avatar's own joint axes: each joint keeps the position the file
        /// puts it at, and has the rotation and scale the avatar's joint has there. Everything the file's axes did to the mesh is moved into where the mesh
        /// itself sits, so the mesh looks the same at rest. See <see cref="UseAvatarJointAxes"/>.
        /// </summary>
        /// <returns>False if it cannot be done, and the matrices should be used as they are</returns>
        private bool TryAlignToAvatar(GltfDocument doc, GltfSkin skin, List<int> joints, Func<int, NumMatrix> fileMatrix,
            SkinInfo info, List<NumMatrix> result)
        {
            // M takes the mesh as it is in the file to where the file's scene puts it at rest
            var worlds = new List<NumMatrix>();
            var bound = new List<NumMatrix>();
            foreach (var j in joints)
            {
                var world = ToNumerics(NodeWorld(doc, skin.Joints[j]));
                worlds.Add(world);
                bound.Add(fileMatrix(j) * world);
            }

            if (!NumMatrix.Invert(bound[0], out var inverseReference))
            {
                _diagnostics.Warn(ModelDiagnosticCode.RigApproximated, "The glTF skin puts its mesh through a matrix that cannot be inverted, so its inverse bind matrices are used as authored");
                return false;
            }

            var toSl = ToNumerics(YUpToZUp);
            var fromSl = ToNumerics(ZUpToYUp);
            var meshToSl = bound[0] * toSl;
            if (!NumMatrix.Invert(meshToSl, out var inverseMeshToSl))
                return false;

            info.MeshToSl = ToLibre(meshToSl);
            info.NormalToSl = ToLibre(NumMatrix.Transpose(inverseMeshToSl));

            for (int i = 0; i < joints.Count; i++)
            {
                // The joint's position from the file, with the avatar's own axes and size at that joint
                var origin = NumVector3.Transform(new NumVector3(worlds[i].M41, worlds[i].M42, worlds[i].M43), toSl);
                var inverseAvatarWorld = RigImport.OnAvatarAxes(info.Names[i], NumMatrix.CreateTranslation(origin), out bool invertible);
                if (!invertible)
                    return false;

                result.Add(fromSl * inverseReference * bound[i] * toSl * inverseAvatarWorld);
            }
            return true;
        }

        /// <summary>A node's transform in the scene, from its own and its parents' (row vector, glTF's axes)</summary>
        private Matrix4 NodeWorld(GltfDocument doc, int node)
        {
            if (_nodeParents == null)
            {
                _nodeParents = new int[doc.Nodes.Count];
                for (int i = 0; i < _nodeParents.Length; i++) _nodeParents[i] = -1;
                for (int i = 0; i < doc.Nodes.Count; i++)
                {
                    foreach (var child in doc.Nodes[i].Children)
                    {
                        if (child >= 0 && child < _nodeParents.Length) _nodeParents[child] = i;
                    }
                }
            }

            var world = LocalTransform(doc.Nodes[node]);
            int steps = 0;
            for (int parent = _nodeParents[node]; parent >= 0; parent = _nodeParents[parent])
            {
                // A node cannot be its own ancestor
                if (++steps > doc.Nodes.Count)
                    throw new InvalidDataException("glTF nodes form a loop");
                world = world * LocalTransform(doc.Nodes[parent]);
            }
            return world;
        }

        /// <summary>Reads <paramref name="count"/> MAT4 elements whose accessor has been through <see cref="CheckAccessor"/></summary>
        private static float[] ReadMatrices(GltfDocument doc, int accessorIndex, int count)
        {
            var accessor = doc.Accessors[accessorIndex];
            var view = doc.BufferViews[accessor.BufferView];
            var buffer = doc.Buffers[view.Buffer].Data!;
            long stride = view.ByteStride > 0 ? view.ByteStride : accessor.DefaultStride;
            long start = (long)view.ByteOffset + accessor.ByteOffset;

            var result = new float[count * 16];
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < 16; k++)
                {
                    float value = BitConverter.ToSingle(buffer, (int)(start + i * stride + k * 4));
                    if (!Utils.IsFinite(value))
                        throw new InvalidDataException("glTF inverse bind matrix contains a value that is not a number");
                    result[i * 16 + k] = value;
                }
            }
            return result;
        }

        private VertexWeight[] ReadWeights(GltfDocument doc, GltfPrimitive primitive, int vertexCount, SkinInfo skin)
        {
            if (!primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_JOINTS_0, out var jointsAccessor) ||
                !primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_WEIGHTS_0, out var weightsAccessor))
                throw new InvalidDataException("glTF primitive of a skinned mesh has no JOINTS_0 and WEIGHTS_0");

            if (primitive.Attributes.ContainsKey("JOINTS_1"))
                _diagnostics.Warn(ModelDiagnosticCode.InfluencesIgnored, "Ignoring glTF influences beyond the first four of each vertex: Second Life takes four");

            int jointCount = CheckAccessor(doc, jointsAccessor, GltfAccessorType.Vec4, "JOINTS_0",
                c => c == GltfComponentType.UnsignedByte || c == GltfComponentType.UnsignedShort, false);
            int weightCount = CheckAccessor(doc, weightsAccessor, GltfAccessorType.Vec4, "WEIGHTS_0",
                c => c == GltfComponentType.Float, true);
            if (jointCount != vertexCount || weightCount != vertexCount)
                throw new InvalidDataException("glTF JOINTS_0, WEIGHTS_0 and POSITION counts differ");

            var joints = doc.GetJoints(primitive);
            var weights = doc.GetWeights(primitive);
            var result = new VertexWeight[vertexCount];
            var influences = new List<KeyValuePair<int, float>>(4);
            int unweighted = 0;

            for (int v = 0; v < vertexCount; v++)
            {
                influences.Clear();
                int[] j = { joints[v].j0, joints[v].j1, joints[v].j2, joints[v].j3 };
                float[] w = { weights[v].X, weights[v].Y, weights[v].Z, weights[v].W };

                for (int k = 0; k < 4; k++)
                {
                    if (!Utils.IsFinite(w[k]) || w[k] < 0f)
                        throw new InvalidDataException("glTF WEIGHTS_0 contains a value that is not a weight");
                    if (w[k] == 0f) continue;
                    if (j[k] >= skin.Map.Length)
                        throw new InvalidDataException("glTF JOINTS_0 refers to a joint the skin does not have");

                    int joint = skin.Map[j[k]];
                    if (joint < 0) continue;

                    influences.Add(new KeyValuePair<int, float>(joint, w[k]));
                }

                // Strongest first, and they add up to one. Nothing it is weighted to being a joint of the avatar
                // leaves a vertex that follows the first one.
                if (!RigImport.Reduce(influences, out var weight)) unweighted++;
                result[v] = weight;
            }

            if (unweighted > 0)
                _diagnostics.Warn(ModelDiagnosticCode.UnweightedVertices, $"{unweighted} vertices have no weight to a joint of the avatar and follow the first joint instead", null, unweighted);
            return result;
        }

        #endregion Skins

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
                throw new LimitExceededException($"glTF model is larger than the {MaxVertices} vertices allowed");
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
                    _diagnostics.Warn(ModelDiagnosticCode.TextureNotLoaded, $"glTF material {material.ID} refers to a missing texture", material.ID);
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
                        _diagnostics.Warn(ModelDiagnosticCode.TextureNotLoaded, $"Not loading texture for glTF material {material.ID}: {ex.Message}", material.ID);
                    }
                }
            }

            material.Pbr = BuildPbr(doc, source, material);

            materials[index] = material;
            return material;
        }

        #region Physically based materials

        /// <summary>A material's PBR textures, and which images of the file they come from</summary>
        private sealed class PbrSource
        {
            public ModelMaterial Material = null!;
            public ModelPbrMaterial Pbr = null!;
            public int BaseImage = -1, NormalImage = -1, MetallicRoughnessImage = -1, EmissiveImage = -1, OcclusionImage = -1;
            public int MetallicRoughnessTexture = -1, OcclusionTexture = -1;
        }

        private List<PbrSource> _pbrSources = new List<PbrSource>();

        private ModelPbrMaterial BuildPbr(GltfDocument doc, GltfDocumentMaterial source, ModelMaterial material)
        {
            var pbr = new ModelPbrMaterial
            {
                Name = material.ID,
                BaseColorFactor = source.BaseColorFactor,
                MetallicFactor = source.MetallicFactor,
                RoughnessFactor = source.RoughnessFactor,
                EmissiveFactor = source.EmissiveFactor,
                AlphaMode = source.AlphaMode,
                AlphaCutoff = source.AlphaCutoff,
                DoubleSided = source.DoubleSided,
                NormalScale = source.NormalTexture?.Scale ?? 1f,
                OcclusionStrength = source.OcclusionTexture?.Strength ?? 1f
            };

            var record = new PbrSource { Material = material, Pbr = pbr };
            pbr.BaseColorTexture = PbrTexture(doc, source.BaseColorTexture, material, "base color", out record.BaseImage, out _);
            pbr.NormalTexture = PbrTexture(doc, source.NormalTexture, material, "normal", out record.NormalImage, out _);
            pbr.MetallicRoughnessTexture = PbrTexture(doc, source.MetallicRoughnessTexture, material, "metallic-roughness",
                out record.MetallicRoughnessImage, out record.MetallicRoughnessTexture);
            pbr.EmissiveTexture = PbrTexture(doc, source.EmissiveTexture, material, "emissive", out record.EmissiveImage, out _);

            // Occlusion has no texture of its own in a Second Life material: it is packed into the red channel of
            // the metallic-roughness texture, so it is only an image to load from
            PbrTexture(doc, source.OcclusionTexture, material, "occlusion", out record.OcclusionImage, out record.OcclusionTexture);

            if (LoadPbrTextures)
            {
                // What the viewer does not use of the material, and a Second Life material cannot hold
                var loss = new List<string>();
                foreach (var texture in new GltfTextureRef?[] { source.BaseColorTexture, source.NormalTexture, source.MetallicRoughnessTexture,
                             source.EmissiveTexture, source.OcclusionTexture })
                {
                    if (texture != null && texture.Index >= 0 && texture.TexCoord != 0) loss.Add($"a texture uses coordinate set {texture.TexCoord}");
                }
                if (source.NormalTexture != null && source.NormalTexture.Index >= 0 && source.NormalTexture.Scale != 1f)
                    loss.Add("the normal map has a scale");
                if (source.OcclusionTexture != null && source.OcclusionTexture.Index >= 0 && source.OcclusionTexture.Strength != 1f)
                    loss.Add("occlusion has a strength");

                if (loss.Count > 0)
                {
                    _diagnostics.Warn(ModelDiagnosticCode.PbrApproximated,
                        $"glTF material {material.ID} has what a Second Life material cannot hold ({string.Join(", ", loss)})", material.ID, loss.Count);
                }
            }

            _pbrSources.Add(record);
            return pbr;
        }

        /// <summary>The texture of a material slot, or null if it has none or its image cannot be found</summary>
        private ModelTexture? PbrTexture(GltfDocument doc, GltfTextureRef? reference, ModelMaterial material, string slot,
            out int imageIndex, out int textureIndex)
        {
            imageIndex = -1;
            textureIndex = reference?.Index ?? -1;
            if (reference == null || reference.Index < 0) return null;

            if (reference.Index >= doc.Textures.Count || doc.Textures[reference.Index].Source < 0 ||
                doc.Textures[reference.Index].Source >= doc.Images.Count)
            {
                if (LoadPbrTextures)
                    _diagnostics.Warn(ModelDiagnosticCode.TextureNotLoaded, $"glTF material {material.ID} refers to a missing {slot} texture", material.ID);
                return null;
            }

            int index = doc.Textures[reference.Index].Source;
            var image = doc.Images[index];
            try
            {
                var texture = new ModelTexture
                {
                    Name = image.Uri != null && !image.Uri.StartsWith("data:", StringComparison.Ordinal)
                        ? DecodeUri(image.Uri)
                        : "gltf-image-" + index,
                    TexCoord = reference.TexCoord,
                    Transform = reference.Transform
                };
                imageIndex = index;
                return texture;
            }
            catch (InvalidDataException ex)
            {
                if (LoadPbrTextures)
                    _diagnostics.Warn(ModelDiagnosticCode.TextureNotLoaded, $"Not loading {slot} texture for glTF material {material.ID}: {ex.Message}", material.ID);
                return null;
            }
        }

        private void WarnAboutMaterialExtensions(GltfDocument doc)
        {
            var used = new List<string>();
            foreach (var name in doc.ExtensionsUsed)
            {
                if (name.StartsWith("KHR_materials_", StringComparison.Ordinal)) used.Add(name);
            }
            if (used.Count > 0)
            {
                _diagnostics.Warn(ModelDiagnosticCode.PbrApproximated,
                    $"The glTF file uses material extensions a Second Life material cannot hold: {string.Join(", ", used)}",
                    string.Join(", ", used), used.Count);
            }
        }

        #endregion Physically based materials

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

        /// <summary>Where an image of the file is: in a file beside it, or in bytes the file holds</summary>
        private struct ImageSource
        {
            public string? File;       // the path, when it is a file
            public string Relative;    // the name the file is given in the model
            public byte[]? Bytes;      // the contents, when they are in the model
            public string Extension;   // of the format, for bytes
        }

        /// <summary>
        /// Finds an image, refusing a file outside the model's directory or too big to read. This is where every
        /// texture the file names is vetted, whatever it is for.
        /// </summary>
        /// <returns>False if it was refused (and the reason is reported)</returns>
        private bool TryFindImage(GltfDocument doc, GltfImage image, out ImageSource source)
        {
            source = new ImageSource { Relative = string.Empty, Extension = string.Empty };
            if (image.Uri != null && !image.Uri.StartsWith("data:", StringComparison.Ordinal))
            {
                var relative = DecodeUri(image.Uri);
                var fname = Path.Combine(Path.GetDirectoryName(_fileName) ?? string.Empty, relative);

                if (RestrictTexturesToModelDirectory && !ModelTextureLoader.IsInModelDirectory(_fileName, fname))
                {
                    _diagnostics.Warn(ModelDiagnosticCode.TextureOutsideModelDirectory, $"Not loading texture {relative}: it is outside the model's directory " +
                                "(see GltfLoader.RestrictTexturesToModelDirectory)", relative);
                    return false;
                }

                CheckFileSize(fname);
                source.File = fname;
                source.Relative = relative;
                return true;
            }

            string extension = ExtensionForMimeType(image.MimeType);
            if (image.Uri != null)
            {
                source.Bytes = DecodeDataUri(image.Uri, out var mimeType);
                if (image.MimeType == null) extension = ExtensionForMimeType(mimeType);
            }
            else
            {
                source.Bytes = ReadBufferView(doc, image.BufferView);
            }
            source.Extension = extension;
            return true;
        }

        private void LoadImage(GltfDocument doc, GltfImage image, ModelMaterial material)
        {
            try
            {
                if (!TryFindImage(doc, image, out var source)) return;

                if (source.File != null)
                    ModelTextureLoader.LoadFile(_diagnostics, source.File, source.Relative, material, _textureCodec);
                else
                    ModelTextureLoader.LoadBytes(_diagnostics, source.Bytes!, source.Extension, material.Texture, material, _textureCodec);
            }
            catch (Exception ex)
            {
                _diagnostics.Warn(ModelDiagnosticCode.TextureNotLoaded, $"Failed loading glTF image {material.Texture}: {ex.Message}", material.Texture);
            }
        }

        /// <summary>Decodes an image of the file, or reports why not and returns null</summary>
        private ManagedImage? DecodeImage(GltfDocument doc, int imageIndex, string name)
        {
            try
            {
                if (!TryFindImage(doc, doc.Images[imageIndex], out var source)) return null;

                return source.File != null
                    ? ModelTextureLoader.DecodeFile(source.File, source.Relative, _textureCodec)
                    : ModelTextureLoader.DecodeBytes(source.Bytes!, source.Extension, name, _textureCodec);
            }
            catch (Exception ex)
            {
                _diagnostics.Warn(ModelDiagnosticCode.TextureNotLoaded, $"Failed loading glTF image {name}: {ex.Message}", name);
                return null;
            }
        }

        /// <summary>
        /// Prepares the textures of the materials as the viewer prepares a glTF material's when it imports one. Each
        /// image is decoded once however many slots and materials use it, and the same result is shared.
        /// </summary>
        private void LoadPbrImages(GltfDocument doc)
        {
            var decoded = new Dictionary<int, ManagedImage?>();
            var made = new Dictionary<string, ModelTexture?>(StringComparer.Ordinal);

            ManagedImage? Decoded(int imageIndex)
            {
                if (!decoded.TryGetValue(imageIndex, out var image))
                {
                    var name = doc.Images[imageIndex].Uri ?? "gltf-image-" + imageIndex;
                    image = DecodeImage(doc, imageIndex, name);
                    decoded[imageIndex] = image;
                }
                return image;
            }

            // A copy to change, without its alpha: the viewer drops it from all but the base color
            ManagedImage? Copy(int imageIndex)
            {
                var image = Decoded(imageIndex)?.Clone();
                if (image != null && (image.Channels & ManagedImage.ImageChannels.Alpha) != 0)
                    image.ConvertChannels(image.Channels & ~ManagedImage.ImageChannels.Alpha);
                return image;
            }

            ModelTexture? Make(string key, Func<ManagedImage?> prepare, bool lossless)
            {
                if (made.TryGetValue(key, out var done)) return done;

                ModelTexture? result = null;
                var image = prepare();
                if (image != null)
                {
                    try
                    {
                        ModelTextureLoader.Encode(image, lossless, out var data, out var width, out var height);
                        result = new ModelTexture { Data = data, Width = width, Height = height };
                    }
                    catch (Exception ex)
                    {
                        _diagnostics.Warn(ModelDiagnosticCode.TextureNotLoaded, $"Failed encoding a glTF material texture: {ex.Message}", key);
                    }
                }
                made[key] = result;
                return result;
            }

            void Fill(ModelTexture? slot, ModelTexture? from)
            {
                if (slot == null || from == null) return;
                slot.Data = from.Data;
                slot.Width = from.Width;
                slot.Height = from.Height;
            }

            foreach (var record in _pbrSources)
            {
                var pbr = record.Pbr;

                // The base color is the one the mesh uses, so it is not made twice
                if (pbr.BaseColorTexture != null && record.Material.TextureData.Length > 0)
                {
                    pbr.BaseColorTexture.Name = record.Material.Texture;
                    pbr.BaseColorTexture.Data = record.Material.TextureData;
                    pbr.BaseColorTexture.Width = record.Material.Width;
                    pbr.BaseColorTexture.Height = record.Material.Height;
                }

                if (record.NormalImage >= 0)
                    Fill(pbr.NormalTexture, Make("normal:" + record.NormalImage, () => Copy(record.NormalImage), lossless: true));
                if (record.EmissiveImage >= 0)
                    Fill(pbr.EmissiveTexture, Make("emissive:" + record.EmissiveImage, () => Copy(record.EmissiveImage), lossless: false));

                // Occlusion shares the metallic-roughness texture, in its red channel
                if (record.MetallicRoughnessImage >= 0)
                {
                    int mr = record.MetallicRoughnessImage;
                    int occlusion = record.OcclusionImage;
                    bool distinct = record.OcclusionTexture >= 0 && record.OcclusionTexture != record.MetallicRoughnessTexture && occlusion >= 0;
                    bool none = record.OcclusionTexture < 0;
                    string key = "mr:" + mr + (distinct ? ":occlusion:" + occlusion : none ? ":none" : ":same");

                    Fill(pbr.MetallicRoughnessTexture, Make(key, () =>
                    {
                        var image = Copy(mr);
                        if (image == null) return null;
                        if (distinct)
                        {
                            // Scaled to the size of the metallic-roughness map, with its red channel taken
                            var map = Copy(occlusion);
                            if (map != null)
                            {
                                map.ResizeBilinear(image.Width, image.Height);
                                image.Red = map.Red;
                            }
                        }
                        else if (none)
                        {
                            // Nothing occludes: red has to be all white or the surface comes out black
                            for (int i = 0; i < image.Red.Length; i++) image.Red[i] = 255;
                        }
                        return image;
                    }, lossless: false));
                }
                else if (record.OcclusionImage >= 0)
                {
                    int occlusion = record.OcclusionImage;
                    pbr.MetallicRoughnessTexture = new ModelTexture { Name = "gltf-image-" + occlusion };
                    Fill(pbr.MetallicRoughnessTexture, Make("mr:none:occlusion:" + occlusion, () =>
                    {
                        // No metallic-roughness map but an occlusion map: a white one, with the occlusion in red
                        var map = Copy(occlusion);
                        if (map == null) return null;
                        var image = new ManagedImage(map.Width, map.Height, ManagedImage.ImageChannels.Color);
                        for (int i = 0; i < image.Red.Length; i++)
                        {
                            image.Red[i] = map.Red[i];
                            image.Green[i] = 255;
                            image.Blue[i] = 255;
                        }
                        return image;
                    }, lossless: false));
                }
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
                throw new LimitExceededException($"{fname} is larger than {MaxFileSize} bytes");
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
