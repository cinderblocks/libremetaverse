using System;
using System.Collections.Generic;
using System.Reflection;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Groups")]
    public class GroupMembersReplyTests
    {
        private GridClient _client;

        [SetUp]
        public void SetUp() => _client = new GridClient();

        [TearDown]
        public void TearDown() => _client.Dispose();

        private void Handle(UUID requestID, OSD result)
        {
            var handler = typeof(GroupManager).GetMethod("GroupMembersHandlerCaps",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(UUID), typeof(OSD) }, null);
            Assert.That(handler, Is.Not.Null);
            handler.Invoke(_client.Groups, new object[] { requestID, result });
        }

        private static OSDMap ValidResponse(UUID groupID) => new OSDMap
        {
            ["group_id"] = OSD.FromUUID(groupID),
            ["member_count"] = OSD.FromInteger(0),
            ["titles"] = new OSDArray { OSD.FromString("Member") },
            ["defaults"] = new OSDMap { ["default_powers"] = OSD.FromULong((ulong)GroupPowers.Invite) },
            ["members"] = new OSDMap()
        };

        [Test]
        public void ExistingConstructor_DefaultsToSuccess()
        {
            var requestID = UUID.Random();
            var groupID = UUID.Random();
            var members = new Dictionary<UUID, GroupMember>();
            var reply = new GroupMembersReplyEventArgs(requestID, groupID, members);

            Assert.That(reply.Success, Is.True);
            Assert.That(reply.RequestID, Is.EqualTo(requestID));
            Assert.That(reply.GroupID, Is.EqualTo(groupID));
            Assert.That(reply.Members, Is.SameAs(members));
        }

        [Test]
        public void NewConstructor_RepresentsFailure()
        {
            var requestID = UUID.Random();
            var groupID = UUID.Random();
            var members = new Dictionary<UUID, GroupMember>();
            var reply = new GroupMembersReplyEventArgs(requestID, groupID, members, false);

            Assert.That(reply.Success, Is.False);
            Assert.That(reply.RequestID, Is.EqualTo(requestID));
            Assert.That(reply.GroupID, Is.EqualTo(groupID));
            Assert.That(reply.Members, Is.SameAs(members));
        }

        [TestCase("missing-fields")]
        [TestCase("wrong-root")]
        [TestCase("wrong-member")]
        [TestCase("invalid-member-id")]
        [TestCase("invalid-title-index")]
        public void InvalidMemberData_RaisesOneCorrelatedFailure(string scenario)
        {
            var requestID = UUID.Random();
            var groupID = UUID.Random();
            OSD result = ValidResponse(groupID);
            var members = (OSDMap)((OSDMap)result)["members"];
            switch (scenario)
            {
                case "missing-fields":
                    result = new OSDMap();
                    groupID = UUID.Zero;
                    break;
                case "wrong-root":
                    result = new OSDArray();
                    groupID = UUID.Zero;
                    break;
                case "wrong-member":
                    members[UUID.Random().ToString()] = new OSDArray();
                    break;
                case "invalid-member-id":
                    members["not-a-uuid"] = new OSDMap();
                    break;
                case "invalid-title-index":
                    members[UUID.Random().ToString()] = new OSDMap { ["title"] = OSD.FromInteger(10) };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unhandled test scenario");
            }

            var replies = new List<GroupMembersReplyEventArgs>();
            _client.Groups.GroupMembersReply += (_, e) => replies.Add(e);

            Handle(requestID, result);

            Assert.That(replies, Has.Count.EqualTo(1));
            Assert.That(replies[0].Success, Is.False);
            Assert.That(replies[0].RequestID, Is.EqualTo(requestID));
            Assert.That(replies[0].GroupID, Is.EqualTo(groupID));
            Assert.That(replies[0].Members, Is.Not.Null.And.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ValidReply_PreservesMembersAndReportsSuccess(bool populated)
        {
            var requestID = UUID.Random();
            var groupID = UUID.Random();
            var memberID = UUID.Random();
            var overrideID = UUID.Random();
            var result = ValidResponse(groupID);
            if (populated)
            {
                result["member_count"] = OSD.FromInteger(2);
                var members = (OSDMap)result["members"];
                members[memberID.ToString()] = new OSDMap
                {
                    ["donated_square_meters"] = OSD.FromInteger(128),
                    ["owner"] = OSD.FromString("Y"),
                    ["last_login"] = OSD.FromString("Online"),
                    ["title"] = OSD.FromInteger(0)
                };
                members[overrideID.ToString()] = new OSDMap
                {
                    ["powers"] = OSD.FromULong((ulong)GroupPowers.Eject),
                    ["title"] = OSD.FromInteger(0)
                };
            }

            var replies = new List<GroupMembersReplyEventArgs>();
            _client.Groups.GroupMembersReply += (_, e) => replies.Add(e);
            Handle(requestID, result);

            Assert.That(replies, Has.Count.EqualTo(1));
            var reply = replies[0];
            Assert.That(reply.Success, Is.True);
            Assert.That(reply.RequestID, Is.EqualTo(requestID));
            Assert.That(reply.GroupID, Is.EqualTo(groupID));
            Assert.That(reply.Members, Has.Count.EqualTo(populated ? 2 : 0));
            if (populated)
            {
                var member = reply.Members[memberID];
                Assert.That(member.ID, Is.EqualTo(memberID));
                Assert.That(member.Contribution, Is.EqualTo(128));
                Assert.That(member.IsOwner, Is.True);
                Assert.That(member.OnlineStatus, Is.EqualTo("Online"));
                Assert.That(member.Title, Is.EqualTo("Member"));
                Assert.That(member.Powers, Is.EqualTo(GroupPowers.Invite));
                Assert.That(reply.Members[overrideID].Powers, Is.EqualTo(GroupPowers.Eject));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThrowingSubscriber_DoesNotProduceAnotherReply(bool valid)
        {
            var groupID = UUID.Random();
            var calls = 0;
            var failure = new FormatException("Subscriber failure");
            _client.Groups.GroupMembersReply += (_, _) =>
            {
                calls++;
                throw failure;
            };

            var exception = Assert.Throws<TargetInvocationException>(() =>
                Handle(UUID.Random(), valid ? ValidResponse(groupID) : new OSDMap()));

            Assert.That(exception.InnerException, Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(1));
        }

    }
}
