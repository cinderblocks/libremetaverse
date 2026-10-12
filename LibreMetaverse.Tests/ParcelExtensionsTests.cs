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
using System.Net.Sockets;
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// What <see cref="ParcelExtensions"/> puts on the wire when a <see cref="Parcel"/> is sent to a simulator. A parcel is
    /// plain data in <c>LibreMetaverse.Protocol</c>; sending it is the client's part. Without a ParcelPropertiesUpdate
    /// capability the update goes by UDP.
    /// </summary>
    [TestFixture]
    [Category("Parcel")]
    public class ParcelExtensionsTests
    {
        private sealed class Wire : IDisposable
        {
            public readonly GridClient Client = new GridClient();
            public readonly UdpClient FarEnd = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            public readonly Simulator Sim;

            public Wire()
            {
                Client.Settings.Packets.ThrottleOutgoing = false;
                Sim = new Simulator(Client, (IPEndPoint)FarEnd.Client.LocalEndPoint!, 0);
                Sim.Start();
            }

            /// <summary>The packets that arrive at the far end, in order, until none comes for a moment.</summary>
            public List<Packet> Received(int expected)
            {
                var packets = new List<Packet>();
                FarEnd.Client.ReceiveTimeout = 2000;
                var from = new IPEndPoint(IPAddress.Any, 0);
                while (packets.Count < expected)
                {
                    byte[] raw = FarEnd.Receive(ref from);
                    int length = raw.Length - 1;
                    packets.Add(Packet.BuildPacket(raw, ref length, new byte[raw.Length * 4]));
                }
                return packets;
            }

            public void Dispose()
            {
                Sim.Stop();
                FarEnd.Dispose();
            }
        }

        private static Parcel SomeParcel() => new Parcel(42)
        {
            Name = "Meadow",
            Desc = "A field",
            MusicURL = "http://music.example/stream",
            Flags = ParcelFlags.AllowFly | ParcelFlags.ForSale,
            SalePrice = 1500,
            PassPrice = 10,
            PassHours = 2.5f,
            Category = ParcelCategory.Park,
            Landing = LandingType.LandingPoint,
            GroupID = new UUID("11111111-2222-3333-4444-555555555555"),
            AuthBuyerID = new UUID("66666666-7777-8888-9999-000000000000"),
            SnapshotID = new UUID("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            UserLocation = new Vector3(1, 2, 3),
            UserLookAt = new Vector3(4, 5, 6),
            OtherCleanTime = 90,
            Media = new ParcelMedia { MediaAutoScale = true, MediaID = new UUID("12345678-1234-1234-1234-123456789012"), MediaURL = "http://tv.example/" }
        };

        [Test]
        public void Update_WithoutTheCapability_SendsTheParcelByUdp_ThenItsAutoReturnTime()
        {
            using var wire = new Wire();
            var parcel = SomeParcel();

            parcel.Update(wire.Client, wire.Sim, wantReply: true);
            var sent = wire.Received(2);

            var update = sent[0] as ParcelPropertiesUpdatePacket;
            Assert.That(update, Is.Not.Null, "the first packet is the update");
            var data = update!.ParcelData;
            Assert.Multiple(() =>
            {
                Assert.That(data.LocalID, Is.EqualTo(42));
                Assert.That(Utils.BytesToString(data.Name), Is.EqualTo("Meadow"));
                Assert.That(Utils.BytesToString(data.Desc), Is.EqualTo("A field"));
                Assert.That(Utils.BytesToString(data.MusicURL), Is.EqualTo("http://music.example/stream"));
                Assert.That(data.ParcelFlags, Is.EqualTo((uint)(ParcelFlags.AllowFly | ParcelFlags.ForSale)));
                Assert.That(data.SalePrice, Is.EqualTo(1500));
                Assert.That(data.PassPrice, Is.EqualTo(10));
                Assert.That(data.PassHours, Is.EqualTo(2.5f));
                Assert.That(data.Category, Is.EqualTo((byte)ParcelCategory.Park));
                Assert.That(data.LandingType, Is.EqualTo((byte)LandingType.LandingPoint));
                Assert.That(data.GroupID, Is.EqualTo(parcel.GroupID));
                Assert.That(data.AuthBuyerID, Is.EqualTo(parcel.AuthBuyerID));
                Assert.That(data.SnapshotID, Is.EqualTo(parcel.SnapshotID));
                Assert.That(data.UserLocation, Is.EqualTo(new Vector3(1, 2, 3)));
                Assert.That(data.UserLookAt, Is.EqualTo(new Vector3(4, 5, 6)));
                Assert.That(data.MediaAutoScale, Is.EqualTo((byte)1));
                Assert.That(data.MediaID, Is.EqualTo(parcel.Media.MediaID));
                Assert.That(Utils.BytesToString(data.MediaURL), Is.EqualTo("http://tv.example/"));
                Assert.That(data.Flags, Is.EqualTo((byte)1), "a reply was asked for");
            });

            var clean = sent[1] as ParcelSetOtherCleanTimePacket;
            Assert.That(clean, Is.Not.Null, "the second packet is the auto-return time");
            Assert.That(clean!.ParcelData.LocalID, Is.EqualTo(42));
            Assert.That(clean.ParcelData.OtherCleanTime, Is.EqualTo(90));
        }

        [Test]
        public void Update_NotAskingForAReply_LeavesTheReplyFlagClear()
        {
            using var wire = new Wire();

            SomeParcel().Update(wire.Client, wire.Sim, wantReply: false);

            var update = (ParcelPropertiesUpdatePacket)wire.Received(2)[0];
            Assert.That(update.ParcelData.Flags, Is.EqualTo((byte)0));
        }

        [Test]
        public void UpdateOtherCleanTime_SendsOnlyTheAutoReturnTime()
        {
            using var wire = new Wire();
            var parcel = new Parcel(7) { OtherCleanTime = 15 };

            parcel.UpdateOtherCleanTime(wire.Sim);

            var clean = (ParcelSetOtherCleanTimePacket)wire.Received(1)[0];
            Assert.That(clean.ParcelData.LocalID, Is.EqualTo(7));
            Assert.That(clean.ParcelData.OtherCleanTime, Is.EqualTo(15));
        }

        [Test]
        public void TheSimulatorIsRequired()
        {
            var client = new GridClient();
            var parcel = new Parcel(1);

            Assert.Throws<ArgumentNullException>(() => parcel.Update(client, null!, false));
            Assert.Throws<ArgumentNullException>(() => parcel.UpdateOtherCleanTime(null!));
        }
    }
}
