using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class SendImRestrictionTests : RlvTestBase
    {
        #region @sendim=<y/n>
        [Test]
        public async Task CanSendIM()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSendIM("Hello", userId1), Is.False);
            Assert.That(_rlv.Permissions.CanSendIM("Hello", userId1, "Group Name"), Is.False);
        }
        #endregion
    }
}
