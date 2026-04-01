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
    public class TempRunRestrictionTests : RlvTestBase
    {
        #region @temprun=<y/n>
        [Test]
        public async Task CanTempRun()
        {
            await CheckSimpleCommand("tempRun", m => m.CanTempRun());
        }
        #endregion
    }
}
