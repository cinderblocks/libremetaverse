using System.Text;
using OpenMetaverse.Packets;

namespace OpenMetaverse.TestClient
{
    /// <summary>
    /// Sends a packet of type GenericMessage to the simulator.
    /// </summary>
    public class GenericMessageCommand : Command
    {
        public GenericMessageCommand(TestClient testClient)
        {
            Name = "sendgeneric";
            Description = "send a generic UDP message to the simulator.";
            Category = CommandCategory.Other;        
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: sendgeneric method_name [value1 value2 ...]";

            string methodName = args[0];

            GenericMessagePacket gmp = new GenericMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Method = Utils.StringToBytes(methodName),
                    Invoice = UUID.Zero
                },
                ParamList = new GenericMessagePacket.ParamListBlock[args.Length - 1]
            };

            StringBuilder sb = new StringBuilder();

            for (int i = 1; i < args.Length; i++)
            {
                GenericMessagePacket.ParamListBlock paramBlock = new GenericMessagePacket.ParamListBlock
 {
     Parameter = Utils.StringToBytes(args[i])
 };
                gmp.ParamList[i - 1] = paramBlock;
                sb.AppendFormat(" {0}", args[i]);
            }

            Client.Network.SendPacket(gmp);

            return $"Sent generic message with method {methodName}, params{sb}";
        }
    }
}