using System;
using System.Security.Permissions;

namespace OpenMetaverse.TestClient
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
                foreach (var kvp in Client.Network.CurrentSim.ObjectsPrimitives)
                {
                    if (kvp.Value == null) { continue; }

                    var prim = kvp.Value;
                    if (prim.Textures?.FaceTextures[faceIndex] == null) { continue; }
                    if (prim.Textures.FaceTextures[faceIndex].TextureID == textureID)
                    {
                        Logger.Log(
                            $"Primitive {prim.ID.ToString()} ({prim.LocalID}) has face index {faceIndex} set to {textureID.ToString()}",
                            Helpers.LogLevel.Info, Client);
                    }
                }

                return "Done searching";
            }

            return "Usage: findtexture [face-index] [texture-uuid]";
        }
    }
}
