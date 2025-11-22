using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Directory
{
    class key2nameCommand : Command
    {
        // Keep legacy synchronous primitives for compatibility but prefer async
        StringBuilder result = new StringBuilder();
        public key2nameCommand(TestClient testClient)
        {
            Name = "key2name";
            Description = "resolve a UUID to an avatar or group name. Usage: key2name UUID";
            Category = CommandCategory.Search;
        }

        // Synchronous compatibility wrapper
        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: key2name UUID";

            UUID key;
            if (!UUID.TryParse(args[0].Trim(), out key))
            {
                return "UUID " + args[0].Trim() + " appears to be invalid";
            }

            result.Clear();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<GroupProfileEventArgs> groupHandler = null;
            EventHandler<UUIDNameReplyEventArgs> avatarHandler = null;

            groupHandler = (sender, e) =>
            {
                // Group is a value type, compare by ID
                if (e.Group.ID == key)
                {
                    result.AppendLine("Group: " + e.Group.Name + " " + e.Group.ID);
                    tcs.TrySetResult(true);
                }
            };

            avatarHandler = (sender, e) =>
            {
                if (e.Names != null && e.Names.Count > 0 && e.Names.ContainsKey(key))
                {
                    result.AppendLine("Avatar: " + e.Names[key] + " " + key);
                    tcs.TrySetResult(true);
                }
            };

            try
            {
                Client.Avatars.UUIDNameReply += avatarHandler;
                Client.Groups.GroupProfile += groupHandler;

                // Fire requests
                Client.Avatars.RequestAvatarName(key);
                Client.Groups.RequestGroupProfile(key);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    result.AppendLine("Timeout waiting for reply, this could mean the Key is not an avatar or a group");
                }
            }
            finally
            {
                Client.Avatars.UUIDNameReply -= avatarHandler;
                Client.Groups.GroupProfile -= groupHandler;
            }

            return result.ToString();
        }

        void Groups_OnGroupProfile(object sender, GroupProfileEventArgs e)
        {
            result.AppendLine("Group: " + e.Group.Name + " " + e.Group.ID);
        }

        void Avatars_OnAvatarNames(object sender, UUIDNameReplyEventArgs e)
        {
            foreach (KeyValuePair<UUID, string> kvp in e.Names)
                result.AppendLine("Avatar: " + kvp.Value + " " + kvp.Key);
        }
    }
}
