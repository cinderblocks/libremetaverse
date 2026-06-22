using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ShareRestrictionTests : RlvTestBase
    {
        #region @share=<y/n>

        [Test]
        public async Task CanShare()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessageAsync("@share=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShare(null), Is.False);
            Assert.That(_rlv.Permissions.CanShare(userId1), Is.False);
        }
        #endregion
    }
}
