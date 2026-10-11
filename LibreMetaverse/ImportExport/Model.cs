/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using System.Threading;
using LibreMetaverse.Assets;
using LibreMetaverse.Rendering;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.ImportExport
{

    public class ModelMaterial
    {
        public string ID = string.Empty;
        public Color4 DiffuseColor = Color4.White;
        public string Texture = string.Empty;
        public byte[] TextureData = Array.Empty<byte>();

        /// <summary>Decoded pixel dimensions of <see cref="Texture"/>, captured by the loader before
        /// J2C encoding. Used to report accurate width/height in a fee-quote request that omits the
        /// real texture bytes -- see LLMeshUploadThread::wholeModelToLLSD's <c>texture_info</c>.</summary>
        public int Width;
        public int Height;

        /// <summary>
        /// The physically based material this was made from, when the model had one (glTF does; OBJ and
        /// COLLADA do not). The mesh upload does not carry it: a Second Life mesh has a color and one texture for each
        /// face. See <see cref="ModelPbrMaterial.ToAssetMaterial"/> for making a material asset from it.
        /// </summary>
        public ModelPbrMaterial? Pbr;
    }

    /// <summary>A texture of a <see cref="ModelPbrMaterial"/>, in the form it would be uploaded in</summary>
    public class ModelTexture
    {
        /// <summary>What the model calls the image: its file name, or a made-up name if it was embedded</summary>
        public string Name = string.Empty;

        /// <summary>The image as JPEG2000, ready to upload, or empty if the textures were not loaded or this one could not be</summary>
        public byte[] Data = Array.Empty<byte>();

        /// <summary>Pixel size of <see cref="Data"/></summary>
        public int Width;
        public int Height;

        /// <summary>The set of texture coordinates the model says this texture uses. Second Life uses the first.</summary>
        public int TexCoord;

        /// <summary>The model's <c>KHR_texture_transform</c>, as the model has it</summary>
        public GltfTextureTransform Transform = GltfTextureTransform.Default;
    }

    /// <summary>
    /// A physically based (metallic-roughness) material as a model file gives it, along with the textures a Second
    /// Life material would use, prepared the way the reference viewer prepares them when it imports a glTF material:
    /// no alpha, the occlusion map packed into the red channel of the metallic-roughness map (white if there is none),
    /// and the normal map compressed without loss.
    /// </summary>
    public class ModelPbrMaterial
    {
        public string Name = string.Empty;

        /// <summary>As the model has them: nothing is clamped or converted</summary>
        public Color4 BaseColorFactor = Color4.White;
        public float MetallicFactor = 1f;
        public float RoughnessFactor = 1f;
        public Vector3 EmissiveFactor = Vector3.Zero;
        public GltfAlphaMode AlphaMode = GltfAlphaMode.Opaque;
        public float AlphaCutoff = 0.5f;
        public bool DoubleSided;

        /// <summary>How strongly the normal map bends the surface. A Second Life material has no such setting.</summary>
        public float NormalScale = 1f;

        /// <summary>How strongly occlusion darkens the surface. A Second Life material has no such setting.</summary>
        public float OcclusionStrength = 1f;

        public ModelTexture? BaseColorTexture;
        public ModelTexture? NormalTexture;

        /// <summary>Occlusion in the red channel, roughness in the green and metalness in the blue, as a Second Life material has it</summary>
        public ModelTexture? MetallicRoughnessTexture;
        public ModelTexture? EmissiveTexture;

        /// <summary>
        /// Makes the Second Life material asset for this material, given the asset IDs the caller got by uploading the
        /// textures (called once for each texture this material has; the textures are in <see cref="ModelTexture.Data"/>).
        /// Like the viewer's import, it leaves double sided off whatever the model says, because that doubles the cost of
        /// drawing it and most exporters turn it on without need, and it leaves out the texture transforms.
        /// </summary>
        /// <param name="textureId">Gives the asset ID of an uploaded texture</param>
        /// <param name="carryTextureTransforms">
        /// Carry the model's <c>KHR_texture_transform</c> over too. The viewer's import does not, and which way is up
        /// in the transform of a Second Life material has not been checked against it, so this is as the model has them.
        /// </param>
        public AssetMaterial ToAssetMaterial(Func<ModelTexture, UUID> textureId, bool carryTextureTransforms = false)
        {
            if (textureId == null) throw new ArgumentNullException(nameof(textureId));

            var asset = new AssetMaterial
            {
                Name = Name,
                BaseColorFactor = BaseColorFactor,
                EmissiveFactor = EmissiveFactor,
                MetallicFactor = MetallicFactor,
                RoughnessFactor = RoughnessFactor,
                AlphaMode = AlphaMode,
                AlphaCutoff = AlphaCutoff,
                DoubleSided = false
            };

            void Slot(int slot, ModelTexture? texture)
            {
                if (texture == null) return;
                asset.TextureIds[slot] = textureId(texture);
                if (carryTextureTransforms) asset.TextureTransforms[slot] = texture.Transform;
            }

            Slot(AssetMaterial.TEXTURE_BASE_COLOR, BaseColorTexture);
            Slot(AssetMaterial.TEXTURE_NORMAL, NormalTexture);
            Slot(AssetMaterial.TEXTURE_METALLIC_ROUGHNESS, MetallicRoughnessTexture);
            Slot(AssetMaterial.TEXTURE_EMISSIVE, EmissiveTexture);
            return asset;
        }
    }

    /// <summary>
    /// The skin of a rigged <see cref="ModelPrim"/>: the avatar joints it is weighted to and where they were
    /// when it was bound. Written to the <c>skin</c> block of the mesh asset.
    /// </summary>
    public class ModelSkin
    {
        /// <summary>Most joints a skin may have. The reference viewer ignores any beyond 110.</summary>
        public const int MaxJoints = 110;

        /// <summary>The names of the avatar skeleton joints the vertices are weighted to. The joint indices in
        /// <see cref="ModelFace.Weights"/> index this list.</summary>
        public List<string> JointNames = new List<string>();

        /// <summary>One inverse bind matrix per joint: 16 floats each, row-major, row-vector convention
        /// (translation in elements 12 to 14). Length is <c>JointNames.Count * 16</c>.</summary>
        public float[] InverseBindMatrices = Array.Empty<float>();

        /// <summary>Takes a vertex of the mesh asset to the space the inverse bind matrices expect: 16 floats,
        /// row-major, row-vector convention. A mesh that was fitted into the asset's unit cube carries the
        /// scale and offset that undid that here.</summary>
        public float[] BindShapeMatrix =
        {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        };

        internal OSDMap ToOsd()
        {
            if (JointNames.Count == 0)
                throw new InvalidOperationException("A skin needs at least one joint");
            if (JointNames.Count > byte.MaxValue)
                throw new InvalidOperationException("A skin cannot have more than 255 joints");
            if (InverseBindMatrices.Length != JointNames.Count * 16)
                throw new InvalidOperationException("A skin needs exactly one inverse bind matrix per joint");
            if (BindShapeMatrix.Length != 16)
                throw new InvalidOperationException("The bind shape matrix must have 16 elements");

            var names = new OSDArray();
            var matrices = new OSDArray();
            for (int i = 0; i < JointNames.Count; i++)
            {
                names.Add(OSD.FromString(JointNames[i]));
                var matrix = new OSDArray(16);
                for (int j = 0; j < 16; j++) matrix.Add(OSD.FromReal(InverseBindMatrices[i * 16 + j]));
                matrices.Add(matrix);
            }

            var bindShape = new OSDArray(16);
            foreach (var value in BindShapeMatrix) bindShape.Add(OSD.FromReal(value));

            return new OSDMap
            {
                ["joint_names"] = names,
                ["inverse_bind_matrix"] = matrices,
                ["bind_shape_matrix"] = bindShape
            };
        }
    }

    public class ModelFace
    {
        public List<Vertex> Vertices = new List<Vertex>();
        public List<uint> Indices = new List<uint>();
        public string MaterialID = string.Empty;
        public ModelMaterial Material = new ModelMaterial();

        /// <summary>
        /// The skin weights of the vertices, parallel to <see cref="Vertices"/>, when the prim has a
        /// <see cref="ModelPrim.Skin"/>. Joint indices refer to <see cref="ModelSkin.JointNames"/>; at most four
        /// influences are used, the weights should add up to one.
        /// </summary>
        public List<VertexWeight>? Weights;

        private readonly Dictionary<Vertex, int> LookUp = new Dictionary<Vertex, int>();
        private readonly Dictionary<SkinnedVertex, int> SkinnedLookUp = new Dictionary<SkinnedVertex, int>();

        private struct SkinnedVertex : IEquatable<SkinnedVertex>
        {
            public Vertex Vertex;
            public VertexWeight Weight;

            public bool Equals(SkinnedVertex other)
            {
                return Vertex.Equals(other.Vertex)
                    && Weight.Joint0 == other.Weight.Joint0 && Weight.Joint1 == other.Weight.Joint1
                    && Weight.Joint2 == other.Weight.Joint2 && Weight.Joint3 == other.Weight.Joint3
                    && Weight.Weight0 == other.Weight.Weight0 && Weight.Weight1 == other.Weight.Weight1
                    && Weight.Weight2 == other.Weight.Weight2 && Weight.Weight3 == other.Weight.Weight3;
            }

            public override bool Equals(object? obj) => obj is SkinnedVertex other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Vertex.GetHashCode();
                    hash = hash * 31 + Weight.Joint0 + Weight.Joint1 * 7 + Weight.Joint2 * 13 + Weight.Joint3 * 17;
                    hash = hash * 31 + Weight.Weight0.GetHashCode() + Weight.Weight1.GetHashCode() * 7
                        + Weight.Weight2.GetHashCode() * 13 + Weight.Weight3.GetHashCode() * 17;
                    return hash;
                }
            }
        }

        /// <summary>
        /// Adds a vertex with skin weights, sharing it with an earlier one only if both the vertex and its
        /// weights are the same. Keeps <see cref="Weights"/> parallel to <see cref="Vertices"/>.
        /// </summary>
        public void AddVertex(Vertex v, VertexWeight weight)
        {
            Weights ??= new List<VertexWeight>();

            var key = new SkinnedVertex { Vertex = v, Weight = weight };
            int index;
            if (SkinnedLookUp.TryGetValue(key, out var value))
            {
                index = value;
            }
            else
            {
                index = Vertices.Count;
                Vertices.Add(v);
                Weights.Add(weight);
                SkinnedLookUp[key] = index;
            }

            Indices.Add((uint)index);
        }

        public void AddVertex(Vertex v)
        {
            int index;

            if (LookUp.TryGetValue(v, out var value))
            {
                index = value;
            }
            else
            {
                index = Vertices.Count;
                Vertices.Add(v);
                LookUp[v] = index;
            }

            Indices.Add((uint)index);
        }

    }

    public class ModelPrim
    {
        /// <summary>Most faces a mesh may have, which is the reference viewer's limit</summary>
        public const int MaxFaces = 8;

        /// <summary>
        /// Most vertices a face may have. The viewer refuses a face that reaches 65535, because its triangles are
        /// indexed with 16 bits. This is stricter than <see cref="FacetedMesh.MaxVerticesPerFace"/>, which is how many
        /// vertices a downloaded mesh may have before it is distrusted.
        /// </summary>
        public const int MaxVerticesPerFace = 65534;

        public List<Vector3> Positions = new List<Vector3>();
        public Vector3 BoundMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        public Vector3 BoundMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        public Vector3 Position;
        public Vector3 Scale;
        public Quaternion Rotation = Quaternion.Identity;
        public List<ModelFace> Faces = new List<ModelFace>();
        public string ID = string.Empty;
        public byte[] Asset = Array.Empty<byte>();

        /// <summary>The skin of a rigged mesh, or null for an ordinary one. Written into the asset by
        /// <see cref="CreateAsset"/> together with the <see cref="ModelFace.Weights"/> of its faces.</summary>
        public ModelSkin? Skin;

        /// <summary>
        /// Builds the mesh asset: the high level of detail (the prim's own faces) and the stub physics shape, plus the
        /// skin of a rigged prim. Use the overload with <see cref="ModelAssetOptions"/> for the lower levels of detail and a
        /// physics shape that fits the mesh, which is what an upload wants.
        /// </summary>
        public void CreateAsset(UUID creator)
        {
            BuildAsset(creator, null, null);
        }

        /// <summary>
        /// Builds the mesh asset as <see cref="CreateAsset(UUID)"/> does, and generates what <paramref name="options"/> asks
        /// for: the medium, low and lowest levels of detail, made by simplifying the faces, and a physics shape that
        /// is the convex hull of the mesh. A rigged prim has no physics shape worth making and keeps the stub.
        /// </summary>
        public void CreateAsset(UUID creator, ModelAssetOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            List<ModelFace>[]? lods = null;
            if (options.GenerateLods)
                lods = ModelLodGenerator.Generate(this, options.LodReduction, cancellationToken);

            List<Vector3>? hull = null;
            if (options.GeneratePhysics && Skin == null)
                hull = ModelPhysics.BaseHull(this);

            BuildAsset(creator, lods, hull);
        }

        /// <param name="lods">The medium, low and lowest levels of detail, or null for the high one only</param>
        /// <param name="hull">The points of the physics hull, or null for the stub</param>
        private void BuildAsset(UUID creator, List<ModelFace>[]? lods, List<Vector3>? hull)
        {
            OSDMap header = new OSDMap();
            header["version"] = 1;
            header["creator"] = creator;
            header["date"] = DateTime.Now;

            var parts = new List<KeyValuePair<string, byte[]>>();
            if (lods != null)
            {
                parts.Add(new KeyValuePair<string, byte[]>("lowest_lod", Helpers.ZCompressOSD(EncodeFaces(lods[2]))));
                parts.Add(new KeyValuePair<string, byte[]>("low_lod", Helpers.ZCompressOSD(EncodeFaces(lods[1]))));
                parts.Add(new KeyValuePair<string, byte[]>("medium_lod", Helpers.ZCompressOSD(EncodeFaces(lods[0]))));
            }
            parts.Add(new KeyValuePair<string, byte[]>("high_lod", Helpers.ZCompressOSD(EncodeFaces(Faces))));
            parts.Add(new KeyValuePair<string, byte[]>("physics_convex", Helpers.ZCompressOSD(hull != null ? ModelPhysics.Encode(hull) : PhysicsStub())));
            if (Skin != null)
                parts.Add(new KeyValuePair<string, byte[]>("skin", Helpers.ZCompressOSD(Skin.ToOsd())));

            int n = 0;
            foreach (var part in parts)
            {
                var parms = new OSDMap();
                parms["offset"] = n;
                parms["size"] = part.Value.Length;
                header[part.Key] = parms;
                n += part.Value.Length;
            }

            byte[] headerBytes = OSDParser.SerializeLLSDBinary(header, false);
            n += headerBytes.Length;

            Asset = new byte[n];

            int offset = 0;
            Buffer.BlockCopy(headerBytes, 0, Asset, offset, headerBytes.Length);
            offset += headerBytes.Length;

            foreach (var part in parts)
            {
                Buffer.BlockCopy(part.Value, 0, Asset, offset, part.Value.Length);
                offset += part.Value.Length;
            }
        }

        private OSDArray EncodeFaces(List<ModelFace> faceList)
        {
            OSDArray faces = new OSDArray();
            foreach (var face in faceList)
            {
                OSDMap faceMap = new OSDMap();

                // Find UV min/max
                Vector2 uvMin = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 uvMax = new Vector2(float.MinValue, float.MinValue);
                foreach (var v in face.Vertices)
                {
                    if (v.TexCoord.X < uvMin.X) uvMin.X = v.TexCoord.X;
                    if (v.TexCoord.Y < uvMin.Y) uvMin.Y = v.TexCoord.Y;

                    if (v.TexCoord.X > uvMax.X) uvMax.X = v.TexCoord.X;
                    if (v.TexCoord.Y > uvMax.Y) uvMax.Y = v.TexCoord.Y;
                }
                OSDMap uvDomain = new OSDMap();
                uvDomain["Min"] = uvMin;
                uvDomain["Max"] = uvMax;
                faceMap["TexCoord0Domain"] = uvDomain;


                OSDMap positionDomain = new OSDMap();
                positionDomain["Min"] = new Vector3(-0.5f, -0.5f, -0.5f);
                positionDomain["Max"] = new Vector3(0.5f, 0.5f, 0.5f);
                faceMap["PositionDomain"] = positionDomain;

                List<byte> posBytes = new List<byte>(face.Vertices.Count * sizeof(ushort) * 3);
                List<byte> norBytes = new List<byte>(face.Vertices.Count * sizeof(ushort) * 3);
                List<byte> uvBytes = new List<byte>(face.Vertices.Count * sizeof(ushort) * 2);

                foreach (var v in face.Vertices)
                {
                    posBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.Position.X, -0.5f, 0.5f)));
                    posBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.Position.Y, -0.5f, 0.5f)));
                    posBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.Position.Z, -0.5f, 0.5f)));

                    norBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.Normal.X, -1f, 1f)));
                    norBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.Normal.Y, -1f, 1f)));
                    norBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.Normal.Z, -1f, 1f)));

                    uvBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.TexCoord.X, uvMin.X, uvMax.X)));
                    uvBytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(v.TexCoord.Y, uvMin.Y, uvMax.Y)));
                }

                faceMap["Position"] = posBytes.ToArray();
                faceMap["Normal"] = norBytes.ToArray();
                faceMap["TexCoord0"] = uvBytes.ToArray();

                List<byte> indexBytes = new List<byte>(face.Indices.Count * sizeof(ushort));
                foreach (var t in face.Indices)
                {
                    indexBytes.AddRange(Utils.UInt16ToBytes((ushort)t));
                }
                faceMap["TriangleList"] = indexBytes.ToArray();

                if (Skin != null)
                {
                    faceMap["Weights"] = EncodeWeights(face, Skin.JointNames.Count);
                }

                faces.Add(faceMap);
            }
            return faces;
        }

        /// <summary>
        /// Encodes a face's skin weights the way the reference viewer writes them: for each vertex up to four
        /// (joint index: 1 byte, weight: 2 bytes little-endian, 0 to 65535) entries, strongest first, then a
        /// 0xFF byte if there were fewer than four. A vertex with four entries has no terminator.
        /// </summary>
        private static byte[] EncodeWeights(ModelFace face, int jointCount)
        {
            if (face.Weights == null || face.Weights.Count != face.Vertices.Count)
                throw new InvalidOperationException("A rigged face needs one set of weights per vertex");

            var bytes = new List<byte>(face.Vertices.Count * 4);
            var entries = new (int Joint, float Weight)[4];
            foreach (var w in face.Weights)
            {
                int count = 0;
                void Add(int joint, float weight)
                {
                    if (!(weight > 0f)) return;
                    if (joint < 0 || joint >= jointCount)
                        throw new InvalidOperationException($"A vertex is weighted to joint {joint}, but the skin has {jointCount}");
                    entries[count++] = (joint, weight);
                }
                Add(w.Joint0, w.Weight0);
                Add(w.Joint1, w.Weight1);
                Add(w.Joint2, w.Weight2);
                Add(w.Joint3, w.Weight3);

                Array.Sort(entries, 0, count, Comparer<(int Joint, float Weight)>.Create((a, b) => b.Weight.CompareTo(a.Weight)));
                for (int i = 0; i < count; i++)
                {
                    bytes.Add((byte)entries[i].Joint);
                    ushort raw = (ushort)(Math.Min(entries[i].Weight, 1f) * 65535f);
                    bytes.Add((byte)(raw & 0xFF));
                    bytes.Add((byte)(raw >> 8));
                }
                if (count < 4) bytes.Add(0xFF);
            }
            return bytes.ToArray();
        }

        public static OSD PhysicsStub()
        {
            OSDMap ret = new OSDMap();
            ret["Max"] = new Vector3(0.5f, 0.5f, 0.5f);
            ret["Min"] = new Vector3(-0.5f, -0.5f, -0.5f);
            ret["BoundingVerts"] = new byte[] { 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 255, 255, 255, 127, 0, 0, 255, 255, 255, 127, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 127, 255, 255, 255, 255, 255, 255, 0, 0, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0, 255, 255, 0, 0, 255, 255 };
            return ret;
        }

    }
}