/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2024, Sjofn LLC.
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Http;
using System.Net.Http;

namespace OpenMetaverse
{
    /// <summary>
    /// Capabilities is the name of the bi-directional HTTP REST protocol
    /// used to communicate non real-time transactions such as teleporting or
    /// group messaging
    /// </summary>
    public partial class Caps
    {
        /// <summary>
        /// Triggered when an event is received via the EventQueueGet 
        /// capability
        /// </summary>
        /// <param name="capsKey">Event name</param>
        /// <param name="message">Decoded event data</param>
        /// <param name="simulator">The simulator that generated the event</param>
        //public delegate void EventQueueCallback(string message, StructuredData.OSD body, Simulator simulator);

        public delegate void EventQueueCallback(string capsKey, IMessage message, Simulator simulator);

        /// <summary>Reference to the simulator this system is connected to</summary>
        public Simulator Simulator;

        internal Uri _SeedCapsURI;
        internal Dictionary<string, Uri> _Caps = new Dictionary<string, Uri>();

        private CancellationTokenSource _HttpCts = new CancellationTokenSource();
        private EventQueueClient _EventQueueClient = null;

        /// <summary>Capabilities URI this system was initialized with</summary>
        public Uri SeedCapsURI => _SeedCapsURI;

        /// <summary>Whether the capabilities event queue is connected and
        /// listening for incoming events</summary>
        public bool IsEventQueueRunning => _EventQueueClient != null && _EventQueueClient.Running;

        public EventQueueClient EventQueue => _EventQueueClient;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="seedcaps"></param>
        internal Caps(Simulator simulator, Uri seedcaps)
        {
            Simulator = simulator;
            _SeedCapsURI = seedcaps;

            MakeSeedRequest();
        }

        public void Disconnect(bool immediate)
        {
            Logger.Log($"Caps system for {Simulator} is {(immediate ? "aborting" : "disconnecting")}", 
                Helpers.LogLevel.Info, Simulator.Client);

            _HttpCts.Cancel();

            _EventQueueClient?.Stop(immediate);
        }

        /// <summary>
        /// Request the URI of a named capability
        /// </summary>
        /// <param name="capability">Name of the capability to request</param>
        /// <returns>The URI of the requested capability, or null if not found</returns>
        public Uri CapabilityURI(string capability)
        {
            return _Caps.TryGetValue(capability, out var cap) ? cap : null;
        }

        /// <summary>
        /// Return the cap names.
        /// </summary>
        public Dictionary<string, Uri>.KeyCollection Capabilities()
        {
            return _Caps.Keys;
        }

        /// <summary>
        /// Create a new CapsClient for specified capability
        /// </summary>
        /// <param name="capability">Capability name</param>
        /// <returns>Newly created CapsClient or null of capability does not exist</returns>
        [Obsolete("CapsClient is obsolete. Use HttpCapsClient instead.")]
        public CapsClient CreateCapsClient(string capability)
        {
            return _Caps.TryGetValue(capability, out var uri) ? new CapsClient(uri, capability) : null;
        }

        /// <summary>
        /// Useful for debugging, but not particularly a good idea
        /// </summary>
        /// <param name="cap">Capability address</param>
        /// <returns>Name of capability if it exists</returns>
        public string CapabilityNameFromURI(Uri cap)
        {
            return _Caps.First(x => x.Value == cap).Key;
        }

        /// <summary>
        /// Request preferred URI for texture fetch capability
        /// </summary>
        /// <returns>URI of preferred capability or null, or null if not found</returns>
        public Uri GetTextureCapURI()
        {
            Uri cap;
            if (_Caps.TryGetValue("ViewerAsset", out cap)) { return cap; }
            return _Caps.TryGetValue("GetTexture", out cap) ? cap : null;
        }

