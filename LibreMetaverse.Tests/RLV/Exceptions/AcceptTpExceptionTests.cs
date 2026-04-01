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
    public class AcceptTpExceptionTests : RlvTestBase
    {
        #region @accepttp[:<UUID>]=<rem/add>

        [Test]
        public async Task CanAutoAcceptTp_User()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@accepttp:{userId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.IsAutoAcceptTp(userId1), Is.True);
            Assert.That(_rlv.Permissions.IsAutoAcceptTp(userId2), Is.False);
            Assert.That(_rlv.Permissions.IsAutoAcceptTp(), Is.False);
        }

        [Test]
        public async Task CanAutoAcceptTp_All()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@accepttp=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.IsAutoAcceptTp(userId1), Is.True);
            Assert.That(_rlv.Permissions.IsAutoAcceptTp(userId2), Is.True);
            Assert.That(_rlv.Permissions.IsAutoAcceptTp(), Is.True);
        }

        #endregion
    }
}
