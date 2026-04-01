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
    public class StartImExceptionTests : RlvTestBase
    {
        #region @startim:<UUID>=<rem/add>
        [Test]
        public async Task CanStartIM_Exception()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@startim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@startim:{userId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanStartIM(userId1), Is.True);
            Assert.That(_rlv.Permissions.CanStartIM(userId2), Is.False);
        }
        #endregion
    }
}
