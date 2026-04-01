using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV
{
    [TestFixture]
    public class RlvCommonTests
    {
        [TestCase("(spine)", RlvAttachmentPoint.Spine)]
        [TestCase("(spine)(spine)", RlvAttachmentPoint.Spine)]
        [TestCase("My (mouth) item (spine) has a lot of tags", RlvAttachmentPoint.Spine)]
        [TestCase("My item (l upper leg) and some random unknown tag (unknown)", RlvAttachmentPoint.LeftUpperLeg)]
        [TestCase("Central item (avatar center)", RlvAttachmentPoint.AvatarCenter)]
        [TestCase("Central item (root)", RlvAttachmentPoint.AvatarCenter)]
        public void TryGetRlvAttachmentPointFromItemName(string itemName, RlvAttachmentPoint expectedRlvAttachmentPoint)
        {
            Assert.That(RlvCommon.TryGetAttachmentPointFromItemName(itemName, out var actualRlvAttachmentPoint), Is.True);
            Assert.That(actualRlvAttachmentPoint, Is.EqualTo(expectedRlvAttachmentPoint));
        }

        [TestCase("(SPINE)", RlvAttachmentPoint.Spine)]
        [TestCase("(Spine)", RlvAttachmentPoint.Spine)]
        [TestCase("Central item (Avatar center)", RlvAttachmentPoint.AvatarCenter)]
        [TestCase("Another item (l upper leg)", RlvAttachmentPoint.LeftUpperLeg)]
        public void TryGetRlvAttachmentPointFromItemName_CaseSensitive(string itemName, RlvAttachmentPoint expectedRlvAttachmentPoint)
        {
            Assert.That(RlvCommon.TryGetAttachmentPointFromItemName(itemName, out var actualRlvAttachmentPoint), Is.True);
            Assert.That(actualRlvAttachmentPoint, Is.EqualTo(expectedRlvAttachmentPoint));
        }

        [TestCase("(unknown)(hand tag)(spine tag)")]
        [TestCase("Hat")]
        [TestCase("")]
        public void TryGetRlvAttachmentPointFromItemNameInvalid(string itemName)
        {
            Assert.That(RlvCommon.TryGetAttachmentPointFromItemName(itemName, out var actualRlvAttachmentPoint), Is.False);
        }
    }
}
