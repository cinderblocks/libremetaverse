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
using System.Linq;
using LibreMetaverse.Packets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;
using Block = LibreMetaverse.Packets.RezMultipleAttachmentsFromInvPacket.ObjectDataBlock;

namespace LibreMetaverse.Tests
{
    /// <summary>The blocks the outfit send puts in RezMultipleAttachmentsFromInv, and the options that shape them</summary>
    [TestFixture]
    public class OutfitSendOptionsTests
    {
        private static readonly UUID Body = new UUID("00000000-0000-0000-0000-000000000b0d");
        private static readonly UUID Head = new UUID("00000000-0000-0000-0000-0000000000ed");
        private static readonly UUID Hair = new UUID("00000000-0000-0000-0000-000000000a12");
        private static readonly UUID Hud = new UUID("00000000-0000-0000-0000-000000000d0d");

        private FakeGridClient _client = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("Placeholder", new Uri("http://test.invalid/placeholder"));
            _client.Self.localID = 500;
        }

        [TearDown]
        public void TearDown() => _client.Dispose();

        private static Block B(UUID item, string name) => new Block { ItemID = item, Name = Utils.StringToBytes(name) };

        private static List<Block> Outfit() => new List<Block>
        {
            B(Hair, "Hair"), B(Head, "Head"), B(Hair, "Hair"), B(Body, "Body"), B(Hud, "HUD"),
        };

        private void Attach(uint localId, uint parentId, UUID item)
        {
            _client.Network.CurrentSim.ObjectsPrimitives[localId] = new Primitive
            {
                LocalID = localId, ParentID = parentId, ID = UUID.Random(),
                NameValues = new[] { new NameValue("AttachItemID", NameValue.ValueType.String,
                    NameValue.ClassType.ReadOnly, NameValue.SendtoType.SimViewer, item.ToString()) },
            };
        }

        private List<UUID> Prepare() => _client.Appearance.PrepareOutfitBlocks(Outfit()).Select(b => b.ItemID).ToList();

        [Test]
        public void Defaults_KeepTheUpstreamSend()
        {
            var agent = new Settings(_client).Agent;
            Assert.That(agent.SendOutfitAfterBake, Is.True);
            Assert.That(agent.OutfitSendDetachAll, Is.True);
            Assert.That(agent.OutfitSendExclude, Is.Null);
            Assert.That(agent.OutfitSendRank, Is.Null);
        }

        [Test]
        public void ByDefault_EachItemIsSentOnceInFolderOrder()
        {
            Attach(600, 500, Head);   // with detach-all, already attached objects are sent too
            Assert.That(Prepare(), Is.EqualTo(new[] { Hair, Head, Body, Hud }));
        }

        [Test]
        public void Exclude_LeavesThoseItemsOut()
        {
            UUID? seen = null;
            _client.Settings.Agent.OutfitSendExclude = (id, name) => { if (name == "HUD") seen = id; return name == "HUD"; };
            Assert.That(Prepare(), Is.EqualTo(new[] { Hair, Head, Body }));
            Assert.That(seen, Is.EqualTo(Hud));
        }

        [Test]
        public void Rank_OrdersByRankAndKeepsFolderOrderForEqualRanks()
        {
            _client.Settings.Agent.OutfitSendRank = name => name == "Body" ? 0 : name == "Head" ? 1 : 5;
            Assert.That(Prepare(), Is.EqualTo(new[] { Body, Head, Hair, Hud }));
        }

        [Test]
        public void WithoutDetachAll_ObjectsAlreadyAttachedToUsAreSkipped()
        {
            _client.Settings.Agent.OutfitSendDetachAll = false;
            Attach(600, 500, Head);      // on us
            Attach(601, 600, Hair);      // a child prim of an attachment, not an attachment root
            Attach(602, 777, Body);      // someone else's
            Assert.That(Prepare(), Is.EqualTo(new[] { Hair, Body, Hud }));
        }

        [Test]
        public void WithoutDetachAll_EverythingAttached_LeavesNothingToSend()
        {
            _client.Settings.Agent.OutfitSendDetachAll = false;
            uint lid = 600;
            foreach (var item in new[] { Hair, Head, Body, Hud }) Attach(lid++, 500, item);
            Assert.That(Prepare(), Is.Empty);
        }

        [Test]
        public void AllOptionsTogether()
        {
            var agent = _client.Settings.Agent;
            agent.OutfitSendDetachAll = false;
            agent.OutfitSendExclude = (_, name) => name == "HUD";
            agent.OutfitSendRank = name => name == "Body" ? 0 : 1;
            Attach(600, 500, Hair);
            Assert.That(Prepare(), Is.EqualTo(new[] { Body, Head }));
        }
    }
}
