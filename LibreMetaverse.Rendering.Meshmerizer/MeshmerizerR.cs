/* Copyright (c) 2008 Robert Adams
 * Copyright (c) 2021-2024, Sjofn LLC. All rights reserved.
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
/*
 * Portions of this code are:
 * Copyright (c) Contributors, http://idealistviewer.org
 * The basic logic of the extrusion code is based on the Idealist viewer code.
 * The Idealist viewer is licensed under the three clause BSD license.
 */
/*
 * MeshmerizerR class implments OpenMetaverse.Rendering.IRendering interface
 * using PrimMesher (http://forge.opensimulator.org/projects/primmesher).
 */

using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using OpenMetaverse.StructuredData;
using LibreMetaverse.PrimMesher;

namespace OpenMetaverse.Rendering
{
    /// <summary>
    /// Meshing code based on the Idealist Viewer (20081213).
    /// </summary>
    [RendererName("MeshmerizerR")]
    public class MeshmerizerR : IRendering
    {
        /// <summary>
        /// Generates a basic mesh structure from a primitive
        /// A 'SimpleMesh' is just the prim's overall shape with no material information.
        /// </summary>
        /// <param name="prim">Primitive to generate the mesh from</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh or null on failure</returns>
        public SimpleMesh GenerateSimpleMesh(Primitive prim, DetailLevel lod)
        {
            PrimMesh newPrim = GeneratePrimMesh(prim, lod, false);
            if (newPrim == null)
                return null;

            SimpleMesh mesh = new SimpleMesh
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
        /// Generates a basic mesh structure from a primitive, adding normals data.
        /// A 'SimpleMesh' is just the prim's overall shape with no material information.
        /// </summary>
        /// <param name="prim">Primitive to generate the mesh from</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh or null on failure</returns>
        public SimpleMesh GenerateSimpleMeshWithNormals(Primitive prim, DetailLevel lod) {
            PrimMesh newPrim = GeneratePrimMesh(prim, lod, true);
	        if(newPrim == null)
		        return null;

            SimpleMesh mesh = new SimpleMesh {
                Path = new Path(),
                Prim = prim,
                Profile = new Profile(),
                Vertices = new List<Vertex>(newPrim.coords.Count)
            };
            
	        for(int i = 0; i < newPrim.coords.Count; i++) {
                Coord c = newPrim.coords[i];
		        // Also saving the normal within the vertice
                Coord n = newPrim.normals[i];
		        mesh.Vertices.Add(new Vertex {Position = new Vector3(c.X, c.Y, c.Z), Normal = new Vector3(n.X, n.Y, n.Z)});
	        }

	        mesh.Indices = new List<ushort>(newPrim.faces.Count * 3);
	        foreach(var face in newPrim.faces) {
                mesh.Indices.Add((ushort) face.v1);
                mesh.Indices.Add((ushort) face.v2);
                mesh.Indices.Add((ushort) face.v3);
            }

	        return mesh;
        }


        /// <summary>
        /// Generates a basic mesh structure from a sculpted primitive.
        /// 'SimpleMesh's have a single mesh and no faces or material information.
        /// </summary>
        /// <param name="prim">Sculpted primitive to generate the mesh from</param>
        /// <param name="sculptTexture">Sculpt texture</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh or null on failure</returns>
        public SimpleMesh GenerateSimpleSculptMesh(Primitive prim, SKBitmap sculptTexture, DetailLevel lod)
        {
            var faceted = GenerateFacetedSculptMesh(prim, sculptTexture, lod);

            if (faceted != null && faceted.Faces.Count == 1)
            {
                Face face = faceted.Faces[0];

                SimpleMesh mesh = new SimpleMesh
                {
                    Indices = face.Indices,
                    Vertices = face.Vertices,
                    Path = faceted.Path,
                    Prim = prim,
                    Profile = faceted.Profile
                };
                mesh.Vertices = face.Vertices;

                return mesh;
            }

            return null;
        }

        /// <summary>
        /// Create a faceted mesh from prim shape parameters.
        /// Generates a a series of faces, each face containing a mesh and
        /// material metadata.
        /// A prim will turn into multiple faces with each being independent
        /// meshes and each having different material information.
        /// </summary>
        /// <param name="prim">Primitive to generate the mesh from</param>
        /// <param name="lod">Level of detail to generate the mesh at</param>
        /// <returns>The generated mesh</returns >
        public FacetedMesh GenerateFacetedMesh(Primitive prim, DetailLevel lod)
        {
            bool isSphere = ((ProfileCurve)(prim.PrimData.profileCurve & 0x07) == ProfileCurve.HalfCircle);
            PrimMesh newPrim = GeneratePrimMesh(prim, lod, true);
            if (newPrim == null)
                return null;

            // copy the vertex information into IRendering structures
            var omvrmesh = new FacetedMesh
            {
                Faces = new List<Face>(),
                Prim = prim,
                Profile = new Profile
                {
                    Faces = new List<ProfileFace>(),
                    Positions = new List<Vector3>()
                },
                Path = new Path {Points = new List<PathPoint>()}
            };
            var indexer = newPrim.GetVertexIndexer();

            for (int i = 0; i < indexer.numPrimFaces; i++)
            {
                Face oface = new Face
                {
                    Vertices = new List<Vertex>(),
                    Indices = new List<ushort>(),
                    TextureFace = prim.Textures.GetFace((uint) i)
                };

                for (int j = 0; j < indexer.viewerVertices[i].Count; j++)
                {
                    var vert = new Vertex();
                    var m = indexer.viewerVertices[i][j];
                    vert.Position = new Vector3(m.v.X, m.v.Y, m.v.Z);
                    vert.Normal = new Vector3(m.n.X, m.n.Y, m.n.Z);
                    vert.TexCoord = new Vector2(m.uv.U, 1.0f - m.uv.V);
                    oface.Vertices.Add(vert);
                }

                for (int j = 0; j < indexer.viewerPolygons[i].Count; j++)
                {
                    var p = indexer.viewerPolygons[i][j];
                    // Skip "degenerate faces" where the same vertex appears twice in the same tri
                    if (p.v1 == p.v2 || p.v1 == p.v2 || p.v2 == p.v3) continue;
                    oface.Indices.Add((ushort)p.v1);
                    oface.Indices.Add((ushort)p.v2);
                    oface.Indices.Add((ushort)p.v3);
                }

                omvrmesh.Faces.Add(oface);
            }

            return omvrmesh;
        }

        /// <summary>
        /// Create a sculpty faceted mesh. The actual scuplt texture is fetched and passed to this
        /// routine since all the context for finding teh texture is elsewhere.
        /// </summary>
        /// <returns>The faceted mesh or null if can't do it</returns>
        public FacetedMesh GenerateFacetedSculptMesh(Primitive prim, SKBitmap scupltTexture, DetailLevel lod)
        {
            LibreMetaverse.PrimMesher.SculptMesh.SculptType smSculptType;
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
            // The lod for sculpties is the resolution of the texture passed.
            // The first guess is 1:1 then lower resolutions after that
            // int mesherLod = (int)Math.Sqrt(scupltTexture.Width * scupltTexture.Height);
            int mesherLod = 32; // number used in Idealist viewer
            switch (lod)
            {
                case DetailLevel.Highest:
                    break;
                case DetailLevel.High:
                    break;
                case DetailLevel.Medium:
                    mesherLod /= 2;
                    break;
                case DetailLevel.Low:
                    mesherLod /= 4;
                    break;
            }
            SculptMesh newMesh =
                new SculptMesh(scupltTexture, smSculptType, mesherLod, true, prim.Sculpt.Mirror, prim.Sculpt.Invert);

            const int numPrimFaces = 1; // a scuplty has only one face

            // copy the vertex information into IRendering structures
            FacetedMesh omvrmesh = new FacetedMesh
            {
                Faces = new List<Face>(),
                Prim = prim,
                Profile = new Profile
                {
                    Faces = new List<ProfileFace>(),
                    Positions = new List<Vector3>()
                },
                Path = new Path {Points = new List<PathPoint>()}
            };

            for (int ii = 0; ii < numPrimFaces; ii++)
            {
                Face oface = new Face
                {
                    Vertices = new List<Vertex>(),
                    Indices = new List<ushort>(),
                    TextureFace = prim.Textures.GetFace((uint) ii)
                };
                int faceVertices = newMesh.coords.Count;

                for (int j = 0; j < faceVertices; j++)
                {
                    var vert = new Vertex
                    {
                        Position = new Vector3(newMesh.coords[j].X, newMesh.coords[j].Y, newMesh.coords[j].Z),
                        Normal = new Vector3(newMesh.normals[j].X, newMesh.normals[j].Y, newMesh.normals[j].Z),
                        TexCoord = new Vector2(newMesh.uvs[j].U, newMesh.uvs[j].V)
                    };
                    oface.Vertices.Add(vert);
                }

                for (int j = 0; j < newMesh.faces.Count; j++)
                {
                    oface.Indices.Add((ushort)newMesh.faces[j].v1);
                    oface.Indices.Add((ushort)newMesh.faces[j].v2);
                    oface.Indices.Add((ushort)newMesh.faces[j].v3);
                }

                if (faceVertices > 0)
                {
                    oface.TextureFace = prim.Textures.FaceTextures[ii] ?? prim.Textures.DefaultTexture;
                    oface.ID = ii;
                    omvrmesh.Faces.Add(oface);
                }
            }

            return omvrmesh;
        }

        /// <summary>
        /// Apply texture coordinate modifications from a
        /// <see cref="OpenMetaverse.Primative.TextureEntryFace"/> to a list of vertices
        /// </summary>
        /// <param name="vertices">Vertex list to modify texture coordinates for</param>
        /// <param name="center">Center-point of the face</param>
        /// <param name="teFace">Face texture parameters</param>
        /// <param name="primScale">Prim scale vector</param>
        public void TransformTexCoords(List<Vertex> vertices, Vector3 center, Primitive.TextureEntryFace teFace, Vector3 primScale)
        {
            // compute trig stuff up front
            float cosineAngle = (float)Math.Cos(teFace.Rotation);
            float sinAngle = (float)Math.Sin(teFace.Rotation);

            for (int ii = 0; ii < vertices.Count; ii++)
            {
                // tex coord comes to us as a number between zero and one
                // transform about the center of the texture
                Vertex vert = vertices[ii];

                // aply planar tranforms to the UV first if applicable
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

        // The mesh reader code is organized so it can be used in several different ways:
        //
        // 1. Fetch the highest detail displayable mesh as a FacetedMesh:
        //      var facetedMesh = GenerateFacetedMeshMesh(prim, meshData);
        // 2. Get the header, examine the submeshes available, and extract the part
        //              desired (good if getting a different LOD of mesh):
        //      OSDMap meshParts = UnpackMesh(meshData);
        //      if (meshParts.ContainsKey("medium_lod"))
        //          var facetedMesh = MeshSubMeshAsFacetedMesh(prim, meshParts["medium_lod"]):
        // 3. Get a simple mesh from one of the submeshes (good if just getting a physics version):
        //      OSDMap meshParts = UnpackMesh(meshData);
        //      Mesh flatMesh = MeshSubMeshAsSimpleMesh(prim, meshParts["physics_mesh"]);
        //
        // "physics_convex" is specially formatted so there is another routine to unpack
        //              that section:
        //      OSDMap meshParts = UnpackMesh(meshData);
        //      if (meshParts.ContainsKey("physics_convex"))
        //          OSMap hullPieces = MeshSubMeshAsConvexHulls(prim, meshParts["physics_convex"]):
        //
        // LL mesh format detailed at http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format

        /// <summary>
        /// Create a mesh faceted mesh from the compressed mesh data.
        /// This returns the highest LOD renderable version of the mesh.
        ///
        /// The actual mesh data is fetched and passed to this
        /// routine since all the context for finding the data is elsewhere.
        /// </summary>
        /// <returns>The faceted mesh or null if can't do it</returns>
        public FacetedMesh GenerateFacetedMeshMesh(Primitive prim, byte[] meshData)
        {
            FacetedMesh ret = null;
            OSDMap meshParts = UnpackMesh(meshData);
            if (meshParts != null)
            {
                byte[] meshBytes = null;
                string[] decreasingLOD = { "high_lod", "medium_lod", "low_lod", "lowest_lod" };
                foreach (string partName in decreasingLOD)
                {
                    if (meshParts.TryGetValue(partName, out var part))
                    {
                        meshBytes = part;
                        break;
                    }
                }
                if (meshBytes != null)
                {
                    ret = MeshSubMeshAsFacetedMesh(prim, meshBytes);
                }

            }
            return ret;
        }

        // A version of GenerateFacetedMeshMesh that takes LOD spec so it's similar in calling convention of
        //    the other Generate* methods.
        public FacetedMesh GenerateFacetedMeshMesh(Primitive prim, byte[] meshData, DetailLevel lod) {
            FacetedMesh ret = null;
            string partName = null;
            switch (lod)
            {
                case DetailLevel.Highest:
                    partName = "high_lod"; break;
                case DetailLevel.High:
                    partName = "medium_lod"; break;
                case DetailLevel.Medium:
                    partName = "low_lod"; break;
                case DetailLevel.Low:
                    partName = "lowest_lod"; break;
            }
            if (partName != null)
            {
                OSDMap meshParts = UnpackMesh(meshData);
                if (meshParts != null)
                {
                    if (meshParts.TryGetValue(partName, out var meshBytes))
                    {
                        if (meshBytes != null)
                        {
                            ret = MeshSubMeshAsFacetedMesh(prim, meshBytes);
                        }
                    }
                }
            }
            return ret;
        }

        // Convert a compressed submesh buffer into a FacetedMesh.
        public FacetedMesh MeshSubMeshAsFacetedMesh(Primitive prim, byte[] compressedMeshData)
        {
            FacetedMesh ret = null;
            OSD meshOSD = Helpers.DecompressOSD(compressedMeshData);

            if (meshOSD is OSDArray meshFaces)
            {
                ret = new FacetedMesh {Faces = new List<Face>()};
                for (int faceIndex = 0; faceIndex < meshFaces.Count; faceIndex++)
                {
                    AddSubMesh(prim, faceIndex, meshFaces[faceIndex], ref ret);
                }
            }
            return ret;
        }


        // Convert a compressed submesh buffer into a SimpleMesh.
        public SimpleMesh MeshSubMeshAsSimpleMesh(Primitive prim, byte[] compressedMeshData)
        {
            SimpleMesh ret = null;
            OSD meshOSD = Helpers.DecompressOSD(compressedMeshData);

            OSDArray meshFaces = meshOSD as OSDArray;
            if (meshOSD != null)
            {
                ret = new SimpleMesh();
                if (meshFaces != null)
                {
                    foreach (OSD subMesh in meshFaces)
                    {
                        AddSubMesh(subMesh, ref ret);
                    }
                }
            }
            return ret;
        }

        public List<List<Vector3>> MeshSubMeshAsConvexHulls(Primitive prim, byte[] compressedMeshData)
        {
            List<List<Vector3>> hulls = new List<List<Vector3>>();
            try {
                OSD convexBlockOsd = Helpers.DecompressOSD(compressedMeshData);

                if (convexBlockOsd is OSDMap convexBlock) {
                    Vector3 min = new Vector3(-0.5f, -0.5f, -0.5f);
                    if (convexBlock.ContainsKey("Min")) min = convexBlock["Min"].AsVector3();
                    Vector3 max = new Vector3(0.5f, 0.5f, 0.5f);
                    if (convexBlock.ContainsKey("Max")) max = convexBlock["Max"].AsVector3();

                    if (convexBlock.ContainsKey("BoundingVerts")) {
                        byte[] boundingVertsBytes = convexBlock["BoundingVerts"].AsBinary();
                        var boundingHull = new List<Vector3>();
                        for (int i = 0; i < boundingVertsBytes.Length;) {
                            ushort uX = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;
                            ushort uY = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;
                            ushort uZ = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;

                            Vector3 pos = new Vector3(
                                Utils.UInt16ToFloat(uX, min.X, max.X),
                                Utils.UInt16ToFloat(uY, min.Y, max.Y),
                                Utils.UInt16ToFloat(uZ, min.Z, max.Z)
                            );

                            boundingHull.Add(pos);
                        }

                        List<Vector3> mBoundingHull = boundingHull;
                    }

                    if (convexBlock.ContainsKey("HullList")) {
                        byte[] hullList = convexBlock["HullList"].AsBinary();

                        byte[] posBytes = convexBlock["Positions"].AsBinary();

                        int posNdx = 0;

                        foreach (byte cnt in hullList) {
                            int count = cnt == 0 ? 256 : cnt;
                            List<Vector3> hull = new List<Vector3>();

                            for (int i = 0; i < count; i++) {
                                ushort uX = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;
                                ushort uY = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;
                                ushort uZ = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;

                                Vector3 pos = new Vector3(
                                    Utils.UInt16ToFloat(uX, min.X, max.X),
                                    Utils.UInt16ToFloat(uY, min.Y, max.Y),
                                    Utils.UInt16ToFloat(uZ, min.Z, max.Z)
                                );

                                hull.Add(pos);
                            }

                            hulls.Add(hull);
                        }
                    }
                }
            }
            catch (Exception) {
                // Logger.Log.WarnFormat("{0} exception decoding convex block: {1}", LogHeader, e);
            }
            return hulls;
        }

        // Add the submesh to the passed SimpleMesh
        private void AddSubMesh(OSD subMeshOsd, ref SimpleMesh holdingMesh) {
            if (subMeshOsd is OSDMap subMeshMap)
            {
                // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
                // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
                // geometry for this submesh.
                if (subMeshMap.ContainsKey("NoGeometry") && ((OSDBoolean)subMeshMap["NoGeometry"]))
                    return;

                holdingMesh.Vertices.AddRange(CollectVertices(subMeshMap));
                holdingMesh.Indices.AddRange(CollectIndices(subMeshMap));
            }
        }

        // Add the submesh to the passed FacetedMesh as a new face.
        private void AddSubMesh(Primitive prim, int faceIndex, OSD subMeshOsd, ref FacetedMesh holdingMesh) {
            if (subMeshOsd is OSDMap subMesh)
            {
                // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
                // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
                // geometry for this submesh.
                if (subMesh.ContainsKey("NoGeometry") && ((OSDBoolean)subMesh["NoGeometry"]))
                    return;

                Face oface = new Face
                {
                    ID = faceIndex,
                    Vertices = new List<Vertex>(),
                    Indices = new List<ushort>(),
                    TextureFace = prim.Textures.GetFace((uint) faceIndex)
                };

                OSDMap subMeshMap = subMesh;

                oface.Vertices = CollectVertices(subMeshMap);
                oface.Indices = CollectIndices(subMeshMap);

                holdingMesh.Faces.Add(oface);
            }
        }

        private List<Vertex> CollectVertices(OSDMap subMeshMap)
        {
            List<Vertex> vertices = new List<Vertex>();

            Vector3 posMax;
            Vector3 posMin;

            // If PositionDomain is not specified, the default is from -0.5 to 0.5
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

            // Vertex positions
            byte[] posBytes = subMeshMap["Position"];

            // Normals
            byte[] norBytes = null;
            if (subMeshMap.TryGetValue("Normal", out var normal))
            {
                norBytes = normal;
            }

            // UV texture map
            Vector2 texPosMax = Vector2.Zero;
            Vector2 texPosMin = Vector2.Zero;
            byte[] texBytes = null;
            if (subMeshMap.TryGetValue("TexCoord0", out var texCoord0))
            {
                texBytes = texCoord0;
                texPosMax = ((OSDMap)subMeshMap["TexCoord0Domain"])["Max"];
                texPosMin = ((OSDMap)subMeshMap["TexCoord0Domain"])["Min"];
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


                if (norBytes != null && norBytes.Length >= i + 4)
                {
                    ushort nX = Utils.BytesToUInt16(norBytes, i);
                    ushort nY = Utils.BytesToUInt16(norBytes, i + 2);
                    ushort nZ = Utils.BytesToUInt16(norBytes, i + 4);

                    vx.Normal = new Vector3(
                        Utils.UInt16ToFloat(nX, posMin.X, posMax.X),
                        Utils.UInt16ToFloat(nY, posMin.Y, posMax.Y),
                        Utils.UInt16ToFloat(nZ, posMin.Z, posMax.Z));
                }

                var vertexIndexOffset = vertices.Count * 4;

                if (texBytes != null && texBytes.Length >= vertexIndexOffset + 4)
                {
                    ushort tX = Utils.BytesToUInt16(texBytes, vertexIndexOffset);
                    ushort tY = Utils.BytesToUInt16(texBytes, vertexIndexOffset + 2);

                    vx.TexCoord = new Vector2(
                        Utils.UInt16ToFloat(tX, texPosMin.X, texPosMax.X),
                        Utils.UInt16ToFloat(tY, texPosMin.Y, texPosMax.Y));
                }

                vertices. Add(vx);
            }
            return vertices;
        }

        private List<ushort> CollectIndices(OSDMap subMeshMap)
        {
            List<ushort> indices = new List<ushort>();

            byte[] triangleBytes = subMeshMap["TriangleList"];
            for (int i = 0; i < triangleBytes.Length; i += 6)
            {
                ushort v1 = (ushort)(Utils.BytesToUInt16(triangleBytes, i));
                indices.Add(v1);
                ushort v2 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 2));
                indices.Add(v2);
                ushort v3 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 4));
                indices.Add(v3);
            }
            return indices;
        }

