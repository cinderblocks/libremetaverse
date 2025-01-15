using System;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    public class ImGroupCommand : Command
    {
        UUID ToGroupID = UUID.Zero;
        ManualResetEvent WaitForSessionStart = new ManualResetEvent(false);
        public ImGroupCommand(TestClient testClient)
        {

            Name = "imgroup";
            Description = "Send an instant message to a group. Usage: imgroup [group_uuid] [message]";
            Category = CommandCategory.Communication;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 2)
                return "Usage: imgroup [group_uuid] [message]";



            if (UUID.TryParse(args[0], out ToGroupID))
            {
                string message = string.Empty;
                for (int ct = 1; ct < args.Length; ct++)
                    message += args[ct] + " ";

                message = message.TrimEnd();
                if (message.Length > 1023)
                {
                    message = message.Remove(1023);
                    Console.WriteLine("Message truncated at 1024 characters");
                }

                Client.Self.GroupChatJoined += Self_GroupChatJoined;

                if (!Client.Self.GroupChatSessions.ContainsKey(ToGroupID))
                {
                    WaitForSessionStart.Reset();
                    Client.Self.RequestJoinGroupChat(ToGroupID);
                }
                else
                {
                    WaitForSessionStart.Set();
                }
                
                if (WaitForSessionStart.WaitOne(TimeSpan.FromSeconds(20), false))
                {
                    Client.Self.InstantMessageGroup(ToGroupID, message);
                }
                else
                {
                    return "Timeout waiting for group session start";
                }

                Client.Self.GroupChatJoined -= Self_GroupChatJoined;
                return "Instant Messaged group " + ToGroupID + " with message: " + message;
            }
            else
            {
                return "failed to instant message group";
            }
        }

        void Self_GroupChatJoined(object sender, GroupChatJoinedEventArgs e)
        {
            if (e.Success)
            {
                Console.WriteLine("Joined {0} Group Chat Success!", e.SessionName);
                WaitForSessionStart.Set();
            }
            else
            {
                Console.WriteLine("Join Group Chat failed :(");
            }
        }

       
    }
}
