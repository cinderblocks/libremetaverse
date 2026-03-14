using OpenMetaverse;

namespace TestClient.Commands.Prims
{
    public class FindTextureCommand : Command
    {
        public FindTextureCommand(TestClient testClient)
        {
            Name = "findtexture";
            Description = "Checks if a specified texture is currently visible on a specified face. " +
                "Usage: findtexture [face-index] [texture-uuid]";
            Category = CommandCategory.Objects;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 2)
            {
                return "Usage: findtexture [face-index] [texture-uuid]";
            }

            if (int.TryParse(args[0], out var faceIndex) &&
                UUID.TryParse(args[1], out var textureID))
            {
                var currentSim = Client.Network?.CurrentSim;
                if (currentSim == null)
                {
                    return "No current simulator available";
                }

                foreach (var kvp in currentSim.ObjectsPrimitives)
                {
                    if (kvp.Value == null) { continue; }

                    var prim = kvp.Value;
                    var tex = prim.Textures;
                    if (tex == null || tex.FaceTextures == null) { continue; }
                    if (faceIndex < 0 || faceIndex >= tex.FaceTextures.Length) { continue; }
                    var face = tex.FaceTextures[faceIndex];
                    if (face == null) { continue; }
                    if (face.TextureID == textureID)
                    {
                        Logger.Info($"Primitive {prim.ID} ({prim.LocalID}) has face index {faceIndex} set to {textureID}", Client);
                    }
                }

                return "Done searching";
            }

            return "Usage: findtexture [face-index] [texture-uuid]";
        }
    }
}

