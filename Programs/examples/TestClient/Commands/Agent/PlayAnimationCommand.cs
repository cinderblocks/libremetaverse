using System.Collections.Frozen;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibreMetaverse;

namespace TestClient.Commands.Agent
{
    public class PlayAnimationCommand : Command
    {
        private readonly FrozenDictionary<UUID, string> m_BuiltInAnimations = Animations.ToDictionary();
        public PlayAnimationCommand(TestClient testClient)
        {
            Name = "play";
            Description = "Attempts to play an animation";
            Category = CommandCategory.Appearance;
        }

        private string Usage()
        {
            const string usage = "Usage:\n" +
                                 "\tplay list - list the built in animations\n" +
                                 "\tplay show - show any currently playing animations\n" +
                                 "\tplay UUID - play an animation asset\n" +
                                 "\tplay ANIMATION - where ANIMATION is one of the values returned from \"play list\"\n";
            return usage;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            StringBuilder result = new StringBuilder();
            if (args.Length != 1)
                return Task.FromResult(Usage());

            UUID animationID;
            string arg = args[0].Trim();

            if (UUID.TryParse(args[0], out animationID))
            {
                Client.Self.AnimationStart(animationID, true);
            }
            else if (arg.ToLower().Equals("list"))
            {
                foreach (string key in m_BuiltInAnimations.Values)
                {
                    result.AppendLine(key);
                }
            }
            else if (arg.ToLower().Equals("show"))
            {
                foreach (var kvp in Client.Self.SignaledAnimations)
                {
                    if (m_BuiltInAnimations.TryGetValue(kvp.Key, out var animation))
                    {
                        result.AppendFormat("The {0} System Animation is being played, sequence is {1}", animation, kvp.Value);
                    }
                    else
                    {
                        result.AppendFormat("The {0} Asset Animation is being played, sequence is {1}", kvp.Key, kvp.Value);
                    }
                }
            }
            else if (m_BuiltInAnimations.Values.Contains(args[0].Trim().ToUpper()))
            {
                foreach (var kvp in m_BuiltInAnimations)
                {
                    if (kvp.Value.Equals(arg.ToUpper()))
                    {
                        Client.Self.AnimationStart(kvp.Key, true);
                        break;
                    }
                }
            }
            else
            {
                return Task.FromResult(Usage());
            }

            return Task.FromResult(result.ToString());
        }
    }
}
