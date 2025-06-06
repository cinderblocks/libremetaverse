﻿/*
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
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace LibreMetaverse.PrimMesher
{
    public class ObjMesh
    {
        private readonly List<Coord> coords = new List<Coord>();
        private List<ViewerPolygon> facePolygons = new List<ViewerPolygon>();

        private List<ViewerVertex> faceVertices = new List<ViewerVertex>();

        public string meshName = string.Empty;
        private readonly List<Coord> normals = new List<Coord>();
        public int numPrimFaces;
        private readonly List<UVCoord> uvs = new List<UVCoord>();
        public List<List<ViewerPolygon>> viewerPolygons = new List<List<ViewerPolygon>>();

        private Dictionary<int, int> viewerVertexLookup = new Dictionary<int, int>();
        public List<List<ViewerVertex>> viewerVertices = new List<List<ViewerVertex>>();

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
                var line = s.ReadLine()?.Trim();
                var tokens = Regex.Split(line, @"\s+");

                // Skip blank lines and comments
                if (tokens.Length > 0 && tokens[0] != string.Empty && !tokens[0].StartsWith("#"))
                    ProcessTokens(tokens);
            }
            MakePrimFace();
        }

        public VertexIndexer GetVertexIndexer()
        {
            var vi = new VertexIndexer();
            vi.numPrimFaces = numPrimFaces;
            vi.viewerPolygons = viewerPolygons;
            vi.viewerVertices = viewerVertices;

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

                    var vertIndices = new int[3];

                    for (var vertexIndex = 1; vertexIndex <= 3; vertexIndex++)
                    {
                        var indices = tokens[vertexIndex].Split('/');

                        var positionIndex = int.Parse(indices[0],
                                                CultureInfo.InvariantCulture) - 1;

                        var texCoordIndex = -1;
                        var normalIndex = -1;

                        if (indices.Length > 1)
                            if (int.TryParse(indices[1], NumberStyles.Integer, CultureInfo.InvariantCulture,
                                out texCoordIndex))
                                texCoordIndex--;
                            else texCoordIndex = -1;

                        if (indices.Length > 2)
                            if (int.TryParse(indices[1], NumberStyles.Integer, CultureInfo.InvariantCulture,
                                out normalIndex))
                                normalIndex--;
                            else normalIndex = -1;

                        var hash = hashInts(positionIndex, texCoordIndex, normalIndex);

                        if (viewerVertexLookup.TryGetValue(hash, out var value))
                        {
                            vertIndices[vertexIndex - 1] = viewerVertexLookup[hash];
                        }
                        else
                        {
                            var vv = new ViewerVertex {v = coords[positionIndex]};
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
            return (i1 + " " + i2 + " " + i3).GetHashCode();
        }
    }
}