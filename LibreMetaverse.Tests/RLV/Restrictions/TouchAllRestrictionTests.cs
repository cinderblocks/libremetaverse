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
    public class TouchAllRestrictionTests : RlvTestBase
    {
        #region @touchall=<y/n>

        [Test]
        public void TouchAll()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId1 = new Guid("11111111-1111-4111-8111-111111111111");

            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectId1, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectId1, userId1, null), Is.True);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectId1, null, 5.0f), Is.True);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectId1, null, null), Is.True);
        }

        [Test]
        public async Task TouchAll_default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId1 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@touchall=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectId1, null, null), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectId1, userId1, null), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectId1, null, 5.0f), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectId1, null, null), Is.True);
        }

        #endregion

    }
}
