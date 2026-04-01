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
    public class RecvImFromRestrictionTests : RlvTestBase
    {
        #region @recvimfrom:<UUID_or_group_name>=<y/n>
        [Test]
        public async Task CanReceiveIMFrom()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvimfrom:{userId1}=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", userId1), Is.False);
            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", userId2), Is.True);
        }

        [Test]
        public async Task CanReceiveIMFrom_Group()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var groupId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvimfrom:First Group=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "First Group"), Is.False);
            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", groupId2, "Second Group"), Is.True);
        }

        [Test]
        public async Task CanReceiveIMTo_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var groupId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvimfrom:allgroups=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "First Group"), Is.False);
            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", groupId2, "Second Group"), Is.False);
        }
        #endregion
    }
}
