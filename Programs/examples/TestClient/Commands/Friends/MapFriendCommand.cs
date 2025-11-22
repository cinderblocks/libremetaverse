using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Friends
{
    public class MapFriendCommand : Command
    {
        public MapFriendCommand(TestClient testClient)
        {
            Name = "mapfriend";
            Description = "Show a friends location. Usage: mapfriend UUID";
            Category = CommandCategory.Friends;
        }
        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
                return Description;

            if (!UUID.TryParse(args[0], out var targetID))
                return Description;

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<FriendFoundReplyEventArgs> del = null;
            del = (object sender, FriendFoundReplyEventArgs e) =>
            {
                if (!e.RegionHandle.Equals(0))
                    tcs.TrySetResult($"Found Friend {e.AgentID} in {e.RegionHandle} at {e.Location.X}/{e.Location.Y}");
                else
                    tcs.TrySetResult($"Found Friend {e.AgentID}, But they appear to be offline");
            };

            try
            {
                Client.Friends.FriendFoundReply += del;
                Client.Friends.MapFriend(targetID);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                if (completed != tcs.Task)
                    return $"Timeout waiting for reply, Do you have mapping rights on {targetID}?";

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                Client.Friends.FriendFoundReply -= del;
            }
        }
    }
}
