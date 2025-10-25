namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ChatShoutRestrictionTests : RestrictionsBase
    {
        #region @chatshout=<y/n>
        [Fact]
        public async Task CanChatShout()
        {
            await CheckSimpleCommand("chatShout", m => m.CanChatShout());
        }
        #endregion

    }
}
