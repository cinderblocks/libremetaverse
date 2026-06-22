using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class SetGroupRestrictionTests : RlvTestBase
    {
        #region @setgroup=<y/n>
        [Test]
        public async Task CanSetGroup()
        {
            await CheckSimpleCommand("setGroup", m => m.CanSetGroup());
        }
        #endregion
    }
}
