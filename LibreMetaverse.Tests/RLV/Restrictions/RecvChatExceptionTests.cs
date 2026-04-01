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
    public class RecvChatExceptionTests : RlvTestBase
    {
        [Test]
        public async Task CanRecvChat()
        {
            await _rlv.ProcessMessage("@recvchat=n", _sender.Id, _sender.Name);
            var userId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanReceiveChat("Hello world", userId), Is.False);
            Assert.That(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId), Is.True);
        }
    }
}
