using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace TestClient.Commands.Directory
{
    class key2nameCommand : Command
    {
        global::System.Threading.AutoResetEvent waitQuery = new global::System.Threading.AutoResetEvent(false);
        StringBuilder result = new StringBuilder();
        public key2nameCommand(TestClient testClient)
        {
            Name = "key2name";
            Description = "resolve a UUID to an avatar or group name. Usage: key2name UUID";
            Category = CommandCategory.Search;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: key2name UUID";

            UUID key;
            if(!UUID.TryParse(args[0].Trim(), out key))
            {
                return "UUID " + args[0].Trim() + " appears to be invalid";
            }
            result.Remove(0, result.Length);
            waitQuery.Reset();
            
            Client.Avatars.UUIDNameReply += Avatars_OnAvatarNames;
            Client.Groups.GroupProfile += Groups_OnGroupProfile;
            Client.Avatars.RequestAvatarName(key);            
            
            Client.Groups.RequestGroupProfile(key);
            if (!waitQuery.WaitOne(TimeSpan.FromSeconds(10), false))
            {
                result.AppendLine("Timeout waiting for reply, this could mean the Key is not an avatar or a group");
            }

            Client.Avatars.UUIDNameReply -= Avatars_OnAvatarNames;
            Client.Groups.GroupProfile -= Groups_OnGroupProfile;
            return result.ToString();
        }

        void Groups_OnGroupProfile(object sender, GroupProfileEventArgs e)
        {
            result.AppendLine("Group: " + e.Group.Name + " " + e.Group.ID);
            waitQuery.Set();
        }

        void Avatars_OnAvatarNames(object sender, UUIDNameReplyEventArgs e)
        {
            foreach (KeyValuePair<UUID, string> kvp in e.Names)
                result.AppendLine("Avatar: " + kvp.Value + " " + kvp.Key);
            waitQuery.Set();
        }        
    }
}
