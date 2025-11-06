/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Security.Cryptography;

namespace OpenMetaverse
{
    /// <summary>
    /// A 128-bit Universally Unique Identifier, used throughout the Second
    /// Life networking protocol
    /// </summary>
    [Serializable]
    public struct UUID : IComparable<UUID>, IEquatable<UUID>
    {
        /// <summary>The System.Guid object this struct wraps around</summary>
        public Guid Guid { get; set; }

        #region Constructors

        /// <summary>
        /// Constructor that takes a string UUID representation
        /// </summary>
        /// <param name="val">A string representation of a UUID, case-insensitive
        /// and can either be hyphenated or non-hyphenated</param>
        /// <example>UUID("11f8aa9c-b071-4242-836b-13b7abe0d489")</example>
        public UUID(string val)
        {
            Guid = string.IsNullOrEmpty(val) ? Guid.Empty : new Guid(val);
        }

        /// <summary>
        /// Constructor that takes a System.Guid object
        /// </summary>
        /// <param name="val">A Guid object that contains the unique identifier
        /// to be represented by this UUID</param>
        public UUID(Guid val)
        {
            Guid = val;
        }

        /// <summary>
        /// Constructor that takes a byte array containing a UUID
        /// </summary>
        /// <param name="source">Byte array containing a 16 byte UUID</param>
        /// <param name="pos">Beginning offset in the array</param>
        public UUID(byte[] source, int pos)
        {
            Guid = UUID.Zero.Guid;
            FromBytes(source, pos);
        }

