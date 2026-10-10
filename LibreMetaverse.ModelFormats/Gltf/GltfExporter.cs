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
using System.Linq;
using LibreMetaverse.Assets;
using LibreMetaverse.Assets.Gltf;
using LibreMetaverse.Rendering;
using NumMatrix = System.Numerics.Matrix4x4;
using NumVector3 = System.Numerics.Vector3;
using Path = System.IO.Path;

namespace LibreMetaverse.ImportExport
{
    /// <summary>An encoded image (PNG or JPEG) to embed in an exported model</summary>
    public sealed class ExportImage
    {
        /// <summary>The compressed image file</summary>
        public byte[] Data { get; }

        /// <summary><c>image/png</c> or <c>image/jpeg</c>, the only formats glTF allows</summary>
        public string MimeType { get; }

        public ExportImage(byte[] data, string mimeType)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
        }
    }

    /// <summary>
    /// Writes decoded Second Life meshes and prims (<see cref="FacetedMesh"/>) out as glTF 2.0, as a
    /// <c>.glb</c> or a self-contained <c>.gltf</c>.
    /// </summary>
    /// <remarks>
    /// The scene is Y-up, as glTF requires: a single top node turns Second Life's Z-up into it. A linkset is
    /// laid out the way the grid lays it out: a node for the root prim's rotation, holding the root's mesh
    /// (scaled by the root's size) and, beside it, each child prim at its own position, rotation and size.
    /// The root prim's world position is dropped, so an export sits at the origin.
    /// A rigged mesh is exported as a glTF skin: its vertices are put through the bind shape matrix, the
    /// joints become nodes (arranged as the default avatar skeleton, each at the rest position its own inverse
    /// bind matrix implies; meshes that disagree about that get joints of their own) and every vertex keeps its weights. It sits under the top node rather than in a
    /// linkset, because glTF ignores the transform of a skinned mesh, and Second Life draws rigged meshes in
    /// avatar space too. The pelvis offset, alternate inverse bind matrices and the lock-scale flag cannot be
    /// expressed in glTF and are dropped.
    /// Only each face's color, opacity and texture are exported. Texture repeats, offsets and rotation,
    /// glow, shininess, bump maps, full bright and the other prim parameters are not. Colors are written as they
    /// are, with no conversion between Second Life's tints and glTF's linear <c>baseColorFactor</c>.
    /// </remarks>
    public sealed class GltfExporter
    {
        private readonly List<FacetedMesh> _meshes = new List<FacetedMesh>();

        /// <summary>
        /// Supplies the encoded image for a face's texture, or null to leave that face untextured. Textures
        /// are JPEG2000 on the grid and glTF cannot hold those, so the caller decodes and re-encodes them.
        /// Called at most once per texture. Without a provider no textures are exported.
        /// </summary>
        public Func<UUID, ExportImage?>? ImageProvider { get; set; }

        /// <summary>Written to the file's <c>asset.generator</c></summary>
        public string Generator { get; set; } = "LibreMetaverse.ModelFormats";

        /// <summary>Number of meshes added so far</summary>
        public int Count => _meshes.Count;

        /// <summary>
        /// Adds a mesh or prim. The prim a mesh belongs to says where it goes: a mesh whose prim has a
        /// parent is placed in the linkset of the root prim with that local ID if that was added too, and
        /// otherwise it is exported on its own, with a warning.
        /// </summary>
        public void Add(FacetedMesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            _meshes.Add(mesh);
        }

        /// <summary>Builds the glTF document</summary>
        public GltfDocument Build()
        {
            return new Builder(this).Run();
        }

        /// <summary>Builds the model as a binary <c>.glb</c></summary>
        public byte[] ToGlb()
        {
            return Build().ToGlb();
        }

        /// <summary>
        /// Builds the model and writes it to a file, as a binary glTF for <c>.glb</c> and as JSON with its
        /// data embedded for <c>.gltf</c>
        /// </summary>
        /// <exception cref="ArgumentException">The file name has any other extension</exception>
        public void Save(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".glb")
                File.WriteAllBytes(path, ToGlb());
            else if (extension == ".gltf")
                File.WriteAllText(path, Build().ToJson());
            else
                throw new ArgumentException("The file name must end in .glb or .gltf", nameof(path));
        }

        private sealed class Builder
        {
            private readonly GltfExporter _owner;
            private readonly GltfDocument _doc = new GltfDocument();
            private readonly MemoryStream _buffer = new MemoryStream();

            private readonly Dictionary<UUID, int> _textures = new Dictionary<UUID, int>();
            private readonly Dictionary<MaterialKey, int> _materials = new Dictionary<MaterialKey, int>();

            private struct MaterialKey : IEquatable<MaterialKey>
            {
                public Color4 Color;
                public int Texture;

                public bool Equals(MaterialKey other) => Color.Equals(other.Color) && Texture == other.Texture;
                public override bool Equals(object? obj) => obj is MaterialKey other && Equals(other);
                public override int GetHashCode() => Color.GetHashCode() * 31 + Texture;
            }

            public Builder(GltfExporter owner)
            {
                _owner = owner;
            }

            public GltfDocument Run()
            {
                _doc.Generator = _owner.Generator;

                // The one node everything hangs from: Second Life is Z-up and glTF is Y-up. Node matrices
                // are column vector, so the row vector matrix is flipped.
                int axes = AddNode(new GltfNode
                {
                    Name = "SecondLife",
                    Matrix = Matrix4.Transpose(ModelAxes.ZUpToYUp)
                });

                AddLinksets(axes);
                AddRiggedMeshes(axes);

                var scene = new GltfScene { Name = "Scene" };
                scene.Nodes.Add(axes);
                _doc.Scenes.Add(scene);
                _doc.DefaultScene = 0;

                if (_buffer.Length > 0)
                {
                    _doc.Buffers.Add(new GltfBuffer { Data = _buffer.ToArray(), ByteLength = (int)_buffer.Length });
                }
                return _doc;
            }

            private int AddNode(GltfNode node)
            {
                _doc.Nodes.Add(node);
                return _doc.Nodes.Count - 1;
            }

            #region Scene

            private void AddLinksets(int axes)
            {
                var roots = new List<FacetedMesh>();
                var children = new Dictionary<uint, List<FacetedMesh>>();
                var known = new HashSet<uint>();

                foreach (var mesh in _owner._meshes)
                {
                    if (!IsRigged(mesh) && mesh.Prim.ParentID == 0) known.Add(mesh.Prim.LocalID);
                }
                foreach (var mesh in _owner._meshes)
                {
                    if (IsRigged(mesh)) continue;

                    uint parent = mesh.Prim.ParentID;
                    if (parent == 0)
                    {
                        roots.Add(mesh);
                    }
                    else if (known.Contains(parent))
                    {
                        if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<FacetedMesh>();
                        list.Add(mesh);
                    }
                    else
                    {
                        Logger.Warn($"Exporting prim {mesh.Prim.LocalID} on its own: its parent {parent} was not added, or is a rigged mesh");
                        roots.Add(mesh);
                    }
                }

                foreach (var root in roots)
                {
                    AddLinkset(axes, root, children.TryGetValue(root.Prim.LocalID, out var kids) ? kids : null);
                }
            }

            private void AddLinkset(int axes, FacetedMesh root, List<FacetedMesh>? children)
            {
                // The group node carries the root prim's rotation. The root's own mesh node under it carries
                // the root's size, and the children are its siblings, so they are not scaled by it.
                var group = new GltfNode { Name = NameOf(root) + " linkset", Rotation = root.Prim.Rotation };
                int groupIndex = AddNode(group);
                _doc.Nodes[axes].Children.Add(groupIndex);

                AddMeshNode(group, root, root.Prim.Scale, Vector3.Zero, Quaternion.Identity);

                if (children == null) return;
                foreach (var child in children)
                {
                    AddMeshNode(group, child, child.Prim.Scale, child.Prim.Position, child.Prim.Rotation);
                }
            }

            private void AddMeshNode(GltfNode parent, FacetedMesh mesh, Vector3 scale, Vector3 position, Quaternion rotation)
            {
                int meshIndex = AddMesh(mesh);
                if (meshIndex < 0) return;

                parent.Children.Add(AddNode(new GltfNode
                {
                    Name = NameOf(mesh),
                    Mesh = meshIndex,
                    Scale = scale,
                    Translation = position,
                    Rotation = rotation
                }));
            }

            private static string NameOf(FacetedMesh mesh)
            {
                var name = mesh.Prim.Properties?.Name;
                return !string.IsNullOrEmpty(name) ? name! : "prim" + mesh.Prim.LocalID;
            }

            #endregion Scene

            #region Geometry

            private int AddMesh(FacetedMesh source, NumMatrix? bindShape = null)
            {
                var mesh = new GltfMesh { Name = NameOf(source) };

                for (int i = 0; i < source.Faces.Count; i++)
                {
                    var primitive = AddPrimitive(source, source.Faces[i], i, bindShape);
                    if (primitive != null) mesh.Primitives.Add(primitive);
                }

                if (mesh.Primitives.Count == 0)
                {
                    Logger.Warn($"Not exporting {mesh.Name}: it has no triangles");
                    return -1;
                }

                _doc.Meshes.Add(mesh);
                return _doc.Meshes.Count - 1;
            }

            private GltfPrimitive? AddPrimitive(FacetedMesh source, Face face, int faceIndex, NumMatrix? bindShape)
            {
                if (face.Vertices == null || face.Indices == null) return null;
                int vertexCount = face.Vertices.Count;
                int triangleCount = face.Indices.Count / 3;
                if (vertexCount == 0 || triangleCount == 0) return null;

                foreach (var index in face.Indices)
                {
                    if (index >= vertexCount)
                    {
                        Logger.Warn($"Not exporting face {faceIndex} of {NameOf(source)}: a triangle refers to a missing vertex");
                        return null;
                    }
                }

                var positions = new float[vertexCount * 3];
                var normals = new float[vertexCount * 3];
                var texCoords = new float[vertexCount * 2];
                var min = new double[] { double.MaxValue, double.MaxValue, double.MaxValue };
                var max = new double[] { double.MinValue, double.MinValue, double.MinValue };
                bool allNormals = true;

                for (int v = 0; v < vertexCount; v++)
                {
                    var vertex = face.Vertices[v];
                    if (!vertex.Position.IsFinite())
                    {
                        Logger.Warn($"Not exporting face {faceIndex} of {NameOf(source)}: a vertex position is not a number");
                        return null;
                    }

                    var position = vertex.Position;
                    var vertexNormal = vertex.Normal;
                    if (bindShape.HasValue)
                    {
                        // Rigged vertices are in the mesh's own space until the bind shape matrix, a row vector
                        // matrix, takes them to the space the inverse bind matrices expect
                        var q = NumVector3.Transform(new NumVector3(position.X, position.Y, position.Z), bindShape.Value);
                        position = new Vector3(q.X, q.Y, q.Z);
                        var n = NumVector3.TransformNormal(new NumVector3(vertexNormal.X, vertexNormal.Y, vertexNormal.Z), bindShape.Value);
                        vertexNormal = new Vector3(n.X, n.Y, n.Z);
                        if (!position.IsFinite())
                        {
                            Logger.Warn($"Not exporting face {faceIndex} of {NameOf(source)}: its bind shape matrix gives a position that is not a number");
                            return null;
                        }
                    }

                    float[] p = { position.X, position.Y, position.Z };
                    for (int c = 0; c < 3; c++)
                    {
                        positions[v * 3 + c] = p[c];
                        min[c] = Math.Min(min[c], p[c]);
                        max[c] = Math.Max(max[c], p[c]);
                    }

                    // Decoded normals are quantized, so they are not quite unit length. A mesh asset with no
                    // normal block decodes to all zeroes, and then there is nothing to write.
                    var normal = vertexNormal;
                    float length = normal.Length();
                    if (!(length > 1e-6f) || float.IsInfinity(length))
                    {
                        allNormals = false;
                    }
                    else
                    {
                        normals[v * 3] = normal.X / length;
                        normals[v * 3 + 1] = normal.Y / length;
                        normals[v * 3 + 2] = normal.Z / length;
                    }

                    var uv = vertex.TexCoord;
                    if (Utils.IsFinite(uv.X) && Utils.IsFinite(uv.Y))
                    {
                        // Second Life's UV origin is the bottom left, glTF's is the top left
                        texCoords[v * 2] = uv.X;
                        texCoords[v * 2 + 1] = 1f - uv.Y;
                    }
                }

                var primitive = new GltfPrimitive();
                primitive.Attributes[GltfPrimitive.ATTR_POSITION] =
                    AddAccessor(positions, GltfAccessorType.Vec3, vertexCount, min, max);
                if (allNormals)
                {
                    primitive.Attributes[GltfPrimitive.ATTR_NORMAL] = AddAccessor(normals, GltfAccessorType.Vec3, vertexCount);
                }
                primitive.Attributes[GltfPrimitive.ATTR_TEXCOORD_0] = AddAccessor(texCoords, GltfAccessorType.Vec2, vertexCount);
                primitive.Indices = AddIndexAccessor(face.Indices, triangleCount * 3);
                if (bindShape.HasValue)
                {
                    AddSkinAttributes(primitive, face, vertexCount, source.SkinData!.JointNames.Length);
                }
                primitive.Material = GetMaterial(face);
                return primitive;
            }

            private int AddAccessor(float[] data, GltfAccessorType type, int count, double[]? min = null, double[]? max = null, int target = 34962)
            {
                var bytes = new byte[data.Length * sizeof(float)];
                using (var ms = new MemoryStream(bytes))
                using (var writer = new BinaryWriter(ms))
                {
                    foreach (var value in data) writer.Write(value);
                }

                _doc.Accessors.Add(new GltfAccessor
                {
                    BufferView = AddView(bytes, target),
                    ComponentType = GltfComponentType.Float,
                    Type = type,
                    Count = count,
                    Min = min,
                    Max = max
                });
                return _doc.Accessors.Count - 1;
            }

            private int AddIndexAccessor(List<ushort> indices, int count)
            {
                var bytes = new byte[count * sizeof(ushort)];
                using (var ms = new MemoryStream(bytes))
                using (var writer = new BinaryWriter(ms))
                {
                    for (int i = 0; i < count; i++) writer.Write(indices[i]);
                }

                _doc.Accessors.Add(new GltfAccessor
                {
                    BufferView = AddView(bytes, 34963),
                    ComponentType = GltfComponentType.UnsignedShort,
                    Type = GltfAccessorType.Scalar,
                    Count = count
                });
                return _doc.Accessors.Count - 1;
            }

            private int AddView(byte[] data, int target)
            {
                // Everything in a glTF buffer view starts on a four byte boundary
                while (_buffer.Length % 4 != 0) _buffer.WriteByte(0);

                _doc.BufferViews.Add(new GltfBufferView
                {
                    Buffer = 0,
                    ByteOffset = (int)_buffer.Length,
                    ByteLength = data.Length,
                    Target = target
                });
                _buffer.Write(data, 0, data.Length);
                return _doc.BufferViews.Count - 1;
            }

            #endregion Geometry

            #region Rig

            private LindenSkeleton? _skeleton;
            private bool _skeletonLoaded;
            private readonly Dictionary<string, string> _canonicalNames = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _parentOf = new Dictionary<string, string>(StringComparer.Ordinal);
            private Dictionary<string, NumMatrix> _defaultWorld = new Dictionary<string, NumMatrix>(StringComparer.Ordinal);
            private string? _skeletonRoot;

            /// <summary>
            /// A set of joint nodes that rigged meshes share. A joint rests where a mesh's inverse bind matrix
            /// says (row vector, in avatar space), so meshes can only share a joint if they agree on that:
            /// otherwise the one that disagrees would be pulled out of shape at rest. Those get a set of their own.
            /// </summary>
            private sealed class RigSet
            {
                // Joints some mesh skins to, and where they rest. Anything else is only on the way down from the root.
                public readonly Dictionary<string, NumMatrix> Pinned = new Dictionary<string, NumMatrix>(StringComparer.Ordinal);
                public readonly Dictionary<string, NumMatrix> Rest = new Dictionary<string, NumMatrix>(StringComparer.Ordinal);
                public readonly Dictionary<string, int> Nodes = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            private const float RestTolerance = 1e-3f;

            private static bool IsRigged(FacetedMesh mesh)
            {
                return mesh.SkinData != null && mesh.SkinData.JointNames != null && mesh.SkinData.JointNames.Length > 0;
            }

            private static bool Agree(NumMatrix a, NumMatrix b)
            {
                return Math.Abs(a.M11 - b.M11) < RestTolerance && Math.Abs(a.M12 - b.M12) < RestTolerance &&
                       Math.Abs(a.M13 - b.M13) < RestTolerance && Math.Abs(a.M21 - b.M21) < RestTolerance &&
                       Math.Abs(a.M22 - b.M22) < RestTolerance && Math.Abs(a.M23 - b.M23) < RestTolerance &&
                       Math.Abs(a.M31 - b.M31) < RestTolerance && Math.Abs(a.M32 - b.M32) < RestTolerance &&
                       Math.Abs(a.M33 - b.M33) < RestTolerance && Math.Abs(a.M41 - b.M41) < RestTolerance &&
                       Math.Abs(a.M42 - b.M42) < RestTolerance && Math.Abs(a.M43 - b.M43) < RestTolerance;
            }

            private void AddRiggedMeshes(int axes)
            {
                var rigged = _owner._meshes.Where(IsRigged).ToList();
                if (rigged.Count == 0) return;

                LoadSkeleton();

                // First decide which joints each mesh can share, and where every one of them rests, before any
                // node is made: a joint that one mesh skins to and another only passes through has to be
                // placed by the mesh that skins to it.
                var sets = new List<RigSet>();
                var setOf = new Dictionary<FacetedMesh, RigSet>();
                foreach (var mesh in rigged)
                {
                    var skin = mesh.SkinData!;
                    var inverseBind = RiggedSkinMath.BuildInvBindMatrices(skin);
                    var worlds = new Dictionary<string, NumMatrix>(StringComparer.Ordinal);
                    for (int i = 0; i < inverseBind.Length; i++)
                    {
                        string name = Canonical(skin.JointNames[i]);
                        if (worlds.ContainsKey(name)) continue;

                        if (NumMatrix.Invert(inverseBind[i], out var world))
                            worlds[name] = world;
                        else
                            Logger.Warn($"Joint {name} of {NameOf(mesh)} has an inverse bind matrix that cannot be inverted");
                    }

                    var set = sets.FirstOrDefault(candidate =>
                        worlds.All(w => !candidate.Pinned.TryGetValue(w.Key, out var pinned) || Agree(pinned, w.Value)));
                    if (set == null)
                    {
                        set = new RigSet();
                        sets.Add(set);
                    }
                    foreach (var w in worlds)
                    {
                        if (!set.Pinned.ContainsKey(w.Key)) set.Pinned[w.Key] = w.Value;
                    }
                    setOf[mesh] = set;
                }
                if (sets.Count > 1)
                    Logger.Warn($"The rigged meshes disagree about where joints rest, so they are exported with {sets.Count} sets of joints");

                foreach (var mesh in rigged)
                {
                    var skin = mesh.SkinData!;
                    var set = setOf[mesh];
                    int meshIndex = AddMesh(mesh, RiggedSkinMath.FloatsToMatrix(skin.BindShapeMatrix));
                    if (meshIndex < 0) continue;

                    var gltfSkin = new GltfSkin { Name = NameOf(mesh) };
                    var seen = new HashSet<int>();
                    foreach (var jointName in skin.JointNames)
                    {
                        int node = GetJointNode(set, axes, Canonical(jointName));
                        if (!seen.Add(node))
                            Logger.Warn($"{NameOf(mesh)} lists joint {jointName} more than once");
                        gltfSkin.Joints.Add(node);
                    }

                    // The skeleton node has to be an ancestor of every joint, or the file is invalid
                    if (_skeletonRoot != null && set.Nodes.TryGetValue(_skeletonRoot, out var rootNode) &&
                        gltfSkin.Joints.All(joint => IsSameOrDescendant(rootNode, joint)))
                    {
                        gltfSkin.Skeleton = rootNode;
                    }

                    // The inverse bind matrices are row vector matrices, so their row major floats are exactly
                    // the column major floats of the column vector matrices glTF wants
                    var inverseBind = RiggedSkinMath.BuildInvBindMatrices(skin);
                    var matrixData = new float[inverseBind.Length * 16];
                    for (int i = 0; i < inverseBind.Length; i++)
                    {
                        var m = inverseBind[i];
                        float[] values =
                        {
                            m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
                            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44
                        };
                        Array.Copy(values, 0, matrixData, i * 16, 16);
                    }
                    // Not vertex data, so the buffer view has no GPU target
                    gltfSkin.InverseBindMatrices = AddAccessor(matrixData, GltfAccessorType.Mat4, inverseBind.Length, target: -1);

                    _doc.Skins.Add(gltfSkin);
                    _doc.Nodes[axes].Children.Add(AddNode(new GltfNode
                    {
                        Name = NameOf(mesh),
                        Mesh = meshIndex,
                        Skin = _doc.Skins.Count - 1
                    }));
                }
            }

            private bool IsSameOrDescendant(int ancestor, int node)
            {
                var parent = new Dictionary<int, int>();
                for (int i = 0; i < _doc.Nodes.Count; i++)
                {
                    foreach (var child in _doc.Nodes[i].Children) parent[child] = i;
                }

                for (int current = node; ; )
                {
                    if (current == ancestor) return true;
                    if (!parent.TryGetValue(current, out current)) return false;
                }
            }

            private void LoadSkeleton()
            {
                if (_skeletonLoaded) return;
                _skeletonLoaded = true;

                try
                {
                    _skeleton = LindenSkeleton.Load();
                    _skeletonRoot = _skeleton.bone.name;
                    _defaultWorld = AvatarBoneMath.BuildBoneWorldMatrices(_skeleton, new Dictionary<string, BoneTransform>());
                    IndexJoint(_skeleton.bone, null);
                }
                catch (Exception ex)
                {
                    // Without the skeleton the joints are just laid out flat, each at its own rest position
                    Logger.Warn($"Not arranging joints as the avatar skeleton: {ex.Message}");
                    _skeleton = null;
                    _skeletonRoot = null;
                    _canonicalNames.Clear();
                    _parentOf.Clear();
                }
            }

            private void IndexJoint(Joint joint, string? parent)
            {
                if (string.IsNullOrEmpty(joint.name)) return;

                _canonicalNames[joint.name] = joint.name;
                foreach (var alias in joint.GetAliasesList())
                {
                    if (!_canonicalNames.ContainsKey(alias)) _canonicalNames[alias] = joint.name;
                }
                if (parent != null) _parentOf[joint.name] = parent;

                // Collision volumes are joints too, as far as fitted mesh is concerned
                foreach (var volume in joint.collision_volume ?? Array.Empty<CollisionVolume>())
                {
                    if (volume == null || string.IsNullOrEmpty(volume.name)) continue;
                    _canonicalNames[volume.name] = volume.name;
                    _parentOf[volume.name] = joint.name;
                }
                foreach (var child in joint.bone ?? Array.Empty<Joint>())
                {
                    IndexJoint(child, joint.name);
                }
            }

            private string Canonical(string name)
            {
                return _canonicalNames.TryGetValue(name, out var canonical) ? canonical : name;
            }

            /// <summary>The node for a joint in a set, made (with the joints above it) if need be</summary>
            private int GetJointNode(RigSet set, int axes, string name)
            {
                if (set.Nodes.TryGetValue(name, out var existing)) return existing;

                int parentNode = axes;
                var parentWorld = NumMatrix.Identity;
                if (_parentOf.TryGetValue(name, out var parent))
                {
                    parentNode = GetJointNode(set, axes, parent);
                    parentWorld = set.Rest[parent];
                }

                if (!set.Pinned.TryGetValue(name, out var world))
                {
                    // Not skinned to by any mesh: it only has to be somewhere sensible
                    world = _defaultWorld.TryGetValue(name, out var fallback) ? fallback : parentWorld;
                }
                set.Rest[name] = world;

                // Rest world = local * parent world, with row vectors
                NumMatrix local = world;
                if (NumMatrix.Invert(parentWorld, out var inverseParent))
                    local = world * inverseParent;
                else
                    Logger.Warn($"Joint {name} has a parent that cannot be inverted; it is placed relative to the origin");

                if (!NumMatrix.Decompose(local, out _, out _, out _))
                    Logger.Warn($"The rest transform of joint {name} cannot be split into translation, rotation and scale; some programs will not read it");

                int index = AddNode(new GltfNode { Name = name, Matrix = ToNodeMatrix(local) });
                _doc.Nodes[parentNode].Children.Add(index);
                set.Nodes[name] = index;
                return index;
            }

            /// <summary>A row vector matrix as the column vector layout glTF nodes use</summary>
            private static Matrix4 ToNodeMatrix(NumMatrix m)
            {
                return new Matrix4(
                    m.M11, m.M21, m.M31, m.M41,
                    m.M12, m.M22, m.M32, m.M42,
                    m.M13, m.M23, m.M33, m.M43,
                    m.M14, m.M24, m.M34, m.M44);
            }

            private void AddSkinAttributes(GltfPrimitive primitive, Face face, int vertexCount, int jointCount)
            {
                var joints = new byte[vertexCount * 4 * sizeof(ushort)];
                var weights = new float[vertexCount * 4];

                using (var ms = new MemoryStream(joints))
                using (var writer = new BinaryWriter(ms))
                {
                    for (int v = 0; v < vertexCount; v++)
                    {
                        // A vertex the asset has no weights for follows the first joint
                        var w = face.Weights != null && v < face.Weights.Count
                            ? face.Weights[v]
                            : new VertexWeight { Weight0 = 1f };

                        int j0 = w.Joint0, j1 = w.Joint1, j2 = w.Joint2, j3 = w.Joint3;
                        float w0 = w.Weight0, w1 = w.Weight1, w2 = w.Weight2, w3 = w.Weight3;
                        RiggedSkinMath.NormalizeSkinWeights(jointCount, ref j0, ref w0, ref j1, ref w1, ref j2, ref w2, ref j3, ref w3);

                        writer.Write((ushort)j0); writer.Write((ushort)j1); writer.Write((ushort)j2); writer.Write((ushort)j3);
                        weights[v * 4] = w0; weights[v * 4 + 1] = w1; weights[v * 4 + 2] = w2; weights[v * 4 + 3] = w3;
                    }
                }

                _doc.Accessors.Add(new GltfAccessor
                {
                    BufferView = AddView(joints, 34962),
                    ComponentType = GltfComponentType.UnsignedShort,
                    Type = GltfAccessorType.Vec4,
                    Count = vertexCount
                });
                primitive.Attributes[GltfPrimitive.ATTR_JOINTS_0] = _doc.Accessors.Count - 1;
                primitive.Attributes[GltfPrimitive.ATTR_WEIGHTS_0] = AddAccessor(weights, GltfAccessorType.Vec4, vertexCount);
            }

            #endregion Rig

            #region Materials

            private int GetMaterial(Face face)
            {
                var entry = face.TextureFace;
                var color = entry != null ? entry.RGBA : Color4.White;
                int texture = entry != null ? GetTexture(entry.TextureID) : -1;

                var key = new MaterialKey { Color = color, Texture = texture };
                if (_materials.TryGetValue(key, out var existing)) return existing;

                var material = new GltfDocumentMaterial
                {
                    Name = "material" + _doc.Materials.Count,
                    BaseColorFactor = color,
                    // The defaults of 1 and 1 are shiny dark metal
                    MetallicFactor = 0f,
                    RoughnessFactor = 1f,
                    AlphaMode = color.A < 0.999f ? GltfAlphaMode.Blend : GltfAlphaMode.Opaque
                };
                if (texture >= 0) material.BaseColorTexture = new GltfTextureRef { Index = texture };

                _doc.Materials.Add(material);
                int index = _doc.Materials.Count - 1;
                _materials[key] = index;
                return index;
            }

            private int GetTexture(UUID id)
            {
                if (id == UUID.Zero || _owner.ImageProvider == null) return -1;
                if (_textures.TryGetValue(id, out var existing)) return existing;

                int result = -1;
                try
                {
                    var image = _owner.ImageProvider(id);
                    if (image != null && image.Data.Length > 0)
                    {
                        _doc.Images.Add(new GltfImage
                        {
                            Name = id.ToString(),
                            MimeType = image.MimeType,
                            BufferView = AddView(image.Data, -1)
                        });
                        _doc.Textures.Add(new GltfTexture { Source = _doc.Images.Count - 1 });
                        result = _doc.Textures.Count - 1;
                    }
                }
                catch (Exception ex)
                {
                    // A texture that cannot be had costs the face its texture, not the export
                    Logger.Warn($"Not exporting texture {id}: {ex.Message}");
                }

                _textures[id] = result;
                return result;
            }

            #endregion Materials
        }
    }
}
