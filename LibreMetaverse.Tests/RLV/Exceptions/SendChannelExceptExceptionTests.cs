using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class SendChannelExceptExceptionTests : RlvTestBase
    {
        #region @sendchannel_except:<channel>=<y/n>
        [Test]
        public async Task CanSendChannelExcept()
        {
            await _rlv.ProcessMessageAsync("@sendchannel_except:456=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanChat(123, "Hello world"), Is.True);
            Assert.That(_rlv.Permissions.CanChat(456, "Hello world"), Is.False);
        }
        #endregion
    }
}
