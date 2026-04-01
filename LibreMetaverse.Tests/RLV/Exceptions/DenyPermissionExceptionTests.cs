using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class DenyPermissionExceptionTests : RlvTestBase
    {
        #region @denypermission=<rem/add>
        [Test]
        public async Task DenyPermission()
        {
            Assert.That(await _rlv.ProcessMessage($"@denypermission=add", _sender.Id, _sender.Name), Is.True);
            Assert.That(_rlv.Permissions.IsAutoDenyPermissions(), Is.True);

            Assert.That(await _rlv.ProcessMessage($"@denypermission=rem", _sender.Id, _sender.Name), Is.True);
            Assert.That(_rlv.Permissions.IsAutoDenyPermissions(), Is.False);
        }
        #endregion
    }
}
