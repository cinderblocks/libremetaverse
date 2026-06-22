using System.Threading.Tasks;
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
