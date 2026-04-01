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
    public class SitRestrictionTests : RlvTestBase
    {

        #region @sit=<y/n>
        [Test]
        public async Task CanSit()
        {
            await CheckSimpleCommand("sit", m => m.CanSit());
        }
        #endregion
    }
}
