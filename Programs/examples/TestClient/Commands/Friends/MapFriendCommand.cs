using System;
using System.Threading;
using System.Text;

namespace OpenMetaverse.TestClient
{
    public class MapFriendCommand : Command
    {
        ManualResetEvent WaitforFriend = new ManualResetEvent(false);

        public MapFriendCommand(TestClient testClient)
        {
            Name = "mapfriend";
            Description = "Show a friends location. Usage: mapfriend UUID";
            Category = CommandCategory.Friends;
        }
        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
                return Description;

            UUID targetID;

            if (!UUID.TryParse(args[0], out targetID))
                return Description;

            StringBuilder sb = new StringBuilder();

            EventHandler<FriendFoundReplyEventArgs> del = delegate(object sender, FriendFoundReplyEventArgs e)
            {
                if (!e.RegionHandle.Equals(0))
                    sb.AppendFormat("Found Friend {0} in {1} at {2}/{3}", e.AgentID, e.RegionHandle, e.Location.X, e.Location.Y);
                else
                    sb.AppendFormat("Found Friend {0}, But they appear to be offline", e.AgentID);

                WaitforFriend.Set();
            };



            Client.Friends.FriendFoundReply += del;
            WaitforFriend.Reset();
            Client.Friends.MapFriend(targetID);
            if (!WaitforFriend.WaitOne(TimeSpan.FromSeconds(10), false))
            {
                sb.AppendFormat("Timeout waiting for reply, Do you have mapping rights on {0}?", targetID);
            }
            Client.Friends.FriendFoundReply -= del;
            return sb.ToString();
        }
    }
}
