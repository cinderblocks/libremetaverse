using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ChatShoutRestrictionTests : RlvTestBase
    {
        #region @chatshout=<y/n>
        [Test]
        public async Task CanChatShout()
        {
            await CheckSimpleCommand("chatShout", m => m.CanChatShout());
        }
        #endregion

    }
}
