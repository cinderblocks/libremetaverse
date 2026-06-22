using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class RecvChatExceptionTests : RlvTestBase
    {
        #region @recvchat:<UUID>=<rem/add>
        [Test]
        public async Task CanRecvChat_Except()
        {
            var userId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessageAsync("@recvchat=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync($"@recvchat:{userId}=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanReceiveChat("Hello world", userId), Is.True);
            Assert.That(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId), Is.True);
        }
        #endregion
    }
}
