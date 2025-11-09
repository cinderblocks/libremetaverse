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
using System.IO;
using System.Text;
using System.Buffers;
using System.Collections.Concurrent;

namespace LibreMetaverse.PrimMesher
{
    public struct Quat
    {
        /// <summary>X value</summary>
        public float X;

        /// <summary>Y value</summary>
        public float Y;

        /// <summary>Z value</summary>
        public float Z;

        /// <summary>W value</summary>
        public float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Quat(Coord axis, float angle)
        {
            axis = axis.Normalize();

            angle *= 0.5f;
            var c = (float)Math.Cos(angle);
            var s = (float)Math.Sin(angle);

            X = axis.X * s;
            Y = axis.Y * s;
            Z = axis.Z * s;
            W = c;

            Normalize();
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
        }

        public Quat Normalize()
        {
            const float MAG_THRESHOLD = 0.0000001f;
            var mag = Length();

            // Catch very small rounding errors when normalizing
            if (mag > MAG_THRESHOLD)
            {
                var oomag = 1f / mag;
                X *= oomag;
                Y *= oomag;
                Z *= oomag;
                W *= oomag;
            }
            else
            {
                X = 0f;
                Y = 0f;
                Z = 0f;
                W = 1f;
            }

            return this;
        }

        public static Quat operator *(Quat q1, Quat q2)
        {
            var x = q1.W * q2.X + q1.X * q2.W + q1.Y * q2.Z - q1.Z * q2.Y;
            var y = q1.W * q2.Y - q1.X * q2.Z + q1.Y * q2.W + q1.Z * q2.X;
            var z = q1.W * q2.Z + q1.X * q2.Y - q1.Y * q2.X + q1.Z * q2.W;
            var w = q1.W * q2.W - q1.X * q2.X - q1.Y * q2.Y - q1.Z * q2.Z;
            return new Quat(x, y, z, w);
        }

        public override string ToString()
        {
            return "< X: " + X + ", Y: " + Y + ", Z: " + Z + ", W: " + W + ">";
        }
    }

    public struct Coord
    {
        public float X;
        public float Y;
        public float Z;

        public Coord(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public Coord Invert()
        {
            X = -X;
            Y = -Y;
            Z = -Z;

            return this;
        }

        public Coord Normalize()
        {
            const float MAG_THRESHOLD = 0.0000001f;
            var mag = Length();

            // Catch very small rounding errors when normalizing
            if (mag > MAG_THRESHOLD)
            {
                var oomag = 1.0f / mag;
                X *= oomag;
                Y *= oomag;
                Z *= oomag;
            }
            else
            {
                X = 0.0f;
                Y = 0.0f;
                Z = 0.0f;
            }

            return this;
        }

        public override string ToString()
        {
            return X + " " + Y + " " + Z;
        }

        public static Coord Cross(Coord c1, Coord c2)
        {
            return new Coord(
                c1.Y * c2.Z - c2.Y * c1.Z,
                c1.Z * c2.X - c2.Z * c1.X,
                c1.X * c2.Y - c2.X * c1.Y
            );
        }

        public static Coord operator +(Coord v, Coord a)
        {
            return new Coord(v.X + a.X, v.Y + a.Y, v.Z + a.Z);
        }

        public static Coord operator *(Coord v, Coord m)
        {
            return new Coord(v.X * m.X, v.Y * m.Y, v.Z * m.Z);
        }

        public static Coord operator *(Coord v, Quat q)
        {
            // From http://www.euclideanspace.com/maths/algebra/realNormedAlgebra/quaternions/transforms/

            var c2 = new Coord(0.0f, 0.0f, 0.0f)
            {
                X = q.W * q.W * v.X +
                    2f * q.Y * q.W * v.Z -
                    2f * q.Z * q.W * v.Y +
                    q.X * q.X * v.X +
                    2f * q.Y * q.X * v.Y +
                    2f * q.Z * q.X * v.Z -
                    q.Z * q.Z * v.X -
                    q.Y * q.Y * v.X,
                Y = 2f * q.X * q.Y * v.X +
                    q.Y * q.Y * v.Y +
                    2f * q.Z * q.Y * v.Z +
                    2f * q.W * q.Z * v.X -
                    q.Z * q.Z * v.Y +
                    q.W * q.W * v.Y -
                    2f * q.X * q.W * v.Z -
                    q.X * q.X * v.Y,
                Z = 2f * q.X * q.Z * v.X +
                    2f * q.Y * q.Z * v.Y +
                    q.Z * q.Z * v.Z -
                    2f * q.W * q.Y * v.X -
                    q.Y * q.Y * v.Z +
                    2f * q.W * q.X * v.Y -
//                    q.X * q.X * v.Z +
                    q.W * q.W * v.Z
            };

            return c2;
        }
    }

    public struct UVCoord
    {
        public float U;
        public float V;


        public UVCoord(float u, float v)
        {
            U = u;
            V = v;
        }

        public UVCoord Flip()
        {
            U = 1.0f - U;
            V = 1.0f - V;
            return this;
        }
    }

    public struct Face
    {
        public int primFace;

        // vertices
        public int v1;

        public int v2;
        public int v3;

        //normals
        public int n1;

        public int n2;
        public int n3;

        // uvs
        public int uv1;

        public int uv2;
        public int uv3;

        public Face(int v1, int v2, int v3)
        {
            primFace = 0;

            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;

            n1 = 0;
            n2 = 0;
            n3 = 0;

            uv1 = 0;
            uv2 = 0;
            uv3 = 0;
        }

        public Face(int v1, int v2, int v3, int n1, int n2, int n3)
        {
            primFace = 0;

            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;

            this.n1 = n1;
            this.n2 = n2;
            this.n3 = n3;

            uv1 = 0;
            uv2 = 0;
            uv3 = 0;
        }

        public Coord SurfaceNormal(List<Coord> coordList)
        {
            var c1 = coordList[v1];
            var c2 = coordList[v2];
            var c3 = coordList[v3];

            var edge1 = new Coord(c2.X - c1.X, c2.Y - c1.Y, c2.Z - c1.Z);
            var edge2 = new Coord(c3.X - c1.X, c3.Y - c1.Y, c3.Z - c1.Z);

            return Coord.Cross(edge1, edge2).Normalize();
        }
    }

    public struct ViewerFace
    {
        public int primFaceNumber;

        public Coord v1;
        public Coord v2;
        public Coord v3;

        public int coordIndex1;
        public int coordIndex2;
        public int coordIndex3;

        public Coord n1;
        public Coord n2;
        public Coord n3;

        public UVCoord uv1;
        public UVCoord uv2;
        public UVCoord uv3;

        public ViewerFace(int primFaceNumber)
        {
            this.primFaceNumber = primFaceNumber;

            v1 = new Coord();
            v2 = new Coord();
            v3 = new Coord();

            coordIndex1 = coordIndex2 = coordIndex3 = -1; // -1 means not assigned yet

            n1 = new Coord();
            n2 = new Coord();
            n3 = new Coord();

            uv1 = new UVCoord();
            uv2 = new UVCoord();
            uv3 = new UVCoord();
        }

        public void Scale(float x, float y, float z)
        {
            v1.X *= x;
            v1.Y *= y;
            v1.Z *= z;

            v2.X *= x;
            v2.Y *= y;
            v2.Z *= z;

            v3.X *= x;
            v3.Y *= y;
            v3.Z *= z;
        }

        public void AddPos(float x, float y, float z)
        {
            v1.X += x;
            v2.X += x;
            v3.X += x;

            v1.Y += y;
            v2.Y += y;
            v3.Y += y;

            v1.Z += z;
            v2.Z += z;
            v3.Z += z;
        }

        public void AddRot(Quat q)
        {
            v1 *= q;
            v2 *= q;
            v3 *= q;

            n1 *= q;
            n2 *= q;
            n3 *= q;
        }

        public void CalcSurfaceNormal()
        {
            var edge1 = new Coord(v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z);
            var edge2 = new Coord(v3.X - v1.X, v3.Y - v1.Y, v3.Z - v1.Z);

            n1 = n2 = n3 = Coord.Cross(edge1, edge2).Normalize();
        }
    }

    internal struct Angle
    {
        internal float angle;
        internal float X;
        internal float Y;

        internal Angle(float angle, float x, float y)
        {
            this.angle = angle;
            X = x;
            Y = y;
        }
    }

    internal class AngleList
    {
        // Cache computed angle lists to avoid recomputing identical profiles
        private static readonly ConcurrentDictionary<string, (Angle[] angles, Coord[] normals)> AngleCache =
            new ConcurrentDictionary<string, (Angle[], Coord[])>();