        /// <summary>
        /// Request preferred URI for object mesh fetch capability
        /// </summary>
        /// <returns>URI of preferred capability or null, or null if not found</returns>
        public Uri GetMeshCapURI()
        {
            Uri cap;
            if (_Caps.TryGetValue("ViewerAsset", out cap)) { return cap; }
            if (_Caps.TryGetValue("GetMesh2", out cap)) { return cap; }
            return _Caps.TryGetValue("GetMesh", out cap) ? cap : null;
        }
        public static OSDArray AllCapabilities = new OSDArray
        {
            "AbuseCategories",
            "AcceptFriendship",
            "AcceptGroupInvite",
            "AgentPreferences",
            "AgentProfile",
            "AgentState",
            "AttachmentResources",
            "AvatarPickerSearch",
            "AvatarRenderInfo",
            "CharacterProperties",
            "ChatSessionRequest",
            "CopyInventoryFromNotecard",
            "CreateInventoryCategory",
            "DeclineFriendship",
            "DeclineGroupInvite",
            "DispatchRegionInfo",
            "DirectDelivery",
            "EnvironmentSettings",
            "EstateAccess",
            "EstateChangeInfo",
            "EventQueueGet",
            "ExtEnvironment",
            "FetchLib2",
            "FetchLibDescendents2",
            "FetchInventory2",
            "FetchInventoryDescendents2",
            "IncrementCOFVersion",
            "InterestList",
            "GetDisplayNames",
            "GetExperiences",
            "AgentExperiences",
            "FindExperienceByName",
            "GetExperienceInfo",
            "GetAdminExperiences",
            "GetCreatorExperiences",
            "ExperiencePreferences",
            "GroupExperiences",
            "UpdateExperience",
            "IsExperienceAdmin",
            "IsExperienceContributor",
            "RegionExperiences",
            "ExperienceQuery",
            "GetMesh",
            "GetMesh2",
            "GetMetadata",
            "GetObjectCost",
            "GetObjectPhysicsData",
            "GetTexture",
            "GroupAPIv1",
            "GroupMemberData",
            "GroupProposalBallot",
            "HomeLocation",
            "LandResources",
            "LSLSyntax",
            "MapLayer",
            "MapLayerGod",
            "MeshUploadFlag",
            "NavMeshGenerationStatus",
            "NewFileAgentInventory",
            "ObjectAnimation",
            "ObjectMedia",
            "ObjectMediaNavigate",
            "ObjectNavMeshProperties",
            "ParcelPropertiesUpdate",
            "ParcelVoiceInfoRequest",
            "ProductInfoRequest",
            "ProvisionVoiceAccountRequest",
            "ReadOfflineMsgs",
            "RegionObjects",
            "RemoteParcelRequest",
            "RenderMaterials",
            "RequestTextureDownload",
            "ResourceCostSelected",
            "RetrieveNavMeshSrc",
            "SearchStatRequest",
            "SearchStatTracking",
            "SendPostcard",
            "SendUserReport",
            "SendUserReportWithScreenshot",
            "ServerReleaseNotes",
            "SetDisplayName",
            "SimConsoleAsync",
            "SimulatorFeatures",
            "StartGroupProposal",
            "TerrainNavMeshProperties",
            "TextureStats",
            "UntrustedSimulatorMessage",
            "UpdateAgentInformation",
            "UpdateAgentLanguage",
            "UpdateAvatarAppearance",
            "UpdateGestureAgentInventory",
            "UpdateGestureTaskInventory",
            "UpdateNotecardAgentInventory",
            "UpdateNotecardTaskInventory",
            "UpdateNotificationPreferences",
            "UpdateScriptAgent",
            "UpdateScriptTask",
            "UpdateSettingsAgentInventory",
            "UpdateSettingsTaskInventory",
            "UploadAgentProfileImage",
            "UploadBakedTexture",
            "UserInfo",
            "ViewerAsset",
            "ViewerBenefits",
            "ViewerMetrics",
            "ViewerStartAuction",
            "ViewerStats",
            "VoiceSignalingRequest",
            // AIS3
            "InventoryAPIv3",
            "LibraryAPIv3"
        };
        private void MakeSeedRequest()
        {
            if (Simulator == null || !Simulator.Client.Network.Connected) { return; }

            Task loginReq = Simulator.Client.HttpCapsClient.PostRequestAsync(_SeedCapsURI, OSDFormat.Xml, Caps.AllCapabilities, 
                _HttpCts.Token, SeedRequestCompleteHandler);
        }

