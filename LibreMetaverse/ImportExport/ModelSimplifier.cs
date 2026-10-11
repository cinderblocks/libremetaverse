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
using System.Threading;
using LibreMetaverse.Rendering;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Reduces a face to fewer triangles by collapsing edges, cheapest first, where the cost of moving a vertex is the squared
    /// distance it would be from the planes of the triangles it touches (Garland and Heckbert's quadric error), worked out at
    /// the prim's real size so a long thin prim is not judged in the unit cube.
    /// </summary>
    /// <remarks>
    /// A collapse moves one vertex onto another, so every vertex that comes out is one that went in, with its own texture
    /// coordinates, normal and weights. Vertices that share a position (the two sides of a texture seam) are one vertex as far as the
    /// shape goes, and a seam costs extra to collapse. The edges around the face are kept in place, a collapse that would flip a
    /// triangle or fold the surface over is refused, and the last triangle is never taken.
    /// </remarks>
    internal static class ModelSimplifier
    {
        private struct PositionKey : IEquatable<PositionKey>
        {
            public int X, Y, Z;

            public PositionKey(Vector3 v)
            {
                // Adding zero turns -0 into 0, so the two are the same position
                X = BitConverter.ToInt32(BitConverter.GetBytes(v.X + 0f), 0);
                Y = BitConverter.ToInt32(BitConverter.GetBytes(v.Y + 0f), 0);
                Z = BitConverter.ToInt32(BitConverter.GetBytes(v.Z + 0f), 0);
            }

            public bool Equals(PositionKey other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object? obj) => obj is PositionKey other && Equals(other);
            public override int GetHashCode()
            {
                // The bits of the floats on a grid differ in their top bits only, so they have to be mixed well
                unchecked
                {
                    uint h = (uint)X;
                    h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
                    h = (h ^ (uint)Y) * 0xC2B2AE35u;
                    h = (h ^ (h >> 13)) ^ (uint)Z;
                    h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
                    h ^= h >> 13;
                    h *= 0xC2B2AE35u;
                    h ^= h >> 16;
                    return (int)h;
                }
            }
        }

        /// <summary>
        /// Hashes an edge. The default hash of a long folds its halves together with an exclusive or, and the two ends of an
        /// edge in a grid are close in number, so nearly every edge of a mesh like that would land in a handful of buckets.
        /// </summary>
        private sealed class EdgeComparer : IEqualityComparer<long>
        {
            public static readonly EdgeComparer Instance = new EdgeComparer();

            public bool Equals(long x, long y) => x == y;

            public int GetHashCode(long key) => unchecked((int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> 32));
        }

        private struct Candidate
        {
            public double Cost;
            public long Sequence;
            public int From, To, FromVersion, ToVersion;
        }

        /// <summary>Returns a face with at most <paramref name="targetTriangles"/> triangles if it can be done, or the face itself</summary>
        internal static ModelFace Simplify(ModelFace face, int targetTriangles, Vector3 primScale, CancellationToken cancellationToken)
        {
            int triangleCount = face.Indices.Count / 3;
            if (targetTriangles < 1) targetTriangles = 1;
            if (triangleCount <= targetTriangles) return face;

            var state = new State(face, primScale, cancellationToken);
            if (state.Alive == 0) return face;
            state.Reduce(targetTriangles);
            return state.Build(face);
        }

        private sealed class State
        {
            private readonly ModelFace _face;
            private readonly CancellationToken _cancel;

            // Positions: one for each distinct place, at the prim's real size
            private readonly List<double[]> _position = new List<double[]>();
            private readonly List<double[]> _quadric = new List<double[]>();
            private readonly List<List<int>> _triangles = new List<List<int>>();
            private readonly List<bool> _boundary = new List<bool>();
            private readonly List<bool> _locked = new List<bool>();
            private readonly List<bool> _seam = new List<bool>();
            private readonly List<bool> _present = new List<bool>();
            private readonly List<int> _version = new List<int>();

            // Corners: the vertex and the position of each corner of each triangle
            private readonly int[] _cornerVertex;
            private readonly int[] _cornerPosition;
            private readonly bool[] _alive;

            private readonly List<Candidate> _heap = new List<Candidate>();
            private long _sequence;

            public int Alive { get; private set; }

            public State(ModelFace face, Vector3 scale, CancellationToken cancellationToken)
            {
                _face = face;
                _cancel = cancellationToken;

                double sx = scale.X > 0f ? scale.X : 1.0, sy = scale.Y > 0f ? scale.Y : 1.0, sz = scale.Z > 0f ? scale.Z : 1.0;
                if (scale.X <= 0f && scale.Y <= 0f && scale.Z <= 0f) { sx = sy = sz = 1.0; }

                var ids = new Dictionary<PositionKey, int>();
                var vertexPosition = new int[face.Vertices.Count];
                var wedges = new List<HashSet<int>>();
                for (int v = 0; v < vertexPosition.Length; v++)
                {
                    var key = new PositionKey(face.Vertices[v].Position);
                    if (!ids.TryGetValue(key, out int id))
                    {
                        id = _position.Count;
                        ids[key] = id;
                        var p = face.Vertices[v].Position;
                        _position.Add(new[] { p.X * sx, p.Y * sy, p.Z * sz });
                        _quadric.Add(new double[10]);
                        _triangles.Add(new List<int>());
                        _boundary.Add(false);
                        _locked.Add(false);
                        _seam.Add(false);
                        _present.Add(true);
                        _version.Add(0);
                        wedges.Add(new HashSet<int>());
                    }
                    vertexPosition[v] = id;
                    wedges[id].Add(v);
                }
                for (int i = 0; i < wedges.Count; i++) _seam[i] = wedges[i].Count > 1;

                int count = face.Indices.Count / 3;
                _cornerVertex = new int[count * 3];
                _cornerPosition = new int[count * 3];
                _alive = new bool[count];

                for (int t = 0; t < count; t++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int v = (int)face.Indices[t * 3 + c];
                        _cornerVertex[t * 3 + c] = v;
                        _cornerPosition[t * 3 + c] = vertexPosition[v];
                    }

                    int a = _cornerPosition[t * 3], b = _cornerPosition[t * 3 + 1], d = _cornerPosition[t * 3 + 2];
                    if (a == b || b == d || a == d) continue;                  // not a triangle
                    if (Area(a, b, d) <= 1e-18) continue;                      // no area

                    _alive[t] = true;
                    Alive++;
                    _triangles[a].Add(t); _triangles[b].Add(t); _triangles[d].Add(t);
                }

                if (Alive == 0) return;
                BuildQuadrics(count);
                BuildCandidates(count);
            }

            private double Area(int a, int b, int c)
            {
                var n = Cross(Sub(_position[b], _position[a]), Sub(_position[c], _position[a]));
                return 0.5 * Math.Sqrt(Dot(n, n));
            }

            private void BuildQuadrics(int count)
            {
                var edgeTriangles = new Dictionary<long, int>(EdgeComparer.Instance);
                for (int t = 0; t < count; t++)
                {
                    if (!_alive[t]) continue;
                    int a = _cornerPosition[t * 3], b = _cornerPosition[t * 3 + 1], c = _cornerPosition[t * 3 + 2];

                    var n = Cross(Sub(_position[b], _position[a]), Sub(_position[c], _position[a]));
                    double twiceArea = Math.Sqrt(Dot(n, n));
                    var unit = new[] { n[0] / twiceArea, n[1] / twiceArea, n[2] / twiceArea };
                    double d = -Dot(unit, _position[a]);
                    double weight = twiceArea * 0.5;
                    foreach (var p in new[] { a, b, c }) AddPlane(_quadric[p], unit, d, weight);

                    CountEdge(edgeTriangles, a, b); CountEdge(edgeTriangles, b, c); CountEdge(edgeTriangles, c, a);
                }

                // An edge with one triangle is an edge of the face: it gets a plane of its own, standing across it, so
                // collapsing along it is cheap and moving it in or out is not. An edge with more than two is left alone.
                for (int t = 0; t < count; t++)
                {
                    if (!_alive[t]) continue;
                    int[] corner = { _cornerPosition[t * 3], _cornerPosition[t * 3 + 1], _cornerPosition[t * 3 + 2] };
                    var n = Cross(Sub(_position[corner[1]], _position[corner[0]]), Sub(_position[corner[2]], _position[corner[0]]));
                    double length = Math.Sqrt(Dot(n, n));
                    var unit = new[] { n[0] / length, n[1] / length, n[2] / length };

                    for (int e = 0; e < 3; e++)
                    {
                        int u = corner[e], v = corner[(e + 1) % 3];
                        int shared = edgeTriangles[EdgeKey(u, v)];
                        if (shared > 2)
                        {
                            _locked[u] = true; _locked[v] = true;
                        }
                        else if (shared == 1)
                        {
                            _boundary[u] = true; _boundary[v] = true;
                            var edge = Sub(_position[v], _position[u]);
                            var across = Cross(edge, unit);
                            double acrossLength = Math.Sqrt(Dot(across, across));
                            if (!(acrossLength > 0)) continue;
                            across = new[] { across[0] / acrossLength, across[1] / acrossLength, across[2] / acrossLength };
                            double d = -Dot(across, _position[u]);
                            double weight = Dot(edge, edge) * 4;
                            AddPlane(_quadric[u], across, d, weight);
                            AddPlane(_quadric[v], across, d, weight);
                        }
                    }
                }
            }

            private static void CountEdge(Dictionary<long, int> edges, int a, int b)
            {
                long key = EdgeKey(a, b);
                edges.TryGetValue(key, out int count);
                edges[key] = count + 1;
            }

            private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

            private static void AddPlane(double[] q, double[] n, double d, double weight)
            {
                q[0] += weight * n[0] * n[0]; q[1] += weight * n[0] * n[1]; q[2] += weight * n[0] * n[2]; q[3] += weight * n[0] * d;
                q[4] += weight * n[1] * n[1]; q[5] += weight * n[1] * n[2]; q[6] += weight * n[1] * d;
                q[7] += weight * n[2] * n[2]; q[8] += weight * n[2] * d;
                q[9] += weight * d * d;
            }

            private static double Error(double[] q, double[] p)
            {
                double x = p[0], y = p[1], z = p[2];
                return q[0] * x * x + 2 * q[1] * x * y + 2 * q[2] * x * z + 2 * q[3] * x +
                       q[4] * y * y + 2 * q[5] * y * z + 2 * q[6] * y +
                       q[7] * z * z + 2 * q[8] * z + q[9];
            }

            private void BuildCandidates(int count)
            {
                var done = new HashSet<long>(EdgeComparer.Instance);
                for (int t = 0; t < count; t++)
                {
                    if (!_alive[t]) continue;
                    for (int e = 0; e < 3; e++)
                    {
                        int u = _cornerPosition[t * 3 + e], v = _cornerPosition[t * 3 + (e + 1) % 3];
                        if (done.Add(EdgeKey(u, v))) Push(u, v);
                    }
                }
            }

            /// <summary>Queues collapsing either end of an edge onto the other</summary>
            private void Push(int u, int v)
            {
                Push(u, v, Cost(u, v));
                Push(v, u, Cost(v, u));
            }

            private void Push(int from, int to, double cost)
            {
                Add(new Candidate { Cost = cost, Sequence = _sequence++, From = from, To = to, FromVersion = _version[from], ToVersion = _version[to] });
            }

            private double Cost(int from, int to)
            {
                // The error is linear in the quadric, so the two need not be added first
                double error = Math.Max(0, Error(_quadric[from], _position[to]) + Error(_quadric[to], _position[to]));
                var edge = Sub(_position[to], _position[from]);

                // Where nothing is lost the shortest edges go first, and a seam is not collapsed for little
                double cost = error + 1e-12 * Dot(edge, edge);
                if (_seam[from] || _seam[to]) cost *= 3;
                return cost;
            }

            #region Heap (cheapest first; ties by when they were queued, so the same face always gives the same result)

            private static bool Before(Candidate a, Candidate b) => a.Cost < b.Cost || (a.Cost == b.Cost && a.Sequence < b.Sequence);

            private void Add(Candidate c)
            {
                _heap.Add(c);
                int i = _heap.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (!Before(_heap[i], _heap[parent])) break;
                    var swap = _heap[i]; _heap[i] = _heap[parent]; _heap[parent] = swap;
                    i = parent;
                }
            }

            private Candidate Take()
            {
                var top = _heap[0];
                var last = _heap[_heap.Count - 1];
                _heap.RemoveAt(_heap.Count - 1);
                if (_heap.Count > 0)
                {
                    _heap[0] = last;
                    int i = 0;
                    while (true)
                    {
                        int left = 2 * i + 1, right = left + 1, best = i;
                        if (left < _heap.Count && Before(_heap[left], _heap[best])) best = left;
                        if (right < _heap.Count && Before(_heap[right], _heap[best])) best = right;
                        if (best == i) break;
                        var swap = _heap[i]; _heap[i] = _heap[best]; _heap[best] = swap;
                        i = best;
                    }
                }
                return top;
            }

            #endregion Heap

            public void Reduce(int target)
            {
                int steps = 0;
                while (Alive > target && _heap.Count > 0)
                {
                    if ((++steps & 1023) == 0) _cancel.ThrowIfCancellationRequested();

                    var candidate = Take();
                    if (!_present[candidate.From] || !_present[candidate.To] ||
                        _version[candidate.From] != candidate.FromVersion || _version[candidate.To] != candidate.ToVersion)
                        continue;

                    Collapse(candidate.From, candidate.To);
                }
            }

            private void Live(int position)
            {
                var list = _triangles[position];
                int kept = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (_alive[list[i]]) list[kept++] = list[i];
                }
                if (kept < list.Count) list.RemoveRange(kept, list.Count - kept);
            }

            private bool Collapse(int from, int to)
            {
                if (_locked[from] || _locked[to]) return false;
                Live(from);
                Live(to);

                var shared = new List<int>();
                foreach (var t in _triangles[from])
                {
                    if (Has(t, to)) shared.Add(t);
                }
                if (shared.Count == 0 || shared.Count > 2) return false;

                // The edge of a face only collapses along itself, and an edge that joins two edges of the face would pinch it shut
                if (_boundary[from] && shared.Count != 1) return false;
                if (_boundary[from] && _boundary[to] && shared.Count == 2) return false;
                if (Alive - shared.Count < 1) return false;

                // The surface stays a surface only if the two ends have no neighbors in common but the points opposite the edge
                Neighbors(from, _around);
                Neighbors(to, _other);
                int common = 0;
                _stamp++;
                foreach (var n in _other) _marked[n] = _stamp;
                foreach (var n in _around)
                {
                    if (_marked[n] == _stamp) common++;
                }
                if (common != shared.Count) return false;

                // Nothing that is left may turn over or be squashed flat
                foreach (var t in _triangles[from])
                {
                    if (shared.Contains(t)) continue;
                    if (!Keeps(t, from, to)) return false;
                }

                // Two triangles on the same three corners would be a surface with no thickness, which is what a closed
                // shape collapses to when too little is left of it
                foreach (var t in _triangles[from])
                {
                    if (shared.Contains(t)) continue;
                    foreach (var u in _triangles[to])
                    {
                        if (_alive[u] && !shared.Contains(u) && SameCorners(t, from, to, u)) return false;
                    }
                }

                // Which version of the vertex each corner moved to is whichever looks most like what it was
                var wedges = new List<int>();
                foreach (var t in _triangles[to])
                {
                    for (int c = 0; c < 3; c++)
                    {
                        if (_cornerPosition[t * 3 + c] == to && !wedges.Contains(_cornerVertex[t * 3 + c])) wedges.Add(_cornerVertex[t * 3 + c]);
                    }
                }

                foreach (var t in shared)
                {
                    _alive[t] = false;
                    Alive--;
                }
                foreach (var t in _triangles[from])
                {
                    if (!_alive[t]) continue;
                    for (int c = 0; c < 3; c++)
                    {
                        if (_cornerPosition[t * 3 + c] != from) continue;
                        _cornerVertex[t * 3 + c] = Closest(_cornerVertex[t * 3 + c], wedges);
                        _cornerPosition[t * 3 + c] = to;
                    }
                    _triangles[to].Add(t);
                }

                for (int i = 0; i < 10; i++) _quadric[to][i] += _quadric[from][i];
                _boundary[to] = _boundary[to] || _boundary[from];
                _seam[to] = _seam[to] || _seam[from];
                _present[from] = false;
                _triangles[from].Clear();
                _version[from]++;
                _version[to]++;

                Live(to);
                Neighbors(to, _around);
                foreach (var n in _around) Push(to, n);
                return true;
            }

            // Would triangle t, with `from` at `to`, have the same three corners as triangle u?
            private bool SameCorners(int t, int from, int to, int u)
            {
                for (int c = 0; c < 3; c++)
                {
                    int p = _cornerPosition[t * 3 + c];
                    if (p == from) p = to;
                    if (!Has(u, p)) return false;
                }
                return true;
            }

            private bool Has(int triangle, int position)
            {
                return _cornerPosition[triangle * 3] == position || _cornerPosition[triangle * 3 + 1] == position ||
                       _cornerPosition[triangle * 3 + 2] == position;
            }

            // The positions that share a triangle with this one, each once (a few, so a list is quicker than a set)
            private readonly List<int> _around = new List<int>();
            private readonly List<int> _other = new List<int>();
            private int[] _marked = Array.Empty<int>();
            private int _stamp;

            private void Neighbors(int position, List<int> result)
            {
                // A stamp on each position seen says so in no time, however many triangles meet here
                if (_marked.Length == 0) _marked = new int[_position.Count];
                result.Clear();
                _stamp++;
                _marked[position] = _stamp;
                foreach (var t in _triangles[position])
                {
                    if (!_alive[t]) continue;
                    for (int c = 0; c < 3; c++)
                    {
                        int p = _cornerPosition[t * 3 + c];
                        if (_marked[p] != _stamp) { _marked[p] = _stamp; result.Add(p); }
                    }
                }
            }

            // Would the triangle still face about the way it did, and still have an area, with `from` at `to`?
            private bool Keeps(int t, int from, int to)
            {
                int[] corner = { _cornerPosition[t * 3], _cornerPosition[t * 3 + 1], _cornerPosition[t * 3 + 2] };
                var before = Cross(Sub(_position[corner[1]], _position[corner[0]]), Sub(_position[corner[2]], _position[corner[0]]));
                for (int i = 0; i < 3; i++)
                {
                    if (corner[i] == from) corner[i] = to;
                }
                var after = Cross(Sub(_position[corner[1]], _position[corner[0]]), Sub(_position[corner[2]], _position[corner[0]]));

                double beforeLength = Math.Sqrt(Dot(before, before)), afterLength = Math.Sqrt(Dot(after, after));
                if (!(afterLength > 1e-18) || !(beforeLength > 0)) return false;
                return Dot(before, after) / (beforeLength * afterLength) > 0.1;
            }

            private int Closest(int vertex, List<int> candidates)
            {
                if (candidates.Count == 0) return vertex;
                var from = _face.Vertices[vertex];
                int best = candidates[0];
                double bestDistance = double.MaxValue;
                foreach (var c in candidates)
                {
                    var v = _face.Vertices[c];
                    double du = v.TexCoord.X - from.TexCoord.X, dv = v.TexCoord.Y - from.TexCoord.Y;
                    double turn = 1 - (v.Normal.X * from.Normal.X + v.Normal.Y * from.Normal.Y + v.Normal.Z * from.Normal.Z);
                    double distance = du * du + dv * dv + turn;
                    if (distance < bestDistance || (distance == bestDistance && c < best)) { bestDistance = distance; best = c; }
                }
                return best;
            }

            public ModelFace Build(ModelFace original)
            {
                var result = new ModelFace { MaterialID = original.MaterialID, Material = original.Material };
                for (int t = 0; t < _alive.Length; t++)
                {
                    if (!_alive[t]) continue;
                    for (int c = 0; c < 3; c++)
                    {
                        int v = _cornerVertex[t * 3 + c];
                        if (original.Weights != null && v < original.Weights.Count)
                            result.AddVertex(original.Vertices[v], original.Weights[v]);
                        else
                            result.AddVertex(original.Vertices[v]);
                    }
                }
                return result;
            }

            private static double[] Sub(double[] u, double[] v) => new[] { u[0] - v[0], u[1] - v[1], u[2] - v[2] };
            private static double[] Cross(double[] u, double[] v) => new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
            private static double Dot(double[] u, double[] v) => u[0] * v[0] + u[1] * v[1] + u[2] * v[2];
        }
    }
}
