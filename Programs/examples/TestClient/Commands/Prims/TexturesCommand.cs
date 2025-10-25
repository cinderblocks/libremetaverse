using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.Assets;

namespace TestClient.Commands.Prims
{
    public class TexturesCommand : Command
    {
        Dictionary<UUID, UUID> alreadyRequested = new Dictionary<UUID, UUID>();
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
                        if (!alreadyRequested.ContainsKey(face.TextureID))
                        {
                            alreadyRequested[face.TextureID] = face.TextureID;

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

                            Client.Assets.RequestImage(face.TextureID, type, Assets_OnImageReceived);
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
                        Client.Assets.RequestImage(face.TextureID, ImageType.Normal, Assets_OnImageReceived);
                    }
                }
            }
        }

        private void Assets_OnImageReceived(TextureRequestState state, AssetTexture asset)
        {
            if (state == TextureRequestState.Finished && enabled && alreadyRequested.ContainsKey(asset.AssetID))
            {
                Logger.DebugLog($"Finished downloading texture {asset.AssetID} ({asset.AssetData.Length} bytes)");
            }
        }
    }
}
