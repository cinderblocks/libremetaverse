using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class SendChatRestrictionTests : RlvTestBase
    {
        #region @sendchat=<y/n>
        [Test]
        public async Task CanSendChat()
        {
            await CheckSimpleCommand("sendChat", m => m.CanSendChat());
        }

        [Test]
        public async Task CanChat_SendChatRestriction()
        {
            await _rlv.ProcessMessage("@sendchat=n", _sender.Id, _sender.Name);

            // No public chat allowed unless it starts with '/'
            Assert.That(_rlv.Permissions.CanChat(0, "Hello"), Is.False);

            // Emotes and other messages starting with / are allowed
            Assert.That(_rlv.Permissions.CanChat(0, "/me says Hello"), Is.True);
            Assert.That(_rlv.Permissions.CanChat(0, "/ something?"), Is.True);

            // Messages containing ()"-*=_^ are prohibited
            Assert.That(_rlv.Permissions.CanChat(0, "/me says Hello ^_^"), Is.False);

            // Private channels are not impacted
            Assert.That(_rlv.Permissions.CanChat(5, "Hello"), Is.True);
        }
        #endregion

    }
}
