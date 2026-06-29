using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class AppearanceManagerTests
    {
        [Test]
        public void WearableTypeToAssetType_BodypartsAndClothing_ReturnsExpected()
        {
            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Shape), Is.EqualTo(AssetType.Bodypart));
            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Skin), Is.EqualTo(AssetType.Bodypart));
            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Hair), Is.EqualTo(AssetType.Bodypart));
            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Eyes), Is.EqualTo(AssetType.Bodypart));

            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Shirt), Is.EqualTo(AssetType.Clothing));
            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Pants), Is.EqualTo(AssetType.Clothing));
            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Shoes), Is.EqualTo(AssetType.Clothing));
            Assert.That(AppearanceManager.WearableTypeToAssetType(WearableType.Tattoo), Is.EqualTo(AssetType.Clothing));
        }

        [Test]
        public void BakeIndexToTextureIndex_Matches_BakeTypeToAgentTextureIndex()
        {
            for (int bakedIndex = 0; bakedIndex < AppearanceManager.BAKED_TEXTURE_COUNT; bakedIndex++)
            {
                var expected = (byte)AppearanceManager.BakeTypeToAgentTextureIndex((BakeType)bakedIndex);
                Assert.That(AppearanceManager.BakeIndexToTextureIndex[bakedIndex], Is.EqualTo(expected),
                    $"BakeIndexToTextureIndex[{bakedIndex}] should map to BakeTypeToAgentTextureIndex for BakeType {(BakeType)bakedIndex}");
            }
        }

        [Test]
        public void BakeIndex_ArrayLength_Equals_BAKED_TEXTURE_COUNT()
        {
            Assert.That(AppearanceManager.BakeIndexToTextureIndex.Length, Is.EqualTo(AppearanceManager.BAKED_TEXTURE_COUNT));
        }

        [Test]
        public void BakeTypeToTextures_Head_IncludesExpectedSlots()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.Head);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.HeadBodypaint));
            Assert.That(textures, Does.Contain(AvatarTextureIndex.HeadTattoo));
            Assert.That(textures, Does.Contain(AvatarTextureIndex.Hair));
            Assert.That(textures, Does.Contain(AvatarTextureIndex.HeadAlpha));
        }

        [Test]
        public void BakeTypeToTextures_UpperBody_IncludesExpectedSlots()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.UpperBody);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.UpperBodypaint));
            Assert.That(textures, Does.Contain(AvatarTextureIndex.UpperTattoo));
            Assert.That(textures, Does.Contain(AvatarTextureIndex.UpperShirt));
            Assert.That(textures, Does.Contain(AvatarTextureIndex.UpperAlpha));
        }

        [Test]
        public void BakeTypeToTextures_LowerBody_Eyes_Skirt_Hair_IncludeExpected()
        {
            var lower = AppearanceManager.BakeTypeToTextures(BakeType.LowerBody);
            Assert.That(lower, Does.Contain(AvatarTextureIndex.LowerPants));
            Assert.That(lower, Does.Contain(AvatarTextureIndex.LowerShoes));

            var eyes = AppearanceManager.BakeTypeToTextures(BakeType.Eyes);
            Assert.That(eyes, Does.Contain(AvatarTextureIndex.EyesIris));
            Assert.That(eyes, Does.Contain(AvatarTextureIndex.EyesAlpha));

            var skirt = AppearanceManager.BakeTypeToTextures(BakeType.Skirt);
            Assert.That(skirt, Does.Contain(AvatarTextureIndex.Skirt));

            var hair = AppearanceManager.BakeTypeToTextures(BakeType.Hair);
            Assert.That(hair, Does.Contain(AvatarTextureIndex.Hair));
            Assert.That(hair, Does.Contain(AvatarTextureIndex.HairAlpha));
        }

        [Test]
        public void MorphLayerForBakeType_ReturnsExpectedIndices()
        {
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.Head), Is.EqualTo(AvatarTextureIndex.Hair));
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.UpperBody), Is.EqualTo(AvatarTextureIndex.UpperShirt));
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.LowerBody), Is.EqualTo(AvatarTextureIndex.LowerPants));
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.Skirt), Is.EqualTo(AvatarTextureIndex.Skirt));
        }

        [Test]
        public void WearableBakeMap_HasExpectedDimensions_And_FirstLayerContainsShapeAndSkin()
        {
            Assert.That(AppearanceManager.WEARABLE_BAKE_MAP.Length, Is.EqualTo(AppearanceManager.BAKED_TEXTURE_COUNT));
            Assert.That(AppearanceManager.WEARABLE_BAKE_MAP[0], Does.Contain(WearableType.Shape));
            Assert.That(AppearanceManager.WEARABLE_BAKE_MAP[0], Does.Contain(WearableType.Skin));
        }

        [Test]
        public void BakeTypeToAgentTextureIndex_ReturnsUnknown_ForInvalid()
        {
            var unknown = AppearanceManager.BakeTypeToAgentTextureIndex((BakeType)(-999));
            Assert.That(unknown, Is.EqualTo(AvatarTextureIndex.Unknown));
        }

        // ── Extended bake support (11 bakes) ─────────────────────────────────

        [Test]
        public void BAKED_TEXTURE_COUNT_Is11()
        {
            Assert.That(AppearanceManager.BAKED_TEXTURE_COUNT, Is.EqualTo(11));
        }

        [Test]
        public void BakedTextureHash_Has11Entries()
        {
            Assert.That(AppearanceManager.BAKED_TEXTURE_HASH.Length, Is.EqualTo(11));
        }

        [Test]
        public void BakedTextureHash_AllEntriesNonZero()
        {
            foreach (var hash in AppearanceManager.BAKED_TEXTURE_HASH)
                Assert.That(hash, Is.Not.EqualTo(UUID.Zero), "Every bake hash must be non-zero");
        }

        [Test]
        public void WearableBakeMap_Has11Rows()
        {
            Assert.That(AppearanceManager.WEARABLE_BAKE_MAP.Length, Is.EqualTo(11));
        }

        [Test]
        public void WearableBakeMap_ExtendedRows_AreAllInvalid()
        {
            for (int i = 6; i < 11; i++)
            {
                foreach (var wt in AppearanceManager.WEARABLE_BAKE_MAP[i])
                    Assert.That(wt, Is.EqualTo(WearableType.Invalid),
                        $"WEARABLE_BAKE_MAP[{i}] (extended bake) should be all-Invalid");
            }
        }

        [Test]
        public void BakeIndexToTextureIndex_ExtendedBakes_MatchAgentTextureIndex()
        {
            // Indexes 6-10 are the extended bakes; they must agree with BakeTypeToAgentTextureIndex.
            for (int i = 6; i < AppearanceManager.BAKED_TEXTURE_COUNT; i++)
            {
                var expected = (byte)AppearanceManager.BakeTypeToAgentTextureIndex((BakeType)i);
                Assert.That(AppearanceManager.BakeIndexToTextureIndex[i], Is.EqualTo(expected),
                    $"BakeIndexToTextureIndex[{i}] ({(BakeType)i})");
            }
        }

        // ── Universal tattoo layers ───────────────────────────────────────────

        [Test]
        public void BakeTypeToTextures_Head_IncludesUniversalTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.Head);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.HeadUniversalTattoo));
        }

        [Test]
        public void BakeTypeToTextures_UpperBody_IncludesUniversalTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.UpperBody);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.UpperUniversalTattoo));
        }

        [Test]
        public void BakeTypeToTextures_LowerBody_IncludesUniversalTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.LowerBody);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.LowerUniversalTattoo));
        }

        [Test]
        public void BakeTypeToTextures_Eyes_IncludesTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.Eyes);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.EyesTattoo));
        }

        [Test]
        public void BakeTypeToTextures_Skirt_IncludesTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.Skirt);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.SkirtTattoo));
        }

        [Test]
        public void BakeTypeToTextures_Hair_IncludesHairTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.Hair);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.HairTattoo));
        }

        // ── Extended bake BakeTypeToTextures ─────────────────────────────────

        [Test]
        public void BakeTypeToTextures_BakedLeftArm_ContainsLeftArmTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.BakedLeftArm);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.LeftArmTattoo));
            Assert.That(textures.Count, Is.EqualTo(1));
        }

        [Test]
        public void BakeTypeToTextures_BakedLeftLeg_ContainsLeftLegTattoo()
        {
            var textures = AppearanceManager.BakeTypeToTextures(BakeType.BakedLeftLeg);
            Assert.That(textures, Does.Contain(AvatarTextureIndex.LeftLegTattoo));
        }

        [Test]
        public void BakeTypeToTextures_BakedAux1_ContainsAux1Tattoo()
        {
            Assert.That(AppearanceManager.BakeTypeToTextures(BakeType.BakedAux1),
                Does.Contain(AvatarTextureIndex.Aux1Tattoo));
        }

        [Test]
        public void BakeTypeToTextures_BakedAux2_ContainsAux2Tattoo()
        {
            Assert.That(AppearanceManager.BakeTypeToTextures(BakeType.BakedAux2),
                Does.Contain(AvatarTextureIndex.Aux2Tattoo));
        }

        [Test]
        public void BakeTypeToTextures_BakedAux3_ContainsAux3Tattoo()
        {
            Assert.That(AppearanceManager.BakeTypeToTextures(BakeType.BakedAux3),
                Does.Contain(AvatarTextureIndex.Aux3Tattoo));
        }

        [Test]
        public void MorphLayerForBakeType_ExtendedBakes_ReturnTattooSlots()
        {
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.BakedLeftArm),
                Is.EqualTo(AvatarTextureIndex.LeftArmTattoo));
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.BakedLeftLeg),
                Is.EqualTo(AvatarTextureIndex.LeftLegTattoo));
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.BakedAux1),
                Is.EqualTo(AvatarTextureIndex.Aux1Tattoo));
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.BakedAux2),
                Is.EqualTo(AvatarTextureIndex.Aux2Tattoo));
            Assert.That(AppearanceManager.MorphLayerForBakeType(BakeType.BakedAux3),
                Is.EqualTo(AvatarTextureIndex.Aux3Tattoo));
        }

        // ── IBakingTextureProvider ────────────────────────────────────────────

        [Test]
        public void TextureProvider_Default_IsGridClientBakingTextureProvider()
        {
            var client = new GridClient();
            Assert.That(client.Appearance.TextureProvider,
                Is.InstanceOf<GridClientBakingTextureProvider>());
        }

        [Test]
        public void TextureProvider_CanBeReplaced()
        {
            var client = new GridClient();
            var mock = new Moq.Mock<IBakingTextureProvider>();
            client.Appearance.TextureProvider = mock.Object;
            Assert.That(client.Appearance.TextureProvider, Is.SameAs(mock.Object));
        }

        // ── LastUpdateReceivedCOFVersion / UpdateLastReceivedCOFVersion ────────

        [Test]
        public void LastUpdateReceivedCOFVersion_InitialValue_IsMinusOne()
        {
            var client = new GridClient();
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(-1));
        }

        [Test]
        public void UpdateLastReceivedCOFVersion_HigherVersion_Updates()
        {
            var client = new GridClient();
            client.Appearance.UpdateLastReceivedCOFVersion(5);
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(5));
        }

        [Test]
        public void UpdateLastReceivedCOFVersion_LowerVersion_DoesNotUpdate()
        {
            var client = new GridClient();
            client.Appearance.UpdateLastReceivedCOFVersion(10);
            client.Appearance.UpdateLastReceivedCOFVersion(7);
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(10));
        }

        [Test]
        public void UpdateLastReceivedCOFVersion_SameVersion_DoesNotChange()
        {
            var client = new GridClient();
            client.Appearance.UpdateLastReceivedCOFVersion(5);
            client.Appearance.UpdateLastReceivedCOFVersion(5);
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(5));
        }

        [Test]
        public void UpdateLastReceivedCOFVersion_MonotonicallyIncreasing_TracksPeak()
        {
            var client = new GridClient();
            var versions = new[] { 1, 3, 2, 7, 5, 10, 9 };
            foreach (var v in versions)
                client.Appearance.UpdateLastReceivedCOFVersion(v);
            Assert.That(client.Appearance.LastUpdateReceivedCOFVersion, Is.EqualTo(10));
        }
    }
}
