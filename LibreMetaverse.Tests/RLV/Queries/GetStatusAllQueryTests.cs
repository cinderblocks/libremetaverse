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
    public class GetStatusAllQueryTests : RlvTestBase
    {
        #region @getstatusall[:<part_of_rule>[;<custom_separator>]]=<channel>

        [Test]
        public async Task GetStatusAll()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplure=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@tplocal=n", sender2.Id, sender2.Name);

            await _rlv.ProcessMessage("@getstatusall=1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/fly/tplure/tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1/tplocal"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion
    }
}