        /// <summary>Decodes mesh asset.</summary>
        /// <returns>OSDMap of all submeshes in the mesh. The value of the submesh name
        /// is the uncompressed data for that mesh.
        /// The OSDMap is made up of the asset_header section (which includes a lot of stuff)
        /// plus each of the submeshes unpacked into compressed byte arrays.</returns>
        public OSDMap UnpackMesh(byte[] assetData)
        {
            OSDMap meshData = new OSDMap();
            try
            {
                using (MemoryStream data = new MemoryStream(assetData))
                {
                    OSDMap header = (OSDMap)OSDParser.DeserializeLLSDBinary(data);
                    meshData["asset_header"] = header;
                    long start = data.Position;

                    foreach(string partName in header.Keys)
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
                        // meshData[partName] = Helpers.ZDecompressOSD(part);   // Do decompression at unpack time
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to decode mesh asset", Helpers.LogLevel.Error, ex);
                meshData = null;
            }
            return meshData;
        }

        // Local routine to create a mesh from prim parameters.
        // Collects parameters and calls PrimMesher to create all the faces of the prim.
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
                sides = 3;
            else if ((ProfileCurve)(primData.profileCurve & 0x07) == ProfileCurve.HalfCircle)
            {
                // half circle, prim is a sphere
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
                hollowsides = 3;

            PrimMesh newPrim =
                new PrimMesh(sides, profileBegin, profileEnd, primData.ProfileHollow, hollowsides)
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
                newPrim.ExtrudeLinear();
            }
            else
            {
                newPrim.taperX = primData.PathTaperX;
                newPrim.taperY = primData.PathTaperY;
                newPrim.twistBegin = (int)(360 * primData.PathTwistBegin);
                newPrim.twistEnd = (int)(360 * primData.PathTwist);
                newPrim.ExtrudeCircular();
            }

            return newPrim;
        }

