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
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// The physics shape of an uploaded mesh: one convex hull, which is what the viewer writes as the base hull when it has
    /// not been asked to analyze the shape, and what an object with the convex hull physics shape type uses.
    /// </summary>
    internal static class ModelPhysics
    {
        /// <summary>
        /// Most points a hull has. The viewer's hull sizes are bytes (so 255), and the grid allows 256, so this keeps clear of both.
        /// </summary>
        internal const int MaxHullPoints = 255;

        // How flat a hull may be, in the mesh's own units, before it is not worth having
        private const float MinRange = 1e-5f;

        private static readonly double[][] Directions = MakeDirections();

        /// <summary>
        /// The convex hull of a prim's mesh, as points in the mesh's own space, or null when there is no good hull to make
        /// (the mesh is flat or a line), in which case the shape is the placeholder box.
        /// </summary>
        internal static List<Vector3>? BaseHull(ModelPrim prim)
        {
            try
            {
                var scale = new Vector3(
                    prim.Scale.X > 0f ? prim.Scale.X : 1f,
                    prim.Scale.Y > 0f ? prim.Scale.Y : 1f,
                    prim.Scale.Z > 0f ? prim.Scale.Z : 1f);

                // The hull is worked out at the mesh's real size, so a long thin prim is not judged in the unit cube
                var seen = new HashSet<Vector3>();
                var points = new List<Vector3>();
                foreach (var face in prim.Faces)
                {
                    foreach (var vertex in face.Vertices)
                    {
                        if (seen.Add(vertex.Position)) points.Add(vertex.Position);
                    }
                }
                if (points.Count < 4) return null;

                var all = new double[points.Count][];
                for (int i = 0; i < all.Length; i++)
                {
                    all[i] = new double[]
                    {
                        points[i].X * (double)scale.X, points[i].Y * (double)scale.Y, points[i].Z * (double)scale.Z
                    };
                }

                // The hull needs no more points than it is allowed, and the points that stick out furthest in any of a spread
                // of directions are the ones that matter
                var chosen = new List<int>();
                if (points.Count <= MaxHullPoints)
                {
                    for (int i = 0; i < points.Count; i++) chosen.Add(i);
                }
                else
                {
                    var set = new SortedSet<int>();
                    foreach (var direction in Directions) set.Add(Furthest(all, direction));
                    chosen.AddRange(set);
                }

                var hull = HullOf(all, chosen);
                if (hull == null) return null;

                // A mesh with more points than the hull can have is judged by how far it sticks out of the hull so far, and
                // the points that stick out furthest, in the direction each side of the hull faces, are added: a long
                // thin shape needs these, because it is the ends that stick out of the sides
                for (int round = 0; round < 4 && points.Count > MaxHullPoints; round++)
                {
                    var outside = new List<KeyValuePair<double, int>>();
                    var known = new HashSet<int>(hull.Vertices);
                    foreach (var face in hull.Faces)
                    {
                        int best = Furthest(all, new[] { face.Nx, face.Ny, face.Nz });
                        double distance = face.Nx * all[best][0] + face.Ny * all[best][1] + face.Nz * all[best][2] - face.D;
                        if (distance > hull.Tolerance && known.Add(best)) outside.Add(new KeyValuePair<double, int>(distance, best));
                    }
                    if (outside.Count == 0) break;

                    outside.Sort((x, y) => y.Key != x.Key ? y.Key.CompareTo(x.Key) : x.Value.CompareTo(y.Value));
                    var next = new List<int>(hull.Vertices);
                    foreach (var o in outside)
                    {
                        if (next.Count >= MaxHullPoints) break;
                        next.Add(o.Value);
                    }
                    var better = HullOf(all, next);
                    if (better == null || better.Vertices.Count > MaxHullPoints) break;
                    hull = better;
                }
                if (hull.Vertices.Count < 4) return null;

                var result = new List<Vector3>(hull.Vertices.Count);
                foreach (var i in hull.Vertices) result.Add(points[i]);

                // A hull that is flat along an axis of the mesh's own space cannot be written
                Vector3 min = result[0], max = result[0];
                foreach (var p in result)
                {
                    min = new Vector3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Vector3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
                if (max.X - min.X < MinRange || max.Y - min.Y < MinRange || max.Z - min.Z < MinRange) return null;
                return result;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not make a physics shape for {prim.ID}, so it has a box: {ex.Message}");
                return null;
            }
        }

        /// <summary>The <c>physics_convex</c> block for a hull: the domain it is quantized across, and the points in 16 bits</summary>
        internal static OSD Encode(List<Vector3> points)
        {
            Vector3 min = points[0], max = points[0];
            foreach (var p in points)
            {
                min = new Vector3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new Vector3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }

            var bytes = new List<byte>(points.Count * 6);
            foreach (var p in points)
            {
                bytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(p.X, min.X, max.X)));
                bytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(p.Y, min.Y, max.Y)));
                bytes.AddRange(Utils.UInt16ToBytes(Utils.FloatToUInt16(p.Z, min.Z, max.Z)));
            }

            var block = new OSDMap();
            block["Min"] = min;
            block["Max"] = max;
            block["BoundingVerts"] = bytes.ToArray();
            return block;
        }

        // The 26 directions of a cube's corners, edges and faces, and the rest spread evenly over a sphere, 255 in all
        private static double[][] MakeDirections()
        {
            var list = new List<double[]>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;
                        double length = Math.Sqrt(x * x + y * y + z * z);
                        list.Add(new[] { x / length, y / length, z / length });
                    }

            int spread = MaxHullPoints - list.Count;
            double golden = Math.PI * (3 - Math.Sqrt(5));
            for (int i = 0; i < spread; i++)
            {
                double y = 1 - 2 * (i + 0.5) / spread;
                double radius = Math.Sqrt(1 - y * y);
                double angle = golden * i;
                list.Add(new[] { Math.Cos(angle) * radius, y, Math.Sin(angle) * radius });
            }
            return list.ToArray();
        }

        internal sealed class HullFace
        {
            public int A, B, C;
            public double Nx, Ny, Nz, D;
        }

        private sealed class HullResult
        {
            public List<int> Vertices = new List<int>();   // indices into the full list of points
            public List<HullFace> Faces = new List<HullFace>();
            public double Tolerance;
        }

        // The hull of some of the points, with indices that mean the same in the full list
        private static HullResult? HullOf(double[][] all, List<int> chosen)
        {
            var subset = new double[chosen.Count][];
            for (int i = 0; i < subset.Length; i++) subset[i] = all[chosen[i]];

            var local = Hull(subset, out var faces);
            if (local == null || faces == null) return null;

            double range = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                double lo = double.MaxValue, hi = double.MinValue;
                foreach (var q in subset) { lo = Math.Min(lo, q[axis]); hi = Math.Max(hi, q[axis]); }
                range = Math.Max(range, hi - lo);
            }

            var result = new HullResult { Faces = faces, Tolerance = range * 1e-6 };
            foreach (var i in local) result.Vertices.Add(chosen[i]);
            // the faces refer to the subset, so they are renumbered to the full list
            foreach (var f in faces) { f.A = chosen[f.A]; f.B = chosen[f.B]; f.C = chosen[f.C]; }
            return result;
        }

        private static int Furthest(double[][] points, double[] direction)
        {
            int best = 0;
            double bestDot = double.MinValue;
            for (int i = 0; i < points.Length; i++)
            {
                double dot = direction[0] * points[i][0] + direction[1] * points[i][1] + direction[2] * points[i][2];
                if (dot > bestDot) { bestDot = dot; best = i; }
            }
            return best;
        }

        /// <summary>The indices of the points that are the vertices of the convex hull, or null if the points are flat or a line</summary>
        internal static List<int>? Hull(double[][] p) => Hull(p, out _);

        internal static List<int>? Hull(double[][] p, out List<HullFace>? hullFaces)
        {
            hullFaces = null;
            int n = p.Length;
            if (n < 4) return null;

            double range = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                double lo = double.MaxValue, hi = double.MinValue;
                foreach (var q in p) { lo = Math.Min(lo, q[axis]); hi = Math.Max(hi, q[axis]); }
                range = Math.Max(range, hi - lo);
            }
            if (!(range > 0)) return null;
            double eps = range * 1e-9;

            // A first tetrahedron that is as big as the points allow: two far apart points, the one furthest from their
            // line, and the one furthest from their plane
            int a = 0, b = 0;
            double far = -1;
            for (int axis = 0; axis < 3; axis++)
            {
                int lo = 0, hi = 0;
                for (int i = 1; i < n; i++)
                {
                    if (p[i][axis] < p[lo][axis]) lo = i;
                    if (p[i][axis] > p[hi][axis]) hi = i;
                }
                double d = Distance(p[lo], p[hi]);
                if (d > far) { far = d; a = lo; b = hi; }
            }
            if (far <= eps) return null;

            int c = -1;
            far = -1;
            for (int i = 0; i < n; i++)
            {
                double d = Length(Cross(Sub(p[b], p[a]), Sub(p[i], p[a]))) / Distance(p[a], p[b]);
                if (d > far) { far = d; c = i; }
            }
            if (far <= eps) return null;

            var normal = Cross(Sub(p[b], p[a]), Sub(p[c], p[a]));
            double normalLength = Length(normal);
            int dIndex = -1;
            far = -1;
            for (int i = 0; i < n; i++)
            {
                double d = Math.Abs(Dot(normal, Sub(p[i], p[a]))) / normalLength;
                if (d > far) { far = d; dIndex = i; }
            }
            if (far <= eps) return null;

            var inside = new[]
            {
                (p[a][0] + p[b][0] + p[c][0] + p[dIndex][0]) / 4,
                (p[a][1] + p[b][1] + p[c][1] + p[dIndex][1]) / 4,
                (p[a][2] + p[b][2] + p[c][2] + p[dIndex][2]) / 4
            };

            var faces = new List<HullFace>
            {
                MakeFace(p, a, b, c, inside), MakeFace(p, a, b, dIndex, inside),
                MakeFace(p, a, c, dIndex, inside), MakeFace(p, b, c, dIndex, inside)
            };

            for (int i = 0; i < n; i++)
            {
                if (i == a || i == b || i == c || i == dIndex) continue;

                var visible = new List<HullFace>();
                foreach (var f in faces)
                {
                    if (f.Nx * p[i][0] + f.Ny * p[i][1] + f.Nz * p[i][2] - f.D > eps) visible.Add(f);
                }
                if (visible.Count == 0) continue;

                // The edges around the part of the hull this point sees are those that belong to one visible face only
                var edges = new HashSet<long>();
                foreach (var f in visible)
                {
                    edges.Add(Key(f.A, f.B)); edges.Add(Key(f.B, f.C)); edges.Add(Key(f.C, f.A));
                }
                var horizon = new List<KeyValuePair<int, int>>();
                foreach (var f in visible)
                {
                    if (!edges.Contains(Key(f.B, f.A))) horizon.Add(new KeyValuePair<int, int>(f.A, f.B));
                    if (!edges.Contains(Key(f.C, f.B))) horizon.Add(new KeyValuePair<int, int>(f.B, f.C));
                    if (!edges.Contains(Key(f.A, f.C))) horizon.Add(new KeyValuePair<int, int>(f.C, f.A));
                }

                faces.RemoveAll(f => visible.Contains(f));
                foreach (var edge in horizon) faces.Add(MakeFace(p, edge.Key, edge.Value, i, inside));
            }

            // A point in the middle of a flat side or along an edge can end up among the vertices, because it came before the
            // points that made the side flat. A real vertex has at least three sides, no two of them the same plane.
            var planes = new SortedDictionary<int, List<HullFace>>();
            foreach (var f in faces)
            {
                foreach (var v in new[] { f.A, f.B, f.C })
                {
                    if (!planes.TryGetValue(v, out var around)) planes[v] = around = new List<HullFace>();
                    around.Add(f);
                }
            }

            var vertices = new List<int>();
            foreach (var entry in planes)
            {
                var distinct = new List<HullFace>();
                foreach (var f in entry.Value)
                {
                    bool seen = false;
                    foreach (var d in distinct)
                    {
                        if (d.Nx * f.Nx + d.Ny * f.Ny + d.Nz * f.Nz > 1 - 1e-9) { seen = true; break; }
                    }
                    if (!seen) distinct.Add(f);
                }
                if (distinct.Count >= 3) vertices.Add(entry.Key);
            }
            hullFaces = faces;
            return vertices;
        }

        private static long Key(int from, int to) => ((long)from << 32) | (uint)to;

        // A face with its normal pointing away from a point inside the hull
        private static HullFace MakeFace(double[][] p, int a, int b, int c, double[] inside)
        {
            var normal = Cross(Sub(p[b], p[a]), Sub(p[c], p[a]));
            double length = Length(normal);
            normal = new[] { normal[0] / length, normal[1] / length, normal[2] / length };
            double d = Dot(normal, p[a]);
            if (Dot(normal, inside) - d > 0)
            {
                normal = new[] { -normal[0], -normal[1], -normal[2] };
                d = -d;
                int swap = b; b = c; c = swap;
            }
            return new HullFace { A = a, B = b, C = c, Nx = normal[0], Ny = normal[1], Nz = normal[2], D = d };
        }

        private static double[] Sub(double[] u, double[] v) => new[] { u[0] - v[0], u[1] - v[1], u[2] - v[2] };
        private static double[] Cross(double[] u, double[] v) => new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
        private static double Dot(double[] u, double[] v) => u[0] * v[0] + u[1] * v[1] + u[2] * v[2];
        private static double Length(double[] u) => Math.Sqrt(Dot(u, u));
        private static double Distance(double[] u, double[] v) => Length(Sub(u, v));
    }
}
