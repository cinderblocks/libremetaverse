using System;
using System.Linq;

namespace OpenMetaverse.TestClient
{
    public class SetMasterKeyCommand : Command
    {
        public DateTime Created = DateTime.Now;

        public SetMasterKeyCommand(TestClient testClient)
        {
            Name = "setMasterKey";
            Description = "Sets the key of the master user.  The master user can IM to run commands.";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            Client.MasterKey = UUID.Parse(args[0]);

            lock (Client.Network.Simulators)
            {
                foreach (var master in Client.Network.Simulators
                             .Select(sim => sim.ObjectsAvatars.FirstOrDefault(
                                kvp => kvp.Value.ID == Client.MasterKey))
                             .Where(master => master.Value != null))
                {
                    Client.Self.InstantMessage(master.Value.ID,
                        "You are now my master. IM me with \"help\" for a command list.");
                    break;
                }
            }

            return "Master set to " + Client.MasterKey;
        }
    }
}
