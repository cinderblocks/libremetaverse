using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Queries
{
    [TestFixture]
    public class GetStatusQueryTests : RlvTestBase
    {

        #region @getstatus[:<part_of_rule>[;<custom_separator>]]=<channel>

        [Test]
        public async Task GetStatus()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplure=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplocal=n", sender2.Id, sender2.Name);

            await _rlv.ProcessMessageAsync("@getstatus=1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/fly/tplure/tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetStatus_filtered()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplure=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplocal=n", sender2.Id, sender2.Name);

            await _rlv.ProcessMessageAsync("@getstatus:tp=1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/tplure/tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetStatus_customSeparator()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplure=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@tplocal=n", sender2.Id, sender2.Name);

            await _rlv.ProcessMessageAsync("@getstatus:; ! =1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $" ! fly ! tplure ! tplure:3d6181b0-6a4b-97ef-18d8-722652995cf1"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion
    }
}
