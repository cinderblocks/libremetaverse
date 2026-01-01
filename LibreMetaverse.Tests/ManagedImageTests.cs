/*
 * Unit tests for ManagedImage
 */

using NUnit.Framework;
using OpenMetaverse.Imaging;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Imaging")]
    public class ManagedImageTests
    {
        [Test]
        public void ExportRaw_RGBA_OrderAndFlip()
        {
            var img = new ManagedImage(2, 2, ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha);
            // internal layout: srcPos = h*Width + w with h starting at 0
            img.Red = new byte[] { 1, 2, 3, 4 };
            img.Green = new byte[] { 10, 20, 30, 40 };
            img.Blue = new byte[] { 100, 110, 120, 130 };
            img.Alpha = new byte[] { 200, 201, 202, 203 };

            var raw = img.ExportRaw();
            Assert.That(raw.Length, Is.EqualTo(2 * 2 * 4));

            // ExportRaw uses a bottom-left origin, flipping the rows vertically.
            // For width=2,height=2 the write order (pos) for (h,w):
            // h=0,w=0 -> pos = (1)*2 + 0 = 2 -> srcPos = 0
            // h=0,w=1 -> pos = 3 -> srcPos = 1
            // h=1,w=0 -> pos = 0 -> srcPos = 2
            // h=1,w=1 -> pos = 1 -> srcPos = 3

            byte[] expected = new byte[16];
            // pos 2 (pixels index 2)
            expected[2 * 4 + 0] = img.Red[0];
            expected[2 * 4 + 1] = img.Green[0];
            expected[2 * 4 + 2] = img.Blue[0];
            expected[2 * 4 + 3] = img.Alpha[0];
            // pos 3
            expected[3 * 4 + 0] = img.Red[1];
            expected[3 * 4 + 1] = img.Green[1];
            expected[3 * 4 + 2] = img.Blue[1];
            expected[3 * 4 + 3] = img.Alpha[1];
            // pos 0
            expected[0 * 4 + 0] = img.Red[2];
            expected[0 * 4 + 1] = img.Green[2];
            expected[0 * 4 + 2] = img.Blue[2];
            expected[0 * 4 + 3] = img.Alpha[2];
            // pos 1
            expected[1 * 4 + 0] = img.Red[3];
            expected[1 * 4 + 1] = img.Green[3];
            expected[1 * 4 + 2] = img.Blue[3];
            expected[1 * 4 + 3] = img.Alpha[3];

            Assert.That(raw, Is.EqualTo(expected));
        }

        [Test]
        public void ResizeNearestNeighbor_RepeatsSourcePixels()
        {
            var img = new ManagedImage(2, 2, ManagedImage.ImageChannels.Color);
            img.Red = new byte[] { 1, 2, 3, 4 };
            img.Green = new byte[] { 10, 20, 30, 40 };
            img.Blue = new byte[] { 100, 110, 120, 130 };

            img.ResizeNearestNeighbor(4, 4);

            Assert.That(img.Width, Is.EqualTo(4));
            Assert.That(img.Height, Is.EqualTo(4));
            Assert.That(img.Red.Length, Is.EqualTo(16));

            // Check a few mapped samples
            // new (x=0,y=0) -> srcX = 0, srcY = 0 -> src index 0
            Assert.That(img.Red[0], Is.EqualTo(1));
            // new (x=2,y=0) -> srcX = 1, srcY = 0 -> src index 1
            Assert.That(img.Red[2], Is.EqualTo(2));
            // new (x=0,y=2) -> srcX = 0, srcY = 1 -> src index 2
            Assert.That(img.Red[8], Is.EqualTo(3));
            // new (x=3,y=3) -> srcX = 1, srcY = 1 -> src index 3
            Assert.That(img.Red[15], Is.EqualTo(4));
        }

        [Test]
        public void ConvertChannels_AddsAlphaAndInitializesTo255()
        {
            var img = new ManagedImage(2, 2, ManagedImage.ImageChannels.Color);
            img.Red = new byte[4];
            img.Green = new byte[4];
            img.Blue = new byte[4];

            img.ConvertChannels(ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha);

            Assert.That(img.Alpha, Is.Not.Null);
            Assert.That(img.Alpha.Length, Is.EqualTo(4));
            foreach (var a in img.Alpha)
                Assert.That(a, Is.EqualTo(255));
        }
    }
}
