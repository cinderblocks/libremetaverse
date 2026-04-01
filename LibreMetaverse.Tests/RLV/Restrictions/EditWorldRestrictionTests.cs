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
    public class EditWorldRestrictionTests : RlvTestBase
    {
        #region @editworld=<y/n>
        [Test]
        public async Task CanEdit_World()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage($"@editworld=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null), Is.False);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1), Is.False);
        }
        #endregion
    }
}
