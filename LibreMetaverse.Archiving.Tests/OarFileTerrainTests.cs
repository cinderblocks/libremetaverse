using System;
using System.Reflection;
using NUnit.Framework;
using LibreMetaverse.Assets;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("OarFile")]
    public class OarFileTerrainTests
    {
        private static bool InvokeLoadTerrain(string filePath, byte[] data, out float[,]? terrain)
        {
            var method = typeof(OarFile).GetMethod("LoadTerrain", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "OarFile.LoadTerrain method not found via reflection");

            float[,]? captured = null;
            OarFile.TerrainLoadedCallback callback = (t, _, _) => captured = t;

            var result = (bool)method!.Invoke(null, new object[] { filePath, data, callback, 0L, (long)data.Length })!;
            terrain = captured;
            return result;
        }

        private static byte[] MakeRaw32Terrain(int side, float fillValue)
        {
            var data = new byte[side * side * 4];
            int pos = 0;
            for (int i = 0; i < side * side; i++)
            {
                var bytes = Utils.FloatToBytes(fillValue);
                Array.Copy(bytes, 0, data, pos, 4);
                pos += 4;
            }
            return data;
        }

        [Test]
        public void LoadTerrain_StandardRegion_Loads256x256()
        {
            var data = MakeRaw32Terrain(256, 21.0f);

            var loaded = InvokeLoadTerrain("terrains/region.r32", data, out var terrain);

            Assert.That(loaded, Is.True);
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain!.GetLength(0), Is.EqualTo(256));
            Assert.That(terrain.GetLength(1), Is.EqualTo(256));
            Assert.That(terrain[0, 0], Is.EqualTo(21.0f));
        }

        [Test]
        public void LoadTerrain_Varregion_Loads512x512()
        {
            var data = MakeRaw32Terrain(512, 30.0f);

            var loaded = InvokeLoadTerrain("terrains/megaregion.f32", data, out var terrain);

            Assert.That(loaded, Is.True);
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain!.GetLength(0), Is.EqualTo(512));
            Assert.That(terrain.GetLength(1), Is.EqualTo(512));
        }

        [Test]
        public void LoadTerrain_WrongByteCount_FailsGracefully()
        {
            var data = new byte[256 * 256 * 4 + 3];

            var loaded = InvokeLoadTerrain("terrains/broken.r32", data, out var terrain);

            Assert.That(loaded, Is.False);
            Assert.That(terrain, Is.Null);
        }

        [Test]
        public void LoadTerrain_NonSquarePostCount_FailsGracefully()
        {
            // A byte count divisible by 4 whose post count isn't a perfect square.
            var data = new byte[257 * 256 * 4];

            var loaded = InvokeLoadTerrain("terrains/nonsquare.r32", data, out var terrain);

            Assert.That(loaded, Is.False);
            Assert.That(terrain, Is.Null);
        }
    }
}
