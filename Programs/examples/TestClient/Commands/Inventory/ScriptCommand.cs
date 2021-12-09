using System;
using System.IO;

namespace OpenMetaverse.TestClient
{
    public class ScriptCommand : Command
    {
        public ScriptCommand(TestClient testClient)
        {
            Name = "script";
            Description = "Reads TestClient commands from a file. One command per line, arguments separated by spaces. Usage: script [filename]";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: script [filename]";

            // Load the file
            string[] lines;
            try { lines = File.ReadAllLines(args[0]); }
            catch (Exception e) { return e.Message; }

            // Execute all of the commands
            foreach (var l in lines)
            {
                string line = l.Trim();

                if (line.Length > 0)
                    ClientManager.Instance.DoCommandAll(line, UUID.Zero);
            }

            return "Finished executing " + lines.Length + " commands";
        }
    }
}
