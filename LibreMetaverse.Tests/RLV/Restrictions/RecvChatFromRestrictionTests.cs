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
    public class RecvChatFromRestrictionTests : RlvTestBase
    {
        #region @recvchatfrom:<UUID>=<y/n>

        [Test]
        public async Task CanRecvChatFrom()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvchatfrom:{userId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveChat("Hello world", userId1), Is.False);
            Assert.That(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId1), Is.True);

            Assert.That(_rlv.Permissions.CanReceiveChat("Hello world", userId2), Is.True);
        }

        #endregion

    }
}
