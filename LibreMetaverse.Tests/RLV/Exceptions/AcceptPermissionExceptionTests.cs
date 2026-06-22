using System.Threading.Tasks;
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
            Assert.That(await _rlv.ProcessMessageAsync($"@acceptpermission=add", _sender.Id, _sender.Name), Is.True);
            Assert.That(_rlv.Permissions.IsAutoAcceptPermissions(), Is.True);

            Assert.That(await _rlv.ProcessMessageAsync($"@acceptpermission=rem", _sender.Id, _sender.Name), Is.True);
            Assert.That(_rlv.Permissions.IsAutoAcceptPermissions(), Is.False);
        }
        #endregion

    }
}
