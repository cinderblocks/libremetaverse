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
        private Dictionary<long, int> viewerVertexLookup = new Dictionary<long, int>();
        public List<List<ViewerVertex>> viewerVertices = new List<List<ViewerVertex>>();

        public ObjMesh(string path)
        {
            using (var sr = File.OpenText(path))
            {
                ProcessStream(sr);
            }
        }

        public ObjMesh(StreamReader sr)
        {
            if (sr == null) throw new ArgumentNullException(nameof(sr));
            ProcessStream(sr);
        }

        private void ProcessStream(StreamReader s)
        {
            numPrimFaces = 0;

            string raw;
            while ((raw = s.ReadLine()) != null)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                // get first token quickly (command)
                int pos = 0;
                while (pos < line.Length && !char.IsWhiteSpace(line[pos])) pos++;
                var cmd = line.Substring(0, pos);

                // fast path for common commands without regex allocations
                if (string.Equals(cmd, "v", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = SplitWhitespace(line);
                    if (tokens.Length >= 4)
                        coords.Add(ParseCoord(tokens));
                    continue;
                }
                if (string.Equals(cmd, "vt", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = SplitWhitespace(line);
                    if (tokens.Length >= 2)
                        uvs.Add(ParseUVCoord(tokens));
                    continue;
                }
                if (string.Equals(cmd, "vn", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = SplitWhitespace(line);
                    if (tokens.Length >= 4)
                        normals.Add(ParseCoord(tokens));
                    continue;
                }
                if (string.Equals(cmd, "o", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = SplitWhitespace(line);
                    if (tokens.Length > 1)
                        meshName = tokens[1];
                    continue;
                }
                if (string.Equals(cmd, "g", StringComparison.OrdinalIgnoreCase))
                {
                    MakePrimFace();
                    continue;
                }
                if (string.Equals(cmd, "f", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = SplitWhitespace(line);
                    // expecting at least "f v1 v2 v3"
                    if (tokens.Length < 4) continue;

                    // parse triangle (triangulation not supported)
                    int[] vertIdx = new int[3];
                    for (int vertexIndex = 1; vertexIndex <= 3; vertexIndex++)
                    {
                        var indices = tokens[vertexIndex].Split('/');

                        var positionIndex = int.Parse(indices[0], CultureInfo.InvariantCulture) - 1;

                        var texCoordIndex = -1;
                        var normalIndex = -1;

                        if (indices.Length > 1 && int.TryParse(indices[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tIdx))
                            texCoordIndex = tIdx - 1;

                        if (indices.Length > 2 && int.TryParse(indices[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nIdx))
                            normalIndex = nIdx - 1;

                        var hash = HashInts(positionIndex, texCoordIndex, normalIndex);

                        if (viewerVertexLookup.TryGetValue(hash, out var value))
                        {
                            vertIdx[vertexIndex - 1] = value;
                        }
                        else
                        {
                            var vv = new ViewerVertex
                            {
                                v = coords[positionIndex],
                                n = normalIndex >= 0 ? normals[normalIndex] : new Coord(),
                                uv = texCoordIndex >= 0 ? uvs[texCoordIndex] : new UVCoord()
                            };
                            faceVertices.Add(vv);
                            var newIndex = faceVertices.Count - 1;
                            viewerVertexLookup[hash] = newIndex;
                            vertIdx[vertexIndex - 1] = newIndex;
                        }
                    }

                    facePolygons.Add(new ViewerPolygon(vertIdx[0], vertIdx[1], vertIdx[2]));
                    continue;
                }

                // other commands ignored: mtllib, usemtl, s, etc.
            }

            MakePrimFace();
        }

        public VertexIndexer GetVertexIndexer()
        {
            return new VertexIndexer
            {
                numPrimFaces = numPrimFaces,
                viewerPolygons = viewerPolygons,
                viewerVertices = viewerVertices
            };
        }

        private void MakePrimFace()
        {
            if (faceVertices.Count > 0 && facePolygons.Count > 0)
            {
                // store current lists and allocate new ones for the next face
                viewerVertices.Add(faceVertices);
                viewerPolygons.Add(facePolygons);

                faceVertices = new List<ViewerVertex>();
                facePolygons = new List<ViewerPolygon>();

                // reuse and clear the lookup dictionary to avoid re-allocations
                viewerVertexLookup.Clear();

                numPrimFaces++;
            }
        }

        private UVCoord ParseUVCoord(string[] tokens)
        {
            // vt u [v] [w] — we accept missing v
            float u = 0f, v = 0f;
            if (tokens.Length > 1)
                u = float.Parse(tokens[1], CultureInfo.InvariantCulture);
            if (tokens.Length > 2)
                v = float.Parse(tokens[2], CultureInfo.InvariantCulture);
            return new UVCoord(u, v);
        }

        private Coord ParseCoord(string[] tokens)
        {
            return new Coord(
                float.Parse(tokens[1], CultureInfo.InvariantCulture),
                float.Parse(tokens[2], CultureInfo.InvariantCulture),
                float.Parse(tokens[3], CultureInfo.InvariantCulture));
        }

        private static long HashInts(int i1, int i2, int i3)
        {
            // cheap, allocation-free hash combiner
            unchecked
            {
                var hash = i1 + 0x9e3779b9;
                hash = hash ^ (i2 + 0x9e3779b9 + (hash << 6) + (hash >> 2));
                hash = hash ^ (i3 + 0x9e3779b9 + (hash << 6) + (hash >> 2));
                return hash;
            }
        }

        // Small whitespace splitter that avoids the overhead of Regex.Split on every line.
        private static string[] SplitWhitespace(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            var parts = new List<string>(8);
            int i = 0, n = s.Length;
            while (i < n)
            {
                while (i < n && char.IsWhiteSpace(s[i])) i++;
                if (i >= n) break;
                int start = i;
                while (i < n && !char.IsWhiteSpace(s[i])) i++;
                parts.Add(s.Substring(start, i - start));
            }
            return parts.ToArray();
        }
    }
}