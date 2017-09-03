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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace PrimMesher
{
    public class ObjMesh
    {
        List<Coord> coords = new List<Coord>();
        List<Coord> normals = new List<Coord>();
        List<UVCoord> uvs = new List<UVCoord>();

        public string meshName = string.Empty;
        public List<List<ViewerVertex>> viewerVertices = new List<List<ViewerVertex>>();
        public List<List<ViewerPolygon>> viewerPolygons = new List<List<ViewerPolygon>>();

        List<ViewerVertex> faceVertices = new List<ViewerVertex>();
        List<ViewerPolygon> facePolygons = new List<ViewerPolygon>();
        public int numPrimFaces;

        Dictionary<int, int> viewerVertexLookup = new Dictionary<int, int>();

        public ObjMesh(string path)
        {
            ProcessStream(new StreamReader(path));
        }


        public ObjMesh(StreamReader sr)
        {
            ProcessStream(sr);
        }


        private void ProcessStream(StreamReader s)
        {
            numPrimFaces = 0;

            while (!s.EndOfStream)
            {
                string line = s.ReadLine().Trim();
                string[] tokens = Regex.Split(line, @"\s+");

                // Skip blank lines and comments
                if (tokens.Length > 0 && tokens[0] != String.Empty && !tokens[0].StartsWith("#"))
                    ProcessTokens(tokens);
            }
            MakePrimFace();
        }

        public VertexIndexer GetVertexIndexer()
        {
            VertexIndexer vi = new VertexIndexer();
            vi.numPrimFaces = this.numPrimFaces;
            vi.viewerPolygons = this.viewerPolygons;
            vi.viewerVertices = this.viewerVertices;

            return vi;
        }


        private void ProcessTokens(string[] tokens)
        {
            switch (tokens[0].ToLower())
            {
                case "o":
                    meshName = tokens[1];
                    break;

                case "v":
                    coords.Add(ParseCoord(tokens));
                    break;

                case "vt":
                    {
                        uvs.Add(ParseUVCoord(tokens));
                        break;
                    }

                case "vn":
                    normals.Add(ParseCoord(tokens));
                    break;

                case "g":

                    MakePrimFace();

                    break;

                case "s":
                    break;

                case "f":

                    int[] vertIndices = new int[3];

                    for (int vertexIndex = 1; vertexIndex <= 3; vertexIndex++)
                    {
                        string[] indices = tokens[vertexIndex].Split('/');

                        int positionIndex = int.Parse(indices[0],
                            CultureInfo.InvariantCulture) - 1;

                        int texCoordIndex = -1;
                        int normalIndex = -1;

                        if (indices.Length > 1)
                        {

                            if (int.TryParse(indices[1], System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out texCoordIndex))
                                texCoordIndex--;
                            else texCoordIndex = -1;

                        }

                        if (indices.Length > 2)
                        {
                            if (int.TryParse(indices[1], System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out normalIndex))
                                normalIndex--;
                            else normalIndex = -1;
                        }

                        int hash = hashInts(positionIndex, texCoordIndex, normalIndex);

                        if (viewerVertexLookup.ContainsKey(hash))
                            vertIndices[vertexIndex - 1] = viewerVertexLookup[hash];
                        else
                        {
                            ViewerVertex vv = new ViewerVertex();
                            vv.v = coords[positionIndex];
                            if (normalIndex > -1)
                                vv.n = normals[normalIndex];
                            if (texCoordIndex > -1)
                                vv.uv = uvs[texCoordIndex];
                            faceVertices.Add(vv);
                            vertIndices[vertexIndex - 1] = viewerVertexLookup[hash] = faceVertices.Count - 1;
                        }
                    }

                    facePolygons.Add(new ViewerPolygon(vertIndices[0], vertIndices[1], vertIndices[2]));
                    break;

                case "mtllib":
                    break;

                case "usemtl":
                    break;

                default:
                    break;
            }
        }


        private void MakePrimFace()
        {
            if (faceVertices.Count > 0 && facePolygons.Count > 0)
            {
                viewerVertices.Add(faceVertices);
                faceVertices = new List<ViewerVertex>();
                viewerPolygons.Add(facePolygons);

                facePolygons = new List<ViewerPolygon>();

                viewerVertexLookup = new Dictionary<int, int>();

                numPrimFaces++;
            }
        }

        private UVCoord ParseUVCoord(string[] tokens)
        {
            return new UVCoord(
                float.Parse(tokens[1], CultureInfo.InvariantCulture),
                float.Parse(tokens[1], CultureInfo.InvariantCulture));
        }

        private Coord ParseCoord(string[] tokens)
        {
            return new Coord(
                float.Parse(tokens[1], CultureInfo.InvariantCulture),
                float.Parse(tokens[2], CultureInfo.InvariantCulture),
                float.Parse(tokens[3], CultureInfo.InvariantCulture));
        }

        private int hashInts(int i1, int i2, int i3)
        {
            return (i1.ToString() + " " + i2.ToString() + " " + i3.ToString()).GetHashCode();
        }
    }
}
