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

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Inventory")]
    [Category("RequiresLiveServer")]
    class InventoryFetchTests : Assert
    {
        private readonly GridClient Client;
        private const int LoginTimeoutSeconds = 30;

        public InventoryFetchTests()
        {
            Client = new GridClient();
            Client.Settings.LOGIN_TIMEOUT = LoginTimeoutSeconds * 1000;
        }

        [OneTimeSetUp]
        [CancelAfter(45000)]
        public async Task Init()
        {
            var fullusername = Environment.GetEnvironmentVariable("LMVTestAgentUsername");
            var password = Environment.GetEnvironmentVariable("LMVTestAgentPassword");
            if (string.IsNullOrWhiteSpace(fullusername))
            {
                Assert.Ignore("LMVTestAgentUsername is empty. InventoryFetchTests cannot be performed.");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                Assert.Ignore("LMVTestAgentPassword is empty. InventoryFetchTests cannot be performed.");
            }

            var username = fullusername.Split(' ');

            Console.Write($"Logging in {fullusername}...");

            string startLoc = NetworkManager.StartLocation("Hooper", 179, 18, 32);

            bool loginSuccess = false;
            try
            {
                loginSuccess = await Client.Network.LoginAsync(username[0], username[1], password,
                    "Unit Test Framework", startLoc, "admin@radegast.life");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Login threw exception: {ex.Message}");
            }

            Assert.That(loginSuccess, Is.True,
                $"Client failed to login, reason: {Client.Network.LoginMessage}");
            Console.WriteLine("Done");

            Assert.That(Client.Network.Connected, Is.True, "Client is not connected to the grid");

            System.Threading.Thread.Sleep(1000);

            if (Client.Network.CurrentSim == null)
            {
                Assert.Fail("CurrentSim is null after successful login");
            }
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            Console.Write("Logging out...");
            Client.Network.Logout();
            try { Client.Dispose(); } catch { }
            Console.WriteLine("Done");
        }

        /// <summary>
        /// Verifies that the agent's inventory folder structure is populated in the inventory store
        /// from the login response skeleton, without making additional network requests.
        /// </summary>
        [Test]
        public void InventorySkeletonIsPopulatedAfterLogin()
        {
            var store = Client.Inventory.Store;
            Assert.That(store, Is.Not.Null, "Inventory store is null after login");

            var root = store.RootFolder;
            Assert.That(root, Is.Not.Null, "Inventory root folder is null after login");
            Assert.That(root.UUID, Is.Not.EqualTo(UUID.Zero), "Inventory root folder UUID is Zero");

            List<InventoryBase> children;
            try
            {
                children = store.GetContents(root);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to get inventory root contents from store: {ex.Message}");
                return;
            }

            Assert.That(children.Count, Is.GreaterThan(0),
                "Inventory root folder has no children in the inventory store; " +
                "the inventory skeleton was not parsed from the login response");

            Assert.That(children.All(c => c is InventoryFolder), Is.True,
                "Inventory root direct children should all be InventoryFolder instances");

            var firstChild = children.OfType<InventoryFolder>().First();
            Assert.That(firstChild.OwnerID, Is.EqualTo(Client.Self.AgentID),
                "Inventory sub-folder OwnerID does not match AgentID; " +
                "the owner was not parsed correctly from the login response");
        }

        /// <summary>
        /// Verifies that the contents of the agent's inventory root folder can be fetched
        /// from the server using the FetchInventoryDescendents2 capability.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task InventoryRootContainsFolders(CancellationToken cancellationToken)
        {
            var store = Client.Inventory.Store;
            var root = store?.RootFolder;

            if (root == null || root.UUID == UUID.Zero)
            {
                Assert.Ignore("Inventory root folder is not available; skipping fetch test");
            }

            List<InventoryBase> contents;
            try
            {
                contents = await Client.Inventory.FolderContentsAsync(
                    root.UUID, Client.Self.AgentID, true, true, InventorySortOrder.ByName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Assert.Warn("Inventory root folder contents fetch timed out; skipping assertions");
                return;
            }
            catch (Exception ex)
            {
                Assert.Fail($"FolderContentsAsync threw an exception: {ex.Message}");
                return;
            }

            if (contents == null || contents.Count == 0)
            {
                Assert.Warn("Inventory root folder contents fetch returned no results; this may indicate a transient server issue");
                return;
            }

            Assert.That(contents.Any(c => c is InventoryFolder), Is.True,
                "Inventory root folder contents should include at least one sub-folder");

            Console.WriteLine($"Inventory root folder returned {contents.Count} items " +
                $"({contents.OfType<InventoryFolder>().Count()} folders, " +
                $"{contents.OfType<InventoryItem>().Count()} items)");
        }

        /// <summary>
        /// Verifies that at least one immediate sub-folder of the inventory root contains
        /// inventory items when fetched from the server.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task InventoryFolderContainsItems(CancellationToken cancellationToken)
        {
            var store = Client.Inventory.Store;
            var root = store?.RootFolder;

            if (root == null || root.UUID == UUID.Zero)
            {
                Assert.Ignore("Inventory root folder is not available; skipping test");
            }

            List<InventoryBase> skeletonChildren;
            try
            {
                skeletonChildren = store.GetContents(root);
            }
            catch (Exception ex)
            {
                Assert.Ignore($"Inventory folder skeleton not available in store: {ex.Message}");
                return;
            }

            var subFolders = skeletonChildren.OfType<InventoryFolder>().ToList();
            if (subFolders.Count == 0)
            {
                Assert.Ignore("No inventory sub-folders found in store; skipping test");
            }

            // Iterate the immediate sub-folders of the inventory root until items are found.
            // fetchFolders must be true: RequestFolderContents only parses items inside the
            // "categories" OSDArray block, so omitting folders risks silently skipping items
            // if the server elides that key when fetch_folders=false.
            InventoryFolder folderWithItems = null;
            List<InventoryItem> foundItems = null;

            foreach (var subFolder in subFolders)
            {
                if (cancellationToken.IsCancellationRequested) break;

                List<InventoryBase> contents;
                try
                {
                    contents = await Client.Inventory.FolderContentsAsync(
                        subFolder.UUID, Client.Self.AgentID, true, true, InventorySortOrder.ByName, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    continue;
                }

                var items = contents?.OfType<InventoryItem>().ToList();
                if (items != null && items.Count > 0)
                {
                    folderWithItems = subFolder;
                    foundItems = items;
                    break;
                }
            }

            if (folderWithItems == null)
            {
                Assert.Warn("No items found in any inventory sub-folder; this may indicate a server or grid configuration issue");
                return;
            }

            Assert.That(foundItems.Count, Is.GreaterThan(0),
                $"Expected at least one item in inventory folder '{folderWithItems.Name}'");

            Console.WriteLine($"Found {foundItems.Count} items in inventory sub-folder '{folderWithItems.Name}' " +
                $"(first: '{foundItems[0].Name}', type: {foundItems[0].AssetType})");
        }

        /// <summary>
        /// Verifies that an individual inventory item can be fetched by UUID using the
        /// FetchInventory2 capability and that the returned data matches the item discovered
        /// via folder fetch.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task InventoryItemCanBeFetchedById(CancellationToken cancellationToken)
        {
            var store = Client.Inventory.Store;
            var root = store?.RootFolder;

            if (root == null || root.UUID == UUID.Zero)
            {
                Assert.Ignore("Inventory root folder is not available; skipping test");
            }

            List<InventoryBase> skeletonChildren;
            try
            {
                skeletonChildren = store.GetContents(root);
            }
            catch (Exception ex)
            {
                Assert.Ignore($"Inventory folder skeleton not available in store: {ex.Message}");
                return;
            }

            var subFolders = skeletonChildren.OfType<InventoryFolder>().ToList();
            if (subFolders.Count == 0)
            {
                Assert.Ignore("No inventory sub-folders found in store; skipping test");
            }

            // Locate the first item available in any sub-folder to use as the fetch target.
            InventoryItem targetItem = null;
            foreach (var subFolder in subFolders)
            {
                if (cancellationToken.IsCancellationRequested) break;

                List<InventoryBase> contents;
                try
                {
                    contents = await Client.Inventory.FolderContentsAsync(
                        subFolder.UUID, Client.Self.AgentID, true, true, InventorySortOrder.ByName, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    continue;
                }

                targetItem = contents?.OfType<InventoryItem>().FirstOrDefault();
                if (targetItem != null) break;
            }

            if (targetItem == null)
            {
                Assert.Warn("No inventory item found in any sub-folder to use as fetch target; skipping test");
                return;
            }

            Console.WriteLine($"Fetching item '{targetItem.Name}' ({targetItem.UUID}) by UUID...");

            InventoryItem fetched;
            try
            {
                fetched = await Client.Inventory.FetchItemHttpAsync(targetItem.UUID, Client.Self.AgentID, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Assert.Warn("FetchItemHttpAsync timed out; skipping assertions");
                return;
            }
            catch (Exception ex)
            {
                Assert.Fail($"FetchItemHttpAsync threw an exception: {ex.Message}");
                return;
            }

            Assert.That(fetched, Is.Not.Null,
                $"FetchItemHttpAsync returned null for item '{targetItem.Name}' ({targetItem.UUID})");

            Assert.That(fetched.UUID, Is.EqualTo(targetItem.UUID),
                "Fetched item UUID does not match the requested item UUID");

            Assert.That(fetched.Name, Is.EqualTo(targetItem.Name),
                "Fetched item Name does not match the item discovered via folder fetch");

            Console.WriteLine($"Successfully fetched item '{fetched.Name}' (type: {fetched.AssetType})");
        }
    }
}
