using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Commands
{
    [TestFixture]
    public class SitGroundCommandTests : RlvTestBase
    {

        #region @sitground=force

        [Test]
        public async Task ForceSitGround()
        {
            Assert.That(await _rlv.ProcessMessageAsync("@sitground=force", _sender.Id, _sender.Name), Is.True);
        }

        [Test]
        public async Task ForceSitGround_RestrictedSit()
        {
            await _rlv.ProcessMessageAsync("@sit=n", _sender.Id, _sender.Name);

            Assert.That(await _rlv.ProcessMessageAsync("@sitground=force", _sender.Id, _sender.Name), Is.False);
        }

        #endregion
    }
}
