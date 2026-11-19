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
using System.Threading;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    /// <summary>
    /// Keeps the compressed object updates of each region on disk, like the viewer's object cache. When a region we
    /// have entries for is entered, the handshake reply no longer says the cache is empty, the region answers with
    /// ObjectUpdateCached probes (local ID and CRC), and a probe whose CRC matches a kept update is replayed locally
    /// instead of being requested. Enabled by <see cref="WorldSettings.ObjectCacheDirectory"/>.
    /// </summary>
    /// <remarks>
    /// One file per region and cache ID (the region's <see cref="Simulator.ID"/>, which changes when the region's
    /// object cache is invalidated). Avatars are never kept. Files are written every
    /// <see cref="SaveInterval"/> and on <see cref="Flush"/> / <see cref="Dispose"/>.
    /// </remarks>
    public sealed class ObjectDiskCache : IDisposable
    {
        private const uint Magic = 0x434A424F; // "OBJC"
        private const int Version = 1;
        private const int MaxBlockLength = 65536;

        /// <summary>How often changed regions are written to disk</summary>
        public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

        private sealed class Region
        {
            public readonly Dictionary<uint, (uint Crc, byte[] Block)> Entries = new Dictionary<uint, (uint, byte[])>();
            public string Path = string.Empty;
            public bool Dirty;
        }

        private readonly Dictionary<string, Region> _regions = new Dictionary<string, Region>();
        private readonly Timer _saveTimer;
        private bool _disposed;

        /// <summary>Directory the cache files are kept in</summary>
        public string Directory { get; }

        /// <summary>Most entries kept per region; updates for new objects beyond it are not kept</summary>
        public int MaxEntriesPerRegion { get; }

        public ObjectDiskCache(string directory, int maxEntriesPerRegion)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A cache directory is required", nameof(directory));
            Directory = directory;
            MaxEntriesPerRegion = Math.Max(0, maxEntriesPerRegion);
            _saveTimer = new Timer(_ => SafeFlush(), null, SaveInterval, SaveInterval);
        }

        /// <summary>File name of a region's cache</summary>
        internal static string FileName(ulong regionHandle, UUID cacheId) => $"{regionHandle:X16}-{cacheId}.objcache";

        /// <summary>True when updates are kept for this region and cache ID</summary>
        public bool HasEntries(ulong regionHandle, UUID cacheId)
        {
            if (cacheId == UUID.Zero) return false;
            var region = Get(regionHandle, cacheId);
            lock (region) return region.Entries.Count > 0;
        }

        /// <summary>Number of updates kept for this region and cache ID</summary>
        public int Count(ulong regionHandle, UUID cacheId)
        {
            if (cacheId == UUID.Zero) return 0;
            var region = Get(regionHandle, cacheId);
            lock (region) return region.Entries.Count;
        }

        /// <summary>Keep a compressed object update received from this region</summary>
        public void Store(ulong regionHandle, UUID cacheId, ObjectUpdateCompressedPacket.ObjectDataBlock block)
        {
            if (cacheId == UUID.Zero || block?.Data == null || block.Data.Length < 26) return;
            if (block.Data[20] == (byte)PCode.Avatar) return;

            var localId = Utils.BytesToUInt(block.Data, 16);
            var crc = Utils.BytesToUInt(block.Data, 22);
            var bytes = new byte[block.Length];
            var i = 0;
            block.ToBytes(bytes, ref i);

            var region = Get(regionHandle, cacheId);
            lock (region)
            {
                if (region.Entries.Count >= MaxEntriesPerRegion && !region.Entries.ContainsKey(localId)) return;
                region.Entries[localId] = (crc, bytes);
                region.Dirty = true;
            }
        }

        /// <summary>
        /// Match cache probes from this region: kept updates whose CRC matches are added to <paramref name="hits"/>,
        /// and the local IDs that still have to be requested are returned
        /// </summary>
        public List<uint> Lookup(ulong regionHandle, UUID cacheId, IEnumerable<(uint LocalID, uint Crc)> probes,
            List<ObjectUpdateCompressedPacket.ObjectDataBlock> hits)
        {
            var misses = new List<uint>();
            if (cacheId == UUID.Zero)
            {
                foreach (var probe in probes) misses.Add(probe.LocalID);
                return misses;
            }

            var region = Get(regionHandle, cacheId);
            lock (region)
            {
                foreach (var probe in probes)
                {
                    if (region.Entries.TryGetValue(probe.LocalID, out var entry) && entry.Crc == probe.Crc)
                    {
                        var i = 0;
                        hits.Add(new ObjectUpdateCompressedPacket.ObjectDataBlock(entry.Block, ref i));
                    }
                    else
                    {
                        misses.Add(probe.LocalID);
                    }
                }
            }
            return misses;
        }

        /// <summary>Write all changed regions to disk</summary>
        public void Flush()
        {
            List<Region> regions;
            lock (_regions) regions = new List<Region>(_regions.Values);
            foreach (var region in regions) Save(region);
        }

        private void SafeFlush()
        {
            try { Flush(); }
            catch (Exception ex) { Logger.Warn("Object cache: saving failed: " + ex.Message); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _saveTimer.Dispose();
            SafeFlush();
        }

        private Region Get(ulong regionHandle, UUID cacheId)
        {
            var name = FileName(regionHandle, cacheId);
            lock (_regions)
            {
                if (_regions.TryGetValue(name, out var region)) return region;
                region = new Region { Path = Path.Combine(Directory, name) };
                try
                {
                    Load(region);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Object cache: ignoring unreadable {region.Path}: {ex.Message}");
                    region.Entries.Clear();
                }
                _regions[name] = region;
                return region;
            }
        }

        private static void Save(Region region)
        {
            KeyValuePair<uint, (uint Crc, byte[] Block)>[] entries;
            lock (region)
            {
                if (!region.Dirty) return;
                region.Dirty = false;
                entries = new KeyValuePair<uint, (uint, byte[])>[region.Entries.Count];
                ((ICollection<KeyValuePair<uint, (uint, byte[])>>)region.Entries).CopyTo(entries, 0);
            }

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(region.Path)!);
            var tmp = region.Path + ".tmp";
            using (var writer = new BinaryWriter(File.Create(tmp)))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(entries.Length);
                foreach (var entry in entries)
                {
                    writer.Write(entry.Key);
                    writer.Write(entry.Value.Crc);
                    writer.Write(entry.Value.Block.Length);
                    writer.Write(entry.Value.Block);
                }
            }

            if (File.Exists(region.Path)) File.Replace(tmp, region.Path, null);
            else File.Move(tmp, region.Path);
        }

        private static void Load(Region region)
        {
            if (!File.Exists(region.Path)) return;
            using var reader = new BinaryReader(File.OpenRead(region.Path));
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version) return;
            var count = reader.ReadInt32();
            for (var k = 0; k < count; k++)
            {
                var localId = reader.ReadUInt32();
                var crc = reader.ReadUInt32();
                var length = reader.ReadInt32();
                if (length < 0 || length > MaxBlockLength) throw new InvalidDataException("bad block length");
                var block = reader.ReadBytes(length);
                if (block.Length != length) throw new EndOfStreamException();
                region.Entries[localId] = (crc, block);
            }
        }
    }
}
