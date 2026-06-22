using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class AlwaysRunRestrictionTests : RlvTestBase
    {
        #region @alwaysrun=<y/n>
        [Test]
        public async Task CanAlwaysRun()
        {
            await CheckSimpleCommand("alwaysRun", m => m.CanAlwaysRun());
        }
        #endregion
    }
}
