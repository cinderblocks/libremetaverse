using System.Collections.Generic;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Groups
{
    public class InviteGroupCommand : Command
    {
        public InviteGroupCommand(TestClient testClient)
        {
            Name = "invitegroup";
            Description = "invite an avatar into a group. Usage: invitegroup AvatarUUID GroupUUID RoleUUID*";
            Category = CommandCategory.Groups;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 2)
            {
                return Task.FromResult(Description);
            }

            UUID avatar = UUID.Zero;
            UUID group = UUID.Zero;
            UUID role = UUID.Zero;
            List<UUID> roles = new List<UUID>();

            if (!UUID.TryParse(args[0], out avatar)) { return Task.FromResult("parse error avatar UUID"); }
            if (!UUID.TryParse(args[1], out group)) { return Task.FromResult("parse error group UUID"); }

            if (2 == args.Length)
            {
                roles.Add(UUID.Zero);
            }
            else
            {
                for (int i = 2; i < args.Length; i++)
                {
                    if (UUID.TryParse(args[i], out role))
                    {
                        roles.Add(role);
                    }
                }
            }

            Client.Groups.Invite(group, roles, avatar);

            return Task.FromResult("invited " + avatar + " to " + group);
        }
    }
}
