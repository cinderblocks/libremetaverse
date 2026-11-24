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
using System.IO;
using System.Linq;
using System.Reflection;
using LibreMetaverse.Caching;
using LibreMetaverse.Packets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;
using Block = LibreMetaverse.Packets.ObjectUpdateCompressedPacket.ObjectDataBlock;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// ObjectDiskCache (LibreMetaverse.Caching): compressed updates kept per region, replayed for
    /// ObjectUpdateCached probes with a matching CRC, and plugged into ObjectManager.CacheProvider so the
    /// handshake tells the region whether we have a cache for it.
    /// </summary>
    [TestFixture]
    public class ObjectDiskCacheTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const ulong Handle = 0x000F424000F42800UL;
        private static readonly UUID CacheId = new UUID("0c0c0c0c-0000-0000-0000-000000000001");

        private string _dir = null!;
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lmv-objcache-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var d in _disposables) { try { d.Dispose(); } catch { } }
            _disposables.Clear();
            try { Directory.Delete(_dir, true); } catch { }
        }

        private ObjectDiskCache NewCache(int max = 100)
        {
            var cache = new ObjectDiskCache(_dir, max);
            _disposables.Add(cache);
            return cache;
        }

        /// <summary>A minimal compressed block for a box</summary>
        private static Block Compressed(uint localId, uint crc, PCode pcode = PCode.Prim)
        {
            var data = new List<byte>();
            data.AddRange(UUID.Random().GetBytes());
            data.AddRange(Utils.UIntToBytes(localId));
            data.Add((byte)pcode);
            data.Add(0);                                        // state
            data.AddRange(Utils.UIntToBytes(crc));
            data.Add((byte)Material.Wood);
            data.Add((byte)ClickAction.Touch);
            data.AddRange(new Vector3(1f, 1f, 1f).GetBytes());  // scale
            data.AddRange(new Vector3(128f, 128f, 25f).GetBytes());
            data.AddRange(Quaternion.Identity.GetBytes());
            data.AddRange(Utils.UIntToBytes(0u));               // compressed flags
            data.AddRange(UUID.Random().GetBytes());           // owner
            data.Add(0);                                        // extra params: none
            data.AddRange(new byte[1 + 2 + 2 + 11]);            // path
            data.AddRange(new byte[1 + 2 + 2 + 2]);             // profile
            var te = new Primitive.TextureEntry(UUID.Random()).GetBytes();
            data.AddRange(Utils.UIntToBytes((uint)te.Length));
            data.AddRange(te);
            return new Block { UpdateFlags = 7, Data = data.ToArray() };
        }

        private static (uint, uint)[] Probes(params (uint, uint)[] probes) => probes;

        [Test]
        public void Lookup_ReturnsKeptUpdatesWithMatchingCrc_AndMissesTheRest()
        {
            var cache = NewCache();
            var kept = Compressed(10, 1000);
            cache.Store(Handle, CacheId, kept);
            cache.Store(Handle, CacheId, Compressed(11, 1100));

            var hits = new List<Block>();
            var misses = cache.Lookup(Handle, CacheId, Probes((10, 1000), (11, 9999), (12, 1200)), hits);

            Assert.That(misses, Is.EqualTo(new[] { 11u, 12u }));
            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.That(hits[0].Data, Is.EqualTo(kept.Data));
            Assert.That(hits[0].UpdateFlags, Is.EqualTo(7u));
        }

        [Test]
        public void Regions_AndCacheIds_AreKeptApart()
        {
            var cache = NewCache();
            cache.Store(Handle, CacheId, Compressed(10, 1000));

            Assert.That(cache.HasEntries(Handle, CacheId), Is.True);
            Assert.That(cache.HasEntries(Handle + 1, CacheId), Is.False);
            Assert.That(cache.HasEntries(Handle, UUID.Random()), Is.False, "a new cache id means the region's cache was invalidated");
        }

        [Test]
        public void AvatarsAndRegionsWithoutCacheId_AreNotKept()
        {
            var cache = NewCache();
            cache.Store(Handle, CacheId, Compressed(10, 1000, PCode.Avatar));
            cache.Store(Handle, UUID.Zero, Compressed(11, 1100));

            Assert.That(cache.Count(Handle, CacheId), Is.EqualTo(0));
            Assert.That(cache.HasEntries(Handle, UUID.Zero), Is.False);
            Assert.That(cache.Lookup(Handle, UUID.Zero, Probes((11, 1100)), new List<Block>()), Is.EqualTo(new[] { 11u }));
        }

        [Test]
        public void NewObjectsBeyondTheLimit_AreNotKept_ButKeptOnesAreUpdated()
        {
            var cache = NewCache(max: 2);
            cache.Store(Handle, CacheId, Compressed(1, 100));
            cache.Store(Handle, CacheId, Compressed(2, 200));
            cache.Store(Handle, CacheId, Compressed(3, 300));
            cache.Store(Handle, CacheId, Compressed(2, 201));

            var hits = new List<Block>();
            var misses = cache.Lookup(Handle, CacheId, Probes((1, 100), (2, 201), (3, 300)), hits);
            Assert.That(cache.Count(Handle, CacheId), Is.EqualTo(2));
            Assert.That(misses, Is.EqualTo(new[] { 3u }));
        }

        [Test]
        public void Flush_WritesAFileThatANewCacheReads()
        {
            var first = NewCache();
            var kept = Compressed(10, 1000);
            first.Store(Handle, CacheId, kept);
            first.Flush();

            Assert.That(File.Exists(Path.Combine(_dir, ObjectDiskCache.FileName(Handle, CacheId))), Is.True);

            var second = NewCache();
            var hits = new List<Block>();
            Assert.That(second.HasEntries(Handle, CacheId), Is.True);
            Assert.That(second.Lookup(Handle, CacheId, Probes((10, 1000)), hits), Is.Empty);
            Assert.That(hits.Single().Data, Is.EqualTo(kept.Data));
        }

        [Test]
        public void Dispose_Flushes()
        {
            using (var cache = new ObjectDiskCache(_dir, 100))
                cache.Store(Handle, CacheId, Compressed(10, 1000));

            Assert.That(NewCache().Count(Handle, CacheId), Is.EqualTo(1));
        }

        [Test]
        public void AnUnreadableFile_IsTreatedAsAnEmptyCache()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllBytes(Path.Combine(_dir, ObjectDiskCache.FileName(Handle, CacheId)),
                new byte[] { 0x4F, 0x42, 0x4A, 0x43, 1, 0, 0, 0, 5, 0, 0, 0, 1, 2 });

            var cache = NewCache();
            Assert.That(cache.HasEntries(Handle, CacheId), Is.False);
            cache.Store(Handle, CacheId, Compressed(10, 1000));
            Assert.That(cache.Count(Handle, CacheId), Is.EqualTo(1));
        }

        private FakeGridClient NewClient(out ObjectDiskCache cache)
        {
            var client = new FakeGridClient();
            _disposables.Add(client);
            client.AddCapability("Placeholder", new Uri("http://test.invalid/placeholder"));
            client.Network.CurrentSim.ID = CacheId;
            cache = NewCache();
            client.Objects.CacheProvider = cache;
            return client;
        }

        private static void Deliver(GridClient client, string handler, Packet packet) =>
            typeof(ObjectManager).GetMethod(handler, Flags)!
                .Invoke(client.Objects, new object?[] { null, new PacketReceivedEventArgs(packet, client.Network.CurrentSim) });

        [Test]
        public void NoCacheProvider_IsTodaysBehavior()
        {
            var client = new FakeGridClient();
            _disposables.Add(client);
            client.AddCapability("Placeholder", new Uri("http://test.invalid/placeholder"));
            Assert.That(client.Objects.CacheProvider, Is.Null);
            Assert.That(client.Network.RegionHandshakeReplyFlags(client.Network.CurrentSim), Is.EqualTo(0x7u));
        }

        [Test]
        public void Handshake_AsksForCacheProbesOnlyWhenTheRegionHasEntries()
        {
            var client = NewClient(out var cache);
            var sim = client.Network.CurrentSim;
            Assert.That(client.Network.RegionHandshakeReplyFlags(sim), Is.EqualTo(0x7u), "nothing kept yet");

            cache.Store(sim.Handle, sim.ID, Compressed(10, 1000));
            Assert.That(client.Network.RegionHandshakeReplyFlags(sim), Is.EqualTo(0x5u));
        }

        [Test]
        public void ReceivedUpdates_AreKept_AndReplayedForAMatchingProbe()
        {
            var client = NewClient(out var cache);
            var sim = client.Network.CurrentSim;
            Deliver(client, "ObjectUpdateCompressedHandler", new ObjectUpdateCompressedPacket
            {
                RegionData = { RegionHandle = sim.Handle, TimeDilation = ushort.MaxValue },
                ObjectData = new[] { Compressed(10, 1000), Compressed(11, 1100) }
            });
            Assert.That(cache.Count(sim.Handle, sim.ID), Is.EqualTo(2));

            // a later visit: the region's objects are no longer in memory
            sim.ObjectsPrimitives.Clear();
            var updates = new List<uint>();
            client.Objects.ObjectUpdate += (_, e) => { lock (updates) updates.Add(e.Prim.LocalID); };

            Deliver(client, "ObjectUpdateCachedHandler", new ObjectUpdateCachedPacket
            {
                RegionData = { RegionHandle = sim.Handle, TimeDilation = ushort.MaxValue },
                ObjectData = new[]
                {
                    new ObjectUpdateCachedPacket.ObjectDataBlock { ID = 10, CRC = 1000 },
                    new ObjectUpdateCachedPacket.ObjectDataBlock { ID = 11, CRC = 1101 },   // changed since
                }
            });

            Assert.That(sim.ObjectsPrimitives.ContainsKey(10), Is.True, "replayed from the cache");
            Assert.That(sim.ObjectsPrimitives.ContainsKey(11), Is.False, "changed: requested instead");
            Assert.That(updates, Is.EqualTo(new[] { 10u }));
        }
    }
}
