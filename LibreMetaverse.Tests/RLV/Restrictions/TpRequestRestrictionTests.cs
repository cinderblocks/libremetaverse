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
    public class TpRequestRestrictionTests : RlvTestBase
    {
        #region @tprequest=<y/n>

        [Test]
        public async Task CanTpRequest()
        {
            await _rlv.ProcessMessage("@tprequest=n", _sender.Id, _sender.Name);

            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanTpRequest(null), Is.False);
            Assert.That(_rlv.Permissions.CanTpRequest(userId1), Is.False);
        }

        [Test]
        public async Task CanTpRequest_Except()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@tprequest=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@tprequest:{userId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTpRequest(null), Is.False);
            Assert.That(_rlv.Permissions.CanTpRequest(userId1), Is.True);
            Assert.That(_rlv.Permissions.CanTpRequest(userId2), Is.False);
        }
        #endregion
    }
}
