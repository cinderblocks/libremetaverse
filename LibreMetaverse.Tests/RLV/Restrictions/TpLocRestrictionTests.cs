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
    public class TpLocRestrictionTests : RlvTestBase
    {
        #region @tploc=<y/n>
        [Test]
        public async Task CanTpLoc()
        {
            await CheckSimpleCommand("tpLoc", m => m.CanTpLoc());
        }
        #endregion
    }
}
