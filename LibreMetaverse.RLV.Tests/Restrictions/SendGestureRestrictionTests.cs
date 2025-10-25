namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SendGestureRestrictionTests : RestrictionsBase
    {

        #region @sendgesture=<y/n>

        [Fact]
        public async Task CanSendGesture()
        {
            await CheckSimpleCommand("sendGesture", m => m.CanSendGesture());
        }

        #endregion
    }
}
