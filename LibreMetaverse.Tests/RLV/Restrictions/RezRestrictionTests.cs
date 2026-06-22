using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class RezRestrictionTests : RlvTestBase
    {
        #region @rez=<y/n>
        [Test]
        public async Task CanRez()
        {
            await CheckSimpleCommand("rez", m => m.CanRez());
        }

        #endregion
    }
}
