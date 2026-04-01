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
    public class RecvChatExceptionTests : RlvTestBase
    {
        #region @recvchat:<UUID>=<rem/add>
        [Test]
        public async Task CanRecvChat_Except()
        {
            var userId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@recvchat=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvchat:{userId}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveChat("Hello world", userId), Is.True);
            Assert.That(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId), Is.True);
        }
        #endregion
    }
}
