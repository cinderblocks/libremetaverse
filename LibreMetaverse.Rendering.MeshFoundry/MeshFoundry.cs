/* Copyright (c) 2008 Robert Adams
 * Copyright (c) 2021-2026, Sjofn LLC. All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * The name of the copyright holder may not be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using LibreMetaverse.PrimMesher;

namespace OpenMetaverse.Rendering
{
    [RendererName("MeshFoundry")]
    public class MeshFoundry : IRendering
    {
        /// <summary>
        /// Generates a basic mesh structure from a primitive.
        /// A <see cref="SimpleMesh"/> is the prim's overall shape with no material information.
        /// </summary>
        /// <param name="prim">Primitive to generate the mesh from</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh, or null on failure</returns>
        public SimpleMesh? GenerateSimpleMesh(Primitive prim, DetailLevel lod)
        {
            PrimMesh newPrim = GeneratePrimMesh(prim, lod, false);
            if (newPrim == null) { return null; }

            var mesh = new SimpleMesh
            {
                Path = new Path(),
                Prim = prim,
                Profile = new Profile(),
                Vertices = new List<Vertex>(newPrim.coords.Count)
            };

            foreach (Coord c in newPrim.coords)
            {
                mesh.Vertices.Add(new Vertex { Position = new Vector3(c.X, c.Y, c.Z) });
            }

            mesh.Indices = new List<ushort>(newPrim.faces.Count * 3);
            foreach (LibreMetaverse.PrimMesher.Face face in newPrim.faces)
            {
                mesh.Indices.Add((ushort)face.v1);
                mesh.Indices.Add((ushort)face.v2);
                mesh.Indices.Add((ushort)face.v3);
            }

            return mesh;
        }

        /// <summary>
        /// Generates a basic mesh structure from a primitive, including vertex normals.
        /// </summary>
        /// <param name="prim">Primitive to generate the mesh from</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh, or null on failure</returns>
        public SimpleMesh? GenerateSimpleMeshWithNormals(Primitive prim, DetailLevel lod)
        {
            PrimMesh newPrim = GeneratePrimMesh(prim, lod, true);
            if (newPrim == null) { return null; }

            var mesh = new SimpleMesh
            {
                Path = new Path(),
                Prim = prim,
                Profile = new Profile(),
                Vertices = new List<Vertex>(newPrim.coords.Count)
            };

            for (int i = 0; i < newPrim.coords.Count; i++)
            {
                Coord c = newPrim.coords[i];
                Coord n = newPrim.normals[i];
                mesh.Vertices.Add(new Vertex
                {
                    Position = new Vector3(c.X, c.Y, c.Z),
                    Normal = new Vector3(n.X, n.Y, n.Z)
                });
            }

            mesh.Indices = new List<ushort>(newPrim.faces.Count * 3);
            foreach (var face in newPrim.faces)
            {
                mesh.Indices.Add((ushort)face.v1);
                mesh.Indices.Add((ushort)face.v2);
                mesh.Indices.Add((ushort)face.v3);
            }

            return mesh;
        }

        /// <summary>
        /// Generates a basic mesh structure from a sculpted primitive.
        /// </summary>
        /// <param name="prim">Sculpted primitive to generate the mesh from</param>
        /// <param name="sculptTexture">Sculpt texture</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh, or null on failure</returns>
        public SimpleMesh? GenerateSimpleSculptMesh(Primitive prim, SKBitmap sculptTexture, DetailLevel lod)
        {
            var faceted = GenerateFacetedSculptMesh(prim, sculptTexture, lod);

            if (faceted != null && faceted.Faces.Count == 1)
            {
                Face face = faceted.Faces[0];
                return new SimpleMesh
                {
                    Indices = face.Indices,
                    Vertices = face.Vertices,
                    Path = faceted.Path,
                    Prim = prim,
                    Profile = faceted.Profile
                };
            }

            return null;
        }

        /// <summary>
        /// Generates a series of faces from a primitive, each containing a mesh and material metadata.
        /// </summary>
        /// <param name="prim">Primitive to generate the mesh from</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh, or null on failure</returns>
        public FacetedMesh? GenerateFacetedMesh(Primitive prim, DetailLevel lod)
        {
            PrimMesh newPrim = GeneratePrimMesh(prim, lod, true);
            if (newPrim == null) { return null;}

            var omvrmesh = new FacetedMesh
            {
                Faces = new List<Face>(),
                Prim = prim,
                Profile = new Profile
                {
                    Faces = new List<ProfileFace>(),
                    Positions = new List<Vector3>()
                },
                Path = new Path { Points = new List<PathPoint>() }
            };

            var indexer = newPrim.GetVertexIndexer();

            for (int i = 0; i < indexer.numPrimFaces; i++)
            {
                Face oface = new Face
                {
                    Vertices = new List<Vertex>(),
                    Indices = new List<ushort>(),
                    TextureFace = (prim.Textures != null)
                        ? (prim.Textures.GetFace((uint)i) ?? new Primitive.TextureEntryFace(null))
                        : new Primitive.TextureEntryFace(null)
                };

                for (int j = 0; j < indexer.viewerVertices[i].Count; j++)
                {
                    var m = indexer.viewerVertices[i][j];
                    oface.Vertices.Add(new Vertex
                    {
                        Position = new Vector3(m.v.X, m.v.Y, m.v.Z),
                        Normal = new Vector3(m.n.X, m.n.Y, m.n.Z),
                        TexCoord = new Vector2(m.uv.U, 1.0f - m.uv.V)
                    });
                }

                for (int j = 0; j < indexer.viewerPolygons[i].Count; j++)
                {
                    var p = indexer.viewerPolygons[i][j];
                    if (p.v1 == p.v2 || p.v1 == p.v3 || p.v2 == p.v3) continue;
                    oface.Indices.Add((ushort)p.v1);
                    oface.Indices.Add((ushort)p.v2);
                    oface.Indices.Add((ushort)p.v3);
                }

                omvrmesh.Faces.Add(oface);
            }

            return omvrmesh;
        }

        /// <summary>
        /// Generates a series of faces from a sculpted primitive, each containing a mesh and material metadata.
        /// </summary>
        /// <param name="prim">Sculpted primitive to generate the mesh from</param>
        /// <param name="sculptTexture">Sculpt texture</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh, or null on failure</returns>
        public FacetedMesh? GenerateFacetedSculptMesh(Primitive prim, SKBitmap sculptTexture, DetailLevel lod)
        {
            if (prim.Sculpt == null) { return null; }

            SculptMesh.SculptType smSculptType;
            switch (prim.Sculpt.Type)
            {
                case SculptType.Cylinder:
                    smSculptType = SculptMesh.SculptType.cylinder;
                    break;
                case SculptType.Plane:
                    smSculptType = SculptMesh.SculptType.plane;
                    break;
                case SculptType.Sphere:
                    smSculptType = SculptMesh.SculptType.sphere;
                    break;
                case SculptType.Torus:
                    smSculptType = SculptMesh.SculptType.torus;
                    break;
                default:
                    smSculptType = SculptMesh.SculptType.plane;
                    break;
            }

            int mesherLod = 32;
            switch (lod)
            {
                case DetailLevel.Highest:
                case DetailLevel.High:
                    break;
                case DetailLevel.Medium:
                    mesherLod /= 2;
                    break;
                case DetailLevel.Low:
                    mesherLod /= 4;
                    break;
            }

            SculptMesh newMesh = new SculptMesh(
                sculptTexture, smSculptType, mesherLod, true,
                prim.Sculpt.Mirror, prim.Sculpt.Invert);

            var omvrmesh = new FacetedMesh
            {
                Faces = new List<Face>(),
                Prim = prim,
                Profile = new Profile
                {
                    Faces = new List<ProfileFace>(),
                    Positions = new List<Vector3>()
                },
                Path = new Path { Points = new List<PathPoint>() }
            };

            var tex = prim.Textures;
            Primitive.TextureEntryFace tf = new Primitive.TextureEntryFace(null);
            if (tex != null)
            {
                if (tex.FaceTextures != null && tex.FaceTextures.Length > 0 && tex.FaceTextures[0] != null)
                    tf = tex.FaceTextures[0];
                else if (tex.DefaultTexture != null)
                    tf = tex.DefaultTexture;
            }

            int faceVertices = newMesh.coords.Count;
            if (faceVertices > 0)
            {
                Face oface = new Face
                {
                    ID = 0,
                    Vertices = new List<Vertex>(faceVertices),
                    Indices = new List<ushort>(newMesh.faces.Count * 3),
                    TextureFace = tf
                };

                for (int j = 0; j < faceVertices; j++)
                {
                    oface.Vertices.Add(new Vertex
                    {
                        Position = new Vector3(newMesh.coords[j].X, newMesh.coords[j].Y, newMesh.coords[j].Z),
                        Normal = new Vector3(newMesh.normals[j].X, newMesh.normals[j].Y, newMesh.normals[j].Z),
                        TexCoord = new Vector2(newMesh.uvs[j].U, newMesh.uvs[j].V)
                    });
                }

                for (int j = 0; j < newMesh.faces.Count; j++)
                {
                    oface.Indices.Add((ushort)newMesh.faces[j].v1);
                    oface.Indices.Add((ushort)newMesh.faces[j].v2);
                    oface.Indices.Add((ushort)newMesh.faces[j].v3);
                }

                omvrmesh.Faces.Add(oface);
            }

            return omvrmesh;
        }

        /// <summary>
        /// Apply texture coordinate modifications from a
        /// <see cref="Primitive.TextureEntryFace"/> to a list of vertices.
        /// </summary>
        /// <param name="vertices">Vertex list to modify texture coordinates for</param>
        /// <param name="center">Center-point of the face (part of the <see cref="IRendering"/> contract; unused — planar mapping derives orientation from vertex normals)</param>
        /// <param name="teFace">Face texture parameters</param>
        /// <param name="primScale">Scale of the prim</param>
        public void TransformTexCoords(List<Vertex> vertices, Vector3 center, Primitive.TextureEntryFace teFace, Vector3 primScale)
        {
            float cosineAngle = (float)Math.Cos(teFace.Rotation);
            float sinAngle = (float)Math.Sin(teFace.Rotation);

            for (int ii = 0; ii < vertices.Count; ii++)
            {
                Vertex vert = vertices[ii];

                if (teFace.TexMapType == MappingType.Planar)
                {
                    Vector3 binormal;
                    float d = Vector3.Dot(vert.Normal, Vector3.UnitX);
                    if (d >= 0.5f || d <= -0.5f)
                    {
                        binormal = Vector3.UnitY;
                        if (vert.Normal.X < 0f) binormal *= -1;
                    }
                    else
                    {
                        binormal = Vector3.UnitX;
                        if (vert.Normal.Y > 0f) binormal *= -1;
                    }
                    Vector3 tangent = binormal % vert.Normal;
                    Vector3 scaledPos = vert.Position * primScale;
                    vert.TexCoord.X = 1f + (Vector3.Dot(binormal, scaledPos) * 2f - 0.5f);
                    vert.TexCoord.Y = -(Vector3.Dot(tangent, scaledPos) * 2f - 0.5f);
                }

                float repeatU = teFace.RepeatU;
                float repeatV = teFace.RepeatV;
                float tX = vert.TexCoord.X - 0.5f;
                float tY = vert.TexCoord.Y - 0.5f;

                vert.TexCoord.X = (tX * cosineAngle + tY * sinAngle) * repeatU + teFace.OffsetU + 0.5f;
                vert.TexCoord.Y = (-tX * sinAngle + tY * cosineAngle) * repeatV + teFace.OffsetV + 0.5f;
                vertices[ii] = vert;
            }
        }

        /// <summary>
        /// Decodes a mesh asset into a <see cref="FacetedMesh"/> at the highest available LOD.
        /// Delegates to <see cref="FacetedMesh.TryDecodeFromAsset"/> for correct skin weight,
        /// TexCoord1, NormalizedScale, and normal domain decoding.
        /// </summary>
        /// <param name="prim">Primitive the mesh belongs to</param>
        /// <param name="meshData">Raw mesh asset bytes</param>
        /// <returns>The decoded mesh, or null on failure</returns>
        public FacetedMesh? GenerateFacetedMeshMesh(Primitive prim, byte[] meshData)
        {
            if (meshData == null || meshData.Length == 0) return null;
            var asset = new AssetMesh(UUID.Zero, meshData);
            if (FacetedMesh.TryDecodeFromAsset(prim, asset, DetailLevel.Highest, out var mesh))
                return mesh;
            DetailLevel? fallback = FindFallbackLod(asset.MeshData, DetailLevel.Highest);
            return fallback.HasValue && FacetedMesh.TryDecodeFromAsset(prim, asset, fallback.Value, out mesh)
                ? mesh : null;
        }

        /// <summary>
        /// Decodes a mesh asset into a <see cref="FacetedMesh"/> at the requested LOD.
        /// Delegates to <see cref="FacetedMesh.TryDecodeFromAsset"/> for correct skin weight,
        /// TexCoord1, NormalizedScale, and normal domain decoding.
        /// </summary>
        /// <param name="prim">Primitive the mesh belongs to</param>
        /// <param name="meshData">Raw mesh asset bytes</param>
        /// <param name="lod">Level of detail to decode</param>
        /// <returns>The decoded mesh, or null on failure</returns>
        public FacetedMesh? GenerateFacetedMeshMesh(Primitive prim, byte[] meshData, DetailLevel lod)
        {
            if (meshData == null || meshData.Length == 0) return null;
            var asset = new AssetMesh(UUID.Zero, meshData);
            if (FacetedMesh.TryDecodeFromAsset(prim, asset, lod, out var mesh))
                return mesh;
            DetailLevel? fallback = FindFallbackLod(asset.MeshData, lod);
            return fallback.HasValue && FacetedMesh.TryDecodeFromAsset(prim, asset, fallback.Value, out mesh)
                ? mesh : null;
        }

        /// <summary>
        /// Decodes a single compressed submesh buffer into a <see cref="SimpleMesh"/>.
        /// </summary>
        /// <param name="prim">Primitive the mesh belongs to</param>
        /// <param name="compressedMeshData">Compressed submesh bytes</param>
        /// <returns>The decoded mesh, or null on failure</returns>
        public SimpleMesh? MeshSubMeshAsSimpleMesh(Primitive prim, byte[] compressedMeshData)
        {
            if (!(Helpers.DecompressOSD(compressedMeshData) is OSDArray meshFaces))
                return null;

            var ret = new SimpleMesh
            {
                Prim = prim,
                Vertices = new List<Vertex>(),
                Indices = new List<ushort>()
            };

            foreach (OSD subMesh in meshFaces)
            {
                AddSubMesh(subMesh, ref ret);
            }

            return ret;
        }

        /// <summary>
        /// Decodes the physics convex hull section of a mesh asset.
        /// </summary>
        /// <param name="prim">Primitive the mesh belongs to</param>
        /// <param name="compressedMeshData">Compressed physics_convex bytes from the mesh asset</param>
        /// <returns>List of decomposed convex hulls, each as a list of vertex positions</returns>
        public List<List<Vector3>> MeshSubMeshAsConvexHulls(Primitive prim, byte[] compressedMeshData)
            => MeshSubMeshAsConvexHulls(prim, compressedMeshData, out _);

        /// <summary>
        /// Decodes the physics convex hull section of a mesh asset, including the overall bounding hull.
        /// </summary>
        /// <param name="prim">Primitive the mesh belongs to</param>
        /// <param name="compressedMeshData">Compressed physics_convex bytes from the mesh asset</param>
        /// <param name="boundingHull">The single overall convex bounding hull of the mesh (BoundingVerts section), or an empty list if not present</param>
        /// <returns>List of decomposed convex hulls, each as a list of vertex positions</returns>
        public List<List<Vector3>> MeshSubMeshAsConvexHulls(Primitive prim, byte[] compressedMeshData, out List<Vector3> boundingHull)
        {
            boundingHull = new List<Vector3>();
            if (compressedMeshData == null || compressedMeshData.Length == 0)
                return new List<List<Vector3>>();
            var hulls = new List<List<Vector3>>();
            try
            {
                OSD convexBlockOsd = Helpers.DecompressOSD(compressedMeshData);

                if (convexBlockOsd is OSDMap convexBlock)
                {
                    Vector3 min = new Vector3(-0.5f, -0.5f, -0.5f);
                    if (convexBlock.ContainsKey("Min")) min = convexBlock["Min"].AsVector3();
                    Vector3 max = new Vector3(0.5f, 0.5f, 0.5f);
                    if (convexBlock.ContainsKey("Max")) max = convexBlock["Max"].AsVector3();

                    if (convexBlock.ContainsKey("BoundingVerts"))
                    {
                        byte[] boundingVertsBytes = convexBlock["BoundingVerts"].AsBinary();
                        for (int i = 0; i < boundingVertsBytes.Length;)
                        {
                            ushort uX = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;
                            ushort uY = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;
                            ushort uZ = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;

                            boundingHull.Add(new Vector3(
                                Utils.UInt16ToFloat(uX, min.X, max.X),
                                Utils.UInt16ToFloat(uY, min.Y, max.Y),
                                Utils.UInt16ToFloat(uZ, min.Z, max.Z)));
                        }
                    }

                    if (convexBlock.ContainsKey("HullList"))
                    {
                        byte[] hullList = convexBlock["HullList"].AsBinary();
                        byte[] posBytes = convexBlock["Positions"].AsBinary();
                        int posNdx = 0;

                        foreach (byte cnt in hullList)
                        {
                            int count = cnt == 0 ? 256 : cnt;
                            var hull = new List<Vector3>(count);

                            for (int i = 0; i < count; i++)
                            {
                                ushort uX = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;
                                ushort uY = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;
                                ushort uZ = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;

                                hull.Add(new Vector3(
                                    Utils.UInt16ToFloat(uX, min.X, max.X),
                                    Utils.UInt16ToFloat(uY, min.Y, max.Y),
                                    Utils.UInt16ToFloat(uZ, min.Z, max.Z)));
                            }

                            hulls.Add(hull);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return whatever was collected before the error
            }

            return hulls;
        }

        /// <summary>
        /// Generates a terrain <see cref="Face"/> from a height map.
        /// </summary>
        /// <param name="zMap">Two-dimensional array of height values</param>
        /// <param name="xBegin">Starting X value</param>
        /// <param name="xEnd">Ending X value</param>
        /// <param name="yBegin">Starting Y value</param>
        /// <param name="yEnd">Ending Y value</param>
        /// <returns>A face containing the terrain mesh</returns>
        public Face TerrainMesh(float[,] zMap, float xBegin, float xEnd, float yBegin, float yEnd)
        {
            SculptMesh newMesh = new SculptMesh(zMap, xBegin, xEnd, yBegin, yEnd, true);
            int faceVertices = newMesh.coords.Count;
            Face terrain = new Face
            {
                Vertices = new List<Vertex>(faceVertices),
                Indices = new List<ushort>(newMesh.faces.Count * 3)
            };

            for (int j = 0; j < faceVertices; j++)
            {
                terrain.Vertices.Add(new Vertex
                {
                    Position = new Vector3(newMesh.coords[j].X, newMesh.coords[j].Y, newMesh.coords[j].Z),
                    Normal = new Vector3(newMesh.normals[j].X, newMesh.normals[j].Y, newMesh.normals[j].Z),
                    TexCoord = new Vector2(newMesh.uvs[j].U, newMesh.uvs[j].V)
                });
            }

            for (int j = 0; j < newMesh.faces.Count; j++)
            {
                terrain.Indices.Add((ushort)newMesh.faces[j].v1);
                terrain.Indices.Add((ushort)newMesh.faces[j].v2);
                terrain.Indices.Add((ushort)newMesh.faces[j].v3);
            }

            return terrain;
        }

        /// <summary>
        /// Unpacks the LLSD header of a mesh asset into its named sections.
        /// Each section value is the raw compressed bytes for that section.
        /// </summary>
        /// <param name="assetData">Raw mesh asset bytes</param>
        /// <returns>OSDMap of section name to compressed bytes, or null on failure</returns>
        public OSDMap? UnpackMesh(byte[] assetData)
        {
            OSDMap? meshData = new OSDMap();
            try
            {
                using (MemoryStream data = new MemoryStream(assetData))
                {
                    OSDMap header = (OSDMap)OSDParser.DeserializeLLSDBinary(data);
                    meshData["asset_header"] = header;
                    long start = data.Position;

                    foreach (string partName in header.Keys)
                    {
                        if (header[partName].Type != OSDType.Map)
                        {
                            meshData[partName] = header[partName];
                            continue;
                        }

                        OSDMap partInfo = (OSDMap)header[partName];
                        if (partInfo["offset"] < 0 || partInfo["size"] == 0)
                        {
                            meshData[partName] = partInfo;
                            continue;
                        }

                        byte[] part = new byte[partInfo["size"]];
                        Buffer.BlockCopy(assetData, partInfo["offset"] + (int)start, part, 0, part.Length);
                        meshData[partName] = part;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to decode mesh asset", ex);
                meshData = null;
            }

            return meshData;
        }

        private void AddSubMesh(OSD subMeshOsd, ref SimpleMesh holdingMesh)
        {
            if (subMeshOsd is OSDMap subMeshMap)
            {
                if (subMeshMap.ContainsKey("NoGeometry") && ((OSDBoolean)subMeshMap["NoGeometry"]))
                    return;

                holdingMesh.Vertices.AddRange(CollectVertices(subMeshMap));
                holdingMesh.Indices.AddRange(CollectIndices(subMeshMap));
            }
        }

        private List<Vertex> CollectVertices(OSDMap subMeshMap)
        {
            var vertices = new List<Vertex>();

            Vector3 posMax;
            Vector3 posMin;

            if (subMeshMap.ContainsKey("PositionDomain"))
            {
                posMax = ((OSDMap)subMeshMap["PositionDomain"])["Max"];
                posMin = ((OSDMap)subMeshMap["PositionDomain"])["Min"];
            }
            else
            {
                posMax = new Vector3(0.5f, 0.5f, 0.5f);
                posMin = new Vector3(-0.5f, -0.5f, -0.5f);
            }

            if (!subMeshMap.TryGetValue("Position", out var posObj) || !(posObj is OSD posOsd) || posOsd.Type != OSDType.Binary)
                return vertices;

            byte[] posBytes = posOsd.AsBinary();

            byte[]? norBytes = null;
            if (subMeshMap.TryGetValue("Normal", out var normalObj) && normalObj is OSD normalOsd && normalOsd.Type == OSDType.Binary)
                norBytes = normalOsd.AsBinary();

            Vector2 texPosMax = Vector2.Zero;
            Vector2 texPosMin = Vector2.Zero;
            byte[]? texBytes = null;
            if (subMeshMap.TryGetValue("TexCoord0", out var texCoord0Obj) && texCoord0Obj is OSD texCoordOsd && texCoordOsd.Type == OSDType.Binary)
            {
                texBytes = texCoordOsd.AsBinary();
                if (subMeshMap.TryGetValue("TexCoord0Domain", out var domainObj) && domainObj is OSDMap domainMap)
                {
                    texPosMax = domainMap["Max"];
                    texPosMin = domainMap["Min"];
                }
            }

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

                // Normal domain is always [-1, 1] per the SL mesh format spec
                if (norBytes != null && norBytes.Length >= i + 6)
                {
                    ushort nX = Utils.BytesToUInt16(norBytes, i);
                    ushort nY = Utils.BytesToUInt16(norBytes, i + 2);
                    ushort nZ = Utils.BytesToUInt16(norBytes, i + 4);
                    vx.Normal = new Vector3(
                        Utils.UInt16ToFloat(nX, -1f, 1f),
                        Utils.UInt16ToFloat(nY, -1f, 1f),
                        Utils.UInt16ToFloat(nZ, -1f, 1f));
                }

                int vertexIndexOffset = vertices.Count * 4;

                if (texBytes != null && texBytes.Length >= vertexIndexOffset + 4)
                {
                    ushort tX = Utils.BytesToUInt16(texBytes, vertexIndexOffset);
                    ushort tY = Utils.BytesToUInt16(texBytes, vertexIndexOffset + 2);
                    vx.TexCoord = new Vector2(
                        Utils.UInt16ToFloat(tX, texPosMin.X, texPosMax.X),
                        Utils.UInt16ToFloat(tY, texPosMin.Y, texPosMax.Y));
                }

                vertices.Add(vx);
            }

            return vertices;
        }

        private List<ushort> CollectIndices(OSDMap subMeshMap)
        {
            var indices = new List<ushort>();
            byte[] triangleBytes = subMeshMap["TriangleList"];
            for (int i = 0; i < triangleBytes.Length; i += 6)
            {
                indices.Add((ushort)Utils.BytesToUInt16(triangleBytes, i));
                indices.Add((ushort)Utils.BytesToUInt16(triangleBytes, i + 2));
                indices.Add((ushort)Utils.BytesToUInt16(triangleBytes, i + 4));
            }
            return indices;
        }

        private PrimMesh GeneratePrimMesh(Primitive prim, DetailLevel lod, bool viewerMode)
        {
            Primitive.ConstructionData primData = prim.PrimData;
            int sides = 4;
            int hollowsides = 4;

            float profileBegin = primData.ProfileBegin;
            float profileEnd = primData.ProfileEnd;

            bool isSphere = false;

            if ((ProfileCurve)(primData.profileCurve & 0x07) == ProfileCurve.Circle)
            {
                switch (lod)
                {
                    case DetailLevel.Low:
                        sides = 6;
                        break;
                    case DetailLevel.Medium:
                        sides = 12;
                        break;
                    default:
                        sides = 24;
                        break;
                }
            }
            else if ((ProfileCurve)(primData.profileCurve & 0x07) == ProfileCurve.EqualTriangle)
            {
                sides = 3;
            }
            else if ((ProfileCurve)(primData.profileCurve & 0x07) == ProfileCurve.HalfCircle)
            {
                isSphere = true;
                switch (lod)
                {
                    case DetailLevel.Low:
                        sides = 6;
                        break;
                    case DetailLevel.Medium:
                        sides = 12;
                        break;
                    default:
                        sides = 24;
                        break;
                }
                profileBegin = 0.5f * profileBegin + 0.5f;
                profileEnd = 0.5f * profileEnd + 0.5f;
            }

            if (primData.ProfileHole == HoleType.Same)
                hollowsides = sides;
            else if (primData.ProfileHole == HoleType.Circle)
            {
                switch (lod)
                {
                    case DetailLevel.Low:
                        hollowsides = 6;
                        break;
                    case DetailLevel.Medium:
                        hollowsides = 12;
                        break;
                    default:
                        hollowsides = 24;
                        break;
                }
            }
            else if (primData.ProfileHole == HoleType.Triangle)
            {
                hollowsides = 3;
            }

            PrimMesh newPrim = new PrimMesh(sides, profileBegin, profileEnd, primData.ProfileHollow, hollowsides)
            {
                viewerMode = viewerMode,
                sphereMode = isSphere,
                holeSizeX = primData.PathScaleX,
                holeSizeY = primData.PathScaleY,
                pathCutBegin = primData.PathBegin,
                pathCutEnd = primData.PathEnd,
                topShearX = primData.PathShearX,
                topShearY = primData.PathShearY,
                radius = primData.PathRadiusOffset,
                revolutions = primData.PathRevolutions,
                skew = primData.PathSkew
            };

            switch (lod)
            {
                case DetailLevel.Low:
                    newPrim.stepsPerRevolution = 6;
                    break;
                case DetailLevel.Medium:
                    newPrim.stepsPerRevolution = 12;
                    break;
                default:
                    newPrim.stepsPerRevolution = 24;
                    break;
            }

            if (primData.PathCurve == PathCurve.Line || primData.PathCurve == PathCurve.Flexible)
            {
                newPrim.taperX = 1.0f - primData.PathScaleX;
                newPrim.taperY = 1.0f - primData.PathScaleY;
                newPrim.twistBegin = (int)(180 * primData.PathTwistBegin);
                newPrim.twistEnd = (int)(180 * primData.PathTwist);
                newPrim.Extrude(PathType.Linear);
            }
            else
            {
                newPrim.taperX = primData.PathTaperX;
                newPrim.taperY = primData.PathTaperY;
                newPrim.twistBegin = (int)(360 * primData.PathTwistBegin);
                newPrim.twistEnd = (int)(360 * primData.PathTwist);
                newPrim.Extrude(PathType.Circular);
            }

            return newPrim;
        }

        /// <summary>
        /// Returns the first LOD available in <paramref name="meshData"/> that differs from
        /// <paramref name="requested"/>, ordered from highest to lowest visual quality.
        /// Returns <c>null</c> if the asset header was never decoded or no alternative exists.
        /// </summary>
        private static DetailLevel? FindFallbackLod(OSDMap meshData, DetailLevel requested)
        {
            if (!meshData.ContainsKey("asset_header")) return null;
            var preference = new[] { DetailLevel.Highest, DetailLevel.High, DetailLevel.Medium, DetailLevel.Low };
            foreach (var candidate in preference)
            {
                if (candidate == requested) continue;
                string key = LodKey(candidate);
                if (meshData.ContainsKey(key) && meshData[key] is OSDArray arr && arr.Count > 0)
                    return candidate;
            }
            return null;
        }

        private static string LodKey(DetailLevel lod)
        {
            switch (lod)
            {
                case DetailLevel.Highest: return "high_lod";
                case DetailLevel.High:    return "medium_lod";
                case DetailLevel.Medium:  return "low_lod";
                case DetailLevel.Low:     return "lowest_lod";
                default:                  return "high_lod";
            }
        }
    }
}
