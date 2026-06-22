using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Queries
{
    [TestFixture]
    public class NotifyQueryTests : RlvTestBase
    {
        #region @notify:<channel_number>[;word]=<rem/add>
        [Test]
        public async Task Notify()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@alwaysrun=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@sendim:group_name=add", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/sendim=n"),
                (1234, "/alwaysrun=n"),
                (1234, "/sendim:group_name=n"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyFiltered()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@notify:1234;run=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@alwaysrun=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@sendim:group_name=add", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/alwaysrun=n"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyMultiCommand()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@sendim=n,sendim:group_name=add,alwaysrun=n", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/sendim=n"),
                (1234, "/sendim:group_name=n"),
                (1234, "/alwaysrun=n"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyMultiChannels()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@notify:12345=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234,  "/notify:1234=n"),
                (1234,  "/notify:12345=n"),
                (12345, "/notify:12345=n"),
                (1234,  "/sendim=n"),
                (12345, "/sendim=n"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyMultiChannelsFiltered()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@notify:12345;im=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@sendim=n", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234,  "/notify:1234=n"),
                (1234,  "/notify:12345;im=n"),
                (1234,  "/sendim=n"),
                (12345, "/sendim=n"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase("@camdistmax:123=n", "/camdistmax:123=n")]
        [TestCase("@setcam_avdistmax:123=n", "/setcam_avdistmax:123=n")]
        [TestCase("@camdistmin:123=n", "/camdistmin:123=n")]
        [TestCase("@setcam_avdistmin:123=n", "/setcam_avdistmin:123=n")]
        [TestCase("@camunlock=n", "/camunlock=n")]
        [TestCase("@setcam_unlock=n", "/setcam_unlock=n")]
        [TestCase("@camtextures:1cdbc6a2-ae6b-3130-9348-3d3b1ca84c53=n", "/camtextures:1cdbc6a2-ae6b-3130-9348-3d3b1ca84c53=n")]
        [TestCase("@touchfar:5=n", "/touchfar:5=n")]
        [TestCase("@fartouch:5=n", "/fartouch:5=n")]
        public async Task NotifySynonyms(string command, string expectedReply)
        {
            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync(command, _sender.Id, _sender.Name);

            _actionCallbacks.Verify(c => c.SendReplyAsync(1234, "/notify:1234=n", It.IsAny<CancellationToken>()), Times.Once);
            _actionCallbacks.Verify(c => c.SendReplyAsync(1234, expectedReply, It.IsAny<CancellationToken>()), Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task NotifyClear_Filtered()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@clear=fly", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/fly=n"),
                // Begin processing clear()...
                (1234, "/fly=y"),
                (1234, "/clear:fly"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyClear()
        {
            var actual = _actionCallbacks.RecordReplies();

            var sender2Id = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessageAsync("@notify:1234=add", sender2Id, "Main");
            await _rlv.ProcessMessageAsync("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@clear", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/fly=n"),
                // Begin processing clear()...
                (1234, "/fly=y"),
                (1234, "/clear"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyInventoryOffer()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportInventoryOfferAcceptedAsync("#RLV/~MyCuffs");
            await _rlv.ReportInventoryOfferAcceptedAsync("Objects/New Folder (3)");
            await _rlv.ReportInventoryOfferDeclinedAsync("#RLV/Foo/Bar");

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/accepted_in_rlv inv_offer ~MyCuffs"),
                (1234, "/accepted_in_inv inv_offer Objects/New Folder (3)"),
                (1234, "/declined inv_offer Foo/Bar"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifySitStandLegal()
        {
            var actual = _actionCallbacks.RecordReplies();

            var sitTarget = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSitAsync(sitTarget);
            await _rlv.ReportUnsitAsync(sitTarget);
            await _rlv.ReportSitAsync(null);
            await _rlv.ReportUnsitAsync(null);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/sat object legally {sitTarget}"),
                (1234, $"/unsat object legally {sitTarget}"),
                (1234, $"/sat ground legally"),
                (1234, $"/unsat ground legally"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifySitStandWithRestrictions()
        {
            var actual = _actionCallbacks.RecordReplies();

            var sitTarget = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessageAsync("@sit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);

            await _rlv.ReportSitAsync(sitTarget);
            await _rlv.ReportUnsitAsync(sitTarget);
            await _rlv.ReportSitAsync(null);
            await _rlv.ReportUnsitAsync(null);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/sat object illegally {sitTarget}"),
                (1234, $"/unsat object illegally {sitTarget}"),
                (1234, $"/sat ground illegally"),
                (1234, $"/unsat ground illegally"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyWear()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var itemId2 = new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemWornAsync(itemId1, false, RlvWearableType.Skin);
            await _rlv.ReportItemWornAsync(itemId2, true, RlvWearableType.Tattoo);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/worn legally skin"),
                (1234, $"/worn legally tattoo"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }


        [Test]
        public async Task NotifyWear_Illegal()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessageAsync("@addoutfit:skin=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemWornAsync(itemId1, false, RlvWearableType.Skin);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/worn illegally skin"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyUnWear()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var folderId1 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var folderId2 = new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemUnwornAsync(wornItem.Id, folderId1, false, RlvWearableType.Skin);
            await _rlv.ReportItemUnwornAsync(wornItem.Id, folderId2, true, RlvWearableType.Tattoo);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/unworn legally skin"),
                (1234, $"/unworn legally tattoo"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyUnWear_illegal()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessageAsync("@remoutfit:skin=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);

            await _rlv.ReportItemUnwornAsync(wornItem.Id, itemId1, false, RlvWearableType.Skin);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/unworn illegally skin"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyAttached()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var itemId2 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemAttachedAsync(itemId1, false, RlvAttachmentPoint.Chest);
            await _rlv.ReportItemAttachedAsync(itemId2, true, RlvAttachmentPoint.AvatarCenter);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/attached legally Chest"),
                (1234, $"/attached legally Avatar Center"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }


        [Test]
        public async Task NotifyAttached_Illegal()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessageAsync("@addattach:chest=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemAttachedAsync(itemId1, false, RlvAttachmentPoint.Chest);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/attached illegally Chest"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyDetached()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var wornItemPrimId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-ffffffffffff");

            var folderId1 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var folderId2 = new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemDetachedAsync(wornItem.Id, wornItemPrimId, folderId1, false, RlvAttachmentPoint.Chest);
            await _rlv.ReportItemDetachedAsync(wornItem.Id, wornItemPrimId, folderId2, true, RlvAttachmentPoint.Skull);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/detached legally Chest"),
                (1234, $"/detached legally Skull"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task NotifyDetached_Illegal()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var wornItemPrimId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-ffffffffffff");

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessageAsync("@remattach:chest=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemDetachedAsync(wornItem.Id, wornItemPrimId, itemId1, false, RlvAttachmentPoint.Chest);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/detached illegally Chest"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion
    }
}
