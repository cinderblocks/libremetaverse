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
    public class UnsitRestrictionTests : RlvTestBase
    {
        #region @unsit=<y/n>
        [Test]
        public async Task CanUnsit()
        {
            await CheckSimpleCommand("unsit", m => m.CanUnsit());
        }
        #endregion
    }
}
