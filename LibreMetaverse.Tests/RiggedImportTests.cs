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
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using LibreMetaverse.Assets.Gltf;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;
using NumMatrix = System.Numerics.Matrix4x4;
using NumVector3 = System.Numerics.Vector3;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Rigged mesh import: the skin block and weights of a mesh asset, <see cref="GltfLoader"/> reading skins, and
    /// the whole trip from a <see cref="GltfExporter"/> file back to an asset. The skinning maths is worked out in
    /// the tests with plain System.Numerics matrices and an explicit axis change, not with the code under test, and
    /// at a pose rather than at rest, because at rest a transposed or mis-conjugated matrix cancels itself out.
    /// </summary>
    internal static class LoaderExtensions
    {
        public static GltfLoader Also(this GltfLoader loader, Action<GltfLoader> action)
        {
            action(loader);
            return loader;
        }
    }

    [TestFixture]
    public class RiggedImportTests
    {
        private const float Tolerance = 6e-3f; // the decoder clamps a lone full weight to 0.999

        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-rig-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private static float Rad(float degrees) => degrees * (float)Math.PI / 180f;

        // Y-up to Z-up, written out: (x, y, z) -> (x, -z, y), as a row vector matrix
        private static readonly NumMatrix YUpToZUp = new NumMatrix(
            1, 0, 0, 0,
            0, 0, 1, 0,
            0, -1, 0, 0,
            0, 0, 0, 1);

        private static NumMatrix Inverse(NumMatrix m)
        {
            Assert.That(NumMatrix.Invert(m, out var inverse), Is.True);
            return inverse;
        }

        private static float[] Floats(NumMatrix m) => new[]
        {
            m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44
        };

        private static NumMatrix FromFloats(float[] f, int offset = 0) => new NumMatrix(
            f[offset], f[offset + 1], f[offset + 2], f[offset + 3], f[offset + 4], f[offset + 5], f[offset + 6], f[offset + 7],
            f[offset + 8], f[offset + 9], f[offset + 10], f[offset + 11], f[offset + 12], f[offset + 13], f[offset + 14], f[offset + 15]);

        private static void AssertClose(NumVector3 actual, NumVector3 expected, string what, float tolerance = Tolerance)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), what + " X");
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), what + " Y");
                Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), what + " Z");
            });
        }

        private static NumVector3 Num(Vector3 v) => new NumVector3(v.X, v.Y, v.Z);

        #region Shared fixture

        private static readonly Vector3[] TetraPositions =
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1)
        };
        private static readonly Vector3[] TetraNormals =
        {
            new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1)
        };
        private static readonly Vector2[] TetraUvs =
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1)
        };
        private static readonly ushort[] TetraIndices = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };

        // Per tetrahedron corner: (joint, weight) pairs, joint 0 = mTorso, joint 1 = mChest
        private static readonly (int, float)[][] TetraInfluences =
        {
            new[] { (0, 1f) },
            new[] { (1, 1f) },
            new[] { (0, 0.5f), (1, 0.5f) },
            new[] { (0, 0.25f), (1, 0.75f) }
        };

        private static VertexWeight Weight(params (int Joint, float Weight)[] influences)
        {
            var w = new VertexWeight();
            if (influences.Length > 0) { w.Joint0 = influences[0].Joint; w.Weight0 = influences[0].Weight; }
            if (influences.Length > 1) { w.Joint1 = influences[1].Joint; w.Weight1 = influences[1].Weight; }
            if (influences.Length > 2) { w.Joint2 = influences[2].Joint; w.Weight2 = influences[2].Weight; }
            if (influences.Length > 3) { w.Joint3 = influences[3].Joint; w.Weight3 = influences[3].Weight; }
            return w;
        }

        // Where the two joints rest in the Y-up scene of a hand-made glTF (row vectors). Different axes and
        // translations on purpose, so no matrix can be transposed or swapped without it showing.
        private static readonly NumMatrix RestTorsoY = NumMatrix.CreateRotationZ(Rad(20)) * NumMatrix.CreateTranslation(0.5f, 1.1f, 0.2f);
        private static readonly NumMatrix RestChestY = NumMatrix.CreateRotationX(Rad(-35)) * NumMatrix.CreateTranslation(-0.3f, 1.7f, 0.1f);

        private static Matrix4 NodeMatrix(NumMatrix rowVector) => new Matrix4(
            rowVector.M11, rowVector.M21, rowVector.M31, rowVector.M41,
            rowVector.M12, rowVector.M22, rowVector.M32, rowVector.M42,
            rowVector.M13, rowVector.M23, rowVector.M33, rowVector.M43,
            rowVector.M14, rowVector.M24, rowVector.M34, rowVector.M44);

        private static byte[] Bytes(IEnumerable<float> values)
        {
            var bytes = new List<byte>();
            foreach (var v in values) bytes.AddRange(BitConverter.GetBytes(v));
            return bytes.ToArray();
        }

        private static int AddView(GltfDocument doc, byte[] data)
        {
            if (doc.Buffers.Count == 0) doc.Buffers.Add(new GltfBuffer { Data = Array.Empty<byte>() });
            var buffer = doc.Buffers[0];
            var old = buffer.Data ?? Array.Empty<byte>();
            int offset = (old.Length + 3) & ~3;
            var grown = new byte[offset + data.Length];
            Buffer.BlockCopy(old, 0, grown, 0, old.Length);
            Buffer.BlockCopy(data, 0, grown, offset, data.Length);
            buffer.Data = grown;
            buffer.ByteLength = grown.Length;
            doc.BufferViews.Add(new GltfBufferView { Buffer = 0, ByteOffset = offset, ByteLength = data.Length });
            return doc.BufferViews.Count - 1;
        }

        private static int AddAccessor(GltfDocument doc, byte[] data, GltfComponentType component, GltfAccessorType type,
            int count, bool normalized = false)
        {
            doc.Accessors.Add(new GltfAccessor
            {
                BufferView = AddView(doc, data),
                ComponentType = component,
                Type = type,
                Count = count,
                Normalized = normalized
            });
            return doc.Accessors.Count - 1;
        }

        /// <summary>
        /// A Y-up glTF made by hand, with no help from the exporter: a tetrahedron stretched to 2 x 3 x 4, skinned to
        /// two avatar joints (mTorso, and mChest below it), a node transform on the mesh that has to be ignored.
        /// </summary>
        private static GltfDocument ForeignSkinned(Action<GltfDocument>? tweak = null, string[]? jointNames = null,
            float fileUnits = 1f, NumMatrix? torsoWorld = null, NumMatrix? chestWorld = null, NumMatrix? bakedTransform = null)
        {
            jointNames ??= new[] { "mTorso", "mChest" };
            var torso = torsoWorld ?? RestTorsoY;
            var chest = chestWorld ?? RestChestY;
            var bake = bakedTransform ?? NumMatrix.CreateScale(1f / fileUnits);
            var doc = new GltfDocument();

            // fileUnits stretches the mesh (100 for a file in centimetres); the inverse bind matrices then
            // carry the scale that brings it back, as they do in files made that way
            var positions = TetraPositions.Select(p => new Vector3(p.X * 2 * fileUnits, p.Y * 3 * fileUnits, p.Z * 4 * fileUnits)).ToArray();
            var primitive = new GltfPrimitive();
            primitive.Attributes[GltfPrimitive.ATTR_POSITION] = AddAccessor(doc,
                Bytes(positions.SelectMany(p => new[] { p.X, p.Y, p.Z })), GltfComponentType.Float, GltfAccessorType.Vec3, 4);
            primitive.Attributes[GltfPrimitive.ATTR_NORMAL] = AddAccessor(doc,
                Bytes(TetraNormals.SelectMany(n => new[] { n.X, n.Y, n.Z })), GltfComponentType.Float, GltfAccessorType.Vec3, 4);
            primitive.Attributes[GltfPrimitive.ATTR_TEXCOORD_0] = AddAccessor(doc,
                Bytes(TetraUvs.SelectMany(u => new[] { u.X, u.Y })), GltfComponentType.Float, GltfAccessorType.Vec2, 4);

            // Joints as bytes, weights as floats
            var joints = new List<byte>();
            var weights = new List<float>();
            foreach (var corner in TetraInfluences)
            {
                for (int k = 0; k < 4; k++)
                {
                    joints.Add(k < corner.Length ? (byte)corner[k].Item1 : (byte)0);
                    weights.Add(k < corner.Length ? corner[k].Item2 : 0f);
                }
            }
            primitive.Attributes[GltfPrimitive.ATTR_JOINTS_0] = AddAccessor(doc, joints.ToArray(),
                GltfComponentType.UnsignedByte, GltfAccessorType.Vec4, 4);
            primitive.Attributes[GltfPrimitive.ATTR_WEIGHTS_0] = AddAccessor(doc, Bytes(weights),
                GltfComponentType.Float, GltfAccessorType.Vec4, 4);
            primitive.Indices = AddAccessor(doc, TetraIndices.SelectMany(BitConverter.GetBytes).ToArray(),
                GltfComponentType.UnsignedShort, GltfAccessorType.Scalar, TetraIndices.Length);

            var mesh = new GltfMesh { Name = "Body" };
            mesh.Primitives.Add(primitive);
            doc.Meshes.Add(mesh);

            // 0 = skinned mesh node (its translation must be ignored), 1 = torso, 2 = chest (child of the torso)
            doc.Nodes.Add(new GltfNode { Name = "Body", Mesh = 0, Skin = 0, Translation = new Vector3(50, 0, 0) });
            doc.Nodes.Add(new GltfNode { Name = jointNames[0], Matrix = NodeMatrix(torso) });
            doc.Nodes.Add(new GltfNode { Name = jointNames[1], Matrix = NodeMatrix(chest * Inverse(torso)) });
            doc.Nodes[1].Children.Add(2);

            var skin = new GltfSkin { Name = "Rig" };
            skin.Joints.Add(1);
            skin.Joints.Add(2);
            // Column vector floats of a column vector matrix are the row major floats of its row vector form
            skin.InverseBindMatrices = AddAccessor(doc,
                Bytes(Floats(bake * Inverse(torso)).Concat(Floats(bake * Inverse(chest)))),
                GltfComponentType.Float, GltfAccessorType.Mat4, 2);
            doc.Skins.Add(skin);

            var scene = new GltfScene();
            scene.Nodes.AddRange(new[] { 0, 1 });
            doc.Scenes.Add(scene);
            doc.DefaultScene = 0;

            tweak?.Invoke(doc);
            return doc;
        }

        private string Write(GltfDocument doc, string name = "rig.glb")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, doc.ToGlb());
            return path;
        }

        private static FacetedMesh Decode(ModelPrim prim)
        {
            var primitive = new Primitive { LocalID = 1, Scale = prim.Scale };
            Assert.That(FacetedMesh.TryDecodeFromAsset(primitive, new AssetMesh(UUID.Random(), prim.Asset),
                DetailLevel.Highest, out var mesh), Is.True);
            return mesh!;
        }

        /// <summary>Second Life's skinning, worked from first principles with row vectors, on what a viewer decodes:
        /// vertex * bindShape * sum(weight * inverseBind * jointWorld), and the normal through the bind shape's
        /// inverse transpose. The live joints are by name.</summary>
        private static (List<NumVector3> Positions, List<NumVector3> Normals) Skinned(FacetedMesh mesh, Dictionary<string, NumMatrix> live)
        {
            var skin = mesh.SkinData!;
            var bindShape = FromFloats(skin.BindShapeMatrix);
            NumMatrix.Invert(bindShape, out var inverseBindShape);
            var normalMatrix = NumMatrix.Transpose(inverseBindShape);

            var positions = new List<NumVector3>();
            var normals = new List<NumVector3>();
            var face = mesh.Faces[0];
            foreach (var index in face.Indices)
            {
                var vertex = face.Vertices[index];
                var w = face.Weights![index];
                var sum = new NumMatrix();
                foreach (var (joint, weight) in new[] { (w.Joint0, w.Weight0), (w.Joint1, w.Weight1), (w.Joint2, w.Weight2), (w.Joint3, w.Weight3) })
                {
                    if (weight == 0f) continue;
                    var inverseBind = FromFloats(skin.InverseBindMatrices, joint * 16);
                    sum += (inverseBind * live[skin.JointNames[joint]]) * weight;
                }
                positions.Add(NumVector3.Transform(NumVector3.Transform(Num(vertex.Position), bindShape), sum));

                var normal = NumVector3.Normalize(NumVector3.TransformNormal(Num(vertex.Normal), normalMatrix));
                normals.Add(NumVector3.Normalize(NumVector3.TransformNormal(normal, sum)));
            }
            return (positions, normals);
        }

        #endregion Shared fixture

        #region Mesh asset

        private static ModelPrim RiggedPrim()
        {
            var prim = new ModelPrim { Scale = new Vector3(1, 1, 1) };
            prim.Skin = new ModelSkin
            {
                JointNames = new List<string> { "mTorso", "mChest", "mNeck" },
                InverseBindMatrices = Enumerable.Range(0, 48).Select(i => (float)(i * 0.25 - 3)).ToArray(),
                BindShapeMatrix = new float[] { 2, 0, 0, 0, 0, 3, 0, 0, 0, 0, 4, 0, 0.5f, -1.5f, 2.5f, 1 }
            };
            var face = new ModelFace { MaterialID = "m" };

            // Vertices are written in order of first use: corners 0, 2, 1, 3. Corner 0 has four influences, so it has
            // no terminator, and corner 2 right behind it has a single influence.
            var weights = new[]
            {
                new VertexWeight { Joint0 = 0, Weight0 = 0.4f, Joint1 = 1, Weight1 = 0.3f, Joint2 = 2, Weight2 = 0.2f, Joint3 = 0, Weight3 = 0.1f },
                Weight((0, 0.5f), (2, 0.5f)),
                Weight((1, 1f)),
                Weight((2, 0.25f), (1, 0.75f))
            };
            for (int t = 0; t < TetraIndices.Length; t++)
            {
                int c = TetraIndices[t];
                face.AddVertex(new Vertex
                {
                    Position = TetraPositions[c] - new Vector3(0.5f, 0.5f, 0.5f),
                    Normal = TetraNormals[c],
                    TexCoord = TetraUvs[c]
                }, weights[c]);
            }
            prim.Faces.Add(face);
            return prim;
        }

        [Test]
        public void CreateAsset_WithASkin_DecodesToTheSameSkinAndWeights()
        {
            var prim = RiggedPrim();
            prim.CreateAsset(UUID.Zero);

            var mesh = Decode(prim);

            var skin = mesh.SkinData!;
            Assert.Multiple(() =>
            {
                Assert.That(skin.JointNames, Is.EqualTo(new[] { "mTorso", "mChest", "mNeck" }));
                Assert.That(skin.InverseBindMatrices, Is.EqualTo(prim.Skin!.InverseBindMatrices));
                Assert.That(skin.BindShapeMatrix, Is.EqualTo(prim.Skin.BindShapeMatrix));
            });

            var face = mesh.Faces[0];
            var sourceFace = prim.Faces[0];
            Assert.That(face.Weights, Is.Not.Null);
            Assert.That(face.Weights!, Has.Count.EqualTo(face.Vertices.Count));
            Assert.That(face.Weights!.Count, Is.EqualTo(sourceFace.Weights!.Count));
            for (int i = 0; i < face.Weights.Count; i++)
            {
                // The same joints with the same weights, however the influences are ordered
                Assert.That(Influences(face.Weights[i]), Is.EqualTo(Influences(sourceFace.Weights[i])).Using<(int, float)>(
                    (a, b) => a.Item1 == b.Item1 && Math.Abs(a.Item2 - b.Item2) <= 0.002f), $"vertex {i}");
            }
        }

        private static List<(int, float)> Influences(VertexWeight w)
        {
            return new[] { (w.Joint0, w.Weight0), (w.Joint1, w.Weight1), (w.Joint2, w.Weight2), (w.Joint3, w.Weight3) }
                .Where(e => e.Item2 > 0.0015f).OrderBy(e => e.Item1).ThenBy(e => e.Item2).ToList();
        }

        [Test]
        public void CreateAsset_WithoutASkin_HasNoSkinOrWeights()
        {
            var prim = RiggedPrim();
            prim.Skin = null;
            foreach (var face in prim.Faces) face.Weights = null;
            prim.CreateAsset(UUID.Zero);

            var mesh = Decode(prim);

            Assert.Multiple(() =>
            {
                Assert.That(mesh.SkinData, Is.Null);
                Assert.That(mesh.Faces[0].Weights, Is.Null);
                Assert.That(prim.Asset, Is.Not.Empty);
            });
        }

        [Test]
        public void CreateAsset_WeightsOfAVertexWithFourInfluences_DoNotSwallowTheNextVertex()
        {
            var prim = RiggedPrim();
            prim.CreateAsset(UUID.Zero);

            var face = Decode(prim).Faces[0];

            // Corner 0 has four influences and corner 2 has one right behind it. If the terminator rule were
            // wrong corner 2 would start in the middle of corner 0's data and come out as something else.
            var corner2 = Influences(face.Weights![face.Indices[1]]);
            Assert.That(corner2, Has.Count.EqualTo(1));
            Assert.That(corner2[0].Item1, Is.EqualTo(1));
            Assert.That(corner2[0].Item2, Is.EqualTo(1f).Within(0.002f));
        }

        [Test]
        public void AddVertex_SamePositionWithDifferentWeights_StaysTwoVertices()
        {
            var face = new ModelFace();
            var v = new Vertex { Position = new Vector3(0.1f, 0.2f, 0.3f), Normal = new Vector3(0, 0, 1) };

            face.AddVertex(v, Weight((0, 1f)));
            face.AddVertex(v, Weight((1, 1f)));
            face.AddVertex(v, Weight((0, 1f)));

            Assert.Multiple(() =>
            {
                Assert.That(face.Vertices, Has.Count.EqualTo(2));
                Assert.That(face.Weights, Has.Count.EqualTo(2));
                Assert.That(face.Indices, Is.EqualTo(new uint[] { 0, 1, 0 }));
            });
        }

        [Test]
        public void CreateAsset_SkinThatDoesNotAddUp_IsRefusedNotWritten()
        {
            var prim = RiggedPrim();
            prim.Skin!.InverseBindMatrices = new float[16];

            Assert.Throws<InvalidOperationException>(() => prim.CreateAsset(UUID.Zero));
        }

        [Test]
        public void CreateAsset_VertexWeightedToAJointTheSkinLacks_IsRefused()
        {
            var prim = RiggedPrim();
            prim.Skin!.JointNames.RemoveAt(2);
            prim.Skin.InverseBindMatrices = prim.Skin.InverseBindMatrices.Take(32).ToArray();

            Assert.Throws<InvalidOperationException>(() => prim.CreateAsset(UUID.Zero));
        }

        #endregion Mesh asset

        #region Foreign glTF

        private static readonly NumMatrix ZUpToYUpMatrix = NumMatrix.Transpose(YUpToZUp);

        private static Dictionary<string, NumMatrix> LiveJoints(NumMatrix torsoY, NumMatrix chestY)
        {
            // The avatar's joints sit where the file's do, in Second Life's axes: the same world matrices with the
            // axes turned on both sides, since the joint frames are turned with everything else
            return new Dictionary<string, NumMatrix>
            {
                ["mTorso"] = ZUpToYUpMatrix * torsoY * YUpToZUp,
                ["mChest"] = ZUpToYUpMatrix * chestY * YUpToZUp
            };
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_ForeignSkinnedFile_AsAuthored_SkinsLikeGltfDoesInSecondLifesAxes(bool posed)
        {
            // The joints of the file stand in for the avatar's, with the file's axes, which only an as-authored read keeps
            var prims = new GltfLoader { UseAvatarJointAxes = false }.Load(Write(ForeignSkinned()), loadImages: false);
            Assert.That(prims, Has.Count.EqualTo(1));
            var mesh = Decode(prims[0]);

            // Move the chest in the file's own scene (Y-up), with a turn and a shift
            var delta = posed ? NumMatrix.CreateRotationX(Rad(30)) * NumMatrix.CreateTranslation(0.2f, -0.1f, 0.4f) : NumMatrix.Identity;
            var posedChest = delta * RestChestY;

            // glTF's formula, by hand, in glTF's axes: vertex * sum(weight * inverseBind * jointWorld)
            var inverseBind = new[] { Inverse(RestTorsoY), Inverse(RestChestY) };
            var world = new[] { RestTorsoY, posedChest };
            var (positions, normals) = Skinned(mesh, LiveJoints(RestTorsoY, posedChest));

            for (int t = 0; t < TetraIndices.Length; t++)
            {
                int corner = TetraIndices[t];
                var sum = new NumMatrix();
                foreach (var (joint, weight) in TetraInfluences[corner]) sum += (inverseBind[joint] * world[joint]) * weight;

                var vertex = new NumVector3(TetraPositions[corner].X * 2, TetraPositions[corner].Y * 3, TetraPositions[corner].Z * 4);
                var expectedPosition = NumVector3.Transform(NumVector3.Transform(vertex, sum), YUpToZUp);
                var expectedNormal = NumVector3.Normalize(NumVector3.TransformNormal(
                    NumVector3.Normalize(NumVector3.TransformNormal(Num(TetraNormals[corner]), sum)), YUpToZUp));

                AssertClose(positions[t], expectedPosition, $"corner {t} position");
                AssertClose(normals[t], expectedNormal, $"corner {t} normal", 5e-3f);
            }
        }

        [Test]
        public void Load_ForeignSkinnedFile_AsAuthored_CarriesTheAvatarJointsAndTheirMatricesInSecondLifesAxes()
        {
            var prim = new GltfLoader { UseAvatarJointAxes = false }.Load(Write(ForeignSkinned()), loadImages: false)[0];

            var skin = prim.Skin!;
            var expected = new[] { Inverse(RestTorsoY), Inverse(RestChestY) }
                .Select(m => Floats(ZUpToYUpMatrix * m * YUpToZUp)).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(skin.JointNames, Is.EqualTo(new[] { "mTorso", "mChest" }));
                Assert.That(skin.InverseBindMatrices.Take(16), Is.EqualTo(expected[0]).Within(1e-5));
                Assert.That(skin.InverseBindMatrices.Skip(16), Is.EqualTo(expected[1]).Within(1e-5));
            });
        }

        [Test]
        public void Load_ForeignSkinnedFile_IsSizedAndBoundLikeAnyOtherMesh_AndIgnoresItsNodeTransform()
        {
            var prim = new GltfLoader().Load(Write(ForeignSkinned()), loadImages: false)[0];

            // 2 x 3 x 4 in Y-up is 2 wide, 4 deep and 3 tall in Z-up. The node's 50 metre shift is not used.
            var bind = FromFloats(prim.Skin!.BindShapeMatrix);
            Assert.Multiple(() =>
            {
                Assert.That(prim.Scale.X, Is.EqualTo(2f).Within(1e-5f));
                Assert.That(prim.Scale.Y, Is.EqualTo(4f).Within(1e-5f));
                Assert.That(prim.Scale.Z, Is.EqualTo(3f).Within(1e-5f));
                Assert.That(prim.Position.X, Is.EqualTo(1f).Within(1e-5f), "the middle of the mesh, with no shift");
                Assert.That(bind.M11, Is.EqualTo(2f).Within(1e-5f), "the bind shape undoes the fit into the unit cube");
                Assert.That(bind.M22, Is.EqualTo(4f).Within(1e-5f));
                Assert.That(bind.M33, Is.EqualTo(3f).Within(1e-5f));
                Assert.That(bind.M41, Is.EqualTo(prim.Position.X).Within(1e-5f));
            });
        }

        [Test]
        public void Load_Skin_WeightsThatDoNotAddUpToOne_AreNormalized()
        {
            var doc = ForeignSkinned(d => SetFloats(d, 4, 0, 2f, 0f, 0f, 0f, 0f, 3f, 1f, 0f));

            var face = Decode(new GltfLoader().Load(Write(doc), false)[0]).Faces[0];

            foreach (var w in face.Weights!)
            {
                Assert.That(w.Weight0 + w.Weight1 + w.Weight2 + w.Weight3, Is.EqualTo(1f).Within(1e-3f));
            }
        }

        [Test]
        public void Load_Skin_JointsTheAvatarDoesNotHave_AreDroppedAndTheRestRenormalized()
        {
            var prim = new GltfLoader().Load(Write(ForeignSkinned(jointNames: new[] { "mTorso", "Bone.001" })), false)[0];

            var mesh = Decode(prim);
            Assert.That(mesh.SkinData!.JointNames, Is.EqualTo(new[] { "mTorso" }));
            foreach (var w in mesh.Faces[0].Weights!)
            {
                Assert.That(w.Joint0, Is.EqualTo(0));
                Assert.That(w.Weight0, Is.GreaterThan(0.99f), "everything that is left follows the one joint");
            }
        }

        [Test]
        public void Load_Skin_WithNoJointOfTheAvatar_LoadsAsAnOrdinaryMesh()
        {
            var prims = new GltfLoader().Load(Write(ForeignSkinned(jointNames: new[] { "foo", "bar" })), false);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Skin, Is.Null);
            Assert.That(prims[0].Position.X, Is.GreaterThan(40f), "with no rig the node's transform places it");
            Assert.That(Decode(prims[0]).SkinData, Is.Null);
        }

        [Test]
        public void Load_Skin_TwoJointsWithTheSameAvatarName_ShareOne()
        {
            var prim = new GltfLoader().Load(Write(ForeignSkinned(jointNames: new[] { "mTorso", "mTorso" })), false)[0];

            var mesh = Decode(prim);
            Assert.That(mesh.SkinData!.JointNames, Is.EqualTo(new[] { "mTorso" }));
            Assert.That(mesh.Faces[0].Weights!.All(w => w.Joint0 == 0 && w.Weight0 > 0.99f), Is.True);
        }

        [Test]
        public void Load_Skin_WithMoreThanFourInfluences_StillLoadsOnTheFirstFour()
        {
            var doc = ForeignSkinned(d =>
            {
                d.Meshes[0].Primitives[0].Attributes["JOINTS_1"] = AddAccessor(d, new byte[16],
                    GltfComponentType.UnsignedByte, GltfAccessorType.Vec4, 4);
                d.Meshes[0].Primitives[0].Attributes["WEIGHTS_1"] = AddAccessor(d, Bytes(new float[16]),
                    GltfComponentType.Float, GltfAccessorType.Vec4, 4);
            });

            Assert.That(new GltfLoader().Load(Write(doc), false), Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_SkinnedMesh_WithTooManyJoints_IsRefused()
        {
            var path = Write(ForeignSkinned());

            Assert.That(new GltfLoader { MaxJoints = 2 }.Load(path, false), Has.Count.EqualTo(1), "control");
            Assert.That(new GltfLoader { MaxJoints = 1 }.Load(path, false), Is.Empty);
        }

        #endregion Foreign glTF



        #region Upload

        [Test]
        public async Task UploadModelAsync_RiggedPrim_SendsTheSkinAndWeightsInItsMesh()
        {
            const string capUrl = "http://test.invalid/new-file-agent-inventory";
            const string uploaderUrl = "http://test.invalid/uploader/rig";

            using var client = new FakeGridClient();
            client.AddCapability("NewFileAgentInventory", new Uri(capUrl));
            client.AddHttpResponse(new Uri(capUrl), HttpStatusCode.OK,
                "{\"state\":\"upload\",\"uploader\":\"" + uploaderUrl + "\",\"upload_price\":10}", "application/json");
            client.AddHttpResponse(new Uri(uploaderUrl), HttpStatusCode.OK,
                "{\"state\":\"complete\",\"new_inventory_item\":\"" + UUID.Random() + "\",\"new_asset\":\"" + UUID.Random() + "\"}",
                "application/json");

            var prims = new GltfLoader().Load(Write(ForeignSkinned()), loadImages: false);
            Assert.That(prims, Has.Count.EqualTo(1));

            var result = await client.Inventory.UploadModelAsync(prims, "Rigged", "a rigged mesh", UUID.Random(), Permissions.NoPermissions);
            Assert.That(result.Success, Is.True);

            // The mesh the grid receives is made again at upload time, so check what was really sent
            var uploadBody = (OSDMap)OSDParser.Deserialize(client.CapturedRequestBodies[1]);
            var sent = ((OSDArray)uploadBody["mesh_list"])[0].AsBinary();
            Assert.That(FacetedMesh.TryDecodeFromAsset(new Primitive { LocalID = 1 }, new AssetMesh(UUID.Random(), sent),
                DetailLevel.Highest, out var mesh), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(mesh!.SkinData!.JointNames, Is.EqualTo(new[] { "mTorso", "mChest" }));
                Assert.That(mesh.SkinData.InverseBindMatrices, Has.Length.EqualTo(32));
                Assert.That(mesh.Faces[0].Weights, Has.Count.EqualTo(mesh.Faces[0].Vertices.Count));
            });
        }

        #endregion Upload

        #region Avatar joint axes

        private static Dictionary<string, NumMatrix> DefaultSkeleton()
        {
            return AvatarBoneMath.BuildBoneWorldMatrices(LindenSkeleton.Load(), new Dictionary<string, BoneTransform>());
        }

        [Test]
        public void TheDefaultSkeletonsBones_HaveNoRotationOrScale_ButItsCollisionVolumesDo()
        {
            // This is what decides how a rig's axes are read: bones are translation only, so a rig is bound to them
            // by position alone, while a collision volume has an orientation and size of its own to keep
            var skeleton = LindenSkeleton.Load();
            var worlds = DefaultSkeleton();
            var bones = skeleton.GetAllJoints().Select(j => j.name).ToList();

            foreach (var name in bones)
            {
                Assert.That(NumMatrix.Decompose(worlds[name], out var scale, out var rotation, out _), Is.True, name);
                Assert.Multiple(() =>
                {
                    Assert.That(Math.Abs(rotation.W), Is.EqualTo(1f).Within(1e-4f), name + " rotation");
                    Assert.That(new[] { scale.X, scale.Y, scale.Z }, Is.EqualTo(new[] { 1f, 1f, 1f }).Within(1e-4f), name + " scale");
                });
            }

            var volumes = worlds.Keys.Except(bones).Where(n => !skeleton.GetAllJoints().Any(j => j.GetAliasesList().Contains(n))).ToList();
            Assert.That(volumes, Is.Not.Empty);
            Assert.That(volumes.Any(n =>
            {
                NumMatrix.Decompose(worlds[n], out var scale, out var rotation, out _);
                return Math.Abs(Math.Abs(rotation.W) - 1f) > 1e-3f || Math.Abs(scale.X - 1f) > 1e-3f;
            }), Is.True, "at least one collision volume is not translation only");
        }

        /// <summary>
        /// A rig with Blender-style bone axes (turned, not the avatar's), at the default skeleton's joint positions,
        /// seen through the avatar's own joints, rest or posed. The glTF skinning is worked by hand in the file's
        /// axes; the Second Life side is what the loader made, decoded, with the real skeleton's matrices.
        /// </summary>
        [TestCase(false, 1f)]
        [TestCase(true, 1f)]
        [TestCase(true, 100f)]
        public void Load_RigWithItsOwnBoneAxes_SitsOnTheAvatarsJointsLikeTheFileSaysItShould(bool posed, float fileUnits)
        {
            var avatar = DefaultSkeleton();
            Func<string, NumMatrix> inYUp = name =>
            {
                var p = avatar[name];
                return NumMatrix.CreateTranslation(p.M41, p.M43, -p.M42); // (x, y, z) -> (x, z, -y)
            };
            // The file's joints: the avatar's positions, with axes of their own
            var torsoY = NumMatrix.CreateRotationZ(Rad(20)) * inYUp("mTorso");
            var chestY = NumMatrix.CreateRotationX(Rad(-35)) * inYUp("mChest");
            var doc = ForeignSkinned(fileUnits: fileUnits, torsoWorld: torsoY, chestWorld: chestY);
            var prims = new GltfLoader().Load(Write(doc), loadImages: false);
            Assert.That(prims, Has.Count.EqualTo(1));
            var mesh = Decode(prims[0]);

            // A move of the chest in world space, after its rest: the same in either set of axes once converted
            var deltaSl = posed ? NumMatrix.CreateRotationZ(Rad(30)) * NumMatrix.CreateTranslation(0.05f, -0.03f, 0.04f) : NumMatrix.Identity;
            var deltaY = YUpToZUp * deltaSl * NumMatrix.Transpose(YUpToZUp);

            var live = new Dictionary<string, NumMatrix> { ["mTorso"] = avatar["mTorso"], ["mChest"] = avatar["mChest"] * deltaSl };
            var (positions, normals) = Skinned(mesh, live);

            var world = new[] { torsoY, chestY * deltaY };
            var inverseBind = new[] { NumMatrix.CreateScale(1f / fileUnits) * Inverse(torsoY), NumMatrix.CreateScale(1f / fileUnits) * Inverse(chestY) };
            for (int t = 0; t < TetraIndices.Length; t++)
            {
                int corner = TetraIndices[t];
                var sum = new NumMatrix();
                foreach (var (joint, weight) in TetraInfluences[corner]) sum += (inverseBind[joint] * world[joint]) * weight;

                var vertex = new NumVector3(TetraPositions[corner].X * 2 * fileUnits, TetraPositions[corner].Y * 3 * fileUnits, TetraPositions[corner].Z * 4 * fileUnits);
                var expectedPosition = NumVector3.Transform(NumVector3.Transform(vertex, sum), YUpToZUp);
                var expectedNormal = NumVector3.Normalize(NumVector3.TransformNormal(
                    NumVector3.Normalize(NumVector3.TransformNormal(Num(TetraNormals[corner]), sum)), YUpToZUp));

                AssertClose(positions[t], expectedPosition, $"corner {t} position");
                AssertClose(normals[t], expectedNormal, $"corner {t} normal", 5e-3f);
            }
        }

        [Test]
        public void Load_RigWeightedToACollisionVolume_KeepsTheAvatarsAxesAndSizeThere()
        {
            // Fitted mesh is weighted to collision volumes, which unlike bones have a rotation and scale at rest. The
            // mesh must look right at rest on the avatar's own volume, wherever the file's rig turned it.
            var avatar = DefaultSkeleton();
            var skeleton = LindenSkeleton.Load();
            var bones = skeleton.GetAllJoints().Select(j => j.name).ToHashSet();
            var volume = avatar.Where(pair => !bones.Contains(pair.Key) &&
                    !skeleton.GetAllJoints().Any(j => j.GetAliasesList().Contains(pair.Key)) &&
                    NumMatrix.Decompose(pair.Value, out var sc, out var ro, out _) &&
                    (Math.Abs(Math.Abs(ro.W) - 1f) > 1e-3f || Math.Abs(sc.X - 1f) > 1e-3f))
                .Select(pair => pair.Key).First();

            Func<string, NumMatrix> inYUp = name => NumMatrix.CreateTranslation(avatar[name].M41, avatar[name].M43, -avatar[name].M42);
            var doc = ForeignSkinned(jointNames: new[] { "mTorso", volume },
                torsoWorld: NumMatrix.CreateRotationZ(Rad(20)) * inYUp("mTorso"),
                chestWorld: NumMatrix.CreateRotationX(Rad(-35)) * inYUp(volume));

            var mesh = Decode(new GltfLoader().Load(Write(doc), false)[0]);

            var live = new Dictionary<string, NumMatrix> { ["mTorso"] = avatar["mTorso"], [volume] = avatar[volume] };
            var (positions, _) = Skinned(mesh, live);
            for (int t = 0; t < TetraIndices.Length; t++)
            {
                int corner = TetraIndices[t];
                // At rest the mesh is where the file put it, in Z-up
                var vertex = new NumVector3(TetraPositions[corner].X * 2, TetraPositions[corner].Y * 3, TetraPositions[corner].Z * 4);
                AssertClose(positions[t], NumVector3.Transform(vertex, YUpToZUp), $"corner {t} at rest on {volume}");
            }
        }

        [Test]
        public void Load_RigWhoseMeshIsStretchedByItsBindMatrices_KeepsNormalsSquareToTheSurface()
        {
            // The inverse bind matrices stretch the mesh unevenly on its way into the scene, so its normals have to
            // be turned with the inverse transpose of that, or they tilt off the surface
            var bake = NumMatrix.CreateScale(1f, 0.4f, 2.5f);
            var a = new Vector3(0, 0, 0); var b = new Vector3(2, 0.5f, 0); var c = new Vector3(0.3f, 1.5f, 1f);
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            var doc = ForeignSkinned(bakedTransform: bake, tweak: d =>
            {
                SetFloats(d, 0, 0, a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z);
                SetFloats(d, 1, 0, n.X, n.Y, n.Z, n.X, n.Y, n.Z, n.X, n.Y, n.Z);
            });

            var mesh = Decode(new GltfLoader().Load(Write(doc), false)[0]);
            // The avatar's joints sit at the file's joint positions (and are bones, so have no rotation)
            Func<NumMatrix, NumMatrix> avatarJoint = world =>
            {
                var p = NumVector3.Transform(new NumVector3(world.M41, world.M42, world.M43), YUpToZUp);
                return NumMatrix.CreateTranslation(p);
            };
            var (positions, normals) = Skinned(mesh, new Dictionary<string, NumMatrix>
            {
                ["mTorso"] = avatarJoint(RestTorsoY),
                ["mChest"] = avatarJoint(RestChestY)
            });

            var e1 = positions[1] - positions[0];
            var e2 = positions[2] - positions[0];
            Assert.Multiple(() =>
            {
                Assert.That(NumVector3.Dot(normals[0], NumVector3.Normalize(e1)), Is.EqualTo(0f).Within(2e-2f));
                Assert.That(NumVector3.Dot(normals[0], NumVector3.Normalize(e2)), Is.EqualTo(0f).Within(2e-2f));
            });
        }

        [Test]
        public void Load_RigWithItsOwnBoneAxes_AsAuthored_TurnsTheMeshOnTheAvatar()
        {
            // The reason for the default: carrying the file's turned axes over to joints that have none
            var avatar = DefaultSkeleton();
            var torsoY = NumMatrix.CreateRotationZ(Rad(20)) * NumMatrix.CreateTranslation(avatar["mTorso"].M41, avatar["mTorso"].M43, -avatar["mTorso"].M42);
            var chestY = NumMatrix.CreateRotationX(Rad(-35)) * NumMatrix.CreateTranslation(avatar["mChest"].M41, avatar["mChest"].M43, -avatar["mChest"].M42);
            var path = Write(ForeignSkinned(torsoWorld: torsoY, chestWorld: chestY));

            var snapped = Skinned(Decode(new GltfLoader().Load(path, false)[0]), avatar).Positions;
            var authored = Skinned(Decode(new GltfLoader { UseAvatarJointAxes = false }.Load(path, false)[0]), avatar).Positions;

            double apart = 0;
            for (int i = 0; i < snapped.Count; i++) apart += (snapped[i] - authored[i]).Length();
            Assert.That(apart / snapped.Count, Is.GreaterThan(0.1), "the two readings must differ for the rotated rig");
        }

        #endregion Avatar joint axes

        #region Hostile input

        // Accessors of ForeignSkinned: 0 POSITION, 1 NORMAL, 2 TEXCOORD_0, 3 JOINTS_0, 4 WEIGHTS_0, 5 indices, 6 inverse bind matrices
        private static void SetFloats(GltfDocument doc, int accessor, int firstElement, params float[] values)
        {
            var view = doc.BufferViews[doc.Accessors[accessor].BufferView];
            for (int i = 0; i < values.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, doc.Buffers[0].Data!, view.ByteOffset + (firstElement + i) * 4, 4);
            }
        }

        private void AssertRefused(Action<GltfDocument> hostile, GltfLoader? loader = null)
        {
            loader ??= new GltfLoader();
            Assert.That(loader.Load(Write(ForeignSkinned(), "control.glb"), false), Has.Count.EqualTo(1),
                "the control must load, or this test proves nothing");
            Assert.That(loader.Load(Write(ForeignSkinned(hostile), "hostile.glb"), false), Is.Empty);
        }

        [Test]
        public void Load_JointIndexBeyondTheSkin_IsRefused() =>
            AssertRefused(d => d.Buffers[0].Data![d.BufferViews[d.Accessors[3].BufferView].ByteOffset] = 7);

        [Test]
        public void Load_NegativeWeight_IsRefused() => AssertRefused(d => SetFloats(d, 4, 0, -0.5f));

        [Test]
        public void Load_NaNWeight_IsRefused() => AssertRefused(d => SetFloats(d, 4, 1, float.NaN));

        [Test]
        public void Load_InfiniteWeight_IsRefused() => AssertRefused(d => SetFloats(d, 4, 0, float.PositiveInfinity));

        [Test]
        public void Load_NaNInverseBindMatrix_IsRefused() => AssertRefused(d => SetFloats(d, 6, 5, float.NaN));

        [Test]
        public void Load_FewerInverseBindMatricesThanJoints_IsRefused() => AssertRefused(d => d.Accessors[6].Count = 1);

        [Test]
        public void Load_InverseBindMatricesThatAreNotMatrices_AreRefused() => AssertRefused(d => d.Accessors[6].Type = GltfAccessorType.Vec4);

        [Test]
        public void Load_JointsThatAreFloats_AreRefused() => AssertRefused(d => d.Accessors[3].ComponentType = GltfComponentType.Float);

        [Test]
        public void Load_WeightsThatAreNotVec4_AreRefused() => AssertRefused(d => d.Accessors[4].Type = GltfAccessorType.Vec3);

        [Test]
        public void Load_WeightsWithAnotherCountThanTheVertices_AreRefused() => AssertRefused(d => d.Accessors[4].Count = 3);

        [Test]
        public void Load_JointsReadingPastTheirBuffer_AreRefused() => AssertRefused(d => d.Accessors[3].ByteOffset = 1000);

        [Test]
        public void Load_SkinJointNodeThatDoesNotExist_IsRefused() => AssertRefused(d => d.Skins[0].Joints[1] = 99);

        [Test]
        public void Load_SkinWithNoJoints_IsRefused() => AssertRefused(d => d.Skins[0].Joints.Clear());

        [Test]
        public void Load_NodeSkinThatDoesNotExist_IsRefused() => AssertRefused(d => d.Nodes[0].Skin = 5);

        [Test]
        public void Load_SkinnedPrimitiveWithoutJointsAndWeights_IsRefused() =>
            AssertRefused(d => d.Meshes[0].Primitives[0].Attributes.Remove(GltfPrimitive.ATTR_JOINTS_0));

        [Test]
        public void Load_SkinnedModelOverTheVertexBudget_IsRefused()
        {
            // The weights, joints and matrices count against the budget along with the geometry: this file holds
            // about 40 elements in all, and the joints and weights are most of the difference between the two limits
            var path = Write(ForeignSkinned());

            Assert.That(new GltfLoader { MaxVertices = 100 }.Load(path, false), Has.Count.EqualTo(1), "control");
            Assert.That(new GltfLoader { MaxVertices = 25 }.Load(path, false), Is.Empty);
        }

        #endregion Hostile input

        #region Exporter round trip

        // Where the avatar's joints are, with the axes the avatar has: none. Translation only.
        private static readonly NumMatrix RestTorsoSl = NumMatrix.CreateTranslation(0.5f, 0.2f, 1.1f);
        private static readonly NumMatrix RestChestSl = NumMatrix.CreateTranslation(-0.3f, 0.1f, 1.7f);
        // The same joints in a rig with axes of its own
        private static readonly NumMatrix RestTorsoRotatedSl = NumMatrix.CreateRotationZ(Rad(20)) * NumMatrix.CreateTranslation(0.5f, 0.2f, 1.1f);
        private static readonly NumMatrix RestChestRotatedSl = NumMatrix.CreateRotationX(Rad(-35)) * NumMatrix.CreateTranslation(-0.3f, 0.1f, 1.7f);
        private static readonly NumMatrix BindShapeSl = NumMatrix.CreateScale(1.1f, 0.9f, 1.0f) * NumMatrix.CreateRotationZ(Rad(10))
            * NumMatrix.CreateTranslation(0.05f, -0.02f, 0.3f);

        // A rig whose joints have no rotation comes back the same either way; one that has comes back the same
        // only if its matrices are kept as authored
        [TestCase(false, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        public void Export_ThenLoad_GivesBackAMeshThatPosesTheSame(bool rotatedRest, bool useAvatarJointAxes)
        {
            var restTorso = rotatedRest ? RestTorsoRotatedSl : RestTorsoSl;
            var restChest = rotatedRest ? RestChestRotatedSl : RestChestSl;
            var face = new Face
            {
                Vertices = new List<Vertex>(),
                Indices = new List<ushort>(TetraIndices),
                Weights = new List<VertexWeight>(),
                TextureFace = new Primitive.TextureEntryFace(null)
            };
            for (int i = 0; i < 4; i++)
            {
                face.Vertices.Add(new Vertex { Position = TetraPositions[i], Normal = TetraNormals[i], TexCoord = TetraUvs[i] });
                face.Weights.Add(Weight(TetraInfluences[i].Select(e => (e.Item1, e.Item2)).ToArray()));
            }
            var original = new FacetedMesh { Prim = new Primitive { LocalID = 1, Scale = new Vector3(5, 5, 5) } };
            original.Faces.Add(face);
            original.SkinData = new MeshSkinData
            {
                JointNames = new[] { "mTorso", "mChest" },
                BindShapeMatrix = Floats(BindShapeSl),
                InverseBindMatrices = Floats(Inverse(restTorso)).Concat(Floats(Inverse(restChest))).ToArray()
            };

            var exporter = new GltfExporter();
            exporter.Add(original);
            var path = Path.Combine(_tempDir, "export.glb");
            exporter.Save(path);

            var prims = new GltfLoader { UseAvatarJointAxes = useAvatarJointAxes }.Load(path, loadImages: false);
            Assert.That(prims, Has.Count.EqualTo(1));
            var reloaded = Decode(prims[0]);

            // The same pose on both: the chest turned and shifted, in world space, after its rest
            var delta = NumMatrix.CreateRotationY(Rad(25)) * NumMatrix.CreateTranslation(0.2f, -0.1f, 0.5f);
            var live = new Dictionary<string, NumMatrix> { ["mTorso"] = restTorso, ["mChest"] = restChest * delta };
            var (expectedPositions, expectedNormals) = Skinned(original, live);
            var (actualPositions, actualNormals) = Skinned(reloaded, live);

            Assert.That(actualPositions, Has.Count.EqualTo(expectedPositions.Count));
            for (int t = 0; t < expectedPositions.Count; t++)
            {
                AssertClose(actualPositions[t], expectedPositions[t], $"corner {t} position");
                AssertClose(actualNormals[t], expectedNormals[t], $"corner {t} normal", 5e-3f);
            }
        }

        #endregion Exporter round trip
    }
}
