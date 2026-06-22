using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class DefaultWearRestrictionTests : RlvTestBase
    {
        #region @defaultwear=<y/n>

        [Test]
        public async Task CanDefaultWear()
        {
            await CheckSimpleCommand("defaultWear", m => m.CanDefaultWear());
        }

        #endregion
    }
}
