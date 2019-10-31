using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace OpenMetaverse.TestClient
{
    /// <summary>
    /// Example of how to put a new script in your inventory
    /// </summary>
    public class UploadScriptCommand : Command
    {
        /// <summary>
        ///  The default constructor for TestClient commands
        /// </summary>
        /// <param name="testClient"></param>
        public UploadScriptCommand(TestClient testClient)
        {
            Name = "uploadscript";
            Description = "Upload a local .lsl file file into your inventory.";
            Category = CommandCategory.Inventory;
        }

        /// <summary>
        /// The default override for TestClient commands
        /// </summary>
        /// <param name="args"></param>
        /// <param name="fromAgentID"></param>
        /// <returns></returns>
        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: uploadscript filename.lsl";

            string file = args.Aggregate(string.Empty, (current, t) => $"{current}{t} ");
            file = file.TrimEnd();

            if (!File.Exists(file))
            {
                return $"Filename '{file}' does not exist";
            }

            string ret = $"Filename: {file}";

            try
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    string body = reader.ReadToEnd();
                    string desc = $"{file} created by LibreMetaverse TestClient {DateTime.Now}";
                    // create the asset
                    Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.LSLText), 
                        file, desc, AssetType.LSLText, UUID.Random(), InventoryType.LSL, PermissionMask.All,
                    delegate(bool success, InventoryItem item)
                    {
                        if (success)
                            // upload the asset
                            Client.Inventory.RequestUpdateScriptAgentInventory(EncodeScript(body), item.UUID, true, 
                                delegate(bool uploadSuccess, string uploadStatus, bool compileSuccess, List<string> compileMessages, UUID itemid, UUID assetid)
                                {
                                    if (uploadSuccess)
                                        ret += $" Script successfully uploaded, ItemID {itemid} AssetID {assetid}";
                                    if (compileSuccess)
                                        ret += " compilation successful";

                                });
                    });
                }
                return ret;

            }
            catch (System.Exception e)
            {
                Logger.Log(e.ToString(), Helpers.LogLevel.Error, Client);
                return $"Error creating script for {ret}";
            }
        }
        /// <summary>
        /// Encodes the script text for uploading
        /// </summary>
        /// <param name="body"></param>
        public static byte[] EncodeScript(string body)
        {
            // Assume this is a string, add 1 for the null terminator ?
            byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(body);
            byte[] assetData = new byte[stringBytes.Length]; //+ 1];
            Array.Copy(stringBytes, 0, assetData, 0, stringBytes.Length);
            return assetData;
        }
    }

}
