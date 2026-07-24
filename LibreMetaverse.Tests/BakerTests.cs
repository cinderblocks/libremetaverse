/*
 * Unit tests for Baker
 */

using NUnit.Framework;
using LibreMetaverse.Imaging;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Imaging")]
    public class BakerTests
    {
        [Test]
        public void Bake_GrayscaleLayerTexture_DoesNotThrow()
        {
            var layerImage = new ManagedImage(4, 4,
                ManagedImage.ImageChannels.Gray | ManagedImage.ImageChannels.Alpha);
            for (int i = 0; i < layerImage.Red.Length; i++) layerImage.Red[i] = 128;
            for (int i = 0; i < layerImage.Alpha.Length; i++) layerImage.Alpha[i] = 255;

            var textureData = new AppearanceManager.TextureData
            {
                Texture = new Assets.AssetTexture(layerImage),
                Color = new Color4(0.5f, 0.25f, 0.75f, 1f),
                TextureIndex = AvatarTextureIndex.UpperShirt
            };

            var baker = new Baker(BakeType.UpperBody);
            baker.AddTexture(textureData);

            Assert.DoesNotThrow(() => baker.Bake());
            Assert.That(baker.BakedTexture, Is.Not.Null);
        }

        [Test]
        public void Bake_GrayscaleHairTextureNoSkin_DoesNotThrow()
        {
            var hairImage = new ManagedImage(4, 4,
                ManagedImage.ImageChannels.Gray | ManagedImage.ImageChannels.Alpha);
            for (int i = 0; i < hairImage.Red.Length; i++) hairImage.Red[i] = 200;
            for (int i = 0; i < hairImage.Alpha.Length; i++) hairImage.Alpha[i] = 255;

            var textureData = new AppearanceManager.TextureData
            {
                Texture = new Assets.AssetTexture(hairImage),
                Color = Color4.White,
                TextureIndex = AvatarTextureIndex.Hair
            };

            // No HeadBodypaint texture is added, so skinTexture.Texture stays null and
            // Bake() takes the head-bake hair branch that calls MultiplyLayerFromAlpha
            // against the (grayscale) hair texture.
            var baker = new Baker(BakeType.Head);
            baker.AddTexture(textureData);

            Assert.DoesNotThrow(() => baker.Bake());
            Assert.That(baker.BakedTexture, Is.Not.Null);
        }

        [Test]
        public void LoadResourceLayer_HeadColorTga_LoadsFromCharacterDirectory()
        {
            var image = Baker.LoadResourceLayer("head_color.tga");
            Assert.That(image, Is.Not.Null);
        }
    }
}
