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
    public class TpLureSecRestrictionTests : RlvTestBase
    {
        #region @tplure_sec=<y/n>
        [Test]
        public async Task CanTpLure_Secure_Default()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@tplure_sec=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTPLure(null), Is.False);
            Assert.That(_rlv.Permissions.CanTPLure(userId1), Is.False);
            Assert.That(_rlv.Permissions.CanTPLure(userId2), Is.False);
        }

        [Test]
        public async Task CanTpLure_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@tplure_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@tplure:{userId1}=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@tplure:{userId2}=add", sender2.Id, sender2.Name);

            Assert.That(_rlv.Permissions.CanTPLure(null), Is.False);
            Assert.That(_rlv.Permissions.CanTPLure(userId1), Is.True);
            Assert.That(_rlv.Permissions.CanTPLure(userId2), Is.False);
        }

        #endregion
    }
}
