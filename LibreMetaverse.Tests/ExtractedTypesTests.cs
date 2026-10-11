/*
 * Copyright (c) 2026, Sjofn LLC.
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
using System.Net;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Small pieces that were taken out of the managers so they can live without a connection. Each keeps the
    /// name it had, and these tests show it still does what it did.
    /// </summary>
    [TestFixture]
    public class ExtractedTypesTests
    {
        [Test]
        public void RegionDefaults_AreA256MetreRegion_AndSimulatorStillSaysSo()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RegionDefaults.SizeX, Is.EqualTo(256u));
                Assert.That(RegionDefaults.SizeY, Is.EqualTo(256u));
                Assert.That(Simulator.DefaultRegionSizeX, Is.EqualTo(RegionDefaults.SizeX));
                Assert.That(Simulator.DefaultRegionSizeY, Is.EqualTo(RegionDefaults.SizeY));
            });
        }

        [Test]
        public void InventoryHelpers_ShadowIdIsTheAssetIdXoredWithTheMagicId_AndBackAgain()
        {
            var asset = UUID.Random();

            var shadow = InventoryHelpers.EncryptAssetID(asset);

            Assert.Multiple(() =>
            {
                Assert.That(InventoryHelpers.MagicId, Is.EqualTo(new UUID("3c115e51-04f4-523c-9fa6-98aff1034730")));
                Assert.That(shadow, Is.EqualTo(asset ^ InventoryHelpers.MagicId));
                Assert.That(InventoryHelpers.DecryptShadowID(shadow), Is.EqualTo(asset));
                Assert.That(InventoryManager.MAGIC_ID, Is.EqualTo(InventoryHelpers.MagicId));
                Assert.That(InventoryManager.EncryptAssetID(asset), Is.EqualTo(shadow));
                Assert.That(InventoryManager.DecryptShadowID(shadow), Is.EqualTo(asset));
            });
        }

        [TestCase(InventoryType.Texture, typeof(InventoryTexture))]
        [TestCase(InventoryType.Notecard, typeof(InventoryNotecard))]
        [TestCase(InventoryType.Material, typeof(InventoryMaterial))]
        [TestCase(InventoryType.Settings, typeof(InventorySettings))]
        [TestCase(InventoryType.Unknown, typeof(InventoryItem))]
        public void InventoryHelpers_CreateInventoryItem_MakesTheClassForTheType_AsInventoryManagerDoes(InventoryType type, Type expected)
        {
            var id = UUID.Random();

            var item = InventoryHelpers.CreateInventoryItem(type, id);
            var viaManager = InventoryManager.CreateInventoryItem(type, id);

            Assert.Multiple(() =>
            {
                Assert.That(item.GetType(), Is.EqualTo(expected));
                Assert.That(viaManager.GetType(), Is.EqualTo(expected));
                Assert.That(item.UUID, Is.EqualTo(id));
                Assert.That(item.InventoryType, Is.EqualTo(type));
            });
        }

        [Test]
        public void GetChildren_GivesThePrimsLinkedToItInItsRegion()
        {
            const ulong handle = 1099511628032UL;
            using var client = new GridClient();
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), handle);
            client.Network.Simulators.Add(sim);
            try
            {
                var root = new Primitive { LocalID = 10, RegionHandle = handle };
                var childA = new Primitive { LocalID = 11, ParentID = 10, RegionHandle = handle };
                var childB = new Primitive { LocalID = 12, ParentID = 10, RegionHandle = handle };
                var other = new Primitive { LocalID = 13, ParentID = 99, RegionHandle = handle };
                foreach (var prim in new[] { root, childA, childB, other }) sim.ObjectsPrimitives[prim.LocalID] = prim;

                var children = root.GetChildren(client).ToList();

                Assert.That(children.Select(c => c.LocalID), Is.EquivalentTo(new uint[] { 11, 12 }));
            }
            finally
            {
                client.Network.Simulators.Remove(sim);
            }
        }

        [Test]
        public void GetChildren_InARegionTheClientIsNotIn_GivesNone()
        {
            using var client = new GridClient();
            var root = new Primitive { LocalID = 10, RegionHandle = 12345UL };

            Assert.That(root.GetChildren(client), Is.Empty);
        }

        [Test]
        public void GetChildren_WithoutAClient_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Primitive().GetChildren(null));
        }

        [Test]
        public void AvatarTextureIndex_KeepsItsNumbers()
        {
            // The numbers are what the grid sends in an avatar's texture entry, so moving the enum must not shift any
            Assert.Multiple(() =>
            {
                Assert.That((int)AvatarTextureIndex.Unknown, Is.EqualTo(-1));
                Assert.That((int)AvatarTextureIndex.HeadBodypaint, Is.EqualTo(0));
                Assert.That((int)AvatarTextureIndex.HeadBaked, Is.EqualTo(8));
                Assert.That((int)AvatarTextureIndex.SkirtBaked, Is.EqualTo(19));
                Assert.That((int)AvatarTextureIndex.NumberOfEntries, Is.EqualTo(45));
            });
        }

        [Test]
        public void Animation_HoldsWhatTheGridTellsAboutOne()
        {
            var animation = new Animation { AnimationID = UUID.Random(), AnimationSequence = 7, AnimationSourceObjectID = UUID.Random() };

            Assert.That(animation.AnimationSequence, Is.EqualTo(7));
            Assert.That(animation.AnimationID, Is.Not.EqualTo(animation.AnimationSourceObjectID));
        }
    }
}
