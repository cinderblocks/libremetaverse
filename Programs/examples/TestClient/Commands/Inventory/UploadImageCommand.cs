/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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
using System.Threading;
using OpenMetaverse;
using SkiaSharp;

namespace TestClient.Commands.Inventory
{
    public class UploadImageCommand : Command
    {
        AutoResetEvent UploadCompleteEvent = new AutoResetEvent(false);
        UUID TextureID = UUID.Zero;
        DateTime start;

        public UploadImageCommand(TestClient testClient)
        {
            Name = "uploadimage";
            Description = "Upload an image to your inventory. Usage: uploadimage [inventoryname] [timeout] [filename]";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            uint timeout;

            if (args.Length != 3)
                return "Usage: uploadimage [inventoryname] [timeout] [filename]";

            TextureID = UUID.Zero;
            var inventoryName = args[0];
            var fileName = args[2];
            if (!uint.TryParse(args[1], out timeout))
                return "Usage: uploadimage [inventoryname] [timeout] [filename]";

            Console.WriteLine("Loading image " + fileName);
            byte[] jpeg2k = LoadImage(fileName);
            if (jpeg2k == null)
                return "Failed to compress image to JPEG2000";
            Console.WriteLine("Finished compressing image to JPEG2000, uploading...");
            start = DateTime.Now;
            DoUpload(jpeg2k, inventoryName);

            return UploadCompleteEvent.WaitOne((int)timeout, false) 
                ? $"Texture upload {((TextureID != UUID.Zero) ? "succeeded" : "failed")}: {TextureID}" 
                : "Texture upload timed out";
        }

        private void DoUpload(byte[] UploadData, string FileName)
        {
            if (UploadData != null)
            {
                string name = global::System.IO.Path.GetFileNameWithoutExtension(FileName);

                Client.Inventory.RequestCreateItemFromAsset(UploadData, name, "Uploaded with TestClient",
                    AssetType.Texture, InventoryType.Texture, Client.Inventory.FindFolderForType(AssetType.Texture),
                    delegate(bool success, string status, UUID itemID, UUID assetID)
                    {
                        Console.WriteLine(
                            "RequestCreateItemFromAsset() returned: Success={0}, Status={1}, ItemID={2}, AssetID={3}", 
                            success, status, itemID, assetID);

                        TextureID = assetID;
                        Console.WriteLine("Upload took {0}", DateTime.Now.Subtract(start));
                        UploadCompleteEvent.Set();
                    }
                );
            }
        }

        private byte[] LoadImage(string fileName)
        {
            byte[] uploadData;
            string lowfilename = fileName.ToLower();
            SKBitmap bitmap = null;
            try
            {
                if (lowfilename.EndsWith(".jp2") || lowfilename.EndsWith(".j2c"))
                {
                    // Upload JPEG2000 images untouched
                    uploadData = global::System.IO.File.ReadAllBytes(fileName);
                }
                else
                {
                    if (lowfilename.EndsWith(".tga") || lowfilename.EndsWith(".targa"))
                    {
                        bitmap = OpenMetaverse.Imaging.Targa.Decode(fileName);
                    }
                    else
                    {
                        var img = SKImage.FromEncodedData(fileName);
                        bitmap = SKBitmap.FromImage(img);
                    }

                    int oldwidth = bitmap.Width;
                    int oldheight = bitmap.Height;

                    if (!IsPowerOfTwo((uint)oldwidth) || !IsPowerOfTwo((uint)oldheight))
                    {
                        var info = new SKImageInfo(256, 256);
                        var scaledImage = new SKBitmap(info);
                        bitmap.ScalePixels(scaledImage.PeekPixels(), new SKSamplingOptions(SKFilterMode.Linear));

                        bitmap.Dispose();
                        bitmap = scaledImage;

                        oldwidth = 256;
                        oldheight = 256;
                    }

                    // Handle resizing to prevent excessively large images
                    if (oldwidth > 1024 || oldheight > 1024)
                    {
                        int newwidth = (oldwidth > 1024) ? 1024 : oldwidth;
                        int newheight = (oldheight > 1024) ? 1024 : oldheight;

                        var info = new SKImageInfo(newwidth, newheight);
                        var scaledImage = new SKBitmap(info);
                        bitmap.ScalePixels(scaledImage.PeekPixels(), new SKSamplingOptions(SKFilterMode.Linear));

                        bitmap.Dispose();
                        bitmap = scaledImage;
                    }
                    uploadData = OpenMetaverse.Imaging.J2K.ToBytes(bitmap);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex + " SL Image Upload ");
                return null;
            }
            finally
            {
                bitmap?.Dispose();
            }
            return uploadData;
        }

        private static bool IsPowerOfTwo(uint n)
        {
            return (n & (n - 1)) == 0 && n != 0;
        }
    }
}