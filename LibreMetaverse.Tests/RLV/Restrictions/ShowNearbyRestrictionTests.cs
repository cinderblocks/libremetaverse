using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ShowNearbyRestrictionTests : RlvTestBase
    {
        #region @shownearby=<y/n>
        [Test]
        public async Task CanShowNearby()
        {
            await CheckSimpleCommand("showNearby", m => m.CanShowNearby());
        }
        #endregion
    }
}
