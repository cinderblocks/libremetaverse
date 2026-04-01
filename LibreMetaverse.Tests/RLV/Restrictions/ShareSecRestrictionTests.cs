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
    public class ShareSecRestrictionTests : RlvTestBase
    {
        #region @share_sec=<y/n>
        [Test]
        public async Task CanShare_Secure_Default()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@share_sec=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShare(null), Is.False);
            Assert.That(_rlv.Permissions.CanShare(userId1), Is.False);
            Assert.That(_rlv.Permissions.CanShare(userId2), Is.False);
        }

        [Test]
        public async Task CanShare_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@share_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@share:{userId1}=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@share:{userId2}=add", sender2.Id, sender2.Name);

            Assert.That(_rlv.Permissions.CanShare(null), Is.False);
            Assert.That(_rlv.Permissions.CanShare(userId1), Is.True);
            Assert.That(_rlv.Permissions.CanShare(userId2), Is.False);
        }

        #endregion
    }
}
