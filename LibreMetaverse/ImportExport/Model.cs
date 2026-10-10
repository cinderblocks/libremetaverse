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

        public void CreateAsset(UUID creator)
        {
            OSDMap header = new OSDMap();
            header["version"] = 1;
            header["creator"] = creator;
            header["date"] = DateTime.Now;

            OSDArray faces = new OSDArray();
            foreach (var face in Faces)
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

            byte[] physicStubBytes = Helpers.ZCompressOSD(PhysicsStub());
            byte[]? skinBytes = Skin != null ? Helpers.ZCompressOSD(Skin.ToOsd()) : null;

            byte[] meshBytes = Helpers.ZCompressOSD(faces);
            int n = 0;

            OSDMap lodParms = new OSDMap();
            lodParms["offset"] = n;
            lodParms["size"] = meshBytes.Length;
            header["high_lod"] = lodParms;
            n += meshBytes.Length;

            lodParms = new OSDMap();
            lodParms["offset"] = n;
            lodParms["size"] = physicStubBytes.Length;
            header["physics_convex"] = lodParms;
            n += physicStubBytes.Length;

            if (skinBytes != null)
            {
                lodParms = new OSDMap();
                lodParms["offset"] = n;
                lodParms["size"] = skinBytes.Length;
                header["skin"] = lodParms;
                n += skinBytes.Length;
            }

            byte[] headerBytes = OSDParser.SerializeLLSDBinary(header, false);
            n += headerBytes.Length;

            Asset = new byte[n];

            int offset = 0;
            Buffer.BlockCopy(headerBytes, 0, Asset, offset, headerBytes.Length);
            offset += headerBytes.Length;

            Buffer.BlockCopy(meshBytes, 0, Asset, offset, meshBytes.Length);
            offset += meshBytes.Length;

            Buffer.BlockCopy(physicStubBytes, 0, Asset, offset, physicStubBytes.Length);
            offset += physicStubBytes.Length;

            if (skinBytes != null)
            {
                Buffer.BlockCopy(skinBytes, 0, Asset, offset, skinBytes.Length);
                offset += skinBytes.Length;
            }

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