namespace LibreMetaverse.RLV.Tests.Commands
{
    public class ClearCommandTests : RestrictionsBase
    {
        #region @Clear

        [Fact]
        public async Task Clear()
        {
            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@clear", _sender.Id, _sender.Name);

            var restrictions = _rlv.Restrictions.FindRestrictions();
            Assert.Empty(restrictions);
        }

        [Fact]
        public async Task Clear_CaseInSensitive()
        {
            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@cLEaR", _sender.Id, _sender.Name);

            var restrictions = _rlv.Restrictions.FindRestrictions();
            Assert.Empty(restrictions);
        }

        [Fact]
        public async Task Clear_SenderBased()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", sender2.Id, sender2.Name);
            await _rlv.ProcessMessage("@fly=n", sender2.Id, sender2.Name);

            await _rlv.ProcessMessage("@clear", sender2.Id, sender2.Name);

            Assert.False(_rlv.Permissions.CanTpLoc());
            Assert.False(_rlv.Permissions.CanTpLm());
            Assert.True(_rlv.Permissions.CanUnsit());
            Assert.True(_rlv.Permissions.CanFly());
        }

        [Fact]
        public async Task Clear_Filtered()
        {
            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@clear=tp", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanTpLoc());
            Assert.True(_rlv.Permissions.CanTpLm());
            Assert.False(_rlv.Permissions.CanUnsit());
            Assert.False(_rlv.Permissions.CanFly());
        }
        #endregion
    }
}
