using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ViewTextureRestrictionTests : RlvTestBase
    {
        #region @viewtexture=<y/n>
        [Test]
        public async Task CanViewTexture()
        {
            await CheckSimpleCommand("viewTexture", m => m.CanViewTexture());
        }
        #endregion
    }
}