        private static readonly Angle[] angles3 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new Angle(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        private static readonly Coord[] normals3 =
        {
            new Coord(0.25f, 0.4330127019f, 0.0f).Normalize(),
            new Coord(-0.5f, 0.0f, 0.0f).Normalize(),
            new Coord(0.25f, -0.4330127019f, 0.0f).Normalize(),
            new Coord(0.25f, 0.4330127019f, 0.0f).Normalize()
        };

        private static readonly Angle[] angles4 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.25f, 0.0f, 1.0f),
            new Angle(0.5f, -1.0f, 0.0f),
            new Angle(0.75f, 0.0f, -1.0f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        private static readonly Coord[] normals4 =
        {
            new Coord(0.5f, 0.5f, 0.0f).Normalize(),
            new Coord(-0.5f, 0.5f, 0.0f).Normalize(),
            new Coord(-0.5f, -0.5f, 0.0f).Normalize(),
            new Coord(0.5f, -0.5f, 0.0f).Normalize(),
            new Coord(0.5f, 0.5f, 0.0f).Normalize()
        };

        private static readonly Angle[] angles24 =
        {
            new Angle(0.0f, 1.0f, 0.0f),
            new Angle(0.041666666666666664f, 0.96592582628906831f, 0.25881904510252074f),
            new Angle(0.083333333333333329f, 0.86602540378443871f, 0.5f),
            new Angle(0.125f, 0.70710678118654757f, 0.70710678118654746f),
            new Angle(0.16666666666666667f, 0.5f, 0.8660254037844386f),
            new Angle(0.20833333333333331f, 0.25881904510252096f, 0.9659258262890682f),
            new Angle(0.25f, 0.0f, 1.0f),
            new Angle(0.29166666666666663f, -0.25881904510252063f, 0.96592582628906831f),
            new Angle(0.33333333333333333f, -0.5f, 0.86602540378443871f),
            new Angle(0.375f, -0.70710678118654746f, 0.70710678118654757f),
            new Angle(0.41666666666666663f, -0.86602540378443849f, 0.5f),
            new Angle(0.45833333333333331f, -0.9659258262890682f, 0.25881904510252102f),
            new Angle(0.5f, -1.0f, 0.0f),
            new Angle(0.54166666666666663f, -0.96592582628906842f, -0.25881904510252035f),
            new Angle(0.58333333333333326f, -0.86602540378443882f, -0.5f),
            new Angle(0.62499999999999989f, -0.70710678118654791f, -0.70710678118654713f),
            new Angle(0.66666666666666667f, -0.5f, -0.86602540378443837f),
            new Angle(0.70833333333333326f, -0.25881904510252152f, -0.96592582628906809f),
            new Angle(0.75f, 0.0f, -1.0f),
            new Angle(0.79166666666666663f, 0.2588190451025203f, -0.96592582628906842f),
            new Angle(0.83333333333333326f, 0.5f, -0.86602540378443904f),
            new Angle(0.875f, 0.70710678118654735f, -0.70710678118654768f),
            new Angle(0.91666666666666663f, 0.86602540378443837f, -0.5f),
            new Angle(0.95833333333333326f, 0.96592582628906809f, -0.25881904510252157f),
            new Angle(1.0f, 1.0f, 0.0f)
        };

        internal List<Angle> angles;
        private float iX, iY; // intersection point
        internal List<Coord> normals;

        private static Angle interpolatePoints(float newPoint, Angle p1, Angle p2)
        {
            var m = (newPoint - p1.angle) / (p2.angle - p1.angle);
            return new Angle(newPoint, p1.X + m * (p2.X - p1.X), p1.Y + m * (p2.Y - p1.Y));
        }

        private void intersection(double x1, double y1, double x2, double y2, double x3, double y3, double x4,
            double y4)
        {
            // ref: http://local.wasp.uwa.edu.au/~pbourke/geometry/lineline2d/
            var denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            var uaNumerator = (x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3);

            if (denom != 0.0)
            {
                var ua = uaNumerator / denom;
                iX = (float)(x1 + ua * (x2 - x1));
                iY = (float)(y1 + ua * (y2 - y1));
            }
        }

        internal void makeAngles(int sides, float startAngle, float stopAngle)
        {
            // Try cache first
            var key = sides + ":" + startAngle.ToString("R") + ":" + stopAngle.ToString("R");
            if (AngleCache.TryGetValue(key, out var cached))
            {
                angles = new List<Angle>(cached.angles);
                normals = new List<Coord>(cached.normals ?? Array.Empty<Coord>());
                return;
            }

            angles = new List<Angle>();
            normals = new List<Coord>();

            var twoPi = Math.PI * 2.0;
            var twoPiInv = 1.0f / (float)twoPi;

            if (sides < 1)
                throw new Exception("number of sides not greater than zero");
            if (stopAngle <= startAngle)
                throw new Exception("stopAngle not greater than startAngle");

            if (sides == 3 || sides == 4 || sides == 24)
            {
                startAngle *= twoPiInv;
                stopAngle *= twoPiInv;

                Angle[] sourceAngles;
                if (sides == 3)
                    sourceAngles = angles3;
                else if (sides == 4)
                    sourceAngles = angles4;
                else sourceAngles = angles24;

                var startAngleIndex = (int)(startAngle * sides);
                var endAngleIndex = sourceAngles.Length - 1;
                if (stopAngle < 1.0f)
                    endAngleIndex = (int)(stopAngle * sides) + 1;
                if (endAngleIndex == startAngleIndex)
                    endAngleIndex++;

                for (var angleIndex = startAngleIndex; angleIndex < endAngleIndex + 1; angleIndex++)
                {
                    angles.Add(sourceAngles[angleIndex]);
                    if (sides == 3)
                        normals.Add(normals3[angleIndex]);
                    else if (sides == 4)
                        normals.Add(normals4[angleIndex]);
                }

                if (startAngle > 0.0f)
                    angles[0] = interpolatePoints(startAngle, angles[0], angles[1]);

                if (stopAngle < 1.0f)
                {
                    var lastAngleIndex = angles.Count - 1;
                    angles[lastAngleIndex] =
                        interpolatePoints(stopAngle, angles[lastAngleIndex - 1], angles[lastAngleIndex]);
                }
            }
            else
            {
                var stepSize = twoPi / sides;

                var startStep = (int)(startAngle / stepSize);
                var angle = stepSize * startStep;
                var step = startStep;
                double stopAngleTest = stopAngle;
                if (stopAngle < twoPi)
                {
                    stopAngleTest = stepSize * ((int)(stopAngle / stepSize) + 1);
                    if (stopAngleTest < stopAngle)
                        stopAngleTest += stepSize;
                    if (stopAngleTest > twoPi)
                        stopAngleTest = twoPi;
                }

                while (angle <= stopAngleTest)
                {
                    Angle newAngle;
                    newAngle.angle = (float)angle;
                    newAngle.X = (float)Math.Cos(angle);
                    newAngle.Y = (float)Math.Sin(angle);
                    angles.Add(newAngle);
                    step += 1;
                    angle = stepSize * step;
                }

                if (startAngle > angles[0].angle)
                {
                    Angle newAngle;
                    intersection(angles[0].X, angles[0].Y, angles[1].X, angles[1].Y, 0.0f, 0.0f,
                        (float)Math.Cos(startAngle), (float)Math.Sin(startAngle));
                    newAngle.angle = startAngle;
                    newAngle.X = iX;
                    newAngle.Y = iY;
                    angles[0] = newAngle;
                }

                var index = angles.Count - 1;
                if (stopAngle < angles[index].angle)
                {
                    Angle newAngle;
                    intersection(angles[index - 1].X, angles[index - 1].Y, angles[index].X, angles[index].Y, 0.0f, 0.0f,
                        (float)Math.Cos(stopAngle), (float)Math.Sin(stopAngle));
                    newAngle.angle = stopAngle;
                    newAngle.X = iX;
                    newAngle.Y = iY;
                    angles[index] = newAngle;
                }
            }

            // Store into cache for reuse
            try
            {
                var a = angles.ToArray();
                var n = normals.Count > 0 ? normals.ToArray() : Array.Empty<Coord>();
                AngleCache.TryAdd(key, (a, n));
            }
            catch
            {
                // ignore cache failures
            }
         }
     }

    /// <summary>
    ///     generates a profile for extrusion
    /// </summary>
    public class Profile
    {
        private const float twoPi = 2.0f * (float)Math.PI;
        public int bottomFaceNumber;

        public bool calcVertexNormals;

        public List<Coord> coords;
        public List<int> cut1CoordIndices;
        public List<int> cut2CoordIndices;
        public Coord cutNormal1;
        public Coord cutNormal2;

