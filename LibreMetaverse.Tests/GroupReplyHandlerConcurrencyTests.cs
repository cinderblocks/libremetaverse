using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Group member/role replies arrive as several packets that are handled concurrently. Each
    /// request must merge every packet and raise its completion event exactly once.
    /// </summary>
    [TestFixture]
    [Category("Groups")]
    public class GroupReplyHandlerConcurrencyTests
    {
        private const int Packets = 32;
        private const int Iterations = 50;

        private GridClient _client;
        private Simulator _sim;

        [SetUp]
        public void SetUp()
        {
            _client = new GridClient();
            _sim = new Simulator(_client, new IPEndPoint(IPAddress.Loopback, 13), 0);
        }

        [TearDown]
        public void TearDown()
        {
            _sim.Dispose();
            _client.Dispose();
        }

        private T GetField<T>(string name) =>
            (T)typeof(GroupManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_client.Groups);

        private void Dispatch(string handlerName, Packet packet) =>
            typeof(GroupManager).GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_client.Groups, new object[] { null, new PacketReceivedEventArgs(packet, _sim) });

        /// <summary>Deliver the packets from separate threads, released together</summary>
        private static void DispatchConcurrently(IReadOnlyList<Action> deliveries)
        {
            using var barrier = new Barrier(deliveries.Count);
            var errors = new ConcurrentQueue<Exception>();
            Parallel.ForEach(deliveries,
                new ParallelOptions { MaxDegreeOfParallelism = deliveries.Count },
                delivery =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        delivery();
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                });
            Assert.That(errors, Is.Empty, () => string.Join(Environment.NewLine, errors));
        }

        private GroupMembersReplyPacket MemberPacket(UUID requestID, UUID groupID, UUID memberID) =>
            new GroupMembersReplyPacket
            {
                GroupData = new GroupMembersReplyPacket.GroupDataBlock
                {
                    GroupID = groupID, RequestID = requestID, MemberCount = Packets
                },
                MemberData = new[]
                {
                    new GroupMembersReplyPacket.MemberDataBlock
                    {
                        AgentID = memberID,
                        OnlineStatus = Array.Empty<byte>(),
                        Title = Array.Empty<byte>()
                    }
                }
            };

        private GroupRoleDataReplyPacket RolePacket(UUID requestID, UUID groupID, UUID roleID) =>
            new GroupRoleDataReplyPacket
            {
                GroupData = new GroupRoleDataReplyPacket.GroupDataBlock
                {
                    GroupID = groupID, RequestID = requestID, RoleCount = Packets
                },
                RoleData = new[]
                {
                    new GroupRoleDataReplyPacket.RoleDataBlock
                    {
                        RoleID = roleID,
                        Name = Array.Empty<byte>(),
                        Title = Array.Empty<byte>(),
                        Description = Array.Empty<byte>()
                    }
                }
            };

        private GroupRoleMembersReplyPacket RoleMemberPacket(UUID requestID, UUID groupID, UUID roleID, UUID memberID) =>
            new GroupRoleMembersReplyPacket
            {
                AgentData = new GroupRoleMembersReplyPacket.AgentDataBlock
                {
                    GroupID = groupID, RequestID = requestID, TotalPairs = Packets
                },
                MemberData = new[]
                {
                    new GroupRoleMembersReplyPacket.MemberDataBlock { RoleID = roleID, MemberID = memberID }
                }
            };

        [Test]
        public void GroupMembers_ConcurrentReplies_RaiseCompletionOnce()
        {
            var requests = GetField<ConcurrentDictionary<UUID, byte>>("GroupMembersRequests");
            var temp = GetField<ConcurrentDictionary<UUID, Dictionary<UUID, GroupMember>>>("TempGroupMembers");
            var replies = new List<GroupMembersReplyEventArgs>();
            _client.Groups.GroupMembersReply += (_, e) => { lock (replies) replies.Add(e); };

            for (var i = 0; i < Iterations; i++)
            {
                replies.Clear();
                var requestID = UUID.Random();
                var groupID = UUID.Random();
                requests[requestID] = 0;
                var ids = Enumerable.Range(0, Packets).Select(_ => UUID.Random()).ToArray();

                DispatchConcurrently(ids
                    .Select(id => (Action)(() => Dispatch("GroupMembersHandler", MemberPacket(requestID, groupID, id))))
                    .ToList());

                Assert.That(replies, Has.Count.EqualTo(1));
                Assert.That(replies[0].RequestID, Is.EqualTo(requestID));
                Assert.That(replies[0].Members.Keys, Is.EquivalentTo(ids));
                Assert.That(requests.ContainsKey(requestID), Is.False);
                Assert.That(temp.ContainsKey(requestID), Is.False);
            }
        }

        [Test]
        public void GroupMembers_RepliesAfterCompletion_AreDroppedWithoutLeaking()
        {
            var requests = GetField<ConcurrentDictionary<UUID, byte>>("GroupMembersRequests");
            var temp = GetField<ConcurrentDictionary<UUID, Dictionary<UUID, GroupMember>>>("TempGroupMembers");
            var replies = new List<GroupMembersReplyEventArgs>();
            _client.Groups.GroupMembersReply += (_, e) => replies.Add(e);

            var requestID = UUID.Random();
            var groupID = UUID.Random();
            requests[requestID] = 0;
            for (var i = 0; i < Packets; i++)
                Dispatch("GroupMembersHandler", MemberPacket(requestID, groupID, UUID.Random()));
            var completed = replies.Single();

            // Duplicate/late packet for the finished request
            Dispatch("GroupMembersHandler", MemberPacket(requestID, groupID, UUID.Random()));

            Assert.That(replies, Has.Count.EqualTo(1));
            Assert.That(completed.Members, Has.Count.EqualTo(Packets));
            Assert.That(temp, Is.Empty);
        }

        [Test]
        public void GroupRoles_ConcurrentReplies_RaiseCompletionOnce()
        {
            var requests = GetField<ConcurrentDictionary<UUID, byte>>("GroupRolesRequests");
            var temp = GetField<ConcurrentDictionary<UUID, Dictionary<UUID, GroupRole>>>("TempGroupRoles");
            var replies = new List<GroupRolesDataReplyEventArgs>();
            _client.Groups.GroupRoleDataReply += (_, e) => { lock (replies) replies.Add(e); };

            for (var i = 0; i < Iterations; i++)
            {
                replies.Clear();
                var requestID = UUID.Random();
                var groupID = UUID.Random();
                requests[requestID] = 0;
                var ids = Enumerable.Range(0, Packets).Select(_ => UUID.Random()).ToArray();

                DispatchConcurrently(ids
                    .Select(id => (Action)(() => Dispatch("GroupRoleDataReplyHandler", RolePacket(requestID, groupID, id))))
                    .ToList());

                Assert.That(replies, Has.Count.EqualTo(1));
                Assert.That(replies[0].RequestID, Is.EqualTo(requestID));
                Assert.That(replies[0].Roles.Keys, Is.EquivalentTo(ids));
                Assert.That(requests.ContainsKey(requestID), Is.False);
                Assert.That(temp.ContainsKey(requestID), Is.False);
            }
        }

        [Test]
        public void GroupRoleMembers_ConcurrentReplies_RaiseCompletionOnce()
        {
            var requests = GetField<ConcurrentDictionary<UUID, byte>>("GroupRolesMembersRequests");
            var temp = GetField<ConcurrentDictionary<UUID, List<KeyValuePair<UUID, UUID>>>>("TempGroupRolesMembers");
            var replies = new List<GroupRolesMembersReplyEventArgs>();
            _client.Groups.GroupRoleMembersReply += (_, e) => { lock (replies) replies.Add(e); };

            for (var i = 0; i < Iterations; i++)
            {
                replies.Clear();
                var requestID = UUID.Random();
                var groupID = UUID.Random();
                var roleID = UUID.Random();
                requests[requestID] = 0;
                var ids = Enumerable.Range(0, Packets).Select(_ => UUID.Random()).ToArray();

                DispatchConcurrently(ids
                    .Select(id => (Action)(() => Dispatch("GroupRoleMembersReplyHandler", RoleMemberPacket(requestID, groupID, roleID, id))))
                    .ToList());

                Assert.That(replies, Has.Count.EqualTo(1));
                Assert.That(replies[0].RequestID, Is.EqualTo(requestID));
                Assert.That(replies[0].RolesMembers.Select(p => p.Value), Is.EquivalentTo(ids));
                Assert.That(requests.ContainsKey(requestID), Is.False);
                Assert.That(temp.ContainsKey(requestID), Is.False);
            }
        }
    }
}
