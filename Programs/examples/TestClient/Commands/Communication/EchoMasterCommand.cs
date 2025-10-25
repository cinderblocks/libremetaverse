using OpenMetaverse;

namespace TestClient.Commands.Communication
{
    public class EchoMasterCommand: Command
    {
        public EchoMasterCommand(TestClient testClient)
		{
			Name = "echoMaster";
			Description = "Repeat everything that master says.";
            Category = CommandCategory.Communication;
		}

        public override string Execute(string[] args, UUID fromAgentID)
		{
			if (!Active)
			{
				Active = true;
                Client.Self.ChatFromSimulator += Self_ChatFromSimulator;
				return "Echoing is now on.";
			}
			else
			{
				Active = false;
                Client.Self.ChatFromSimulator -= Self_ChatFromSimulator;
				return "Echoing is now off.";
			}
		}

        void Self_ChatFromSimulator(object sender, ChatEventArgs e)
        {
            if (e.Message.Length > 0 && (Client.MasterKey == e.SourceID || (Client.MasterName == e.FromName && !Client.AllowObjectMaster)))
                Client.Self.Chat(e.Message, 0, ChatType.Normal);
        }		
    }
}
