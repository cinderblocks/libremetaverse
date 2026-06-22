using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ShowNamesRestrictionTests : RlvTestBase
    {
        #region @shownames=<y/n>
        [Test]
        public async Task CanShowNames()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessageAsync("@shownames=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShowNames(null), Is.False);
            Assert.That(_rlv.Permissions.CanShowNames(userId1), Is.False);
        }
        #endregion
    }
}
