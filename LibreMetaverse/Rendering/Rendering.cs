/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;

// The common elements shared between rendering plugins are defined here

namespace OpenMetaverse.Rendering
{
    #region Enums

    public enum FaceType : ushort
    {
        PathBegin = 0x1 << 0,
        PathEnd = 0x1 << 1,
        InnerSide = 0x1 << 2,
        ProfileBegin = 0x1 << 3,
        ProfileEnd = 0x1 << 4,
        OuterSide0 = 0x1 << 5,
        OuterSide1 = 0x1 << 6,
        OuterSide2 = 0x1 << 7,
        OuterSide3 = 0x1 << 8
    }

    [Flags]
    public enum FaceMask
    {
        Single = 0x0001,
        Cap = 0x0002,
        End = 0x0004,
        Side = 0x0008,
        Inner = 0x0010,
        Outer = 0x0020,
        Hollow = 0x0040,
        Open = 0x0080,
        Flat = 0x0100,
        Top = 0x0200,
        Bottom = 0x0400
    }

    public enum DetailLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Highest = 3
    }

    #endregion Enums

    #region Structs

    [StructLayout(LayoutKind.Explicit)]
    public struct Vertex : IEquatable<Vertex>
    {
        [FieldOffset(0)]
        public Vector3 Position;
        [FieldOffset(12)]
        public Vector3 Normal;
        [FieldOffset(24)]
        public Vector2 TexCoord;

        public override string ToString()
        {
            return $"P: {Position} N: {Normal} T: {TexCoord}";
        }

        public override int GetHashCode()
        {
            int hash = Position.GetHashCode();
            hash = hash * 31 + Normal.GetHashCode();
            hash = hash * 31 + TexCoord.GetHashCode();
            return hash;
        }

        public static bool operator ==(Vertex value1, Vertex value2)
        {
            return value1.Position == value2.Position
                && value1.Normal == value2.Normal
                && value1.TexCoord == value2.TexCoord;
        }

        public static bool operator !=(Vertex value1, Vertex value2)
        {
            return !(value1 == value2);
        }

        public override bool Equals(object? obj)
        {
            return (obj is Vertex vertex) && this == vertex;
        }

        public bool Equals(Vertex other)
        {
            return this == other;
        }
    }

    public struct ProfileFace
    {
        public int Index;
        public int Count;
        public float ScaleU;
        public bool Cap;
        public bool Flat;
        public FaceType Type;

        public override string ToString()
        {
            return Type.ToString();
        }
    }

    public struct Profile
    {
        public float MinX;
        public float MaxX;
        public bool Open;
        public bool Concave;
        public int TotalOutsidePoints;
        public List<Vector3> Positions;
        public List<ProfileFace> Faces;
    }

    public struct PathPoint
    {
        public Vector3 Position;
        public Vector2 Scale;
        public Quaternion Rotation;
        public float TexT;
    }

    public struct Path
    {
        public List<PathPoint> Points;
        public bool Open;
    }

    public struct Face
    {
        // Only used for Inner/Outer faces
        public int BeginS;
        public int BeginT;
        public int NumS;
        public int NumT;

        public int ID;
        public Vector3 Center;
        public Vector3 MinExtent;
        public Vector3 MaxExtent;
        public List<Vertex> Vertices;
        public List<ushort> Indices;
        public List<int> Edge;
        public FaceMask Mask;
        public Primitive.TextureEntryFace TextureFace;
        public object UserData;

        /// <summary>
        /// Per-vertex skin weights for rigged mesh faces.
        /// Parallel to <see cref="Vertices"/> — index <c>i</c> holds the
        /// joint influences for vertex <c>i</c>.  Null when the face has no
        /// skinning data.
        /// </summary>
        public List<VertexWeight> Weights;

        /// <summary>
        /// Optional second UV channel (e.g. lightmap / PBR).
        /// Parallel to <see cref="Vertices"/> — index <c>i</c> holds the
        /// second texture coordinate for vertex <c>i</c>.  Null when the
        /// submesh has no <c>TexCoord1</c> data.
        /// </summary>
        public List<Vector2>? TexCoords1;

        /// <summary>
        /// Normalized scale for this face, used for mesh normalization.
        /// Defaults to <c>(1, 1, 1)</c> when not present in the asset.
        /// </summary>
        public Vector3 NormalizedScale;

        public override string ToString()
        {
            return Mask.ToString();
        }
    }

    /// <summary>
    /// Stores up to 4 bone influences for a single vertex in a rigged mesh.
    /// Joint indices reference the <see cref="MeshSkinData.JointNames"/> array.
    /// Weights are normalized so they sum to 1.
    /// </summary>
    public struct VertexWeight
    {
        public int   Joint0;
        public int   Joint1;
        public int   Joint2;
        public int   Joint3;
        public float Weight0;
        public float Weight1;
        public float Weight2;
        public float Weight3;
    }

    /// <summary>
    /// Skinning data decoded from the <c>skin</c> section of a Second Life
    /// mesh asset.  Contains joint names, bind matrices, and per-vertex
    /// weights needed for rigged / fitted mesh rendering.
    /// </summary>
    public class MeshSkinData
    {
        /// <summary>Ordered list of joint (bone) names referenced by this mesh.</summary>
        public string[] JointNames = Array.Empty<string>();

        /// <summary>
        /// Inverse bind matrix for each joint — a 4×4 row-major float[16]
        /// per joint, using row-vector convention (same as OpenTK).
        /// Length = <c>JointNames.Length * 16</c>.
        /// </summary>
        public float[] InverseBindMatrices = Array.Empty<float>();

        /// <summary>
        /// The bind-shape matrix (4×4, row-major, 16 floats, row-vector convention).
        /// Transforms vertices from mesh-local space into bind-pose space
        /// before joint skinning is applied.
        /// </summary>
        public float[] BindShapeMatrix = new float[]
        {
            1,0,0,0,
            0,1,0,0,
            0,0,1,0,
            0,0,0,1
        };

        /// <summary>
        /// Optional pelvis offset (Z) baked into the skin section.
        /// </summary>
        public float PelvisOffset;

        /// <summary>
        /// Alternative inverse bind matrices for joints with overridden positions
        /// (custom joint positions from the DAE file).  Same layout as
        /// <see cref="InverseBindMatrices"/> — 16 floats per joint, row-major.
        /// Empty when no overrides are present.
        /// </summary>
        public float[] AltInverseBindMatrices = Array.Empty<float>();

        /// <summary>
        /// When true, bone scale is locked if the mesh overrides joint positions.
        /// </summary>
        public bool LockScaleIfJointPosition;
    }

    #endregion Structs

    #region Exceptions

    public class RenderingException : Exception
    {
        public RenderingException(string message)
            : base(message)
        {
        }

        public RenderingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    #endregion Exceptions

    #region Mesh Classes

    public class Mesh
    {
        // Initialize reference-type members to non-null defaults to satisfy nullable analysis
        public Primitive Prim = new Primitive();
        public Path Path;
        public Profile Profile;

        public override string ToString()
        {
            var name = Prim.Properties?.Name;
            return !string.IsNullOrEmpty(name)
                ? name!
                : $"{Prim.LocalID} ({Prim.PrimData})";
        }
    }

    /// <summary>
    /// Contains all mesh faces that belong to a prim
    /// </summary>
    public class FacetedMesh : Mesh
    {
        /// <summary>List of primitive faces</summary>
        public List<Face> Faces = new List<Face>();

        /// <summary>
        /// Skinning data for rigged meshes.  Null when the mesh asset does
        /// not contain a <c>skin</c> section (i.e. the mesh is not rigged).
        /// </summary>
        public MeshSkinData? SkinData;

        /// <summary>
        /// Decodes mesh asset into FacetedMesh
        /// </summary>
        /// <param name="prim">Mesh primitive</param>
        /// <param name="meshAsset">Asset retrieved from the asset server</param>
        /// <param name="LOD">Level of detail</param>
        /// <param name="mesh">Resulting decoded FacetedMesh</param>
        /// <returns>True if mesh asset decoding was successful</returns>
        public static bool TryDecodeFromAsset(Primitive prim, AssetMesh meshAsset, DetailLevel LOD, out FacetedMesh? mesh)
        {
            mesh = null;

            try
            {
                if (!meshAsset.Decode())
                {
                    return false;
                }

                OSDMap MeshData = meshAsset.MeshData;

                mesh = new FacetedMesh
                {
                    Faces = new List<Face>(),
                    Prim = prim,
                    Profile = new Profile { Faces = new List<ProfileFace>(), Positions = new List<Vector3>() },
                    Path = new Path { Points = new List<PathPoint>() }
                };

                // Parse skin section for rigged / fitted mesh support.
                if (MeshData.TryGetValue("skin", out var skinOsd) && skinOsd is OSDMap skinMap)
                {
                    mesh.SkinData = DecodeSkinData(skinMap);
                }

                OSD? facesOSD = null;

                switch (LOD)
                {
                    default:
                    case DetailLevel.Highest:
                        facesOSD = MeshData["high_lod"];
                        break;

                    case DetailLevel.High:
                        facesOSD = MeshData["medium_lod"];
                        break;

                    case DetailLevel.Medium:
                        facesOSD = MeshData["low_lod"];
                        break;

                    case DetailLevel.Low:
                        facesOSD = MeshData["lowest_lod"];
                        break;
                }

                if (!(facesOSD is OSDArray decodedMeshOsdArray))
                {
                    return false;
                }

                for (int faceNr = 0; faceNr < decodedMeshOsdArray.Count; faceNr++)
                {
                    OSD subMeshOsd = decodedMeshOsdArray[faceNr];

                    // Decode each individual face
                    if (subMeshOsd is OSDMap subMeshMap)
                    {
                        // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
                    // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
                    // geometry for this submesh.
                    if (subMeshMap.ContainsKey("NoGeometry") && ((OSDBoolean)subMeshMap["NoGeometry"]))
                        continue;

                        Face oface = new Face
                        {
                            ID = faceNr,
                            Vertices = new List<Vertex>(),
                            Indices = new List<ushort>(),
                            TextureFace = prim.Textures != null ? prim.Textures.GetFace((uint)faceNr) : new Primitive.TextureEntryFace(null)
                        };

                        Vector3 posMax;
                        Vector3 posMin;

                        // If PositionDomain is not specified, the default is from -0.5 to 0.5
                        if (subMeshMap.TryGetValue("PositionDomain", out var positionDomainObj) && positionDomainObj is OSDMap positionDomain)
                        {
                            posMax = (Vector3)positionDomain["Max"];
                            posMin = (Vector3)positionDomain["Min"];
                        }
                        else
                        {
                            posMax = new Vector3(0.5f, 0.5f, 0.5f);
                            posMin = new Vector3(-0.5f, -0.5f, -0.5f);
                        }

                        // Vertex positions
                        byte[] posBytes = subMeshMap["Position"];

                        // Normals
                        byte[]? norBytes = null;
                        if (subMeshMap.TryGetValue("Normal", out var normal))
                        {
                            norBytes = normal;
                        }

                        // NOTE: The SL mesh format also defines a "Tangent" binary field
                        // (uint16 quads, domain [-1,1], w sign for bitangent handedness).
                        // The SL viewer currently disables tangent parsing (#if 0 in
                        // llvolume.cpp) so we skip it here as well.

                        // UV texture map
                        Vector2 texPosMax = Vector2.Zero;
                        Vector2 texPosMin = Vector2.Zero;
                        byte[]? texBytes = null;
                        if (subMeshMap.TryGetValue("TexCoord0", out var texCoord0))
                        {
                            texBytes = texCoord0;
                            texPosMax = ((OSDMap)subMeshMap["TexCoord0Domain"])["Max"];
                            texPosMin = ((OSDMap)subMeshMap["TexCoord0Domain"])["Min"];
                        }

                        // Second UV channel (lightmap / PBR)
                        Vector2 tex1PosMax = Vector2.Zero;
                        Vector2 tex1PosMin = Vector2.Zero;
                        byte[]? tex1Bytes = null;
                        if (subMeshMap.TryGetValue("TexCoord1", out var texCoord1))
                        {
                            tex1Bytes = texCoord1;
                            if (subMeshMap.TryGetValue("TexCoord1Domain", out var tc1d) && tc1d is OSDMap tc1Domain)
                            {
                                tex1PosMax = (Vector2)tc1Domain["Max"];
                                tex1PosMin = (Vector2)tc1Domain["Min"];
                            }
                        }

                        // Per-face normalized scale
                        if (subMeshMap.TryGetValue("NormalizedScale", out var nsOsd))
                        {
                            oface.NormalizedScale = (Vector3)nsOsd;
                        }
                        else
                        {
                            oface.NormalizedScale = new Vector3(1f, 1f, 1f);
                        }

                        // Allocate TexCoords1 list when second UV channel is present.
                        if (tex1Bytes != null)
                        {
                            int numVerts = posBytes.Length / 6;
                            oface.TexCoords1 = new List<Vector2>(numVerts);
                        }

                        // Extract the vertex position data
                        // If present normals and texture coordinates too
                        for (int i = 0; i < posBytes.Length; i += 6)
                        {
                            ushort uX = Utils.BytesToUInt16(posBytes, i);
                            ushort uY = Utils.BytesToUInt16(posBytes, i + 2);
                            ushort uZ = Utils.BytesToUInt16(posBytes, i + 4);

                            Vertex vx = new Vertex
                            {
                                Position = new Vector3(
                                    Utils.UInt16ToFloat(uX, posMin.X, posMax.X),
                                    Utils.UInt16ToFloat(uY, posMin.Y, posMax.Y),
                                    Utils.UInt16ToFloat(uZ, posMin.Z, posMax.Z))
                            };

                            if (norBytes != null && norBytes.Length >= i + 6)
                            {
                                ushort nX = Utils.BytesToUInt16(norBytes, i);
                                ushort nY = Utils.BytesToUInt16(norBytes, i + 2);
                                ushort nZ = Utils.BytesToUInt16(norBytes, i + 4);

                                // Normal domain is always [-1, 1] per the SL mesh format
                                // (not the position domain).
                                vx.Normal = new Vector3(
                                    Utils.UInt16ToFloat(nX, -1f, 1f),
                                    Utils.UInt16ToFloat(nY, -1f, 1f),
                                    Utils.UInt16ToFloat(nZ, -1f, 1f));
                            }

                            var vertexIndexOffset = oface.Vertices.Count * 4;

                            if (texBytes != null && texBytes.Length >= vertexIndexOffset + 4)
                            {
                                ushort tX = Utils.BytesToUInt16(texBytes, vertexIndexOffset);
                                ushort tY = Utils.BytesToUInt16(texBytes, vertexIndexOffset + 2);

                                vx.TexCoord = new Vector2(
                                    Utils.UInt16ToFloat(tX, texPosMin.X, texPosMax.X),
                                    Utils.UInt16ToFloat(tY, texPosMin.Y, texPosMax.Y));
                            }

                            if (tex1Bytes != null && tex1Bytes.Length >= vertexIndexOffset + 4)
                            {
                                ushort t1X = Utils.BytesToUInt16(tex1Bytes, vertexIndexOffset);
                                ushort t1Y = Utils.BytesToUInt16(tex1Bytes, vertexIndexOffset + 2);

                                oface.TexCoords1?.Add(new Vector2(
                                    Utils.UInt16ToFloat(t1X, tex1PosMin.X, tex1PosMax.X),
                                    Utils.UInt16ToFloat(t1Y, tex1PosMin.Y, tex1PosMax.Y)));
                            }

                            oface.Vertices.Add(vx);
                        }

                        byte[] triangleBytes = subMeshMap["TriangleList"];
                        for (int i = 0; i < triangleBytes.Length; i += 6)
                        {
                            ushort v1 = (ushort)(Utils.BytesToUInt16(triangleBytes, i));
                            oface.Indices.Add(v1);
                            ushort v2 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 2));
                            oface.Indices.Add(v2);
                            ushort v3 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 4));
                            oface.Indices.Add(v3);
                        }

                        // Parse per-vertex skin weights for rigged mesh faces.
                        if (mesh.SkinData != null
                            && subMeshMap.TryGetValue("Weights", out var weightsOsd)
                            && weightsOsd.Type == OSDType.Binary)
                        {
                            oface.Weights = DecodeVertexWeights(
                                weightsOsd.AsBinary(),
                                mesh.SkinData.JointNames.Length,
                                oface.Vertices.Count);
                        }

                        mesh.Faces.Add(oface);
                    }
                }

                // Apply sculpt-type modifier flags (mirror / invert) per SL viewer.
                bool doMirror = prim.Sculpt != null && prim.Sculpt.Mirror;
                bool doInvert = prim.Sculpt != null && prim.Sculpt.Invert;

                bool doReflectX         = doMirror;
                bool doReverseTriangles = doMirror ^ doInvert;
                bool doInvertNormals    = doInvert;

                if (doReflectX || doInvertNormals || doReverseTriangles)
                {
                    for (int fi = 0; fi < mesh.Faces.Count; fi++)
                    {
                        var face = mesh.Faces[fi];

                        if (doReflectX || doInvertNormals)
                        {
                            for (int vi = 0; vi < face.Vertices.Count; vi++)
                            {
                                var v = face.Vertices[vi];
                                if (doReflectX)
                                {
                                    v.Position.X = -v.Position.X;
                                    v.Normal.X   = -v.Normal.X;
                                }
                                if (doInvertNormals)
                                {
                                    v.Normal.X = -v.Normal.X;
                                    v.Normal.Y = -v.Normal.Y;
                                    v.Normal.Z = -v.Normal.Z;
                                }
                                face.Vertices[vi] = v;
                            }
                        }

                        if (doReverseTriangles)
                        {
                            for (int ti = 0; ti + 2 < face.Indices.Count; ti += 3)
                            {
                                ushort tmp = face.Indices[ti + 1];
                                face.Indices[ti + 1] = face.Indices[ti + 2];
                                face.Indices[ti + 2] = tmp;
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to decode mesh asset: " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Decodes the <c>skin</c> section from a mesh asset into a <see cref="MeshSkinData"/>.
        /// </summary>
        private static MeshSkinData DecodeSkinData(OSDMap skinMap)
        {
            var data = new MeshSkinData();

            if (skinMap.TryGetValue("joint_names", out var jnOsd) && jnOsd is OSDArray jnArr)
            {
                data.JointNames = new string[jnArr.Count];
                for (int i = 0; i < jnArr.Count; i++)
                    data.JointNames[i] = jnArr[i].AsString();
            }

            if (skinMap.TryGetValue("inverse_bind_matrix", out var ibmOsd) && ibmOsd is OSDArray ibmArr)
            {
                data.InverseBindMatrices = new float[ibmArr.Count * 16];
                for (int i = 0; i < ibmArr.Count; i++)
                {
                    if (ibmArr[i] is OSDArray matArr)
                    {
                        for (int j = 0; j < 16 && j < matArr.Count; j++)
                            data.InverseBindMatrices[i * 16 + j] = (float)matArr[j].AsReal();
                    }
                }
            }

            if (skinMap.TryGetValue("bind_shape_matrix", out var bsmOsd) && bsmOsd is OSDArray bsmArr)
            {
                for (int j = 0; j < 16 && j < bsmArr.Count; j++)
                    data.BindShapeMatrix[j] = (float)bsmArr[j].AsReal();
            }

            if (skinMap.TryGetValue("pelvis_offset", out var poOsd))
            {
                data.PelvisOffset = (float)poOsd.AsReal();
            }

            if (skinMap.TryGetValue("alt_inverse_bind_matrix", out var aibmOsd) && aibmOsd is OSDArray aibmArr)
            {
                data.AltInverseBindMatrices = new float[aibmArr.Count * 16];
                for (int i = 0; i < aibmArr.Count; i++)
                {
                    if (aibmArr[i] is OSDArray aMatArr)
                    {
                        for (int j = 0; j < 16 && j < aMatArr.Count; j++)
                            data.AltInverseBindMatrices[i * 16 + j] = (float)aMatArr[j].AsReal();
                    }
                }
            }

            if (skinMap.TryGetValue("lock_scale_if_joint_position", out var lsOsd))
            {
                data.LockScaleIfJointPosition = lsOsd.AsBoolean();
            }

            return data;
        }

        /// <summary>
        /// Decodes per-vertex skin weights from binary data in a mesh sub-mesh.
        /// </summary>
        /// <remarks>
        /// Each vertex is encoded as a sequence of (joint_index: u8, weight: u16 LE) triplets.
        /// The vertex terminates when <c>joint_index &gt;= jointCount</c> (sentinel).
        /// Weights are normalized to sum to 1.  Up to 4 influences per vertex.
        /// </remarks>
        private static List<VertexWeight> DecodeVertexWeights(byte[] data, int jointCount, int vertexCount)
        {
            var weights = new List<VertexWeight>(vertexCount);
            int idx = 0;

            for (int v = 0; v < vertexCount && idx < data.Length; v++)
            {
                var vw = new VertexWeight();
                int influence = 0;

                while (idx < data.Length)
                {
                    int jointIdx = data[idx++];
                    if (jointIdx == 0xFF)
                        break; // end-of-vertex sentinel

                    if (idx + 1 >= data.Length) break;
                    ushort rawWeight = (ushort)(data[idx] | (data[idx + 1] << 8));
                    idx += 2;

                    if (jointIdx >= jointCount)
                        continue; // skip invalid but non-sentinel joint index

                    // Clamp to [0.001, 0.999] to match the SL viewer.
                    float w = rawWeight / 65535f;
                    if (w < 0.001f) w = 0.001f;
                    else if (w > 0.999f) w = 0.999f;

                    if (influence < 4)
                    {
                        switch (influence)
                        {
                            case 0: vw.Joint0 = jointIdx; vw.Weight0 = w; break;
                            case 1: vw.Joint1 = jointIdx; vw.Weight1 = w; break;
                            case 2: vw.Joint2 = jointIdx; vw.Weight2 = w; break;
                            case 3: vw.Joint3 = jointIdx; vw.Weight3 = w; break;
                        }
                        influence++;
                    }
                }

                // Normalize weights so they sum to 1.
                float total = vw.Weight0 + vw.Weight1 + vw.Weight2 + vw.Weight3;
                if (total > 0f && Math.Abs(total - 1f) > 0.001f)
                {
                    float inv = 1f / total;
                    vw.Weight0 *= inv;
                    vw.Weight1 *= inv;
                    vw.Weight2 *= inv;
                    vw.Weight3 *= inv;
                }

                weights.Add(vw);
            }

            // Pad with default weight (joint 0, weight 1.0) for vertices beyond the data.
            // Matches SL viewer fallback where unweighted vertices bind to the first joint.
            while (weights.Count < vertexCount)
            {
                weights.Add(new VertexWeight { Joint0 = 0, Weight0 = 1f });
            }

            return weights;
        }
    }

    public class SimpleMesh : Mesh
    {
        public List<Vertex> Vertices = new List<Vertex>();
        public List<ushort> Indices = new List<ushort>();

        public SimpleMesh()
        {
        }

        public SimpleMesh(SimpleMesh mesh)
        {
            this.Indices = new List<ushort>(mesh.Indices);
            this.Path.Open = mesh.Path.Open;
            this.Path.Points = new List<PathPoint>(mesh.Path.Points);
            this.Prim = mesh.Prim;
            this.Profile.Concave = mesh.Profile.Concave;
            this.Profile.Faces = new List<ProfileFace>(mesh.Profile.Faces);
            this.Profile.MaxX = mesh.Profile.MaxX;
            this.Profile.MinX = mesh.Profile.MinX;
            this.Profile.Open = mesh.Profile.Open;
            this.Profile.Positions = new List<Vector3>(mesh.Profile.Positions);
            this.Profile.TotalOutsidePoints = mesh.Profile.TotalOutsidePoints;
            this.Vertices = new List<Vertex>(mesh.Vertices);
        }
    }

    #endregion Mesh Classes

    #region Plugin Loading

    public static class RenderingLoader
    {
        public static List<string> ListRenderers(string path)
        {
            List<string> plugins = new List<string>();
            string[] files = Directory.GetFiles(path, "OpenMetaverse.Rendering.*.dll");

            foreach (string f in files)
            {
                try
                {
                    Assembly a = Assembly.LoadFrom(f);
                    System.Type[] types = a.GetTypes();
                    foreach (System.Type type in types)
                    {
                        if (type.GetInterface("IRendering") != null)
                        {
                            if (type.GetCustomAttributes(typeof(RendererNameAttribute), false).Length == 1)
                            {
                                plugins.Add(f);
                            }
                            else
                            {
                                Logger.Warn("Rendering plugin does not support the [RendererName] attribute: " + f);
                            }

                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn($"Unrecognized rendering plugin {f}: {e.Message}", e);
                }
            }

            return plugins;
        }

        public static IRendering LoadRenderer(string filename)
        {
            try
            {
                Assembly a = Assembly.LoadFrom(filename);
                System.Type[] types = a.GetTypes();
                foreach (System.Type type in types)
                {
                    if (type.GetInterface("IRendering") != null)
                    {
                            if (type.GetCustomAttributes(typeof(RendererNameAttribute), false).Length == 1)
                        {
                            var inst = Activator.CreateInstance(type) as IRendering;
                            if (inst != null) return inst;
                            throw new RenderingException("Failed to instantiate rendering plugin");
                        }
                        else
                        {
                            throw new RenderingException(
                                "Rendering plugin does not support the [RendererName] attribute");
                        }
                    }
                }

                throw new RenderingException(
                    "Rendering plugin does not support the IRendering interface");
            }
            catch (Exception e)
            {
                throw new RenderingException("Failed loading rendering plugin: " + e.Message, e);
            }
        }
    }

    #endregion Plugin Loading
}

