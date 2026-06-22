using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class TempRunRestrictionTests : RlvTestBase
    {
        #region @temprun=<y/n>
        [Test]
        public async Task CanTempRun()
        {
            await CheckSimpleCommand("tempRun", m => m.CanTempRun());
        }
        #endregion
    }
}