        /// <summary>
        /// Method for generating mesh Face from a heightmap
        /// </summary>
        /// <param name="zMap">Two dimension array of floats containing height information</param>
        /// <param name="xBegin">Starting value for X</param>
        /// <param name="xEnd">Max value for X</param>
        /// <param name="yBegin">Starting value for Y</param>
        /// <param name="yEnd">Max value of Y</param>
        /// <returns></returns>
        public Face TerrainMesh(float[,] zMap, float xBegin, float xEnd, float yBegin, float yEnd)
        {
            SculptMesh newMesh = new SculptMesh(zMap, xBegin, xEnd, yBegin, yEnd, true);
            Face terrain = new Face();
            int faceVertices = newMesh.coords.Count;
            terrain.Vertices = new List<Vertex>(faceVertices);
            terrain.Indices = new List<ushort>(newMesh.faces.Count * 3);

            for (int j = 0; j < faceVertices; j++)
            {
                var vert = new Vertex
                {
                    Position = new Vector3(newMesh.coords[j].X, newMesh.coords[j].Y, newMesh.coords[j].Z),
                    Normal = new Vector3(newMesh.normals[j].X, newMesh.normals[j].Y, newMesh.normals[j].Z),
                    TexCoord = new Vector2(newMesh.uvs[j].U, newMesh.uvs[j].V)
                };
                terrain.Vertices.Add(vert);
            }

            for (int j = 0; j < newMesh.faces.Count; j++)
            {
                terrain.Indices.Add((ushort)newMesh.faces[j].v1);
                terrain.Indices.Add((ushort)newMesh.faces[j].v2);
                terrain.Indices.Add((ushort)newMesh.faces[j].v3);
            }

            return terrain;
        }
    }
}
