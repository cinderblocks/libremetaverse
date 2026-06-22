using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class StandTpRestrictionTests : RlvTestBase
    {
        #region @standtp=<y/n>
        [Test]
        public async Task CanStandTp()
        {
            await CheckSimpleCommand("standTp", m => m.CanStandTp());
        }
        #endregion
    }
}
