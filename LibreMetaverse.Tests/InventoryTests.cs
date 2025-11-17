/*
 * Copyright (c) 2025, Sjofn LLC
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

using NUnit.Framework;
using OpenMetaverse;
using System.IO;
using System.Threading.Tasks;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class InventoryTests
    {
        private GridClient client;
        private Inventory inventory;

        [SetUp]
        public void Setup()
        {
            client = new GridClient();
            inventory = new Inventory(client, UUID.Random());
        }

        [Test]
        public void AddAndGetItem()
        {
            var folder = new InventoryFolder(UUID.Random())
            {
                Name = "TestFolder",
                ParentUUID = UUID.Zero
            };
            inventory.UpdateNodeFor(folder);

            var item = new InventoryItem(UUID.Random()) { Name = "Item1", ParentUUID = folder.UUID };
            inventory.UpdateNodeFor(item);

            Assert.That(inventory.Contains(item.UUID), Is.True);
            Assert.That(inventory.TryGetValue(item.UUID, out InventoryBase baseItem), Is.True);
            Assert.That(baseItem, Is.InstanceOf<InventoryItem>());
            Assert.That(baseItem.Name, Is.EqualTo("Item1"));

            var contents = inventory.GetContents(folder);
            Assert.That(contents.Count, Is.EqualTo(1));
            Assert.That(contents[0].UUID, Is.EqualTo(item.UUID));
        }

        [Test]
        public void UpdateItemParent()
        {
            var folder1 = new InventoryFolder(UUID.Random()) { Name = "F1" };
            var folder2 = new InventoryFolder(UUID.Random()) { Name = "F2" };
            inventory.UpdateNodeFor(folder1);
            inventory.UpdateNodeFor(folder2);

            var item = new InventoryItem(UUID.Random()) { Name = "Item", ParentUUID = folder1.UUID };
            inventory.UpdateNodeFor(item);
            Assert.That(item.ParentUUID, Is.EqualTo(folder1.UUID));

            // Move to folder2
            item.ParentUUID = folder2.UUID;
            inventory.UpdateNodeFor(item);

            var contents1 = inventory.GetContents(folder1);
            var contents2 = inventory.GetContents(folder2);
            Assert.That(contents1.Exists(i => i.UUID == item.UUID), Is.False);
            Assert.That(contents2.Exists(i => i.UUID == item.UUID), Is.True);
        }

        [Test]
        public void RemoveItem()
        {
            var folder = new InventoryFolder(UUID.Random()) { Name = "Folder" };
            inventory.UpdateNodeFor(folder);

            var item = new InventoryItem(UUID.Random()) { Name = "ToRemove", ParentUUID = folder.UUID };
            inventory.UpdateNodeFor(item);
            Assert.That(inventory.Contains(item.UUID), Is.True);

            inventory.RemoveNodeFor(item);
            Assert.That(inventory.Contains(item.UUID), Is.False);
        }

        [Test]
        public async Task SaveAndRestoreAsync()
        {
            var folder = new InventoryFolder(UUID.Random()) { Name = "FolderSave" };
            inventory.UpdateNodeFor(folder);
            var item = new InventoryItem(UUID.Random()) { Name = "SaveItem", ParentUUID = folder.UUID };
            inventory.UpdateNodeFor(item);

            var tmp = Path.GetTempFileName();
            try
            {
                await inventory.SaveToDiskAsync(tmp);
                // Clear and restore
                inventory.Clear();
                Assert.That(inventory.Contains(item.UUID), Is.False);

                var count = await inventory.RestoreFromDiskAsync(tmp);
                Assert.That(count, Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Test]
        public void GetNodeOrDefault_ReturnsNullWhenMissing()
        {
            var node = inventory.GetNodeOrDefault(UUID.Random());
            Assert.That(node, Is.Null);
        }

        [Test]
        public void TryGetValue_Generic()
        {
            var folder = new InventoryFolder(UUID.Random()) { Name = "GenFolder" };
            inventory.UpdateNodeFor(folder);

            var item = new InventoryItem(UUID.Random()) { Name = "GenItem", ParentUUID = folder.UUID };
            inventory.UpdateNodeFor(item);

            Assert.That(inventory.TryGetValue<InventoryItem>(item.UUID, out var got), Is.True);
            Assert.That(got, Is.Not.Null);
            Assert.That(got.UUID, Is.EqualTo(item.UUID));
        }
    }
}
