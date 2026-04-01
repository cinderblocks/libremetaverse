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
    public class ShowLocRestrictionTests : RlvTestBase
    {
        #region @showloc=<y/n>
        [Test]
        public async Task CanShowLoc()
        {
            await CheckSimpleCommand("showLoc", m => m.CanShowLoc());
        }
        #endregion

    }
}
