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

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Appearance")]
    public class AvatarManagerTests
    {
        // ── RequestOwnAvatarTextures ───────────────────────────────────────────

        /// <summary>
        /// RequestOwnAvatarTextures constructs a GenericMessage packet and calls
        /// Network.SendPacket. When the client has never connected, SendPacket
        /// silently drops the packet; the method must not throw.
        /// </summary>
        [Test]
        public void RequestOwnAvatarTextures_WhenNotConnected_DoesNotThrow()
        {
            var client = new GridClient();
            Assert.That(() => client.Avatars.RequestOwnAvatarTextures(), Throws.Nothing);
        }

        // ── Stale-version guard via AppearanceManager.UpdateLastReceivedCOFVersion ──

        /// <summary>
        /// The stale-version guard should accept any COF version higher than the
        /// last received, reflecting the SL viewer's mLastUpdateReceivedCOFVersion logic.
        /// </summary>
        [Test]
        public void UpdateLastReceivedCOFVersion_AcceptsHigherVersion()
        {
            var client = new GridClient();
            client.Appearance.UpdateLastReceivedCOFVersion(3);
            client.Appearance.UpdateLastReceivedCOFVersion(8);
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(8));
        }

        /// <summary>
        /// The stale-version guard must reject out-of-order (lower) COF versions,
        /// matching the SL viewer's processAvatarAppearance drop condition.
        /// </summary>
        [Test]
        public void UpdateLastReceivedCOFVersion_RejectsLowerVersion()
        {
            var client = new GridClient();
            client.Appearance.UpdateLastReceivedCOFVersion(10);
            client.Appearance.UpdateLastReceivedCOFVersion(4);
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(10));
        }

        /// <summary>
        /// A COF version equal to the last received should not update the stored value,
        /// matching the SL viewer's "cofVersion &lt;= mLastUpdateReceivedCOFVersion" drop condition.
        /// </summary>
        [Test]
        public void UpdateLastReceivedCOFVersion_EqualVersion_IsNotAdvanced()
        {
            var client = new GridClient();
            client.Appearance.UpdateLastReceivedCOFVersion(6);
            client.Appearance.UpdateLastReceivedCOFVersion(6);
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(6));
        }
    }
}
