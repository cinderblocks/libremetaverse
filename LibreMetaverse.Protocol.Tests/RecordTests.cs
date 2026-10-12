/*
 * Copyright (c) 2026, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The records that were declared in the managers (groups, friends, regions, instant messages and the enums beside
    /// them) and are in the protocol assembly now, which has no client in it.
    /// </summary>
    [TestFixture]
    [Category("Records")]
    public class RecordTests
    {
        #region Friends

        [Test]
        public void FriendRights_AreTheFlagsOfTheProtocol()
        {
            Assert.Multiple(() =>
            {
                Assert.That((int)FriendRights.None, Is.EqualTo(0));
                Assert.That((int)FriendRights.CanSeeOnline, Is.EqualTo(1));
                Assert.That((int)FriendRights.CanSeeOnMap, Is.EqualTo(2));
                Assert.That((int)FriendRights.CanModifyObjects, Is.EqualTo(4));
            });
        }

        [Test]
        public void AFriendHasTheRightsTheyGaveAndTheRightsIGave()
        {
            var id = UUID.Random();

            // Their rights are what they let me do; mine are what I let them do
            var friend = new FriendInfo(id, FriendRights.CanSeeOnline | FriendRights.CanModifyObjects, FriendRights.CanSeeOnMap);

            Assert.Multiple(() =>
            {
                Assert.That(friend.UUID, Is.EqualTo(id));
                Assert.That(friend.TheirFriendRights, Is.EqualTo(FriendRights.CanSeeOnline | FriendRights.CanModifyObjects));
                Assert.That(friend.MyFriendRights, Is.EqualTo(FriendRights.CanSeeOnMap));
                Assert.That(friend.CanSeeThemOnMap, Is.True);
                Assert.That(friend.CanSeeThemOnline, Is.False);
                Assert.That(friend.CanModifyMyObjects, Is.True);
                Assert.That(friend.CanModifyTheirObjects, Is.False);
            });
        }

        [Test]
        public void EachOfTheSixRights_ComesFromItsOwnPlaceInTheConstructor()
        {
            var theirs = new[] { FriendRights.CanSeeOnline, FriendRights.CanSeeOnMap, FriendRights.CanModifyObjects };

            foreach (var right in theirs)
            {
                var onlyTheirs = new FriendInfo(UUID.Random(), right, FriendRights.None);
                var onlyMine = new FriendInfo(UUID.Random(), FriendRights.None, right);

                Assert.Multiple(() =>
                {
                    Assert.That(onlyTheirs.TheirFriendRights, Is.EqualTo(right), "theirs: " + right);
                    Assert.That(onlyTheirs.MyFriendRights, Is.EqualTo(FriendRights.None), "theirs: " + right);
                    Assert.That(onlyMine.MyFriendRights, Is.EqualTo(right), "mine: " + right);
                    Assert.That(onlyMine.TheirFriendRights, Is.EqualTo(FriendRights.None), "mine: " + right);
                });
            }
        }

        [Test]
        public void SettingTheRights_ChangesTheFlagsToo()
        {
            var friend = new FriendInfo(UUID.Random(), FriendRights.None, FriendRights.None);

            friend.TheirFriendRights = FriendRights.CanSeeOnMap;
            friend.MyFriendRights = FriendRights.CanSeeOnline | FriendRights.CanModifyObjects;

            Assert.Multiple(() =>
            {
                Assert.That(friend.CanSeeThemOnline, Is.True);
                Assert.That(friend.CanModifyTheirObjects, Is.True);
                Assert.That(friend.CanSeeThemOnMap, Is.False);
                Assert.That(friend.TheirFriendRights, Is.EqualTo(FriendRights.CanSeeOnMap));
            });
        }

        [Test]
        public void AFriend_IsDescribedByNameOrByID()
        {
            var id = UUID.Random();
            var friend = new FriendInfo(id, FriendRights.CanSeeOnline, FriendRights.None);

            Assert.That(friend.ToString(), Does.StartWith(id.ToString()));

            friend.Name = "Test Resident";
            Assert.That(friend.ToString(), Does.StartWith("Test Resident (Their Rights: CanSeeOnline, My Rights: None)"));
        }

        #endregion Friends

        #region Regions

        [Test]
        public void TwoGridRegions_AreTheSameWhenTheyAreAtTheSamePlace()
        {
            var a = new GridRegion { X = 1000, Y = 1001, Name = "One" };
            var b = new GridRegion { X = 1000, Y = 1001, Name = "Renamed" };
            var c = new GridRegion { X = 1001, Y = 1001, Name = "One" };

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
                Assert.That(a, Is.Not.EqualTo(c));
                Assert.That(a.Equals("not a region"), Is.False);
                Assert.That(a.ToString(), Does.StartWith("One (1000/1001)"));
            });
        }

        [Test]
        public void ALayerContainsTheRegionsWithinItsEdges_IncludingTheEdges()
        {
            var layer = new GridLayer { Left = 10, Right = 20, Bottom = 100, Top = 110 };

            Assert.Multiple(() =>
            {
                Assert.That(layer.ContainsRegion(10, 100), Is.True);
                Assert.That(layer.ContainsRegion(20, 110), Is.True);
                Assert.That(layer.ContainsRegion(15, 105), Is.True);
                Assert.That(layer.ContainsRegion(9, 105), Is.False);
                Assert.That(layer.ContainsRegion(21, 105), Is.False);
                Assert.That(layer.ContainsRegion(15, 99), Is.False);
                Assert.That(layer.ContainsRegion(15, 111), Is.False);
            });
        }

        #endregion Regions

        #region Groups

        private static Group SampleGroup() => new Group
        {
            ID = UUID.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Builders",
            Charter = "We build",
            FounderID = UUID.Parse("22222222-2222-2222-2222-222222222222"),
            InsigniaID = UUID.Parse("33333333-3333-3333-3333-333333333333"),
            OpenEnrollment = true,
            MembershipFee = 10,
            GroupMembershipCount = 5
        };

        [Test]
        public void TwoGroupsWithTheSameFields_AreEqual_AndOneThatDiffersIsNot()
        {
            var a = SampleGroup();
            var b = SampleGroup();
            var other = SampleGroup();
            other.MembershipFee = 11;

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
                Assert.That(a, Is.Not.EqualTo(other));
                Assert.That(a.ToString(), Is.EqualTo("Builders"));
            });
        }

        private delegate void Change(ref Group group);

        [Test]
        public void EachFieldOfAGroup_CountsInItsEquality()
        {
            // A Group is a struct, so each change is made through a reference
            var changes = new Change[]
            {
                (ref Group g) => g.ID = UUID.Random(), (ref Group g) => g.InsigniaID = UUID.Random(), (ref Group g) => g.FounderID = UUID.Random(),
                (ref Group g) => g.Name = "x", (ref Group g) => g.Charter = "x", (ref Group g) => g.MemberTitle = "x",
                (ref Group g) => g.OpenEnrollment = !g.OpenEnrollment, (ref Group g) => g.ShowInList = !g.ShowInList,
                (ref Group g) => g.Powers = GroupPowers.Invite, (ref Group g) => g.AcceptNotices = !g.AcceptNotices,
                (ref Group g) => g.AllowPublish = !g.AllowPublish, (ref Group g) => g.MaturePublish = !g.MaturePublish,
                (ref Group g) => g.MembershipFee += 1, (ref Group g) => g.Money += 1, (ref Group g) => g.Contribution += 1,
                (ref Group g) => g.GroupMembershipCount += 1, (ref Group g) => g.GroupRolesCount += 1,
                (ref Group g) => g.ListInProfile = !g.ListInProfile
            };

            Assert.Multiple(() =>
            {
                for (int i = 0; i < changes.Length; i++)
                {
                    var changed = SampleGroup();
                    changes[i](ref changed);
                    Assert.That(changed.Equals(SampleGroup()), Is.False, "change " + i);
                }
            });
        }

        [Test]
        public void ANoticeWithAnAttachment_SerializesTheItemAndItsOwner_AsLlsd()
        {
            var item = UUID.Random();
            var owner = UUID.Random();
            var notice = new GroupNotice { Subject = "s", Message = "m", AttachmentID = item, OwnerID = owner };

            var map = (OSDMap)OSDParser.Deserialize(notice.SerializeAttachment());

            Assert.Multiple(() =>
            {
                Assert.That(map["item_id"].AsUUID(), Is.EqualTo(item));
                Assert.That(map["owner_id"].AsUUID(), Is.EqualTo(owner));
            });
        }

        [Test]
        public void ANoticeWithoutAnAttachmentOrAnOwner_HasNoAttachmentBytes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new GroupNotice { AttachmentID = UUID.Random() }.SerializeAttachment(), Is.Empty);
                Assert.That(new GroupNotice { OwnerID = UUID.Random() }.SerializeAttachment(), Is.Empty);
                Assert.That(new GroupNotice().SerializeAttachment(), Is.Empty);
            });
        }

        [Test]
        public void TwoChatSessionMembers_AreEqualWhenAllTheirFieldsAre()
        {
            var id = UUID.Random();
            var a = new ChatSessionMember { AvatarKey = id, CanVoiceChat = true, IsModerator = false };
            var b = new ChatSessionMember { AvatarKey = id, CanVoiceChat = true, IsModerator = false };
            var c = new ChatSessionMember { AvatarKey = id, CanVoiceChat = true, IsModerator = true };

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
                Assert.That(a, Is.Not.EqualTo(c));
            });
        }

        #endregion Groups

        #region Agent records

        [Test]
        public void AnInstantMessage_DescribesItsFields()
        {
            var im = new InstantMessage { FromAgentName = "Test Resident", Message = "hello there", Dialog = InstantMessageDialog.MessageFromAgent };

            Assert.That(im.ToString(), Does.Contain("Test Resident").And.Contain("hello there").And.Contain("MessageFromAgent"));
        }

        [Test]
        public void TheMuteFlags_AreTheFlagsOfTheProtocol()
        {
            Assert.Multiple(() =>
            {
                Assert.That((int)MuteFlags.TextChat, Is.EqualTo(1));
                Assert.That((int)MuteFlags.VoiceChat, Is.EqualTo(2));
                Assert.That((int)MuteFlags.Particles, Is.EqualTo(4));
                Assert.That((int)MuteFlags.ObjectSounds, Is.EqualTo(8));
                Assert.That(MuteFlags.All, Is.EqualTo(MuteFlags.TextChat | MuteFlags.VoiceChat | MuteFlags.Particles | MuteFlags.ObjectSounds));
                Assert.That((int)MuteType.ByName, Is.EqualTo(0));
                Assert.That((int)MuteType.Resident, Is.EqualTo(1));
                Assert.That((int)MuteType.Object, Is.EqualTo(2));
                Assert.That((int)MuteType.Group, Is.EqualTo(3));
            });
        }

        #endregion Agent records

        #region Parcel

        [Test]
        public void ANewParcel_HasItsLocalIdAndEmptyDefaults()
        {
            var parcel = new Parcel(17);

            Assert.Multiple(() =>
            {
                Assert.That(parcel.LocalID, Is.EqualTo(17));
                Assert.That(parcel.ClaimDate, Is.EqualTo(Utils.Epoch));
                Assert.That(parcel.Bitmap, Is.Empty);
                Assert.That(parcel.Name, Is.Empty);
                Assert.That(parcel.Desc, Is.Empty);
                Assert.That(parcel.MusicURL, Is.Empty);
                Assert.That(parcel.AccessWhiteList, Is.Empty);
                Assert.That(parcel.AccessBlackList, Is.Empty);
            });
        }

        [Test]
        public void TheAccessLists_AreNotSharedBetweenParcels()
        {
            var one = new Parcel(1);
            var two = new Parcel(2);

            one.AccessWhiteList.Add(new ParcelAccessEntry { AgentID = UUID.Random(), Flags = AccessList.Access });

            Assert.That(two.AccessWhiteList, Is.Empty);
            Assert.That(one.AccessBlackList, Is.Empty);
        }

        [Test]
        public void ParcelToString_NamesTheFieldsThatIdentifyTheParcel()
        {
            var owner = UUID.Random();
            var parcel = new Parcel(9) { OwnerID = owner, Name = "Meadow", Area = 512, SalePrice = 1500 };

            string text = parcel.ToString();

            Assert.That(text, Does.Contain("LocalID = 9").And.Contain($"OwnerID = {owner}").And.Contain("Name = Meadow")
                .And.Contain("Area = 512").And.Contain("SalePrice = 1500"));
        }

        [Test]
        public void ParcelPrimOwners_HoldsWhatTheReplyReports()
        {
            var owner = UUID.Random();
            var when = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            var entry = new ParcelPrimOwners { OwnerID = owner, IsGroupOwned = true, Count = 7, OnlineStatus = true, NewestPrim = when };

            Assert.Multiple(() =>
            {
                Assert.That(entry.OwnerID, Is.EqualTo(owner));
                Assert.That(entry.IsGroupOwned, Is.True);
                Assert.That(entry.Count, Is.EqualTo(7));
                Assert.That(entry.OnlineStatus, Is.True);
                Assert.That(entry.NewestPrim, Is.EqualTo(when));
            });
        }

        #endregion Parcel
    }
}
