using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class FarTouchRestrictionTests : RlvTestBase
    {
        #region  @touchfar @fartouch[:max_distance]=<y/n>

        [TestCase("fartouch")]
        [TestCase("touchfar")]
        public async Task CanFarTouch(string command)
        {
            await _rlv.ProcessMessage($"@{command}:0.9=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetMaxFarTouchDistance(out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(0.9f));
        }

        [TestCase("fartouch")]
        [TestCase("touchfar")]
        public async Task CanFarTouch_Synonym(string command)
        {
            await _rlv.ProcessMessage($"@{command}:0.9=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetMaxFarTouchDistance(out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(0.9f));
        }

        [TestCase("fartouch")]
        [TestCase("touchfar")]
        public async Task CanFarTouch_Default(string command)
        {
            await _rlv.ProcessMessage($"@{command}=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetMaxFarTouchDistance(out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(1.5f));
        }

        [TestCase("fartouch", "fartouch")]
        [TestCase("fartouch", "touchfar")]
        [TestCase("touchfar", "touchfar")]
        [TestCase("touchfar", "fartouch")]
        public async Task CanFarTouch_Multiple_Synonyms(string command1, string command2)
        {
            await _rlv.ProcessMessage($"@{command1}:12.34=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@{command2}:6.78=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetMaxFarTouchDistance(out var actualDistance2), Is.True);

            await _rlv.ProcessMessage($"@{command1}:6.78=y", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetMaxFarTouchDistance(out var actualDistance1), Is.True);

            Assert.That(actualDistance1, Is.EqualTo(12.34f).Within(FloatTolerance));
            Assert.That(actualDistance2, Is.EqualTo(6.78f).Within(FloatTolerance));
        }

        #endregion

    }
}
