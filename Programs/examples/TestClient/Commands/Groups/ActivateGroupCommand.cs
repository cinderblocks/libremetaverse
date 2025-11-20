using System;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace TestClient.Commands.Groups
{
    /// <summary>
    /// Changes Avatars currently active group
    /// </summary>
    public class ActivateGroupCommand : Command
    {
        string activeGroup;

        public ActivateGroupCommand(TestClient testClient)
        {
            Name = "activategroup";
            Description = "Set a group as active. Usage: activategroup GroupName";
            Category = CommandCategory.Groups;
        }
        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return Description;

            activeGroup = string.Empty;

            string groupName = string.Join(" ", args).Trim();

            UUID groupUUID = await Client.GroupName2UUIDAsync(groupName).ConfigureAwait(false);
            if (UUID.Zero != groupUUID)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                EventHandler<PacketReceivedEventArgs> pcallback = null;
                pcallback = (sender, e) =>
                {
                    AgentDataUpdatePacket p = (AgentDataUpdatePacket)e.Packet;
                    if (p.AgentData.AgentID == Client.Self.AgentID)
                    {
                        activeGroup = Utils.BytesToString(p.AgentData.GroupName) + " ( " + Utils.BytesToString(p.AgentData.GroupTitle) + " )";
                        tcs.TrySetResult(true);
                    }
                };

                try
                {
                    Client.Network.RegisterCallback(PacketType.AgentDataUpdate, pcallback);

                    Console.WriteLine("setting " + groupName + " as active group");
                    Client.Groups.ActivateGroup(groupUUID);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

                    if (completed != tcs.Task)
                    {
                        return Client + " failed to activate the group " + groupName;
                    }

                    return "Active group is now " + activeGroup;
                }
                finally
                {
                    Client.Network.UnregisterCallback(PacketType.AgentDataUpdate, pcallback);
                }
            }
            return Client + " doesn't seem to be member of the group " + groupName;
        }
    }
}
