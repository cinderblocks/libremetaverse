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
    public class ShowNamesSecRestrictionTests : RlvTestBase
    {
        #region  @shownames_sec[:except_uuid]=<y/n>
        [Test]
        public async Task CanShowNames_Secure_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownames_sec=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShowNames(null), Is.False);
            Assert.That(_rlv.Permissions.CanShowNames(userId1), Is.False);
            Assert.That(_rlv.Permissions.CanShowNames(userId2), Is.False);
        }

        [Test]
        public async Task CanShowNames_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("22222222-2222-4222-8222-222222222222"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownames_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownames:{userId1}=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownames:{userId2}=add", sender2.Id, sender2.Name);

            Assert.That(_rlv.Permissions.CanShowNames(null), Is.False);
            Assert.That(_rlv.Permissions.CanShowNames(userId1), Is.True);
            Assert.That(_rlv.Permissions.CanShowNames(userId2), Is.False);
        }

        [Test]
        public async Task CanShowNames_Secure_Except()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("22222222-2222-4222-8222-222222222222"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownames_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownames_sec:{userId1}=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShowNames(null), Is.False);
            Assert.That(_rlv.Permissions.CanShowNames(userId1), Is.True);
            Assert.That(_rlv.Permissions.CanShowNames(userId2), Is.False);
        }

        #endregion
    }
}