        public string errorMessage;

        public Coord faceNormal = new Coord(0.0f, 0.0f, 1.0f);
        public List<int> faceNumbers;
        public List<Face> faces;
        public List<UVCoord> faceUVs;
        public List<int> hollowCoordIndices;
        public int hollowFaceNumber = -1;
        public int numHollowVerts;

        public int numOuterVerts;
        public int numPrimFaces;

        // use these for making individual meshes for each prim face
        public List<int> outerCoordIndices;

        public int outerFaceNumber = -1;
        public List<float> us;
        public List<Coord> vertexNormals;

        public Profile()
        {
            coords = new List<Coord>(8);
            faces = new List<Face>(8);
            vertexNormals = new List<Coord>(8);
            us = new List<float>(8);
            faceUVs = new List<UVCoord>(8);
            faceNumbers = new List<int>(8);
        }

        public Profile(int sides, float profileStart, float profileEnd, float hollow, int hollowSides, bool createFaces,
            bool calcVertexNormals)
        {
            this.calcVertexNormals = calcVertexNormals;

            coords = new List<Coord>();
            faces = new List<Face>();
            vertexNormals = new List<Coord>();
            us = new List<float>();
            faceUVs = new List<UVCoord>();
            faceNumbers = new List<int>(8);

            var center = new Coord(0.0f, 0.0f, 0.0f);

            var hollowCoords = new List<Coord>();
            var hollowNormals = new List<Coord>();
            var hollowUs = new List<float>();

            if (calcVertexNormals)
            {
                outerCoordIndices = new List<int>();
                hollowCoordIndices = new List<int>();
                cut1CoordIndices = new List<int>();
                cut2CoordIndices = new List<int>();
            }

            var hasHollow = hollow > 0.0f;

            var hasProfileCut = profileStart > 0.0f || profileEnd < 1.0f;

            var angles = new AngleList();
            var hollowAngles = new AngleList();

            var xScale = 0.5f;
            var yScale = 0.5f;
            if (sides == 4) // corners of a square are sqrt(2) from center
            {
                xScale = 0.707107f;
                yScale = 0.707107f;
            }

            var startAngle = profileStart * twoPi;
            var stopAngle = profileEnd * twoPi;

            try
            {
                angles.makeAngles(sides, startAngle, stopAngle);
            }
            catch (Exception ex)
            {
                errorMessage = "makeAngles failed: Exception: " + ex
                               + "\nsides: " + sides + " startAngle: " + startAngle + " stopAngle: " + stopAngle;

                return;
            }

            numOuterVerts = angles.angles.Count;

            // flag to create as few triangles as possible for 3 or 4 side profile
            var simpleFace = sides < 5 && !hasHollow && !hasProfileCut;

            if (hasHollow)
            {
                if (sides == hollowSides)
                    hollowAngles = angles;
                else
                    try
                    {
                        hollowAngles.makeAngles(hollowSides, startAngle, stopAngle);
                    }
                    catch (Exception ex)
                    {
                        errorMessage = "makeAngles failed: Exception: " + ex
                                       + "\nsides: " + sides + " startAngle: " + startAngle + " stopAngle: " +
                                       stopAngle;

                        return;
                    }
                numHollowVerts = hollowAngles.angles.Count;
            }
            else if (!simpleFace)
            {
                coords.Add(center);
                if (this.calcVertexNormals)
                    vertexNormals.Add(new Coord(0.0f, 0.0f, 1.0f));
                us.Add(0.0f);
            }

            var z = 0.0f;

            Angle angle;
            var newVert = new Coord();

            // Pre-size lists for performance
            var estimatedTotalVerts = numOuterVerts + (hasHollow ? numHollowVerts : 0) + 2;
            if (estimatedTotalVerts < 8) estimatedTotalVerts = 8;
            coords.Capacity = Math.Max(coords.Capacity, estimatedTotalVerts);
            vertexNormals.Capacity = Math.Max(vertexNormals.Capacity, estimatedTotalVerts);
            faceUVs.Capacity = Math.Max(faceUVs.Capacity, estimatedTotalVerts);
            us.Capacity = Math.Max(us.Capacity, estimatedTotalVerts);
            if (createFaces)
                faces.Capacity = Math.Max(faces.Capacity, numOuterVerts * 2 + 8);

            if (hasHollow && hollowSides != sides)
            {
                var numHollowAngles = hollowAngles.angles.Count;
                for (var i = 0; i < numHollowAngles; i++)
                {
                    angle = hollowAngles.angles[i];
                    newVert.X = hollow * xScale * angle.X;
                    newVert.Y = hollow * yScale * angle.Y;
                    newVert.Z = z;

                    hollowCoords.Add(newVert);
                    if (this.calcVertexNormals)
                    {
                        hollowNormals.Add(hollowSides < 5
                            ? hollowAngles.normals[i].Invert()
                            : new Coord(-angle.X, -angle.Y, 0.0f));

                        if (hollowSides == 4)
                            hollowUs.Add(angle.angle * hollow * 0.707107f);
                        else
                            hollowUs.Add(angle.angle * hollow);
                    }
                }
            }

            var index = 0;
            var numAngles = angles.angles.Count;

            for (var i = 0; i < numAngles; i++)
            {
                angle = angles.angles[i];
                newVert.X = angle.X * xScale;
                newVert.Y = angle.Y * yScale;
                newVert.Z = z;
                coords.Add(newVert);
                if (this.calcVertexNormals)
                {
                    outerCoordIndices?.Add(coords.Count - 1);

                    if (sides < 5)
                    {
                        vertexNormals.Add(angles.normals[i]);
                        var u = angle.angle;
                        us.Add(u);
                    }
                    else
                    {
                        vertexNormals.Add(new Coord(angle.X, angle.Y, 0.0f));
                        us.Add(angle.angle);
                    }
                }

                if (hasHollow)
                {
                    if (hollowSides == sides)
                    {
                        newVert.X *= hollow;
                        newVert.Y *= hollow;
                        newVert.Z = z;
                        hollowCoords.Add(newVert);
                        if (this.calcVertexNormals)
                        {
                            hollowNormals.Add(sides < 5
                                ? angles.normals[i].Invert()
                                : new Coord(-angle.X, -angle.Y, 0.0f));

                            hollowUs.Add(angle.angle * hollow);
                        }
                    }
                }
                else if (!simpleFace && createFaces && angle.angle > 0.0001f)
                {
                    var newFace = new Face
                    {
                        v1 = 0,
                        v2 = index,
                        v3 = index + 1
                    };

                    faces.Add(newFace);
                }
                index += 1;
            }

            if (hasHollow)
            {
                hollowCoords.Reverse();
                if (this.calcVertexNormals)
                {
                    hollowNormals.Reverse();
                    hollowUs.Reverse();
                }

                if (createFaces)
                {
                    var numTotalVerts = numOuterVerts + numHollowVerts;

                    if (numOuterVerts == numHollowVerts)
                    {
                        var newFace = new Face();

                        for (var coordIndex = 0; coordIndex < numOuterVerts - 1; coordIndex++)
                        {
                            newFace.v1 = coordIndex;
                            newFace.v2 = coordIndex + 1;
                            newFace.v3 = numTotalVerts - coordIndex - 1;
                            faces.Add(newFace);

                            newFace.v1 = coordIndex + 1;
                            newFace.v2 = numTotalVerts - coordIndex - 2;
                            newFace.v3 = numTotalVerts - coordIndex - 1;
                            faces.Add(newFace);
                        }
                    }
                    else
                    {
                        if (numOuterVerts < numHollowVerts)
                        {
                            var newFace = new Face();
                            var j = 0; // j is the index for outer vertices
                            var maxJ = numOuterVerts - 1;
                            for (var i = 0; i < numHollowVerts; i++) // i is the index for inner vertices
                            {
                                if (j < maxJ)
                                    if (angles.angles[j + 1].angle - hollowAngles.angles[i].angle <
                                        hollowAngles.angles[i].angle - angles.angles[j].angle + 0.000001f)
                                    {
                                        newFace.v1 = numTotalVerts - i - 1;
                                        newFace.v2 = j;
                                        newFace.v3 = j + 1;

                                        faces.Add(newFace);
                                        j += 1;
                                    }

                                newFace.v1 = j;
                                newFace.v2 = numTotalVerts - i - 2;
                                newFace.v3 = numTotalVerts - i - 1;

                                faces.Add(newFace);
                            }
                        }
                        else // numHollowVerts < numOuterVerts
                        {
                            var newFace = new Face();
                            var j = 0; // j is the index for inner vertices
                            var maxJ = numHollowVerts - 1;
                            for (var i = 0; i < numOuterVerts; i++)
                            {
                                if (j < maxJ)
                                    if (hollowAngles.angles[j + 1].angle - angles.angles[i].angle <
                                        angles.angles[i].angle - hollowAngles.angles[j].angle + 0.000001f)
                                    {
                                        newFace.v1 = i;
                                        newFace.v2 = numTotalVerts - j - 2;
                                        newFace.v3 = numTotalVerts - j - 1;

                                        faces.Add(newFace);
                                        j += 1;
                                    }

                                newFace.v1 = numTotalVerts - j - 1;
                                newFace.v2 = i;
                                newFace.v3 = i + 1;

                                faces.Add(newFace);
                            }
                        }
                    }
                }

                if (calcVertexNormals)
                {
                    // Add hollow coords with reserved capacity to avoid reallocation
                    for (var hcIndex = 0; hcIndex < hollowCoords.Count; hcIndex++)
                    {
                        coords.Add(hollowCoords[hcIndex]);
                        hollowCoordIndices.Add(coords.Count - 1);
                    }
                }
                else
                    coords.AddRange(hollowCoords);

                if (this.calcVertexNormals)
                {
                    vertexNormals.AddRange(hollowNormals);
                    us.AddRange(hollowUs);
                }
            }

            if (simpleFace && createFaces)
                if (sides == 3)
                {
                    faces.Add(new Face(0, 1, 2));
                }
                else if (sides == 4)
                {
                    faces.Add(new Face(0, 1, 2));
                    faces.Add(new Face(0, 2, 3));
                }

            if (calcVertexNormals && hasProfileCut)
            {
                var lastOuterVertIndex = numOuterVerts - 1;

                if (hasHollow)
                {
                    cut1CoordIndices.Add(0);
                    cut1CoordIndices.Add(coords.Count - 1);

                    cut2CoordIndices.Add(lastOuterVertIndex + 1);
                    cut2CoordIndices.Add(lastOuterVertIndex);

                    cutNormal1.X = coords[0].Y - coords[coords.Count - 1].Y;
                    cutNormal1.Y = -(coords[0].X - coords[coords.Count - 1].X);

                    cutNormal2.X = coords[lastOuterVertIndex + 1].Y - coords[lastOuterVertIndex].Y;
                    cutNormal2.Y = -(coords[lastOuterVertIndex + 1].X - coords[lastOuterVertIndex].X);
                }

                else
                {
                    cut1CoordIndices.Add(0);
                    cut1CoordIndices.Add(1);

                    cut2CoordIndices.Add(lastOuterVertIndex);
                    cut2CoordIndices.Add(0);

                    cutNormal1.X = vertexNormals[1].Y;
                    cutNormal1.Y = -vertexNormals[1].X;

                    cutNormal2.X = -vertexNormals[vertexNormals.Count - 2].Y;
                    cutNormal2.Y = vertexNormals[vertexNormals.Count - 2].X;
                }
                cutNormal1.Normalize();
                cutNormal2.Normalize();
            }

            MakeFaceUVs();

            if (calcVertexNormals)
            {
                // calculate prim face numbers

                // face number order is top, outer, hollow, bottom, start cut, end cut
                // I know it's ugly but so is the whole concept of prim face numbers

                var faceNum = 1; // start with outer faces
                outerFaceNumber = faceNum;

                var startVert = hasProfileCut && !hasHollow ? 1 : 0;
                if (startVert > 0)
                    faceNumbers.Add(-1);
                for (var i = 0; i < numOuterVerts - 1; i++)
                    faceNumbers.Add(sides < 5 && i <= sides ? faceNum++ : faceNum);

                faceNumbers.Add(hasProfileCut ? -1 : faceNum++);

                if (sides > 4 && (hasHollow || hasProfileCut))
                    faceNum++;

                if (sides < 5 && (hasHollow || hasProfileCut) && numOuterVerts < sides)
                    faceNum++;

                if (hasHollow)
                {
                    for (var i = 0; i < numHollowVerts; i++)
                        faceNumbers.Add(faceNum);

                    hollowFaceNumber = faceNum++;
                }

                bottomFaceNumber = faceNum++;

                if (hasHollow && hasProfileCut)
                    faceNumbers.Add(faceNum++);

                for (var i = 0; i < faceNumbers.Count; i++)
                    if (faceNumbers[i] == -1)
                        faceNumbers[i] = faceNum++;
            }
        }

