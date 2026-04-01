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
    public class RecvImRestrictionTests : RlvTestBase
    {
        #region @recvim=<y/n>
        [Test]
        public async Task CanReceiveIM()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello", userId1), Is.False);
            Assert.That(_rlv.Permissions.CanReceiveIM("Hello", userId1, "Group Name"), Is.False);
        }

        #endregion
    }
}
