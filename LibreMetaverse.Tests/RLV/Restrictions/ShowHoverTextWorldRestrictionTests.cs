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
    public class ShowHoverTextWorldRestrictionTests : RlvTestBase
    {
        #region @showhovertextworld=<y/n>

        [Test]
        public async Task CanShowHoverTextWorld()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@showhovertextworld=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1), Is.True);

            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId2), Is.False);
            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId2), Is.True);
        }

        #endregion
    }
}