        /// <summary>
        /// Constructor that takes an unsigned 64-bit unsigned integer to 
        /// convert to a UUID
        /// </summary>
        /// <param name="val">64-bit unsigned integer to convert to a UUID</param>
        public UUID(ulong val)
        {
            byte[] end = BitConverter.GetBytes(val);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(end);

            Guid = new Guid(0, 0, 0, end);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="val">UUID to copy</param>
        public UUID(UUID val)
        {
            Guid = val.Guid;
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>
        /// IComparable.CompareTo implementation
        /// </summary>
        public int CompareTo(UUID id)
        {
            return Guid.CompareTo(id.Guid);
        }

        /// <summary>
        /// Assigns this UUID from 16 bytes out of a byte array
        /// </summary>
        /// <param name="source">Byte array containing the UUID to assign this UUID to</param>
        /// <param name="pos">Starting position of the UUID in the byte array</param>
        public void FromBytes(byte[] source, int pos)
        {
            int a = (source[pos + 0] << 24) | (source[pos + 1] << 16) | (source[pos + 2] << 8) | source[pos + 3];
            short b = (short)((source[pos + 4] << 8) | source[pos + 5]);
            short c = (short)((source[pos + 6] << 8) | source[pos + 7]);

            Guid = new Guid(a, b, c, source[pos + 8], source[pos + 9], source[pos + 10], source[pos + 11],
                source[pos + 12], source[pos + 13], source[pos + 14], source[pos + 15]);
        }

        /// <summary>
        /// Returns a copy of the raw bytes for this UUID (network byte order)
        /// </summary>
        /// <returns>A 16 byte array containing this UUID</returns>
        public byte[] GetBytes()
        {
            var output = new byte[16];
            ToBytes(output, 0);
            return output;
        }

        /// <summary>
        /// Writes the raw bytes for this UUID to a byte array
        /// </summary>
        /// <param name="dest">Destination byte array</param>
        /// <param name="pos">Position in the destination array to start
        /// writing. Must be at least 16 bytes before the end of the array</param>
        public void ToBytes(byte[] dest, int pos)
        {
            GuidToNetworkBytes(Guid, dest, pos);
        }

        /// <summary>
        /// Calculate an LLCRC (cyclic redundancy check) for this UUID
        /// </summary>
        /// <returns>The CRC checksum for this UUID</returns>
        public uint CRC()
        {
            // Use Guid.ToByteArray directly and compute without extra allocations
            byte[] b = Guid.ToByteArray();

            // Convert to network order as ToBytes would and accumulate big-endian words
            uint w0 = (uint)((b[3] << 24) | (b[2] << 16) | (b[1] << 8) | b[0]);
            uint w1 = (uint)((b[7] << 24) | (b[6] << 16) | (b[5] << 8) | b[4]);
            uint w2 = (uint)((b[11] << 24) | (b[10] << 16) | (b[9] << 8) | b[8]);
            uint w3 = (uint)((b[15] << 24) | (b[14] << 16) | (b[13] << 8) | b[12]);

            return w0 + w1 + w2 + w3;
        }

        /// <summary>
        /// Create a 64-bit integer representation from the second half of this UUID
        /// </summary>
        /// <returns>An integer created from the last eight bytes of this UUID</returns>
        public ulong GetULong()
        {
            // Extract the last 8 bytes from the Guid's raw bytes (consistent with ToBytes mapping)
            byte[] b = Guid.ToByteArray();

            // Network-order mapping puts raw[8]..raw[15] in the last 8 bytes
            return ((ulong)b[8]) |
                   ((ulong)b[9] << 8) |
                   ((ulong)b[10] << 16) |
                   ((ulong)b[11] << 24) |
                   ((ulong)b[12] << 32) |
                   ((ulong)b[13] << 40) |
                   ((ulong)b[14] << 48) |
                   ((ulong)b[15] << 56);
        }

        #endregion Public Methods

        #region Static Methods

        /// <summary>
        /// Generate a UUID from a string
        /// </summary>
        /// <param name="val">A string representation of a UUID, case 
        /// insensitive and can either be hyphenated or non-hyphenated</param>
        /// <example>UUID.Parse("11f8aa9c-b071-4242-836b-13b7abe0d489")</example>
        public static UUID Parse(string val)
        {
            return new UUID(val);
        }

        /// <summary>
        /// Generate a UUID from a string
        /// </summary>
        /// <param name="val">A string representation of a UUID, case 
        /// insensitive and can either be hyphenated or non-hyphenated</param>
        /// <param name="result">Will contain the parsed UUID if successful,
        /// otherwise null</param>
        /// <returns>True if the string was successfully parse, otherwise false</returns>
        /// <example>UUID.TryParse("11f8aa9c-b071-4242-836b-13b7abe0d489", result)</example>
        public static bool TryParse(string val, out UUID result)
        {
            if (string.IsNullOrEmpty(val))
            {
                result = UUID.Zero;
                return false;
            }

            // Use Guid.TryParse to avoid exceptions and improve perf
            if (Guid.TryParse(val, out var g))
            {
                result = new UUID(g);
                return true;
            }

            result = UUID.Zero;
            return false;
        }

        /// <summary>
        /// Combine two UUIDs together by taking the MD5 hash of a byte array
        /// containing both UUIDs
        /// </summary>
        /// <param name="first">First UUID to combine</param>
        /// <param name="second">Second UUID to combine</param>
        /// <returns>The UUID product of the combination</returns>
        public static UUID Combine(UUID first, UUID second)
        {
            // Construct the buffer that will be MD5 hashed. Keep network order.
            byte[] input = new byte[32];
            GuidToNetworkBytes(first.Guid, input, 0);
            GuidToNetworkBytes(second.Guid, input, 16);

            return new UUID(Utils.MD5(input), 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>UUID</returns>
        public static UUID Random()
        {
            return new UUID(Guid.NewGuid());
        }

        /// <summary>
        /// Slower than Random(), but generates a cryptographically secure UUID (v4)
        /// </summary>
        /// <returns>UUID</returns>
        public static UUID SecureRandom()
        {
            byte[] data = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(data); }

            // Set RFC4122 version (4) and variant (10)
            // Version in the 7th byte of the RFC layout, for Guid raw bytes this is index 6
            data[6] = (byte)((data[6] & 0x0F) | 0x40); // version 4
            data[8] = (byte)((data[8] & 0x3F) | 0x80); // variant 10xxxxxx

            return new UUID(new Guid(data));
        }

        #endregion Static Methods

        #region Overrides

        /// <summary>
        /// Return a hash code for this UUID, used by .NET for hash tables
        /// </summary>
        /// <returns>An integer composed of all the UUID bytes XORed together</returns>
        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }

        /// <summary>
        /// Comparison function
        /// </summary>
        /// <param name="o">An object to compare to this UUID</param>
        /// <returns>True if the object is a UUID and both UUIDs are equal</returns>
        public override bool Equals(object o)
        {
            if (!(o is UUID uuid)) return false;

            return Guid == uuid.Guid;
        }

        /// <summary>
        /// Comparison function
        /// </summary>
        /// <param name="uuid">UUID to compare to</param>
        /// <returns>True if the UUIDs are equal, otherwise false</returns>
        public bool Equals(UUID uuid)
        {
            return Guid == uuid.Guid;
        }

        /// <summary>
        /// Get a hyphenated string representation of this UUID
        /// </summary>
        /// <returns>A string representation of this UUID, lowercase and 
        /// with hyphens</returns>
        /// <example>11f8aa9c-b071-4242-836b-13b7abe0d489</example>
        public override string ToString()
        {
            return Guid == Guid.Empty ? ZeroString : Guid.ToString();
        }

        #endregion Overrides

        #region Operators

        /// <summary>
        /// Equals operator
        /// </summary>
        /// <param name="lhs">First UUID for comparison</param>
        /// <param name="rhs">Second UUID for comparison</param>
        /// <returns>True if the UUIDs are byte for byte equal, otherwise false</returns>
        public static bool operator ==(UUID lhs, UUID rhs)
        {
            return lhs.Guid == rhs.Guid;
        }

        /// <summary>
        /// Not equals operator
        /// </summary>
        /// <param name="lhs">First UUID for comparison</param>
        /// <param name="rhs">Second UUID for comparison</param>
        /// <returns>True if the UUIDs are not equal, otherwise true</returns>
        public static bool operator !=(UUID lhs, UUID rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// XOR operator
        /// </summary>
        /// <param name="lhs">First UUID</param>
        /// <param name="rhs">Second UUID</param>
        /// <returns>A UUID that is a XOR combination of the two input UUIDs</returns>
        public static UUID operator ^(UUID lhs, UUID rhs)
        {
            // Avoid extra GetBytes allocations by converting Guid raw bytes
            byte[] outBytes = new byte[16];
            byte[] a = lhs.Guid.ToByteArray();
            byte[] b = rhs.Guid.ToByteArray();

            // Convert raw little-endian Guid layout to network layout while xoring
            outBytes[0] = (byte)(a[3] ^ b[3]);
            outBytes[1] = (byte)(a[2] ^ b[2]);
            outBytes[2] = (byte)(a[1] ^ b[1]);
            outBytes[3] = (byte)(a[0] ^ b[0]);

            outBytes[4] = (byte)(a[5] ^ b[5]);
            outBytes[5] = (byte)(a[4] ^ b[4]);

            outBytes[6] = (byte)(a[7] ^ b[7]);
            outBytes[7] = (byte)(a[6] ^ b[6]);

            for (int i = 8; i < 16; i++)
            {
                outBytes[i] = (byte)(a[i] ^ b[i]);
            }

            return new UUID(outBytes, 0);
        }

        /// <summary>
        /// String typecasting operator
        /// </summary>
        /// <param name="val">A UUID in string form. Case insensitive, 
        /// hyphenated or non-hyphenated</param>
        /// <returns>A UUID built from the string representation</returns>
        public static explicit operator UUID(string val)
        {
            return new UUID(val);
        }

        #endregion Operators

        /// <summary>A UUID with a value of all zeroes</summary>
        public static readonly UUID Zero = new UUID(Guid.Empty);

        /// <summary>A cache of UUID.Zero as a string to optimize a common path</summary>
        private static readonly string ZeroString = Guid.Empty.ToString();

        #region Helpers

        /// <summary>
        /// Convert a Guid to network-order (big-endian) bytes like original ToBytes did.
        /// </summary>
        private static void GuidToNetworkBytes(Guid g, byte[] dest, int pos)
        {
            byte[] raw = g.ToByteArray();

            dest[pos + 0] = raw[3];
            dest[pos + 1] = raw[2];
            dest[pos + 2] = raw[1];
            dest[pos + 3] = raw[0];
            dest[pos + 4] = raw[5];
            dest[pos + 5] = raw[4];
            dest[pos + 6] = raw[7];
            dest[pos + 7] = raw[6];
            Buffer.BlockCopy(raw, 8, dest, pos + 8, 8);
        }

        #endregion Helpers
    }
}
