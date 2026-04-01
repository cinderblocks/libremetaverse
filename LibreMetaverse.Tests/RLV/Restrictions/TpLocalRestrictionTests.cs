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
    public class TpLocalRestrictionTests : RlvTestBase
    {
        #region @tplocal[:max_distance]=<y/n>
        [Test]
        public async Task CanTpLocal_Default()
        {
            await _rlv.ProcessMessage("@TpLocal=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTpLocal(out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(0.0f).Within(FloatTolerance));
        }

        [Test]
        public async Task CanTpLocal()
        {
            await _rlv.ProcessMessage("@TpLocal:0.9=n", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.CanTpLocal(out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(0.9f).Within(FloatTolerance));
        }
        #endregion
    }
}
