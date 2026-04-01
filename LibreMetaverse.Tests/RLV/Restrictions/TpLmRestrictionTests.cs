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
    public class TpLmRestrictionTests : RlvTestBase
    {
        #region @tplm=<y/n>
        [Test]
        public async Task CanTpLm()
        {
            await CheckSimpleCommand("tpLm", m => m.CanTpLm());
        }
        #endregion
    }
}
