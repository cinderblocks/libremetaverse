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
using System.Text;
using System.Threading.Tasks;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// What is checked before a model is uploaded. Every error has a model that is wrong in that one way and a
    /// control that is as close to it as it can be and is fine, and a model with an error must send nothing.
    /// </summary>
    [TestFixture]
    public class ModelUploadChecksTests
    {
        private const string CapUrl = "http://test.invalid/new-file-agent-inventory";
        private const string UploaderUrl = "http://test.invalid/uploader/abc123";

        private FakeGridClient _client = null!;
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("NewFileAgentInventory", new Uri(CapUrl));
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-upload-check-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        #region Builders

        private static ModelFace Face(int vertices = 3, ModelMaterial? material = null)
        {
            var face = new ModelFace { MaterialID = "m", Material = material ?? new ModelMaterial { ID = "m" } };
            for (int i = 0; i < vertices; i++)
            {
                face.Vertices.Add(new Vertex
                {
                    Position = new Vector3(i % 100 / 200f - 0.25f, i / 100 / 200f - 0.25f, 0f),
                    Normal = Vector3.UnitZ,
                    TexCoord = new Vector2(0.5f, 0.5f)
                });
            }
            for (int i = 0; i + 2 < vertices; i += 3)
            {
                face.Indices.Add((uint)i);
                face.Indices.Add((uint)i + 1);
                face.Indices.Add((uint)i + 2);
            }
            // vertices that are not in a whole triangle still belong to one
            if (vertices % 3 != 0)
            {
                face.Indices.Add((uint)vertices - 2);
                face.Indices.Add((uint)vertices - 1);
                face.Indices.Add(0);
            }
            return face;
        }

        private static ModelPrim Prim(string id = "root", params ModelFace[] faces)
        {
            var prim = new ModelPrim { ID = id, Rotation = Quaternion.Identity, Scale = Vector3.One };
            prim.Faces.AddRange(faces.Length > 0 ? faces : new[] { Face() });
            return prim;
        }

        private static ModelPrim RiggedPrim(int joints = 2)
        {
            var prim = Prim();
            prim.Skin = new ModelSkin { InverseBindMatrices = new float[joints * 16] };
            for (int j = 0; j < joints; j++)
            {
                prim.Skin.JointNames.Add("joint" + j);
                for (int k = 0; k < 4; k++) prim.Skin.InverseBindMatrices[j * 16 + k * 5] = 1f;
            }
            prim.Faces[0].Weights = Enumerable.Range(0, 3).Select(_ => new VertexWeight { Joint0 = 0, Weight0 = 1f }).ToList();
            return prim;
        }

        private static ModelDiagnostic Only(IEnumerable<ModelDiagnostic> diagnostics, ModelDiagnosticCode code)
        {
            var found = diagnostics.Where(d => d.Code == code).ToList();
            Assert.That(found, Has.Count.EqualTo(1), "one " + code);
            return found[0];
        }

        #endregion Builders

        #region Models that are fine

        [Test]
        public void Check_ValidModel_SaysNothing()
        {
            Assert.That(ModelUploadChecks.Check(new[] { Prim("a"), RiggedPrim() }), Is.Empty);
        }

        [Test]
        public void Check_MeshTheLoadersMake_IsNotHeldToTheUnitCubeOrToHavingSizeOnEveryAxis()
        {
            // Positions outside -0.5 .. 0.5 are clamped when written, and a flat mesh has no size along one axis
            var prim = Prim();
            var v = prim.Faces[0].Vertices[0];
            v.Position = new Vector3(5, 5, 5);
            prim.Faces[0].Vertices[0] = v;
            prim.Scale = new Vector3(1, 1, 0);

            Assert.That(ModelUploadChecks.Check(new[] { prim }), Is.Empty);
        }

        [Test]
        public void Check_EightFacesAndTheMostVertices_AreTheLimitsAndAreAllowed()
        {
            var eight = Prim("eight", Enumerable.Range(0, 8).Select(_ => Face()).ToArray());
            var big = Prim("big", Face(ModelPrim.MaxVerticesPerFace));

            Assert.That(ModelUploadChecks.Check(new[] { eight, big }), Is.Empty);
        }

        [Test]
        public void Limits_AreTheReferenceViewersNotJustSomeNumber()
        {
            // The viewer stops a face at 65535 vertices ("verts.size() >= 65535" in lldaeloader.cpp) and a model at
            // 8 faces (MAX_MODEL_FACES in llmodel.h). The tests above are written against the constants, so this
            // is what ties the constants to the viewer.
            Assert.That(ModelPrim.MaxVerticesPerFace, Is.EqualTo(65534));
            Assert.That(ModelPrim.MaxFaces, Is.EqualTo(8));
            Assert.That(ModelSkin.MaxJoints, Is.EqualTo(110));
        }

        #endregion Models that are fine

        #region Errors

        [Test]
        public void Check_NineFaces_IsTooMany()
        {
            var d = Only(ModelUploadChecks.Check(new[] { Prim("nine", Enumerable.Range(0, 9).Select(_ => Face()).ToArray()) }), ModelDiagnosticCode.TooManyFaces);

            Assert.That(d.Severity, Is.EqualTo(ModelDiagnosticSeverity.Error));
            Assert.That(d.Count, Is.EqualTo(9));
            Assert.That(d.Subject, Is.EqualTo("nine"));
        }

        [Test]
        public void Check_OneVertexOverTheLimit_IsTooBigToIndex()
        {
            var d = Only(ModelUploadChecks.Check(new[] { Prim("huge", Face(ModelPrim.MaxVerticesPerFace + 1)) }), ModelDiagnosticCode.LimitExceeded);

            Assert.That(d.Severity, Is.EqualTo(ModelDiagnosticSeverity.Error));
            Assert.That(d.Subject, Is.EqualTo("huge"));
        }

        [Test]
        public void Check_PrimWithNoFaces_HasNoGeometry()
        {
            var prim = Prim();
            prim.Faces.Clear();

            Assert.That(Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.NoGeometry).Severity, Is.EqualTo(ModelDiagnosticSeverity.Error));
        }

        [Test]
        public void Check_FaceWithNoTriangles_HasNoGeometry()
        {
            var empty = Face();
            empty.Indices.Clear();

            var d = Only(ModelUploadChecks.Check(new[] { Prim("a", Face(), empty) }), ModelDiagnosticCode.NoGeometry);

            Assert.That(d.Count, Is.EqualTo(1));
        }

        [Test]
        public void Check_IndexAtTheVertexCount_PointsAtAVertexThatIsNotThere()
        {
            var bad = Face();
            bad.Indices[2] = 3; // the face has vertices 0, 1 and 2
            var good = Face();

            var d = Only(ModelUploadChecks.Check(new[] { Prim("a", good, bad) }), ModelDiagnosticCode.InvalidModel);

            Assert.That(d.Count, Is.EqualTo(1));
        }

        [Test]
        public void Check_IndexCountThatIsNotWholeTriangles_IsInvalid()
        {
            var bad = Face();
            bad.Indices.RemoveAt(2);

            Only(ModelUploadChecks.Check(new[] { Prim("a", bad) }), ModelDiagnosticCode.InvalidModel);
        }

        [TestCase("position")]
        [TestCase("normal")]
        [TestCase("uv")]
        public void Check_VertexValueThatIsNotANumber_IsInvalid(string which)
        {
            var bad = Face();
            var v = bad.Vertices[1];
            if (which == "position") v.Position = new Vector3(float.NaN, 0, 0);
            if (which == "normal") v.Normal = new Vector3(0, float.PositiveInfinity, 0);
            if (which == "uv") v.TexCoord = new Vector2(0, float.NaN);
            bad.Vertices[1] = v;

            Only(ModelUploadChecks.Check(new[] { Prim("a", bad) }), ModelDiagnosticCode.InvalidModel);
        }

        [TestCase("position")]
        [TestCase("scale")]
        [TestCase("rotation")]
        public void Check_PrimPlacementThatIsNotANumber_IsInvalid(string which)
        {
            var prim = Prim();
            if (which == "position") prim.Position = new Vector3(float.NaN, 0, 0);
            if (which == "scale") prim.Scale = new Vector3(1, float.PositiveInfinity, 1);
            if (which == "rotation") prim.Rotation = new Quaternion(0, 0, float.NaN, 1);

            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        [Test]
        public void Check_EachPrimIsNamedByItsIdOrItsPlace()
        {
            var unnamed = Prim("");
            unnamed.Faces.Clear();

            var d = Only(ModelUploadChecks.Check(new[] { Prim("fine"), unnamed }), ModelDiagnosticCode.NoGeometry);

            Assert.That(d.Subject, Is.EqualTo("model_1"), "the name the upload gives it");
        }

        #endregion Errors

        #region Rigs

        [Test]
        public void Check_RigThatIsFine_SaysNothing()
        {
            Assert.That(ModelUploadChecks.Check(new[] { RiggedPrim(ModelSkin.MaxJoints) }), Is.Empty);
        }

        [Test]
        public void Check_MoreJointsThanTheViewerUses_IsOverTheLimit()
        {
            var d = Only(ModelUploadChecks.Check(new[] { RiggedPrim(ModelSkin.MaxJoints + 1) }), ModelDiagnosticCode.LimitExceeded);

            Assert.That(d.Count, Is.EqualTo(ModelSkin.MaxJoints + 1));
        }

        [Test]
        public void Check_SkinWithNoJoints_IsInvalid()
        {
            var prim = RiggedPrim();
            prim.Skin!.JointNames.Clear();
            prim.Skin.InverseBindMatrices = Array.Empty<float>();

            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        [Test]
        public void Check_FewerMatricesThanJoints_IsInvalid()
        {
            var prim = RiggedPrim(2);
            prim.Skin!.InverseBindMatrices = new float[16];

            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        [Test]
        public void Check_BindShapeThatIsNotFourByFour_IsInvalid()
        {
            var prim = RiggedPrim();
            prim.Skin!.BindShapeMatrix = new float[15];

            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        [TestCase("matrix")]
        [TestCase("bind")]
        public void Check_SkinMatrixThatIsNotANumber_IsInvalid(string which)
        {
            var prim = RiggedPrim();
            if (which == "matrix") prim.Skin!.InverseBindMatrices[3] = float.NaN;
            else prim.Skin!.BindShapeMatrix[3] = float.PositiveInfinity;

            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        [Test]
        public void Check_RiggedFaceWithoutWeights_IsInvalid()
        {
            var prim = RiggedPrim();
            prim.Faces[0].Weights = null;

            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        [Test]
        public void Check_WeightsForFewerVerticesThanTheFace_IsInvalid()
        {
            var prim = RiggedPrim();
            prim.Faces[0].Weights!.RemoveAt(0);

            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        [Test]
        public void Check_WeightToAJointTheSkinLacks_IsInvalidButOnlyIfItCounts()
        {
            var prim = RiggedPrim(2);
            var w = prim.Faces[0].Weights![1];
            w.Joint1 = 2; w.Weight1 = 0f; // nothing is written for a weight of zero, so this is harmless
            prim.Faces[0].Weights![1] = w;
            Assert.That(ModelUploadChecks.Check(new[] { prim }), Is.Empty, "control");

            w.Weight1 = 0.5f;
            prim.Faces[0].Weights![1] = w;
            Only(ModelUploadChecks.Check(new[] { prim }), ModelDiagnosticCode.InvalidModel);
        }

        #endregion Rigs

        #region Warnings

        [Test]
        public void Check_TextureWithUnknownSize_WarnsOncePerMaterial()
        {
            var material = new ModelMaterial { ID = "t", Texture = "t.j2c", TextureData = new byte[] { 1, 2, 3 } };
            var known = new ModelMaterial { ID = "k", Texture = "k.j2c", TextureData = new byte[] { 1 }, Width = 64, Height = 64 };

            var diagnostics = ModelUploadChecks.Check(new[]
            {
                Prim("a", Face(3, material), Face(3, material), Face(3, known)),
                Prim("b", Face(3, material))
            });

            var d = Only(diagnostics, ModelDiagnosticCode.TextureSizeUnknown);
            Assert.That(d.Severity, Is.EqualTo(ModelDiagnosticSeverity.Warning));
            Assert.That(d.Subject, Is.EqualTo("t.j2c"));
            Assert.That(diagnostics, Has.Count.EqualTo(1));
        }

        #endregion Warnings

        #region Upload

        private void QueueGoodReplies()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":10}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{UUID.Random()}\"}}", "application/json");
        }

        [Test]
        public async Task UploadModelAsync_ValidModel_SaysNothingAndUploads()
        {
            QueueGoodReplies();

            var result = await _client.Inventory.UploadModelAsync(new[] { Prim("a") }, "m", "d", UUID.Random(), Permissions.NoPermissions);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public async Task UploadModelAsync_ModelWithAnError_SendsNothing()
        {
            QueueGoodReplies();
            var tooMany = Prim("nine", Enumerable.Range(0, 9).Select(_ => Face()).ToArray());

            var result = await _client.Inventory.UploadModelAsync(new[] { tooMany }, "m", "d", UUID.Random(), Permissions.NoPermissions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Status, Is.EqualTo("invalid_model"));
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(result.HasErrors, Is.True);
                Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.TooManyFaces));
                Assert.That(_client.CapturedRequests.Count, Is.EqualTo(0), "nothing was sent");
            });
        }

        [Test]
        public async Task UploadModelAsync_ModelWithAnError_IsRefusedBeforeTheCapabilityIsLookedFor()
        {
            using var client = new FakeGridClient();
            var broken = Prim();
            broken.Faces.Clear();

            var result = await client.Inventory.UploadModelAsync(new[] { broken }, "m", "d", UUID.Random(), Permissions.NoPermissions);

            Assert.That(result.Status, Is.EqualTo("invalid_model"));
        }

        [Test]
        public async Task UploadModelAsync_TextureOfUnknownSize_WarnsAndStillUploads()
        {
            QueueGoodReplies();
            var material = new ModelMaterial { ID = "t", Texture = "t.j2c", TextureData = new byte[] { 1, 2, 3 } };

            var result = await _client.Inventory.UploadModelAsync(new[] { Prim("a", Face(3, material)) }, "m", "d", UUID.Random(), Permissions.NoPermissions);

            Assert.That(result.Success, Is.True);
            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.TextureSizeUnknown));
        }

        [Test]
        public async Task UploadModelAsync_OtherFailures_KeepTheirStatusAndCarryTheWarnings()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":500}}", "application/json");
            var material = new ModelMaterial { ID = "t", Texture = "t.j2c", TextureData = new byte[] { 1 } };

            var result = await _client.Inventory.UploadModelAsync(new[] { Prim("a", Face(3, material)) }, "m", "d", UUID.Random(),
                Permissions.NoPermissions, confirmCost: price => price < 100);

            Assert.That(result.Status, Is.EqualTo("cost_rejected"));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.TextureSizeUnknown));
        }

        #endregion Upload

        #region From a file to the grid

        [Test]
        public async Task ObjWithNineMaterials_WarnsWhenLoadedAndIsRefusedWhenUploaded()
        {
            var text = new StringBuilder("v 0 0 0\nv 1 0 0\nv 0 1 0\n");
            for (int i = 0; i < 9; i++) text.Append($"usemtl m{i}\nf 1 2 3\n");
            var path = Path.Combine(_tempDir, "nine.obj");
            File.WriteAllText(path, text.ToString());

            var loaded = new ObjLoader().LoadWithDiagnostics(path, false);
            var uploaded = await _client.Inventory.UploadModelAsync(loaded.Prims, "m", "d", UUID.Random(), Permissions.NoPermissions);

            Assert.That(loaded.HasErrors, Is.False);
            Assert.That(loaded.Diagnostics.Single(), Has.Property("Code").EqualTo(ModelDiagnosticCode.TooManyFaces)
                .And.Property("Severity").EqualTo(ModelDiagnosticSeverity.Warning));
            Assert.That(uploaded.Status, Is.EqualTo("invalid_model"));
            Assert.That(uploaded.Diagnostics.Single(), Has.Property("Code").EqualTo(ModelDiagnosticCode.TooManyFaces)
                .And.Property("Severity").EqualTo(ModelDiagnosticSeverity.Error));
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(0));
        }

        // A face of one triangle after another, each vertex used once, so the face has as many vertices as it has corners
        private string WriteBigObj(int vertices)
        {
            var text = new StringBuilder("vn 0 0 1\n");
            for (int i = 0; i < vertices; i++) text.Append($"v {i % 256} {i / 256 % 256} {i / 65536}\n");
            for (int t = 0; t + 2 < vertices; t += 3) text.Append($"f {t + 1}//1 {t + 2}//1 {t + 3}//1\n");
            if (vertices % 3 != 0) text.Append($"f {vertices - 1}//1 {vertices}//1 1//1\n");
            var path = Path.Combine(_tempDir, $"big{vertices}.obj");
            File.WriteAllText(path, text.ToString());
            return path;
        }

        [Test]
        public void ObjLoader_FaceAtTheLimitLoads_AndOneVertexOverIsSkippedWithTheSameNumberTheUploadUses()
        {
            var atLimit = new ObjLoader().LoadWithDiagnostics(WriteBigObj(ModelPrim.MaxVerticesPerFace), false);
            var over = new ObjLoader().LoadWithDiagnostics(WriteBigObj(ModelPrim.MaxVerticesPerFace + 1), false);

            Assert.That(atLimit.Prims, Has.Count.EqualTo(1));
            Assert.That(atLimit.Prims[0].Faces[0].Vertices, Has.Count.EqualTo(ModelPrim.MaxVerticesPerFace));
            Assert.That(ModelUploadChecks.Check(atLimit.Prims), Is.Empty, "what the loader lets through, the upload accepts");
            Assert.That(over.Prims, Is.Empty);
            Assert.That(over.Diagnostics.Select(d => d.Code), Does.Contain(ModelDiagnosticCode.FaceSkipped));
        }

        #endregion From a file to the grid
    }
}
