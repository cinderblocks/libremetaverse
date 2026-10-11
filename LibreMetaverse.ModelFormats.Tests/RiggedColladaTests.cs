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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
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
    /// Rigged mesh import from COLLADA. The viewer is the reference: it takes the joint names, inverse bind matrices
    /// (transposed to row vectors), bind shape matrix and weights as the file has them, with no axis or unit change.
    /// The skinning is worked out in the tests from the file's own numbers with plain System.Numerics matrices, at a
    /// pose and not only at rest, and compared with what a viewer would compute from the mesh asset that came out.
    /// </summary>
    [TestFixture]
    public class RiggedColladaTests
    {
        private const float Tolerance = 6e-3f; // the decoder clamps a lone full weight to 0.999

        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-dae-rig-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        #region Helpers

        private static float Rad(float degrees) => degrees * (float)Math.PI / 180f;

        private static NumMatrix Inverse(NumMatrix m)
        {
            Assert.That(NumMatrix.Invert(m, out var inverse), Is.True);
            return inverse;
        }

        private static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A row vector matrix as COLLADA writes a column vector one: a row at a time, which is the row vector matrix a column at a time</summary>
        private static string ColladaMatrix(NumMatrix m) => string.Join(" ", new[]
        {
            m.M11, m.M21, m.M31, m.M41, m.M12, m.M22, m.M32, m.M42, m.M13, m.M23, m.M33, m.M43, m.M14, m.M24, m.M34, m.M44
        }.Select(Num));

        private static NumMatrix FromFloats(float[] f, int offset = 0) => new NumMatrix(
            f[offset], f[offset + 1], f[offset + 2], f[offset + 3], f[offset + 4], f[offset + 5], f[offset + 6], f[offset + 7],
            f[offset + 8], f[offset + 9], f[offset + 10], f[offset + 11], f[offset + 12], f[offset + 13], f[offset + 14], f[offset + 15]);

        private static NumVector3 V(Vector3 v) => new NumVector3(v.X, v.Y, v.Z);

        private static void AssertClose(NumVector3 actual, NumVector3 expected, string what, float tolerance = Tolerance)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), what + " X");
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), what + " Y");
                Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), what + " Z");
            });
        }

        private static Dictionary<string, NumMatrix> DefaultSkeleton() =>
            AvatarBoneMath.BuildBoneWorldMatrices(LindenSkeleton.Load(), new Dictionary<string, BoneTransform>());

        private static FacetedMesh Decode(ModelPrim prim)
        {
            var primitive = new Primitive { LocalID = 1, Scale = prim.Scale };
            Assert.That(FacetedMesh.TryDecodeFromAsset(primitive, new AssetMesh(UUID.Random(), prim.Asset),
                DetailLevel.Highest, out var mesh), Is.True);
            return mesh!;
        }

        /// <summary>What a viewer draws from a decoded rigged mesh, worked from first principles with row vectors</summary>
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
                    sum += (FromFloats(skin.InverseBindMatrices, joint * 16) * live[skin.JointNames[joint]]) * weight;
                }
                positions.Add(NumVector3.Transform(NumVector3.Transform(V(vertex.Position), bindShape), sum));

                var normal = NumVector3.Normalize(NumVector3.TransformNormal(V(vertex.Normal), normalMatrix));
                normals.Add(NumVector3.Normalize(NumVector3.TransformNormal(normal, sum)));
            }
            return (positions, normals);
        }

        #endregion Helpers

        #region Fixture

        // The mesh as the file has it, Z-up: a tetrahedron stretched unevenly and moved off the origin
        private static readonly NumVector3 MeshScale = new NumVector3(2, 3, 4);
        private static readonly NumVector3 MeshOffset = new NumVector3(1, -2, 0.5f);
        private static readonly NumVector3[] Tetra =
        {
            new NumVector3(0, 0, 0), new NumVector3(1, 0, 0), new NumVector3(0, 1, 0), new NumVector3(0, 0, 1)
        };
        private static readonly int[] Corners = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };

        private static NumVector3 FilePosition(int position) => Tetra[position] * MeshScale + MeshOffset;

        private static NumVector3 FlatNormal(int triangle)
        {
            var a = FilePosition(Corners[triangle * 3]);
            var b = FilePosition(Corners[triangle * 3 + 1]);
            var c = FilePosition(Corners[triangle * 3 + 2]);
            return NumVector3.Normalize(NumVector3.Cross(b - a, c - a));
        }

        // Which joints pull on each position, and with which of the four weights below
        private static readonly float[] WeightValues = { 1f, 0.5f, 0.25f, 0.75f };
        private static readonly (int Joint, float Weight)[][] Influences =
        {
            new[] { (0, 1f) },
            new[] { (1, 1f) },
            new[] { (0, 0.5f), (1, 0.5f) },
            new[] { (0, 0.25f), (1, 0.75f) }
        };

        private static readonly NumMatrix BindShape = NumMatrix.CreateScale(1.1f, 0.9f, 1.0f) * NumMatrix.CreateRotationZ(Rad(10))
            * NumMatrix.CreateTranslation(0.05f, -0.02f, 0.3f);

        /// <summary>Where the avatar's joints are, which is what a rig made for Second Life is built on</summary>
        private static NumMatrix Avatar(string joint) => DefaultSkeleton()[joint];

        /// <summary>
        /// A skinned COLLADA file. The positions are indexed apart from the normals and texture coordinates
        /// (a different list of each, per corner), the way real exports are, and the weights belong to the positions.
        /// </summary>
        private static string Dae(string[]? jointNames = null, NumMatrix[]? inverseBind = null, NumMatrix? bindShape = null,
            string upAxis = "Z_UP", string unit = "<unit name=\"meter\" meter=\"1\"/>", bool idRefs = false)
        {
            jointNames ??= new[] { "mTorso", "mChest" };
            inverseBind ??= jointNames.Select(n => Inverse(Avatar(n))).ToArray();
            bindShape ??= BindShape;

            var positions = string.Join(" ", Enumerable.Range(0, 4).SelectMany(i =>
            {
                var p = FilePosition(i);
                return new[] { p.X, p.Y, p.Z };
            }).Select(Num));
            var normals = string.Join(" ", Enumerable.Range(0, 4).SelectMany(t =>
            {
                var n = FlatNormal(t);
                return Enumerable.Repeat(new[] { n.X, n.Y, n.Z }, 3).SelectMany(x => x);
            }).Select(Num));
            var uvs = string.Join(" ", Enumerable.Range(0, 12).SelectMany(c => new[] { (c % 3) * 0.5f, (c / 3) * 0.25f }).Select(Num));
            var p = string.Join(" ", Enumerable.Range(0, 12).SelectMany(c => new[] { Corners[c], c, c }));

            var vcount = string.Join(" ", Influences.Select(i => i.Length));
            var v = string.Join(" ", Influences.SelectMany(i => i.SelectMany(e => new[] { e.Joint, Array.IndexOf(WeightValues, e.Weight) })));

            string jointSource = idRefs
                ? $"<IDREF_array id=\"Skin-joints-array\" count=\"{jointNames.Length}\">{string.Join(" ", jointNames)}</IDREF_array>"
                : $"<Name_array id=\"Skin-joints-array\" count=\"{jointNames.Length}\">{string.Join(" ", jointNames)}</Name_array>";

            var joints = new StringBuilder();
            foreach (var name in jointNames)
            {
                var at = DefaultSkeleton().TryGetValue(name, out var world) ? world : NumMatrix.Identity;
                joints.Append($"<node id=\"{name}\" name=\"{name}\" sid=\"{name}\" type=\"JOINT\"><matrix sid=\"transform\">{ColladaMatrix(at)}</matrix></node>");
            }

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<COLLADA xmlns=""http://www.collada.org/2005/11/COLLADASchema"" version=""1.4.1"">
  <asset><created>2026-01-01T00:00:00</created><modified>2026-01-01T00:00:00</modified>{unit}<up_axis>{upAxis}</up_axis></asset>
  <library_effects><effect id=""Effect0""><profile_COMMON><technique sid=""common""><lambert><diffuse><color>1 1 1 1</color></diffuse></lambert></technique></profile_COMMON></effect></library_effects>
  <library_materials><material id=""Mat0""><instance_effect url=""#Effect0"" /></material></library_materials>
  <library_geometries>
    <geometry id=""Mesh0"">
      <mesh>
        <source id=""Mesh0-positions""><float_array id=""Mesh0-positions-array"" count=""12"">{positions}</float_array></source>
        <source id=""Mesh0-normals""><float_array id=""Mesh0-normals-array"" count=""36"">{normals}</float_array></source>
        <source id=""Mesh0-uv""><float_array id=""Mesh0-uv-array"" count=""24"">{uvs}</float_array></source>
        <vertices id=""Mesh0-vertices""><input semantic=""POSITION"" source=""#Mesh0-positions"" /></vertices>
        <triangles count=""4"" material=""Mat0Symbol"">
          <input semantic=""VERTEX"" offset=""0"" source=""#Mesh0-vertices"" />
          <input semantic=""NORMAL"" offset=""1"" source=""#Mesh0-normals"" />
          <input semantic=""TEXCOORD"" offset=""2"" source=""#Mesh0-uv"" />
          <p>{p}</p>
        </triangles>
      </mesh>
    </geometry>
  </library_geometries>
  <library_controllers>
    <controller id=""Skin"">
      <skin source=""#Mesh0"">
        <bind_shape_matrix>{ColladaMatrix(bindShape.Value)}</bind_shape_matrix>
        <source id=""Skin-joints"">{jointSource}</source>
        <source id=""Skin-poses""><float_array id=""Skin-poses-array"" count=""{inverseBind.Length * 16}"">{string.Join(" ", inverseBind.Select(ColladaMatrix))}</float_array></source>
        <source id=""Skin-weights""><float_array id=""Skin-weights-array"" count=""4"">{string.Join(" ", WeightValues.Select(Num))}</float_array></source>
        <joints>
          <input semantic=""JOINT"" source=""#Skin-joints"" />
          <input semantic=""INV_BIND_MATRIX"" source=""#Skin-poses"" />
        </joints>
        <vertex_weights count=""4"">
          <input semantic=""JOINT"" source=""#Skin-joints"" offset=""0"" />
          <input semantic=""WEIGHT"" source=""#Skin-weights"" offset=""1"" />
          <vcount>{vcount}</vcount>
          <v>{v}</v>
        </vertex_weights>
      </skin>
    </controller>
  </library_controllers>
  <library_visual_scenes>
    <visual_scene id=""Scene"" name=""Scene"">
      <node id=""Armature"" name=""Armature"" type=""NODE"">{joints}</node>
      <node id=""Body"" name=""Body"" type=""NODE"">
        <matrix>1 0 0 7 0 1 0 8 0 0 1 9 0 0 0 1</matrix>
        <instance_controller url=""#Skin"">
          <skeleton>#{jointNames[0]}</skeleton>
          <bind_material><technique_common><instance_material symbol=""Mat0Symbol"" target=""#Mat0"" /></technique_common></bind_material>
        </instance_controller>
      </node>
    </visual_scene>
  </library_visual_scenes>
</COLLADA>";
        }

        private string Write(string xml, string name = "rig.dae")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, xml, new UTF8Encoding(false));
            return path;
        }

        private ModelPrim LoadOne(string xml, ColladaLoader? loader = null)
        {
            var prims = (loader ?? new ColladaLoader()).Load(Write(xml), false);
            Assert.That(prims, Has.Count.EqualTo(1));
            return prims[0];
        }

        private static string Tweak(string xml, string from, string to)
        {
            Assert.That(xml, Does.Contain(from), "the fixture has what the test changes");
            return xml.Replace(from, to);
        }

        private static Dictionary<string, NumMatrix> Posed(params string[] joints)
        {
            // Each joint turned about itself by a different angle, in the avatar's own axes
            var live = new Dictionary<string, NumMatrix>();
            for (int i = 0; i < joints.Length; i++)
                live[joints[i]] = NumMatrix.CreateRotationZ(Rad(i == 0 ? 25 : -40)) * Avatar(joints[i]);
            return live;
        }

        /// <summary>The file's own skinning: vertex * bind shape * sum(weight * inverse bind * joint) with the file's numbers</summary>
        private static List<NumVector3> FileSkinned(NumMatrix[] inverseBind, Dictionary<string, NumMatrix> live, string[] jointNames)
        {
            var result = new List<NumVector3>();
            foreach (var position in Corners)
            {
                var sum = new NumMatrix();
                foreach (var (joint, weight) in Influences[position])
                    sum += (inverseBind[joint] * live[jointNames[joint]]) * weight;
                result.Add(NumVector3.Transform(NumVector3.Transform(FilePosition(position), BindShape), sum));
            }
            return result;
        }

        #endregion Fixture

        #region The viewer's reading

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Load_RigMadeFromTheAvatarsJoints_SkinsLikeTheFileSays(bool posed, bool useAvatarJointAxes)
        {
            // The same either way: a rig on the avatar's own axes has nothing for the avatar's axes to change
            var names = new[] { "mTorso", "mChest" };
            var inverseBind = names.Select(n => Inverse(Avatar(n))).ToArray();
            var prim = LoadOne(Dae(), new ColladaLoader { UseAvatarJointAxes = useAvatarJointAxes });

            var live = posed ? Posed(names) : names.ToDictionary(n => n, Avatar);
            var (positions, _) = Skinned(Decode(prim), live);
            var expected = FileSkinned(inverseBind, live, names);

            Assert.That(positions, Has.Count.EqualTo(expected.Count));
            for (int t = 0; t < expected.Count; t++) AssertClose(positions[t], expected[t], $"corner {t}");
        }

        [Test]
        public void Load_Rig_KeepsTheFilesNamesMatricesAndBindShapeAsTheViewerWould()
        {
            var names = new[] { "mTorso", "mChest" };
            var prim = LoadOne(Dae(), new ColladaLoader { UseAvatarJointAxes = false });

            Assert.That(prim.Skin, Is.Not.Null);
            Assert.That(prim.Skin!.JointNames, Is.EqualTo(names));

            // The file's matrices, read a column at a time to make them row vector matrices
            for (int i = 0; i < names.Length; i++)
            {
                var actual = FromFloats(prim.Skin.InverseBindMatrices, i * 16);
                var expected = Inverse(Avatar(names[i]));
                Assert.That(Flatten(actual), Is.EqualTo(Flatten(expected)).Within(1e-4f), names[i]);
            }

            // The mesh is fitted into the unit cube first, and then the file's bind shape applies
            var fit = NumMatrix.CreateScale(prim.Scale.X, prim.Scale.Y, prim.Scale.Z) *
                      NumMatrix.CreateTranslation(prim.Position.X, prim.Position.Y, prim.Position.Z);
            Assert.That(Flatten(FromFloats(prim.Skin.BindShapeMatrix)), Is.EqualTo(Flatten(fit * BindShape)).Within(1e-4f));
        }

        private static float[] Flatten(NumMatrix m) => new[]
        {
            m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44
        };

        [Test]
        public void Load_Rig_IsSizedLikeAnyOtherMeshAndIgnoresItsNodesTransform()
        {
            // The body node is moved by (7, 8, 9); a rigged mesh is placed by its joints
            var prim = LoadOne(Dae());

            Assert.Multiple(() =>
            {
                Assert.That(prim.Scale.X, Is.EqualTo(MeshScale.X).Within(1e-4f));
                Assert.That(prim.Scale.Y, Is.EqualTo(MeshScale.Y).Within(1e-4f));
                Assert.That(prim.Scale.Z, Is.EqualTo(MeshScale.Z).Within(1e-4f));
                Assert.That(prim.Position.X, Is.EqualTo(MeshOffset.X + MeshScale.X / 2).Within(1e-4f));
                Assert.That(prim.Position.Y, Is.EqualTo(MeshOffset.Y + MeshScale.Y / 2).Within(1e-4f));
                Assert.That(prim.Position.Z, Is.EqualTo(MeshOffset.Z + MeshScale.Z / 2).Within(1e-4f));
            });
        }

        [Test]
        public void Load_Rig_WeightsBelongToThePositionsNotToTheCorners()
        {
            // Corners 0 and 3 share position 0 and have different normals: two vertices, one weight
            var prim = LoadOne(Dae());
            var face = Decode(prim).Faces[0];

            Assert.That(face.Vertices.Count, Is.GreaterThan(4), "flat normals split the shared positions");
            var distinctWeights = face.Weights!.Select(w => (w.Joint0, w.Weight0, w.Joint1, w.Weight1)).Distinct().Count();
            Assert.That(distinctWeights, Is.EqualTo(4), "and there are four weights between them");
        }

        [Test]
        public void Load_Rig_NormalsAreSquareToTheSurfaceAfterAnUnevenBindShape()
        {
            // At rest the joints' matrices cancel, leaving the bind shape. A normal turns with its inverse transpose
            // (in the file's own numbers, not the fitted ones) and has to stay square to the surface.
            var names = new[] { "mTorso", "mChest" };
            var stretched = NumMatrix.CreateScale(3f, 1f, 0.5f) * NumMatrix.CreateRotationZ(Rad(30));
            var prim = LoadOne(Dae(bindShape: stretched));

            var (positions, normals) = Skinned(Decode(prim), names.ToDictionary(n => n, Avatar));

            for (int t = 0; t < Corners.Length; t += 3)
            {
                var a = positions[t]; var b = positions[t + 1]; var c = positions[t + 2];
                var geometric = NumVector3.Normalize(NumVector3.Cross(b - a, c - a));
                for (int k = 0; k < 3; k++)
                    Assert.That(Math.Abs(NumVector3.Dot(geometric, normals[t + k])), Is.EqualTo(1f).Within(1e-2f), $"triangle {t / 3} corner {k}");
            }
        }

        #endregion The viewer's reading

        #region Avatar joint axes

        [TestCase(false)]
        [TestCase(true)]
        public void Load_RigWithItsOwnBoneAxes_SitsOnTheAvatarsJointsLikeTheFileSaysItShould(bool posed)
        {
            // A rig from a modelling program: bones at the avatar's positions but turned. Its inverse bind matrices
            // are the inverse of those turned joints, with the mesh bound to them at rest.
            var names = new[] { "mTorso", "mChest" };
            var turned = new[]
            {
                NumMatrix.CreateRotationZ(Rad(20)) * Avatar("mTorso"),
                NumMatrix.CreateRotationX(Rad(-35)) * Avatar("mChest")
            };
            var xml = Dae(inverseBind: turned.Select(Inverse).ToArray());

            var prim = LoadOne(xml);

            // On the avatar the joints have its own axes: the mesh rests where the file put it, and a joint
            // turned about itself bends the mesh about that joint, as it would for any avatar
            var live = posed ? Posed(names) : names.ToDictionary(n => n, Avatar);
            var (positions, _) = Skinned(Decode(prim), live);
            for (int t = 0; t < Corners.Length; t++)
            {
                int position = Corners[t];
                var sum = new NumMatrix();
                foreach (var (joint, weight) in Influences[position])
                    sum += (Inverse(Avatar(names[joint])) * live[names[joint]]) * weight;
                var expected = NumVector3.Transform(NumVector3.Transform(FilePosition(position), BindShape), sum);
                AssertClose(positions[t], expected, $"corner {t}");
            }
        }

        [Test]
        public void Load_RigWithItsOwnBoneAxes_AsAuthored_TurnsTheMeshOnTheAvatar()
        {
            // The contrast: the viewer's reading carries the turned axes over, and at rest the mesh comes out bent
            var names = new[] { "mTorso", "mChest" };
            var turned = new[]
            {
                NumMatrix.CreateRotationZ(Rad(20)) * Avatar("mTorso"),
                NumMatrix.CreateRotationX(Rad(-35)) * Avatar("mChest")
            };
            var prim = LoadOne(Dae(inverseBind: turned.Select(Inverse).ToArray()), new ColladaLoader { UseAvatarJointAxes = false });

            var (positions, _) = Skinned(Decode(prim), names.ToDictionary(n => n, Avatar));

            var rest = FilePosition(Corners[0]);
            var atRest = NumVector3.Transform(rest, BindShape);
            Assert.That((positions[0] - atRest).Length(), Is.GreaterThan(0.05f), "turned about the joint");
        }

        [Test]
        public void Load_RigWeightedToACollisionVolume_KeepsTheAvatarsAxesAndSizeThere()
        {
            // Fitted mesh is weighted to collision volumes, which unlike bones have a rotation and scale at rest. A
            // rig built on them is the avatar's own, and comes out the same in either reading.
            var avatar = DefaultSkeleton();
            var skeleton = LindenSkeleton.Load();
            var bones = skeleton.GetAllJoints().Select(j => j.name).ToHashSet();
            var volume = avatar.Where(pair => !bones.Contains(pair.Key) &&
                    !skeleton.GetAllJoints().Any(j => j.GetAliasesList().Contains(pair.Key)) &&
                    NumMatrix.Decompose(pair.Value, out var sc, out var ro, out _) &&
                    (Math.Abs(Math.Abs(ro.W) - 1f) > 1e-3f || Math.Abs(sc.X - 1f) > 1e-3f))
                .Select(pair => pair.Key).First();

            var names = new[] { "mTorso", volume };
            var xml = Dae(jointNames: names, inverseBind: names.Select(n => Inverse(avatar[n])).ToArray());

            var aligned = LoadOne(xml);
            var authored = LoadOne(xml, new ColladaLoader { UseAvatarJointAxes = false });

            Assert.That(aligned.Skin!.InverseBindMatrices, Is.EqualTo(authored.Skin!.InverseBindMatrices).Within(1e-3f));
            var live = Posed(names);
            var (positions, _) = Skinned(Decode(aligned), live);
            var expected = FileSkinned(names.Select(n => Inverse(avatar[n])).ToArray(), live, names);
            for (int t = 0; t < expected.Count; t++) AssertClose(positions[t], expected[t], $"corner {t}");
        }

        [Test]
        public void Load_RigWeightedToACollisionVolumeBoundAtAnotherShape_KeepsThatShape()
        {
            // A collision volume's scale follows the wearer's shape sliders, so a rig bound at another shape carries
            // another scale in its matrices. The viewer keeps it, and so must the avatar-axes reading: only the rotation
            // is the avatar's.
            var avatar = DefaultSkeleton();
            var skeleton = LindenSkeleton.Load();
            var bones = skeleton.GetAllJoints().Select(j => j.name).ToHashSet();
            var volume = avatar.Where(pair => !bones.Contains(pair.Key) &&
                    !skeleton.GetAllJoints().Any(j => j.GetAliasesList().Contains(pair.Key)) &&
                    NumMatrix.Decompose(pair.Value, out var sc, out var ro, out _) &&
                    (Math.Abs(Math.Abs(ro.W) - 1f) > 1e-3f || Math.Abs(sc.X - 1f) > 1e-3f))
                .Select(pair => pair.Key).First();

            var names = new[] { "mTorso", volume };
            var bound = new Dictionary<string, NumMatrix>
            {
                ["mTorso"] = avatar["mTorso"],
                [volume] = NumMatrix.CreateScale(1.25f, 0.8f, 1.1f) * avatar[volume]
            };
            var inverseBind = names.Select(n => Inverse(bound[n])).ToArray();
            var xml = Dae(jointNames: names, inverseBind: inverseBind);

            // Posed about the joints as they are bound
            var live = new Dictionary<string, NumMatrix>
            {
                ["mTorso"] = NumMatrix.CreateRotationZ(Rad(25)) * bound["mTorso"],
                [volume] = NumMatrix.CreateRotationZ(Rad(-40)) * bound[volume]
            };
            var expected = FileSkinned(inverseBind, live, names);

            foreach (var useAvatarJointAxes in new[] { false, true })
            {
                var prim = LoadOne(xml, new ColladaLoader { UseAvatarJointAxes = useAvatarJointAxes });
                var (positions, _) = Skinned(Decode(prim), live);
                for (int t = 0; t < expected.Count; t++)
                    AssertClose(positions[t], expected[t], $"corner {t} with avatar joint axes {useAvatarJointAxes}");
            }
        }

        #endregion Avatar joint axes

        #region Joints and influences

        [Test]
        public void Load_JointNamesGivenAsIdRefs_AreRead()
        {
            var prim = LoadOne(Dae(idRefs: true));

            Assert.That(prim.Skin!.JointNames, Is.EqualTo(new[] { "mTorso", "mChest" }));
        }

        [Test]
        public void Load_ArraysWithWhiteSpaceAroundThem_AreRead()
        {
            // Pretty-printed arrays have a line break before the first value and after the last
            var xml = Dae();
            xml = Tweak(xml, "<Name_array id=\"Skin-joints-array\" count=\"2\">mTorso mChest</Name_array>",
                "<Name_array id=\"Skin-joints-array\" count=\"2\">\n  mTorso\n  mChest\n</Name_array>");
            xml = Tweak(xml, "<vcount>1 1 2 2</vcount>", "<vcount>\n 1 1 2 2\n</vcount>");

            var prim = LoadOne(xml);

            Assert.That(prim.Skin!.JointNames, Is.EqualTo(new[] { "mTorso", "mChest" }));
            var (positions, _) = Skinned(Decode(prim), new[] { "mTorso", "mChest" }.ToDictionary(n => n, Avatar));
            AssertClose(positions[0], NumVector3.Transform(FilePosition(Corners[0]), BindShape), "corner 0");
        }

        [Test]
        public void Load_JointsTheAvatarDoesNotHave_AreDroppedAndTheRestRenormalized()
        {
            var xml = Dae(jointNames: new[] { "mTorso", "NotAJoint" }, inverseBind: new[] { Inverse(Avatar("mTorso")), NumMatrix.Identity });

            var prim = LoadOne(xml);

            Assert.That(prim.Skin!.JointNames, Is.EqualTo(new[] { "mTorso" }));
            foreach (var w in Decode(prim).Faces[0].Weights!)
            {
                Assert.That(w.Joint0, Is.EqualTo(0));
                Assert.That(w.Weight0, Is.EqualTo(1f).Within(2e-3f));
            }
        }

        [Test]
        public void Load_SkinWithNoJointOfTheAvatar_LoadsAsAnOrdinaryMeshWhereItsNodeSaysSo()
        {
            var xml = Dae(jointNames: new[] { "Foo", "Bar" }, inverseBind: new[] { NumMatrix.Identity, NumMatrix.Identity });

            var prim = LoadOne(xml);

            Assert.That(prim.Skin, Is.Null);
            Assert.That(prim.Position.X, Is.EqualTo(MeshOffset.X + MeshScale.X / 2 + 7).Within(1e-3f), "moved by the node, as any mesh is");
        }

        [Test]
        public void Load_TwoJointsWithTheSameAvatarName_ShareOne()
        {
            var xml = Dae(jointNames: new[] { "mTorso", "mTorso" }, inverseBind: new[] { Inverse(Avatar("mTorso")), NumMatrix.Identity });

            var prim = LoadOne(xml);

            Assert.That(prim.Skin!.JointNames, Is.EqualTo(new[] { "mTorso" }));
            Assert.That(Decode(prim).Faces[0].Weights!.Select(w => w.Joint0), Is.All.EqualTo(0));
        }

        [Test]
        public void Load_VertexWithMoreThanFourInfluences_KeepsTheStrongestFourAndAddsThemUp()
        {
            var names = new[] { "mTorso", "mChest", "mNeck", "mHead", "mPelvis" };
            // Position 0 gets five: weights 0.4, 0.3, 0.15, 0.1 and 0.05 (the last is dropped)
            var xml = Dae(jointNames: names);
            xml = Tweak(xml, "<float_array id=\"Skin-weights-array\" count=\"4\">1 0.5 0.25 0.75</float_array>",
                "<float_array id=\"Skin-weights-array\" count=\"9\">1 0.5 0.25 0.75 0.4 0.3 0.15 0.1 0.05</float_array>");
            xml = Tweak(xml, "<vcount>1 1 2 2</vcount>", "<vcount>5 1 2 2</vcount>");
            xml = Tweak(xml, "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>4 8 3 7 2 6 1 5 0 4  1 0 0 1 1 1 0 2 1 3</v>");

            var prim = LoadOne(xml);

            // Straight from the loader: the decoder renormalizes weights that do not add up, which would hide a mistake
            var w = prim.Faces[0].Weights![0];
            var kept = new[] { (w.Joint0, w.Weight0), (w.Joint1, w.Weight1), (w.Joint2, w.Weight2), (w.Joint3, w.Weight3) };
            Assert.Multiple(() =>
            {
                Assert.That(kept.Select(k => k.Item1), Is.EqualTo(new[] { 0, 1, 2, 3 }), "pelvis, the weakest, is out");
                Assert.That(kept.Sum(k => k.Item2), Is.EqualTo(1f).Within(1e-5f));
                Assert.That(kept[0].Item2, Is.EqualTo(0.4f / 0.95f).Within(1e-5f));
            });
        }

        [Test]
        public void Load_WeightToJointMinusOne_IsTheBindShapeAndDoesNotCount()
        {
            var xml = Dae();
            xml = Tweak(xml, "<vcount>1 1 2 2</vcount>", "<vcount>2 1 2 2</vcount>");
            xml = Tweak(xml, "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>-1 3 0 0  1 0 0 1 1 1 0 2 1 3</v>");

            var prim = LoadOne(xml);

            var w = Decode(prim).Faces[0].Weights![0];
            Assert.That(w.Joint0, Is.EqualTo(0));
            Assert.That(w.Weight0, Is.EqualTo(1f).Within(2e-3f));
        }

        [Test]
        public void Load_MoreJointsThanTheLimit_IsRefused()
        {
            var xml = Dae();

            Assert.That(new ColladaLoader { MaxJoints = 2 }.Load(Write(xml), false), Has.Count.EqualTo(1), "control");
            Assert.That(new ColladaLoader { MaxJoints = 1 }.Load(Write(xml), false), Is.Empty);
        }

        #endregion Joints and influences

        #region Axes and units

        [Test]
        public void Load_RigInAYUpFileInInches_IsTakenExactlyAsItIs()
        {
            // The viewer does not turn or scale the skin data of a Y-up file: the numbers go to the grid as they are.
            // A rig that is not Z-up in meters only fits the avatar if it was made that way, and that is the file's
            // business. (A warning says so.)
            var xml = Dae(upAxis: "Y_UP", unit: "<unit name=\"inch\" meter=\"0.0254\"/>");
            var prim = LoadOne(xml, new ColladaLoader { UseAvatarJointAxes = false });

            var names = new[] { "mTorso", "mChest" };
            var live = Posed(names);
            var (positions, _) = Skinned(Decode(prim), live);
            var expected = FileSkinned(names.Select(n => Inverse(Avatar(n))).ToArray(), live, names);
            for (int t = 0; t < expected.Count; t++) AssertClose(positions[t], expected[t], $"corner {t}");
            Assert.That(prim.Scale.X, Is.EqualTo(MeshScale.X).Within(1e-4f), "not scaled by the unit");
        }

        #endregion Axes and units

        #region Static meshes

        [Test]
        public void Load_AGeometryNoControllerUses_IsStillAnOrdinaryMesh()
        {
            // The same file with the body node instancing the geometry directly
            var xml = Dae();
            xml = Tweak(xml, "<instance_controller url=\"#Skin\">", "<instance_geometry url=\"#Mesh0\">");
            xml = Tweak(xml, "<skeleton>#mTorso</skeleton>", "");
            xml = Tweak(xml, "</instance_controller>", "</instance_geometry>");

            var prim = LoadOne(xml);

            Assert.That(prim.Skin, Is.Null);
        }

        [Test]
        public void Load_TwoNodesOfOneController_AreTwoRiggedPrimsWithTheSameSkin()
        {
            var xml = Dae();
            xml = Tweak(xml, "</visual_scene>",
                "<node id=\"Body2\" name=\"Body2\" type=\"NODE\"><matrix>1 0 0 1 0 1 0 0 0 0 1 0 0 0 0 1</matrix>" +
                "<instance_controller url=\"#Skin\"><skeleton>#mTorso</skeleton><bind_material><technique_common>" +
                "<instance_material symbol=\"Mat0Symbol\" target=\"#Mat0\" /></technique_common></bind_material></instance_controller></node></visual_scene>");

            var prims = new ColladaLoader().Load(Write(xml), false);

            Assert.That(prims, Has.Count.EqualTo(2));
            Assert.That(prims.Select(p => p.ID), Is.EquivalentTo(new[] { "Body", "Body2" }));
            Assert.That(prims.All(p => p.Skin != null && p.Skin.JointNames.Count == 2), Is.True);
            Assert.That(prims[0].Asset, Is.EqualTo(prims[1].Asset));
        }

        [Test]
        public void Load_OneGeometryUsedByASkinAndByAPlainNode_GivesARiggedPrimAndAStaticOne()
        {
            var xml = Dae();
            xml = Tweak(xml, "</visual_scene>",
                "<node id=\"Plain\" name=\"Plain\" type=\"NODE\"><matrix>1 0 0 50 0 1 0 0 0 0 1 0 0 0 0 1</matrix>" +
                "<instance_geometry url=\"#Mesh0\"><bind_material><technique_common>" +
                "<instance_material symbol=\"Mat0Symbol\" target=\"#Mat0\" /></technique_common></bind_material></instance_geometry></node></visual_scene>");

            var prims = new ColladaLoader().Load(Write(xml), false);

            Assert.That(prims, Has.Count.EqualTo(2));
            var rigged = prims.Single(p => p.ID == "Body");
            var plain = prims.Single(p => p.ID == "Plain");
            Assert.Multiple(() =>
            {
                Assert.That(rigged.Skin, Is.Not.Null);
                Assert.That(plain.Skin, Is.Null);
                Assert.That(plain.Position.X, Is.EqualTo(50f + MeshOffset.X + MeshScale.X / 2).Within(1e-3f), "placed by its node");
                Assert.That(ReferenceEquals(rigged.Faces, plain.Faces), Is.False, "no face list in common");
                Assert.That(rigged.Faces[0].Weights, Is.Not.Null);
                Assert.That(plain.Faces[0].Weights, Is.Null);
            });
        }

        #endregion Static meshes

        #region Hostile input

        private void AssertRefused(Func<string, string> hostile, ColladaLoader? loader = null)
        {
            // Each case comes with a control that loads, so a refusal is the change and not the fixture
            var control = Write(Dae(), "control.dae");
            Assert.That((loader ?? new ColladaLoader()).Load(control, false), Has.Count.EqualTo(1), "control");

            var bad = Write(hostile(Dae()), "hostile.dae");
            Assert.That((loader ?? new ColladaLoader()).Load(bad, false), Is.Empty);
        }

        [Test]
        public void Load_JointIndexBeyondTheSkin_IsRefused() =>
            AssertRefused(x => Tweak(x, "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>9 0 1 0 0 1 1 1 0 2 1 3</v>"));

        [Test]
        public void Load_WeightIndexBeyondTheWeights_IsRefused() =>
            AssertRefused(x => Tweak(x, "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>0 9 1 0 0 1 1 1 0 2 1 3</v>"));

        [Test]
        public void Load_NegativeJointIndexOtherThanMinusOne_IsRefused() =>
            AssertRefused(x => Tweak(x, "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>-2 0 1 0 0 1 1 1 0 2 1 3</v>"));

        [Test]
        public void Load_NegativeWeight_IsRefused() =>
            AssertRefused(x => Tweak(x, "count=\"4\">1 0.5 0.25 0.75</float_array>", "count=\"4\">-1 0.5 0.25 0.75</float_array>"));

        [Test]
        public void Load_NaNWeight_IsRefused() =>
            AssertRefused(x => Tweak(x, "count=\"4\">1 0.5 0.25 0.75</float_array>", "count=\"4\">NaN 0.5 0.25 0.75</float_array>"));

        [Test]
        public void Load_InfiniteWeight_IsRefused() =>
            AssertRefused(x => Tweak(x, "count=\"4\">1 0.5 0.25 0.75</float_array>", "count=\"4\">Infinity 0.5 0.25 0.75</float_array>"));

        [Test]
        public void Load_WeightsForFewerVerticesThanTheMesh_AreRefused() =>
            AssertRefused(x => Tweak(Tweak(x, "<vcount>1 1 2 2</vcount>", "<vcount>1 1 2</vcount>"),
                "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>0 0 1 0 0 1 1 1 0 2</v>"));

        [Test]
        public void Load_WeightPairsThatDoNotAddUpToTheCounts_AreRefused() =>
            AssertRefused(x => Tweak(x, "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>0 0 1 0 0 1 1 1 0 2 1</v>"));

        [Test]
        public void Load_AWeightListThatIsNotNumbers_IsRefused() =>
            AssertRefused(x => Tweak(x, "<v>0 0 1 0 0 1 1 1 0 2 1 3</v>", "<v>0 0 1 0 0 1 1 1 0 2 x 3</v>"));

        [Test]
        public void Load_NegativeVertexWeightCount_IsRefused() =>
            AssertRefused(x => Tweak(x, "<vcount>1 1 2 2</vcount>", "<vcount>-1 3 2 2</vcount>"));

        [Test]
        public void Load_FewerInverseBindMatricesThanJoints_IsRefused() =>
            AssertRefused(x => Tweak(x, "<float_array id=\"Skin-poses-array\" count=\"32\">", "<float_array id=\"Skin-poses-array\" count=\"32\">"
                ).Replace(DaeLast16(x), ""));

        // The text of the second matrix, so a test can cut it out
        private static string DaeLast16(string xml)
        {
            var start = xml.IndexOf("<float_array id=\"Skin-poses-array\"", StringComparison.Ordinal);
            var open = xml.IndexOf('>', start) + 1;
            var close = xml.IndexOf("</float_array>", open, StringComparison.Ordinal);
            var values = xml.Substring(open, close - open).Split(' ');
            return " " + string.Join(" ", values.Skip(16));
        }

        [Test]
        public void Load_InverseBindMatrixThatIsNotANumber_IsRefused() =>
            AssertRefused(x =>
            {
                var start = x.IndexOf("<float_array id=\"Skin-poses-array\"", StringComparison.Ordinal);
                var open = x.IndexOf('>', start) + 1;
                return x.Substring(0, open) + "NaN" + x.Substring(open + x.Substring(open).IndexOf(' '));
            });

        [Test]
        public void Load_BindShapeMatrixWithTheWrongNumberOfValues_IsRefused() =>
            AssertRefused(x =>
            {
                var start = x.IndexOf("<bind_shape_matrix>", StringComparison.Ordinal) + "<bind_shape_matrix>".Length;
                return x.Substring(0, start) + "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0" + x.Substring(x.IndexOf("</bind_shape_matrix>", StringComparison.Ordinal));
            });

        [Test]
        public void Load_BindShapeMatrixThatIsNotNumbers_IsRefused() =>
            AssertRefused(x => Tweak(x, "<bind_shape_matrix>", "<bind_shape_matrix>oops "));

        [Test]
        public void Load_PositionThatIsNotANumber_IsRefused() =>
            AssertRefused(x =>
            {
                var start = x.IndexOf("<float_array id=\"Mesh0-positions-array\"", StringComparison.Ordinal);
                var open = x.IndexOf('>', start) + 1;
                return x.Substring(0, open) + "NaN " + x.Substring(open);
            });

        [Test]
        public void Load_SkinWithoutWeightsOrJoints_IsRefused()
        {
            AssertRefused(x => Tweak(x, "<joints>", "<joints_>").Replace("</joints>", "</joints_>"));
        }

        #endregion Hostile input

        #region Diagnostics

        private ModelLoadResult Diagnose(string xml, ColladaLoader? loader = null) =>
            (loader ?? new ColladaLoader()).LoadWithDiagnostics(Write(xml), false);

        [Test]
        public void LoadWithDiagnostics_RigMadeForTheAvatar_SaysNothing()
        {
            var result = Diagnose(Dae());

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void LoadWithDiagnostics_JointsTheAvatarDoesNotHave_AreListedWithTheirNames()
        {
            var xml = Dae(jointNames: new[] { "mTorso", "NotAJoint" }, inverseBind: new[] { Inverse(Avatar("mTorso")), NumMatrix.Identity });

            var result = Diagnose(xml);

            var ignored = result.Diagnostics.Single(d => d.Code == ModelDiagnosticCode.JointsIgnored);
            Assert.That(ignored.Subject, Is.EqualTo("NotAJoint"));
            Assert.That(ignored.Count, Is.EqualTo(1));
            // and the one vertex that only the missing joint pulled on is counted
            var unweighted = result.Diagnostics.Single(d => d.Code == ModelDiagnosticCode.UnweightedVertices);
            Assert.That(unweighted.Count, Is.EqualTo(1));
        }

        [Test]
        public void LoadWithDiagnostics_SkinWithNoJointOfTheAvatar_SaysTheRigWasDropped()
        {
            var xml = Dae(jointNames: new[] { "Foo", "Bar" }, inverseBind: new[] { NumMatrix.Identity, NumMatrix.Identity });

            var result = Diagnose(xml);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics.Select(d => d.Code), Is.EquivalentTo(new[]
                { ModelDiagnosticCode.JointsIgnored, ModelDiagnosticCode.RigDropped }));
            Assert.That(result.Diagnostics.Single(d => d.Code == ModelDiagnosticCode.RigDropped).Subject, Is.EqualTo("Skin"));
        }

        [Test]
        public void LoadWithDiagnostics_ARigThatIsNotZUpInMeters_SaysItWasTakenAsItIs()
        {
            var result = Diagnose(Dae(upAxis: "Y_UP"));

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics.Select(d => d.Code), Is.EqualTo(new[] { ModelDiagnosticCode.RigAxesNotApplied }));
        }

        [Test]
        public void LoadWithDiagnostics_AJointMatrixThatCannotBeInverted_SaysTheMatricesWereUsedAsAuthored()
        {
            var xml = Dae(inverseBind: new[] { new NumMatrix(), Inverse(Avatar("mChest")) });

            var result = Diagnose(xml);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            var approximated = result.Diagnostics.Single(d => d.Code == ModelDiagnosticCode.RigApproximated);
            Assert.That(approximated.Subject, Is.EqualTo("mTorso"));
            Assert.That(Diagnose(xml, new ColladaLoader { UseAvatarJointAxes = false }).Diagnostics, Is.Empty,
                "nothing to approximate when the matrices are used as they are");
        }

        [Test]
        public void LoadWithDiagnostics_WithoutAnAvatarSkeleton_SaysJointNamesWereNotChecked()
        {
            var original = AvatarSkeletonIndex.Source;
            try
            {
                AvatarSkeletonIndex.Source = () => null;

                var result = Diagnose(Dae(jointNames: new[] { "Foo", "Bar" }, inverseBind: new[] { NumMatrix.Identity, NumMatrix.Identity }));

                Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(ModelDiagnosticCode.SkeletonUnavailable));
            }
            finally
            {
                AvatarSkeletonIndex.Source = original;
            }
        }

        [Test]
        public void LoadWithDiagnostics_MoreJointsThanTheLimit_IsALimitError()
        {
            var result = Diagnose(Dae(), new ColladaLoader { MaxJoints = 1 });

            Assert.That(result.Prims, Is.Empty);
            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.LimitExceeded));
        }

        [Test]
        public void LoadWithDiagnostics_NodesOfAControllerTheFileDoesNotDefine_AreSkipped()
        {
            var xml = Tweak(Dae(), "<instance_controller url=\"#Skin\">", "<instance_controller url=\"#Nowhere\">");

            var result = Diagnose(xml);

            Assert.That(result.Prims, Is.Empty);
            var skipped = result.Diagnostics.Single();
            Assert.That(skipped.Code, Is.EqualTo(ModelDiagnosticCode.NodeSkipped));
            Assert.That(skipped.Subject, Is.EqualTo("Nowhere"));
        }

        [Test]
        public void LoadWithDiagnostics_AnIndexBeyondThePositions_IsALoaderGapNotADamagedFile()
        {
            // Nothing checks this one, so it surfaces as the unexpected exception it is
            var xml = Tweak(Dae(), "<p>0 0 0 ", "<p>99 0 0 ");

            var result = Diagnose(xml);

            Assert.That(result.Prims, Is.Empty);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.LoaderFailed));
        }

        #endregion Diagnostics

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

            var prims = new ColladaLoader().Load(Write(Dae()), false);
            Assert.That(prims, Has.Count.EqualTo(1));

            var result = await client.Inventory.UploadModelAsync(prims, "Rigged", "a rigged mesh", UUID.Random(), Permissions.NoPermissions);
            Assert.That(result.Success, Is.True);

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
    }
}
