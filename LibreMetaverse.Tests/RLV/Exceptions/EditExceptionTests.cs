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
    public class EditExceptionTests : RlvTestBase
    {
        #region @edit:<UUID>=<rem/add>
        [Test]
        public async Task CanEdit_Exception()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@edit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@edit:{objectId1}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null), Is.False);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1), Is.True);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId2), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId2), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId2), Is.False);
        }

        [Test]
        public async Task CanEdit_Specific()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@editobj:{objectId1}=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null), Is.True);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1), Is.False);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId2), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId2), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId2), Is.True);
        }
        #endregion
    }
}
