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
    public class ShowNamesRestrictionTests : RlvTestBase
    {
        #region @shownames=<y/n>
        [Test]
        public async Task CanShowNames()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@shownames=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanShowNames(null), Is.False);
            Assert.That(_rlv.Permissions.CanShowNames(userId1), Is.False);
        }
        #endregion
    }
}
