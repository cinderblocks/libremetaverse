using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class TpLmRestrictionTests : RlvTestBase
    {
        #region @tplm=<y/n>
        [Test]
        public async Task CanTpLm()
        {
            await CheckSimpleCommand("tpLm", m => m.CanTpLm());
        }
        #endregion
    }
}
