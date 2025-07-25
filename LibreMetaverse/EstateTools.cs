/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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
using OpenMetaverse.Packets;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LibreMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    /// <summary>Describes tasks returned in LandStatReply</summary>
    public class EstateTask
    {
        public Vector3 Position;
        public float Score;
        public float MonoScore;
        public UUID TaskID;
        public uint TaskLocalID;
        public string TaskName;
        public string OwnerName;
    }

    /// <summary>
    /// Estate level administration and utilities
    /// </summary>
    public class EstateTools
    {
        private GridClient Client;

        /// <summary>Textures for each of the four terrain height levels</summary>
        public GroundTextureSettings GroundTextures;

        /// <summary>Upper/lower texture boundaries for each corner of the sim</summary>
        public GroundTextureHeightSettings GroundTextureLimits;

        /// <summary>
        /// Constructor for EstateTools class
        /// </summary>
        /// <param name="client"></param>
        public EstateTools(GridClient client)
        {
            GroundTextures = new GroundTextureSettings();
            GroundTextureLimits = new GroundTextureHeightSettings();

            Client = client;
            Client.Network.RegisterCallback(PacketType.LandStatReply, LandStatReplyHandler);
            Client.Network.RegisterCallback(PacketType.EstateOwnerMessage, EstateOwnerMessageHandler);
            Client.Network.RegisterCallback(PacketType.EstateCovenantReply, EstateCovenantReplyHandler);

            Client.Network.RegisterEventCallback("LandStatReply", new Caps.EventQueueCallback(LandStatCapsReplyHandler));
        }

        #region Enums
        public enum RegionMaturity
        {
            PG = 13,
            Mature = 21,
            Adult = 42
        }

        /// <summary>Used in the ReportType field of a LandStatRequest</summary>
        public enum LandStatReportType
        {
            TopScripts = 0,
            TopColliders = 1
        }

        /// <summary>Used by EstateOwnerMessage packets</summary>
        public enum EstateAccessDelta : uint
        {
            BanUser = 64,
            BanUserAllEstates = 66,
            UnbanUser = 128,
            UnbanUserAllEstates = 130,
            AddManager = 256,
            AddManagerAllEstates = 257,
            RemoveManager = 512,
            RemoveManagerAllEstates = 513,
            AddUserAsAllowed = 4,
            AddAllowedAllEstates = 6,
            RemoveUserAsAllowed = 8,
            RemoveUserAllowedAllEstates = 10,
            AddGroupAsAllowed = 16,
            AddGroupAllowedAllEstates = 18,
            RemoveGroupAsAllowed = 32,
            RemoveGroupAllowedAllEstates = 34
        }

        /// <summary>Used by EstateOwnerMessage packets</summary>
        public enum EstateAccessReplyDelta : uint
        {
            AllowedUsers = 17,
            AllowedGroups = 18,
            EstateBans = 20,
            EstateManagers = 24
        }

        /// <summary>
        /// 
        /// </summary>
        [Flags]
        public enum EstateReturnFlags : uint
        {
            /// <summary>No flags set</summary>
            None = 2,
            /// <summary>Only return targets scripted objects</summary>
            ReturnScripted = 6,
            /// <summary>Only return targets objects if on others land</summary>
            ReturnOnOthersLand = 3,
            /// <summary>Returns target's scripted objects and objects on other parcels</summary>
            ReturnScriptedAndOnOthers = 7
        }
        #endregion
        #region Structs
        /// <summary>Ground texture settings for each corner of the region</summary>
        // TODO: maybe move this class to the Simulator object and implement it there too        
        public struct GroundTextureSettings
        {
            public UUID Low;
            public UUID MidLow;
            public UUID MidHigh;
            public UUID High;
        }

        /// <summary>Used by GroundTextureHeightSettings</summary>
        public struct GroundTextureHeight
        {
            public float Low;
            public float High;
        }

        /// <summary>The high and low texture thresholds for each corner of the sim</summary>
        public struct GroundTextureHeightSettings
        {
            public GroundTextureHeight SW;
            public GroundTextureHeight NW;
            public GroundTextureHeight SE;
            public GroundTextureHeight NE;
        }
        #endregion

        #region Event delegates, Raise Events

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<TopCollidersReplyEventArgs> m_TopCollidersReply;

        /// <summary>Raises the TopCollidersReply event</summary>
        /// <param name="e">A TopCollidersReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnTopCollidersReply(TopCollidersReplyEventArgs e)
        {
            EventHandler<TopCollidersReplyEventArgs> handler = m_TopCollidersReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TopCollidersReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<TopCollidersReplyEventArgs> TopCollidersReply
        {
            add { lock (m_TopCollidersReply_Lock) { m_TopCollidersReply += value; } }
            remove { lock (m_TopCollidersReply_Lock) { m_TopCollidersReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<TopScriptsReplyEventArgs> m_TopScriptsReply;

        /// <summary>Raises the TopScriptsReply event</summary>
        /// <param name="e">A TopScriptsReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnTopScriptsReply(TopScriptsReplyEventArgs e)
        {
            EventHandler<TopScriptsReplyEventArgs> handler = m_TopScriptsReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TopScriptsReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<TopScriptsReplyEventArgs> TopScriptsReply
        {
            add { lock (m_TopScriptsReply_Lock) { m_TopScriptsReply += value; } }
            remove { lock (m_TopScriptsReply_Lock) { m_TopScriptsReply -= value; } }
        }


        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<EstateUsersReplyEventArgs> m_EstateUsersReply;

        /// <summary>Raises the EstateUsersReply event</summary>
        /// <param name="e">A EstateUsersReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnEstateUsersReply(EstateUsersReplyEventArgs e)
        {
            EventHandler<EstateUsersReplyEventArgs> handler = m_EstateUsersReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_EstateUsersReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<EstateUsersReplyEventArgs> EstateUsersReply
        {
            add { lock (m_EstateUsersReply_Lock) { m_EstateUsersReply += value; } }
            remove { lock (m_EstateUsersReply_Lock) { m_EstateUsersReply -= value; } }
        }


        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<EstateGroupsReplyEventArgs> m_EstateGroupsReply;

        /// <summary>Raises the EstateGroupsReply event</summary>
        /// <param name="e">A EstateGroupsReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnEstateGroupsReply(EstateGroupsReplyEventArgs e)
        {
            EventHandler<EstateGroupsReplyEventArgs> handler = m_EstateGroupsReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_EstateGroupsReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<EstateGroupsReplyEventArgs> EstateGroupsReply
        {
            add { lock (m_EstateGroupsReply_Lock) { m_EstateGroupsReply += value; } }
            remove { lock (m_EstateGroupsReply_Lock) { m_EstateGroupsReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<EstateManagersReplyEventArgs> m_EstateManagersReply;

        /// <summary>Raises the EstateManagersReply event</summary>
        /// <param name="e">A EstateManagersReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnEstateManagersReply(EstateManagersReplyEventArgs e)
        {
            EventHandler<EstateManagersReplyEventArgs> handler = m_EstateManagersReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_EstateManagersReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<EstateManagersReplyEventArgs> EstateManagersReply
        {
            add { lock (m_EstateManagersReply_Lock) { m_EstateManagersReply += value; } }
            remove { lock (m_EstateManagersReply_Lock) { m_EstateManagersReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<EstateBansReplyEventArgs> m_EstateBansReply;

        /// <summary>Raises the EstateBansReply event</summary>
        /// <param name="e">A EstateBansReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnEstateBansReply(EstateBansReplyEventArgs e)
        {
            EventHandler<EstateBansReplyEventArgs> handler = m_EstateBansReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_EstateBansReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<EstateBansReplyEventArgs> EstateBansReply
        {
            add { lock (m_EstateBansReply_Lock) { m_EstateBansReply += value; } }
            remove { lock (m_EstateBansReply_Lock) { m_EstateBansReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<EstateCovenantReplyEventArgs> m_EstateCovenantReply;

        /// <summary>Raises the EstateCovenantReply event</summary>
        /// <param name="e">A EstateCovenantReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnEstateCovenantReply(EstateCovenantReplyEventArgs e)
        {
            EventHandler<EstateCovenantReplyEventArgs> handler = m_EstateCovenantReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_EstateCovenantReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<EstateCovenantReplyEventArgs> EstateCovenantReply
        {
            add { lock (m_EstateCovenantReply_Lock) { m_EstateCovenantReply += value; } }
            remove { lock (m_EstateCovenantReply_Lock) { m_EstateCovenantReply -= value; } }
        }


        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<EstateUpdateInfoReplyEventArgs> m_EstateUpdateInfoReply;

        /// <summary>Raises the EstateUpdateInfoReply event</summary>
        /// <param name="e">A EstateUpdateInfoReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnEstateUpdateInfoReply(EstateUpdateInfoReplyEventArgs e)
        {
            EventHandler<EstateUpdateInfoReplyEventArgs> handler = m_EstateUpdateInfoReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_EstateUpdateInfoReply_Lock = new object();

        /// <summary>Raised when the data server responds to a <see cref="LandStatRequest"/> request.</summary>
        public event EventHandler<EstateUpdateInfoReplyEventArgs> EstateUpdateInfoReply
        {
            add { lock (m_EstateUpdateInfoReply_Lock) { m_EstateUpdateInfoReply += value; } }
            remove { lock (m_EstateUpdateInfoReply_Lock) { m_EstateUpdateInfoReply -= value; } }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Requests estate information such as top scripts and colliders
        /// </summary>
        /// <param name="parcelLocalID"></param>
        /// <param name="reportType"></param>
        /// <param name="requestFlags"></param>
        /// <param name="filter"></param>
        public void LandStatRequest(int parcelLocalID, LandStatReportType reportType, uint requestFlags, string filter)
        {
            LandStatRequestPacket p = new LandStatRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                RequestData =
                {
                    Filter = Utils.StringToBytes(filter),
                    ParcelLocalID = parcelLocalID,
                    ReportType = (uint)reportType,
                    RequestFlags = requestFlags
                }
            };
            Client.Network.SendPacket(p);
        }

        /// <summary>Requests estate settings, including estate manager and access/ban lists</summary>
        public void RequestInfo()
        {
            EstateOwnerMessage("getinfo", "");
        }

        /// <summary>Requests the "Top Scripts" list for the current region</summary>
        public void RequestTopScripts()
        {
            //EstateOwnerMessage("scripts", "");
            LandStatRequest(0, LandStatReportType.TopScripts, 0, "");
        }

        /// <summary>Requests the "Top Colliders" list for the current region</summary>
        public void RequestTopColliders()
        {
            //EstateOwnerMessage("colliders", "");
            LandStatRequest(0, LandStatReportType.TopColliders, 0, "");
        }

        /// <summary>
        /// Set several estate specific configuration variables
        /// </summary>
        /// <param name="WaterHeight">The Height of the waterlevel over the entire estate. Defaults to 20</param>
        /// <param name="TerrainRaiseLimit">The maximum height change allowed above the baked terrain. Defaults to 4</param>
        /// <param name="TerrainLowerLimit">The minimum height change allowed below the baked terrain. Defaults to -4</param>
        /// <param name="UseEstateSun">true to use</param>
        /// <param name="FixedSun">if True forces the sun position to the position in SunPosition</param>
        /// <param name="SunPosition">The current position of the sun on the estate, or when FixedSun is true the static position
        /// the sun will remain. <remarks>6.0 = Sunrise, 30.0 = Sunset</remarks></param>
        public void SetTerrainVariables(float WaterHeight, float TerrainRaiseLimit,
            float TerrainLowerLimit, bool UseEstateSun, bool FixedSun, float SunPosition)
        {
            List<string> simVariables = new List<string>();
            simVariables.Add(WaterHeight.ToString(Utils.EnUsCulture));
            simVariables.Add(TerrainRaiseLimit.ToString(Utils.EnUsCulture));
            simVariables.Add(TerrainLowerLimit.ToString(Utils.EnUsCulture));
            simVariables.Add(UseEstateSun ? "Y" : "N");
            simVariables.Add(FixedSun ? "Y" : "N");
            simVariables.Add(SunPosition.ToString(Utils.EnUsCulture));
            simVariables.Add("Y"); //Not used?
            simVariables.Add("N"); //Not used?
            simVariables.Add("0.00"); //Also not used?
            EstateOwnerMessage("setregionterrain", simVariables);
        }

        /// <summary>
        /// Request return of objects owned by specified avatar 
        /// </summary>
        /// <param name="Target">The Agents <see cref="UUID"/> owning the primitives to return</param>
        /// <param name="flag">specify the coverage and type of objects to be included in the return</param>
        /// <param name="EstateWide">true to perform return on entire estate</param>
        public void SimWideReturn(UUID Target, EstateReturnFlags flag, bool EstateWide)
        {
            if (EstateWide)
            {
                List<string> param = new List<string>();
                param.Add(flag.ToString());
                param.Add(Target.ToString());
                EstateOwnerMessage("estateobjectreturn", param);
            }
            else
            {
                SimWideDeletesPacket simDelete = new SimWideDeletesPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    DataBlock =
                    {
                        TargetID = Target,
                        Flags = (uint)flag
                    }
                };
                Client.Network.SendPacket(simDelete);
            }
        }

        /// <summary></summary>
        /// <param name="method"></param>
        /// <param name="param"></param>
        public void EstateOwnerMessage(string method, string param)
        {
            List<string> listParams = new List<string>();
            listParams.Add(param);
            EstateOwnerMessage(method, listParams);
        }

        /// <summary>
        /// Used for setting and retrieving various estate panel settings
        /// </summary>
        /// <param name="method">EstateOwnerMessage Method field</param>
        /// <param name="listParams">List of parameters to include</param>
        public void EstateOwnerMessage(string method, List<string> listParams)
        {
            EstateOwnerMessagePacket estate = new EstateOwnerMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Invoice = UUID.Random(),
                    Method = Utils.StringToBytes(method)
                },
                ParamList = new EstateOwnerMessagePacket.ParamListBlock[listParams.Count]
            };
            for (int i = 0; i < listParams.Count; i++)
            {
                estate.ParamList[i] = new EstateOwnerMessagePacket.ParamListBlock
                {
                    Parameter = Utils.StringToBytes(listParams[i])
                };
            }
            Client.Network.SendPacket((Packet)estate);
        }

        /// <summary>
        /// Kick an avatar from an estate
        /// </summary>
        /// <param name="userID">Key of Agent to remove</param>
        public void KickUser(UUID userID)
        {
            EstateOwnerMessage("kickestate", userID.ToString());
        }

        /// <summary>
        /// Ban an avatar from an estate</summary>
        /// <param name="userID">Key of Agent to remove</param>
        /// <param name="allEstates">Ban user from this estate and all others owned by the estate owner</param>
        public void BanUser(UUID userID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.BanUserAllEstates : (uint)EstateAccessDelta.BanUser;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(userID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }

        /// <summary>Unban an avatar from an estate</summary>
        /// <param name="userID">Key of Agent to remove</param>
        ///  /// <param name="allEstates">Unban user from this estate and all others owned by the estate owner</param>
        public void UnbanUser(UUID userID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.UnbanUserAllEstates : (uint)EstateAccessDelta.UnbanUser;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(userID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }

        /// <summary>
        /// Send a message dialog to everyone in an entire estate
        /// </summary>
        /// <param name="message">Message to send all users in the estate</param>
        public void EstateMessage(string message)
        {
            List<string> listParams = new List<string>();
            listParams.Add(Client.Self.FirstName + " " + Client.Self.LastName);
            listParams.Add(message);
            EstateOwnerMessage("instantmessage", listParams);
        }

        /// <summary>
        /// Send a message dialog to everyone in a simulator
        /// </summary>
        /// <param name="message">Message to send all users in the simulator</param>
        public void SimulatorMessage(string message)
        {
            List<string> listParams = new List<string>();
            listParams.Add("-1");
            listParams.Add("-1");
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(Client.Self.FirstName + " " + Client.Self.LastName);
            listParams.Add(message);
            EstateOwnerMessage("simulatormessage", listParams);
        }

        /// <summary>
        /// Send an avatar back to their home location
        /// </summary>
        /// <param name="pest">Key of avatar to send home</param>
        public void TeleportHomeUser(UUID pest)
        {
            List<string> listParams = new List<string>();
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(pest.ToString());
            EstateOwnerMessage("teleporthomeuser", listParams);
        }

        /// <summary>
        /// Begin the region restart process
        /// </summary>
        public void RestartRegion()
        {
            RestartRegion(120);
        }
        public void RestartRegion(int delay)
        {
            if (delay < 30) delay = 30;
            else if (delay > 240) delay = 240;
            EstateOwnerMessage("restart", delay.ToString());
        }

        /// <summary>
        /// Cancels a region restart
        /// </summary>
        public void CancelRestart()
        {
            EstateOwnerMessage("restart", "-1");
        }

        [Obsolete("Use SetRegionInfo with new arguments or SetRegionInfoUdp")]
        public void SetRegionInfo(bool blockTerraform, bool blockFly, bool allowDamage, bool allowLandResell,
            bool restrictPushing, bool allowParcelJoinDivide, float agentLimit, float objectBonus,
            RegionMaturity maturity)
        {
            SetRegionInfo(blockTerraform, blockFly, false, allowDamage, allowLandResell, restrictPushing,
                allowParcelJoinDivide, agentLimit, objectBonus, false, maturity);
        }
        
        public void SetRegionInfo(bool blockTerraform, bool blockFly, bool blockFlyOver, bool allowDamage, 
            bool allowLandResell, bool restrictPushing, bool allowParcelJoinDivide, float agentLimit, float objectBonus, 
            bool blockParcelSearch, RegionMaturity maturity)
        {
            if (Client.Network.CurrentSim?.Caps?.CapabilityURI("DispatchRegionInfo") == null 
                || !SetRegionInfoHttp(blockTerraform, blockFly, blockFlyOver, allowDamage, allowLandResell, restrictPushing, 
                    allowParcelJoinDivide, agentLimit, objectBonus, blockParcelSearch, maturity).Result)
            {
                Logger.Log("Falling back to LLUDP SetRegionInfo", Helpers.LogLevel.Info);
                SetRegionInfoUdp(blockTerraform, blockFly, allowDamage, allowLandResell, restrictPushing,
                    allowParcelJoinDivide, agentLimit, objectBonus, maturity);
            }
        }
        
        public async Task<bool> SetRegionInfoHttp(bool blockTerraform, bool blockFly, bool blockFlyOver, bool allowDamage, 
            bool allowLandResell, bool restrictPushing, bool allowParcelJoinDivide, float agentLimit, float objectBonus, 
            bool blockParcelSearch, RegionMaturity maturity)
        {
            var uri = Client.Network.CurrentSim?.Caps?.CapabilityURI("DispatchRegionInfo");
            if (uri != null)
            {
                var req = new OSDMap
                {
                    ["block_terraform"] = blockTerraform,
                    ["block_fly"] = blockFly,
                    ["block_fly_over"] = blockFlyOver,
                    ["allow_damage"] = allowDamage,
                    ["allow_land_resell"] = allowLandResell,
                    ["agent_limit"] = agentLimit,
                    ["prim_bonus"] = objectBonus,
                    ["sim_access"] = maturity.ToString("D"),
                    ["restrict_pushobject"] = restrictPushing,
                    ["allow_parcel_changes"] = allowParcelJoinDivide,
                    ["block_parcel_search"] = blockParcelSearch
                };
                using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(req), Encoding.UTF8,
                           "application/llsd+xml"))
                {
                    using (var reply = await Client.HttpCapsClient.PostAsync(uri, content))
                    {
                        if (reply.IsSuccessStatusCode)
                        {
                            return true;
                        }

                        Logger.Log($"Failed to set region info via capability: {reply.ReasonPhrase}", Helpers.LogLevel.Warning);
                    }
                }
            }
            return false;
        }
        
        /// <summary>Estate panel "Region" tab settings</summary>
        public void SetRegionInfoUdp(bool blockTerraform, bool blockFly, bool allowDamage, bool allowLandResell, 
            bool restrictPushing, bool allowParcelJoinDivide, float agentLimit, float objectBonus, RegionMaturity maturity)
        {

            List<string> listParams = new List<string>();
            listParams.Add(blockTerraform ? "Y" : "N");
            listParams.Add(blockFly ? "Y" : "N");
            listParams.Add(allowDamage ? "Y" : "N");
            listParams.Add(allowLandResell ? "Y" : "N");
            listParams.Add(agentLimit.ToString(CultureInfo.InvariantCulture));
            listParams.Add(objectBonus.ToString(CultureInfo.InvariantCulture));
            listParams.Add(maturity.ToString("D"));
            listParams.Add(restrictPushing ? "Y" : "N");
            listParams.Add(allowParcelJoinDivide ? "Y" : "N");
            EstateOwnerMessage("setregioninfo", listParams);
        }

        /// <summary>Estate panel "Debug" tab settings</summary>
        public void SetRegionDebug(bool disableScripts, bool disableCollisions, bool disablePhysics)
        {
            List<string> listParams = new List<string>();
            listParams.Add(disableScripts ? "Y" : "N");
            listParams.Add(disableCollisions ? "Y" : "N");
            listParams.Add(disablePhysics ? "Y" : "N");
            EstateOwnerMessage("setregiondebug", listParams);
        }

        /// <summary>Used for setting the region's terrain textures for its four height levels</summary>
        /// <param name="low"></param>
        /// <param name="midLow"></param>
        /// <param name="midHigh"></param>
        /// <param name="high"></param>
        public void SetRegionTerrain(UUID low, UUID midLow, UUID midHigh, UUID high)
        {
            List<string> listParams = new List<string>();
            listParams.Add("0 " + low);
            listParams.Add("1 " + midLow);
            listParams.Add("2 " + midHigh);
            listParams.Add("3 " + high);
            EstateOwnerMessage("texturedetail", listParams);
            EstateOwnerMessage("texturecommit", "");
        }

        /// <summary>Used for setting sim terrain texture heights</summary> 
        public void SetRegionTerrainHeights(float lowSW, float highSW, float lowNW, float highNW, float lowSE, float highSE, float lowNE, float highNE)
        {
            List<string> listParams = new List<string>();
            listParams.Add("0 " + lowSW.ToString(Utils.EnUsCulture) + " " + highSW.ToString(Utils.EnUsCulture)); //SW low-high 
            listParams.Add("1 " + lowNW.ToString(Utils.EnUsCulture) + " " + highNW.ToString(Utils.EnUsCulture)); //NW low-high 
            listParams.Add("2 " + lowSE.ToString(Utils.EnUsCulture) + " " + highSE.ToString(Utils.EnUsCulture)); //SE low-high 
            listParams.Add("3 " + lowNE.ToString(Utils.EnUsCulture) + " " + highNE.ToString(Utils.EnUsCulture)); //NE low-high 
            EstateOwnerMessage("textureheights", listParams);
            EstateOwnerMessage("texturecommit", "");
        }

        /// <summary>Requests the estate covenant</summary>
        public void RequestCovenant()
        {
            EstateCovenantRequestPacket req = new EstateCovenantRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };
            Client.Network.SendPacket(req);
        }

        /// <summary>
        /// Requests Estate Covenant notecard from <see cref="Client.Network.CurrentSim" /> asset service
        /// </summary>
        /// <param name="covenantId">Asset UUID for estate covenant notecard</param>
        /// <param name="callback">Asset Received callback</param>
        /// <seealso cref="AssetManager.RequestAssetUDP"/>
        public void RequestCovenantNotecard(UUID covenantId, AssetManager.AssetReceivedCallback callback)
        {
            RequestCovenantNotecard(covenantId, Client.Network.CurrentSim, callback);
        }

        /// <summary>
        /// Requests Estate Covenant notecard from asset service
        /// </summary>
        /// <param name="covenantId">Asset UUID for estate covenant notecard</param>
        /// <param name="simulator">Requested simulator</param>
        /// <param name="callback">Asset Received callback</param>
        /// <seealso cref="AssetManager.RequestAssetUDP"/>
        public void RequestCovenantNotecard(UUID covenantId, Simulator simulator, AssetManager.AssetReceivedCallback callback)
        {
            var transfer = new AssetDownload
            {
                ID = UUID.Random(),
                AssetID = covenantId,
                AssetType = AssetType.Notecard,
                Priority = 101.0f,
                Channel = ChannelType.Asset,
                Source = SourceType.SimEstate,
                Simulator = simulator,
                Callback = callback
            };
            Client.Assets.RequestEstateAsset(transfer, EstateAssetType.Covenant);
        }

        /// <summary>
        /// Upload a terrain RAW file
        /// </summary>
        /// <param name="fileData">A byte array containing the encoded terrain data</param>
        /// <param name="fileName">The name of the file being uploaded</param>
        /// <returns>The Id of the transfer request</returns>
        public UUID UploadTerrain(byte[] fileData, string fileName)
        {
            AssetUpload upload = new AssetUpload
            {
                AssetData = fileData,
                AssetType = AssetType.Unknown,
                Size = fileData.Length,
                ID = UUID.Random()
            };

            // Tell the library we have a pending file to upload
            Client.Assets.SetPendingAssetUploadData(upload);

            // Create and populate a list with commands specific to uploading a raw terrain file
            List<string> paramList = new List<string>();
            paramList.Add("upload filename");
            paramList.Add(fileName);

            // Tell the simulator we have a new raw file to upload
            Client.Estate.EstateOwnerMessage("terrain", paramList);

            return upload.ID;
        }

        /// <summary>
        /// Teleports all users home in current Estate
        /// </summary>
        public void TeleportHomeAllUsers()
        {
            List<string> Params = new List<string>();
            Params.Add(Client.Self.AgentID.ToString());
            EstateOwnerMessage("teleporthomeallusers", Params);
        }

        /// <summary>
        /// Remove estate manager</summary>
        /// <param name="userID">Key of Agent to Remove</param>
        /// <param name="allEstates">removes manager to this estate and all others owned by the estate owner</param>
        public void RemoveEstateManager(UUID userID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.RemoveManagerAllEstates : (uint)EstateAccessDelta.RemoveManager;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(userID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }

        /// <summary>
        /// Add estate manager</summary>
        /// <param name="userID">Key of Agent to Add</param>
        /// <param name="allEstates">Add agent as manager to this estate and all others owned by the estate owner</param>
        public void AddEstateManager(UUID userID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.AddManagerAllEstates : (uint)EstateAccessDelta.AddManager;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(userID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }

        /// <summary>
        /// Add's an agent to the estate Allowed list</summary>
        /// <param name="userID">Key of Agent to Add</param>
        /// <param name="allEstates">Add agent as an allowed reisdent to All estates if true</param>
        public void AddAllowedUser(UUID userID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.AddAllowedAllEstates : (uint)EstateAccessDelta.AddUserAsAllowed;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(userID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }

        /// <summary>
        /// Removes an agent from the estate Allowed list</summary>
        /// <param name="userID">Key of Agent to Remove</param>
        /// <param name="allEstates">Removes agent as an allowed reisdent from All estates if true</param>
        public void RemoveAllowedUser(UUID userID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.RemoveUserAllowedAllEstates : (uint)EstateAccessDelta.RemoveUserAsAllowed;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(userID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }
        ///
        /// <summary>
        /// Add's a group to the estate Allowed list</summary>
        /// <param name="groupID">Key of Group to Add</param>
        /// <param name="allEstates">Add Group as an allowed group to All estates if true</param>
        public void AddAllowedGroup(UUID groupID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.AddGroupAllowedAllEstates : (uint)EstateAccessDelta.AddGroupAsAllowed;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(groupID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }
        ///
        /// <summary>
        /// Removes a group from the estate Allowed list</summary>
        /// <param name="groupID">Key of Group to Remove</param>
        /// <param name="allEstates">Removes Group as an allowed Group from All estates if true</param>
        public void RemoveAllowedGroup(UUID groupID, bool allEstates)
        {
            List<string> listParams = new List<string>();
            uint flag = allEstates ? (uint)EstateAccessDelta.RemoveGroupAllowedAllEstates : (uint)EstateAccessDelta.RemoveGroupAsAllowed;
            listParams.Add(Client.Self.AgentID.ToString());
            listParams.Add(flag.ToString());
            listParams.Add(groupID.ToString());
            EstateOwnerMessage("estateaccessdelta", listParams);
        }
        #endregion


        #region Packet Handlers

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void EstateCovenantReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            EstateCovenantReplyPacket reply = (EstateCovenantReplyPacket)e.Packet;
            OnEstateCovenantReply(new EstateCovenantReplyEventArgs(
               reply.Data.CovenantID,
               reply.Data.CovenantTimestamp,
               Utils.BytesToString(reply.Data.EstateName),
               reply.Data.EstateOwnerID));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void EstateOwnerMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            EstateOwnerMessagePacket message = (EstateOwnerMessagePacket)e.Packet;
            uint estateID;
            string method = Utils.BytesToString(message.MethodData.Method);
            //List<string> parameters = new List<string>();

            if (method == "estateupdateinfo")
            {
                string estateName = Utils.BytesToString(message.ParamList[0].Parameter);
                UUID estateOwner = new UUID(Utils.BytesToString(message.ParamList[1].Parameter));
                estateID = Utils.BytesToUInt(message.ParamList[2].Parameter);
                /*
                foreach (EstateOwnerMessagePacket.ParamListBlock param in message.ParamList)
                {
                    parameters.Add(Utils.BytesToString(param.Parameter));
                }
                */
                bool denyNoPaymentInfo;
                if (Utils.BytesToUInt(message.ParamList[8].Parameter) == 0) denyNoPaymentInfo = true;
                else denyNoPaymentInfo = false;

                OnEstateUpdateInfoReply(new EstateUpdateInfoReplyEventArgs(estateName, estateOwner, estateID, denyNoPaymentInfo));
            }

            else if (method == "setaccess")
            {
                int count;
                estateID = Utils.BytesToUInt(message.ParamList[0].Parameter);
                if (message.ParamList.Length > 1)
                {
                    //param comes in as a string for some reason
                    uint param;
                    if (!uint.TryParse(Utils.BytesToString(message.ParamList[1].Parameter), out param)) return;

                    EstateAccessReplyDelta accessType = (EstateAccessReplyDelta)param;

                    switch (accessType)
                    {
                        case EstateAccessReplyDelta.EstateManagers:
                            //if (OnGetEstateManagers != null)
                            {
                                if (message.ParamList.Length > 5)
                                {
                                    if (!int.TryParse(Utils.BytesToString(message.ParamList[5].Parameter), out count)) return;
                                    List<UUID> managers = new List<UUID>();
                                    for (int i = 6; i < message.ParamList.Length; i++)
                                    {
                                        try
                                        {
                                            UUID managerID = new UUID(message.ParamList[i].Parameter, 0);
                                            managers.Add(managerID);
                                        }
                                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                                    }
                                    OnEstateManagersReply(new EstateManagersReplyEventArgs(estateID, count, managers));
                                }
                            }
                            break;

                        case EstateAccessReplyDelta.EstateBans:
                            //if (OnGetEstateBans != null)
                            {
                                if (message.ParamList.Length > 5)
                                {
                                    if (!int.TryParse(Utils.BytesToString(message.ParamList[4].Parameter), out count)) return;
                                    List<UUID> bannedUsers = new List<UUID>();
                                    for (int i = 6; i < message.ParamList.Length; i++)
                                    {
                                        try
                                        {
                                            UUID bannedID = new UUID(message.ParamList[i].Parameter, 0);
                                            bannedUsers.Add(bannedID);
                                        }
                                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                                    }
                                    OnEstateBansReply(new EstateBansReplyEventArgs(estateID, count, bannedUsers));
                                }
                            }
                            break;

                        case EstateAccessReplyDelta.AllowedUsers:
                            //if (OnGetAllowedUsers != null)
                            {
                                if (message.ParamList.Length > 5)
                                {
                                    if (!int.TryParse(Utils.BytesToString(message.ParamList[2].Parameter), out count)) return;
                                    List<UUID> allowedUsers = new List<UUID>();
                                    for (int i = 6; i < message.ParamList.Length; i++)
                                    {
                                        try
                                        {
                                            UUID allowedID = new UUID(message.ParamList[i].Parameter, 0);
                                            allowedUsers.Add(allowedID);
                                        }
                                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                                    }
                                    OnEstateUsersReply(new EstateUsersReplyEventArgs(estateID, count, allowedUsers));
                                }
                            }
                            break;

                        case EstateAccessReplyDelta.AllowedGroups:
                            //if (OnGetAllowedGroups != null)
                            {
                                if (message.ParamList.Length > 5)
                                {
                                    if (!int.TryParse(Utils.BytesToString(message.ParamList[3].Parameter), out count)) return;
                                    List<UUID> allowedGroups = new List<UUID>();
                                    for (int i = 6; i < message.ParamList.Length; i++)
                                    {
                                        try
                                        {
                                            UUID groupID = new UUID(message.ParamList[i].Parameter, 0);
                                            allowedGroups.Add(groupID);
                                        }
                                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                                    }
                                    OnEstateGroupsReply(new EstateGroupsReplyEventArgs(estateID, count, allowedGroups));
                                }
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void LandStatReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            //if (OnLandStatReply != null || OnGetTopScripts != null || OnGetTopColliders != null)
            //if (OnGetTopScripts != null || OnGetTopColliders != null)
            {
                LandStatReplyPacket p = (LandStatReplyPacket)e.Packet;
                Dictionary<UUID, EstateTask> Tasks = new Dictionary<UUID, EstateTask>();

                foreach (LandStatReplyPacket.ReportDataBlock rep in p.ReportData)
                {
                    EstateTask task = new EstateTask
                    {
                        Position = new Vector3(rep.LocationX, rep.LocationY, rep.LocationZ),
                        Score = rep.Score,
                        TaskID = rep.TaskID,
                        TaskLocalID = rep.TaskLocalID,
                        TaskName = Utils.BytesToString(rep.TaskName),
                        OwnerName = Utils.BytesToString(rep.OwnerName)
                    };
                    Tasks.Add(task.TaskID, task);
                }

                LandStatReportType type = (LandStatReportType)p.RequestData.ReportType;

                if (type == LandStatReportType.TopScripts)
                {
                    OnTopScriptsReply(new TopScriptsReplyEventArgs((int)p.RequestData.TotalObjectCount, Tasks));
                }
                else if (type == LandStatReportType.TopColliders)
                {
                    OnTopCollidersReply(new TopCollidersReplyEventArgs((int)p.RequestData.TotalObjectCount, Tasks));
                }

                /*
                if (OnGetTopColliders != null)
                {
                    //FIXME - System.UnhandledExceptionEventArgs
                    OnLandStatReply(
                        type,
                        p.RequestData.RequestFlags,
                        (int)p.RequestData.TotalObjectCount,
                        Tasks
                    );
                }
                */

            }
        }
        private void LandStatCapsReplyHandler(string capsKey, IMessage message, Simulator simulator)
        {
            LandStatReplyMessage m = (LandStatReplyMessage)message;
            Dictionary<UUID, EstateTask> Tasks = new Dictionary<UUID, EstateTask>();

            foreach (LandStatReplyMessage.ReportDataBlock rep in m.ReportDataBlocks)
            {
                EstateTask task = new EstateTask
                {
                    Position = rep.Location,
                    Score = rep.Score,
                    MonoScore = rep.MonoScore,
                    TaskID = rep.TaskID,
                    TaskLocalID = rep.TaskLocalID,
                    TaskName = rep.TaskName,
                    OwnerName = rep.OwnerName
                };
                Tasks.Add(task.TaskID, task);
            }

            LandStatReportType type = (LandStatReportType)m.ReportType;

            if (type == LandStatReportType.TopScripts)
            {
                OnTopScriptsReply(new TopScriptsReplyEventArgs((int)m.TotalObjectCount, Tasks));
            }
            else if (type == LandStatReportType.TopColliders)
            {
                OnTopCollidersReply(new TopCollidersReplyEventArgs((int)m.TotalObjectCount, Tasks));
            }
        }
        #endregion
    }
    #region EstateTools EventArgs Classes

    /// <summary>Raised on LandStatReply when the report type is for "top colliders"</summary>
    public class TopCollidersReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The number of returned items in LandStatReply
        /// </summary>
        public int ObjectCount { get; }

        /// <summary>
        /// A Dictionary of Object UUIDs to tasks returned in LandStatReply
        /// </summary>
        public Dictionary<UUID, EstateTask> Tasks { get; }

        /// <summary>Construct a new instance of the TopCollidersReplyEventArgs class</summary>
        /// <param name="objectCount">The number of returned items in LandStatReply</param>
        /// <param name="tasks">Dictionary of Object UUIDs to tasks returned in LandStatReply</param>
        public TopCollidersReplyEventArgs(int objectCount, Dictionary<UUID, EstateTask> tasks)
        {
            this.ObjectCount = objectCount;
            this.Tasks = tasks;
        }
    }

    /// <summary>Raised on LandStatReply when the report type is for "top Scripts"</summary>
    public class TopScriptsReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The number of scripts returned in LandStatReply
        /// </summary>
        public int ObjectCount { get; }

        /// <summary>
        /// A Dictionary of Object UUIDs to tasks returned in LandStatReply
        /// </summary>
        public Dictionary<UUID, EstateTask> Tasks { get; }

        /// <summary>Construct a new instance of the TopScriptsReplyEventArgs class</summary>
        /// <param name="objectCount">The number of returned items in LandStatReply</param>
        /// <param name="tasks">Dictionary of Object UUIDs to tasks returned in LandStatReply</param>
        public TopScriptsReplyEventArgs(int objectCount, Dictionary<UUID, EstateTask> tasks)
        {
            this.ObjectCount = objectCount;
            this.Tasks = tasks;
        }
    }

    /// <summary>Returned, along with other info, upon a successful .RequestInfo()</summary>
    public class EstateBansReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The identifier of the estate
        /// </summary>
        public uint EstateID { get; }

        /// <summary>
        /// The number of returned itmes
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// List of UUIDs of Banned Users
        /// </summary>
        public List<UUID> Banned { get; }

        /// <summary>Construct a new instance of the EstateBansReplyEventArgs class</summary>
        /// <param name="estateID">The estate's identifier on the grid</param>
        /// <param name="count">The number of returned items in LandStatReply</param>
        /// <param name="banned">User UUIDs banned</param>
        public EstateBansReplyEventArgs(uint estateID, int count, List<UUID> banned)
        {
            this.EstateID = estateID;
            this.Count = count;
            this.Banned = banned;
        }
    }

    /// <summary>Returned, along with other info, upon a successful .RequestInfo()</summary>
    public class EstateUsersReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The identifier of the estate
        /// </summary>
        public uint EstateID { get; }

        /// <summary>
        /// The number of returned items
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// List of UUIDs of Allowed Users
        /// </summary>
        public List<UUID> AllowedUsers { get; }

        /// <summary>Construct a new instance of the EstateUsersReplyEventArgs class</summary>
        /// <param name="estateID">The estate's identifier on the grid</param>
        /// <param name="count">The number of users</param>
        /// <param name="allowedUsers">Allowed users UUIDs</param>
        public EstateUsersReplyEventArgs(uint estateID, int count, List<UUID> allowedUsers)
        {
            this.EstateID = estateID;
            this.Count = count;
            this.AllowedUsers = allowedUsers;
        }
    }

    /// <summary>Returned, along with other info, upon a successful .RequestInfo()</summary>
    public class EstateGroupsReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The identifier of the estate
        /// </summary>
        public uint EstateID { get; }

        /// <summary>
        /// The number of returned items
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// List of UUIDs of Allowed Groups
        /// </summary>
        public List<UUID> AllowedGroups { get; }

        /// <summary>Construct a new instance of the EstateGroupsReplyEventArgs class</summary>
        /// <param name="estateID">The estate's identifier on the grid</param>
        /// <param name="count">The number of Groups</param>
        /// <param name="allowedGroups">Allowed Groups UUIDs</param>
        public EstateGroupsReplyEventArgs(uint estateID, int count, List<UUID> allowedGroups)
        {
            this.EstateID = estateID;
            this.Count = count;
            this.AllowedGroups = allowedGroups;
        }
    }

    /// <summary>Returned, along with other info, upon a successful .RequestInfo()</summary>
    public class EstateManagersReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The identifier of the estate
        /// </summary>
        public uint EstateID { get; }

        /// <summary>
        /// The number of returned items
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// List of UUIDs of the Estate's Managers
        /// </summary>
        public List<UUID> Managers { get; }

        /// <summary>Construct a new instance of the EstateManagersReplyEventArgs class</summary>
        /// <param name="estateID">The estate's identifier on the grid</param>
        /// <param name="count">The number of Managers</param>
        /// <param name="managers"> Managers UUIDs</param>
        public EstateManagersReplyEventArgs(uint estateID, int count, List<UUID> managers)
        {
            this.EstateID = estateID;
            this.Count = count;
            this.Managers = managers;
        }
    }

    /// <summary>Returned, along with other info, upon a successful .RequestInfo()</summary>
    public class EstateCovenantReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The Covenant
        /// </summary>
        public UUID CovenantID { get; }

        /// <summary>
        /// The timestamp
        /// </summary>
        public long Timestamp { get; }

        /// <summary>
        /// The Estate name
        /// </summary>
        public string EstateName { get; }

        /// <summary>
        /// The Estate Owner's ID (can be a GroupID)
        /// </summary>
        public UUID EstateOwnerID { get; }

        /// <summary>Construct a new instance of the EstateCovenantReplyEventArgs class</summary>
        /// <param name="covenantID">The Covenant ID</param>
        /// <param name="timestamp">The timestamp</param>
        /// <param name="estateName">The estate's name</param>
        /// <param name="estateOwnerID">The Estate Owner's ID (can be a GroupID)</param>
        public EstateCovenantReplyEventArgs(UUID covenantID, long timestamp, string estateName, UUID estateOwnerID)
        {
            this.CovenantID = covenantID;
            this.Timestamp = timestamp;
            this.EstateName = estateName;
            this.EstateOwnerID = estateOwnerID;

        }
    }


    /// <summary>Returned, along with other info, upon a successful .RequestInfo()</summary>
    public class EstateUpdateInfoReplyEventArgs : EventArgs
    {
        /// <summary>
        /// The estate's name
        /// </summary>
        public string EstateName { get; }

        /// <summary>
        /// The Estate Owner's ID (can be a GroupID)
        /// </summary>
        public UUID EstateOwner { get; }

        /// <summary>
        /// The identifier of the estate on the grid
        /// </summary>
        public uint EstateID { get; }

        /// <summary></summary>
        public bool DenyNoPaymentInfo { get; }

        /// <summary>Construct a new instance of the EstateUpdateInfoReplyEventArgs class</summary>
        /// <param name="estateName">The estate's name</param>
        /// <param name="estateOwner">The Estate Owners ID (can be a GroupID)</param>
        /// <param name="estateID">The estate's identifier on the grid</param>
        /// <param name="denyNoPaymentInfo"></param>
        public EstateUpdateInfoReplyEventArgs(string estateName, UUID estateOwner, uint estateID, bool denyNoPaymentInfo)
        {
            this.EstateName = estateName;
            this.EstateOwner = estateOwner;
            this.EstateID = estateID;
            this.DenyNoPaymentInfo = denyNoPaymentInfo;

        }
    }
    #endregion
}