        public void MakeFaceUVs()
        {
            // preallocate to avoid repeated growth
            faceUVs = new List<UVCoord>(coords.Count);
            for (var i = 0; i < coords.Count; i++)
            {
                var c = coords[i];
                faceUVs.Add(new UVCoord(1.0f - (0.5f + c.X), 1.0f - (0.5f - c.Y)));
            }
        }

        public Profile Copy()
        {
            return Copy(true);
        }

        public Profile Copy(bool needFaces)
        {
            var copy = new Profile();

            // Reserve capacity for copying to reduce reallocations
            copy.coords = new List<Coord>(coords.Count);
            copy.coords.AddRange(coords);

            copy.faceUVs = new List<UVCoord>(faceUVs.Count);
            copy.faceUVs.AddRange(faceUVs);

            if (needFaces)
            {
                copy.faces = new List<Face>(faces.Count);
                copy.faces.AddRange(faces);
            }

            copy.calcVertexNormals = calcVertexNormals;
            if (calcVertexNormals)
            {
                copy.vertexNormals = new List<Coord>(vertexNormals.Count);
                copy.vertexNormals.AddRange(vertexNormals);
                copy.faceNormal = faceNormal;
                copy.cutNormal1 = cutNormal1;
                copy.cutNormal2 = cutNormal2;
                copy.us = new List<float>(us.Count);
                copy.us.AddRange(us);
                copy.faceNumbers = new List<int>(faceNumbers.Count);
                copy.faceNumbers.AddRange(faceNumbers);

                copy.cut1CoordIndices = cut1CoordIndices != null ? new List<int>(cut1CoordIndices) : null;
                copy.cut2CoordIndices = cut2CoordIndices != null ? new List<int>(cut2CoordIndices) : null;
                copy.hollowCoordIndices = hollowCoordIndices != null ? new List<int>(hollowCoordIndices) : null;
                copy.outerCoordIndices = outerCoordIndices != null ? new List<int>(outerCoordIndices) : null;
            }
            copy.numOuterVerts = numOuterVerts;
            copy.numHollowVerts = numHollowVerts;

            return copy;
        }

        public void AddPos(Coord v)
        {
            AddPos(v.X, v.Y, v.Z);
        }

        public void AddPos(float x, float y, float z)
        {
            var numVerts = coords.Count;

            for (var i = 0; i < numVerts; i++)
            {
                var vert = coords[i];
                vert.X += x;
                vert.Y += y;
                vert.Z += z;
                coords[i] = vert;
            }
        }

        public void AddRot(Quat q)
        {
            var numVerts = coords.Count;

            for (var i = 0; i < numVerts; i++)
                coords[i] *= q;

            if (calcVertexNormals)
            {
                var numNormals = vertexNormals.Count;
                for (var i = 0; i < numNormals; i++)
                    vertexNormals[i] *= q;

                faceNormal *= q;
                cutNormal1 *= q;
                cutNormal2 *= q;
            }
        }

        public void Scale(float x, float y)
        {
            var numVerts = coords.Count;

            for (var i = 0; i < numVerts; i++)
            {
                var vert = coords[i];
                vert.X *= x;
                vert.Y *= y;
                coords[i] = vert;
            }
        }

        /// <summary>
        ///     Changes order of the vertex indices and negates the center vertex normal. Does not alter vertex normals of radial
        ///     vertices
        /// </summary>
        public void FlipNormals()
        {
            var numFaces = faces.Count;
            for (var i = 0; i < numFaces; i++)
            {
                Face tmpFace = faces[i];
                (tmpFace.v3, tmpFace.v1) = (tmpFace.v1, tmpFace.v3);
                faces[i] = tmpFace;
            }

            if (calcVertexNormals)
            {
                var normalCount = vertexNormals.Count;
                if (normalCount > 0)
                {
                    var n = vertexNormals[normalCount - 1];
                    n.Z = -n.Z;
                    vertexNormals[normalCount - 1] = n;
                }
            }

            faceNormal.X = -faceNormal.X;
            faceNormal.Y = -faceNormal.Y;
            faceNormal.Z = -faceNormal.Z;

            var numfaceUVs = faceUVs.Count;
            for (var i = 0; i < numfaceUVs; i++)
            {
                var uv = faceUVs[i];
                uv.V = 1.0f - uv.V;
                faceUVs[i] = uv;
            }
        }

