using System;
using System.Net.Http;
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
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Libremetaverse AIS Client");
        }

        public bool IsAvailable => (Client.Network.CurrentSim.Caps != null &&
                                    Client.Network.CurrentSim.Caps.CapabilityURI(INVENTORY_CAP_NAME) != null);

        public async Task CreateInventory(UUID parentUuid, OSD newInventory, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            UUID tid = new UUID();
            string url = $"{cap}/category/{parentUuid}?tid={tid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            var content = new StringContent(newInventory.ToString()); // Total guess for now!
            var req = httpClient.PostAsync(url, content);
            var reply = await req;

            callback();
        }

        public async Task SlamFolder(UUID folderUuid, OSD newInventory, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            UUID tid = new UUID();
            string url = $"{cap}/category/{folderUuid}/links?tid={tid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            var content = new StringContent(newInventory.ToString()); // Total guess for now!
            var req = httpClient.PutAsync(url, content);
            var reply = await req;

            callback();
        }

        public async Task RemoveCategory(UUID categoryUuid, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            UUID tid = new UUID();
            string url = $"{cap}/category/{categoryUuid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            var op = httpClient.DeleteAsync(url);
            var reply = await op;

            callback();
        }

        public async Task RemoveItem(UUID itemUuid, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/item/{itemUuid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            var op = httpClient.DeleteAsync(url);
            var reply = await op;

            callback();
        }

        public async Task CopyLibraryCategory(UUID sourceUuid, UUID destUuid, bool copySubfolders, Action callback)
        {
            var cap = getLibraryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            UUID tid = new UUID();
            string url = $"{cap}/category/{sourceUuid}?tid={tid}";
            if (copySubfolders)
                url += ",depth=0";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = new HttpMethod("COPY");
            var req = httpClient.SendAsync(message);
            var reply = await req;

            callback();
        }

        public async Task PurgeDescendents(UUID categoryUuid, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/category/{categoryUuid}/children";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            var op = httpClient.DeleteAsync(url);
            var reply = await op;

            callback();
        }

        public async Task UpdateCategory(UUID categoryUuid, OSD updates, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/category/{categoryUuid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = new HttpMethod("PATCH");
            var req = httpClient.SendAsync(message);
            var reply = await req;

            callback();
        }

        public async Task UpdateItem(UUID itemUuid, OSD updates, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/item/{itemUuid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);

            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = new HttpMethod("PATCH");
            var req = httpClient.SendAsync(message);
            var reply = await req;

            callback();
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
