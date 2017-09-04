/*
 * Copyright (c) Contributors
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */


using System.Collections.Generic;

namespace LibreMetaverse.PrimMesher
{
    public struct ViewerVertex
    {
        public Coord v;
        public Coord n;
        public UVCoord uv;

        public ViewerVertex(Coord coord, Coord normal, UVCoord uv)
        {
            v = coord;
            n = normal;
            this.uv = uv;
        }
    }

    public struct ViewerPolygon
    {
        public int v1;
        public int v2;
        public int v3;

        public ViewerPolygon(int v1, int v2, int v3)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
    }

    public class VertexIndexer
    {
        public int numPrimFaces;
        public List<List<ViewerPolygon>> viewerPolygons;
        public List<List<ViewerVertex>> viewerVertices;
        private readonly int[][] viewerVertIndices;

        public VertexIndexer()
        {
        }


        public VertexIndexer(PrimMesh primMesh)
        {
            var maxPrimFaceNumber = 0;

            foreach (var vf in primMesh.viewerFaces)
                if (maxPrimFaceNumber < vf.primFaceNumber)
                    maxPrimFaceNumber = vf.primFaceNumber;

            numPrimFaces = maxPrimFaceNumber + 1;

            var numViewerVerts = new int[numPrimFaces];
            var numVertsPerPrimFace = new int[numPrimFaces];

            for (var i = 0; i < numPrimFaces; i++)
            {
                numViewerVerts[i] = 0;
                numVertsPerPrimFace[i] = 0;
            }

            foreach (var vf in primMesh.viewerFaces)
                numVertsPerPrimFace[vf.primFaceNumber] += 3;

            viewerVertices = new List<List<ViewerVertex>>(numPrimFaces);
            viewerPolygons = new List<List<ViewerPolygon>>(numPrimFaces);
            viewerVertIndices = new int[numPrimFaces][];

            // create index lists
            for (var primFaceNumber = 0; primFaceNumber < numPrimFaces; primFaceNumber++)
            {
                //set all indices to -1 to indicate an invalid index
                var vertIndices = new int[primMesh.coords.Count];
                for (var i = 0; i < primMesh.coords.Count; i++)
                    vertIndices[i] = -1;
                viewerVertIndices[primFaceNumber] = vertIndices;

                viewerVertices.Add(new List<ViewerVertex>(numVertsPerPrimFace[primFaceNumber]));
                viewerPolygons.Add(new List<ViewerPolygon>());
            }

            // populate the index lists
            foreach (var vf in primMesh.viewerFaces)
            {
                int v1, v2, v3;

                var vertIndices = viewerVertIndices[vf.primFaceNumber];
                var viewerVerts = viewerVertices[vf.primFaceNumber];

                // add the vertices
                if (vertIndices[vf.coordIndex1] < 0)
                {
                    viewerVerts.Add(new ViewerVertex(vf.v1, vf.n1, vf.uv1));
                    v1 = viewerVerts.Count - 1;
                    vertIndices[vf.coordIndex1] = v1;
                }
                else
                {
                    v1 = vertIndices[vf.coordIndex1];
                }

                if (vertIndices[vf.coordIndex2] < 0)
                {
                    viewerVerts.Add(new ViewerVertex(vf.v2, vf.n2, vf.uv2));
                    v2 = viewerVerts.Count - 1;
                    vertIndices[vf.coordIndex2] = v2;
                }
                else
                {
                    v2 = vertIndices[vf.coordIndex2];
                }

                if (vertIndices[vf.coordIndex3] < 0)
                {
                    viewerVerts.Add(new ViewerVertex(vf.v3, vf.n3, vf.uv3));
                    v3 = viewerVerts.Count - 1;
                    vertIndices[vf.coordIndex3] = v3;
                }
                else
                {
                    v3 = vertIndices[vf.coordIndex3];
                }

                viewerPolygons[vf.primFaceNumber].Add(new ViewerPolygon(v1, v2, v3));
            }
        }
    }
}