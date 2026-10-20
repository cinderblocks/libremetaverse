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
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using LibreMetaverse.ImportExport;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Making a new material in inventory, in the order the viewer's material editor does it: upload the textures, make an empty
    /// material item, set its permissions, upload the material to it. The grid's replies to the item messages (UDP) are stood in for,
    /// and the capability replies are queued, so the order of the steps and what each carries can be read back.
    /// </summary>
    [TestFixture]
    public class MaterialCreateTests
    {
        private const string TextureCap = "http://test.invalid/new-file-agent-inventory";
        private const string TextureUploader = "http://test.invalid/texture-uploader";
        private const string MaterialCap = "http://test.invalid/update-material-agent-inventory";
        private const string MaterialUploader = "http://test.invalid/material-uploader";

        private FakeGridClient _client = null!;
        private readonly UUID _folder = UUID.Random();
        private UUID _item;
        private readonly List<(UUID Folder, string Name, string Description, PermissionMask NextOwner, int RequestsSoFar)> _created =
            new List<(UUID, string, string, PermissionMask, int)>();
        private readonly List<InventoryItem> _updated = new List<InventoryItem>();
        private Func<InventoryItem?>? _result;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("NewFileAgentInventory", new Uri(TextureCap));
            _client.AddCapability("UpdateMaterialAgentInventory", new Uri(MaterialCap));
            _item = UUID.Random();
            _created.Clear();
            _updated.Clear();

            // The grid makes the item the way the create message asks: the owner has everything, the next owner what was said, the rest nothing
            _result = null;
            _client.Inventory.CreateMaterialItemHook = (folder, name, description, nextOwner, token) =>
            {
                _created.Add((folder, name, description, nextOwner, _client.CapturedRequests.Count));
                if (_result != null) return Task.FromResult(_result());
                var item = InventoryManager.CreateInventoryItem(InventoryType.Material, _item);
                item.Name = name;
                item.Description = description;
                item.ParentUUID = folder;
                item.Permissions = new Permissions((uint)PermissionMask.All, 0, 0, (uint)nextOwner, (uint)PermissionMask.All);
                return Task.FromResult<InventoryItem?>(item);
            };
            _client.Inventory.UpdateItemHook = item => _updated.Add(item);
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        private static readonly Permissions Shared = new Permissions((uint)PermissionMask.All, (uint)PermissionMask.Copy,
            (uint)PermissionMask.Transfer, (uint)(PermissionMask.Copy | PermissionMask.Modify), (uint)PermissionMask.All);

        private void MaterialAccepts(string assetId)
        {
            _client.AddHttpResponse(new Uri(MaterialCap), HttpStatusCode.OK, $"{{\"state\":\"upload\",\"uploader\":\"{MaterialUploader}\"}}");
            _client.AddHttpResponse(new Uri(MaterialUploader), HttpStatusCode.OK, $"{{\"state\":\"complete\",\"new_asset\":\"{assetId}\"}}");
        }

        // Textures take two requests each: the capability says where to upload, and the upload says what it made
        private void TexturesAccepted(params UUID[] assetIds)
        {
            _client.AddHttpResponse(new Uri(TextureCap), HttpStatusCode.OK, $"{{\"state\":\"upload\",\"uploader\":\"{TextureUploader}\"}}");
            _client.AddHttpResponseSequence(new Uri(TextureUploader), assetIds.Select(id => (HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{id}\"}}")).ToArray());
        }

        private static ModelTexture Texture(string name, bool loaded = true) =>
            new ModelTexture { Name = name, Data = loaded ? new byte[] { 1, 2, 3 } : Array.Empty<byte>(), Width = 64, Height = 64 };

        private static ModelPbrMaterial Paint(params (string Slot, ModelTexture Texture)[] textures)
        {
            var pbr = new ModelPbrMaterial { Name = "Paint", MetallicFactor = 0.25f, RoughnessFactor = 0.75f };
            foreach (var (slot, texture) in textures)
            {
                if (slot == "base") pbr.BaseColorTexture = texture;
                if (slot == "normal") pbr.NormalTexture = texture;
                if (slot == "mr") pbr.MetallicRoughnessTexture = texture;
                if (slot == "emissive") pbr.EmissiveTexture = texture;
            }
            return pbr;
        }

        // The material the grid was sent, from the binary LLSD it was wrapped in
        private string UploadedMaterialJson()
        {
            var body = _client.CapturedRequestBodies.Last();
            var envelope = (OSDMap)OSDParser.DeserializeLLSDBinary(body);
            return envelope["data"].AsString();
        }

        #region A material whose textures are already uploaded

        [Test]
        public async Task CreateMaterialAsync_MakesTheItemSetsItsPermissionsAndUploadsTheMaterial()
        {
            var asset = UUID.Random();
            MaterialAccepts(asset.ToString());

            var result = await _client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "a wall", Shared, _folder);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ItemID, Is.EqualTo(_item));
                Assert.That(result.AssetID, Is.EqualTo(asset));

                var made = _created.Single();
                Assert.That(made.Folder, Is.EqualTo(_folder));
                Assert.That((made.Name, made.Description), Is.EqualTo(("Wall", "a wall")));
                Assert.That(made.NextOwner, Is.EqualTo(Shared.NextOwnerMask), "the create message carries the next owner's permissions");

                // and the rest are set on the item, which the create message cannot do
                var item = _updated.Single();
                Assert.That(item.Permissions.OwnerMask, Is.EqualTo(PermissionMask.All));
                Assert.That(item.Permissions.BaseMask, Is.EqualTo(PermissionMask.All));
                Assert.That(item.Permissions.EveryoneMask, Is.EqualTo(Shared.EveryoneMask));
                Assert.That(item.Permissions.GroupMask, Is.EqualTo(Shared.GroupMask));
                Assert.That(item.Permissions.NextOwnerMask, Is.EqualTo(Shared.NextOwnerMask));

                // the material goes to the item that was made
                Assert.That(_client.CapturedRequests[0].Body, Does.Contain(_item.ToString()));
                Assert.That(_client.CapturedRequests[1].Uri, Is.EqualTo(new Uri(MaterialUploader)));
                Assert.That(_created.Single().RequestsSoFar, Is.EqualTo(0), "the item is made before anything is sent to the material capability");
            });
        }

        [Test]
        public async Task CreateMaterialAsync_PermissionsTheItemAlreadyHas_AreNotSetAgain()
        {
            MaterialAccepts(UUID.Random().ToString());
            var nothing = new Permissions((uint)PermissionMask.All, 0, 0, (uint)PermissionMask.All, (uint)PermissionMask.All);

            var result = await _client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "", nothing, _folder);

            Assert.That(result.Success, Is.True);
            Assert.That(_updated, Is.Empty);
        }

        [Test]
        public async Task CreateMaterialAsync_WithoutTheCapability_MakesNothing()
        {
            using var client = new FakeGridClient();
            var made = 0;
            client.Inventory.CreateMaterialItemHook = (f, n, d, p, t) => { made++; return Task.FromResult<InventoryItem?>(null); };

            var result = await client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "", Shared, _folder);

            Assert.That(result.Status, Is.EqualTo("capability_missing"));
            Assert.That(made, Is.EqualTo(0), "no empty item left behind");
        }

        [Test]
        public async Task CreateMaterialAsync_WithNoFolderToPutItIn_MakesNothing()
        {
            // a fresh client has no inventory, so no Materials folder to find
            var result = await _client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "", Shared);

            Assert.That(result.Status, Is.EqualTo("no_material_folder"));
            Assert.That(_created, Is.Empty);
        }

        [Test]
        public async Task CreateMaterialAsync_ItemTheGridRefuses_SendsNothing()
        {
            _result = () => null;

            var result = await _client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "", Shared, _folder);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo("item_not_created"));
            Assert.That(_client.CapturedRequests, Is.Empty);
        }

        [Test]
        public async Task CreateMaterialAsync_ItemTheGridNeverAnswersFor_TimesOut()
        {
            var original = InventoryManager.ItemCreateTimeout;
            try
            {
                InventoryManager.ItemCreateTimeout = TimeSpan.FromMilliseconds(100);
                _client.Inventory.CreateMaterialItemHook = async (f, n, d, p, token) =>
                {
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                    return null;
                };

                var result = await _client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "", Shared, _folder);

                Assert.That(result.Status, Is.EqualTo("item_create_timeout"));
                Assert.That(_client.CapturedRequests, Is.Empty);
            }
            finally
            {
                InventoryManager.ItemCreateTimeout = original;
            }
        }

        [Test]
        public async Task CreateMaterialAsync_Cancelled_Throws()
        {
            _client.Inventory.CreateMaterialItemHook = async (f, n, d, p, token) =>
            {
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                return null;
            };
            using var cancel = new CancellationTokenSource(50);

            await Assert.CatchAsync<OperationCanceledException>(() => _client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "", Shared, _folder, cancel.Token));
        }

        [Test]
        public async Task CreateMaterialAsync_UploadThatFails_SaysWhichItemIsLeft()
        {
            _client.AddHttpResponse(new Uri(MaterialCap), HttpStatusCode.OK, $"{{\"state\":\"upload\",\"uploader\":\"{MaterialUploader}\"}}");
            _client.AddHttpResponse(new Uri(MaterialUploader), HttpStatusCode.OK, "{\"state\":\"error\"}");

            var result = await _client.Inventory.CreateMaterialAsync(new AssetMaterial(), "Wall", "", Shared, _folder);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ItemID, Is.EqualTo(_item), "the empty item the caller may want to remove");
        }

        [Test]
        public async Task CreateMaterialAsync_WithNothingToMake_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _client.Inventory.CreateMaterialAsync((AssetMaterial)null!, "Wall", "", Shared, _folder));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _client.Inventory.CreateMaterialAsync((ModelPbrMaterial)null!, Shared));
        }

        #endregion A material whose textures are already uploaded

        #region A material read from a model

        [Test]
        public async Task CreateMaterialAsync_FromAModel_UploadsTheTexturesFirstAndPutsTheirIdsInTheMaterial()
        {
            var ids = new[] { UUID.Random(), UUID.Random(), UUID.Random() };
            TexturesAccepted(ids);
            MaterialAccepts(UUID.Random().ToString());
            var baseColor = Texture("base.png"); var normal = Texture("normal.png"); var mr = Texture("mr.png");

            var result = await _client.Inventory.CreateMaterialAsync(Paint(("base", baseColor), ("normal", normal), ("mr", mr)),
                Shared, folderID: _folder, textureFolderID: UUID.Random());

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Textures[baseColor], Is.EqualTo(ids[0]));
                Assert.That(result.Textures[normal], Is.EqualTo(ids[1]));
                Assert.That(result.Textures[mr], Is.EqualTo(ids[2]));

                // all six texture requests came before the item was made, and the item before the material
                Assert.That(_created.Single().RequestsSoFar, Is.EqualTo(6));
                Assert.That(_client.CapturedRequests.Count, Is.EqualTo(8));

                // and the material the grid got has the ids the textures were given, in the right slots
                var json = UploadedMaterialJson();
                Assert.That(json, Does.Contain(ids[0].ToString()));
                Assert.That(json, Does.Contain(ids[1].ToString()));
                Assert.That(json, Does.Contain(ids[2].ToString()));
                Assert.That(json.IndexOf(ids[0].ToString(), StringComparison.Ordinal), Is.LessThan(json.IndexOf(ids[1].ToString(), StringComparison.Ordinal)),
                    "base color before normal, as the material lists them");
            });
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_NamesTheItemAndItsTexturesAfterTheMaterial()
        {
            TexturesAccepted(UUID.Random());
            MaterialAccepts(UUID.Random().ToString());

            await _client.Inventory.CreateMaterialAsync(Paint(("normal", Texture("n.png"))), Shared, name: "Hull plating", folderID: _folder);

            var textureQuery = (OSDMap)OSDParser.Deserialize(_client.CapturedRequestBodies[0]);
            Assert.That(textureQuery["name"].AsString(), Is.EqualTo("Hull plating (Normal)"));
            Assert.That(textureQuery["asset_type"].AsString(), Is.EqualTo("texture"));
            Assert.That(_created.Single().Name, Is.EqualTo("Hull plating"));
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_UsesTheModelsNameIfNoneIsGiven()
        {
            MaterialAccepts(UUID.Random().ToString());

            await _client.Inventory.CreateMaterialAsync(Paint(), Shared, folderID: _folder);

            Assert.That(_created.Single().Name, Is.EqualTo("Paint"));
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_TextureNeverLoaded_IsLeftOutAndSaidSo()
        {
            TexturesAccepted(UUID.Random());
            MaterialAccepts(UUID.Random().ToString());
            var ghost = Texture("ghost.png", loaded: false);

            var result = await _client.Inventory.CreateMaterialAsync(Paint(("base", ghost), ("normal", Texture("n.png"))), Shared, folderID: _folder);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Textures.ContainsKey(ghost), Is.False);
            var warning = result.Diagnostics.Single();
            Assert.That(warning.Code, Is.EqualTo(ModelDiagnosticCode.TextureNotLoaded));
            Assert.That(warning.Subject, Is.EqualTo("ghost.png"));
            Assert.That(_client.CapturedRequests.Count(r => r.Uri == new Uri(TextureCap)), Is.EqualTo(1), "only the texture that has data was uploaded");
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_AskedFirstWhatTheTexturesCost()
        {
            TexturesAccepted(UUID.Random(), UUID.Random());
            MaterialAccepts(UUID.Random().ToString());
            int asked = -1;

            await _client.Inventory.CreateMaterialAsync(Paint(("base", Texture("a")), ("normal", Texture("b"))), Shared, folderID: _folder,
                confirmCost: cost => { asked = cost; Assert.That(_client.CapturedRequests, Is.Empty, "nothing is paid for yet"); return true; });

            Assert.That(asked, Is.EqualTo(2 * _client.Settings.UploadCost));
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_CostRefused_UploadsNothing()
        {
            TexturesAccepted(UUID.Random());

            var result = await _client.Inventory.CreateMaterialAsync(Paint(("base", Texture("a"))), Shared, folderID: _folder, confirmCost: _ => false);

            Assert.That(result.Status, Is.EqualTo("cost_rejected"));
            Assert.That(_client.CapturedRequests, Is.Empty);
            Assert.That(_created, Is.Empty);
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_WithoutTheTextureCapability_UploadsNothing()
        {
            using var client = new FakeGridClient();
            client.AddCapability("UpdateMaterialAgentInventory", new Uri(MaterialCap));
            client.Inventory.CreateMaterialItemHook = (f, n, d, p, t) => throw new InvalidOperationException("nothing should be made");

            var result = await client.Inventory.CreateMaterialAsync(Paint(("base", Texture("a"))), Shared, folderID: _folder);

            Assert.That(result.Status, Is.EqualTo("capability_missing"));
            Assert.That(client.CapturedRequests, Is.Empty);
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_TextureThatFails_StopsAndKeepsWhatWasUploaded()
        {
            var first = UUID.Random();
            _client.AddHttpResponse(new Uri(TextureCap), HttpStatusCode.OK, $"{{\"state\":\"upload\",\"uploader\":\"{TextureUploader}\"}}");
            _client.AddHttpResponseSequence(new Uri(TextureUploader),
                (HttpStatusCode.OK, $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{first}\"}}"),
                (HttpStatusCode.OK, "{\"state\":\"error\"}"));
            var a = Texture("a"); var b = Texture("b");

            var result = await _client.Inventory.CreateMaterialAsync(Paint(("base", a), ("normal", b)), Shared, folderID: _folder);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Does.StartWith("texture_upload_failed"));
            Assert.That(result.Textures[a], Is.EqualTo(first), "paid for, and in inventory");
            Assert.That(result.Textures.ContainsKey(b), Is.False);
            Assert.That(_created, Is.Empty, "no material item for a material that cannot be finished");
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_TexturesAlreadyUploaded_AreNotUploadedAgain()
        {
            var shared = Texture("shared.png");
            var known = UUID.Random();
            var uploaded = new Dictionary<ModelTexture, UUID> { [shared] = known };
            TexturesAccepted(UUID.Random());
            MaterialAccepts(UUID.Random().ToString());
            var own = Texture("own.png");

            var result = await _client.Inventory.CreateMaterialAsync(Paint(("base", shared), ("normal", own)), Shared, folderID: _folder, uploadedTextures: uploaded);

            Assert.That(result.Success, Is.True);
            Assert.That(_client.CapturedRequests.Count(r => r.Uri == new Uri(TextureCap)), Is.EqualTo(1), "just the one that was new");
            Assert.That(uploaded, Has.Count.EqualTo(2), "and it is recorded for the next material");
            Assert.That(UploadedMaterialJson(), Does.Contain(known.ToString()));
        }

        [Test]
        public async Task CreateMaterialAsync_FromAModel_OneTextureInTwoSlots_IsUploadedOnce()
        {
            var id = UUID.Random();
            TexturesAccepted(id);
            MaterialAccepts(UUID.Random().ToString());
            var both = Texture("both.png");

            await _client.Inventory.CreateMaterialAsync(Paint(("base", both), ("emissive", both)), Shared, folderID: _folder);

            Assert.That(_client.CapturedRequests.Count(r => r.Uri == new Uri(TextureCap)), Is.EqualTo(1));
            var json = UploadedMaterialJson();
            Assert.That(json.Split(new[] { id.ToString() }, StringSplitOptions.None).Length - 1, Is.GreaterThanOrEqualTo(2), "named in both slots");
        }

        #endregion A material read from a model
    }
}
