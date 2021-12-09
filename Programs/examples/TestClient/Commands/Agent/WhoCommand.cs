using System.Text;

namespace OpenMetaverse.TestClient
{
    public class WhoCommand: Command
    {
        public WhoCommand(TestClient testClient)
		{
			Name = "who";
			Description = "Lists seen avatars.";
            Category = CommandCategory.Other;
		}

        public override string Execute(string[] args, UUID fromAgentID)
		{
			StringBuilder result = new StringBuilder();

            lock (Client.Network.Simulators)
            {
                foreach (var sim
                    in Client.Network.Simulators)
                {
                    sim
.ObjectsAvatars.ForEach(
                        delegate(Avatar av)
                        {
                            result.AppendLine();
                            result.AppendFormat("{0} (Group: {1}, Location: {2}, UUID: {3} LocalID: {4})",
                                av.Name, av.GroupName, av.Position, av.ID, av.LocalID);
                        }
                    );
                }
            }

            return result.ToString();
		}
    }
}
