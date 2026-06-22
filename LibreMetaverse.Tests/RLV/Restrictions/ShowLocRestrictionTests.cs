using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ShowLocRestrictionTests : RlvTestBase
    {
        #region @showloc=<y/n>
        [Test]
        public async Task CanShowLoc()
        {
            await CheckSimpleCommand("showLoc", m => m.CanShowLoc());
        }
        #endregion

    }
}
