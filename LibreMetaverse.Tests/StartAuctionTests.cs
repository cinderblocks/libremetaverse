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

using System.Net;
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for land auction initiation. There is no "ViewerStartAuction" capability -- it's a
    /// legacy UDP packet (AgentID/SessionID + parcel LocalID/SnapshotID), verified against the
    /// reference viewer's LLFloaterAuction::onClickStartAuction and the ViewerStartAuction entry in
    /// message_template.msg. An earlier version of ParcelManager.StartAuctionAsync instead POSTed a
    /// fictitious "parcel_id"/"snapshot_id"/"starting_bid" LLSD body to a non-existent capability,
    /// which meant the call always silently no-oped.
    /// </summary>
    [TestFixture]
    [Category("Parcel")]
    public class StartAuctionTests
    {
        [Test]
        public void ViewerStartAuctionPacket_RoundTripsAgentAndParcelFields()
        {
            var agentId = UUID.Random();
            var sessionId = UUID.Random();
            var snapshotId = UUID.Random();
            const int localId = 12345;

            var packet = new ViewerStartAuctionPacket
            {
                AgentData = { AgentID = agentId, SessionID = sessionId },
                ParcelData = { LocalID = localId, SnapshotID = snapshotId }
            };

            var bytes = packet.ToBytes();
            int end = bytes.Length - 1;
            var parsed = Packet.BuildPacket(bytes, ref end, null);

            Assert.That(parsed, Is.InstanceOf<ViewerStartAuctionPacket>());
            var roundTripped = (ViewerStartAuctionPacket)parsed;
            Assert.That(roundTripped.AgentData.AgentID, Is.EqualTo(agentId));
            Assert.That(roundTripped.AgentData.SessionID, Is.EqualTo(sessionId));
            Assert.That(roundTripped.ParcelData.LocalID, Is.EqualTo(localId));
            Assert.That(roundTripped.ParcelData.SnapshotID, Is.EqualTo(snapshotId));
        }

        [Test]
        public void StartAuction_DoesNotThrow()
        {
            var client = new GridClient();
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);

            Assert.DoesNotThrow(() => client.Parcels.StartAuction(sim, 1, UUID.Random()));
        }
    }
}
