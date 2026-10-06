using System;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class MuteListParsingTests
    {
        private static readonly UUID Id = new UUID("6a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");

        [Test]
        public void Entry_WithFlags_ReadsFlags()
        {
            var entry = AgentManager.ParseMuteListEntry($"1 {Id} Some Avatar|3");

            Assert.That(entry.Type, Is.EqualTo(MuteType.Resident));
            Assert.That(entry.ID, Is.EqualTo(Id));
            Assert.That(entry.Name, Is.EqualTo("Some Avatar"));
            Assert.That(entry.Flags, Is.EqualTo(MuteFlags.TextChat | MuteFlags.VoiceChat));
        }

        [TestCase(0, MuteFlags.Default)]
        [TestCase(1, MuteFlags.TextChat)]
        [TestCase(4, MuteFlags.Particles)]
        [TestCase(15, MuteFlags.All)]
        public void Entry_FlagsValues(int flags, MuteFlags expected)
        {
            var entry = AgentManager.ParseMuteListEntry($"0 {Id} Name|{flags}");

            Assert.That(entry.Flags, Is.EqualTo(expected));
        }

        [Test]
        public void Entry_WithoutFlags_DefaultsToNoFlags()
        {
            var entry = AgentManager.ParseMuteListEntry($"2 {Id} Annoying Object");

            Assert.That(entry.Type, Is.EqualTo(MuteType.Object));
            Assert.That(entry.Name, Is.EqualTo("Annoying Object"));
            Assert.That(entry.Flags, Is.EqualTo(MuteFlags.Default));
        }

        [Test]
        public void Entry_NameIsNotSwallowedByFlags()
        {
            var entry = AgentManager.ParseMuteListEntry($"1 {Id} Name With  Spaces|8");

            Assert.That(entry.Name, Is.EqualTo("Name With  Spaces"));
            Assert.That(entry.Flags, Is.EqualTo(MuteFlags.ObjectSounds));
        }

        [TestCase("")]
        [TestCase("not a mute entry")]
        [TestCase("1")]
        [TestCase("|3")]
        public void InvalidLine_Throws(string line)
        {
            Assert.That(() => AgentManager.ParseMuteListEntry(line), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void LeadingWhitespace_IsAccepted()
        {
            var entry = AgentManager.ParseMuteListEntry($"  1 {Id} Some Avatar|3");
            Assert.That(entry.Name, Is.EqualTo("Some Avatar"));
        }

        [TestCase(200000, "")]
        [TestCase(200000, " ")]
        [TestCase(200000, " 1")]
        public void LongRunOfDigits_IsRefusedQuickly(int digits, string suffix)
        {
            // an unanchored pattern retries from every digit, which takes minutes for a line this long
            var line = new string('1', digits) + suffix;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Assert.That(() => AgentManager.ParseMuteListEntry(line), Throws.InstanceOf<ArgumentException>());

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
        }
    }
}
