/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2025, Sjofn, LLC
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Voice.Vivox;
using Microsoft.Extensions.Logging;
using OpenMetaverse;
using LibreMetaverse.Appearance;
using OpenMetaverse.Packets;
using TestClient.Commands.Appearance;

namespace TestClient
{
    public class TestClient : GridClient
    {
        public UUID GroupID = UUID.Zero;
        public Dictionary<UUID, GroupMember> GroupMembers;
        public Dictionary<string, Command> Commands = new Dictionary<string, Command>();
        public bool Running = true;
        public bool GroupCommands = false;
        public string MasterName = string.Empty;
        public UUID MasterKey = UUID.Zero;
        public bool AllowObjectMaster = false;
        public ClientManager ClientManager;
        public VoiceManager VoiceManager;
        // Shell-like inventory commands need to be aware of the 'current' inventory folder.
        public InventoryFolder CurrentDirectory = null;

        private readonly System.Timers.Timer updateTimer;
        private UUID GroupMembersRequestID;
        public Dictionary<UUID, Group> GroupsCache = null;
        private readonly ManualResetEvent GroupsEvent = new ManualResetEvent(false);
        private CloneCommand CloneManager;

        /// <summary>
        /// Current Outfit Folder manager for tracking and managing avatar appearance
        /// </summary>
        public CurrentOutfitFolder OutfitManager { get; }

        /// <summary>
        /// Initializes a new instance of the TestClient class using the specified client manager.
        /// </summary>
        /// <remarks>This constructor sets up event handlers, configures default settings, and initializes key components
        /// required for the TestClient to operate. It also starts the internal update timer and registers necessary network
        /// callbacks. The provided ClientManager must be properly initialized before being passed to this
        /// constructor.</remarks>
        /// <param name="manager">The ClientManager instance that manages client operations and state for this TestClient.</param>
        public TestClient(ClientManager manager)
        {
            ClientManager = manager;
            CloneManager = new CloneCommand(this);

            updateTimer = new System.Timers.Timer(500);
            updateTimer.Elapsed += updateTimer_Elapsed;

            RegisterAllCommands(Assembly.GetExecutingAssembly());

            Settings.LOG_LEVEL = LogLevel.Debug;
            Settings.LOG_RESENDS = false;
            Settings.STORE_LAND_PATCHES = true;
            Settings.ALWAYS_DECODE_OBJECTS = true;
            Settings.ALWAYS_REQUEST_OBJECTS = true;
            Settings.SEND_AGENT_UPDATES = true;
            Settings.USE_ASSET_CACHE = true;

            Network.RegisterCallback(PacketType.AgentDataUpdate, AgentDataUpdateHandler);
            Network.LoginProgress += LoginHandler;
            Objects.AvatarUpdate += Objects_AvatarUpdate;
            Objects.TerseObjectUpdate += Objects_TerseObjectUpdate;
            Network.SimChanged += Network_SimChanged;
            Self.IM += Self_IM;
            Groups.GroupMembersReply += GroupMembersHandler;
            Inventory.InventoryObjectOffered += Inventory_OnInventoryObjectReceived;            

            Network.RegisterCallback(PacketType.AlertMessage, AlertMessageHandler);

            VoiceManager = new VoiceManager(this);

            // Initialize Current Outfit Folder manager
            OutfitManager = new CurrentOutfitFolder(this);

            updateTimer.Start();
        }

        void Objects_TerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
        {
            if (e.Prim.LocalID == Self.LocalID)
            {
                SetDefaultCamera();
            }
        }

        void Objects_AvatarUpdate(object sender, AvatarUpdateEventArgs e)
        {
            if (e.Avatar.LocalID == Self.LocalID)
            {
                SetDefaultCamera();
            }
        }

        void Network_SimChanged(object sender, SimChangedEventArgs e)
        {
            Self.Movement.SetFOVVerticalAngle(Utils.TWO_PI - 0.05f);
        }

        public void SetDefaultCamera()
        {
            // SetCamera 5m behind the avatar
            Self.Movement.Camera.LookAt(
                Self.SimPosition + new Vector3(-5, 0, 0) * Self.Movement.BodyRotation,
                Self.SimPosition
            );
        }


        void Self_IM(object sender, InstantMessageEventArgs e)
        {
            bool groupIM = e.IM.GroupIM && GroupMembers != null && GroupMembers.ContainsKey(e.IM.FromAgentID);

            if (e.IM.FromAgentID == MasterKey || (GroupCommands && groupIM))
            {
                // Received an IM from someone that is authenticated
                Console.WriteLine("<{0} ({1})> {2}: {3} (@{4}:{5})", e.IM.GroupIM ? "GroupIM" : "IM", e.IM.Dialog, e.IM.FromAgentName, e.IM.Message, 
                    e.IM.RegionID, e.IM.Position);

                if (e.IM.Dialog == InstantMessageDialog.RequestTeleport)
                {
                    Console.WriteLine("Accepting teleport lure.");
                    Self.TeleportLureRespond(e.IM.FromAgentID, e.IM.IMSessionID, true);
                }
                else if (
                    e.IM.Dialog == InstantMessageDialog.MessageFromAgent ||
                    e.IM.Dialog == InstantMessageDialog.MessageFromObject)
                {
                    ClientManager.Instance.DoCommandAll(e.IM.Message, e.IM.FromAgentID);
                }
            }
            else
            {
                // Received an IM from someone that is not the bot's master, ignore
                Console.WriteLine("<{0} ({1})> {2} (not master): {3} (@{4}:{5})", e.IM.GroupIM ? "GroupIM" : "IM", e.IM.Dialog, e.IM.FromAgentName, e.IM.Message,
                    e.IM.RegionID, e.IM.Position);
            }
        }