        public void AddValue2FaceVertexIndices(int num)
        {
            var numFaces = faces.Count;
            for (var i = 0; i < numFaces; i++)
            {
                var tmpFace = faces[i];
                tmpFace.v1 += num;
                tmpFace.v2 += num;
                tmpFace.v3 += num;

                faces[i] = tmpFace;
            }
        }

        public void AddValue2FaceNormalIndices(int num)
        {
            if (calcVertexNormals)
            {
                var numFaces = faces.Count;
                for (var i = 0; i < numFaces; i++)
                {
                    var tmpFace = faces[i];
                    tmpFace.n1 += num;
                    tmpFace.n2 += num;
                    tmpFace.n3 += num;

                    faces[i] = tmpFace;
                }
            }
        }

        public void DumpRaw(string path, string name, string title)
        {
            if (path == null)
                return;
            var fileName = name + "_" + title + ".raw";
            var completePath = System.IO.Path.Combine(path, fileName);
            using (var sw = new StreamWriter(completePath))
            {
                for (var i = 0; i < faces.Count; i++)
                {
                    var s = coords[faces[i].v1].ToString();
                    s += " " + coords[faces[i].v2];
                    s += " " + coords[faces[i].v3];

                    sw.WriteLine(s);
                }
            }
        }
    }

    public struct PathNode
    {
        public Coord position;
        public Quat rotation;
        public float xScale;
        public float yScale;
        public float percentOfPath;
    }

    public enum PathType
    {
        Linear = 0,
        Circular = 1,
        Flexible = 2
    }

    public class Path
    {
        private const float twoPi = 2.0f * (float)Math.PI;
        public float dimpleBegin;
        public float dimpleEnd = 1.0f;
        public float holeSizeX = 1.0f; // called pathScaleX in pbs
        public float holeSizeY = 0.25f;
        public float pathCutBegin;
        public float pathCutEnd = 1.0f;
        public List<PathNode> pathNodes = new List<PathNode>();
        public float radius;
        public float revolutions = 1.0f;
        public float skew;
        public int stepsPerRevolution = 24;
        public float taperX;
        public float taperY;
        public float topShearX;
        public float topShearY;

        public float twistBegin;
        public float twistEnd;

        public void Create(PathType pathType, int steps)
        {
            if (taperX > 0.999f)
                taperX = 0.999f;
            if (taperX < -0.999f)
                taperX = -0.999f;
            if (taperY > 0.999f)
                taperY = 0.999f;
            if (taperY < -0.999f)
                taperY = -0.999f;

            if (pathType == PathType.Linear || pathType == PathType.Flexible)
            {
                var step = 0;

                var length = pathCutEnd - pathCutBegin;
                var twistTotal = twistEnd - twistBegin;
                var twistTotalAbs = Math.Abs(twistTotal);
                if (twistTotalAbs > 0.01f)
                    steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

                var start = -0.5f;
                var stepSize = length / steps;
                var percentOfPathMultiplier = stepSize * 0.999999f;
                var xOffset = topShearX * pathCutBegin;
                var yOffset = topShearY * pathCutBegin;
                var zOffset = start;
                var xOffsetStepIncrement = topShearX * length / steps;
                var yOffsetStepIncrement = topShearY * length / steps;

                var percentOfPath = pathCutBegin;
                zOffset += percentOfPath;

                // sanity checks

                var done = false;

                while (!done)
                {
                    var newNode = new PathNode { xScale = 1.0f };

                    if (taperX == 0.0f)
                        newNode.xScale = 1.0f;
                    else if (taperX > 0.0f)
                        newNode.xScale = 1.0f - percentOfPath * taperX;
                    else newNode.xScale = 1.0f + (1.0f - percentOfPath) * taperX;

                    newNode.yScale = 1.0f;
                    if (taperY == 0.0f)
                        newNode.yScale = 1.0f;
                    else if (taperY > 0.0f)
                        newNode.yScale = 1.0f - percentOfPath * taperY;
                    else newNode.yScale = 1.0f + (1.0f - percentOfPath) * taperY;

                    var twist = twistBegin + twistTotal * percentOfPath;

                    newNode.rotation = new Quat(new Coord(0.0f, 0.0f, 1.0f), twist);
                    newNode.position = new Coord(xOffset, yOffset, zOffset);
                    newNode.percentOfPath = percentOfPath;

                    pathNodes.Add(newNode);

                    if (step < steps)
                    {
                        step += 1;
                        percentOfPath += percentOfPathMultiplier;
                        xOffset += xOffsetStepIncrement;
                        yOffset += yOffsetStepIncrement;
                        zOffset += stepSize;
                        if (percentOfPath > pathCutEnd)
                            done = true;
                    }
                    else
                    {
                        done = true;
                    }
                }
            } // end of linear path code

            else // pathType == Circular
            {
                var twistTotal = twistEnd - twistBegin;

                // if the profile has a lot of twist, add more layers otherwise the layers may overlap
                // and the resulting mesh may be quite inaccurate. This method is arbitrary and doesn't
                // accurately match the viewer
                var twistTotalAbs = Math.Abs(twistTotal);
                if (twistTotalAbs > 0.01f)
                {
                    if (twistTotalAbs > Math.PI * 1.5f)
                        steps *= 2;
                    if (twistTotalAbs > Math.PI * 3.0f)
                        steps *= 2;
                }

                var yPathScale = holeSizeY * 0.5f;
                var pathLength = pathCutEnd - pathCutBegin;
                var totalSkew = skew * 2.0f * pathLength;
                var skewStart = pathCutBegin * 2.0f * skew - skew;
                var xOffsetTopShearXFactor = topShearX * (0.25f + 0.5f * (0.5f - holeSizeY));
                var yShearCompensation = 1.0f + Math.Abs(topShearY) * 0.25f;

                // It's not quite clear what pushY (Y top shear) does, but subtracting it from the start and end
                // angles appears to approximate its effects on path cut. Likewise, adding it to the angle used
                // to calculate the sine for generating the path radius appears to approximate its effects there
                // too, but there are some subtle differences in the radius which are noticeable as the prim size
                // increases, and it may affect megaprims quite a bit. The effect of the Y top shear parameter on
                // the meshes generated with this technique appear nearly identical in shape to the same prims when
                // displayed by the viewer.

                var startAngle = twoPi * pathCutBegin * revolutions - topShearY * 0.9f;
                var endAngle = twoPi * pathCutEnd * revolutions - topShearY * 0.9f;
                var stepSize = twoPi / stepsPerRevolution;

                var step = (int)(startAngle / stepSize);
                var angle = startAngle;

                var done = false;
                while (!done) // loop through the length of the path and add the layers
                {
                    var newNode = new PathNode();

                    var xProfileScale = (1.0f - Math.Abs(skew)) * holeSizeX;
                    var yProfileScale = holeSizeY;

                    var percentOfPath = angle / (twoPi * revolutions);
                    var percentOfAngles = (angle - startAngle) / (endAngle - startAngle);

                    if (taperX > 0.01f)
                        xProfileScale *= 1.0f - percentOfPath * taperX;
                    else if (taperX < -0.01f)
                        xProfileScale *= 1.0f + (1.0f - percentOfPath) * taperX;

                    if (taperY > 0.01f)
                        yProfileScale *= 1.0f - percentOfPath * taperY;
                    else if (taperY < -0.01f)
                        yProfileScale *= 1.0f + (1.0f - percentOfPath) * taperY;

                    newNode.xScale = xProfileScale;
                    newNode.yScale = yProfileScale;

                    var radiusScale = 1.0f;
                    if (radius > 0.001f)
                        radiusScale = 1.0f - radius * percentOfPath;
                    else if (radius < 0.001f)
                        radiusScale = 1.0f + radius * (1.0f - percentOfPath);

                    var twist = twistBegin + twistTotal * percentOfPath;

                    var xOffset = 0.5f * (skewStart + totalSkew * percentOfAngles);
                    xOffset += (float)Math.Sin(angle) * xOffsetTopShearXFactor;

                    var yOffset = yShearCompensation * (float)Math.Cos(angle) * (0.5f - yPathScale) * radiusScale;

                    var zOffset = (float)Math.Sin(angle + topShearY) * (0.5f - yPathScale) * radiusScale;

                    newNode.position = new Coord(xOffset, yOffset, zOffset);

                    // now orient the rotation of the profile layer relative to it's position on the path
                    // adding taperY to the angle used to generate the quat appears to approximate the viewer

                    newNode.rotation = new Quat(new Coord(1.0f, 0.0f, 0.0f), angle + topShearY);

                    // next apply twist rotation to the profile layer
                    if (twistTotal != 0.0f || twistBegin != 0.0f)
                        newNode.rotation *= new Quat(new Coord(0.0f, 0.0f, 1.0f), twist);

                    newNode.percentOfPath = percentOfPath;

                    pathNodes.Add(newNode);

                    // calculate terms for next iteration
                    // calculate the angle for the next iteration of the loop

                    if (angle >= endAngle - 0.01)
                    {
                        done = true;
                    }
                    else
                    {
                        step += 1;
                        angle = stepSize * step;
                        if (angle > endAngle)
                            angle = endAngle;
                    }
                }
            }
        }
    }

