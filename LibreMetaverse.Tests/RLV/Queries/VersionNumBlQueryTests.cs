using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Queries
{
    [TestFixture]
    public class VersionNumBlQueryTests : RlvTestBase
    {

        #region @versionnumbl=<channel_number>

        [TestCase("", RlvService.RLVVersionNum)]
        [TestCase("sendim,recvim", RlvService.RLVVersionNum + ",recvim,sendim")]
        public async Task VersionNumBL(string seed, string expectedResponse)
        {
            var actual = _actionCallbacks.RecordReplies();
            SeedBlacklist(seed);

            await _rlv.ProcessMessage("@versionnumbl=1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, expectedResponse),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion
    }
}
