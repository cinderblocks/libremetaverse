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
    public class RecvImExceptionTests : RlvTestBase
    {
        #region @recvim:<UUID_or_group_name>=<rem/add>
        [Test]
        public async Task CanReceiveIM_Exception()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvim:{userId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", userId1), Is.True);
        }


        [Test]
        public async Task CanReceiveIM_Exception_SingleGroup()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvim:Group Name=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "Group Name"), Is.True);
        }

        [Test]
        public async Task CanReceiveIM_Exception_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvim:allgroups=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "Group name"), Is.True);
        }

        #endregion
    }
}
