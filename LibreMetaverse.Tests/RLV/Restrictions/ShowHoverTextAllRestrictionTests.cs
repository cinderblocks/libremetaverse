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
    public class ShowHoverTextAllRestrictionTests : RlvTestBase
    {

        #region @showhovertextall=<y/n>
        [Test]
        public async Task CanShowHoverTextAll()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@showhovertextall=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1), Is.False);
            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1), Is.False);
        }
        #endregion
    }
}
