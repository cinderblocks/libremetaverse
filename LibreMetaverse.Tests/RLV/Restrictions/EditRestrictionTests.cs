using System;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class EditRestrictionTests : RlvTestBase
    {
        #region @edit=<y/n>

        [Test]
        public async Task CanEditFolderNameSpecifiesToAddInsteadOfReplace()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessageAsync("@edit=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null), Is.False);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1), Is.False);
        }
        #endregion
    }
}
