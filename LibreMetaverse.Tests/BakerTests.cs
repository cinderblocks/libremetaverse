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
        public void LoadResourceLayer_HeadColorTga_LoadsFromCharacterDirectory()
        {
            var image = Baker.LoadResourceLayer("head_color.tga");
            Assert.That(image, Is.Not.Null);
        }
    }
}
