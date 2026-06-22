using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Commands
{
    [TestFixture]
    public class UnsitCommandTests : RlvTestBase
    {
        #region @unsit=force

        [Test]
        public async Task ForceUnSit()
        {
            Assert.That(await _rlv.ProcessMessageAsync("@unsit=force", _sender.Id, _sender.Name), Is.True);
        }

        [Test]
        public async Task ForceUnSit_RestrictedUnsit()
        {
            await _rlv.ProcessMessageAsync("@unsit=n", _sender.Id, _sender.Name);

            Assert.That(await _rlv.ProcessMessageAsync("@unsit=force", _sender.Id, _sender.Name), Is.False);
        }

        #endregion
    }
}
