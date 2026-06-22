using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class EmoteExceptionTests : RlvTestBase
    {
        #region @emote=<rem/add>
        [Test]
        public async Task CanEmote()
        {
            await CheckSimpleCommand("emote", m => m.CanEmote());
        }

        #endregion
    }
}
