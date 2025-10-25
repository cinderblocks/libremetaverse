/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2022, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names 
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CoreJ2K;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using SkiaSharp;
using Targa = OpenMetaverse.Imaging.Targa;

namespace TestClient.Commands.Inventory
{
    public class DumpOutfitCommand : Command
    {
        List<UUID> OutfitAssets = new List<UUID>();

        public DumpOutfitCommand(TestClient testClient)
        {
            Name = "dumpoutfit";
            Description = "Dumps all of the textures from an avatars outfit to the hard drive. Usage: dumpoutfit [avatar-uuid]";
            Category = CommandCategory.Inventory;

        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: dumpoutfit [avatar-uuid]";

            UUID target;

            if (!UUID.TryParse(args[0], out target))
                return "Usage: dumpoutfit [avatar-uuid]";

            lock (Client.Network.Simulators)
            {
                foreach (var sim in Client.Network.Simulators)
                {
                   var kvp = sim.ObjectsAvatars.FirstOrDefault(
                       avatar => avatar.Value.ID == target);

                    if (kvp.Value != null)
                    {
                        var targetAv = kvp.Value;
                        StringBuilder output = new StringBuilder("Downloading ");

                        lock (OutfitAssets) OutfitAssets.Clear();

                        for (int j = 0; j < targetAv.Textures.FaceTextures.Length; j++)
                        {
                            Primitive.TextureEntryFace face = targetAv.Textures.FaceTextures[j];

                            if (face != null)
                            {
                                ImageType type = ImageType.Normal;

                                switch ((AvatarTextureIndex)j)
                                {
                                    case AvatarTextureIndex.HeadBaked:
                                    case AvatarTextureIndex.EyesBaked:
                                    case AvatarTextureIndex.UpperBaked:
                                    case AvatarTextureIndex.LowerBaked:
                                    case AvatarTextureIndex.SkirtBaked:
                                        type = ImageType.Baked;
                                        break;
                                }

                                OutfitAssets.Add(face.TextureID);
                                Client.Assets.RequestImage(face.TextureID, type, Assets_OnImageReceived);
                                output.Append(((AvatarTextureIndex)j).ToString());
                                output.Append(' ');
                            }
                        }

                        return output.ToString();
                    }
                }
            }

            return "Couldn't find avatar " + target;
        }

        private void Assets_OnImageReceived(TextureRequestState state, AssetTexture assetTexture)
        {
            lock (OutfitAssets)
            {
                if (OutfitAssets.Contains(assetTexture.AssetID))
                {
                    if (state == TextureRequestState.Finished)
                    {
                        try
                        {
                            File.WriteAllBytes(assetTexture.AssetID + ".jp2", assetTexture.AssetData);
                            Console.WriteLine($"Wrote JPEG2000 image {assetTexture.AssetID}.jp2");

                            using (var bitmap = J2kImage.FromBytes(assetTexture.AssetData).As<SKBitmap>())
                            {
                                var mi = new ManagedImage(bitmap);
                                var bytes = Targa.Encode(mi);
                                File.WriteAllBytes(assetTexture.AssetID + ".tga", bytes);
                                Console.WriteLine($"Wrote TGA image {assetTexture.AssetID}.tga");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to download image {assetTexture.AssetID}");
                    }

                    OutfitAssets.Remove(assetTexture.AssetID);
                }
            }
        }
    }
}
