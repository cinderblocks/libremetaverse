/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn, LLC.
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
using System.Linq;
using System.Threading;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Packets;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpenMetaverse
{
    #region Enums

    /// <summary>
    /// Map layer request type
    /// </summary>
    public enum GridLayerType : uint
    {
        /// <summary>Objects and terrain are shown</summary>
        Objects = 0,
        /// <summary>Only the terrain is shown, no objects</summary>
        Terrain = 1,
        /// <summary>Overlay showing land for sale and for auction</summary>
        LandForSale = 2
    }

    /// <summary>
    /// Type of grid item, such as telehub, event, popular location, etc.
    /// </summary>
    public enum GridItemType : uint
    {
        /// <summary>Telehub</summary>
        Telehub = 1,
        /// <summary>PG rated event</summary>
        PgEvent = 2,
        /// <summary>Mature rated event</summary>
        MatureEvent = 3,
        /// <summary>Popular location</summary>
        Popular = 4,
        /// <summary>Locations of avatar groups in a region</summary>
        AgentLocations = 6,
        /// <summary>Land for sale</summary>
        LandForSale = 7,
        /// <summary>Classified ad</summary>
        Classified = 8,
        /// <summary>Adult rated event</summary>
        AdultEvent = 9,
        /// <summary>Adult land for sale</summary>
        AdultLandForSale = 10
    }

    #endregion Enums

    #region Structs

    /// <summary>
	/// Information about a region on the grid map
	/// </summary>
	public struct GridRegion
	{
        /// <summary>Sim X position on World Map</summary>
		public int X;
        /// <summary>Sim Y position on World Map</summary>
		public int Y;
        /// <summary>Sim Name (NOTE: In lowercase!)</summary>
		public string Name;
        /// <summary>Access level</summary>
		public SimAccess Access;
        /// <summary>Appears to always be zero (None)</summary>
        public RegionFlags RegionFlags;
        /// <summary>Water Height</summary>
		public byte WaterHeight;
        /// <summary></summary>
		public byte Agents;
        /// <summary>UUID of the World Map image</summary>
		public UUID MapImageID;
        /// <summary>Unique identifier for this region, a combination of the X 
        /// and Y position</summary>
		public ulong RegionHandle;


        public override string ToString()
        {
            return $"{Name} ({X}/{Y}), Handle: {RegionHandle}, MapImage: {MapImageID}, Access: {Access}";
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return (obj is GridRegion region) && Equals(region);
        }

        private bool Equals(GridRegion region)
        {
            return this.X == region.X && this.Y == region.Y;
        }
	}

    /// <summary>
    /// Visual chunk of the grid map
    /// </summary>
    public struct GridLayer
    {
        public int Bottom;
        public int Left;
        public int Top;
        public int Right;
        public UUID ImageID;

        public bool ContainsRegion(int x, int y)
        {
            return x >= Left && x <= Right && y >= Bottom && y <= Top;
        }
    }

    #endregion Structs

    #region Map Item Classes

    /// <summary>
    /// Base class for Map Items
    /// </summary>
    public abstract class MapItem
    {
        /// <summary>The Global X position of the item</summary>
        public uint GlobalX;
        /// <summary>The Global Y position of the item</summary>
        public uint GlobalY;

        /// <summary>Get the Local X position of the item</summary>
        public uint LocalX => GlobalX % Simulator.DefaultRegionSizeX;

        /// <summary>Get the Local Y position of the item</summary>
        public uint LocalY => GlobalY % Simulator.DefaultRegionSizeY;

        /// <summary>Get the Handle of the region</summary>
        public ulong RegionHandle =>
            Utils.UIntsToLong((uint)(GlobalX - (GlobalX % Simulator.DefaultRegionSizeX)),
                (uint)(GlobalY - (GlobalY % Simulator.DefaultRegionSizeY)));
    }

    /// <summary>
    /// Represents an agent or group of agents location
    /// </summary>
    public class MapAgentLocation : MapItem
    {       
        public int AvatarCount;
        public string Identifier;
    }

    /// <summary>
    /// Represents a Telehub location
    /// </summary>
    public class MapTelehub : MapItem
    {        
    }

    /// <summary>
    /// Represents a non-adult parcel of land for sale
    /// </summary>
    public class MapLandForSale : MapItem
    {        
        public int Size;
        public int Price;
        public string Name;
        public UUID ID;        
    }

    /// <summary>
    /// Represents an Adult parcel of land for sale
    /// </summary>
    public class MapAdultLandForSale : MapItem
    {     
        public int Size;
        public int Price;
        public string Name;
        public UUID ID;
    }

    /// <summary>
    /// Represents a PG Event
    /// </summary>
    public class MapPGEvent : MapItem
    {
        public DirectoryManager.EventFlags Flags; // Extra
        public DirectoryManager.EventCategories Category; // Extra2
        public string Description;
    }

    /// <summary>
    /// Represents a Mature event
    /// </summary>
    public class MapMatureEvent : MapItem
    {
        public DirectoryManager.EventFlags Flags; // Extra
        public DirectoryManager.EventCategories Category; // Extra2
        public string Description;
    }

    /// <summary>
    /// Represents an Adult event
    /// </summary>
    public class MapAdultEvent : MapItem
    {
        public DirectoryManager.EventFlags Flags; // Extra
        public DirectoryManager.EventCategories Category; // Extra2
        public string Description;
    }
    #endregion Grid Item Classes

    /// <summary>
	/// Manages grid-wide tasks such as the world map
	/// </summary>
	public class GridManager
    {
        #region Delegates

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<CoarseLocationUpdateEventArgs> m_CoarseLocationUpdate;

        /// <summary>Raises the CoarseLocationUpdate event</summary>
        /// <param name="e">A CoarseLocationUpdateEventArgs object containing the
        /// data sent by simulator</param>
        protected virtual void OnCoarseLocationUpdate(CoarseLocationUpdateEventArgs e)
        {
            EventHandler<CoarseLocationUpdateEventArgs> handler = m_CoarseLocationUpdate;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_CoarseLocationUpdateLock = new object();

        /// <summary>Raised when the simulator sends a <see cref="CoarseLocationUpdatePacket"/> 
        /// containing the location of agents in the simulator</summary>
        public event EventHandler<CoarseLocationUpdateEventArgs> CoarseLocationUpdate
        {
            add { lock (m_CoarseLocationUpdateLock) { m_CoarseLocationUpdate += value; } }
            remove { lock (m_CoarseLocationUpdateLock) { m_CoarseLocationUpdate -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GridRegionEventArgs> m_GridRegion;

        /// <summary>Raises the GridRegion event</summary>
        /// <param name="e">A GridRegionEventArgs object containing the
        /// data sent by simulator</param>
        protected virtual void OnGridRegion(GridRegionEventArgs e)
        {
            EventHandler<GridRegionEventArgs> handler = m_GridRegion;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GridRegionLock = new object();

        /// <summary>Raised when the simulator sends a Region Data in response to 
        /// a Map request</summary>
        public event EventHandler<GridRegionEventArgs> GridRegion
        {
            add { lock (m_GridRegionLock) { m_GridRegion += value; } }
            remove { lock (m_GridRegionLock) { m_GridRegion -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GridLayerEventArgs> m_GridLayer;

        /// <summary>Raises the GridLayer event</summary>
        /// <param name="e">A GridLayerEventArgs object containing the
        /// data sent by simulator</param>
        protected virtual void OnGridLayer(GridLayerEventArgs e)
        {
            EventHandler<GridLayerEventArgs> handler = m_GridLayer;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GridLayerLock = new object();

        /// <summary>Raised when the simulator sends GridLayer object containing
        /// a map tile coordinates and texture information</summary>
        public event EventHandler<GridLayerEventArgs> GridLayer
        {
            add { lock (m_GridLayerLock) { m_GridLayer += value; } }
            remove { lock (m_GridLayerLock) { m_GridLayer -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GridItemsEventArgs> m_GridItems;

        /// <summary>Raises the GridItems event</summary>
        /// <param name="e">A GridItemEventArgs object containing the
        /// data sent by simulator</param>
        protected virtual void OnGridItems(GridItemsEventArgs e)
        {
            EventHandler<GridItemsEventArgs> handler = m_GridItems;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GridItemsLock = new object();

        /// <summary>Raised when the simulator sends GridItems object containing
        /// details on events, land sales at a specific location</summary>
        public event EventHandler<GridItemsEventArgs> GridItems
        {
            add { lock (m_GridItemsLock) { m_GridItems += value; } }
            remove { lock (m_GridItemsLock) { m_GridItems -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<RegionHandleReplyEventArgs> m_RegionHandleReply;

        /// <summary>Raises the RegionHandleReply event</summary>
        /// <param name="e">A RegionHandleReplyEventArgs object containing the
        /// data sent by simulator</param>
        protected virtual void OnRegionHandleReply(RegionHandleReplyEventArgs e)
        {
            EventHandler<RegionHandleReplyEventArgs> handler = m_RegionHandleReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RegionHandleReplyLock = new object();

        /// <summary>Raised in response to a Region lookup</summary>
        public event EventHandler<RegionHandleReplyEventArgs> RegionHandleReply
        {
            add { lock (m_RegionHandleReplyLock) { m_RegionHandleReply += value; } }
            remove { lock (m_RegionHandleReplyLock) { m_RegionHandleReply -= value; } }
        }

        #endregion Delegates

        /// <summary>Unknown</summary>
        public float SunPhase { get; private set; }

        /// <summary>Current direction of the sun</summary>
        public Vector3 SunDirection { get; private set; }

        /// <summary>Current angular velocity of the sun</summary>
        public Vector3 SunAngVelocity { get; private set; }

        /// <summary>Microseconds since the start of SL 4-hour day</summary>
        public ulong TimeOfDay { get; private set; }

        /// <summary>A dictionary of all the regions, indexed by region name</summary>
        internal Dictionary<string, GridRegion> Regions = new Dictionary<string, GridRegion>();
        /// <summary>A dictionary of all the regions, indexed by region handle</summary>
        internal Dictionary<ulong, GridRegion> RegionsByHandle = new Dictionary<ulong, GridRegion>();
        /// <summary>A dictionary of regions by region handle</summary>
        internal Dictionary<UUID, ulong> RegionsByUUID = new Dictionary<UUID, ulong>();

        private readonly GridClient Client;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="client">Instance of GridClient object to associate with this GridManager instance</param>
		public GridManager(GridClient client)
		{
			Client = client;

            //Client.Network.RegisterCallback(PacketType.MapLayerReply, MapLayerReplyHandler);
            Client.Network.RegisterCallback(PacketType.MapBlockReply, MapBlockReplyHandler);
            Client.Network.RegisterCallback(PacketType.MapItemReply, MapItemReplyHandler);
            Client.Network.RegisterCallback(PacketType.SimulatorViewerTimeMessage, SimulatorViewerTimeMessageHandler);
            Client.Network.RegisterCallback(PacketType.CoarseLocationUpdate, CoarseLocationHandler, false);
            Client.Network.RegisterCallback(PacketType.RegionIDAndHandleReply, RegionHandleReplyHandler);
		}

        /// <summary>
        /// Request a map layer from simulator capability
        /// </summary>
        /// <param name="layer">Requested <see cref="GridLayerType"/></param>
        public void RequestMapLayer(GridLayerType layer)
        {
            Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("MapLayer");
            if (cap == null) 
                return;
            
            OSDMap payload = new OSDMap {["Flags"] = OSD.FromInteger((int) layer)};
            Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload, 
                CancellationToken.None, MapLayerResponseHandler);
        }

        /// <summary>
        /// Request a map layer through the simulator
        /// </summary>
        /// <param name="regionName">The name of the region</param>
        /// <param name="layer">Requested <see cref="GridLayerType"/></param>
        public void RequestMapRegion(string regionName, GridLayerType layer)
        {
            MapNameRequestPacket request = new MapNameRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Flags = (uint)layer,
                    EstateID = 0, // Filled in on the sim
                    Godlike = false // Filled in on the sim
                },
                NameData =
                {
                    Name = Utils.StringToBytes(regionName.ToLowerInvariant())
                }
            };

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Return map blocks for a given segment of the world map
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <param name="returnNonExistent"></param>
        public void RequestMapBlocks(GridLayerType layer, ushort minX, ushort minY, ushort maxX, ushort maxY, 
            bool returnNonExistent)
        {
            MapBlockRequestPacket request = new MapBlockRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Flags = (uint)layer
                }
            };

            request.AgentData.Flags |= (uint)(returnNonExistent ? 0x10000 : 0);
            request.AgentData.EstateID = 0; // Filled in at the simulator
            request.AgentData.Godlike = false; // Filled in at the simulator

            request.PositionData.MinX = minX;
            request.PositionData.MinY = minY;
            request.PositionData.MaxX = maxX;
            request.PositionData.MaxY = maxY;

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Returns a list of map items
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="item"></param>
        /// <param name="layer"></param>
        /// <param name="timeout"></param>
        /// <returns>List of Map items</returns>
        public List<MapItem> MapItems(ulong regionHandle, GridItemType item, GridLayerType layer, TimeSpan timeout)
        {
            List<MapItem> itemList = null;
            AutoResetEvent itemsEvent = new AutoResetEvent(false);

            void Callback(object sender, GridItemsEventArgs e)
            {
                if (e.Type != GridItemType.AgentLocations) 
                    return;
                
                itemList = e.Items;
                itemsEvent.Set();
            }

            GridItems += Callback;

            RequestMapItems(regionHandle, item, layer);
            itemsEvent.WaitOne(timeout, false);

            GridItems -= Callback;

            return itemList;
        }

        /// <summary>
        /// Request <see cref="GridItemType"/> for a given region
        /// </summary>
        /// <param name="regionHandle">Requested region handle</param>
        /// <param name="item"><see cref="GridItemType"/> being requested</param>
        /// <param name="layer"><see cref="GridLayerType"/> being requested</param>
        public void RequestMapItems(ulong regionHandle, GridItemType item, GridLayerType layer)
        {
            MapItemRequestPacket request = new MapItemRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Flags = (uint)layer,
                    Godlike = false, // Filled in on the sim
                    EstateID = 0 // Filled in on the sim
                },
                RequestData =
                {
                    ItemType = (uint)item,
                    RegionHandle = regionHandle
                }
            };

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Request data for all mainland (Linden managed) simulators
        /// </summary>
        public void RequestMainlandSims(GridLayerType layer)
        {
            RequestMapBlocks(layer, 0, 0, 65535, 65535, false);
        }

        /// <summary>
        /// Request the region handle for the specified region UUID
        /// </summary>
        /// <param name="regionID">UUID of the region to look up</param>
        public void RequestRegionHandle(UUID regionID)
        {
            ulong handle = 0;
            var found = false;
            lock (RegionsByUUID)
            {
                found = RegionsByUUID.TryGetValue(regionID, out handle);
            }

            if (found)
            {
                if (m_RegionHandleReply != null)
                {
                    OnRegionHandleReply(new RegionHandleReplyEventArgs(regionID, handle));
                }

                return;
            }

            RegionHandleRequestPacket request = new RegionHandleRequestPacket
            {
                RequestBlock = new RegionHandleRequestPacket.RequestBlockBlock
                {
                    RegionID = regionID
                }
            };
            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Retrieves <see cref="GridRegion"/> information using the region handle
        /// </summary>
        /// <remarks>This function will block until it can find the region or gives up</remarks>
        /// <param name="handle">Region Handle of requested <see cref="GridRegion"/></param>
        /// <param name="layer"><see cref="GridLayerType"/> for the
        /// <see cref="GridRegion"/> being requested</param>
        /// <param name="region">Output for the fetched <see cref="GridRegion"/>,
        /// or empty struct if failure</param>
        /// <returns>True if the <see cref="GridRegion"/> was fetched, otherwise false</returns>
        public bool GetGridRegion(ulong handle, GridLayerType layer, out GridRegion region)
        {
            // Check if cached
            if (RegionsByHandle.TryGetValue(handle, out region))
            {
                return true;
            }

            Utils.LongToUInts(handle, out var globalX, out var globalY);
            const uint regionWidthUnits = 256;
            ushort gridX = (ushort)(globalX / regionWidthUnits);
            ushort gridY = (ushort)(globalY / regionWidthUnits);

            // Ask the server for the name of the region anchored at the specified grid position.
            AutoResetEvent regionEvent = new AutoResetEvent(false);

            GridRegion foundRegion = default(GridRegion);
            bool found = false;

            void RegionCallback(object sender, GridRegionEventArgs e)
            {
                // See note in HandleCallback, above.
                if (e.Region.RegionHandle != handle) 
                    return;
                
                found = true;
                foundRegion = e.Region;
                regionEvent.Set();
            }

            GridRegion += RegionCallback;
            RequestMapBlocks(layer, gridX, gridY, gridX, gridY, true);
            regionEvent.WaitOne(Client.Settings.MAP_REQUEST_TIMEOUT, false);
            GridRegion -= RegionCallback;
            region = foundRegion;

            if (!found)
            {
                Logger.Log($"Could not find region at region handle {handle}", Helpers.LogLevel.Warning, Client);
            }

            return found;
        }

        /// <summary>
        /// Retrieves <see cref="GridRegion"/> information using the region name
        /// </summary>
        /// <remarks>This function will block until it can find the region or gives up</remarks>
        /// <param name="name">Name of requested <see cref="GridRegion"/></param>
        /// <param name="layer"><see cref="GridLayerType"/> for the
        /// <see cref="GridRegion"/> being requested</param>
        /// <param name="region">Output for the fetched <see cref="GridRegion"/>,
        /// or empty struct if failure</param>
        /// <returns>True if the <see cref="GridRegion"/> was fetched, otherwise false</returns>
        public bool GetGridRegion(string name, GridLayerType layer, out GridRegion region)
        {
            if (string.IsNullOrEmpty(name))
            {
                Logger.Log("GetGridRegion called with a null or empty region name", Helpers.LogLevel.Error, Client);
                region = new GridRegion();
                return false;
            }

            var key = name.ToLowerInvariant();
            if (Regions.TryGetValue(key, out region))
            {
                return true;
            }

            AutoResetEvent regionEvent = new AutoResetEvent(false);

            void Callback(object sender, GridRegionEventArgs e)
            {
                if (string.Equals(e.Region.Name, name, StringComparison.InvariantCultureIgnoreCase))
                {
                    regionEvent.Set();
                }
            }

            GridRegion += Callback;

            RequestMapRegion(name, layer);
            regionEvent.WaitOne(Client.Settings.MAP_REQUEST_TIMEOUT, false);

            GridRegion -= Callback;

            if (Regions.TryGetValue(key, out region))
                return true;
            
            Logger.Log($"Could not find region named {name}", Helpers.LogLevel.Warning, Client);
            region = new GridRegion();
            return false;
            
        }
        
        protected void MapLayerResponseHandler(HttpResponseMessage response, byte[] responseData, Exception error)
        {
            if (error != null)
            {
                Logger.Log($"MapLayerResponseHandler error: {error.Message}", Helpers.LogLevel.Error, Client, error);
                return;
            }

            OSD result = OSDParser.Deserialize(responseData);
            OSDMap body = (OSDMap)result;
            OSDArray layerData = (OSDArray)body["LayerData"];

            if (m_GridLayer != null)
            {
                foreach (var data in layerData)
                {
                    OSDMap thisLayerData = (OSDMap)data;

                    GridLayer layer;
                    layer.Bottom = thisLayerData["Bottom"].AsInteger();
                    layer.Left = thisLayerData["Left"].AsInteger();
                    layer.Top = thisLayerData["Top"].AsInteger();
                    layer.Right = thisLayerData["Right"].AsInteger();
                    layer.ImageID = thisLayerData["ImageID"].AsUUID();

                    OnGridLayer(new GridLayerEventArgs(layer));
                }
            }

            if (body.ContainsKey("MapBlocks"))
            {
                // TODO: At one point this will become activated
                Logger.Log("Got MapBlocks through CAPS, please finish this function!", Helpers.LogLevel.Error, Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void MapBlockReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            MapBlockReplyPacket map = (MapBlockReplyPacket)e.Packet;

            foreach (MapBlockReplyPacket.DataBlock block in map.Data)
            {
                if (block.X == 0 && block.Y == 0) 
                    continue;
                
                GridRegion region;

                region.X = block.X;
                region.Y = block.Y;
                region.Name = Utils.BytesToString(block.Name);
                // RegionFlags seems to always be zero here?
                region.RegionFlags = (RegionFlags)block.RegionFlags;
                region.WaterHeight = block.WaterHeight;
                region.Agents = block.Agents;
                region.Access = (SimAccess)block.Access;
                region.MapImageID = block.MapImageID;
                region.RegionHandle = Utils.UIntsToLong((uint)(region.X * 256), (uint)(region.Y * 256));

                lock (Regions)
                {
                    Regions[region.Name.ToLowerInvariant()] = region;
                    RegionsByHandle[region.RegionHandle] = region;
                }

                if (m_GridRegion != null)
                {
                    OnGridRegion(new GridRegionEventArgs(region));
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void MapItemReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GridItems == null) 
                return;
            
            MapItemReplyPacket reply = (MapItemReplyPacket)e.Packet;
            GridItemType type = (GridItemType)reply.RequestData.ItemType;
            List<MapItem> items = new List<MapItem>();

            foreach (var data in reply.Data)
            {
                var name = Utils.BytesToString(data.Name);

                switch (type)
                {
                    case GridItemType.AgentLocations:
                        MapAgentLocation location = new MapAgentLocation
                        {
                            GlobalX = data.X,
                            GlobalY = data.Y,
                            Identifier = name,
                            AvatarCount = data.Extra
                        };
                        items.Add(location);
                        break;
                    case GridItemType.Classified:
                        //FIXME:
                        Logger.Log("FIXME", Helpers.LogLevel.Error, Client);
                        break;
                    case GridItemType.LandForSale:
                        MapLandForSale landsale = new MapLandForSale
                        {
                            GlobalX = data.X,
                            GlobalY = data.Y,
                            ID = data.ID,
                            Name = name,
                            Size = data.Extra,
                            Price = data.Extra2
                        };
                        items.Add(landsale);
                        break;
                    case GridItemType.MatureEvent:
                        MapMatureEvent matureEvent = new MapMatureEvent
                        {
                            GlobalX = data.X,
                            GlobalY = data.Y,
                            Description = name,
                            Flags = (DirectoryManager.EventFlags)data.Extra2
                        };
                        items.Add(matureEvent);
                        break;
                    case GridItemType.PgEvent:
                        MapPGEvent PGEvent = new MapPGEvent
                        {
                            GlobalX = data.X,
                            GlobalY = data.Y,
                            Description = name,
                            Flags = (DirectoryManager.EventFlags)data.Extra2
                        };
                        items.Add(PGEvent);
                        break;
                    case GridItemType.Popular:
                        //FIXME:
                        Logger.Log("FIXME", Helpers.LogLevel.Error, Client);
                        break;
                    case GridItemType.Telehub:
                        MapTelehub teleHubItem = new MapTelehub
                        {
                            GlobalX = data.X,
                            GlobalY = data.Y
                        };
                        items.Add(teleHubItem);
                        break;
                    case GridItemType.AdultLandForSale:
                        MapAdultLandForSale adultLandsale = new MapAdultLandForSale
                        {
                            GlobalX = data.X,
                            GlobalY = data.Y,
                            ID = data.ID,
                            Name = name,
                            Size = data.Extra,
                            Price = data.Extra2
                        };
                        items.Add(adultLandsale);
                        break;
                    case GridItemType.AdultEvent:
                        MapAdultEvent adultEvent = new MapAdultEvent
                        {
                            GlobalX = data.X,
                            GlobalY = data.Y,
                            Description = Utils.BytesToString(data.Name),
                            Flags = (DirectoryManager.EventFlags)data.Extra2
                        };
                        items.Add(adultEvent);
                        break;
                    default:
                        Logger.Log($"Unknown map item type: {type}", Helpers.LogLevel.Warning, Client);
                        break;
                }
            }

            OnGridItems(new GridItemsEventArgs(type, items));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void SimulatorViewerTimeMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            SimulatorViewerTimeMessagePacket time = (SimulatorViewerTimeMessagePacket)e.Packet;
            
            SunPhase = time.TimeInfo.SunPhase;
            SunDirection = time.TimeInfo.SunDirection;
            SunAngVelocity = time.TimeInfo.SunAngVelocity;
            TimeOfDay = time.TimeInfo.UsecSinceStart;
            // TODO: Does anyone have a use for the time stuff?
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void CoarseLocationHandler(object sender, PacketReceivedEventArgs e)
        {
            CoarseLocationUpdatePacket coarse = (CoarseLocationUpdatePacket)e.Packet;

            // populate a dictionary from the packet, for local use
            var coarseEntries = new Dictionary<UUID, Vector3>(coarse.AgentData.Length);
            for (var i = 0; i < coarse.AgentData.Length; i++)
            {
                if (i < coarse.Location.Length)
                    coarseEntries[coarse.AgentData[i].AgentID] = new Vector3(coarse.Location[i].X, coarse.Location[i].Y, coarse.Location[i].Z * 4);

                // the friend we are tracking on radar
                if (i == coarse.Index.Prey)
                    e.Simulator.preyID = coarse.AgentData[i].AgentID;
            }

            // find stale entries (people who left the sim)
            var coarseKeys = new HashSet<UUID>(coarseEntries.Keys);
            var removedEntries = e.Simulator.avatarPositions.Keys
                .Where(avatarId => !coarseKeys.Contains(avatarId))
                .ToList();

            // entry not listed in the previous update
            var newEntries = new List<UUID>(coarse.AgentData.Length);
            
            // remove stale entries
            foreach (var trackedID in removedEntries)
            {
                e.Simulator.avatarPositions.TryRemove(trackedID, out var removed);
            }

            // add or update tracked info, and record who is new
            foreach (var entry in coarseEntries)
            {
                if (!e.Simulator.avatarPositions.TryGetValue(entry.Key, out _))
                {
                    newEntries.Add(entry.Key);
                }
                e.Simulator.avatarPositions[entry.Key] = entry.Value;
            }

            if (m_CoarseLocationUpdate != null)
            {
                ThreadPool.QueueUserWorkItem(o =>
                { OnCoarseLocationUpdate(new CoarseLocationUpdateEventArgs(e.Simulator, newEntries, removedEntries)); });
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void RegionHandleReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            RegionIDAndHandleReplyPacket reply = (RegionIDAndHandleReplyPacket)e.Packet;

            lock (RegionsByUUID)
            {
                RegionsByUUID[reply.ReplyBlock.RegionID] = reply.ReplyBlock.RegionHandle;
            }

            if (m_RegionHandleReply != null)
            {
                OnRegionHandleReply(new RegionHandleReplyEventArgs(reply.ReplyBlock.RegionID, reply.ReplyBlock.RegionHandle));
            }
        }

    }
    #region EventArgs classes

    public class CoarseLocationUpdateEventArgs : EventArgs
    {
        public Simulator Simulator { get; }
        public ICollection<UUID> NewEntries { get; }
        public ICollection<UUID> RemovedEntries { get; }

        public CoarseLocationUpdateEventArgs(Simulator simulator, ICollection<UUID> newEntries, ICollection<UUID> removedEntries)
        {
            Simulator = simulator;
            NewEntries = newEntries;
            RemovedEntries = removedEntries;
        }
    }

    public class GridRegionEventArgs : EventArgs
    {
        public GridRegion Region { get; }

        public GridRegionEventArgs(GridRegion region)
        {
            Region = region;
        }
    }

    public class GridLayerEventArgs : EventArgs
    {
        public GridLayer Layer { get; }

        public GridLayerEventArgs(GridLayer layer)
        {
            Layer = layer;
        }
    }

    public class GridItemsEventArgs : EventArgs
    {
        public GridItemType Type { get; }
        public List<MapItem> Items { get; }

        public GridItemsEventArgs(GridItemType type, List<MapItem> items)
        {
            Type = type;
            Items = items;
        }
    }

    public class RegionHandleReplyEventArgs : EventArgs
    {
        public UUID RegionID { get; }
        public ulong RegionHandle { get; }

        public RegionHandleReplyEventArgs(UUID regionID, ulong regionHandle)
        {
            RegionID = regionID;
            RegionHandle = regionHandle;
        }
    }

    #endregion
}
