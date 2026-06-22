using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class TpLureRestrictionTests : RlvTestBase
    {
        #region @tplure=<y/n>

        [Test]
        public void CanTpLure_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanTPLure(null), Is.True);
            Assert.That(_rlv.Permissions.CanTPLure(userId1), Is.True);
        }

        [Test]
        public async Task CanTpLure()
        {
            await _rlv.ProcessMessageAsync("@tplure=n", _sender.Id, _sender.Name);

            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanTPLure(null), Is.False);
            Assert.That(_rlv.Permissions.CanTPLure(userId1), Is.False);
        }
        #endregion
    }
}
