using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class JumpRestrictionTests : RlvTestBase
    {
        #region @jump=<y/n> (RLVa)
        [Test]
        public async Task CanJump()
        {
            await CheckSimpleCommand("jump", m => m.CanJump());
        }
        #endregion
    }
}
