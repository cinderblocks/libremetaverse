using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ChatWhisperRestrictionTests : RlvTestBase
    {
        #region @chatwhisper=<y/n>
        [Test]
        public async Task CanChatWhisper()
        {
            await CheckSimpleCommand("chatWhisper", m => m.CanChatWhisper());
        }
        #endregion

    }
}
