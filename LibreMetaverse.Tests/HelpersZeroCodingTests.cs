/*
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
using NUnit.Framework;
using OpenMetaverse;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Helpers")]
    public class HelpersZeroCodingTests
    {
        [Test]
        public void ZeroDecode_ValidInput_DecodesCorrectly()
        {
            // Create a simple zerocoded packet: header (6 bytes) + encoded data
            byte[] src = new byte[] 
            { 
                0x00, 0x01, 0x02, 0x03, 0x04, 0x05, // header
                0x00, 0x03, // 3 zeros
                0xFF,       // non-zero byte
                0x00, 0x01, // 1 zero
                0xAA        // non-zero byte
            };
            byte[] dest = new byte[100];
            
            int resultLen = Helpers.ZeroDecode(src, src.Length, dest);
            
            // Expected: header + 3 zeros + 0xFF + 1 zero + 0xAA = 12 bytes
            Assert.That(resultLen, Is.EqualTo(12));
            Assert.That(dest[0], Is.EqualTo(0x00));
            Assert.That(dest[5], Is.EqualTo(0x05));
            Assert.That(dest[6], Is.EqualTo(0x00));
            Assert.That(dest[7], Is.EqualTo(0x00));
            Assert.That(dest[8], Is.EqualTo(0x00));
            Assert.That(dest[9], Is.EqualTo(0xFF));
            Assert.That(dest[10], Is.EqualTo(0x00));
            Assert.That(dest[11], Is.EqualTo(0xAA));
        }

        [Test]
        public void ZeroDecode_NullSource_ThrowsArgumentNullException()
        {
            byte[] dest = new byte[100];
            
            Assert.Throws<ArgumentNullException>(() => 
                Helpers.ZeroDecode(null, 10, dest));
        }

        [Test]
        public void ZeroDecode_NullDestination_ThrowsArgumentNullException()
        {
            byte[] src = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF };
            
            Assert.Throws<ArgumentNullException>(() => 
                Helpers.ZeroDecode(src, src.Length, null));
        }

        [Test]
        public void ZeroDecode_SrclenTooSmall_ThrowsArgumentException()
        {
            byte[] src = new byte[] { 0x00, 0x01, 0x02 };
            byte[] dest = new byte[100];
            
            Assert.Throws<ArgumentException>(() => 
                Helpers.ZeroDecode(src, 3, dest));
        }

        [Test]
        public void ZeroDecode_SrclenGreaterThanSrcLength_ThrowsArgumentException()
        {
            byte[] src = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
            byte[] dest = new byte[100];
            
            Assert.Throws<ArgumentException>(() => 
                Helpers.ZeroDecode(src, 100, dest));
        }

        [Test]
        public void ZeroDecode_DestinationTooSmall_ThrowsIndexOutOfRangeException()
        {
            // Create packet that will decode to more bytes than dest can hold
            byte[] src = new byte[] 
            { 
                0x00, 0x01, 0x02, 0x03, 0x04, 0x05, // header
                0x00, 0xFF  // 255 zeros
            };
            byte[] dest = new byte[10]; // Too small
            
            Assert.Throws<IndexOutOfRangeException>(() => 
                Helpers.ZeroDecode(src, src.Length, dest));
        }

        [Test]
        public void ZeroDecode_ReadingZeroCountOutOfBounds_ThrowsIndexOutOfRangeException()
        {
            // Create packet where zero marker is at the end with no count byte
            byte[] src = new byte[] 
            { 
                0x00, 0x01, 0x02, 0x03, 0x04, 0x05, // header
                0x00 // zero marker but no count byte
            };
            byte[] dest = new byte[100];
            
            Assert.Throws<IndexOutOfRangeException>(() => 
                Helpers.ZeroDecode(src, src.Length, dest));
        }

        [Test]
        public void ZeroEncode_ValidInput_EncodesCorrectly()
        {
            // Create packet with runs of zeros
            byte[] src = new byte[] 
            { 
                0x00, 0x01, 0x02, 0x03, 0x04, 0x05, // header
                0x00, 0x00, 0x00, // 3 zeros
                0xFF,             // non-zero
                0x00,             // 1 zero
                0xAA              // non-zero
            };
            byte[] dest = new byte[100];
            
            int resultLen = Helpers.ZeroEncode(src, src.Length, dest);
            
            // Should compress 3 zeros to 0x00 0x03, and 1 zero to 0x00 0x01
            Assert.That(resultLen, Is.GreaterThan(0));
            
            // Verify it can be decoded back
            byte[] decoded = new byte[100];
            int decodedLen = Helpers.ZeroDecode(dest, resultLen, decoded);
            
            Assert.That(decodedLen, Is.EqualTo(src.Length));
            for (int i = 0; i < src.Length; i++)
            {
                Assert.That(decoded[i], Is.EqualTo(src[i]), 
                    $"Mismatch at position {i}: expected {src[i]:X2}, got {decoded[i]:X2}");
            }
        }

        [Test]
        public void ZeroEncode_255ConsecutiveZeros_HandlesOverflowCorrectly()
        {
            // Test the byte overflow case: 255 consecutive zeros
            byte[] src = new byte[6 + 256]; // header + 256 zeros
            for (int i = 0; i < 6; i++) src[i] = (byte)i;
            // Rest are zeros (default initialization)
            
            byte[] dest = new byte[500];
            
            int resultLen = Helpers.ZeroEncode(src, src.Length, dest);
            
            Assert.That(resultLen, Is.GreaterThan(0));
            
            // Decode and verify
            byte[] decoded = new byte[500];
            int decodedLen = Helpers.ZeroDecode(dest, resultLen, decoded);
            
            Assert.That(decodedLen, Is.EqualTo(src.Length));
            
            // Check header
            for (int i = 0; i < 6; i++)
            {
                Assert.That(decoded[i], Is.EqualTo(src[i]));
            }
            
            // Check all zeros
            for (int i = 6; i < src.Length; i++)
            {
                Assert.That(decoded[i], Is.Zero, $"Expected zero at position {i}");
            }
        }

        [Test]
        public void ZeroEncode_NullSource_ThrowsArgumentNullException()
        {
            byte[] dest = new byte[100];
            
            Assert.Throws<ArgumentNullException>(() => 
                Helpers.ZeroEncode(null, 10, dest));
        }

        [Test]
        public void ZeroEncode_NullDestination_ThrowsArgumentNullException()
        {
            byte[] src = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF };
            
            Assert.Throws<ArgumentNullException>(() => 
                Helpers.ZeroEncode(src, src.Length, null));
        }

        [Test]
        public void ZeroEncode_DestinationTooSmall_ThrowsIndexOutOfRangeException()
        {
            byte[] src = new byte[100];
            for (int i = 0; i < 6; i++) src[i] = (byte)i;
            for (int i = 6; i < 100; i++) src[i] = 0xFF; // No zeros to compress
            
            byte[] dest = new byte[10]; // Too small
            
            Assert.Throws<IndexOutOfRangeException>(() => 
                Helpers.ZeroEncode(src, src.Length, dest));
        }

        [Test]
        public void ZeroEncode_WithAppendedAcks_HandlesCorrectly()
        {
            // Create packet with appended ACKs flag set
            byte[] src = new byte[] 
            { 
                0x10, 0x01, 0x02, 0x03, 0x04, 0x05, // header with MSG_APPENDED_ACKS flag
                0x00, 0x00, 0x00, // 3 zeros
                0xFF,             // non-zero
                0x00, 0x00, 0x00, 0x00, // ACK (4 bytes)
                0x01              // ACK count
            };
            byte[] dest = new byte[100];
            
            int resultLen = Helpers.ZeroEncode(src, src.Length, dest);
            
            Assert.That(resultLen, Is.GreaterThan(0));
            
            // The ACKs should be copied without encoding
            // Verify the ACK count is preserved
            Assert.That(dest[resultLen - 1], Is.EqualTo(0x01));
        }

        [Test]
        public void ZeroEncodeAndDecode_RoundTrip_PreservesData()
        {
            // Test various patterns
            byte[][] testCases = new byte[][]
            {
                // Simple case
                new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF, 0x00, 0xAA },
                
                // Multiple zero runs
                new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xAA },
                
                // No zeros after header
                new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF, 0xAA, 0xBB },
                
                // All zeros after header
                new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x00, 0x00, 0x00, 0x00 }
            };

            foreach (var testCase in testCases)
            {
                byte[] encoded = new byte[500];
                byte[] decoded = new byte[500];
                
                int encodedLen = Helpers.ZeroEncode(testCase, testCase.Length, encoded);
                Assert.That(encodedLen, Is.GreaterThan(0), "Encoding failed");
                
                int decodedLen = Helpers.ZeroDecode(encoded, encodedLen, decoded);
                Assert.That(decodedLen, Is.EqualTo(testCase.Length), 
                    "Decoded length doesn't match original");
                
                for (int i = 0; i < testCase.Length; i++)
                {
                    Assert.That(decoded[i], Is.EqualTo(testCase[i]), 
                        $"Mismatch at position {i} in test case");
                }
            }
        }
    }
}
