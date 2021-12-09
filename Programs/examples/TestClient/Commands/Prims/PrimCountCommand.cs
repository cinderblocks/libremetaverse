using System;

namespace OpenMetaverse.TestClient
{
    public class PrimCountCommand: Command
    {
        public PrimCountCommand(TestClient testClient)
		{
			Name = "primcount";
			Description = "Shows the number of objects currently being tracked.";
            Category = CommandCategory.TestClient;
		}

        public override string Execute(string[] args, UUID fromAgentID)
		{
            int count = 0;

            lock (Client.Network.Simulators)
            {
                foreach (var sim in Client.Network.Simulators)
                {
                    int avcount = sim.ObjectsAvatars.Count;
                    int primcount = sim.ObjectsPrimitives.Count;

                    Console.WriteLine("{0} (Avatars: {1} Primitives: {2})", 
                        sim.Name, avcount, primcount);

                    count += avcount;
                    count += primcount;
                }
            }

			return "Tracking a total of " + count + " objects";
		}
    }
}
