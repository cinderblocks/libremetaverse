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
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the CreateTaskInventoryItem capability. Verified against the reference viewer
    /// (LLViewerObject::createInventoryItem / createInventoryItemCoro in llviewerobject.cpp): POST
    /// {object_id, asset_type, inventory_type, sub_type, name, description,
    /// permissions:{base,owner,everyone,group,next_owner}} to the capability; the response is a
    /// bare {"success":true/false}, with the new item itself arriving later via a normal
    /// task-inventory refresh rather than in this response.
    /// </summary>
    [TestFixture]
    public class CreateTaskInventoryItemTests
    {
        private const string CapUrl = "http://test.invalid/create-task-inventory-item";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("CreateTaskInventoryItem", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task CreateTaskInventoryItemAsync_Success_PostsExpectedShapeAndReturnsTrue()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"success\":true}", "application/json");

            var objectId = UUID.Random();
            var permissions = new Permissions((uint)PermissionMask.All, 0, 0, (uint)PermissionMask.All, (uint)PermissionMask.All);

            var result = await _client.Inventory.CreateTaskInventoryItemAsync(objectId, AssetType.LSLText,
                InventoryType.LSL, (byte)ScriptSubtype.Lua, "New Script", "a script", permissions);

            Assert.That(result, Is.True);
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1));

            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain($"<key>object_id</key>"));
            Assert.That(body, Does.Contain("<key>asset_type</key>"));
            Assert.That(body, Does.Contain("<key>inventory_type</key>"));
            Assert.That(body, Does.Contain("<key>sub_type</key>"));
            Assert.That(body, Does.Contain("<key>name</key>"));
            Assert.That(body, Does.Contain("<key>description</key>"));
            Assert.That(body, Does.Contain("<key>permissions</key>"));
            Assert.That(body, Does.Contain("<key>base</key>"));
            Assert.That(body, Does.Contain("<key>owner</key>"));
            Assert.That(body, Does.Contain("<key>everyone</key>"));
            Assert.That(body, Does.Contain("<key>group</key>"));
            Assert.That(body, Does.Contain("<key>next_owner</key>"));
            Assert.That(body, Does.Contain("New Script"));
        }

        [Test]
        public async Task CreateTaskInventoryItemAsync_ServerReportsFailure_ReturnsFalse()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                "{\"success\":false,\"message\":\"no room\"}", "application/json");

            var result = await _client.Inventory.CreateTaskInventoryItemAsync(UUID.Random(), AssetType.Notecard,
                InventoryType.Notecard, 0, "New Note", string.Empty, Permissions.NoPermissions);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task CreateTaskInventoryItemAsync_NonSuccessStatus_ReturnsFalse()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.InternalServerError, string.Empty);

            var result = await _client.Inventory.CreateTaskInventoryItemAsync(UUID.Random(), AssetType.Notecard,
                InventoryType.Notecard, 0, "New Note", string.Empty, Permissions.NoPermissions);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task CreateTaskInventoryItemAsync_NoCapability_ReturnsFalseWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                var result = await client.Inventory.CreateTaskInventoryItemAsync(UUID.Random(), AssetType.Notecard,
                    InventoryType.Notecard, 0, "New Note", string.Empty, Permissions.NoPermissions);

                Assert.That(result, Is.False);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }
    }
}
