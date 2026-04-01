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
    public class InteractRestrictionTests : RlvTestBase
    {
        #region @interact=<y/n>

        [Test]
        public async Task CanInteract()
        {
            await CheckSimpleCommand("interact", m => m.CanInteract());
        }

        [Test]
        public async Task CanInteract_default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId1 = new Guid("55555555-5555-4555-8555-555555555555");

            await _rlv.ProcessMessage($"@interact=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectId1, null, null), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectId1, userId1, null), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectId1, null, 5.0f), Is.False);
            Assert.That(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectId1, null, null), Is.False);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1), Is.False);

            Assert.That(_rlv.Permissions.CanRez(), Is.False);

            Assert.That(_rlv.Permissions.CanSit(), Is.False);
        }

        #endregion
    }
}
