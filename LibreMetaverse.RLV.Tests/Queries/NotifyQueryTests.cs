using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class NotifyQueryTests : RestrictionsBase
    {
        #region @notify:<channel_number>[;word]=<rem/add>
        [Fact]
        public async Task Notify()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@alwaysrun=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendim:group_name=add", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/sendim=n"),
                (1234, "/alwaysrun=n"),
                (1234, "/sendim:group_name=n"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyFiltered()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@notify:1234;run=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@alwaysrun=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendim:group_name=add", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/alwaysrun=n"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyMultiCommand()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendim=n,sendim:group_name=add,alwaysrun=n", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/sendim=n"),
                (1234, "/sendim:group_name=n"),
                (1234, "/alwaysrun=n"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyMultiChannels()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@notify:12345=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234,  "/notify:1234=n"),
                (1234,  "/notify:12345=n"),
                (12345, "/notify:12345=n"),
                (1234,  "/sendim=n"),
                (12345, "/sendim=n"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyMultiChannelsFiltered()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@notify:12345;im=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234,  "/notify:1234=n"),
                (1234,  "/notify:12345;im=n"),
                (1234,  "/sendim=n"),
                (12345, "/sendim=n"),
            };

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("@camdistmax:123=n", "/camdistmax:123=n")]
        [InlineData("@setcam_avdistmax:123=n", "/setcam_avdistmax:123=n")]
        [InlineData("@camdistmin:123=n", "/camdistmin:123=n")]
        [InlineData("@setcam_avdistmin:123=n", "/setcam_avdistmin:123=n")]
        [InlineData("@camunlock=n", "/camunlock=n")]
        [InlineData("@setcam_unlock=n", "/setcam_unlock=n")]
        [InlineData("@camtextures:1cdbc6a2-ae6b-3130-9348-3d3b1ca84c53=n", "/camtextures:1cdbc6a2-ae6b-3130-9348-3d3b1ca84c53=n")]
        [InlineData("@touchfar:5=n", "/touchfar:5=n")]
        [InlineData("@fartouch:5=n", "/fartouch:5=n")]
        public async Task NotifySynonyms(string command, string expectedReply)
        {
            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

            _actionCallbacks.Verify(c => c.SendReplyAsync(1234, "/notify:1234=n", It.IsAny<CancellationToken>()), Times.Once);
            _actionCallbacks.Verify(c => c.SendReplyAsync(1234, expectedReply, It.IsAny<CancellationToken>()), Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NotifyClear_Filtered()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@clear=fly", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/fly=n"),
                // Begin processing clear()...
                (1234, "/fly=y"),
                (1234, "/clear:fly"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyClear()
        {
            var actual = _actionCallbacks.RecordReplies();

            var sender2Id = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@notify:1234=add", sender2Id, "Main");
            await _rlv.ProcessMessage("@fly=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@clear", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/fly=n"),
                // Begin processing clear()...
                (1234, "/fly=y"),
                (1234, "/clear"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyInventoryOffer()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportInventoryOfferAccepted("#RLV/~MyCuffs");
            await _rlv.ReportInventoryOfferAccepted("Objects/New Folder (3)");
            await _rlv.ReportInventoryOfferDeclined("#RLV/Foo/Bar");

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/notify:1234=n"),
                (1234, "/accepted_in_rlv inv_offer ~MyCuffs"),
                (1234, "/accepted_in_inv inv_offer Objects/New Folder (3)"),
                (1234, "/declined inv_offer Foo/Bar"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifySitStandLegal()
        {
            var actual = _actionCallbacks.RecordReplies();

            var sitTarget = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSit(sitTarget);
            await _rlv.ReportUnsit(sitTarget);
            await _rlv.ReportSit(null);
            await _rlv.ReportUnsit(null);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/sat object legally {sitTarget}"),
                (1234, $"/unsat object legally {sitTarget}"),
                (1234, $"/sat ground legally"),
                (1234, $"/unsat ground legally"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifySitStandWithRestrictions()
        {
            var actual = _actionCallbacks.RecordReplies();

            var sitTarget = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@sit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);

            await _rlv.ReportSit(sitTarget);
            await _rlv.ReportUnsit(sitTarget);
            await _rlv.ReportSit(null);
            await _rlv.ReportUnsit(null);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/sat object illegally {sitTarget}"),
                (1234, $"/unsat object illegally {sitTarget}"),
                (1234, $"/sat ground illegally"),
                (1234, $"/unsat ground illegally"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyWear()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var itemId2 = new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemWorn(itemId1, false, RlvWearableType.Skin);
            await _rlv.ReportItemWorn(itemId2, true, RlvWearableType.Tattoo);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/worn legally skin"),
                (1234, $"/worn legally tattoo"),
            };

            Assert.Equal(expected, actual);
        }


        [Fact]
        public async Task NotifyWear_Illegal()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@addoutfit:skin=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemWorn(itemId1, false, RlvWearableType.Skin);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/worn illegally skin"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyUnWear()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var folderId1 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var folderId2 = new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemUnworn(wornItem.Id, folderId1, false, RlvWearableType.Skin);
            await _rlv.ReportItemUnworn(wornItem.Id, folderId2, true, RlvWearableType.Tattoo);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/unworn legally skin"),
                (1234, $"/unworn legally tattoo"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyUnWear_illegal()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@remoutfit:skin=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);

            await _rlv.ReportItemUnworn(wornItem.Id, itemId1, false, RlvWearableType.Skin);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/unworn illegally skin"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyAttached()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var itemId2 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemAttached(itemId1, false, RlvAttachmentPoint.Chest);
            await _rlv.ReportItemAttached(itemId2, true, RlvAttachmentPoint.AvatarCenter);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/attached legally Chest"),
                (1234, $"/attached legally Avatar Center"),
            };

            Assert.Equal(expected, actual);
        }


        [Fact]
        public async Task NotifyAttached_Illegal()
        {
            var actual = _actionCallbacks.RecordReplies();

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@addattach:chest=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemAttached(itemId1, false, RlvAttachmentPoint.Chest);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/attached illegally Chest"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyDetached()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var wornItemPrimId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-ffffffffffff");

            var folderId1 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var folderId2 = new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemDetached(wornItem.Id, wornItemPrimId, folderId1, false, RlvAttachmentPoint.Chest);
            await _rlv.ReportItemDetached(wornItem.Id, wornItemPrimId, folderId2, true, RlvAttachmentPoint.Skull);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/detached legally Chest"),
                (1234, $"/detached legally Skull"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task NotifyDetached_Illegal()
        {
            var actual = _actionCallbacks.RecordReplies();
            var wornItem = new RlvObject("TargetItem", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var wornItemPrimId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-ffffffffffff");

            var itemId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@remattach:chest=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@notify:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportItemDetached(wornItem.Id, wornItemPrimId, itemId1, false, RlvAttachmentPoint.Chest);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"/notify:1234=n"),
                (1234, $"/detached illegally Chest"),
            };

            Assert.Equal(expected, actual);
        }
        #endregion
    }
}
