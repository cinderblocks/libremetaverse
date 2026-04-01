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
    public class FlyRestrictionTests : RlvTestBase
    {
        #region @fly=<y/n>
        [Test]
        public async Task CanFly()
        {
            await CheckSimpleCommand("fly", m => m.CanFly());
        }
        #endregion
    }
}
