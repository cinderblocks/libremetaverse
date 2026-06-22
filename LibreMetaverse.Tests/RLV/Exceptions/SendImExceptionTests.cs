using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class SendImExceptionTests : RlvTestBase
    {
        #region @sendim:<UUID_or_group_name>=<rem/add>
        [Test]
        public async Task CanSendIM_Exception()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync($"@sendim:{userId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSendIM("Hello world", userId1), Is.True);
        }

        [Test]
        public async Task CanSendIM_Exception_SingleGroup()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync($"@sendim:Group Name=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Group Name"), Is.True);
        }

        [Test]
        public async Task CanSendIM_Exception_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync($"@sendim:allgroups=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Group name"), Is.True);
        }

        #endregion
    }
}
