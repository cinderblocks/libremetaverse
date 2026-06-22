using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class FlyRestrictionTests : RlvTestBase
    {
        #region @fly=<y/n>
        [Test]
        public async Task CanFly()
        {
            await CheckSimpleCommand("fly", m => m.CanFly());
        }
        #endregion
    }
}
