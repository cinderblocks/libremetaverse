using System;
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
            try
            {
                UUID tid = new UUID();
                string url = $"{cap}/category/{parentUuid}?tid={tid}";
                var content = new StringContent(newInventory.ToString());
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/llsd+xml");
                var req = httpClient.PostAsync(url, content);
                var reply = await req;
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }

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
            try
            {
                UUID tid = new UUID();
                string url = $"{cap}/category/{folderUuid}/links?tid={tid}";
                var content = new StringContent(newInventory.ToString());
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/llsd+xml");
                var req = httpClient.PutAsync(url, content);
                var reply = await req;
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }

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
            try
            {
                string url = $"{cap}/category/{categoryUuid}";
                var op = httpClient.DeleteAsync(url);
                var reply = await op;
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
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
            try
            {
                string url = $"{cap}/item/{itemUuid}";
                var op = httpClient.DeleteAsync(url);
                var reply = await op;
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
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
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            callback();
        }

        public async Task PurgeDescendents(UUID categoryUuid, Action<UUID> callback)
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
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            callback(categoryUuid);
        }

        public async Task UpdateCategory(UUID categoryUuid, OSD updates, Action callback)
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
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
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
            }
            catch (System.ArgumentException)
            {
                // supress "Only 'http' and 'https' schemes are allowed." https IS the scheme, but for 
                // whatever reason, it's throwing here.
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
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
