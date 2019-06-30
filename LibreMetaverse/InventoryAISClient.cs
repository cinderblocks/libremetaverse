using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse
{
    public class InventoryAISClient
    {
        public const string INVENTORY_CAP_NAME = "InventoryAPIv3";
        public const string LIBRARY_CAP_NAME = "LibraryAPIv3";

        [NonSerialized]
        private readonly GridClient Client;

        private static readonly HttpClient httpClient = new HttpClient();

        public InventoryAISClient(GridClient client)
        {
            Client = client;
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "LibreMetaverse AIS Client");
        }

        public bool IsAvailable => (Client.Network.CurrentSim.Caps != null &&
                                    Client.Network.CurrentSim.Caps.CapabilityURI(INVENTORY_CAP_NAME) != null);

        public async Task CreateInventory(UUID parentUuid, OSD newInventory, bool createLink, InventoryManager.ItemCreatedCallback callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                UUID tid = new UUID();
                string url = $"{cap}/category/{parentUuid}?tid={tid}";
                var content = new ByteArrayContent(OSDParser.SerializeLLSDXmlBytes(newInventory));
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/llsd+xml");
                var req = httpClient.PostAsync(url, content);
                var reply = await req;

                if (reply.IsSuccessStatusCode 
                    && OSDParser.Deserialize(reply.Content.ReadAsStringAsync().Result) is OSDMap map
                    && map["_embedded"] is OSDMap embedded)
                {
                    List<InventoryItem> items = !createLink 
                        ? parseItemsFromResponse((OSDMap)embedded["items"]) 
                        : parseLinksFromResponse((OSDMap)embedded["links"]);
                    callback(true, items.First());
                }
                else
                {
                    Logger.Log("Could not create inventory: " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback(false, null);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        public async Task SlamFolder(UUID folderUuid, OSD newInventory, Action<bool> callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                UUID tid = new UUID();
                string url = $"{cap}/category/{folderUuid}/links?tid={tid}";
                var content = new ByteArrayContent(OSDParser.SerializeLLSDXmlBytes(newInventory));
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/llsd+xml");
                var req = httpClient.PutAsync(url, content);
                var reply = await req;
                if (reply.IsSuccessStatusCode)
                {
                    callback?.Invoke(true);
                }
                else
                {
                    Logger.Log("Could not slam folder: " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        public async Task RemoveCategory(UUID categoryUuid, Action<bool> callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                string url = $"{cap}/category/{categoryUuid}";
                var op = httpClient.DeleteAsync(url);
                var reply = await op;
                if (reply.IsSuccessStatusCode)
                {
                    callback?.Invoke(true);
                }
                else
                {
                    Logger.Log("Could not remove folder: " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        public async Task RemoveItem(UUID itemUuid, Action<bool> callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                string url = $"{cap}/item/{itemUuid}";
                var op = httpClient.DeleteAsync(url);
                var reply = await op;
                if (reply.IsSuccessStatusCode)
                {
                    callback?.Invoke(true);
                }
                else
                {
                    Logger.Log("Could not remove item: " + itemUuid + " " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        public async Task CopyLibraryCategory(UUID sourceUuid, UUID destUuid, bool copySubfolders, Action<bool> callback)
        {
            var cap = getLibraryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                UUID tid = new UUID();
                string url = $"{cap}/category/{sourceUuid}?tid={tid}";
                if (copySubfolders)
                    url += ",depth=0";

                HttpRequestMessage message = new HttpRequestMessage
                {
                    RequestUri = new Uri(url),
                    Method = new HttpMethod("COPY")
                };
                message.Headers.Add("Destination", destUuid.ToString());
                var req = httpClient.SendAsync(message);
                var reply = await req;
                if (reply.IsSuccessStatusCode)
                {
                    callback?.Invoke(true);
                }
                else
                {
                    Logger.Log("Could not copy library folder: " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        public async Task PurgeDescendents(UUID categoryUuid, Action<bool, UUID> callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                string url = $"{cap}/category/{categoryUuid}/children";
                var op = httpClient.DeleteAsync(url);
                var reply = await op;
                if (reply.IsSuccessStatusCode)
                {
                    callback?.Invoke(true, categoryUuid);
                }
                else
                {
                    Logger.Log("Could not purge descendents: " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback?.Invoke(false, categoryUuid);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        public async Task UpdateCategory(UUID categoryUuid, OSD updates, Action<bool> callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                string url = $"{cap}/category/{categoryUuid}";
                HttpRequestMessage message = new HttpRequestMessage
                {
                    RequestUri = new Uri(url),
                    Method = new HttpMethod("PATCH"),
                    Content = new ByteArrayContent(OSDParser.SerializeLLSDXmlBytes(updates))
                };
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/llsd+xml");
                var req = httpClient.SendAsync(message);
                var reply = await req;
                if (reply.IsSuccessStatusCode)
                {
                    callback?.Invoke(true);
                }
                else
                {
                    Logger.Log("Could not update folder: " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        public async Task UpdateItem(UUID itemUuid, OSD updates, Action<bool> callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                string url = $"{cap}/item/{itemUuid}";
                var message = new HttpRequestMessage
                {
                    RequestUri = new Uri(url),
                    Method = new HttpMethod("PATCH"),
                    Content = new ByteArrayContent(OSDParser.SerializeLLSDXmlBytes(updates))
                };
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/llsd+xml");
                var req = httpClient.SendAsync(message);
                var reply = await req;
                if (reply.IsSuccessStatusCode)
                {
                    callback?.Invoke(true);
                }
                else
                {
                    Logger.Log("Could not update item: " + reply.ReasonPhrase, Helpers.LogLevel.Warning);
                    callback?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
        }

        private List<InventoryItem> parseItemsFromResponse(OSDMap itemsOsd)
        {
            List<InventoryItem> ret = new List<InventoryItem>();

            foreach (KeyValuePair<string, OSD> o in itemsOsd)
            {
                var item = (OSDMap)o.Value;
                ret.Add(InventoryItem.FromOSD(item));
            }
            return ret;
        }

        private List<InventoryItem> parseLinksFromResponse(OSDMap linksOsd)
        {
            List<InventoryItem> ret = new List<InventoryItem>();

            foreach (KeyValuePair<string, OSD> o in linksOsd)
            {
                var link = (OSDMap)o.Value;
                InventoryType type = (InventoryType)link["inv_type"].AsInteger();
                if (type == InventoryType.Texture && (AssetType)link["type"].AsInteger() == AssetType.Object)
                {
                    type = InventoryType.Attachment;
                }
                InventoryItem item = InventoryManager.CreateInventoryItem(type, link["item_id"]);

                item.ParentUUID = link["parent_id"];
                item.Name = link["name"];
                item.Description = link["desc"];
                item.OwnerID = link["agent_id"];
                item.ParentUUID = link["parent_id"];
                item.AssetUUID = link["linked_id"];
                item.AssetType = AssetType.Link;
                item.CreationDate = Utils.UnixTimeToDateTime(link["created_at"]);

                item.CreatorID = link["agent_id"]; // hack
                item.LastOwnerID = link["agent_id"]; // hack
                item.Permissions = Permissions.NoPermissions;
                item.GroupOwned = false;
                item.GroupID = UUID.Zero;

                item.SalePrice = 0;
                item.SaleType = SaleType.Not;

                ret.Add(item);
            }

            return ret;
        }

        private Uri getInventoryCap()
        {
            Uri cap = null;
            if (Client.Network.CurrentSim.Caps != null)
            {
                cap = Client.Network.CurrentSim.Caps.CapabilityURI(INVENTORY_CAP_NAME);
            }
            return cap;
        }

        private Uri getLibraryCap()
        {
            Uri cap = null;
            if (Client.Network.CurrentSim.Caps != null)
            {
                cap = Client.Network.CurrentSim.Caps.CapabilityURI(LIBRARY_CAP_NAME);
            }
            return cap;
        }
    }
}