    public class PrimMesh
    {
        private const float twoPi = 2.0f * (float)Math.PI;
        public bool calcVertexNormals;

        public List<Coord> coords;
        public float dimpleBegin;
        public float dimpleEnd = 1.0f;
        public string errorMessage = "";
        public List<Face> faces;

        public float holeSizeX = 1.0f; // called pathScaleX in pbs
        public float holeSizeY = 0.25f;
        private readonly float hollow;
        private readonly int hollowSides = 4;
        public List<Coord> normals;
        private bool normalsProcessed;

        public int numPrimFaces;
        public float pathCutBegin;
        public float pathCutEnd = 1.0f;
        private readonly float profileEnd = 1.0f;

        private readonly float profileStart;
        public float radius;
        public float revolutions = 1.0f;

        private readonly int sides = 4;
        public float skew;
        public bool sphereMode = false;
        public int stepsPerRevolution = 24;
        public float taperX;
        public float taperY;
        public float topShearX;
        public float topShearY;
        public int twistBegin;
        public int twistEnd;

        public List<ViewerFace> viewerFaces;
        public bool viewerMode;


        /// <summary>
        ///     Constructs a PrimMesh object and creates the profile for extrusion.
        /// </summary>
        /// <param name="sides"></param>
        /// <param name="profileStart"></param>
        /// <param name="profileEnd"></param>
        /// <param name="hollow"></param>
        /// <param name="hollowSides"></param>
        public PrimMesh(int sides, float profileStart, float profileEnd, float hollow, int hollowSides)
        {
            coords = new List<Coord>();
            faces = new List<Face>();

            this.sides = sides;
            this.profileStart = profileStart;
            this.profileEnd = profileEnd;
            this.hollow = hollow;
            this.hollowSides = hollowSides;

            if (sides < 3)
                this.sides = 3;
            if (hollowSides < 3)
                this.hollowSides = 3;
            if (profileStart < 0.0f)
                this.profileStart = 0.0f;
            if (profileEnd > 1.0f)
                this.profileEnd = 1.0f;
            if (profileEnd < 0.02f)
                this.profileEnd = 0.02f;
            if (profileStart >= profileEnd)
                this.profileStart = profileEnd - 0.02f;
            if (hollow > 0.99f)
                this.hollow = 0.99f;
            if (hollow < 0.0f)
                this.hollow = 0.0f;
        }

        public int ProfileOuterFaceNumber { get; private set; } = -1;

        public int ProfileHollowFaceNumber { get; private set; } = -1;

        public bool HasProfileCut { get; private set; }

        public bool HasHollow { get; private set; }

        /// <summary>
        ///     Human-readable string representation of the parameters used to create a mesh.
        /// </summary>
        /// <returns></returns>
        public string ParamsToDisplayString()
        {
            var sb = new StringBuilder(512);
            sb.AppendFormat("sides..................: {0}", sides);
            sb.AppendLine();
            sb.AppendFormat("hollowSides..........: {0}", hollowSides);
            sb.AppendLine();
            sb.AppendFormat("profileStart.........: {0}", profileStart);
            sb.AppendLine();
            sb.AppendFormat("profileEnd...........: {0}", profileEnd);
            sb.AppendLine();
            sb.AppendFormat("hollow...............: {0}", hollow);
            sb.AppendLine();
            sb.AppendFormat("twistBegin...........: {0}", twistBegin);
            sb.AppendLine();
            sb.AppendFormat("twistEnd.............: {0}", twistEnd);
            sb.AppendLine();
            sb.AppendFormat("topShearX............: {0}", topShearX);
            sb.AppendLine();
            sb.AppendFormat("topShearY............: {0}", topShearY);
            sb.AppendLine();
            sb.AppendFormat("pathCutBegin.........: {0}", pathCutBegin);
            sb.AppendLine();
            sb.AppendFormat("pathCutEnd...........: {0}", pathCutEnd);
            sb.AppendLine();
            sb.AppendFormat("dimpleBegin..........: {0}", dimpleBegin);
            sb.AppendLine();
            sb.AppendFormat("dimpleEnd............: {0}", dimpleEnd);
            sb.AppendLine();
            sb.AppendFormat("skew.................: {0}", skew);
            sb.AppendLine();
            sb.AppendFormat("holeSizeX............: {0}", holeSizeX);
            sb.AppendLine();
            sb.AppendFormat("holeSizeY............: {0}", holeSizeY);
            sb.AppendLine();
            sb.AppendFormat("taperX...............: {0}", taperX);
            sb.AppendLine();
            sb.AppendFormat("taperY...............: {0}", taperY);
            sb.AppendLine();
            sb.AppendFormat("radius...............: {0}", radius);
            sb.AppendLine();
            sb.AppendFormat("revolutions..........: {0}", revolutions);
            sb.AppendLine();
            sb.AppendFormat("stepsPerRevolution...: {0}", stepsPerRevolution);
            sb.AppendLine();
            sb.AppendFormat("sphereMode...........: {0}", sphereMode);
            sb.AppendLine();
            sb.AppendFormat("hasProfileCut........: {0}", HasProfileCut);
            sb.AppendLine();
            sb.AppendFormat("hasHollow............: {0}", HasHollow);
            sb.AppendLine();
            sb.AppendFormat("viewerMode...........: {0}", viewerMode);

            return sb.ToString();
        }

