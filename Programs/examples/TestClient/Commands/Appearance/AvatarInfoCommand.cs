using System.Linq;
using System.Text;
using OpenMetaverse;

namespace TestClient.Commands.Appearance
{
    public class AvatarInfoCommand : Command
    {
        public AvatarInfoCommand(TestClient testClient)
        {
            Name = "avatarinfo";
            Description = "Print out information on a nearby avatar. Usage: avatarinfo [firstname] [lastname]";
            Category = CommandCategory.Appearance;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 2)
                return "Usage: avatarinfo [firstname] [lastname]";

            string targetName = $"{args[0]} {args[1]}";

            var kvp = Client.Network.CurrentSim.ObjectsAvatars.SingleOrDefault(
                avatar => (avatar.Value.Name == targetName));

            if (kvp.Value != null)
            {
                var foundAv = kvp.Value;
                StringBuilder output = new StringBuilder();

                output.AppendFormat("{0} ({1})", targetName, foundAv.ID);
                output.AppendLine();

                for (int i = 0; i < foundAv.Textures.FaceTextures.Length; i++)
                {
                    if (foundAv.Textures.FaceTextures[i] != null)
                    {
                        Primitive.TextureEntryFace face = foundAv.Textures.FaceTextures[i];
                        AvatarTextureIndex type = (AvatarTextureIndex)i;

                        output.AppendFormat("{0}: {1}", type, face.TextureID);
                        output.AppendLine();
                    }
                }

                return output.ToString();
            }
            else
            {
                return $"No nearby avatar named {targetName}";
            }
        }
    }
}
