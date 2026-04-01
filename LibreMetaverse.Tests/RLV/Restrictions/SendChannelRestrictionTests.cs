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
    public class SendChannelRestrictionTests : RlvTestBase
    {
        #region @sendchannel[:<channel>]=<y/n>
        [Test]
        public async Task CanSendChannel()
        {
            await _rlv.ProcessMessage("@sendchannel=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanChat(123, "Hello world"), Is.False);
        }
        #endregion
    }
}
