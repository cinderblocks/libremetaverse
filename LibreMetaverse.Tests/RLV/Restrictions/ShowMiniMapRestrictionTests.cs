using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ShowMiniMapRestrictionTests : RlvTestBase
    {
        #region @showminimap=<y/n>
        [Test]
        public async Task CanShowMiniMap()
        {
            await CheckSimpleCommand("showMiniMap", m => m.CanShowMiniMap());
        }
        #endregion
    }
}
