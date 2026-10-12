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
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using LibreMetaverse.Assets;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The texture animation of a prim (llSetTextureAnim) is kept in region archives as the 16 bytes the simulator sends
    /// to viewers, in base64. The blobs here were written by OpenSim, from the CC0 archives described in
    /// LibreMetaverse.Archiving.Tests/OarTestData.
    /// </summary>
    [TestFixture]
    public class TextureAnimationXmlTests
    {
        private static readonly string[] OpenSimBlobs =
        {
            "A/8EBAAAAAAAAAAAAABwQQ==",
            "E/8BAQAAgD8AAAAACtejPA==",
            "E/8AAAAAAAAAAAAAzczMPQ==",
            "E/8AAAAAAAAAAAAAzcxMPQ==",
            "E/8AAAAAAAAAAAAACtcjPA=="
        };

        private static string Group(string element) =>
            "<SceneObjectGroup><SceneObjectPart><UUID><UUID>11111111-1111-1111-1111-111111111111</UUID></UUID>" + element + "</SceneObjectPart></SceneObjectGroup>";

        private static PrimObject Read(string element)
        {
            var prim = new AssetPrim();
            Assert.That(prim.DecodeXml(Group(element)), Is.True);
            return prim.Parent;
        }

        private static string Written(PrimObject prim) =>
            XDocument.Parse(new AssetPrim(prim, new List<PrimObject>()).EncodeXml()).Descendants("TextureAnimation").Single().Value;

        [TestCaseSource(nameof(OpenSimBlobs))]
        public void ATextureAnimationOpenSimWrote_IsWrittenBackByteForByte(string blob)
        {
            var prim = Read("<TextureAnimation>" + blob + "</TextureAnimation>");

            Assert.That(prim.TextureAnimation.HasValue, Is.True);
            Assert.That(Written(prim), Is.EqualTo(blob));
        }

        [Test]
        public void ATextureAnimationOpenSimWrote_IsRead()
        {
            // A looping 4 by 4 flip book on every face at 15 frames a second, and a smooth scroll
            var flipBook = Read("<TextureAnimation>" + OpenSimBlobs[0] + "</TextureAnimation>").TextureAnimation.Value;
            var scroll = Read("<TextureAnimation>" + OpenSimBlobs[1] + "</TextureAnimation>").TextureAnimation.Value;

            Assert.Multiple(() =>
            {
                Assert.That(flipBook.Flags, Is.EqualTo(Primitive.TextureAnimMode.ANIM_ON | Primitive.TextureAnimMode.LOOP));
                Assert.That(flipBook.Face, Is.EqualTo(255u), "all faces");
                Assert.That(flipBook.SizeX, Is.EqualTo(4u));
                Assert.That(flipBook.SizeY, Is.EqualTo(4u));
                Assert.That(flipBook.Start, Is.EqualTo(0f));
                Assert.That(flipBook.Length, Is.EqualTo(0f));
                Assert.That(flipBook.Rate, Is.EqualTo(15f));

                Assert.That(scroll.Flags, Is.EqualTo(Primitive.TextureAnimMode.ANIM_ON | Primitive.TextureAnimMode.LOOP | Primitive.TextureAnimMode.SMOOTH));
                Assert.That(scroll.SizeX, Is.EqualTo(1u));
                Assert.That(scroll.Start, Is.EqualTo(1f));
                Assert.That(scroll.Rate, Is.EqualTo(0.02f).Within(1e-6f));
            });
        }

        [Test]
        public void AnEmptyElement_IsAPrimWithNoAnimation_AndSoIsOneWrittenWithNone()
        {
            var read = Read("<TextureAnimation></TextureAnimation>");

            Assert.Multiple(() =>
            {
                Assert.That(read.TextureAnimation.HasValue, Is.False);
                Assert.That(Written(new PrimObject { ID = UUID.Random() }), Is.EqualTo(string.Empty));
                Assert.That(Written(read), Is.EqualTo(string.Empty));
            });
        }

        [TestCase("not base64 !!")]
        [TestCase("AAAA")]
        public void ATextureAnimationThatCannotBeRead_IsLeftOut(string content)
        {
            Assert.That(Read("<TextureAnimation>" + content + "</TextureAnimation>").TextureAnimation.HasValue, Is.False);
        }

        [Test]
        public void AnAnimationThatIsSetUpButOff_IsKeptAsItWasRead()
        {
            // llSetTextureAnim with the animation off still leaves its settings in a blob; nothing is lost writing it back
            var off = new Primitive.TextureAnimation { Flags = Primitive.TextureAnimMode.ANIM_OFF, Face = 2, SizeX = 3, SizeY = 5, Start = 1f, Length = 6f, Rate = 2f };
            var blob = Convert.ToBase64String(off.GetBytes());

            Assert.That(Written(Read("<TextureAnimation>" + blob + "</TextureAnimation>")), Is.EqualTo(blob));
        }

        [Test]
        public void APrimitiveWithTheAnimationOn_KeepsItConvertedToAPrimObjectAndBack()
        {
            var animation = new Primitive.TextureAnimation { Flags = Primitive.TextureAnimMode.ANIM_ON | Primitive.TextureAnimMode.PING_PONG, Face = 3, SizeX = 2, SizeY = 2, Start = 0f, Length = 4f, Rate = 1.5f };

            var primObject = PrimObject.FromPrimitive(new Primitive { TextureAnim = animation, Scale = Vector3.One });
            var back = primObject.ToPrimitive();

            Assert.Multiple(() =>
            {
                Assert.That(primObject.TextureAnimation, Is.EqualTo(animation));
                Assert.That(back.TextureAnim, Is.EqualTo(animation));
            });
        }

        [Test]
        public void APrimitiveWithTheAnimationOff_HasNoAnimationAsAPrimObject()
        {
            var primObject = PrimObject.FromPrimitive(new Primitive { Scale = Vector3.One });

            Assert.That(primObject.TextureAnimation.HasValue, Is.False);
            Assert.That(primObject.ToPrimitive().TextureAnim.Flags, Is.EqualTo(Primitive.TextureAnimMode.ANIM_OFF));
        }

        [Test]
        public void ThePrimObjectSerializesItsAnimation_AndReadsWhatWasSerializedWithout()
        {
            var animation = new Primitive.TextureAnimation { Flags = Primitive.TextureAnimMode.ANIM_ON, Face = 1, SizeX = 4, SizeY = 4, Start = 2f, Length = 8f, Rate = 0.5f };
            var with = new PrimObject { ID = UUID.Random(), TextureAnimation = animation };
            var without = new PrimObject { ID = UUID.Random() };

            var readWith = new PrimObject();
            readWith.Deserialize(with.Serialize());
            var readWithout = new PrimObject();
            readWithout.Deserialize(without.Serialize());

            Assert.Multiple(() =>
            {
                Assert.That(readWith.TextureAnimation, Is.EqualTo(animation));
                Assert.That(readWithout.TextureAnimation.HasValue, Is.False);
            });
        }
    }
}
