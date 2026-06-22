using System;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class StartImRestrictionTests : RlvTestBase
    {
        #region @startim=<y/n>
        [Test]
        public async Task CanStartIM()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessageAsync("@startim=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanStartIM(userId1), Is.False);
        }
        #endregion

    }
}
