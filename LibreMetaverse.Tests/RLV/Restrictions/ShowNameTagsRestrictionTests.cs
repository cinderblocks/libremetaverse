using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class ShowNameTagsRestrictionTests : RlvTestBase
    {
        #region @shownametags=<y/n>
        [Test]
        public async Task CanShowNameTags()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@shownametags=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShowNameTags(null), Is.False);
            Assert.That(_rlv.Permissions.CanShowNameTags(userId1), Is.False);
        }
        #endregion
    }
}