        /// <summary>
        ///     Extrudes a profile along a path.
        /// </summary>
        public void Extrude(PathType pathType)
        {
            bool needEndFaces;

            coords = new List<Coord>();
            this.faces = new List<Face>();

            if (viewerMode)
            {
                // Pre-size viewerFaces to reduce growth allocations. Estimate: layers * verts * 2
                // We'll estimate conservatively; path created later but this helps avoid many small resizes
                viewerFaces = new List<ViewerFace>(64);
                calcVertexNormals = true;
            }

            if (calcVertexNormals)
                normals = new List<Coord>();

            var steps = 1;

            var length = pathCutEnd - pathCutBegin;
            var twistTotal = twistEnd - twistBegin;
            var twistTotalAbs = Math.Abs(twistTotal);
            if (twistTotalAbs > 0.01f)
                steps += (int)(twistTotalAbs * 3.66); //  dahlia's magic number

            var hollow = this.hollow;

            if (pathType == PathType.Circular)
            {
                needEndFaces = false;
                if (pathCutBegin != 0.0f || pathCutEnd != 1.0f)
                    needEndFaces = true;
                else if (taperX != 0.0f || taperY != 0.0f)
                    needEndFaces = true;
                else if (skew != 0.0f)
                    needEndFaces = true;
                else if (twistTotal != 0.0f)
                    needEndFaces = true;
                else if (radius != 0.0f)
                    needEndFaces = true;
            }
            else
            {
                needEndFaces = true;
            }

            // sanity checks
            var initialProfileRot = 0.0f;
            if (pathType == PathType.Circular)
            {
                switch (sides)
                {
                    case 3:
                        initialProfileRot = (float)Math.PI;
                        if (hollowSides == 4)
                        {
                            if (hollow > 0.7f)
                                hollow = 0.7f;
                            hollow *= 0.707f;
                        }
                        else
                        {
                            hollow *= 0.5f;
                        }
                        break;
                    case 4:
                        initialProfileRot = 0.25f * (float)Math.PI;
                        if (hollowSides != 4)
                            hollow *= 0.707f;
                        break;
                    default:
                        if (sides > 4)
                        {
                            initialProfileRot = (float)Math.PI;
                            if (hollowSides == 4)
                            {
                                if (hollow > 0.7f)
                                    hollow = 0.7f;
                                hollow /= 0.7f;
                            }
                        }
                        break;
                }
            }
            else
            {
                switch (sides)
                {
                    case 3:
                        if (hollowSides == 4)
                        {
                            if (hollow > 0.7f)
                                hollow = 0.7f;
                            hollow *= 0.707f;
                        }
                        else
                        {
                            hollow *= 0.5f;
                        }
                        break;
                    case 4:
                        initialProfileRot = 1.25f * (float)Math.PI;
                        if (hollowSides != 4)
                            hollow *= 0.707f;
                        break;
                    case 24 when hollowSides == 4:
                        hollow *= 1.414f;
                        break;
                }
            }

            var profile = new Profile(sides, profileStart, profileEnd, hollow, hollowSides, true, calcVertexNormals);
            errorMessage = profile.errorMessage;

            numPrimFaces = profile.numPrimFaces;

            var cut1FaceNumber = profile.bottomFaceNumber + 1;
            var cut2FaceNumber = cut1FaceNumber + 1;
            if (!needEndFaces)
            {
                cut1FaceNumber -= 2;
                cut2FaceNumber -= 2;
            }

            ProfileOuterFaceNumber = profile.outerFaceNumber;
            if (!needEndFaces)
                ProfileOuterFaceNumber--;

            if (HasHollow)
            {
                ProfileHollowFaceNumber = profile.hollowFaceNumber;
                if (!needEndFaces)
                    ProfileHollowFaceNumber--;
            }

            var cut1Vert = -1;
            var cut2Vert = -1;
            if (HasProfileCut)
            {
                cut1Vert = HasHollow ? profile.coords.Count - 1 : 0;
                cut2Vert = HasHollow ? profile.numOuterVerts - 1 : profile.numOuterVerts;
            }

            if (initialProfileRot != 0.0f)
            {
                profile.AddRot(new Quat(new Coord(0.0f, 0.0f, 1.0f), initialProfileRot));
                if (viewerMode)
                    profile.MakeFaceUVs();
            }

            var lastCutNormal1 = new Coord();
            var lastCutNormal2 = new Coord();
            var lastV = 0.0f;

            var path = new Path
            {
                twistBegin = twistBegin,
                twistEnd = twistEnd,
                topShearX = topShearX,
                topShearY = topShearY,
                pathCutBegin = pathCutBegin,
                pathCutEnd = pathCutEnd,
                dimpleBegin = dimpleBegin,
                dimpleEnd = dimpleEnd,
                skew = skew,
                holeSizeX = holeSizeX,
                holeSizeY = holeSizeY,
                taperX = taperX,
                taperY = taperY,
                radius = radius,
                revolutions = revolutions,
                stepsPerRevolution = stepsPerRevolution
            };

            path.Create(pathType, steps);

            for (var nodeIndex = 0; nodeIndex < path.pathNodes.Count; nodeIndex++)
            {
                var node = path.pathNodes[nodeIndex];

                // Transform profile coords into a temporary pooled buffer to avoid copying entire Profile
                var profileCount = profile.coords.Count;
                var coordsLen = coords.Count;
                var pool = ArrayPool<Coord>.Shared;
                var tmp = pool.Rent(profileCount);
                try
                {
                    for (var ci = 0; ci < profileCount; ci++)
                    {
                        var v = profile.coords[ci];
                        v.X *= node.xScale;
                        v.Y *= node.yScale;
                        v *= node.rotation;
                        v.X += node.position.X;
                        v.Y += node.position.Y;
                        v.Z += node.position.Z;
                        tmp[ci] = v;
                    }

                    // append transformed coords
                    for (var ci = 0; ci < profileCount; ci++) coords.Add(tmp[ci]);

                    // normals
                    if (calcVertexNormals)
                    {
                        for (var ni = 0; ni < profile.vertexNormals.Count; ni++)
                        {
                            var n = profile.vertexNormals[ni];
                            n *= node.rotation;
                            normals.Add(n);
                        }
                    }

                    // add profile faces with index offsets
                    if (node.percentOfPath < pathCutBegin + 0.01f || node.percentOfPath > pathCutEnd - 0.01f)
                    {
                        var normalsOffset = (normals != null) ? (normals.Count - profile.vertexNormals.Count) : 0;
                        for (var fi = 0; fi < profile.faces.Count; fi++)
                        {
                            var f = profile.faces[fi];
                            var nf = new Face
                            {
                                primFace = f.primFace,
                                v1 = f.v1 + coordsLen,
                                v2 = f.v2 + coordsLen,
                                v3 = f.v3 + coordsLen,
                                uv1 = f.uv1,
                                uv2 = f.uv2,
                                uv3 = f.uv3
                            };
                            if (calcVertexNormals)
                            {
                                nf.n1 = f.n1 + normalsOffset;
                                nf.n2 = f.n2 + normalsOffset;
                                nf.n3 = f.n3 + normalsOffset;
                            }
                            faces.Add(nf);
                        }
                    }

                    // build side faces between layers
                    var numVerts = profileCount;
                    var thisV = 1.0f - node.percentOfPath;
                    if (nodeIndex > 0)
                    {
                        var startVert = coordsLen + 1;
                        var endVert = coords.Count;
                        if (sides < 5 || HasProfileCut || HasHollow) startVert--;

                        for (var i = startVert; i < endVert; i++)
                        {
                            var iNext = i == endVert - 1 ? startVert : i + 1;
                            var whichVert = i - startVert;

                            var newFace1 = new Face { v1 = i, v2 = i - numVerts, v3 = iNext };
                            newFace1.n1 = newFace1.v1; newFace1.n2 = newFace1.v2; newFace1.n3 = newFace1.v3;
                            faces.Add(newFace1);

                            var newFace2 = new Face { v1 = iNext, v2 = i - numVerts, v3 = iNext - numVerts };
                            newFace2.n1 = newFace2.v1; newFace2.n2 = newFace2.v2; newFace2.n3 = newFace2.v3;
                            faces.Add(newFace2);

                            if (!viewerMode) continue;

                            var primFaceNum = profile.faceNumbers[whichVert]; if (!needEndFaces) primFaceNum--;
                            var vf1 = new ViewerFace(primFaceNum); var vf2 = new ViewerFace(primFaceNum);

                            var uIndex = whichVert; if (!HasHollow && sides > 4 && uIndex < profile.us.Count - 1) uIndex++;
                            var u1 = profile.us[uIndex]; var u2 = uIndex < profile.us.Count - 1 ? profile.us[uIndex + 1] : 1.0f;
                            if (whichVert == cut1Vert || whichVert == cut2Vert) { u1 = 0f; u2 = 1f; }
                            else if (sides < 5 && whichVert < profile.numOuterVerts) { u1 *= sides; u2 *= sides; u2 -= (int)u1; u1 -= (int)u1; if (u2 < 0.1f) u2 = 1f; }
                            if (sphereMode && whichVert != cut1Vert && whichVert != cut2Vert) { u1 = u1 * 2f - 1f; u2 = u2 * 2f - 1f; if (whichVert >= profile.numOuterVerts) { u1 -= hollow; u2 -= hollow; } }

                            vf1.uv1.U = u1; vf1.uv2.U = u1; vf1.uv3.U = u2; vf1.uv1.V = thisV; vf1.uv2.V = lastV; vf1.uv3.V = thisV;
                            vf2.uv1.U = u2; vf2.uv2.U = u1; vf2.uv3.U = u2; vf2.uv1.V = thisV; vf2.uv2.V = lastV; vf2.uv3.V = lastV;

                            vf1.v1 = coords[newFace1.v1]; vf1.v2 = coords[newFace1.v2]; vf1.v3 = coords[newFace1.v3];
                            vf2.v1 = coords[newFace2.v1]; vf2.v2 = coords[newFace2.v2]; vf2.v3 = coords[newFace2.v3];
                            vf1.coordIndex1 = newFace1.v1; vf1.coordIndex2 = newFace1.v2; vf1.coordIndex3 = newFace1.v3;
                            vf2.coordIndex1 = newFace2.v1; vf2.coordIndex2 = newFace2.v2; vf2.coordIndex3 = newFace2.v3;

                            // profile cut faces
                            if (whichVert == cut1Vert)
                            {
                                var tcn1 = profile.cutNormal1; tcn1 *= node.rotation;
                                vf1.primFaceNumber = cut1FaceNumber; vf2.primFaceNumber = cut1FaceNumber;
                                vf1.n1 = tcn1; vf1.n2 = vf1.n3 = lastCutNormal1; vf2.n1 = vf2.n3 = tcn1; vf2.n2 = lastCutNormal1;
                            }
                            else if (whichVert == cut2Vert)
                            {
                                var tcn2 = profile.cutNormal2; tcn2 *= node.rotation;
                                vf1.primFaceNumber = cut2FaceNumber; vf2.primFaceNumber = cut2FaceNumber;
                                vf1.n1 = tcn2; vf1.n2 = lastCutNormal2; vf1.n3 = lastCutNormal2; vf2.n1 = tcn2; vf2.n3 = tcn2; vf2.n2 = lastCutNormal2;
                            }
                            else
                            {
                                if (sides < 5 && whichVert < profile.numOuterVerts || hollowSides < 5 && whichVert >= profile.numOuterVerts)
                                {
                                    vf1.CalcSurfaceNormal(); vf2.CalcSurfaceNormal();
                                }
                                else
                                {
                                    vf1.n1 = normals[newFace1.n1]; vf1.n2 = normals[newFace1.n2]; vf1.n3 = normals[newFace1.n3];
                                    vf2.n1 = normals[newFace2.n1]; vf2.n2 = normals[newFace2.n2]; vf2.n3 = normals[newFace2.n3];
                                }
                            }

                            viewerFaces.Add(vf1); viewerFaces.Add(vf2);
                        }
                    }

                    lastCutNormal1 = profile.cutNormal1; lastCutNormal2 = profile.cutNormal2; lastV = thisV;

                    if (needEndFaces && nodeIndex == path.pathNodes.Count - 1 && viewerMode)
                    {
                        var faceNormal = profile.faceNormal;
                        for (var fi = 0; fi < profile.faces.Count; fi++)
                        {
                            var f = profile.faces[fi];
                            var nv = new ViewerFace(0);
                            nv.v1 = tmp[f.v1 - coordsLen]; nv.v2 = tmp[f.v2 - coordsLen]; nv.v3 = tmp[f.v3 - coordsLen];
                            nv.coordIndex1 = f.v1 - coordsLen; nv.coordIndex2 = f.v2 - coordsLen; nv.coordIndex3 = f.v3 - coordsLen;
                            nv.n1 = faceNormal; nv.n2 = faceNormal; nv.n3 = faceNormal;
                            nv.uv1 = profile.faceUVs[f.v1 - coordsLen]; nv.uv2 = profile.faceUVs[f.v2 - coordsLen]; nv.uv3 = profile.faceUVs[f.v3 - coordsLen];
                            if (pathType == PathType.Linear) { nv.uv1.Flip(); nv.uv2.Flip(); nv.uv3.Flip(); }
                            viewerFaces.Add(nv);
                        }
                    }
                }
                finally { pool.Return(tmp); }
            }
        }

