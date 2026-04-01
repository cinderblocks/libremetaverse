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
    public class ShowInvRestrictionTests : RlvTestBase
    {
        #region @showinv=<y/n>
        [Test]
        public async Task CanShowInv()
        {
            await CheckSimpleCommand("showInv", m => m.CanShowInv());
        }

        #endregion
    }
}
