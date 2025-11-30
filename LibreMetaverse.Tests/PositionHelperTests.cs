using NUnit.Framework;
using OpenMetaverse;
using LibreMetaverse;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Utilities")]
    public class PositionHelperTests
    {
        [Test]
        public void ToGlobalAndLocalPosition_RoundTrip()
        {
            uint gx = 512 * Simulator.DefaultRegionSizeX; // use a multiple
            uint gy = 1024 * Simulator.DefaultRegionSizeY;
            ulong handle = Utils.UIntsToLong(gx, gy);

            var local = new Vector3(10.5f, 20.25f, 5.0f);
            var global = PositionHelper.ToGlobalPosition(handle, local);

            Assert.That(global.X, Is.EqualTo(gx + local.X).Within(0.0001));
            Assert.That(global.Y, Is.EqualTo(gy + local.Y).Within(0.0001));
            Assert.That(global.Z, Is.EqualTo(local.Z).Within(0.0001));

            var backLocal = PositionHelper.ToLocalPosition(handle, global);
            Assert.That(backLocal.X, Is.EqualTo(local.X).Within(0.0001));
            Assert.That(backLocal.Y, Is.EqualTo(local.Y).Within(0.0001));
            Assert.That(backLocal.Z, Is.EqualTo(local.Z).Within(0.0001));
        }

        [Test]
        public void FormatCoordinates_FormatsCorrectly()
        {
            Assert.That(PositionHelper.FormatCoordinates(), Is.EqualTo(string.Empty));
            Assert.That(PositionHelper.FormatCoordinates(10), Is.EqualTo(" (10)"));
            Assert.That(PositionHelper.FormatCoordinates(10, 20), Is.EqualTo(" (10,20)"));
            Assert.That(PositionHelper.FormatCoordinates(10, 20, 30), Is.EqualTo(" (10,20,30)"));
        }
    }
}
