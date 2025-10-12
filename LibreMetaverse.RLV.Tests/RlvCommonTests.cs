namespace LibreMetaverse.RLV.Tests
{
    public class RlvCommonTests
    {
        [Theory]
        [InlineData("(spine)", RlvAttachmentPoint.Spine)]
        [InlineData("(spine)(spine)", RlvAttachmentPoint.Spine)]
        [InlineData("My (mouth) item (spine) has a lot of tags", RlvAttachmentPoint.Spine)]
        [InlineData("My item (l upper leg) and some random unknown tag (unknown)", RlvAttachmentPoint.LeftUpperLeg)]
        [InlineData("Central item (avatar center)", RlvAttachmentPoint.AvatarCenter)]
        [InlineData("Central item (root)", RlvAttachmentPoint.AvatarCenter)]
        public void TryGetRlvAttachmentPointFromItemName(string itemName, RlvAttachmentPoint expectedRlvAttachmentPoint)
        {
            Assert.True(RlvCommon.TryGetAttachmentPointFromItemName(itemName, out var actualRlvAttachmentPoint));
            Assert.Equal(expectedRlvAttachmentPoint, actualRlvAttachmentPoint);
        }

        [Theory]
        [InlineData("(SPINE)", RlvAttachmentPoint.Spine)]
        [InlineData("(Spine)", RlvAttachmentPoint.Spine)]
        [InlineData("Central item (Avatar center)", RlvAttachmentPoint.AvatarCenter)]
        [InlineData("Another item (l upper leg)", RlvAttachmentPoint.LeftUpperLeg)]
        public void TryGetRlvAttachmentPointFromItemName_CaseSensitive(string itemName, RlvAttachmentPoint expectedRlvAttachmentPoint)
        {
            Assert.True(RlvCommon.TryGetAttachmentPointFromItemName(itemName, out var actualRlvAttachmentPoint));
            Assert.Equal(expectedRlvAttachmentPoint, actualRlvAttachmentPoint);
        }

        [Theory]
        [InlineData("(unknown)(hand tag)(spine tag)")]
        [InlineData("Hat")]
        [InlineData("")]
        public void TryGetRlvAttachmentPointFromItemNameInvalid(string itemName)
        {
            Assert.False(RlvCommon.TryGetAttachmentPointFromItemName(itemName, out var actualRlvAttachmentPoint));
        }
    }
}
