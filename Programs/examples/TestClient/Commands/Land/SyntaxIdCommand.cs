using System.Text;
using LibreMetaverse;

namespace OpenMetaverse.TestClient
{
    public class SyntaxIdCommand : Command
    {
        public SyntaxIdCommand(TestClient testClient)
        {
            Name = "syntaxid";
            Description = "Downloads lsl syntax file for current simulator";
            Category = CommandCategory.Simulator;
        }
        
        public override string Execute(string[] args, UUID fromAgentID)
        {
            var syntax = new LibreMetaverse.LslSyntax(Client);
            
            var output = new StringBuilder("LSL Tokens:");
            output.AppendLine();
            foreach (var token in LslSyntax.Keywords.Keys)
            {
                output.AppendLine(token);
            }
            return output.ToString();
        }
    }
}