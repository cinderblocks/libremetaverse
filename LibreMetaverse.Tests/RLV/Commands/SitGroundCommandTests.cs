using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
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
            Assert.That(await _rlv.ProcessMessage("@sitground=force", _sender.Id, _sender.Name), Is.True);
        }

        [Test]
        public async Task ForceSitGround_RestrictedSit()
        {
            await _rlv.ProcessMessage("@sit=n", _sender.Id, _sender.Name);

            Assert.That(await _rlv.ProcessMessage("@sitground=force", _sender.Id, _sender.Name), Is.False);
        }

        #endregion
    }
}
