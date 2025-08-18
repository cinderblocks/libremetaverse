namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class EmoteExceptionTests : RestrictionsBase
    {
        #region @emote=<rem/add>
        [Fact]
        public async Task CanEmote()
        {
            await CheckSimpleCommand("emote", m => m.CanEmote());
        }

        #endregion
    }
}
