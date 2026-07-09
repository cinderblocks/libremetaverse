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

using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for AssetMaterial's override-related additions (override-aware setters,
    /// ApplyOverride, SetBaseMaterial, and the compact ToOverrideOsd/FromOverrideOsd LLSD shape used
    /// by the ModifyRegion capability). Verified field-for-field against the reference viewer's
    /// LLGLTFMaterial (indra/llprimitive/llgltfmaterial.cpp): overriding a scalar to exactly its
    /// GLTF-spec default value nudges it by float epsilon so it stays distinguishable, after a round
    /// trip, from a field that was never touched.
    /// </summary>
    [TestFixture]
    public class AssetMaterialOverrideTests
    {
        [Test]
        public void SetBaseColorFactor_OverrideToDefault_IsNudgedOffDefault()
        {
            var mat = new AssetMaterial();
            mat.SetBaseColorFactor(AssetMaterial.Default.BaseColorFactor, forOverride: true);

            Assert.That(mat.BaseColorFactor, Is.Not.EqualTo(AssetMaterial.Default.BaseColorFactor));
        }

        [Test]
        public void SetBaseColorFactor_NotOverride_LeavesExactDefaultUnchanged()
        {
            var mat = new AssetMaterial();
            mat.SetBaseColorFactor(AssetMaterial.Default.BaseColorFactor);

            Assert.That(mat.BaseColorFactor, Is.EqualTo(AssetMaterial.Default.BaseColorFactor));
        }

        [Test]
        public void SetMetallicFactor_OverrideToOne_ClampsBelowDefault()
        {
            var mat = new AssetMaterial();
            mat.SetMetallicFactor(1f, forOverride: true);

            Assert.That(mat.MetallicFactor, Is.LessThan(1f));
            Assert.That(mat.MetallicFactor, Is.Not.EqualTo(AssetMaterial.Default.MetallicFactor));
        }

        [Test]
        public void SetAlphaMode_OverrideToDefaultOpaque_SetsOverrideFlag()
        {
            var mat = new AssetMaterial();
            mat.SetAlphaMode(GltfAlphaMode.Opaque, forOverride: true);

            Assert.That(mat.OverrideAlphaMode, Is.True);
        }

        [Test]
        public void SetTextureId_OverrideToNull_UsesSentinelUuid()
        {
            var mat = new AssetMaterial();
            mat.SetTextureId(AssetMaterial.TEXTURE_BASE_COLOR, UUID.Zero, forOverride: true);

            Assert.That(mat.TextureIds[AssetMaterial.TEXTURE_BASE_COLOR], Is.EqualTo(AssetMaterial.GLTF_OVERRIDE_NULL_UUID));
        }

        [Test]
        public void ApplyOverride_SentinelTexture_ClearsBaseTexture()
        {
            var baseMat = new AssetMaterial();
            baseMat.SetTextureId(AssetMaterial.TEXTURE_BASE_COLOR, UUID.Random());

            var overrideMat = new AssetMaterial();
            overrideMat.SetTextureId(AssetMaterial.TEXTURE_BASE_COLOR, UUID.Zero, forOverride: true);

            baseMat.ApplyOverride(overrideMat);

            Assert.That(baseMat.TextureIds[AssetMaterial.TEXTURE_BASE_COLOR], Is.EqualTo(UUID.Zero));
        }

        [Test]
        public void ApplyOverride_UntouchedTexture_LeavesBaseTextureAlone()
        {
            var originalId = UUID.Random();
            var baseMat = new AssetMaterial();
            baseMat.SetTextureId(AssetMaterial.TEXTURE_BASE_COLOR, originalId);

            var overrideMat = new AssetMaterial(); // no textures touched -- all zero, i.e. "no override"
            baseMat.ApplyOverride(overrideMat);

            Assert.That(baseMat.TextureIds[AssetMaterial.TEXTURE_BASE_COLOR], Is.EqualTo(originalId));
        }

        [Test]
        public void ApplyOverride_ChangedScalar_ReplacesBaseValue()
        {
            var baseMat = new AssetMaterial();
            var overrideMat = new AssetMaterial();
            overrideMat.SetRoughnessFactor(0.3f, forOverride: true);

            baseMat.ApplyOverride(overrideMat);

            Assert.That(baseMat.RoughnessFactor, Is.EqualTo(0.3f));
        }

        [Test]
        public void SetBaseMaterial_ResetsFieldsButKeepsTransforms()
        {
            var mat = new AssetMaterial { Name = "Foo" };
            mat.SetRoughnessFactor(0.2f, forOverride: true);
            mat.SetTextureOffset(AssetMaterial.TEXTURE_BASE_COLOR, new Vector2(0.5f, 0.25f));

            mat.SetBaseMaterial();

            Assert.That(mat.Name, Is.EqualTo(string.Empty));
            Assert.That(mat.RoughnessFactor, Is.EqualTo(AssetMaterial.Default.RoughnessFactor));
            Assert.That(mat.TextureTransforms[AssetMaterial.TEXTURE_BASE_COLOR].Offset, Is.EqualTo(new Vector2(0.5f, 0.25f)));
        }

        [Test]
        public void ToOverrideOsd_RoundTrip_PreservesOverriddenFields()
        {
            var mat = new AssetMaterial();
            mat.SetRoughnessFactor(0.4f, forOverride: true);
            mat.SetMetallicFactor(0.6f, forOverride: true);
            var textureId = UUID.Random();
            mat.SetTextureId(AssetMaterial.TEXTURE_EMISSIVE, textureId, forOverride: true);
            mat.SetTextureRotation(AssetMaterial.TEXTURE_EMISSIVE, 1.25f);

            var osd = mat.ToOverrideOsd();
            var roundTripped = AssetMaterial.FromOverrideOsd(osd);

            Assert.That(roundTripped.RoughnessFactor, Is.EqualTo(0.4f));
            Assert.That(roundTripped.MetallicFactor, Is.EqualTo(0.6f));
            Assert.That(roundTripped.TextureIds[AssetMaterial.TEXTURE_EMISSIVE], Is.EqualTo(textureId));
            Assert.That(roundTripped.TextureTransforms[AssetMaterial.TEXTURE_EMISSIVE].Rotation, Is.EqualTo(1.25f));
        }

        [Test]
        public void ToOverrideOsd_UntouchedMaterial_ProducesEmptyMap()
        {
            var mat = new AssetMaterial();
            var osd = mat.ToOverrideOsd();

            Assert.That(osd.Count, Is.EqualTo(0));
        }

        [Test]
        public void ToOverrideOsd_OverrideToDefaultRoughness_StillEncodesField()
        {
            var mat = new AssetMaterial();
            mat.SetRoughnessFactor(AssetMaterial.Default.RoughnessFactor, forOverride: true);

            var osd = mat.ToOverrideOsd();

            Assert.That(osd.ContainsKey("rf"), Is.True, "an explicit override to the default value must still round-trip as an override");

            var roundTripped = AssetMaterial.FromOverrideOsd(osd);
            Assert.That(roundTripped.RoughnessFactor, Is.Not.EqualTo(AssetMaterial.Default.RoughnessFactor));
        }

        [Test]
        public void ToJson_RoundTrip_PreservesNameAndColor()
        {
            var mat = new AssetMaterial { Name = "Chrome" };
            mat.SetBaseColorFactor(new Color4(0.1f, 0.2f, 0.3f, 1f));

            var json = mat.ToJson();
            var decoded = new AssetMaterial(UUID.Random(), System.Text.Encoding.UTF8.GetBytes(json));

            Assert.That(decoded.Name, Is.EqualTo("Chrome"));
            Assert.That(decoded.BaseColorFactor, Is.EqualTo(mat.BaseColorFactor));
        }
    }
}
