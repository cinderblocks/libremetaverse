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

using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Regression tests for Issue #161: uint overflow in TextureEntry bitfield encoding/decoding.
    ///
    /// The old code used `uint bit = 1; bit &lt;&lt;= face` in the decode loop, which silently
    /// overflows to 0 for face >= 32, causing all per-face overrides at those indices to be
    /// dropped on decode. The encode side used uint[] sentinels with `(uint)(1 &lt;&lt; i)` which
    /// has the same overflow for i >= 32. Both were fixed by switching to ulong/1UL.
    /// </summary>
    [TestFixture]
    [Category("TextureEntry")]
    public class TextureEntryTests
    {
        // MAX_FACES = 45; faces 32-44 were all affected by the overflow.

        [Test]
        [TestCase(32, Description = "First face affected by 32-bit overflow")]
        [TestCase(33)]
        [TestCase(40)]
        [TestCase(44, Description = "Last valid face (MAX_FACES - 1)")]
        public void RoundTrip_HighFace_TextureID_IsPreserved(int faceIndex)
        {
            var defaultTexture = UUID.Random();
            var faceTexture = UUID.Random();

            var te = new Primitive.TextureEntry(defaultTexture);
            te.CreateFace((uint)faceIndex).TextureID = faceTexture;

            var decoded = RoundTrip(te);

            Assert.That(decoded.FaceTextures[faceIndex], Is.Not.Null,
                $"Face {faceIndex} should have a per-face entry after round-trip");
            Assert.That(decoded.FaceTextures[faceIndex].TextureID, Is.EqualTo(faceTexture),
                $"Face {faceIndex} texture should be preserved; if it equals the default the bitfield was dropped");
        }

        [Test]
        public void RoundTrip_AllHighFaces_TextureIDs_AllPreserved()
        {
            var defaultTexture = UUID.Random();
            var te = new Primitive.TextureEntry(defaultTexture);

            var expectedTextures = new UUID[Primitive.TextureEntry.MAX_FACES];
            for (int i = 32; i < Primitive.TextureEntry.MAX_FACES; i++)
            {
                expectedTextures[i] = UUID.Random();
                te.CreateFace((uint)i).TextureID = expectedTextures[i];
            }

            var decoded = RoundTrip(te);

            for (int i = 32; i < Primitive.TextureEntry.MAX_FACES; i++)
            {
                Assert.That(decoded.FaceTextures[i], Is.Not.Null,
                    $"Face {i} should have a per-face entry after round-trip");
                Assert.That(decoded.FaceTextures[i].TextureID, Is.EqualTo(expectedTextures[i]),
                    $"Face {i} texture not preserved after round-trip");
            }
        }

        [Test]
        public void RoundTrip_LowAndHighFaces_BothPreserved()
        {
            var defaultTexture = UUID.Random();
            var face0Tex = UUID.Random();
            var face15Tex = UUID.Random();
            var face32Tex = UUID.Random();
            var face44Tex = UUID.Random();

            var te = new Primitive.TextureEntry(defaultTexture);
            te.CreateFace(0).TextureID = face0Tex;
            te.CreateFace(15).TextureID = face15Tex;
            te.CreateFace(32).TextureID = face32Tex;
            te.CreateFace(44).TextureID = face44Tex;

            var decoded = RoundTrip(te);

            Assert.That(decoded.FaceTextures[0].TextureID, Is.EqualTo(face0Tex), "Face 0");
            Assert.That(decoded.FaceTextures[15].TextureID, Is.EqualTo(face15Tex), "Face 15");
            Assert.That(decoded.FaceTextures[32].TextureID, Is.EqualTo(face32Tex), "Face 32");
            Assert.That(decoded.FaceTextures[44].TextureID, Is.EqualTo(face44Tex), "Face 44");
        }

        [Test]
        public void RoundTrip_Face32_RGBA_IsPreserved()
        {
            // Use colors that are exactly representable after byte compression (0/255 boundaries).
            var te = new Primitive.TextureEntry(UUID.Random());
            te.DefaultTexture.RGBA = new Color4(1f, 1f, 1f, 1f); // white
            te.CreateFace(32).RGBA = new Color4(1f, 0f, 0f, 1f); // pure red

            var decoded = RoundTrip(te);

            Assert.That(decoded.FaceTextures[32], Is.Not.Null,
                "Face 32 should have a per-face RGBA override after round-trip");
            // A regressed build would fall back to white (the default); red means the override survived.
            Assert.That(decoded.FaceTextures[32].RGBA.R, Is.EqualTo(1f).Within(0.01f), "R channel");
            Assert.That(decoded.FaceTextures[32].RGBA.G, Is.EqualTo(0f).Within(0.01f), "G channel");
            Assert.That(decoded.FaceTextures[32].RGBA.B, Is.EqualTo(0f).Within(0.01f), "B channel");
        }

        [Test]
        public void RoundTrip_Face32_RepeatU_IsPreserved()
        {
            var te = new Primitive.TextureEntry(UUID.Random());
            // Default RepeatU is 1.0; set face 32 to something distinct.
            te.CreateFace(32).RepeatU = 4.0f;

            var decoded = RoundTrip(te);

            Assert.That(decoded.FaceTextures[32], Is.Not.Null,
                "Face 32 should have a per-face RepeatU override after round-trip");
            Assert.That(decoded.FaceTextures[32].RepeatU, Is.EqualTo(4.0f).Within(0.01f));
        }

        [Test]
        public void RoundTrip_Face32_RepeatV_IsPreserved()
        {
            var te = new Primitive.TextureEntry(UUID.Random());
            te.CreateFace(32).RepeatV = 4.0f;

            var decoded = RoundTrip(te);

            Assert.That(decoded.FaceTextures[32], Is.Not.Null,
                "Face 32 should have a per-face RepeatV override after round-trip");
            Assert.That(decoded.FaceTextures[32].RepeatV, Is.EqualTo(4.0f).Within(0.01f));
        }

        [Test]
        public void RoundTrip_AllFaces_TextureIDs_AllPreserved()
        {
            // Comprehensive: every face gets a unique texture; verifies the full encode/decode path.
            var defaultTexture = UUID.Random();
            var te = new Primitive.TextureEntry(defaultTexture);

            var expected = new UUID[Primitive.TextureEntry.MAX_FACES];
            for (int i = 0; i < Primitive.TextureEntry.MAX_FACES; i++)
            {
                expected[i] = UUID.Random();
                te.CreateFace((uint)i).TextureID = expected[i];
            }

            var decoded = RoundTrip(te);

            for (int i = 0; i < Primitive.TextureEntry.MAX_FACES; i++)
            {
                Assert.That(decoded.FaceTextures[i], Is.Not.Null,
                    $"Face {i} should have a per-face entry after round-trip");
                Assert.That(decoded.FaceTextures[i].TextureID, Is.EqualTo(expected[i]),
                    $"Face {i} texture not preserved");
            }
        }

        private static Primitive.TextureEntry RoundTrip(Primitive.TextureEntry te)
        {
            byte[] bytes = te.GetBytes();
            return new Primitive.TextureEntry(bytes, 0, bytes.Length);
        }
    }
}
