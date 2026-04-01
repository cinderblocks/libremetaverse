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
    public class SitTpRestrictionTests : RlvTestBase
    {
        #region @sittp[:max_distance]=<y/n>

        [Test]
        public void CanSitTp_Default()
        {
            Assert.That(_rlv.Permissions.CanSitTp(out var maxDistance), Is.False);
            Assert.That(maxDistance, Is.EqualTo(1.5f));
        }

        [Test]
        public async Task CanSitTp_Single()
        {
            await _rlv.ProcessMessage("@SitTp:2.5=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSitTp(out var maxDistance), Is.True);
            Assert.That(maxDistance, Is.EqualTo(2.5f));
        }

        [Test]
        public async Task CanSitTp_Multiple_SingleSender()
        {
            await _rlv.ProcessMessage("@SitTp:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@SitTp:4.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@SitTp:2.5=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSitTp(out var maxDistance), Is.True);
            Assert.That(maxDistance, Is.EqualTo(2.5f));
        }

        [Test]
        public async Task CanSitTp_Multiple_SingleSender_WithRemoval()
        {
            await _rlv.ProcessMessage("@SitTp:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@SitTp:4.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@SitTp:2.5=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@SitTp:8.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@SitTp:8.5=y", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSitTp(out var maxDistance), Is.True);
            Assert.That(maxDistance, Is.EqualTo(2.5f));
        }

        [Test]
        public async Task CanSitTp_Multiple_MultipleSenders()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var sender3 = new RlvObject("Sender 3", new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));

            await _rlv.ProcessMessage("@SitTp:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@SitTp:4.5=n", sender2.Id, sender2.Name);
            await _rlv.ProcessMessage("@SitTp:2.5=n", sender3.Id, sender3.Name);

            Assert.That(_rlv.Permissions.CanSitTp(out var maxDistance), Is.True);
            Assert.That(maxDistance, Is.EqualTo(2.5f));
        }

        [Test]
        public async Task CanSitTp_Off()
        {
            await _rlv.ProcessMessage("@SitTp:2.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@SitTp:2.5=y", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanSitTp(out var maxDistance), Is.False);
            Assert.That(maxDistance, Is.EqualTo(1.5f));
        }
        #endregion
    }
}
