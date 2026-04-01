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
    public class TouchThisExceptionTests : RlvTestBase
    {
        #region @touchthis:<Guid>=<rem/add>

        [Test]
        public async Task TouchThis_default()
        {
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-111111111111");

            var userId1 = new Guid("55555555-5555-4555-8555-555555555555");

            await _rlv.ProcessMessage($"@touchthis:{objectPrimId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectPrimId1, null, null), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectPrimId1, userId1, null), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectPrimId1, null, 5.0f), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectPrimId1, null, null), Is.False);

            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectPrimId2, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectPrimId2, userId1, null), Is.True);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectPrimId2, null, 5.0f), Is.True);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectPrimId2, null, null), Is.True);
        }

        #endregion

    }
}
