using System;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse
{
    class InventoryAISClient
    {
        readonly string INVENTORY_CAP_NAME = "InventoryAPIv3";
        readonly string LIBRARY_CAP_NAME   = "LibraryAPIv3";

        enum CommandType {
            COPYINVENTORY,
            SLAMFOLDER,
            REMOVECATEGORY,
            REMOVEITEM,
            PURGEDESCENDENTS,
            UPDATECATEGORY,
            UPDATEITEM,
            COPYLIBRARYCATEGORY
        }

        [NonSerialized]
        private GridClient Client;

        public InventoryAISClient(GridClient client)
        {
            Client = client;

        }

        public bool IsAvailable => (Client.Network.CurrentSim.Caps != null &&
                                    Client.Network.CurrentSim.Caps.CapabilityURI(INVENTORY_CAP_NAME) != null);

        public void CreateInventory(UUID parentUuid, OSD newInventory, Action callback)
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
            // Enqueue
        }

        public void SlamFolder(UUID folderUuid, OSD newInventory, Action callback)
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
            // Enqueue
        }

        public void RemoveCategory(UUID categoryUuid, Action callback)
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
            // Enqueue
        }

        public void RemoveItem(UUID itemUuid, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/item/{itemUuid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);
            // Enqueue
        }

        public void CopyLibraryCategory(UUID sourceUuid, UUID destUuid, bool copySubfolders, Action callback)
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
            // Enqueue
        }

        public void PurgeDescendents(UUID categoryUuid, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/category/{categoryUuid}/children";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);
            // Enqueue
        }

        public void UpdateCategory(UUID categoryUuid, OSD updates, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/category/{categoryUuid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);
            // Enqueue
        }

        public void UpdateItem(UUID itemUuid, OSD updates, Action callback)
        {
            var cap = getInventoryCap();
            if (cap == null)
            {
                Logger.Log("No AIS3 Capability!", Helpers.LogLevel.Warning, Client);
                return;
            }

            string url = $"{cap}/item/{itemUuid}";
            Logger.Log("url: " + url, Helpers.LogLevel.Debug, Client);
            // Enqueue
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
