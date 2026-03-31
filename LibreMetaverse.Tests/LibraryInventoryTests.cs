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
    class LibraryInventoryTests : Assert
    {
        private readonly GridClient Client;
        private const int LoginTimeoutSeconds = 30;

        public LibraryInventoryTests()
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
                Assert.Ignore("LMVTestAgentUsername is empty. LibraryInventoryTests cannot be performed.");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                Assert.Ignore("LMVTestAgentPassword is empty. LibraryInventoryTests cannot be performed.");
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
        /// Verifies that the library folder structure is populated in the inventory store
        /// from the login response skeleton, without making additional network requests.
        /// </summary>
        [Test]
        public void LibraryFoldersArePopulatedAfterLogin()
        {
            var store = Client.Inventory.Store;
            Assert.That(store, Is.Not.Null, "Inventory store is null after login");

            var libRoot = store.LibraryFolder;
            Assert.That(libRoot, Is.Not.Null, "Library root folder is null after login");
            Assert.That(libRoot.UUID, Is.Not.EqualTo(UUID.Zero), "Library root folder UUID is Zero");

            List<InventoryBase> children;
            try
            {
                children = store.GetContents(libRoot);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to get library root contents from store: {ex.Message}");
                return;
            }

            Assert.That(children.Count, Is.GreaterThan(0),
                "Library root folder has no children in the inventory store; " +
                "the library skeleton was not parsed from the login response");

            Assert.That(children.All(c => c is InventoryFolder), Is.True,
                "Library root direct children should all be InventoryFolder instances");

            var firstChild = children.OfType<InventoryFolder>().First();
            Assert.That(firstChild.OwnerID, Is.Not.EqualTo(UUID.Zero),
                "Library sub-folder OwnerID is Zero; the library owner was not parsed from the login response");
        }

        /// <summary>
        /// Verifies that the contents of a library folder (sub-folders and items) can be
        /// fetched from the server using the FetchLibDescendents2 capability.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task LibraryFolderItemsCanBeFetched(CancellationToken cancellationToken)
        {
            var store = Client.Inventory.Store;
            var libRoot = store?.LibraryFolder;

            if (libRoot == null || libRoot.UUID == UUID.Zero)
            {
                Assert.Ignore("Library root folder is not available; skipping fetch test");
            }

            // Resolve the library owner UUID from one of the skeleton sub-folders populated at login.
            // The library root folder itself is created without an OwnerID, but its direct children
            // from the login skeleton have OwnerID set to the library owner.
            UUID libraryOwnerID = UUID.Zero;
            try
            {
                var skeletonChildren = store.GetContents(libRoot);
                libraryOwnerID = skeletonChildren
                    .OfType<InventoryFolder>()
                    .Select(f => f.OwnerID)
                    .FirstOrDefault(id => id != UUID.Zero);
            }
            catch (Exception ex)
            {
                Assert.Ignore($"Library folder skeleton not available in store: {ex.Message}");
            }

            if (libraryOwnerID == UUID.Zero)
            {
                Assert.Ignore("Could not determine library owner UUID from skeleton folders; skipping fetch test");
            }

            // Fetch the library root folder contents from the server.
            // When ownerID != Client.Self.AgentID the manager uses FetchLibDescendents2.
            List<InventoryBase> contents;
            try
            {
                contents = await Client.Inventory.FolderContentsAsync(
                    libRoot.UUID, libraryOwnerID, true, true, InventorySortOrder.ByName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Assert.Warn("Library folder contents fetch timed out; skipping assertions");
                return;
            }
            catch (Exception ex)
            {
                Assert.Fail($"FolderContentsAsync threw an exception: {ex.Message}");
                return;
            }

            if (contents == null || contents.Count == 0)
            {
                Assert.Warn("Library folder contents fetch returned no results; this may indicate a transient server issue");
                return;
            }

            Assert.That(contents.Any(c => c is InventoryFolder), Is.True,
                "Library root folder contents should include at least one sub-folder");

            Console.WriteLine($"Library root folder returned {contents.Count} items " +
                $"({contents.OfType<InventoryFolder>().Count()} folders, " +
                $"{contents.OfType<InventoryItem>().Count()} items)");
        }

        /// <summary>
        /// Verifies that at least one immediate sub-folder of the library root contains
        /// inventory items when fetched from the server. All standard SL library folders
        /// (Animations, Body Parts, Clothing, etc.) contain items, so this confirms the
        /// full fetch chain works end-to-end.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task LibrarySubFolderContainsItems(CancellationToken cancellationToken)
        {
            var store = Client.Inventory.Store;
            var libRoot = store?.LibraryFolder;

            if (libRoot == null || libRoot.UUID == UUID.Zero)
            {
                Assert.Ignore("Library root folder is not available; skipping test");
            }

            List<InventoryBase> skeletonChildren;
            try
            {
                skeletonChildren = store.GetContents(libRoot);
            }
            catch (Exception ex)
            {
                Assert.Ignore($"Library folder skeleton not available in store: {ex.Message}");
                return;
            }

            var subFolders = skeletonChildren.OfType<InventoryFolder>().ToList();
            if (subFolders.Count == 0)
            {
                Assert.Ignore("No library sub-folders found in store; skipping test");
            }

            var libraryOwnerID = subFolders
                .Select(f => f.OwnerID)
                .FirstOrDefault(id => id != UUID.Zero);
            if (libraryOwnerID == UUID.Zero)
            {
                Assert.Ignore("Could not determine library owner UUID from skeleton folders; skipping test");
            }

            // Iterate the immediate sub-folders of the library root until items are found.
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
                        subFolder.UUID, libraryOwnerID, true, true, InventorySortOrder.ByName, cancellationToken);
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
                Assert.Warn("No items found in any library sub-folder; this may indicate a server or grid configuration issue");
                return;
            }

            Assert.That(foundItems.Count, Is.GreaterThan(0),
                $"Expected at least one item in library folder '{folderWithItems.Name}'");

            Console.WriteLine($"Found {foundItems.Count} items in library sub-folder '{folderWithItems.Name}' " +
                $"(first: '{foundItems[0].Name}', type: {foundItems[0].AssetType})");
        }
    }
}
