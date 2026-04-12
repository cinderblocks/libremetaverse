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

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenMetaverse;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Appearance")]
    public class AppearanceTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static List<VisualParam> Group0Params =>
            VisualParams.Params.Values.Where(p => p.Group == 0).ToList();

        // ── Avatar.DecodeVisualParams ──────────────────────────────────────────

        [Test]
        public void DecodeVisualParams_EmptyArray_ReturnsEmptyDictionary()
        {
            var avatar = new Avatar { VisualParameters = System.Array.Empty<byte>() };
            var decoded = avatar.DecodeVisualParams();
            Assert.That(decoded.Count, Is.EqualTo(0));
        }

        [Test]
        public void DecodeVisualParams_CountMatchesGroup0ParamCount()
        {
            var group0 = Group0Params;
            var avatar = new Avatar
            {
                VisualParameters = new byte[group0.Count]
            };

            var decoded = avatar.DecodeVisualParams();

            Assert.That(decoded.Count, Is.EqualTo(group0.Count));
        }

        [Test]
        public void DecodeVisualParams_RoundTrip_DefaultValues()
        {
            var group0 = Group0Params;
            var bytes = group0
                .Select(p => Utils.FloatToByte(p.DefaultValue, p.MinValue, p.MaxValue))
                .ToArray();

            var avatar = new Avatar { VisualParameters = bytes };
            var decoded = avatar.DecodeVisualParams();

            foreach (var vp in group0)
            {
                Assert.That(decoded.ContainsKey(vp.ParamID), Is.True,
                    $"ParamID {vp.ParamID} ({vp.Name}) missing from decoded result");
                var expected = Utils.ByteToFloat(
                    Utils.FloatToByte(vp.DefaultValue, vp.MinValue, vp.MaxValue),
                    vp.MinValue, vp.MaxValue);
                Assert.That(decoded[vp.ParamID], Is.EqualTo(expected).Within(1e-5f),
                    $"ParamID {vp.ParamID} ({vp.Name}) value mismatch");
            }
        }

        [Test]
        public void DecodeVisualParams_KeysAreGroup0ParamIDs()
        {
            var group0 = Group0Params;
            var avatar = new Avatar { VisualParameters = new byte[group0.Count] };

            var decoded = avatar.DecodeVisualParams();
            var expectedIds = new HashSet<int>(group0.Select(p => p.ParamID));

            Assert.That(new HashSet<int>(decoded.Keys), Is.EqualTo(expectedIds));
        }

        [Test]
        public void DecodeVisualParams_TruncatedArray_ReturnsPartialResult()
        {
            var group0 = Group0Params;
            const int partialCount = 10;
            var avatar = new Avatar { VisualParameters = new byte[partialCount] };

            var decoded = avatar.DecodeVisualParams();

            Assert.That(decoded.Count, Is.EqualTo(partialCount));
        }

        [Test]
        public void DecodeVisualParams_MinValue_DecodesNearParamMin()
        {
            var group0 = Group0Params;
            // encode 0x00 for all params — closest to MinValue after quantization
            var avatar = new Avatar { VisualParameters = new byte[group0.Count] };

            var decoded = avatar.DecodeVisualParams();

            foreach (var vp in group0)
            {
                Assert.That(decoded[vp.ParamID], Is.LessThanOrEqualTo(vp.MinValue + (vp.MaxValue - vp.MinValue) / 255f + 1e-5f),
                    $"ParamID {vp.ParamID} ({vp.Name}) decoded value exceeds expected range");
            }
        }

        [Test]
        public void DecodeVisualParams_MaxValue_DecodesNearParamMax()
        {
            var group0 = Group0Params;
            // encode 0xFF for all params — closest to MaxValue after quantization
            var bytes = Enumerable.Repeat((byte)0xFF, group0.Count).ToArray();
            var avatar = new Avatar { VisualParameters = bytes };

            var decoded = avatar.DecodeVisualParams();

            foreach (var vp in group0)
            {
                Assert.That(decoded[vp.ParamID], Is.GreaterThanOrEqualTo(vp.MaxValue - (vp.MaxValue - vp.MinValue) / 255f - 1e-5f),
                    $"ParamID {vp.ParamID} ({vp.Name}) decoded value below expected range");
            }
        }

        // ── AvatarAppearanceEventArgs.DecodeVisualParams ───────────────────────

        [Test]
        public void AvatarAppearanceEventArgs_DecodeVisualParams_EmptyList_ReturnsEmptyDictionary()
        {
            var args = MakeAppearanceEventArgs(new List<byte>());
            var decoded = args.DecodeVisualParams();
            Assert.That(decoded.Count, Is.EqualTo(0));
        }

        [Test]
        public void AvatarAppearanceEventArgs_DecodeVisualParams_RoundTrip_DefaultValues()
        {
            var group0 = Group0Params;
            var bytes = group0
                .Select(p => Utils.FloatToByte(p.DefaultValue, p.MinValue, p.MaxValue))
                .ToList();

            var args = MakeAppearanceEventArgs(bytes);
            var decoded = args.DecodeVisualParams();

            Assert.That(decoded.Count, Is.EqualTo(group0.Count));
            foreach (var vp in group0)
            {
                Assert.That(decoded.ContainsKey(vp.ParamID), Is.True,
                    $"ParamID {vp.ParamID} ({vp.Name}) missing from decoded result");
            }
        }

        [Test]
        public void AvatarAppearanceEventArgs_DecodeVisualParams_MatchesAvatarDecodeVisualParams()
        {
            var group0 = Group0Params;
            var bytes = group0
                .Select(p => Utils.FloatToByte(p.DefaultValue, p.MinValue, p.MaxValue))
                .ToList();

            var avatar = new Avatar { VisualParameters = bytes.ToArray() };
            var args = MakeAppearanceEventArgs(bytes);

            var fromAvatar = avatar.DecodeVisualParams();
            var fromArgs = args.DecodeVisualParams();

            Assert.That(fromArgs.Count, Is.EqualTo(fromAvatar.Count));
            foreach (var kvp in fromAvatar)
            {
                Assert.That(fromArgs.ContainsKey(kvp.Key), Is.True);
                Assert.That(fromArgs[kvp.Key], Is.EqualTo(kvp.Value).Within(1e-5f));
            }
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private static AvatarAppearanceEventArgs MakeAppearanceEventArgs(List<byte> visualParams)
        {
            return new AvatarAppearanceEventArgs(
                sim: null!,
                avatarID: UUID.Zero,
                isTrial: false,
                defaultTexture: new Primitive.TextureEntryFace(null),
                faceTextures: System.Array.Empty<Primitive.TextureEntryFace>(),
                visualParams: visualParams,
                appearanceVersion: 1,
                COFVersion: 0,
                appearanceFlags: AppearanceFlags.None,
                childCount: 0);
        }
    }
}
