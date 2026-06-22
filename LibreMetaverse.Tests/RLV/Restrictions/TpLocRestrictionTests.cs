using System.Threading.Tasks;
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
