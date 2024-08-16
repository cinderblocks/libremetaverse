using System.Linq;
using System.Text.RegularExpressions;

namespace OpenMetaverse.TestClient
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
                // Build the predicat from the args list
                string predicatPrim = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
                predicatPrim = predicatPrim.TrimEnd();

                // Build Regex
                Regex regexPrimName = new Regex(predicatPrim.ToLower());

                // Print result
                Logger.Log(
                    $"Searching prim for [{predicatPrim}] ({Client.Network.CurrentSim.ObjectsPrimitives.Count} prims loaded in simulator)\n", Helpers.LogLevel.Info, Client);

                foreach (var pair in Client.Network.CurrentSim.ObjectsPrimitives)
                {
                    var prim = pair.Value;

                    bool match = false;
                    string name = "(unknown)";
                    string description = "(unknown)";


                    match = (prim.Text != null && regexPrimName.IsMatch(prim.Text.ToLower()));

                    if (prim.Properties != null && !match)
                    {
                        match = regexPrimName.IsMatch(prim.Properties.Name.ToLower());
                        if (!match)
                            match = regexPrimName.IsMatch(prim.Properties.Description.ToLower());
                    }

                    if (match)
                    {
                        if (prim.Properties != null)
                        {
                            name = prim.Properties.Name;
                            description = prim.Properties.Description;
                        }

                        Logger.Log(
                                   $"\nNAME={name}\nID = {prim.ID}\nFLAGS = {prim.Flags.ToString()}\nTEXT = '{prim.Text}'\nDESC='{description}'",
                                   Helpers.LogLevel.Info,
                                   Client);
                    }
                }
            }
            catch (System.Exception e)
            {
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                return "Error searching";
            }

            return "Done searching";
        }
    }
}
