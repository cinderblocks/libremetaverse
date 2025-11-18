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
using System.Collections.Generic;

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
            Assert.That(contents1, Has.None.Matches<InventoryBase>(i => i.UUID == item.UUID));
            Assert.That(contents2, Has.Some.Matches<InventoryBase>(i => i.UUID == item.UUID));
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

        [Test]
        public void RemoveFolderRemovesAllDescendants()
        {
            var parent = new InventoryFolder(UUID.Random()) { Name = "Parent", ParentUUID = UUID.Zero };
            var childFolder = new InventoryFolder(UUID.Random()) { Name = "ChildFolder", ParentUUID = parent.UUID };
            var item = new InventoryItem(UUID.Random()) { Name = "NestedItem", ParentUUID = childFolder.UUID };

            inventory.UpdateNodeFor(parent);
            inventory.UpdateNodeFor(childFolder);
            inventory.UpdateNodeFor(item);

            Assert.That(inventory.Contains(parent.UUID), Is.True);
            Assert.That(inventory.Contains(childFolder.UUID), Is.True);
            Assert.That(inventory.Contains(item.UUID), Is.True);

            // Remove parent should remove child folder and item
            inventory.RemoveNodeFor(parent);

            Assert.That(inventory.Contains(parent.UUID), Is.False);
            Assert.That(inventory.Contains(childFolder.UUID), Is.False);
            Assert.That(inventory.Contains(item.UUID), Is.False);
        }

        [Test]
        public void DescendentCountUpdatesOnAddAndRemove()
        {
            var folder = new InventoryFolder(UUID.Random()) { Name = "Folder" };
            inventory.UpdateNodeFor(folder);

            var folderNode = inventory.GetNodeFor(folder.UUID);
            Assert.That(((InventoryFolder)folderNode.Data).DescendentCount, Is.Zero);

            var item = new InventoryItem(UUID.Random()) { Name = "Item", ParentUUID = folder.UUID };
            inventory.UpdateNodeFor(item);

            folderNode = inventory.GetNodeFor(folder.UUID);
            Assert.That(((InventoryFolder)folderNode.Data).DescendentCount, Is.GreaterThan(0));

            inventory.RemoveNodeFor(item);

            // After removal the folder's descendant count should be decreased
            folderNode = inventory.GetNodeFor(folder.UUID);
            Assert.That(((InventoryFolder)folderNode.Data).DescendentCount, Is.Zero);
        }

        [Test]
        public void MovingItemUpdatesAncestorDescendentCounts()
        {
            var f1 = new InventoryFolder(UUID.Random()) { Name = "F1" };
            var f2 = new InventoryFolder(UUID.Random()) { Name = "F2" };
            inventory.UpdateNodeFor(f1);
            inventory.UpdateNodeFor(f2);

            var item = new InventoryItem(UUID.Random()) { Name = "Movable", ParentUUID = f1.UUID };
            inventory.UpdateNodeFor(item);

            var n1 = (InventoryFolder)inventory.GetNodeFor(f1.UUID).Data;
            var n2 = (InventoryFolder)inventory.GetNodeFor(f2.UUID).Data;

            Assert.That(n1.DescendentCount, Is.GreaterThan(0));
            Assert.That(n2.DescendentCount, Is.Zero);

            // Move item to f2
            item.ParentUUID = f2.UUID;
            inventory.UpdateNodeFor(item);

            n1 = (InventoryFolder)inventory.GetNodeFor(f1.UUID).Data;
            n2 = (InventoryFolder)inventory.GetNodeFor(f2.UUID).Data;

            Assert.That(n1.DescendentCount, Is.Zero);
            Assert.That(n2.DescendentCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task ConcurrentAddMoveRemoveStress()
        {
            // Create three folders
            var f1 = new InventoryFolder(UUID.Random()) { Name = "Folder1", ParentUUID = UUID.Zero };
            var f2 = new InventoryFolder(UUID.Random()) { Name = "Folder2", ParentUUID = UUID.Zero };
            var f3 = new InventoryFolder(UUID.Random()) { Name = "Folder3", ParentUUID = UUID.Zero };

            inventory.UpdateNodeFor(f1);
            inventory.UpdateNodeFor(f2);
            inventory.UpdateNodeFor(f3);

            const int itemCount = 500;
            var items = new List<InventoryItem>(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                items.Add(new InventoryItem(UUID.Random()) { Name = $"Item{i}", ParentUUID = f1.UUID });
            }

            // Concurrently add all items
            var addTasks = new List<Task>(itemCount);
            foreach (var it in items)
            {
                addTasks.Add(Task.Run(() => inventory.UpdateNodeFor(it)));
            }
            await Task.WhenAll(addTasks);

            // Ensure all items exist
            foreach (var it in items)
            {
                Assert.That(inventory.Contains(it.UUID), Is.True, $"Missing item {it.UUID}");
            }

            // Concurrently move items: even to f2, odd to f3
            var moveTasks = new List<Task>(itemCount);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                moveTasks.Add(Task.Run(() =>
                {
                    it.ParentUUID = (i % 2 == 0) ? f2.UUID : f3.UUID;
                    inventory.UpdateNodeFor(it);
                }));
            }
            await Task.WhenAll(moveTasks);

            // Check descendant counts: f1 should be zero, f2 + f3 should equal itemCount
            var n1 = (InventoryFolder)inventory.GetNodeFor(f1.UUID).Data;
            var n2 = (InventoryFolder)inventory.GetNodeFor(f2.UUID).Data;
            var n3 = (InventoryFolder)inventory.GetNodeFor(f3.UUID).Data;

            Assert.That(n1.DescendentCount, Is.EqualTo(0));
            Assert.That(n2.DescendentCount + n3.DescendentCount, Is.EqualTo(itemCount));

            // Concurrently remove all items
            var removeTasks = new List<Task>(itemCount);
            foreach (var it in items)
            {
                removeTasks.Add(Task.Run(() => inventory.RemoveNodeFor(it)));
            }
            await Task.WhenAll(removeTasks);

            // Ensure none of the items exist
            foreach (var it in items)
            {
                Assert.That(inventory.Contains(it.UUID), Is.False, $"Item still present after removal: {it.UUID}");
            }

            // Descendant counts should be zero after removals
            n1 = (InventoryFolder)inventory.GetNodeFor(f1.UUID).Data;
            n2 = (InventoryFolder)inventory.GetNodeFor(f2.UUID).Data;
            n3 = (InventoryFolder)inventory.GetNodeFor(f3.UUID).Data;

            Assert.That(n1.DescendentCount, Is.EqualTo(0));
            Assert.That(n2.DescendentCount, Is.EqualTo(0));
            Assert.That(n3.DescendentCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ConcurrentLinkAddRemoveStress()
        {
            var folder = new InventoryFolder(UUID.Random()) { Name = "LinkFolder", ParentUUID = UUID.Zero };
            var folder2 = new InventoryFolder(UUID.Random()) { Name = "LinkFolder2", ParentUUID = UUID.Zero };
            inventory.UpdateNodeFor(folder);
            inventory.UpdateNodeFor(folder2);

            var assetId = UUID.Random();
            const int itemCount = 200;
            var items = new List<InventoryItem>(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                var it = new InventoryItem(UUID.Random()) { Name = $"L{i}", ParentUUID = folder.UUID };
                it.AssetType = AssetType.Link;
                it.AssetUUID = assetId;
                items.Add(it);
            }

            // Concurrently add all link items
            var addTasks = new List<Task>(itemCount);
            foreach (var it in items)
            {
                addTasks.Add(Task.Run(() => inventory.UpdateNodeFor(it)));
            }
            await Task.WhenAll(addTasks);

            var found = inventory.FindAllLinks(assetId);
            Assert.That(found.Count, Is.EqualTo(itemCount));

            // Concurrently move half to folder2
            var moveTasks = new List<Task>(itemCount);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                moveTasks.Add(Task.Run(() =>
                {
                    it.ParentUUID = (i % 2 == 0) ? folder2.UUID : folder.UUID;
                    inventory.UpdateNodeFor(it);
                }));
            }
            await Task.WhenAll(moveTasks);

            // Verify link index still returns all
            found = inventory.FindAllLinks(assetId);
            Assert.That(found.Count, Is.EqualTo(itemCount));

            // Concurrently remove all
            var removeTasks = new List<Task>(itemCount);
            foreach (var it in items)
            {
                removeTasks.Add(Task.Run(() => inventory.RemoveNodeFor(it)));
            }
            await Task.WhenAll(removeTasks);

            // Ensure no links remain
            found = inventory.FindAllLinks(assetId);
            Assert.That(found.Count, Is.EqualTo(0));

            // Ensure items removed from inventory
            foreach (var it in items)
            {
                Assert.That(inventory.Contains(it.UUID), Is.False);
            }
        }

        [Test]
        public void LinkIndexUpdateOnAssetChange()
        {
            var folder = new InventoryFolder(UUID.Random()) { Name = "LUpdate", ParentUUID = UUID.Zero };
            inventory.UpdateNodeFor(folder);

            var asset1 = UUID.Random();
            var asset2 = UUID.Random();

            var item = new InventoryItem(UUID.Random()) { Name = "LinkItem", ParentUUID = folder.UUID };
            item.AssetType = AssetType.Link;
            item.AssetUUID = asset1;

            inventory.UpdateNodeFor(item);

            var found1 = inventory.FindAllLinks(asset1);
            Assert.That(found1.Count, Is.EqualTo(1));
            Assert.That(found1[0].Data.UUID, Is.EqualTo(item.UUID));

            // Change the underlying asset the link points to
            item.AssetUUID = asset2;
            inventory.UpdateNodeFor(item);

            found1 = inventory.FindAllLinks(asset1);
            var found2 = inventory.FindAllLinks(asset2);

            Assert.That(found1.Count, Is.EqualTo(0));
            Assert.That(found2.Count, Is.EqualTo(1));
            Assert.That(found2[0].Data.UUID, Is.EqualTo(item.UUID));
        }
    }
}
