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
    public class AllowIdleRestrictionTests : RlvTestBase
    {
        #region @allowidle=<y/n>
        [Test]
        public async Task CanAllowIdle()
        {
            await CheckSimpleCommand("allowIdle", m => m.CanAllowIdle());
        }
        #endregion
    }
}
