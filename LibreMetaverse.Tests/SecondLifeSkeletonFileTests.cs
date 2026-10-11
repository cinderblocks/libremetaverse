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
using System.Text.RegularExpressions;
using LibreMetaverse.Assets;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using NUnit.Framework;
using NumMatrix = System.Numerics.Matrix4x4;
using NumVector3 = System.Numerics.Vector3;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Rigged mesh import from files a person made: the Second Life skeleton with an avatar's meshes weighted to it,
    /// exported from Blender as COLLADA and as glTF (see TestData/SecondLifeSkeleton/README.md). The two formats come
    /// from the same scene, so each has to agree with the other, and at rest the skinned mesh has to be where the
    /// file puts it.
    /// </summary>
    [TestFixture]
    public class SecondLifeSkeletonFileTests
    {
        private const float Tolerance = 2e-3f;

        private static readonly string[] Variants = { "default-blender-all-bones", "default-blender-deform-bones" };

        private static string FixturePath(string name) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "SecondLifeSkeleton", name);

        #region Helpers

        private static ModelLoadResult LoadDae(string variant) =>
            new ColladaLoader().LoadWithDiagnostics(FixturePath(variant + ".dae"), false);

        private static ModelLoadResult LoadGlb(string variant) =>
            new GltfLoader().LoadWithDiagnostics(FixturePath(variant + ".glb"), false);

        private static NumVector3 V(Vector3 v) => new NumVector3(v.X, v.Y, v.Z);

        private static NumMatrix FromFloats(float[] f, int offset = 0) => new NumMatrix(
            f[offset], f[offset + 1], f[offset + 2], f[offset + 3], f[offset + 4], f[offset + 5], f[offset + 6], f[offset + 7],
            f[offset + 8], f[offset + 9], f[offset + 10], f[offset + 11], f[offset + 12], f[offset + 13], f[offset + 14], f[offset + 15]);

        private static Dictionary<string, NumMatrix> DefaultSkeleton() =>
            AvatarBoneMath.BuildBoneWorldMatrices(LindenSkeleton.Load(), new Dictionary<string, BoneTransform>());

        private static FacetedMesh Decode(ModelPrim prim)
        {
            var primitive = new Primitive { LocalID = 1, Scale = prim.Scale };
            Assert.That(FacetedMesh.TryDecodeFromAsset(primitive, new AssetMesh(UUID.Random(), prim.Asset),
                DetailLevel.Highest, out var mesh), Is.True);
            return mesh!;
        }

        /// <summary>Where a viewer puts the vertices of a decoded rigged mesh with the joints at <paramref name="live"/>, worked out with row vectors</summary>
        private static List<NumVector3> Skinned(FacetedMesh mesh, Dictionary<string, NumMatrix> live)
        {
            var skin = mesh.SkinData!;
            var bindShape = FromFloats(skin.BindShapeMatrix);
            var result = new List<NumVector3>();
            foreach (var face in mesh.Faces)
            {
                foreach (var index in face.Indices)
                {
                    var w = face.Weights![index];
                    var sum = new NumMatrix();
                    foreach (var (joint, weight) in new[] { (w.Joint0, w.Weight0), (w.Joint1, w.Weight1), (w.Joint2, w.Weight2), (w.Joint3, w.Weight3) })
                    {
                        if (weight == 0f) continue;
                        sum += (FromFloats(skin.InverseBindMatrices, joint * 16) * live[skin.JointNames[joint]]) * weight;
                    }
                    result.Add(NumVector3.Transform(NumVector3.Transform(V(face.Vertices[index].Position), bindShape), sum));
                }
            }
            return result;
        }

        private static (NumVector3 Min, NumVector3 Max) Bounds(IEnumerable<NumVector3> points) =>
            (points.Aggregate(new NumVector3(float.MaxValue), NumVector3.Min), points.Aggregate(new NumVector3(float.MinValue), NumVector3.Max));

        private static void AssertClose(NumVector3 actual, NumVector3 expected, string what)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), what + " X");
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), what + " Y");
                Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Tolerance), what + " Z");
            });
        }

        /// <summary>A mesh named by what it is weighted to and how big it is, which is the same in both formats</summary>
        private static string Key(ModelPrim prim) =>
            $"{prim.Faces.Sum(f => f.Vertices.Count)}:{string.Join(",", prim.Skin!.JointNames.OrderBy(n => n, StringComparer.Ordinal))}";

        private static string BaseName(string name) => Regex.Replace(name, @"\.\d+$", "");

        /// <summary>The positions each mesh of a COLLADA file draws, by mesh name, read from the text</summary>
        private static Dictionary<string, List<NumVector3>> ReadVertices(string text)
        {
            var raw = new Dictionary<string, List<NumVector3>>();
            foreach (Match m in Regex.Matches(text, "<geometry id=\"[^\"]*\" name=\"([^\"]*)\">\\s*<mesh>\\s*<source id=\"[^\"]*\">\\s*<float_array id=\"[^\"]*\" count=\"(\\d+)\">([^<]*)<"))
            {
                var f = m.Groups[3].Value.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                Assert.That(f, Has.Length.EqualTo(int.Parse(m.Groups[2].Value)));
                raw[BaseName(m.Groups[1].Value)] = Enumerable.Range(0, f.Length / 3).Select(i => new NumVector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2])).ToList();
            }
            return raw;
        }

        /// <summary>
        /// Where each skinned joint sits according to the file: the inverse of its inverse bind matrix, whose last
        /// column is the origin. A COLLADA matrix is written a row at a time and takes column vectors.
        /// </summary>
        private static Dictionary<string, NumVector3> ReadJointOrigins(string text)
        {
            var origins = new Dictionary<string, NumVector3>();
            foreach (Match c in Regex.Matches(text, "<controller [^>]*>.*?</controller>", RegexOptions.Singleline))
            {
                var names = Regex.Match(c.Value, "<Name_array[^>]*>([^<]*)<").Groups[1].Value.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var poses = Regex.Match(c.Value, "<float_array id=\"[^\"]*poses-array\"[^>]*>([^<]*)<").Groups[1].Value
                    .Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                for (int j = 0; j < names.Length; j++)
                {
                    var columnVector = new NumMatrix(
                        poses[j * 16 + 0], poses[j * 16 + 4], poses[j * 16 + 8], poses[j * 16 + 12],
                        poses[j * 16 + 1], poses[j * 16 + 5], poses[j * 16 + 9], poses[j * 16 + 13],
                        poses[j * 16 + 2], poses[j * 16 + 6], poses[j * 16 + 10], poses[j * 16 + 14],
                        poses[j * 16 + 3], poses[j * 16 + 7], poses[j * 16 + 11], poses[j * 16 + 15]);
                    Assert.That(NumMatrix.Invert(columnVector, out var world), Is.True);
                    origins[names[j]] = new NumVector3(world.M41, world.M42, world.M43);
                }
            }
            return origins;
        }

        #endregion Helpers

        [TestCaseSource(nameof(Variants))]
        public void Collada_loads_every_mesh_rigged(string variant)
        {
            var result = LoadDae(variant);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Prims, Has.Count.EqualTo(8));
            Assert.That(result.Prims.Select(p => p.Skin), Has.All.Not.Null);
        }

        [TestCaseSource(nameof(Variants))]
        public void Gltf_loads_every_mesh_rigged_although_the_skin_lists_the_whole_armature(string variant)
        {
            // The one glTF skin has 133 or more joints, over the 110 a mesh may have. No mesh is weighted to
            // more than twelve of them, and a mesh only takes the joints it uses.
            var result = LoadGlb(variant);

            Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
            Assert.That(result.Prims, Has.Count.EqualTo(8));
            Assert.That(result.Prims.Select(p => p.Skin!.JointNames.Count), Has.All.InRange(1, 12));
        }

        [Test]
        public void Gltf_reports_the_attachment_bones_it_leaves_out()
        {
            var result = LoadGlb("default-blender-all-bones");

            var ignored = result.Diagnostics.Single(d => d.Code == ModelDiagnosticCode.JointsIgnored);
            Assert.That(ignored.Subject, Does.Contain("aSkull"));
            Assert.That(ignored.Subject, Does.Not.Contain("mSkull"));
        }

        [TestCaseSource(nameof(Variants))]
        public void Joints_are_avatar_joints_each_used_and_weights_add_up_to_one(string variant)
        {
            var avatar = DefaultSkeleton();
            foreach (var prims in new[] { LoadDae(variant).Prims, LoadGlb(variant).Prims })
            {
                foreach (var prim in prims)
                {
                    var skin = prim.Skin!;
                    Assert.That(skin.JointNames, Is.Unique);
                    Assert.That(skin.JointNames, Has.All.Matches<string>(avatar.ContainsKey), "joints of the avatar");
                    Assert.That(skin.InverseBindMatrices, Has.Length.EqualTo(skin.JointNames.Count * 16));

                    var used = new HashSet<int>();
                    foreach (var face in prim.Faces)
                    {
                        Assert.That(face.Weights, Has.Count.EqualTo(face.Vertices.Count));
                        foreach (var w in face.Weights!)
                        {
                            Assert.That(w.Weight0 + w.Weight1 + w.Weight2 + w.Weight3, Is.EqualTo(1f).Within(1e-4f));
                            foreach (var (joint, weight) in new[] { (w.Joint0, w.Weight0), (w.Joint1, w.Weight1), (w.Joint2, w.Weight2), (w.Joint3, w.Weight3) })
                            {
                                if (weight > 0f) used.Add(joint);
                            }
                        }
                    }
                    Assert.That(used.Min(), Is.GreaterThanOrEqualTo(0));
                    Assert.That(used.Max(), Is.LessThan(skin.JointNames.Count));
                    Assert.That(used, Has.Count.EqualTo(skin.JointNames.Count), "every joint of the skin has weight somewhere");
                }
            }
        }

        /// <summary>The nearest point of <paramref name="cloud"/> to <paramref name="p"/></summary>
        private static float DistanceToCloud(NumVector3 p, List<NumVector3> cloud) => cloud.Min(q => (q - p).Length());

        [TestCaseSource(nameof(Variants))]
        public void At_rest_every_vertex_is_where_the_file_draws_it(string variant)
        {
            // These files are Blender's own orientation, facing -Y, where Second Life's avatar faces +X, so they
            // are not posed with the stock skeleton. The loader keeps each joint where the file puts it and
            // gives it the avatar's axes, so that is the skeleton to pose with. Where the file puts each joint,
            // and where it draws each vertex, are read here straight from the COLLADA text.
            var text = File.ReadAllText(FixturePath(variant + ".dae"));

            var raw = ReadVertices(text);
            Assert.That(raw, Has.Count.EqualTo(8));
            var origins = ReadJointOrigins(text);

            var skeleton = DefaultSkeleton();
            var live = new Dictionary<string, NumMatrix>(skeleton);
            foreach (var pair in origins)
            {
                var m = skeleton[pair.Key];
                live[pair.Key] = new NumMatrix(m.M11, m.M12, m.M13, 0, m.M21, m.M22, m.M23, 0, m.M31, m.M32, m.M33, 0,
                    pair.Value.X, pair.Value.Y, pair.Value.Z, 1);
            }

            var prims = LoadDae(variant).Prims;
            Assert.That(prims.Select(p => BaseName(p.ID)).OrderBy(n => n), Is.EqualTo(raw.Keys.OrderBy(n => n)));
            foreach (var prim in prims)
            {
                var cloud = raw[BaseName(prim.ID)];
                var posed = Skinned(Decode(prim), live);
                Assert.That(posed.Max(p => DistanceToCloud(p, cloud)), Is.LessThan(Tolerance), prim.ID);
            }
        }

        [TestCaseSource(nameof(Variants))]
        public void Gltf_and_Collada_agree_on_every_mesh(string variant)
        {
            var avatar = DefaultSkeleton();
            var dae = LoadDae(variant).Prims.ToDictionary(Key);
            var glb = LoadGlb(variant).Prims.ToDictionary(Key);

            Assert.That(glb.Keys, Is.EquivalentTo(dae.Keys));
            foreach (var key in dae.Keys)
            {
                var (daeMin, daeMax) = Bounds(Skinned(Decode(dae[key]), avatar));
                var (glbMin, glbMax) = Bounds(Skinned(Decode(glb[key]), avatar));
                AssertClose(glbMin, daeMin, key + " min");
                AssertClose(glbMax, daeMax, key + " max");
            }
        }

        [TestCaseSource(nameof(Variants))]
        public void A_pose_moves_the_skinned_mesh_the_same_way_from_either_format(string variant)
        {
            // Turn every joint a mesh uses about its own origin, each by a different angle. A mesh that follows
            // its joints moves in the same way whichever file it came from, and only the weights and inverse bind
            // matrices say how.
            var rest = DefaultSkeleton();
            var dae = LoadDae(variant).Prims.ToDictionary(Key);
            var glb = LoadGlb(variant).Prims.ToDictionary(Key);
            var live = new Dictionary<string, NumMatrix>(rest);
            int n = 0;
            foreach (var name in dae.Values.SelectMany(p => p.Skin!.JointNames).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            {
                float angle = 0.15f + 0.1f * (n++ % 5);
                var turn = n % 3 == 0 ? NumMatrix.CreateRotationX(angle) : n % 3 == 1 ? NumMatrix.CreateRotationY(angle) : NumMatrix.CreateRotationZ(angle);
                live[name] = turn * rest[name];
            }

            bool anythingMoved = false;
            foreach (var key in dae.Keys)
            {
                var (daeMin, daeMax) = Bounds(Skinned(Decode(dae[key]), live));
                var (glbMin, glbMax) = Bounds(Skinned(Decode(glb[key]), live));
                AssertClose(glbMin, daeMin, key + " posed min");
                AssertClose(glbMax, daeMax, key + " posed max");

                var (restMin, restMax) = Bounds(Skinned(Decode(dae[key]), rest));
                if ((restMin - daeMin).Length() > 0.01f || (restMax - daeMax).Length() > 0.01f) anythingMoved = true;
            }
            Assert.That(anythingMoved, Is.True, "the pose should move something, or the test shows nothing");
        }

        #region Exported for Second Life

        // The same scene exported from Blender 4.5.10 with the "sl+open sim rigged" preset and Apply under Global
        // Orientation, Forward -X and Up Z, which is how the README says to export for Second Life. The avatar
        // faces +X here, as Second Life's does, and the file's skeleton is the stock one.
        private const string SecondLifeExport = "sl-open-sim-rigged";

        [Test]
        public void Export_for_Second_Life_loads_every_mesh_rigged()
        {
            var result = new ColladaLoader().LoadWithDiagnostics(FixturePath(SecondLifeExport + ".dae"), false);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Prims, Has.Count.EqualTo(8));
            Assert.That(result.Prims.Select(p => p.Skin!.JointNames.Count).OrderBy(n => n), Is.EqualTo(new[] { 1, 1, 1, 2, 2, 6, 7, 12 }));
        }

        [Test]
        public void Export_for_Second_Life_faces_forward_on_the_stock_skeleton()
        {
            var text = File.ReadAllText(FixturePath(SecondLifeExport + ".dae"));
            var stock = DefaultSkeleton();

            var eyelashes = ReadVertices(text)["eyelashMesh"];
            Assert.That(eyelashes.Average(v => v.X), Is.GreaterThan(0.05f), "the face is toward +X");
            Assert.That(eyelashes.Average(v => v.Y), Is.EqualTo(0f).Within(0.01f));

            foreach (var joint in ReadJointOrigins(text))
            {
                var expected = new NumVector3(stock[joint.Key].M41, stock[joint.Key].M42, stock[joint.Key].M43);
                AssertClose(joint.Value, expected, joint.Key);
            }
        }

        [Test]
        public void Export_for_Second_Life_at_rest_on_the_stock_skeleton_is_where_the_file_draws_it()
        {
            // Nothing is moved to suit the file this time: the viewer's own skeleton at rest must put every vertex
            // of every mesh where the file has it. The file's joints are a couple of millimetres from the stock
            // ones, so the tolerance is a little looser than when the file's own joints are used.
            var raw = ReadVertices(File.ReadAllText(FixturePath(SecondLifeExport + ".dae")));
            var stock = DefaultSkeleton();

            var prims = new ColladaLoader().LoadWithDiagnostics(FixturePath(SecondLifeExport + ".dae"), false).Prims;
            Assert.That(prims.Select(p => BaseName(p.ID)).OrderBy(n => n), Is.EqualTo(raw.Keys.OrderBy(n => n)));
            foreach (var prim in prims)
            {
                var cloud = raw[BaseName(prim.ID)];
                Assert.That(Skinned(Decode(prim), stock).Max(p => DistanceToCloud(p, cloud)), Is.LessThan(4e-3f), prim.ID);
            }
        }

        [Test]
        public void Export_for_Second_Life_is_the_Blender_scene_turned_to_face_forward()
        {
            // The same scene as the Blender-oriented files, so every vertex is the same point with the avatar turned
            // from facing -Y to facing +X: (x, y) in the Blender file is (-y, x) here. Each mesh keeps its size
            // and its joints, and nothing else changes.
            var blender = ReadVertices(File.ReadAllText(FixturePath("default-blender-deform-bones.dae")));
            var secondLife = ReadVertices(File.ReadAllText(FixturePath(SecondLifeExport + ".dae")));
            Assert.That(secondLife.Keys, Is.EquivalentTo(blender.Keys));

            foreach (var name in blender.Keys)
            {
                var turned = blender[name].Select(v => new NumVector3(-v.Y, v.X, v.Z)).ToList();
                Assert.That(secondLife[name], Has.Count.EqualTo(turned.Count), name);
                Assert.That(secondLife[name].Max(p => DistanceToCloud(p, turned)), Is.LessThan(Tolerance), name);
            }
        }

        #endregion Exported for Second Life
    }
}
