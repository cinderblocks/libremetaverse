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
    public class PermissiveRestrictionTests : RlvTestBase
    {
        #region @Permissive
        [Test]
        public async Task Permissive_On()
        {
            await _rlv.ProcessMessage("@permissive=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.IsPermissive(), Is.False);
        }

        [Test]
        public async Task Permissive_Off()
        {
            await _rlv.ProcessMessage("@permissive=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@permissive=y", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.IsPermissive(), Is.True);
        }
        #endregion
    }
}