        /// <summary>
        /// Initialize everything that needs to be initialized once we're logged in.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void LoginHandler(object sender, LoginProgressEventArgs e)
        {
            if (e.Status == LoginStatus.Success)
            {
                // Start in the inventory root folder.
                CurrentDirectory = Inventory.Store.RootFolder;
            }
        }

        public void RegisterAllCommands(Assembly assembly)
        {
            foreach (Type t in assembly.GetTypes())
            {
                try
                {
                    if (t.IsSubclassOf(typeof(Command)))
                    {
                        ConstructorInfo info = t.GetConstructor(new[] { typeof(TestClient) });
                        Command command = (Command)info.Invoke(new object[] { this });
                        RegisterCommand(command);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        public void RegisterCommand(Command command)
        {
            command.Client = this;
            if (!Commands.ContainsKey(command.Name.ToLower()))
            {
                Commands.Add(command.Name.ToLower(), command);
            }
        }

        public void ReloadGroupsCache()
        {
            // Keep synchronous wrapper for compatibility by calling the async implementation
            ReloadGroupsCacheAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously reload the groups cache with a 10-second timeout.
        /// Use this method when you can await instead of blocking the calling thread.
        /// </summary>
        public async Task ReloadGroupsCacheAsync()
        {
            var tcs = new TaskCompletionSource<Dictionary<UUID, Group>>();

            EventHandler<CurrentGroupsEventArgs> handler = null;
            handler = (sender, e) =>
            {
                // TrySetResult in case of multiple firings
                tcs.TrySetResult(e.Groups);
            };

            try
            {
                Groups.CurrentGroups += handler;
                Groups.RequestCurrentGroups();

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);

                Groups.CurrentGroups -= handler;

                if (completed == tcs.Task)
                {
                    var groups = await tcs.Task.ConfigureAwait(false);

                    if (GroupsCache == null)
                    {
                        GroupsCache = groups;
                    }
                    else
                    {
                        lock (GroupsCache)
                        {
                            GroupsCache = groups;
                        }
                    }
                }
            }
            finally
            {
                // Ensure handler is removed in case of exceptions
                Groups.CurrentGroups -= handler;
            }
        }

        /// <summary>
        /// Lookup a group's UUID by name. Prefer the async version when possible.
        /// </summary>
        public UUID GroupName2UUID(string groupName)
        {
            // Maintain existing synchronous API by blocking on the async implementation
            return GroupName2UUIDAsync(groupName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously lookup a group's UUID by name. This will request the groups cache
        /// if it is empty and wait up to 10 seconds for a reply.
        /// </summary>
        public async Task<UUID> GroupName2UUIDAsync(string groupName)
        {
            UUID tryUUID;
            if (UUID.TryParse(groupName, out tryUUID))
                return tryUUID;

            if (GroupsCache == null)
            {
                await ReloadGroupsCacheAsync().ConfigureAwait(false);
                if (GroupsCache == null)
                    return UUID.Zero;
            }

            // Copy reference for thread-safety with minimal locking
            Dictionary<UUID, Group> snapshot;
            lock (GroupsCache)
            {
                snapshot = new Dictionary<UUID, Group>(GroupsCache);
            }

            foreach (Group currentGroup in snapshot.Values)
            {
                if (string.Equals(currentGroup.Name, groupName, StringComparison.CurrentCultureIgnoreCase))
                    return currentGroup.ID;
            }

            return UUID.Zero;
        }

        private void updateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (Command c in Commands.Values)
                if (c.Active)
                    c.Think();
        }

        private void AgentDataUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            AgentDataUpdatePacket p = (AgentDataUpdatePacket)e.Packet;
            if (p.AgentData.AgentID == e.Simulator.Client.Self.AgentID && p.AgentData.ActiveGroupID != UUID.Zero)
            {
                GroupID = p.AgentData.ActiveGroupID;
                
                GroupMembersRequestID = e.Simulator.Client.Groups.RequestGroupMembers(GroupID);
            }
        }

        private void GroupMembersHandler(object sender, GroupMembersReplyEventArgs e)
        {
            if (e.RequestID != GroupMembersRequestID) return;

            GroupMembers = e.Members;
        }

        private void AlertMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            
            AlertMessagePacket message = (AlertMessagePacket)packet;

            Logger.Info("[AlertMessage] " + Utils.BytesToString(message.AlertData.Message), this);
        }
       
        private void Inventory_OnInventoryObjectReceived(object sender, InventoryObjectOfferedEventArgs e)
        {
            if (MasterKey != UUID.Zero)
            {
                if (e.Offer.FromAgentID != MasterKey)
                    return;
            }
            else if (GroupMembers != null && !GroupMembers.ContainsKey(e.Offer.FromAgentID))
            {
                return;
            }

            e.Accept = true;
            return;
        }

        /// <summary>
        /// Dispose pattern override to cleanup OutfitManager
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                OutfitManager?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}

