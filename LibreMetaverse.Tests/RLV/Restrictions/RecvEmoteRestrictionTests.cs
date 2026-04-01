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
    public class RecvEmoteRestrictionTests : RlvTestBase
    {
        #region @recvemote=<y/n>
        [Test]
        public async Task CanRecvChat_RecvEmote()
        {
            await _rlv.ProcessMessage("@recvemote=n", _sender.Id, _sender.Name);

            var userId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanReceiveChat("Hello world", userId), Is.True);
            Assert.That(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId), Is.False);
        }
        #endregion
    }
}
