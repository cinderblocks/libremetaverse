using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
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
            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@clear", _sender.Id, _sender.Name);

            var restrictions = _rlv.Restrictions.FindRestrictions();
            Assert.That(restrictions, Is.Empty);
        }

        [Test]
        public async Task Clear_CaseInSensitive()
        {
            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@cLEaR", _sender.Id, _sender.Name);

            var restrictions = _rlv.Restrictions.FindRestrictions();
            Assert.That(restrictions, Is.Empty);
        }

        [Test]
        public async Task Clear_SenderBased()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", sender2.Id, sender2.Name);
            await _rlv.ProcessMessage("@fly=n", sender2.Id, sender2.Name);

            await _rlv.ProcessMessage("@clear", sender2.Id, sender2.Name);

            Assert.That(_rlv.Permissions.CanTpLoc(), Is.False);
            Assert.That(_rlv.Permissions.CanTpLm(), Is.False);
            Assert.That(_rlv.Permissions.CanUnsit(), Is.True);
            Assert.That(_rlv.Permissions.CanFly(), Is.True);
        }

        [Test]
        public async Task Clear_Filtered()
        {
            await _rlv.ProcessMessage("@tploc=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplm=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@clear=tp", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTpLoc(), Is.True);
            Assert.That(_rlv.Permissions.CanTpLm(), Is.True);
            Assert.That(_rlv.Permissions.CanUnsit(), Is.False);
            Assert.That(_rlv.Permissions.CanFly(), Is.False);
        }
        #endregion
    }
}
