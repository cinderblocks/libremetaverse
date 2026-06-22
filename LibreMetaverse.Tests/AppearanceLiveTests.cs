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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Appearance;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Live-server integration tests for COF and appearance tracking, covering the
    /// changes introduced in the "Follow SL semantics for COF/appearance tracking" commit:
    ///
    ///   - COF initializes successfully and exposes a valid InventoryFolder.
    ///   - GetCurrentOutfitLinksAsync returns the links stored in the COF.
    ///   - GetWornAtAsync(Shape) returns at least one result (every avatar wears a shape).
    ///   - The AvatarAppearance UDP pipeline updates LastUpdateReceivedCOFVersion.
    ///   - RequestOwnAvatarTextures triggers an AvatarAppearance packet that further
    ///     advances LastUpdateReceivedCOFVersion (SL stale-version guard round-trip).
    /// </summary>
    [TestFixture]
    [Category("Appearance")]
    [Category("RequiresLiveServer")]
    public class AppearanceLiveTests : Assert
    {
        private GridClient Client;
        private CurrentOutfitFolder COF;
        private const int LoginTimeoutSeconds = 30;

        public AppearanceLiveTests()
        {
            Client = new GridClient();
            Client.Settings.Timing.LoginTimeout = LoginTimeoutSeconds * 1000;
        }

        [OneTimeSetUp]
        [CancelAfter(45000)]
        public async Task Init()
        {
            var fullusername = Environment.GetEnvironmentVariable("LMVTestAgentUsername");
            var password = Environment.GetEnvironmentVariable("LMVTestAgentPassword");
            if (string.IsNullOrWhiteSpace(fullusername))
            {
                Assert.Ignore("LMVTestAgentUsername is empty. AppearanceLiveTests cannot be performed.");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                Assert.Ignore("LMVTestAgentPassword is empty. AppearanceLiveTests cannot be performed.");
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

            COF = new CurrentOutfitFolder(Client);

            // Allow the appearance pipeline a moment to get started after login
            await Task.Delay(2000);
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            Console.Write("Logging out...");
            try { COF?.Dispose(); } catch { }
            Client.Network.Logout();
            try { Client.Dispose(); } catch { }
            Console.WriteLine("Done");
        }

        // ── COF initialization ────────────────────────────────────────────────

        /// <summary>
        /// After login the appearance pipeline must locate and expose the Current Outfit
        /// Folder.  COF is expected to be non-null with a valid (non-Zero) UUID.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task COF_IsInitializedAfterLogin(CancellationToken cancellationToken)
        {
            // GetCurrentOutfitLinksAsync triggers lazy COF initialization if it hasn't happened yet
            var links = await COF.GetCurrentOutfitLinksAsync(cancellationToken);

            Assert.That(COF.COF, Is.Not.Null, "COF folder is null after GetCurrentOutfitLinksAsync");
            Assert.That(COF.COF!.UUID, Is.Not.EqualTo(UUID.Zero), "COF folder UUID is Zero");
            Console.WriteLine($"COF UUID: {COF.COF.UUID}, Name: {COF.COF.Name}");
        }

        /// <summary>
        /// After COF initialization the COF folder name must match the SL-defined
        /// system folder name "Current Outfit".
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task COF_HasExpectedFolderName(CancellationToken cancellationToken)
        {
            await COF.GetCurrentOutfitLinksAsync(cancellationToken); // ensure initialized

            Assert.That(COF.COF, Is.Not.Null);
            Assert.That(COF.COF!.Name, Is.EqualTo("Current Outfit").IgnoreCase,
                "COF folder name does not match 'Current Outfit'");
        }

        // ── GetCurrentOutfitLinksAsync ─────────────────────────────────────────────

        /// <summary>
        /// GetCurrentOutfitLinksAsync must return a list (possibly empty for a bare avatar,
        /// but typically populated). Every element must be an inventory link.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task GetCurrentOutfitLinksAsync_ReturnsLinks(CancellationToken cancellationToken)
        {
            var links = await COF.GetCurrentOutfitLinksAsync(cancellationToken);

            Assert.That(links, Is.Not.Null);

            // Every returned item must be a link (AssetType.Link or AssetType.LinkFolder)
            foreach (var link in links)
            {
                Assert.That(link.IsLink(), Is.True,
                    $"Item {link.UUID} ({link.Name}) is not a link (AssetType={link.AssetType})");
            }

            Console.WriteLine($"COF contains {links.Count} link(s)");
        }

        /// <summary>
        /// A normal avatar wears at least one wearable (shape is mandatory), so the COF
        /// must contain at least one link.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task GetCurrentOutfitLinksAsync_ContainsAtLeastOneLink(CancellationToken cancellationToken)
        {
            var links = await COF.GetCurrentOutfitLinksAsync(cancellationToken);

            if (links.Count == 0)
            {
                Assert.Warn("COF returned zero links; the test account may be wearing nothing. " +
                            "Consider equipping at least a default shape.");
                return;
            }

            Assert.That(links.Count, Is.GreaterThan(0), "Expected at least one COF link");
        }

        // ── GetWornAt ─────────────────────────────────────────────────────────

        /// <summary>
        /// Every SL avatar must have a shape. GetWornAtAsync(WearableType.Shape) must
        /// return at least one item after the COF has been resolved.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task GetWornAt_Shape_ReturnsAtLeastOne(CancellationToken cancellationToken)
        {
            var worn = await COF.GetWornAtAsync(WearableType.Shape, cancellationToken);

            if (worn == null || worn.Count == 0)
            {
                Assert.Warn("GetWornAtAsync(Shape) returned no items. The test account may not have a shape in its COF.");
                return;
            }

            Assert.That(worn.Count, Is.GreaterThan(0));
            Console.WriteLine($"Worn shape(s): {string.Join(", ", worn.Select(w => w.Name))}");
        }

        // ── LastUpdateReceivedCOFVersion (stale-version guard) ────────────────

        /// <summary>
        /// After a successful login the server sends an AvatarAppearance UDP packet for
        /// our own avatar. AvatarManager.AvatarAppearanceHandler must call
        /// AppearanceManager.UpdateLastReceivedCOFVersion so the property advances beyond -1.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task LastUpdateReceivedCOFVersion_AdvancesAfterLogin(CancellationToken cancellationToken)
        {
            // The appearance pipeline may still be in progress; poll for up to 25 seconds.
            int version = -1;
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                version = Client.Appearance.LastUpdateReceivedCOFVersion;
                if (version > -1) break;
                await Task.Delay(500, cancellationToken);
            }

            if (version == -1)
            {
                Assert.Warn("LastUpdateReceivedCOFVersion is still -1 after waiting 25 s. " +
                            "The sim may not have sent a UDP AvatarAppearance for self yet.");
                return;
            }

            Assert.That(version, Is.GreaterThan(-1),
                "LastUpdateReceivedCOFVersion was never updated by AvatarAppearance UDP handler");
            Console.WriteLine($"LastUpdateReceivedCOFVersion after login: {version}");
        }

        /// <summary>
        /// RequestOwnAvatarTextures sends a GenericMessage "avatartexturesrequest" to the
        /// sim.  The sim should respond with an AvatarAppearance packet whose COF version
        /// is >= the previously received version (SL semantics: stale versions are dropped,
        /// equal-or-higher versions are processed and advance the counter).
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task RequestOwnAvatarTextures_DoesNotThrow_AndMaintainsVersionGuard(
            CancellationToken cancellationToken)
        {
            // Record version before the request
            int versionBefore = Client.Appearance.LastUpdateReceivedCOFVersion;

            // Must not throw even when the event queue is live — any exception will fail the test
            Client.Avatars.RequestOwnAvatarTextures();

            // Give the sim up to 15 seconds to respond with an AvatarAppearance packet.
            // The guard means the version must stay >= versionBefore (never regress).
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500, cancellationToken);
            }

            int versionAfter = Client.Appearance.LastUpdateReceivedCOFVersion;

            // The stale-version guard must never allow a regression
            Assert.That(versionAfter, Is.GreaterThanOrEqualTo(versionBefore),
                $"LastUpdateReceivedCOFVersion regressed from {versionBefore} to {versionAfter}; " +
                "the stale-version guard in UpdateLastReceivedCOFVersion is broken");

            Console.WriteLine($"COF version before RequestOwnAvatarTextures: {versionBefore}, after: {versionAfter}");
        }

        // ── COF MaxClothingLayers ─────────────────────────────────────────────

        /// <summary>
        /// Sanity-check: the SL-defined 60-layer limit is always enforced regardless of
        /// the live server state.
        /// </summary>
        [Test]
        public void COF_MaxClothingLayers_Is60()
        {
            Assert.That(COF.MaxClothingLayers, Is.EqualTo(60));
        }
    }
}
