using System.Linq;
using System.Text.RegularExpressions;
using OpenMetaverse;

namespace TestClient.Commands.Prims
{
    public class PrimRegexCommand : Command
    {
        public PrimRegexCommand(TestClient testClient)
        {
            Name = "primregex";
            Description = "Find prim by text predicat. " +
                "Usage: primregex [text predicat] (eg findprim .away.)";
            Category = CommandCategory.Objects;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: primregex [text predicat]";

            try
            {
                // Build the predicate from the args list
                var predicatePrim = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
                predicatePrim = predicatePrim.TrimEnd();

                // Build Regex
                var regexPrimName = new Regex(predicatePrim.ToLower());

                // Print result
                Logger.Info($"Searching prim for [{predicatePrim}] ({Client.Network.CurrentSim.ObjectsPrimitives.Count} prims loaded in simulator)\n", Client);

                foreach (var kvp in Client.Network.CurrentSim.ObjectsPrimitives)
                {
                    if (kvp.Value == null) { continue; }

                    var prim = kvp.Value;
                    var name = "(unknown)";
                    var description = "(unknown)";

                    var match = (prim.Text != null && regexPrimName.IsMatch(prim.Text.ToLower()));

                    if (prim.Properties != null && !match)
                    {
                        match = regexPrimName.IsMatch(prim.Properties.Name.ToLower());
                        if (!match)
                            match = regexPrimName.IsMatch(prim.Properties.Description.ToLower());
                    }

                    if (!match) { continue; }

                    if (prim.Properties != null)
                    {
                        name = prim.Properties.Name;
                        description = prim.Properties.Description;
                    }
                    Logger.Info($"\nNAME={name}\nID = {prim.ID}\nFLAGS = {prim.Flags.ToString()}\nTEXT = '{prim.Text}'\nDESC='{description}'", Client);
                }
            }
            catch (global::System.Exception e)
            {
                Logger.Error(e.Message, e, Client);
                return "Error searching";
            }

            return "Done searching";
        }
    }
}

