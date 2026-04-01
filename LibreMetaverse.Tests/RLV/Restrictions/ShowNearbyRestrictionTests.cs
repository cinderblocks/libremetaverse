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
    public class ShowNearbyRestrictionTests : RlvTestBase
    {
        #region @shownearby=<y/n>
        [Test]
        public async Task CanShowNearby()
        {
            await CheckSimpleCommand("showNearby", m => m.CanShowNearby());
        }
        #endregion
    }
}
