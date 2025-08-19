namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ChatWhisperRestrictionTests : RestrictionsBase
    {
        #region @chatwhisper=<y/n>
        [Fact]
        public async Task CanChatWhisper()
        {
            await CheckSimpleCommand("chatWhisper", m => m.CanChatWhisper());
        }
        #endregion

    }
}
