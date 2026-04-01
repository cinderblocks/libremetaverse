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
    public class AcceptPermissionExceptionTests : RlvTestBase
    {
        #region @acceptpermission=<rem/add>
        [Test]
        public async Task AcceptPermission()
        {
            Assert.That(await _rlv.ProcessMessage($"@acceptpermission=add", _sender.Id, _sender.Name), Is.True);
            Assert.That(_rlv.Permissions.IsAutoAcceptPermissions(), Is.True);

            Assert.That(await _rlv.ProcessMessage($"@acceptpermission=rem", _sender.Id, _sender.Name), Is.True);
            Assert.That(_rlv.Permissions.IsAutoAcceptPermissions(), Is.False);
        }
        #endregion

    }
}
