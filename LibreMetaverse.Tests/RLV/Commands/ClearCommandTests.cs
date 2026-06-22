using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Commands
{
    [TestFixture]
    public class ClearCommandTests : RlvTestBase
    {
        #region @Clear

        [Test]
        public async Task Clear()
        {
            await _rlv.ProcessMessageAsync("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessageAsync("@clear", _sender.Id, _sender.Name);

            var restrictions = _rlv.Restrictions.FindRestrictions();
            Assert.That(restrictions, Is.Empty);
        }

        [Test]
        public async Task Clear_CaseInSensitive()
        {
            await _rlv.ProcessMessageAsync("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessageAsync("@cLEaR", _sender.Id, _sender.Name);

            var restrictions = _rlv.Restrictions.FindRestrictions();
            Assert.That(restrictions, Is.Empty);
        }

        [Test]
        public async Task Clear_SenderBased()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            await _rlv.ProcessMessageAsync("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@unsit=n", sender2.Id, sender2.Name);
            await _rlv.ProcessMessageAsync("@fly=n", sender2.Id, sender2.Name);

            await _rlv.ProcessMessageAsync("@clear", sender2.Id, sender2.Name);

            Assert.That(_rlv.Permissions.CanTpLoc(), Is.False);
            Assert.That(_rlv.Permissions.CanTpLm(), Is.False);
            Assert.That(_rlv.Permissions.CanUnsit(), Is.True);
            Assert.That(_rlv.Permissions.CanFly(), Is.True);
        }

        [Test]
        public async Task Clear_Filtered()
        {
            await _rlv.ProcessMessageAsync("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessageAsync("@clear=tp", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTpLoc(), Is.True);
            Assert.That(_rlv.Permissions.CanTpLm(), Is.True);
            Assert.That(_rlv.Permissions.CanUnsit(), Is.False);
            Assert.That(_rlv.Permissions.CanFly(), Is.False);
        }
        #endregion
    }
}
