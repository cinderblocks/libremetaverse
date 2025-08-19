namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SendChatRestrictionTests : RestrictionsBase
    {
        #region @sendchat=<y/n>
        [Fact]
        public async Task CanSendChat()
        {
            await CheckSimpleCommand("sendChat", m => m.CanSendChat());
        }

        [Fact]
        public async Task CanChat_SendChatRestriction()
        {
            await _rlv.ProcessMessage("@sendchat=n", _sender.Id, _sender.Name);

            // No public chat allowed unless it starts with '/'
            Assert.False(_rlv.Permissions.CanChat(0, "Hello"));

            // Emotes and other messages starting with / are allowed
            Assert.True(_rlv.Permissions.CanChat(0, "/me says Hello"));
            Assert.True(_rlv.Permissions.CanChat(0, "/ something?"));

            // Messages containing ()"-*=_^ are prohibited
            Assert.False(_rlv.Permissions.CanChat(0, "/me says Hello ^_^"));

            // Private channels are not impacted
            Assert.True(_rlv.Permissions.CanChat(5, "Hello"));
        }
        #endregion

    }
}
