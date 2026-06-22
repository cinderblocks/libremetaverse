using System.Threading.Tasks;
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
