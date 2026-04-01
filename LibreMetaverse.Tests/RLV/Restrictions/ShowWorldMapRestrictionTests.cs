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
    public class ShowWorldMapRestrictionTests : RlvTestBase
    {
        #region  @showworldmap=<y/n>
        [Test]
        public async Task CanShowWorldMap()
        {
            await CheckSimpleCommand("showWorldMap", m => m.CanShowWorldMap());
        }
        #endregion
    }
}
