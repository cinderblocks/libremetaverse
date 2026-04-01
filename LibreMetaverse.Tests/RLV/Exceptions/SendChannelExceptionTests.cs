using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class SendChannelExceptionTests : RlvTestBase
    {
        #region @sendchannel:<channel>=<rem/add>
        [Test]
        public async Task CanSendChannel_Exception()
        {
            await _rlv.ProcessMessage("@sendchannel=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendchannel:123=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanChat(123, "Hello world"), Is.True);
        }
        #endregion
    }
}
