using System.Collections.Concurrent;
using System.Threading.Tasks;
using LibreMetaverse;

namespace TestClient.Commands.Prims
{
    public class TexturesCommand : Command
    {
        ConcurrentDictionary<UUID, UUID> alreadyRequested = new ConcurrentDictionary<UUID, UUID>();
        bool enabled = false;

        public TexturesCommand(TestClient testClient)
        {
            enabled = testClient.ClientManager.GetTextures;

            Name = "textures";
            Description = "Turns automatic texture downloading on or off. Usage: textures [on/off]";
            Category = CommandCategory.Objects;
            
            testClient.Objects.ObjectUpdate += Objects_OnNewPrim;            
            testClient.Objects.AvatarUpdate += Objects_OnNewAvatar;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: textures [on/off]";

            if (args[0].ToLower() == "on")
            {
                Client.ClientManager.GetTextures = enabled = true;
                return "Texture downloading is on";
            }
            else if (args[0].ToLower() == "off")
            {
                Client.ClientManager.GetTextures = enabled = false;
                return "Texture downloading is off";
            }
            else
            {
                return "Usage: textures [on/off]";
            }
        }

        void Objects_OnNewAvatar(object sender, AvatarUpdateEventArgs e)
        {
            Avatar avatar = e.Avatar;
            if (enabled)
            {
                // Search this avatar for textures
                for (int i = 0; i < avatar.Textures.FaceTextures.Length; i++)
                {
                    Primitive.TextureEntryFace face = avatar.Textures.FaceTextures[i];

                    if (face != null)
                    {
                        if (alreadyRequested.TryAdd(face.TextureID, face.TextureID))
                        {

                            // Determine if this is a baked outfit texture or a normal texture
                            ImageType type = ImageType.Normal;
                            AvatarTextureIndex index = (AvatarTextureIndex)i;
                            switch (index)
                            {
                                case AvatarTextureIndex.EyesBaked:
                                case AvatarTextureIndex.HeadBaked:
                                case AvatarTextureIndex.LowerBaked:
                                case AvatarTextureIndex.SkirtBaked:
                                case AvatarTextureIndex.UpperBaked:
                                    type = ImageType.Baked;
                                    break;
                            }

                            _ = Client.Assets.RequestImageAsync(face.TextureID, type).ContinueWith(
                                t => { if (t.Status == global::System.Threading.Tasks.TaskStatus.RanToCompletion && t.Result != null && enabled && alreadyRequested.ContainsKey(t.Result.AssetID)) Logger.DebugLog($"Finished downloading texture {t.Result.AssetID} ({t.Result.AssetData.Length} bytes)"); },
                                TaskContinuationOptions.ExecuteSynchronously);
                        }
                    }
                }
            }
        }

        void Objects_OnNewPrim(object sender, PrimEventArgs e)
        {
            Primitive prim = e.Prim;

            if (enabled)
            {
                // Search this prim for textures
                foreach (var face in prim.Textures.FaceTextures)
                {
                    if (face == null) continue;
                    if (!alreadyRequested.ContainsKey(face.TextureID))
                    {
                        alreadyRequested[face.TextureID] = face.TextureID;
                        _ = Client.Assets.RequestImageAsync(face.TextureID).ContinueWith(
                            t => { if (t.Status == global::System.Threading.Tasks.TaskStatus.RanToCompletion && t.Result != null && enabled && alreadyRequested.ContainsKey(t.Result.AssetID)) Logger.DebugLog($"Finished downloading texture {t.Result.AssetID} ({t.Result.AssetData.Length} bytes)"); },
                            TaskContinuationOptions.ExecuteSynchronously);
                    }
                }
            }
        }

    }
}
