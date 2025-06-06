using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace OpenMetaverse.TestClient
{
    public class PlayAnimationCommand : Command
    {        
        private readonly ImmutableDictionary<UUID, string> m_BuiltInAnimations = Animations.ToDictionary();
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
            StringBuilder result = new StringBuilder();
            if (args.Length != 1)
                return Usage();

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
                Client.Self.SignaledAnimations.ForEach(delegate(KeyValuePair<UUID, int> kvp) {
                    if (m_BuiltInAnimations.TryGetValue(kvp.Key, out var animation))
                    {
                        result.AppendFormat("The {0} System Animation is being played, sequence is {1}", animation, kvp.Value);
                    }
                    else
                    {
                        result.AppendFormat("The {0} Asset Animation is being played, sequence is {1}", kvp.Key, kvp.Value);
                    }
                });                                
            }
            else if (m_BuiltInAnimations.ContainsValue(args[0].Trim().ToUpper()))
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
                return Usage();
            }

            return result.ToString();
        }
    }
}
