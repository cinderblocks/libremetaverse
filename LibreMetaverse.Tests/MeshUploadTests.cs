/*
 * Copyright (c) 2026, Sjofn LLC
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
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the two-phase mesh model upload flow. Verified against the reference viewer
    /// (LLMeshUploadThread::requestWholeModelFee/doWholeModelUpload, llmeshrepository.cpp): phase 1
    /// POSTs a fee-quote request (name/permissions/asset_resources) to NewFileAgentInventory and
    /// gets back a "state":"upload" response carrying an "uploader" URL and an "upload_price";
    /// phase 2 POSTs the same asset_resources map to that uploader URL and gets back a
    /// "state":"complete" response carrying "new_inventory_item" and "new_asset".
    /// </summary>
    [TestFixture]
    public class MeshUploadTests
    {
        private const string CapUrl = "http://test.invalid/new-file-agent-inventory";
        private const string UploaderUrl = "http://test.invalid/uploader/abc123";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("NewFileAgentInventory", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        private static ModelPrim MakeTestPrim(string id, byte[]? textureData = null, int width = 64, int height = 32)
        {
            var material = new ModelMaterial
            {
                ID = "mat0",
                DiffuseColor = new Color4(0.2f, 0.4f, 0.6f, 1f),
                Texture = textureData != null ? "tex0" : string.Empty,
                TextureData = textureData ?? Array.Empty<byte>(),
                Width = width,
                Height = height
            };

            var face = new ModelFace { MaterialID = material.ID, Material = material };
            face.AddVertex(new Vertex { Position = new Vector3(-0.5f, -0.5f, 0f), Normal = Vector3.UnitZ, TexCoord = Vector2.Zero });
            face.AddVertex(new Vertex { Position = new Vector3(0.5f, -0.5f, 0f), Normal = Vector3.UnitZ, TexCoord = new Vector2(1, 0) });
            face.AddVertex(new Vertex { Position = new Vector3(0f, 0.5f, 0f), Normal = Vector3.UnitZ, TexCoord = new Vector2(0.5f, 1) });

            var prim = new ModelPrim
            {
                ID = id,
                Position = Vector3.Zero,
                Rotation = Quaternion.Identity,
                Scale = new Vector3(1, 1, 1)
            };
            prim.Faces.Add(face);
            return prim;
        }

        [Test]
        public async Task UploadModelAsync_HappyPath_CompletesBothPhases()
        {
            var itemId = UUID.Random();
            var assetId = UUID.Random();

            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":42}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{itemId}\",\"new_asset\":\"{assetId}\"}}", "application/json");

            var prims = new List<ModelPrim> { MakeTestPrim("root", new byte[] { 1, 2, 3, 4 }) };
            var result = await _client.Inventory.UploadModelAsync(
                prims, "Test Model", "a description", UUID.Random(), Permissions.NoPermissions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Status, Is.EqualTo("complete"));
                Assert.That(result.ItemID, Is.EqualTo(itemId));
                Assert.That(result.AssetID, Is.EqualTo(assetId));
                Assert.That(_client.CapturedRequests.Count, Is.EqualTo(2), "expected one fee-request POST and one upload POST");
            });
        }

        [Test]
        public async Task UploadModelAsync_FeeRequestBody_HasExpectedShape()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":10}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{UUID.Random()}\"}}", "application/json");

            var folderId = UUID.Random();
            var prims = new List<ModelPrim> { MakeTestPrim("root", new byte[] { 9, 9, 9 }) };
            await _client.Inventory.UploadModelAsync(prims, "Test Model", "desc", folderId, Permissions.NoPermissions);

            var feeRequestBody = OSDParser.Deserialize(_client.CapturedRequestBodies[0]) as OSDMap;
            var uploadBody = OSDParser.Deserialize(_client.CapturedRequestBodies[1]) as OSDMap;
            Assert.That(feeRequestBody, Is.Not.Null);
            Assert.That(uploadBody, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(feeRequestBody!["asset_type"].AsString(), Is.EqualTo("mesh"));
                Assert.That(feeRequestBody["inventory_type"].AsString(), Is.EqualTo("object"));
                Assert.That(feeRequestBody["name"].AsString(), Is.EqualTo("Test Model"));
                Assert.That(feeRequestBody["folder_id"].AsUUID(), Is.EqualTo(folderId));
                Assert.That(feeRequestBody["texture_folder_id"].AsUUID(), Is.EqualTo(folderId));

                var assetResources = feeRequestBody["asset_resources"] as OSDMap;
                Assert.That(assetResources, Is.Not.Null);

                var meshList = assetResources!["mesh_list"] as OSDArray;
                Assert.That(meshList, Has.Count.EqualTo(1));
                Assert.That(meshList![0].AsBinary(), Is.EqualTo(prims[0].Asset), "mesh_list must carry the actual encoded geometry, not a stand-in");

                var instanceList = assetResources["instance_list"] as OSDArray;
                Assert.That(instanceList, Has.Count.EqualTo(1));
                var instance = (OSDMap)instanceList![0];
                Assert.That(instance["mesh"].AsInteger(), Is.EqualTo(0));
                Assert.That(instance["material"].AsInteger(), Is.EqualTo((int)Material.Wood));
                Assert.That(instance["physics_shape_type"].AsInteger(), Is.EqualTo((int)PhysicsShapeType.ConvexHull));

                var faceList = instance["face_list"] as OSDArray;
                Assert.That(faceList, Has.Count.EqualTo(1));
                var face = (OSDMap)faceList![0];
                Assert.That(face["image"].AsInteger(), Is.EqualTo(0));
                Assert.That(face["diffuse_color"].AsColor4(), Is.EqualTo(new Color4(0.2f, 0.4f, 0.6f, 1f)));

                // Fee-quote phase: dimensions only, no real texture bytes (matches
                // LLMeshUploadThread::wholeModelToLLSD's include_textures=false path).
                var textureList = assetResources["texture_list"] as OSDArray;
                Assert.That(textureList, Has.Count.EqualTo(1));
                Assert.That(textureList![0].AsBinary(), Is.EqualTo(Array.Empty<byte>()), "fee-quote must not include real texture bytes");

                var textureInfo = assetResources["texture_info"] as OSDArray;
                Assert.That(textureInfo, Has.Count.EqualTo(1));
                Assert.That(((OSDMap)textureInfo![0])["width"].AsInteger(), Is.EqualTo(64));
                Assert.That(((OSDMap)textureInfo[0])["height"].AsInteger(), Is.EqualTo(32));

                // Final upload phase: real texture bytes, no texture_info.
                var uploadAssetResources = uploadBody;
                var uploadTextureList = uploadAssetResources!["texture_list"] as OSDArray;
                Assert.That(uploadTextureList, Has.Count.EqualTo(1));
                Assert.That(uploadTextureList![0].AsBinary(), Is.EqualTo(new byte[] { 9, 9, 9 }));
                Assert.That(uploadAssetResources.ContainsKey("texture_info"), Is.False);
            });
        }

        [Test]
        public async Task UploadModelAsync_UploadBody_IsBareAssetResourcesNotWrapped()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":10}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{UUID.Random()}\"}}", "application/json");

            var prims = new List<ModelPrim> { MakeTestPrim("root") };
            await _client.Inventory.UploadModelAsync(prims, "Test Model", "desc", UUID.Random(), Permissions.NoPermissions);

            var uploadBody = OSDParser.Deserialize(_client.CapturedRequestBodies[1]) as OSDMap;
            Assert.That(uploadBody, Is.Not.Null);

            Assert.Multiple(() =>
            {
                // Phase 2 posts asset_resources directly -- no outer folder_id/asset_type wrapper.
                Assert.That(uploadBody!.ContainsKey("mesh_list"), Is.True);
                Assert.That(uploadBody.ContainsKey("instance_list"), Is.True);
                Assert.That(uploadBody.ContainsKey("asset_type"), Is.False);
                Assert.That(uploadBody.ContainsKey("folder_id"), Is.False);
            });
        }

        [Test]
        public async Task UploadModelAsync_ConfirmCostRejects_StopsBeforeSecondPost()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":500}}", "application/json");

            var prims = new List<ModelPrim> { MakeTestPrim("root") };
            var result = await _client.Inventory.UploadModelAsync(
                prims, "Test Model", "desc", UUID.Random(), Permissions.NoPermissions,
                confirmCost: price => price < 100);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Status, Is.EqualTo("cost_rejected"));
                Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1), "the upload POST must not happen after rejection");
            });
        }

        [Test]
        public async Task UploadModelAsync_NoCapability_ReturnsFalseWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                client.AddCapability("SomeOtherCap", new Uri("http://test.invalid/other"));

                var prims = new List<ModelPrim> { MakeTestPrim("root") };
                var result = await client.Inventory.UploadModelAsync(
                    prims, "Test Model", "desc", UUID.Random(), Permissions.NoPermissions);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Status, Is.EqualTo("capability_missing"));
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public void UploadModelAsync_EmptyPrimList_Throws()
        {
            Assert.ThrowsAsync<ArgumentException>(() =>
                _client.Inventory.UploadModelAsync(
                    new List<ModelPrim>(), "Test Model", "desc", UUID.Random(), Permissions.NoPermissions));
        }

        [Test]
        public async Task UploadModelAsync_TextureWithUnknownDimensions_FeeRequestFallsBackToRealBytes()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":10}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{UUID.Random()}\"}}", "application/json");

            // Width/Height default to 0 here, simulating a pre-encoded .j2c/.jp2 texture that
            // ColladaLoader passed through without decoding, so its real dimensions are unknown.
            var prims = new List<ModelPrim> { MakeTestPrim("root", new byte[] { 5, 5, 5 }, width: 0, height: 0) };
            await _client.Inventory.UploadModelAsync(prims, "Test Model", "desc", UUID.Random(), Permissions.NoPermissions);

            var feeRequestBody = OSDParser.Deserialize(_client.CapturedRequestBodies[0]) as OSDMap;
            var assetResources = feeRequestBody!["asset_resources"] as OSDMap;

            Assert.Multiple(() =>
            {
                // Must never quote against a fabricated 0x0 -- fall back to sending real bytes instead.
                var textureList = assetResources!["texture_list"] as OSDArray;
                Assert.That(textureList![0].AsBinary(), Is.EqualTo(new byte[] { 5, 5, 5 }));
                Assert.That(assetResources.ContainsKey("texture_info"), Is.False);
            });
        }

        [Test]
        public async Task UploadModelAsync_MultiplePrims_DedupsSharedMaterialTexture()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":10}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{UUID.Random()}\"}}", "application/json");

            var sharedTexture = new byte[] { 7, 7, 7 };
            var material = new ModelMaterial { ID = "shared", DiffuseColor = Color4.White, Texture = "shared.tex", TextureData = sharedTexture };

            ModelPrim MakePrimWithSharedMaterial(string id)
            {
                var face = new ModelFace { MaterialID = material.ID, Material = material };
                face.AddVertex(new Vertex { Position = Vector3.Zero, Normal = Vector3.UnitZ, TexCoord = Vector2.Zero });
                face.AddVertex(new Vertex { Position = Vector3.UnitX, Normal = Vector3.UnitZ, TexCoord = Vector2.Zero });
                face.AddVertex(new Vertex { Position = Vector3.UnitY, Normal = Vector3.UnitZ, TexCoord = Vector2.Zero });
                var prim = new ModelPrim { ID = id, Rotation = Quaternion.Identity, Scale = new Vector3(1, 1, 1) };
                prim.Faces.Add(face);
                return prim;
            }

            var prims = new List<ModelPrim> { MakePrimWithSharedMaterial("a"), MakePrimWithSharedMaterial("b") };
            await _client.Inventory.UploadModelAsync(prims, "Test Model", "desc", UUID.Random(), Permissions.NoPermissions);

            var feeRequestBody = OSDParser.Deserialize(_client.CapturedRequestBodies[0]) as OSDMap;
            var assetResources = feeRequestBody!["asset_resources"] as OSDMap;

            Assert.Multiple(() =>
            {
                Assert.That(((OSDArray)assetResources!["mesh_list"]), Has.Count.EqualTo(2), "one mesh_list entry per prim, no geometry dedup");
                Assert.That(((OSDArray)assetResources["texture_list"]), Has.Count.EqualTo(1), "both faces share the same ModelMaterial instance, so the texture is only embedded once");

                var instanceList = (OSDArray)assetResources["instance_list"];
                var face0 = (OSDMap)((OSDArray)((OSDMap)instanceList[0])["face_list"])[0];
                var face1 = (OSDMap)((OSDArray)((OSDMap)instanceList[1])["face_list"])[0];
                Assert.That(face0["image"].AsInteger(), Is.EqualTo(0));
                Assert.That(face1["image"].AsInteger(), Is.EqualTo(0));
            });
        }
    }
}
