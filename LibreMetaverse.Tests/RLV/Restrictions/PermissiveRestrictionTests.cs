using System.Threading.Tasks;
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
            await _rlv.ProcessMessageAsync("@permissive=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.IsPermissive(), Is.False);
        }

        [Test]
        public async Task Permissive_Off()
        {
            await _rlv.ProcessMessageAsync("@permissive=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@permissive=y", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.IsPermissive(), Is.True);
        }
        #endregion
    }
}
