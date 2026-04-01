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
    public class SendChannelExceptExceptionTests : RlvTestBase
    {
        #region @sendchannel_except:<channel>=<y/n>
        [Test]
        public async Task CanSendChannelExcept()
        {
            await _rlv.ProcessMessage("@sendchannel_except:456=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanChat(123, "Hello world"), Is.True);
            Assert.That(_rlv.Permissions.CanChat(456, "Hello world"), Is.False);
        }
        #endregion
    }
}