        private void SeedRequestCompleteHandler(HttpResponseMessage response, byte[] responseData, Exception error)
        {
            if (error != null)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Logger.Log("Seed capability returned a 404, capability system is aborting",
                        Helpers.LogLevel.Error);
                }
                else
                {
                    Logger.Log($"Seed capability returned {response.StatusCode}. Trying again.",
                        Helpers.LogLevel.Warning);
                    MakeSeedRequest();
                }
                return;
            }

            try
            {
                OSD result = OSDParser.Deserialize(responseData);
                if (result is OSDMap respMap)
                {
                    foreach (var cap in respMap.Keys)
                    {
                        _Caps[cap] = respMap[cap].AsUri();
                    }

                    if (_Caps.TryGetValue("EventQueueGet", out var eventQueueGetCap))
                    {
                        Logger.DebugLog($"Starting event queue for {Simulator}", Simulator.Client);

                        _EventQueueClient = new EventQueueClient(eventQueueGetCap, Simulator);
                        _EventQueueClient.OnConnected += EventQueueConnectedHandler;
                        _EventQueueClient.OnEvent += EventQueueEventHandler;
                        _EventQueueClient.Start();
                    }

                    OnCapabilitiesReceived(Simulator);
                }
            }
            catch (LitJson.JsonException)
            {
                Logger.Log("Invalid caps response; '" + System.Text.Encoding.UTF8.GetString(responseData) + "' for seed request.", Helpers.LogLevel.Warning, Simulator.Client);
            }
        }

        private void EventQueueConnectedHandler()
        {
            Simulator.Client.Network.RaiseConnectedEvent(Simulator);
        }

        /// <summary>
        /// Process any incoming events, check to see if we have a message created for the event, 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="body"></param>
        private void EventQueueEventHandler(string eventName, OSDMap body)
        {
            IMessage message = Messages.MessageUtils.DecodeEvent(eventName, body);
            if (message != null)
            {
                Simulator.Client.Network.CapsEvents.BeginRaiseEvent(eventName, message, Simulator);

                #region Stats Tracking
                if (Simulator.Client.Settings.TRACK_UTILIZATION)
                {
                    Simulator.Client.Stats.Update(eventName, OpenMetaverse.Stats.Type.Message, 0, body.ToString().Length);
                }
                #endregion
            }
            else
            {
                Logger.Log($"No Message handler exists for event {eventName}. Unable to decode. Will try Generic Handler next", 
                    Helpers.LogLevel.Warning);
                Logger.Log("Please report this information at https://github.com/cinderblocks/libremetaverse/issues: " + Environment.NewLine + body, 
                    Helpers.LogLevel.Debug);

                // try generic decoder next which takes a caps event and tries to match it to an existing packet
                if (body.Type == OSDType.Map)
                {
                    OSDMap map = body;
                    Packet packet = Packet.BuildPacket(eventName, map);
                    if (packet != null)
                    {
                        var incomingPacket = new NetworkManager.IncomingPacket
                        {
                            Simulator = Simulator,
                            Packet = packet
                        };

                        Logger.DebugLog($"Serializing {packet.Type} capability with generic handler", 
                            Simulator.Client);

                        Simulator.Client.Network.EnqueueIncoming(incomingPacket);
                    }
                    else
                    {
                        Logger.Log($"No Packet or Message handler exists for {eventName}", 
                            Helpers.LogLevel.Warning);
                    }
                }
            }
        }

        /// <summary>Raised whenever the capabilities have been received from a simulator</summary>
        public event EventHandler<CapabilitiesReceivedEventArgs> CapabilitiesReceived;

        /// <summary>
        /// Raises the CapabilitiesReceived event
        /// </summary>
        /// <param name="simulator">Simulator we received the capabilities from</param>
        private void OnCapabilitiesReceived(Simulator simulator)
        {
            CapabilitiesReceived?.Invoke(this, new CapabilitiesReceivedEventArgs(simulator));
        }
    }

    #region EventArgs

    public class CapabilitiesReceivedEventArgs : EventArgs
    {
        /// <summary>The simulator that received a capabilities</summary>
        public Simulator Simulator { get; }

        public CapabilitiesReceivedEventArgs(Simulator simulator)
        {
            Simulator = simulator;
        }
    }

    #endregion
}
