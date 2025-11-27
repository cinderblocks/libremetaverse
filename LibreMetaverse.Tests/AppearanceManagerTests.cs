using NUnit.Framework;
using OpenMetaverse;

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
    }
}