        /// <summary>
        ///     Extrudes a profile along a straight line path. Used for prim types box, cylinder, and prism.
        /// </summary>
        [Obsolete("Use Extrude(PathType.Linear")]
        public void ExtrudeLinear()
        {
            Extrude(PathType.Linear);
        }

        /// <summary>
        ///     Extrude a profile into a circular path prim mesh. Used for prim types torus, tube, and ring.
        /// </summary>
        [Obsolete("Use Extrude(PathType.Circular")]
        public void ExtrudeCircular()
        {
            Extrude(PathType.Circular);
        }

        private static Coord SurfaceNormal(Coord c1, Coord c2, Coord c3)
        {
            var edge1 = new Coord(c2.X - c1.X, c2.Y - c1.Y, c2.Z - c1.Z);
            var edge2 = new Coord(c3.X - c1.X, c3.Y - c1.Y, c3.Z - c1.Z);

            var normal = Coord.Cross(edge1, edge2);

            normal.Normalize();

            return normal;
        }

        private Coord SurfaceNormal(Face face)
        {
            return SurfaceNormal(coords[face.v1], coords[face.v2], coords[face.v3]);
        }

        /// <summary>
        ///     Calculate the surface normal for a face in the list of faces
        /// </summary>
        /// <param name="faceIndex"></param>
        /// <returns></returns>
        public Coord SurfaceNormal(int faceIndex)
        {
            var numFaces = faces.Count;
            if (faceIndex < 0 || faceIndex >= numFaces)
                throw new Exception("faceIndex out of range");

            return SurfaceNormal(faces[faceIndex]);
        }

        /// <summary>
        ///     Duplicates a PrimMesh object. All object properties are copied by value, including lists.
        /// </summary>
        /// <returns></returns>
        public PrimMesh Copy()
        {
            var copy = new PrimMesh(sides, profileStart, profileEnd, hollow, hollowSides)
            {
                twistBegin = twistBegin,
                twistEnd = twistEnd,
                topShearX = topShearX,
                topShearY = topShearY,
                pathCutBegin = pathCutBegin,
                pathCutEnd = pathCutEnd,
                dimpleBegin = dimpleBegin,
                dimpleEnd = dimpleEnd,
                skew = skew,
                holeSizeX = holeSizeX,
                holeSizeY = holeSizeY,
                taperX = taperX,
                taperY = taperY,
                radius = radius,
                revolutions = revolutions,
                stepsPerRevolution = stepsPerRevolution,
                calcVertexNormals = calcVertexNormals,
                normalsProcessed = normalsProcessed,
                viewerMode = viewerMode,
                numPrimFaces = numPrimFaces,
                errorMessage = errorMessage,
                coords = coords != null ? new List<Coord>(coords) : null,
                faces = faces != null ? new List<Face>(faces) : null,
                viewerFaces = viewerFaces != null ? new List<ViewerFace>(viewerFaces) : null,
                normals = normals != null ? new List<Coord>(normals) : null
            };


            return copy;
        }

        /// <summary>
        ///     Calculate surface normals for all faces in the list of faces in this mesh
        /// </summary>
        public void CalcNormals()
        {
            if (normalsProcessed)
                return;

            normalsProcessed = true;

            var numFaces = faces.Count;

            if (!calcVertexNormals)
                normals = new List<Coord>(numFaces);

            for (var i = 0; i < numFaces; i++)
            {
                var face = faces[i];

                normals.Add(SurfaceNormal(i).Normalize());

                var normIndex = normals.Count - 1;
                face.n1 = normIndex;
                face.n2 = normIndex;
                face.n3 = normIndex;

                faces[i] = face;
            }
        }

        /// <summary>
        ///     Adds a value to each XYZ vertex coordinate in the mesh
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void AddPos(float x, float y, float z)
        {
            var numVerts = coords.Count;

            for (var i = 0; i < numVerts; i++)
            {
                var vert = coords[i];
                vert.X += x;
                vert.Y += y;
                vert.Z += z;
                coords[i] = vert;
            }

            if (viewerFaces != null)
            {
                var numViewerFaces = viewerFaces.Count;

                for (var i = 0; i < numViewerFaces; i++)
                {
                    var v = viewerFaces[i];
                    v.AddPos(x, y, z);
                    viewerFaces[i] = v;
                }
            }
        }

        /// <summary>
        ///     Rotates the mesh
        /// </summary>
        /// <param name="q"></param>
        public void AddRot(Quat q)
        {
            var numVerts = coords.Count;

            for (var i = 0; i < numVerts; i++)
                coords[i] *= q;

            if (normals != null)
            {
                var numNormals = normals.Count;
                for (var i = 0; i < numNormals; i++)
                    normals[i] *= q;
            }

            if (viewerFaces != null)
            {
                var numViewerFaces = viewerFaces.Count;

                for (var i = 0; i < numViewerFaces; i++)
                {
                    var v = viewerFaces[i];
                    v.v1 *= q;
                    v.v2 *= q;
                    v.v3 *= q;

                    v.n1 *= q;
                    v.n2 *= q;
                    v.n3 *= q;
                    viewerFaces[i] = v;
                }
            }
        }

#if VERTEX_INDEXER
        public VertexIndexer GetVertexIndexer()
        {
            if (viewerMode && viewerFaces.Count > 0)
                return new VertexIndexer(this);
            return null;
        }
#endif

        /// <summary>
        ///     Scales the mesh
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Scale(float x, float y, float z)
        {
            var numVerts = coords.Count;

            var m = new Coord(x, y, z);
            for (var i = 0; i < numVerts; i++)
                coords[i] *= m;

            if (viewerFaces != null)
            {
                var numViewerFaces = viewerFaces.Count;
                for (var i = 0; i < numViewerFaces; i++)
                {
                    var v = viewerFaces[i];
                    v.v1 *= m;
                    v.v2 *= m;
                    v.v3 *= m;
                    viewerFaces[i] = v;
                }
            }
        }

        /// <summary>
        ///     Dumps the mesh to a Blender compatible "Raw" format file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="title"></param>
        public void DumpRaw(string path, string name, string title)
        {
            if (path == null)
                return;
            var fileName = name + "_" + title + ".raw";
            var completePath = System.IO.Path.Combine(path, fileName);
            using (var sw = new StreamWriter(completePath))
            {
                for (var i = 0; i < faces.Count; i++)
                {
                    var s = coords[faces[i].v1].ToString();
                    s += " " + coords[faces[i].v2];
                    s += " " + coords[faces[i].v3];

                    sw.WriteLine(s);
                }
            }
        }
    }
}