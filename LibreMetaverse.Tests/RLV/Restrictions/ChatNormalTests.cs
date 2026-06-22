using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ChatNormalTests : RlvTestBase
    {
        #region @chatnormal=<y/n>
        [Test]
        public async Task CanChatNormal()
        {
            await CheckSimpleCommand("chatNormal", m => m.CanChatNormal());
        }
        #endregion

    }
}
