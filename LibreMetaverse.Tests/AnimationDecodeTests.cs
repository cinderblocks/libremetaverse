using System;
using System.IO;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The joint and keyframe counts of an animation come from the asset, so they must not be trusted.
    /// </summary>
    [TestFixture]
    public class AnimationDecodeTests
    {
        private static byte[] Animation(uint jointCount, Action<BinaryWriter> joints = null)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((ushort)1);
                bw.Write((ushort)0);
                bw.Write(2);            // priority
                bw.Write(1f);           // length
                bw.Write((byte)0);      // expression name
                bw.Write(0f);           // in point
                bw.Write(1f);           // out point
                bw.Write(0);            // loop
                bw.Write(0f);           // ease in
                bw.Write(0f);           // ease out
                bw.Write(0u);           // hand pose
                bw.Write(jointCount);
                joints?.Invoke(bw);
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static void Joint(BinaryWriter bw, string name, int rotationKeys, int positionKeys)
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes(name));
            bw.Write((byte)0);
            bw.Write(1);                // priority
            bw.Write(rotationKeys);
            for (int i = 0; i < rotationKeys; i++) { bw.Write((ushort)(i * 1000)); bw.Write((ushort)100); bw.Write((ushort)200); bw.Write((ushort)300); }
            bw.Write(positionKeys);
            for (int i = 0; i < positionKeys; i++) { bw.Write((ushort)(i * 1000)); bw.Write((ushort)400); bw.Write((ushort)500); bw.Write((ushort)600); }
        }

        [Test]
        public void WellFormedAnimation_Decodes()
        {
            var data = Animation(2, bw =>
            {
                Joint(bw, "mPelvis", 3, 2);
                Joint(bw, "mTorso", 0, 1);
            });

            var anim = new BinBVHAnimationReader(data);

            Assert.That(anim.joints.Length, Is.EqualTo(2));
            Assert.That(anim.joints[0].Name, Is.EqualTo("mPelvis"));
            Assert.That(anim.joints[0].rotationkeys.Length, Is.EqualTo(3));
            Assert.That(anim.joints[0].positionkeys.Length, Is.EqualTo(2));
            Assert.That(anim.joints[1].Name, Is.EqualTo("mTorso"));
            Assert.That(anim.joints[1].positionkeys.Length, Is.EqualTo(1));
        }

        [TestCase(uint.MaxValue)]
        [TestCase(2147483647u)]
        [TestCase(1000000u)]
        [TestCase(BinBVHAnimationReader.MaxJoints + 1u)]
        public void JointCountLargerThanTheDataOrTheLimit_IsRefused(uint count)
        {
            // a few dozen bytes that claim a very large number of joints
            Assert.Throws<InvalidDataException>(() => new BinBVHAnimationReader(Animation(count)));
        }

        [Test]
        public void JointCountLargerThanTheRemainingData_IsRefused()
        {
            // two joints' worth of data, three declared
            var data = Animation(3, bw => { Joint(bw, "a", 0, 0); Joint(bw, "b", 0, 0); });
            Assert.Throws<InvalidDataException>(() => new BinBVHAnimationReader(data));
        }

        [Test]
        public void KeyframeCountLargerThanTheRemainingData_IsRefused()
        {
            var data = Animation(1, bw =>
            {
                bw.Write((byte)'j'); bw.Write((byte)0);
                bw.Write(1);
                bw.Write(9000);         // 9000 rotation keys (within the per-joint limit) ...
                bw.Write(new byte[16]); // ... but only two keys of data
            });

            Assert.Throws<InvalidDataException>(() => new BinBVHAnimationReader(data));
        }

        [Test]
        public void TruncatedAnimations_NeverThrowAnythingButAnException_AndNeverHang()
        {
            var full = Animation(2, bw => { Joint(bw, "mPelvis", 3, 2); Joint(bw, "mTorso", 1, 1); });
            for (int length = 0; length < full.Length; length++)
            {
                var truncated = new byte[length];
                Array.Copy(full, truncated, length);
                try { new BinBVHAnimationReader(truncated); }
                catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException || ex is IndexOutOfRangeException) { }
            }
        }

        [Test]
        public void RandomData_IsRefusedQuickly()
        {
            var random = new Random(3);
            for (int i = 0; i < 300; i++)
            {
                var data = new byte[random.Next(0, 200)];
                random.NextBytes(data);
                try { new BinBVHAnimationReader(data); }
                catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException || ex is IndexOutOfRangeException || ex is OverflowException) { }
            }
        }
    }
}
