/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2022, Sjofn LLC
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
using System.Net;
using System.IO;
using ComponentAce.Compression.Libs.zlib;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Interfaces;

namespace OpenMetaverse.Messages.Linden
{
    #region Teleport/Region/Movement Messages

    /// <summary>
    /// Sent to the client to indicate a teleport request has completed
    /// </summary>
    public class TeleportFinishMessage : IMessage
    {
        /// <summary>The <see cref="UUID"/> of the agent</summary>
        public UUID AgentID;
        /// <summary></summary>
        public int LocationID;
        /// <summary>The simulators handle the agent teleported to</summary>
        public ulong RegionHandle;
        /// <summary>A Uri which contains a list of Capabilities the simulator supports</summary>
        public Uri SeedCapability;
        /// <summary>Indicates the level of access required
        /// to access the simulator, or the content rating, or the simulators 
        /// map status</summary>
        public SimAccess SimAccess;
        /// <summary>The IP Address of the simulator</summary>
        public IPAddress IP;
        /// <summary>The UDP Port the simulator will listen for UDP traffic on</summary>
        public int Port;
        /// <summary>Status flags indicating the state of the Agent upon arrival, Flying, etc.</summary>
        public TeleportFlags Flags;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1);

            OSDArray infoArray = new OSDArray(1);

            OSDMap info = new OSDMap(8)
            {
                {"AgentID", OSD.FromUUID(AgentID)},
                {"LocationID", OSD.FromInteger(LocationID)},
                {"RegionHandle", OSD.FromULong(RegionHandle)},
                {"SeedCapability", OSD.FromUri(SeedCapability)},
                {"SimAccess", OSD.FromInteger((byte) SimAccess)},
                {"SimIP", MessageUtils.FromIP(IP)},
                {"SimPort", OSD.FromInteger(Port)},
                {"TeleportFlags", OSD.FromUInteger((uint) Flags)}
            };
            // Unused by the client

            infoArray.Add(info);

            map.Add("Info", infoArray);

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray array = (OSDArray)map["Info"];
            OSDMap blockMap = (OSDMap)array[0];

            AgentID = blockMap["AgentID"].AsUUID();
            LocationID = blockMap["LocationID"].AsInteger();
            RegionHandle = blockMap["RegionHandle"].AsULong();
            SeedCapability = blockMap["SeedCapability"].AsUri();
            SimAccess = (SimAccess)blockMap["SimAccess"].AsInteger();
            IP = MessageUtils.ToIP(blockMap["SimIP"]);
            Port = blockMap["SimPort"].AsInteger();
            Flags = (TeleportFlags)blockMap["TeleportFlags"].AsUInteger();
        }
    }

    /// <summary>
    /// Sent to the viewer when a neighboring simulator is requesting the agent make a connection to it.
    /// </summary>
    public class EstablishAgentCommunicationMessage : IMessage
    {
        public UUID AgentID;
        public IPAddress Address;
        public int Port;
        public Uri SeedCapability;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3)
            {
                ["agent-id"] = OSD.FromUUID(AgentID),
                ["sim-ip-and-port"] = OSD.FromString($"{Address}:{Port}"),
                ["seed-capability"] = OSD.FromUri(SeedCapability)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            string ipAndPort = map["sim-ip-and-port"].AsString();
            int i = ipAndPort.IndexOf(':');

            AgentID = map["agent-id"].AsUUID();
            Address = IPAddress.Parse(ipAndPort.Substring(0, i));
            Port = Int32.Parse(ipAndPort.Substring(i + 1));
            SeedCapability = map["seed-capability"].AsUri();
        }
    }

    public class CrossedRegionMessage : IMessage
    {
        public Vector3 LookAt;
        public Vector3 Position;
        public UUID AgentID;
        public UUID SessionID;
        public ulong RegionHandle;
        public Uri SeedCapability;
        public IPAddress IP;
        public int Port;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3);

            OSDArray infoArray = new OSDArray(1);
            OSDMap infoMap = new OSDMap(2)
            {
                ["LookAt"] = OSD.FromVector3(LookAt),
                ["Position"] = OSD.FromVector3(Position)
            };
            infoArray.Add(infoMap);
            map["Info"] = infoArray;

            OSDArray agentDataArray = new OSDArray(1);
            OSDMap agentDataMap = new OSDMap(2)
            {
                ["AgentID"] = OSD.FromUUID(AgentID),
                ["SessionID"] = OSD.FromUUID(SessionID)
            };
            agentDataArray.Add(agentDataMap);
            map["AgentData"] = agentDataArray;

            OSDArray regionDataArray = new OSDArray(1);
            OSDMap regionDataMap = new OSDMap(4)
            {
                ["RegionHandle"] = OSD.FromULong(RegionHandle),
                ["SeedCapability"] = OSD.FromUri(SeedCapability),
                ["SimIP"] = MessageUtils.FromIP(IP),
                ["SimPort"] = OSD.FromInteger(Port)
            };
            regionDataArray.Add(regionDataMap);
            map["RegionData"] = regionDataArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDMap infoMap = (OSDMap)((OSDArray)map["Info"])[0];
            LookAt = infoMap["LookAt"].AsVector3();
            Position = infoMap["Position"].AsVector3();

            OSDMap agentDataMap = (OSDMap)((OSDArray)map["AgentData"])[0];
            AgentID = agentDataMap["AgentID"].AsUUID();
            SessionID = agentDataMap["SessionID"].AsUUID();

            OSDMap regionDataMap = (OSDMap)((OSDArray)map["RegionData"])[0];
            RegionHandle = regionDataMap["RegionHandle"].AsULong();
            SeedCapability = regionDataMap["SeedCapability"].AsUri();
            IP = MessageUtils.ToIP(regionDataMap["SimIP"]);
            Port = regionDataMap["SimPort"].AsInteger();
        }
    }

    public class EnableSimulatorMessage : IMessage
    {
        public class SimulatorInfoBlock
        {
            public ulong RegionHandle;
            public IPAddress IP;
            public int Port;
        }

        public SimulatorInfoBlock[] Simulators;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1);

            OSDArray array = new OSDArray(Simulators.Length);
            foreach (SimulatorInfoBlock block in Simulators)
            {
                OSDMap blockMap = new OSDMap(3)
                {
                    ["Handle"] = OSD.FromULong(block.RegionHandle),
                    ["IP"] = MessageUtils.FromIP(block.IP),
                    ["Port"] = OSD.FromInteger(block.Port)
                };
                array.Add(blockMap);
            }

            map["SimulatorInfo"] = array;
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray array = (OSDArray)map["SimulatorInfo"];
            Simulators = new SimulatorInfoBlock[array.Count];

            for (int i = 0; i < array.Count; i++)
            {
                OSDMap blockMap = (OSDMap)array[i];

                SimulatorInfoBlock block = new SimulatorInfoBlock
                {
                    RegionHandle = blockMap["Handle"].AsULong(),
                    IP = MessageUtils.ToIP(blockMap["IP"]),
                    Port = blockMap["Port"].AsInteger()
                };
                Simulators[i] = block;
            }
        }
    }

    /// <summary>
    /// A message sent to the client which indicates a teleport request has failed
    /// and contains some information on why it failed
    /// </summary>
    public class TeleportFailedMessage : IMessage
    {
        /// <summary></summary>
        public string ExtraParams;
        /// <summary>A string key of the reason the teleport failed e.g. CouldntTPCloser
        /// Which could be used to look up a value in a dictionary or enum</summary>
        public string MessageKey;
        /// <summary>The <see cref="UUID"/> of the Agent</summary>
        public UUID AgentID;
        /// <summary>A string human readable message containing the reason </summary>
        /// <remarks>An example: Could not teleport closer to destination</remarks>
        public string Reason;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2);

            OSDMap alertInfoMap = new OSDMap(2)
            {
                ["ExtraParams"] = OSD.FromString(ExtraParams),
                ["Message"] = OSD.FromString(MessageKey)
            };

            OSDArray alertArray = new OSDArray {alertInfoMap};
            map["AlertInfo"] = alertArray;

            OSDMap infoMap = new OSDMap(2)
            {
                ["AgentID"] = OSD.FromUUID(AgentID),
                ["Reason"] = OSD.FromString(Reason)
            };
            OSDArray infoArray = new OSDArray {infoMap};
            map["Info"] = infoArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {

            OSDArray alertInfoArray = (OSDArray)map["AlertInfo"];

            OSDMap alertInfoMap = (OSDMap)alertInfoArray[0];
            ExtraParams = alertInfoMap["ExtraParams"].AsString();
            MessageKey = alertInfoMap["Message"].AsString();

            OSDArray infoArray = (OSDArray)map["Info"];
            OSDMap infoMap = (OSDMap)infoArray[0];
            AgentID = infoMap["AgentID"].AsUUID();
            Reason = infoMap["Reason"].AsString();
        }
    }

    public class LandStatReplyMessage : IMessage
    {
        public uint ReportType;
        public uint RequestFlags;
        public uint TotalObjectCount;

        public class ReportDataBlock
        {
            public Vector3 Location;
            public string OwnerName;
            public float Score;
            public UUID TaskID;
            public uint TaskLocalID;
            public string TaskName;
            public float MonoScore;
            public DateTime TimeStamp;
        }

        public ReportDataBlock[] ReportDataBlocks;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3);

            OSDMap requestDataMap = new OSDMap(3)
            {
                ["ReportType"] = OSD.FromUInteger(this.ReportType),
                ["RequestFlags"] = OSD.FromUInteger(this.RequestFlags),
                ["TotalObjectCount"] = OSD.FromUInteger(this.TotalObjectCount)
            };

            OSDArray requestDatArray = new OSDArray {requestDataMap};
            map["RequestData"] = requestDatArray;

            OSDArray reportDataArray = new OSDArray();
            OSDArray dataExtendedArray = new OSDArray();
            foreach (ReportDataBlock t in ReportDataBlocks)
            {
                OSDMap reportMap = new OSDMap(8)
                {
                    ["LocationX"] = OSD.FromReal(t.Location.X),
                    ["LocationY"] = OSD.FromReal(t.Location.Y),
                    ["LocationZ"] = OSD.FromReal(t.Location.Z),
                    ["OwnerName"] = OSD.FromString(t.OwnerName),
                    ["Score"] = OSD.FromReal(t.Score),
                    ["TaskID"] = OSD.FromUUID(t.TaskID),
                    ["TaskLocalID"] = OSD.FromReal(t.TaskLocalID),
                    ["TaskName"] = OSD.FromString(t.TaskName)
                };
                reportDataArray.Add(reportMap);

                OSDMap extendedMap = new OSDMap(2)
                {
                    ["MonoScore"] = OSD.FromReal(t.MonoScore),
                    ["TimeStamp"] = OSD.FromDate(t.TimeStamp)
                };
                dataExtendedArray.Add(extendedMap);
            }

            map["ReportData"] = reportDataArray;
            map["DataExtended"] = dataExtendedArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {

            OSDArray requestDataArray = (OSDArray)map["RequestData"];
            OSDMap requestMap = (OSDMap)requestDataArray[0];

            this.ReportType = requestMap["ReportType"].AsUInteger();
            this.RequestFlags = requestMap["RequestFlags"].AsUInteger();
            this.TotalObjectCount = requestMap["TotalObjectCount"].AsUInteger();

            if (TotalObjectCount < 1)
            {
                ReportDataBlocks = new ReportDataBlock[0];
                return;
            }

            OSDArray dataArray = (OSDArray)map["ReportData"];
            OSDArray dataExtendedArray = (OSDArray)map["DataExtended"];

            ReportDataBlocks = new ReportDataBlock[dataArray.Count];
            for (int i = 0; i < dataArray.Count; i++)
            {
                OSDMap blockMap = (OSDMap)dataArray[i];
                OSDMap extMap = (OSDMap)dataExtendedArray[i];
                ReportDataBlock block = new ReportDataBlock
                {
                    Location = new Vector3(
                        (float) blockMap["LocationX"].AsReal(),
                        (float) blockMap["LocationY"].AsReal(),
                        (float) blockMap["LocationZ"].AsReal()),
                    OwnerName = blockMap["OwnerName"].AsString(),
                    Score = (float) blockMap["Score"].AsReal(),
                    TaskID = blockMap["TaskID"].AsUUID(),
                    TaskLocalID = blockMap["TaskLocalID"].AsUInteger(),
                    TaskName = blockMap["TaskName"].AsString(),
                    MonoScore = (float) extMap["MonoScore"].AsReal(),
                    TimeStamp = Utils.UnixTimeToDateTime(extMap["TimeStamp"].AsUInteger())
                };

                ReportDataBlocks[i] = block;
            }
        }
    }

    #endregion

    #region Parcel Messages

    /// <summary>
    /// Contains a list of prim owner information for a specific parcel in a simulator
    /// </summary>
    /// <remarks>
    /// A Simulator will always return at least 1 entry
    /// If agent does not have proper permission the OwnerID will be UUID.Zero
    /// If agent does not have proper permission OR there are no primitives on parcel
    /// the DataBlocksExtended map will not be sent from the simulator
    /// </remarks>
    public class ParcelObjectOwnersReplyMessage : IMessage
    {
        /// <summary>
        /// Prim ownership information for a specified owner on a single parcel
        /// </summary>
        public class PrimOwner
        {
            /// <summary>The <see cref="UUID"/> of the prim owner, 
            /// UUID.Zero if agent has no permission to view prim owner information</summary>
            public UUID OwnerID;
            /// <summary>The total number of prims</summary>
            public int Count;
            /// <summary>True if the OwnerID is a <see cref="Group"/></summary>
            public bool IsGroupOwned;
            /// <summary>True if the owner is online 
            /// <remarks>This is no longer used by the LL Simulators</remarks></summary>
            public bool OnlineStatus;
            /// <summary>The date the most recent prim was rezzed</summary>
            public DateTime TimeStamp;
        }

        /// <summary>An Array of <see cref="PrimOwner"/> objects</summary>
        public PrimOwner[] PrimOwnersBlock;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDArray dataArray = new OSDArray(PrimOwnersBlock.Length);
            OSDArray dataExtendedArray = new OSDArray();

            foreach (PrimOwner t in PrimOwnersBlock)
            {
                OSDMap dataMap = new OSDMap(4)
                {
                    ["OwnerID"] = OSD.FromUUID(t.OwnerID),
                    ["Count"] = OSD.FromInteger(t.Count),
                    ["IsGroupOwned"] = OSD.FromBoolean(t.IsGroupOwned),
                    ["OnlineStatus"] = OSD.FromBoolean(t.OnlineStatus)
                };
                dataArray.Add(dataMap);

                OSDMap dataExtendedMap = new OSDMap(1) {["TimeStamp"] = OSD.FromDate(t.TimeStamp)};
                dataExtendedArray.Add(dataExtendedMap);
            }

            OSDMap map = new OSDMap {{"Data", dataArray}};
            if (dataExtendedArray.Count > 0)
                map.Add("DataExtended", dataExtendedArray);

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray dataArray = (OSDArray)map["Data"];

            // DataExtended is optional, will not exist of parcel contains zero prims
            OSDArray dataExtendedArray;
            if (map.ContainsKey("DataExtended"))
            {
                dataExtendedArray = (OSDArray)map["DataExtended"];
            }
            else
            {
                dataExtendedArray = new OSDArray();
            }

            PrimOwnersBlock = new PrimOwner[dataArray.Count];

            for (int i = 0; i < dataArray.Count; i++)
            {
                OSDMap dataMap = (OSDMap)dataArray[i];
                PrimOwner block = new PrimOwner
                {
                    OwnerID = dataMap["OwnerID"].AsUUID(),
                    Count = dataMap["Count"].AsInteger(),
                    IsGroupOwned = dataMap["IsGroupOwned"].AsBoolean(),
                    OnlineStatus = dataMap["OnlineStatus"].AsBoolean()
                };
                // deprecated

                /* if the agent has no permissions, or there are no prims, the counts
                 * should not match up, so we don't decode the DataExtended map */
                if (dataExtendedArray.Count == dataArray.Count)
                {
                    OSDMap dataExtendedMap = (OSDMap)dataExtendedArray[i];
                    block.TimeStamp = Utils.UnixTimeToDateTime(dataExtendedMap["TimeStamp"].AsUInteger());
                }

                PrimOwnersBlock[i] = block;
            }
        }
    }

    /// <summary>
    /// The details of a single parcel in a region, also contains some regionwide globals
    /// </summary>
    [Serializable]
    public class ParcelPropertiesMessage : IMessage
    {
        /// <summary>Simulator-local ID of this parcel</summary>
        public int LocalID;
        /// <summary>Maximum corner of the axis-aligned bounding box for this
        /// parcel</summary>
        public Vector3 AABBMax;
        /// <summary>Minimum corner of the axis-aligned bounding box for this
        /// parcel</summary>
        public Vector3 AABBMin;
        /// <summary>Total parcel land area</summary>
        public int Area;
        /// <summary></summary>
        public uint AuctionID;
        /// <summary>Key of authorized buyer</summary>
        public UUID AuthBuyerID;
        /// <summary>Bitmap describing land layout in 4x4m squares across the 
        /// entire region</summary>
        public byte[] Bitmap;
        /// <summary></summary>
        public ParcelCategory Category;
        /// <summary>Date land was claimed</summary>
        public DateTime ClaimDate;
        /// <summary>Appears to always be zero</summary>
        public int ClaimPrice;
        /// <summary>Parcel Description</summary>
        public string Desc;
        /// <summary></summary>
        public ParcelFlags ParcelFlags;
        /// <summary></summary>
        public UUID GroupID;
        /// <summary>Total number of primitives owned by the parcel group on 
        /// this parcel</summary>
        public int GroupPrims;
        /// <summary>Whether the land is deeded to a group or not</summary>
        public bool IsGroupOwned;
        /// <summary></summary>
        public LandingType LandingType;
        /// <summary>Maximum number of primitives this parcel supports</summary>
        public int MaxPrims;
        /// <summary>The Asset UUID of the Texture which when applied to a 
        /// primitive will display the media</summary>
        public UUID MediaID;
        /// <summary>A URL which points to any Quicktime supported media type</summary>
        public string MediaURL;
        /// <summary>A byte, if 0x1 viewer should auto scale media to fit object</summary>
        public bool MediaAutoScale;
        /// <summary>URL For Music Stream</summary>
        public string MusicURL;
        /// <summary>Parcel Name</summary>
        public string Name;
        /// <summary>Autoreturn value in minutes for others' objects</summary>
        public int OtherCleanTime;
        /// <summary></summary>
        public int OtherCount;
        /// <summary>Total number of other primitives on this parcel</summary>
        public int OtherPrims;
        /// <summary>UUID of the owner of this parcel</summary>
        public UUID OwnerID;
        /// <summary>Total number of primitives owned by the parcel owner on 
        /// this parcel</summary>
        public int OwnerPrims;
        /// <summary></summary>
        public float ParcelPrimBonus;
        /// <summary>How long is pass valid for</summary>
        public float PassHours;
        /// <summary>Price for a temporary pass</summary>
        public int PassPrice;
        /// <summary></summary>
        public int PublicCount;
        /// <summary>Disallows people outside the parcel from being able to see in</summary>
        public bool Privacy;
        /// <summary></summary>
        public bool RegionDenyAnonymous;
        /// <summary></summary>
        public bool RegionDenyIdentified;
        /// <summary></summary>
        public bool RegionDenyTransacted;
        /// <summary>True if the region denies access to age unverified users</summary>
        public bool RegionDenyAgeUnverified;
        /// <summary></summary>
        public bool RegionPushOverride;
        /// <summary>This field is no longer used</summary>
        public int RentPrice;
        /// The result of a request for parcel properties
        public ParcelResult RequestResult;
        /// <summary>Sale price of the parcel, only useful if ForSale is set</summary>
        /// <remarks>The SalePrice will remain the same after an ownership
        /// transfer (sale), so it can be used to see the purchase price after
        /// a sale if the new owner has not changed it</remarks>
        public int SalePrice;
        /// <summary>
        /// Number of primitives your avatar is currently
        /// selecting and sitting on in this parcel
        /// </summary>
        public int SelectedPrims;
        /// <summary></summary>
        public int SelfCount;
        /// <summary>
        /// A number which increments by 1, starting at 0 for each ParcelProperties request. 
        /// Can be overriden by specifying the sequenceID with the ParcelPropertiesRequest being sent. 
        /// a Negative number indicates the action in <seealso cref="ParcelPropertiesStatus"/> has occurred. 
        /// </summary>
        public int SequenceID;
        /// <summary>Maximum primitives across the entire simulator</summary>
        public int SimWideMaxPrims;
        /// <summary>Total primitives across the entire simulator</summary>
        public int SimWideTotalPrims;
        /// <summary></summary>
        public bool SnapSelection;
        /// <summary>Key of parcel snapshot</summary>
        public UUID SnapshotID;
        /// <summary>Parcel ownership status</summary>
        public ParcelStatus Status;
        /// <summary>Total number of primitives on this parcel</summary>
        public int TotalPrims;
        /// <summary></summary>
        public Vector3 UserLocation;
        /// <summary></summary>
        public Vector3 UserLookAt;
        /// <summary>A description of the media</summary>
        public string MediaDesc;
        /// <summary>An Integer which represents the height of the media</summary>
        public int MediaHeight;
        /// <summary>An integer which represents the width of the media</summary>
        public int MediaWidth;
        /// <summary>A boolean, if true the viewer should loop the media</summary>
        public bool MediaLoop;
        /// <summary>A string which contains the mime type of the media</summary>
        public string MediaType;
        /// <summary>true to obscure (hide) media url</summary>
        public bool ObscureMedia;
        /// <summary>true to obscure (hide) music url</summary>
        public bool ObscureMusic;
        /// <summary> true if avatars in this parcel should be invisible to people outside</summary>
        public bool SeeAVs;
        /// <summary> true if avatars outside can hear any sounds avatars inside play</summary>
        public bool AnyAVSounds;
        /// <summary> true if group members outside can hear any sounds avatars inside play</summary>
        public bool GroupAVSounds;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3);

            OSDArray dataArray = new OSDArray(1);
            OSDMap parcelDataMap = new OSDMap(47)
            {
                ["LocalID"] = OSD.FromInteger(LocalID),
                ["AABBMax"] = OSD.FromVector3(AABBMax),
                ["AABBMin"] = OSD.FromVector3(AABBMin),
                ["Area"] = OSD.FromInteger(Area),
                ["AuctionID"] = OSD.FromInteger(AuctionID),
                ["AuthBuyerID"] = OSD.FromUUID(AuthBuyerID),
                ["Bitmap"] = OSD.FromBinary(Bitmap),
                ["Category"] = OSD.FromInteger((int) Category),
                ["ClaimDate"] = OSD.FromDate(ClaimDate),
                ["ClaimPrice"] = OSD.FromInteger(ClaimPrice),
                ["Desc"] = OSD.FromString(Desc),
                ["ParcelFlags"] = OSD.FromUInteger((uint) ParcelFlags),
                ["GroupID"] = OSD.FromUUID(GroupID),
                ["GroupPrims"] = OSD.FromInteger(GroupPrims),
                ["IsGroupOwned"] = OSD.FromBoolean(IsGroupOwned),
                ["LandingType"] = OSD.FromInteger((int) LandingType),
                ["MaxPrims"] = OSD.FromInteger(MaxPrims),
                ["MediaID"] = OSD.FromUUID(MediaID),
                ["MediaURL"] = OSD.FromString(MediaURL),
                ["MediaAutoScale"] = OSD.FromBoolean(MediaAutoScale),
                ["MusicURL"] = OSD.FromString(MusicURL),
                ["Name"] = OSD.FromString(Name),
                ["OtherCleanTime"] = OSD.FromInteger(OtherCleanTime),
                ["OtherCount"] = OSD.FromInteger(OtherCount),
                ["OtherPrims"] = OSD.FromInteger(OtherPrims),
                ["OwnerID"] = OSD.FromUUID(OwnerID),
                ["OwnerPrims"] = OSD.FromInteger(OwnerPrims),
                ["ParcelPrimBonus"] = OSD.FromReal((float) ParcelPrimBonus),
                ["PassHours"] = OSD.FromReal((float) PassHours),
                ["PassPrice"] = OSD.FromInteger(PassPrice),
                ["PublicCount"] = OSD.FromInteger(PublicCount),
                ["Privacy"] = OSD.FromBoolean(Privacy),
                ["RegionDenyAnonymous"] = OSD.FromBoolean(RegionDenyAnonymous),
                ["RegionDenyIdentified"] = OSD.FromBoolean(RegionDenyIdentified),
                ["RegionDenyTransacted"] = OSD.FromBoolean(RegionDenyTransacted),
                ["RegionPushOverride"] = OSD.FromBoolean(RegionPushOverride),
                ["RentPrice"] = OSD.FromInteger(RentPrice),
                ["RequestResult"] = OSD.FromInteger((int) RequestResult),
                ["SalePrice"] = OSD.FromInteger(SalePrice),
                ["SelectedPrims"] = OSD.FromInteger(SelectedPrims),
                ["SelfCount"] = OSD.FromInteger(SelfCount),
                ["SequenceID"] = OSD.FromInteger(SequenceID),
                ["SimWideMaxPrims"] = OSD.FromInteger(SimWideMaxPrims),
                ["SimWideTotalPrims"] = OSD.FromInteger(SimWideTotalPrims),
                ["SnapSelection"] = OSD.FromBoolean(SnapSelection),
                ["SnapshotID"] = OSD.FromUUID(SnapshotID),
                ["Status"] = OSD.FromInteger((int) Status),
                ["TotalPrims"] = OSD.FromInteger(TotalPrims),
                ["UserLocation"] = OSD.FromVector3(UserLocation),
                ["UserLookAt"] = OSD.FromVector3(UserLookAt),
                ["SeeAVs"] = OSD.FromBoolean(SeeAVs),
                ["AnyAVSounds"] = OSD.FromBoolean(AnyAVSounds),
                ["GroupAVSounds"] = OSD.FromBoolean(GroupAVSounds)
            };
            dataArray.Add(parcelDataMap);
            map["ParcelData"] = dataArray;

            OSDArray mediaDataArray = new OSDArray(1);
            OSDMap mediaDataMap = new OSDMap(7)
            {
                ["MediaDesc"] = OSD.FromString(MediaDesc),
                ["MediaHeight"] = OSD.FromInteger(MediaHeight),
                ["MediaWidth"] = OSD.FromInteger(MediaWidth),
                ["MediaLoop"] = OSD.FromBoolean(MediaLoop),
                ["MediaType"] = OSD.FromString(MediaType),
                ["ObscureMedia"] = OSD.FromBoolean(ObscureMedia),
                ["ObscureMusic"] = OSD.FromBoolean(ObscureMusic)
            };
            mediaDataArray.Add(mediaDataMap);
            map["MediaData"] = mediaDataArray;

            OSDArray ageVerificationBlockArray = new OSDArray(1);
            OSDMap ageVerificationBlockMap = new OSDMap(1)
            {
                ["RegionDenyAgeUnverified"] = OSD.FromBoolean(RegionDenyAgeUnverified)
            };
            ageVerificationBlockArray.Add(ageVerificationBlockMap);
            map["AgeVerificationBlock"] = ageVerificationBlockArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDMap parcelDataMap = (OSDMap)((OSDArray)map["ParcelData"])[0];
            LocalID = parcelDataMap["LocalID"].AsInteger();
            AABBMax = parcelDataMap["AABBMax"].AsVector3();
            AABBMin = parcelDataMap["AABBMin"].AsVector3();
            Area = parcelDataMap["Area"].AsInteger();
            AuctionID = (uint)parcelDataMap["AuctionID"].AsInteger();
            AuthBuyerID = parcelDataMap["AuthBuyerID"].AsUUID();
            Bitmap = parcelDataMap["Bitmap"].AsBinary();
            Category = (ParcelCategory)parcelDataMap["Category"].AsInteger();
            ClaimDate = Utils.UnixTimeToDateTime((uint)parcelDataMap["ClaimDate"].AsInteger());
            ClaimPrice = parcelDataMap["ClaimPrice"].AsInteger();
            Desc = parcelDataMap["Desc"].AsString();

            // LL sends this as binary, we'll convert it here
            if (parcelDataMap["ParcelFlags"].Type == OSDType.Binary)
            {
                byte[] bytes = parcelDataMap["ParcelFlags"].AsBinary();
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                ParcelFlags = (ParcelFlags)BitConverter.ToUInt32(bytes, 0);
            }
            else
            {
                ParcelFlags = (ParcelFlags)parcelDataMap["ParcelFlags"].AsUInteger();
            }
            GroupID = parcelDataMap["GroupID"].AsUUID();
            GroupPrims = parcelDataMap["GroupPrims"].AsInteger();
            IsGroupOwned = parcelDataMap["IsGroupOwned"].AsBoolean();
            LandingType = (LandingType)parcelDataMap["LandingType"].AsInteger();
            MaxPrims = parcelDataMap["MaxPrims"].AsInteger();
            MediaID = parcelDataMap["MediaID"].AsUUID();
            MediaURL = parcelDataMap["MediaURL"].AsString();
            MediaAutoScale = parcelDataMap["MediaAutoScale"].AsBoolean(); // 0x1 = yes
            MusicURL = parcelDataMap["MusicURL"].AsString();
            Name = parcelDataMap["Name"].AsString();
            OtherCleanTime = parcelDataMap["OtherCleanTime"].AsInteger();
            OtherCount = parcelDataMap["OtherCount"].AsInteger();
            OtherPrims = parcelDataMap["OtherPrims"].AsInteger();
            OwnerID = parcelDataMap["OwnerID"].AsUUID();
            OwnerPrims = parcelDataMap["OwnerPrims"].AsInteger();
            ParcelPrimBonus = (float)parcelDataMap["ParcelPrimBonus"].AsReal();
            PassHours = (float)parcelDataMap["PassHours"].AsReal();
            PassPrice = parcelDataMap["PassPrice"].AsInteger();
            PublicCount = parcelDataMap["PublicCount"].AsInteger();
            Privacy = parcelDataMap["Privacy"].AsBoolean();
            RegionDenyAnonymous = parcelDataMap["RegionDenyAnonymous"].AsBoolean();
            RegionDenyIdentified = parcelDataMap["RegionDenyIdentified"].AsBoolean();
            RegionDenyTransacted = parcelDataMap["RegionDenyTransacted"].AsBoolean();
            RegionPushOverride = parcelDataMap["RegionPushOverride"].AsBoolean();
            RentPrice = parcelDataMap["RentPrice"].AsInteger();
            RequestResult = (ParcelResult)parcelDataMap["RequestResult"].AsInteger();
            SalePrice = parcelDataMap["SalePrice"].AsInteger();
            SelectedPrims = parcelDataMap["SelectedPrims"].AsInteger();
            SelfCount = parcelDataMap["SelfCount"].AsInteger();
            SequenceID = parcelDataMap["SequenceID"].AsInteger();
            SimWideMaxPrims = parcelDataMap["SimWideMaxPrims"].AsInteger();
            SimWideTotalPrims = parcelDataMap["SimWideTotalPrims"].AsInteger();
            SnapSelection = parcelDataMap["SnapSelection"].AsBoolean();
            SnapshotID = parcelDataMap["SnapshotID"].AsUUID();
            Status = (ParcelStatus)parcelDataMap["Status"].AsInteger();
            TotalPrims = parcelDataMap["TotalPrims"].AsInteger();
            UserLocation = parcelDataMap["UserLocation"].AsVector3();
            UserLookAt = parcelDataMap["UserLookAt"].AsVector3();
            SeeAVs = parcelDataMap["SeeAVs"].AsBoolean();
            AnyAVSounds = parcelDataMap["AnyAVSounds"].AsBoolean();
            GroupAVSounds = parcelDataMap["GroupAVSounds"].AsBoolean();

            if (map.ContainsKey("MediaData")) // temporary, OpenSim doesn't send this block
            {
                OSDMap mediaDataMap = (OSDMap)((OSDArray)map["MediaData"])[0];
                MediaDesc = mediaDataMap["MediaDesc"].AsString();
                MediaHeight = mediaDataMap["MediaHeight"].AsInteger();
                MediaWidth = mediaDataMap["MediaWidth"].AsInteger();
                MediaLoop = mediaDataMap["MediaLoop"].AsBoolean();
                MediaType = mediaDataMap["MediaType"].AsString();
                ObscureMedia = mediaDataMap["ObscureMedia"].AsBoolean();
                ObscureMusic = mediaDataMap["ObscureMusic"].AsBoolean();
            }

            OSDMap ageVerificationBlockMap = (OSDMap)((OSDArray)map["AgeVerificationBlock"])[0];
            RegionDenyAgeUnverified = ageVerificationBlockMap["RegionDenyAgeUnverified"].AsBoolean();
        }
    }

    /// <summary>A message sent from the viewer to the simulator to updated a specific parcels settings</summary>
    public class ParcelPropertiesUpdateMessage : IMessage
    {
        /// <summary>The <seealso cref="UUID"/> of the agent authorized to purchase this
        /// parcel of land or a NULL <seealso cref="UUID"/> if the sale is authorized to anyone</summary>
        public UUID AuthBuyerID;
        /// <summary>true to enable auto scaling of the parcel media</summary>
        public bool MediaAutoScale;
        /// <summary>The category of this parcel used when search is enabled to restrict
        /// search results</summary>
        public ParcelCategory Category;
        /// <summary>A string containing the description to set</summary>
        public string Desc;
        /// <summary>The <seealso cref="UUID"/> of the <seealso cref="Group"/> which allows for additional
        /// powers and restrictions.</summary>
        public UUID GroupID;
        /// <summary>The <seealso cref="LandingType"/> which specifies how avatars which teleport
        /// to this parcel are handled</summary>
        public LandingType Landing;
        /// <summary>The LocalID of the parcel to update settings on</summary>
        public int LocalID;
        /// <summary>A string containing the description of the media which can be played
        /// to visitors</summary>
        public string MediaDesc;
        /// <summary></summary>
        public int MediaHeight;
        /// <summary></summary>
        public bool MediaLoop;
        /// <summary></summary>
        public UUID MediaID;
        /// <summary></summary>
        public string MediaType;
        /// <summary></summary>
        public string MediaURL;
        /// <summary></summary>
        public int MediaWidth;
        /// <summary></summary>
        public string MusicURL;
        /// <summary></summary>
        public string Name;
        /// <summary></summary>
        public bool ObscureMedia;
        /// <summary></summary>
        public bool ObscureMusic;
        /// <summary></summary>
        public ParcelFlags ParcelFlags;
        /// <summary></summary>
        public float PassHours;
        /// <summary></summary>
        public uint PassPrice;
        /// <summary></summary>
        public bool Privacy;
        /// <summary></summary>
        public uint SalePrice;
        /// <summary></summary>
        public UUID SnapshotID;
        /// <summary></summary>
        public Vector3 UserLocation;
        /// <summary></summary>
        public Vector3 UserLookAt;
        /// <summary> true if avatars in this parcel should be invisible to people outside</summary>
        public bool SeeAVs;
        /// <summary> true if avatars outside can hear any sounds avatars inside play</summary>
        public bool AnyAVSounds;
        /// <summary> true if group members outside can hear any sounds avatars inside play</summary>
        public bool GroupAVSounds;

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            AuthBuyerID = map["auth_buyer_id"].AsUUID();
            MediaAutoScale = map["auto_scale"].AsBoolean();
            Category = (ParcelCategory)map["category"].AsInteger();
            Desc = map["description"].AsString();
            GroupID = map["group_id"].AsUUID();
            Landing = (LandingType)map["landing_type"].AsUInteger();
            LocalID = map["local_id"].AsInteger();
            MediaDesc = map["media_desc"].AsString();
            MediaHeight = map["media_height"].AsInteger();
            MediaLoop = map["media_loop"].AsBoolean();
            MediaID = map["media_id"].AsUUID();
            MediaType = map["media_type"].AsString();
            MediaURL = map["media_url"].AsString();
            MediaWidth = map["media_width"].AsInteger();
            MusicURL = map["music_url"].AsString();
            Name = map["name"].AsString();
            ObscureMedia = map["obscure_media"].AsBoolean();
            ObscureMusic = map["obscure_music"].AsBoolean();
            ParcelFlags = (ParcelFlags)map["parcel_flags"].AsUInteger();
            PassHours = (float)map["pass_hours"].AsReal();
            PassPrice = map["pass_price"].AsUInteger();
            Privacy = map["privacy"].AsBoolean();
            SalePrice = map["sale_price"].AsUInteger();
            SnapshotID = map["snapshot_id"].AsUUID();
            UserLocation = map["user_location"].AsVector3();
            UserLookAt = map["user_look_at"].AsVector3();
            if (map.ContainsKey("see_avs"))
            {
                SeeAVs = map["see_avs"].AsBoolean();
                AnyAVSounds = map["any_av_sounds"].AsBoolean();
                GroupAVSounds = map["group_av_sounds"].AsBoolean();
            }
            else
            {
                SeeAVs = true;
                AnyAVSounds = true;
                GroupAVSounds = true;
            }
        }

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap
            {
                ["auth_buyer_id"] = OSD.FromUUID(AuthBuyerID),
                ["auto_scale"] = OSD.FromBoolean(MediaAutoScale),
                ["category"] = OSD.FromInteger((byte) Category),
                ["description"] = OSD.FromString(Desc),
                ["flags"] = OSD.FromBinary(Utils.EmptyBytes),
                ["group_id"] = OSD.FromUUID(GroupID),
                ["landing_type"] = OSD.FromInteger((byte) Landing),
                ["local_id"] = OSD.FromInteger(LocalID),
                ["media_desc"] = OSD.FromString(MediaDesc),
                ["media_height"] = OSD.FromInteger(MediaHeight),
                ["media_id"] = OSD.FromUUID(MediaID),
                ["media_loop"] = OSD.FromBoolean(MediaLoop),
                ["media_type"] = OSD.FromString(MediaType),
                ["media_url"] = OSD.FromString(MediaURL),
                ["media_width"] = OSD.FromInteger(MediaWidth),
                ["music_url"] = OSD.FromString(MusicURL),
                ["name"] = OSD.FromString(Name),
                ["obscure_media"] = OSD.FromBoolean(ObscureMedia),
                ["obscure_music"] = OSD.FromBoolean(ObscureMusic),
                ["parcel_flags"] = OSD.FromUInteger((uint) ParcelFlags),
                ["pass_hours"] = OSD.FromReal(PassHours),
                ["privacy"] = OSD.FromBoolean(Privacy),
                ["pass_price"] = OSD.FromInteger(PassPrice),
                ["sale_price"] = OSD.FromInteger(SalePrice),
                ["snapshot_id"] = OSD.FromUUID(SnapshotID),
                ["user_location"] = OSD.FromVector3(UserLocation),
                ["user_look_at"] = OSD.FromVector3(UserLookAt),
                ["see_avs"] = OSD.FromBoolean(SeeAVs),
                ["any_av_sounds"] = OSD.FromBoolean(AnyAVSounds),
                ["group_av_sounds"] = OSD.FromBoolean(GroupAVSounds)
            };

            return map;
        }
    }

    /// <summary>Base class used for the RemoteParcelRequest message</summary>
    [Serializable]
    public abstract class RemoteParcelRequestBlock
    {
        public abstract OSDMap Serialize();
        public abstract void Deserialize(OSDMap map);
    }

    /// <summary>
    /// A message sent from the viewer to the simulator to request information
    /// on a remote parcel
    /// </summary>
    public class RemoteParcelRequestRequest : RemoteParcelRequestBlock
    {
        /// <summary>Local sim position of the parcel we are looking up</summary>
        public Vector3 Location;
        /// <summary>Region handle of the parcel we are looking up</summary>
        public ulong RegionHandle;
        /// <summary>Region <see cref="UUID"/> of the parcel we are looking up</summary>
        public UUID RegionID;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3)
            {
                ["location"] = OSD.FromVector3(Location),
                ["region_handle"] = OSD.FromULong(RegionHandle),
                ["region_id"] = OSD.FromUUID(RegionID)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            Location = map["location"].AsVector3();
            RegionHandle = map["region_handle"].AsULong();
            RegionID = map["region_id"].AsUUID();
        }
    }

    /// <summary>
    /// A message sent from the simulator to the viewer in response to a <see cref="RemoteParcelRequestRequest"/> 
    /// which will contain parcel information
    /// </summary>
    [Serializable]
    public class RemoteParcelRequestReply : RemoteParcelRequestBlock
    {
        /// <summary>The grid-wide unique parcel ID</summary>
        public UUID ParcelID;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1) {["parcel_id"] = OSD.FromUUID(ParcelID)};
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            if (map == null || !map.ContainsKey("parcel_id"))
                ParcelID = UUID.Zero;
            else
                ParcelID = map["parcel_id"].AsUUID();
        }
    }

    /// <summary>
    /// A message containing a request for a remote parcel from a viewer, or a response
    /// from the simulator to that request
    /// </summary>
    [Serializable]
    public class RemoteParcelRequestMessage : IMessage
    {
        /// <summary>The request or response details block</summary>
        public RemoteParcelRequestBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("parcel_id"))
                Request = new RemoteParcelRequestReply();
            else if (map.ContainsKey("location"))
                Request = new RemoteParcelRequestRequest();
            else
                Logger.Log("Unable to deserialize RemoteParcelRequest: No message handler exists for method: " + map.AsString(), Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }
    #endregion

    #region Inventory Messages

    public class NewFileAgentInventoryMessage : IMessage
    {
        public UUID FolderID;
        public AssetType AssetType;
        public InventoryType InventoryType;
        public string Name;
        public string Description;
        public PermissionMask EveryoneMask;
        public PermissionMask GroupMask;
        public PermissionMask NextOwnerMask;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(5)
            {
                ["folder_id"] = OSD.FromUUID(FolderID),
                ["asset_type"] = OSD.FromString(Utils.AssetTypeToString(AssetType)),
                ["inventory_type"] = OSD.FromString(Utils.InventoryTypeToString(InventoryType)),
                ["name"] = OSD.FromString(Name),
                ["description"] = OSD.FromString(Description),
                ["everyone_mask"] = OSD.FromInteger((int) EveryoneMask),
                ["group_mask"] = OSD.FromInteger((int) GroupMask),
                ["next_owner_mask"] = OSD.FromInteger((int) NextOwnerMask)
            };

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            FolderID = map["folder_id"].AsUUID();
            AssetType = Utils.StringToAssetType(map["asset_type"].AsString());
            InventoryType = Utils.StringToInventoryType(map["inventory_type"].AsString());
            Name = map["name"].AsString();
            Description = map["description"].AsString();
            EveryoneMask = (PermissionMask)map["everyone_mask"].AsInteger();
            GroupMask = (PermissionMask)map["group_mask"].AsInteger();
            NextOwnerMask = (PermissionMask)map["next_owner_mask"].AsInteger();
        }
    }

    public class NewFileAgentInventoryReplyMessage : IMessage
    {
        public string State;
        public Uri Uploader;

        public NewFileAgentInventoryReplyMessage()
        {
            State = "upload";
        }

        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap
            {
                ["state"] = OSD.FromString(State),
                ["uploader"] = OSD.FromUri(Uploader)
            };

            return map;
        }

        public void Deserialize(OSDMap map)
        {
            State = map["state"].AsString();
            Uploader = map["uploader"].AsUri();
        }
    }

    public class NewFileAgentInventoryVariablePriceMessage : IMessage
    {
        public UUID FolderID;
        public AssetType AssetType;
        public InventoryType InventoryType;
        public string Name;
        public string Description;
        public PermissionMask EveryoneMask;
        public PermissionMask GroupMask;
        public PermissionMask NextOwnerMask;
        // TODO: asset_resources?

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap
            {
                ["folder_id"] = OSD.FromUUID(FolderID),
                ["asset_type"] = OSD.FromString(Utils.AssetTypeToString(AssetType)),
                ["inventory_type"] = OSD.FromString(Utils.InventoryTypeToString(InventoryType)),
                ["name"] = OSD.FromString(Name),
                ["description"] = OSD.FromString(Description),
                ["everyone_mask"] = OSD.FromInteger((int) EveryoneMask),
                ["group_mask"] = OSD.FromInteger((int) GroupMask),
                ["next_owner_mask"] = OSD.FromInteger((int) NextOwnerMask)
            };

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            FolderID = map["folder_id"].AsUUID();
            AssetType = Utils.StringToAssetType(map["asset_type"].AsString());
            InventoryType = Utils.StringToInventoryType(map["inventory_type"].AsString());
            Name = map["name"].AsString();
            Description = map["description"].AsString();
            EveryoneMask = (PermissionMask)map["everyone_mask"].AsInteger();
            GroupMask = (PermissionMask)map["group_mask"].AsInteger();
            NextOwnerMask = (PermissionMask)map["next_owner_mask"].AsInteger();
        }
    }

    public class NewFileAgentInventoryVariablePriceReplyMessage : IMessage
    {
        public int ResourceCost;
        public string State;
        public int UploadPrice;
        public Uri Rsvp;

        public NewFileAgentInventoryVariablePriceReplyMessage()
        {
            State = "confirm_upload";
        }

        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap
            {
                ["resource_cost"] = OSD.FromInteger(ResourceCost),
                ["state"] = OSD.FromString(State),
                ["upload_price"] = OSD.FromInteger(UploadPrice),
                ["rsvp"] = OSD.FromUri(Rsvp)
            };

            return map;
        }

        public void Deserialize(OSDMap map)
        {
            ResourceCost = map["resource_cost"].AsInteger();
            State = map["state"].AsString();
            UploadPrice = map["upload_price"].AsInteger();
            Rsvp = map["rsvp"].AsUri();
        }
    }

    public class NewFileAgentInventoryUploadReplyMessage : IMessage
    {
        public UUID NewInventoryItem;
        public UUID NewAsset;
        public string State;
        public PermissionMask NewBaseMask;
        public PermissionMask NewEveryoneMask;
        public PermissionMask NewOwnerMask;
        public PermissionMask NewNextOwnerMask;

        public NewFileAgentInventoryUploadReplyMessage()
        {
            State = "complete";
        }

        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap
            {
                ["new_inventory_item"] = OSD.FromUUID(NewInventoryItem),
                ["new_asset"] = OSD.FromUUID(NewAsset),
                ["state"] = OSD.FromString(State),
                ["new_base_mask"] = OSD.FromInteger((int) NewBaseMask),
                ["new_everyone_mask"] = OSD.FromInteger((int) NewEveryoneMask),
                ["new_owner_mask"] = OSD.FromInteger((int) NewOwnerMask),
                ["new_next_owner_mask"] = OSD.FromInteger((int) NewNextOwnerMask)
            };

            return map;
        }

        public void Deserialize(OSDMap map)
        {
            NewInventoryItem = map["new_inventory_item"].AsUUID();
            NewAsset = map["new_asset"].AsUUID();
            State = map["state"].AsString();
            NewBaseMask = (PermissionMask)map["new_base_mask"].AsInteger();
            NewEveryoneMask = (PermissionMask)map["new_everyone_mask"].AsInteger();
            NewOwnerMask = (PermissionMask)map["new_owner_mask"].AsInteger();
            NewNextOwnerMask = (PermissionMask)map["new_next_owner_mask"].AsInteger();
        }
    }

    public class BulkUpdateInventoryMessage : IMessage
    {
        public class FolderDataInfo
        {
            public UUID FolderID;
            public UUID ParentID;
            public string Name;
            public FolderType Type;

            public static FolderDataInfo FromOSD(OSD data)
            {
                FolderDataInfo ret = new FolderDataInfo();

                if (!(data is OSDMap map)) return ret;

                ret.FolderID = map["FolderID"];
                ret.ParentID = map["ParentID"];
                ret.Name = map["Name"];
                ret.Type = (FolderType)map["Type"].AsInteger();
                return ret;
            }
        }

        public class ItemDataInfo
        {
            public UUID ItemID;
            public uint CallbackID;
            public UUID FolderID;
            public UUID CreatorID;
            public UUID OwnerID;
            public UUID GroupID;
            public PermissionMask BaseMask;
            public PermissionMask OwnerMask;
            public PermissionMask GroupMask;
            public PermissionMask EveryoneMask;
            public PermissionMask NextOwnerMask;
            public bool GroupOwned;
            public UUID AssetID;
            public AssetType Type;
            public InventoryType InvType;
            public uint Flags;
            public SaleType SaleType;
            public int SalePrice;
            public string Name;
            public string Description;
            public DateTime CreationDate;
            public uint CRC;

            public static ItemDataInfo FromOSD(OSD data)
            {
                ItemDataInfo ret = new ItemDataInfo();

                if (!(data is OSDMap map)) return ret;

                ret.ItemID = map["ItemID"];
                ret.CallbackID = map["CallbackID"];
                ret.FolderID = map["FolderID"];
                ret.CreatorID = map["CreatorID"];
                ret.OwnerID = map["OwnerID"];
                ret.GroupID = map["GroupID"];
                ret.BaseMask = (PermissionMask)map["BaseMask"].AsUInteger();
                ret.OwnerMask = (PermissionMask)map["OwnerMask"].AsUInteger();
                ret.GroupMask = (PermissionMask)map["GroupMask"].AsUInteger();
                ret.EveryoneMask = (PermissionMask)map["EveryoneMask"].AsUInteger();
                ret.NextOwnerMask = (PermissionMask)map["NextOwnerMask"].AsUInteger();
                ret.GroupOwned = map["GroupOwned"];
                ret.AssetID = map["AssetID"];
                ret.Type = (AssetType)map["Type"].AsInteger();
                ret.InvType = (InventoryType)map["InvType"].AsInteger();
                ret.Flags = map["Flags"];
                ret.SaleType = (SaleType)map["SaleType"].AsInteger();
                ret.SalePrice = map["SaleType"];
                ret.Name = map["Name"];
                ret.Description = map["Description"];
                ret.CreationDate = Utils.UnixTimeToDateTime(map["CreationDate"]);
                ret.CRC = map["CRC"];

                return ret;
            }
        }

        public UUID AgentID;
        public UUID TransactionID;
        public FolderDataInfo[] FolderData;
        public ItemDataInfo[] ItemData;

        public OSDMap Serialize()
        {
            throw new NotImplementedException();
        }

        public void Deserialize(OSDMap map)
        {
            if (map["AgentData"] is OSDArray)
            {
                OSDArray array = (OSDArray)map["AgentData"];
                if (array.Count > 0)
                {
                    OSDMap adata = (OSDMap)array[0];
                    AgentID = adata["AgentID"];
                    TransactionID = adata["TransactionID"];
                }
            }
            
            if (map["FolderData"] is OSDArray)
            {
                OSDArray array = (OSDArray)map["FolderData"];
                FolderData =  new FolderDataInfo[array.Count];
                for (int i = 0; i < array.Count; i++)
                {
                    FolderData[i] = FolderDataInfo.FromOSD(array[i]);
                }
            }
            else
            {
                FolderData = new FolderDataInfo[0];
            }

            if (map["ItemData"] is OSDArray)
            {
                OSDArray array = (OSDArray)map["ItemData"];
                ItemData = new ItemDataInfo[array.Count];
                for (int i = 0; i < array.Count; i++)
                {
                    ItemData[i] = ItemDataInfo.FromOSD(array[i]);
                }
            }
            else
            {
                ItemData = new ItemDataInfo[0];
            }
        }
    }

    #endregion

    #region Agent Messages

    /// <summary>
    /// A message sent from the simulator to an agent which contains
    /// the groups the agent is in
    /// </summary>
    public class AgentGroupDataUpdateMessage : IMessage
    {
        /// <summary>The Agent receiving the message</summary>
        public UUID AgentID;

        /// <summary>Group Details specific to the agent</summary>
        public class GroupData
        {
            /// <summary>true of the agent accepts group notices</summary>
            public bool AcceptNotices;
            /// <summary>The agents tier contribution to the group</summary>
            public int Contribution;
            /// <summary>The Groups <seealso cref="UUID"/></summary>
            public UUID GroupID;
            /// <summary>The <seealso cref="UUID"/> of the groups insignia</summary>
            public UUID GroupInsigniaID;
            /// <summary>The name of the group</summary>
            public string GroupName;
            /// <summary>The aggregate permissions the agent has in the group for all roles the agent
            /// is assigned</summary>
            public GroupPowers GroupPowers;
        }

        /// <summary>An optional block containing additional agent specific information</summary>
        public class NewGroupData
        {
            /// <summary>true of the agent allows this group to be
            /// listed in their profile</summary>
            public bool ListInProfile;
        }

        /// <summary>An array containing <seealso cref="GroupData"/> information
        /// for each <see cref="Group"/> the agent is a member of</summary>
        public GroupData[] GroupDataBlock;
        /// <summary>An array containing <seealso cref="NewGroupData"/> information
        /// for each <see cref="Group"/> the agent is a member of</summary>
        public NewGroupData[] NewGroupDataBlock;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3);

            OSDMap agent = new OSDMap(1) {["AgentID"] = OSD.FromUUID(AgentID)};

            OSDArray agentArray = new OSDArray {agent};

            map["AgentData"] = agentArray;

            OSDArray groupDataArray = new OSDArray(GroupDataBlock.Length);

            foreach (GroupData t in GroupDataBlock)
            {
                OSDMap group = new OSDMap(6)
                {
                    ["AcceptNotices"] = OSD.FromBoolean(t.AcceptNotices),
                    ["Contribution"] = OSD.FromInteger(t.Contribution),
                    ["GroupID"] = OSD.FromUUID(t.GroupID),
                    ["GroupInsigniaID"] = OSD.FromUUID(t.GroupInsigniaID),
                    ["GroupName"] = OSD.FromString(t.GroupName),
                    ["GroupPowers"] = OSD.FromLong((long) t.GroupPowers)
                };
                groupDataArray.Add(@group);
            }

            map["GroupData"] = groupDataArray;

            OSDArray newGroupDataArray = new OSDArray(NewGroupDataBlock.Length);

            foreach (NewGroupData t in NewGroupDataBlock)
            {
                OSDMap group = new OSDMap(1) {["ListInProfile"] = OSD.FromBoolean(t.ListInProfile)};
                newGroupDataArray.Add(@group);
            }

            map["NewGroupData"] = newGroupDataArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray agentArray = (OSDArray)map["AgentData"];
            OSDMap agentMap = (OSDMap)agentArray[0];
            AgentID = agentMap["AgentID"].AsUUID();

            OSDArray groupArray = (OSDArray)map["GroupData"];

            GroupDataBlock = new GroupData[groupArray.Count];

            for (int i = 0; i < groupArray.Count; i++)
            {
                OSDMap groupMap = (OSDMap)groupArray[i];

                GroupData groupData = new GroupData
                {
                    GroupID = groupMap["GroupID"].AsUUID(),
                    Contribution = groupMap["Contribution"].AsInteger(),
                    GroupInsigniaID = groupMap["GroupInsigniaID"].AsUUID(),
                    GroupName = groupMap["GroupName"].AsString(),
                    GroupPowers = (GroupPowers) groupMap["GroupPowers"].AsLong(),
                    AcceptNotices = groupMap["AcceptNotices"].AsBoolean()
                };

                GroupDataBlock[i] = groupData;
            }

            // If request for current groups came very close to login
            // the Linden sim will not include the NewGroupData block, but
            // it will instead set all ListInProfile fields to false
            if (map.ContainsKey("NewGroupData"))
            {
                OSDArray newGroupArray = (OSDArray)map["NewGroupData"];

                NewGroupDataBlock = new NewGroupData[newGroupArray.Count];

                for (int i = 0; i < newGroupArray.Count; i++)
                {
                    OSDMap newGroupMap = (OSDMap)newGroupArray[i];
                    NewGroupData newGroupData = new NewGroupData
                    {
                        ListInProfile = newGroupMap["ListInProfile"].AsBoolean()
                    };
                    NewGroupDataBlock[i] = newGroupData;
                }
            }
            else
            {
                NewGroupDataBlock = new NewGroupData[GroupDataBlock.Length];
                for (int i = 0; i < NewGroupDataBlock.Length; i++)
                {
                    NewGroupData newGroupData = new NewGroupData {ListInProfile = false};
                    NewGroupDataBlock[i] = newGroupData;
                }
            }
        }
    }

    /// <summary>
    /// A message sent from the viewer to the simulator which 
    /// specifies the language and permissions for others to detect
    /// the language specified
    /// </summary>
    public class UpdateAgentLanguageMessage : IMessage
    {
        /// <summary>A string containng the default language 
        /// to use for the agent</summary>
        public string Language;
        /// <summary>true of others are allowed to
        /// know the language setting</summary>
        public bool LanguagePublic;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["language"] = OSD.FromString(Language),
                ["language_is_public"] = OSD.FromBoolean(LanguagePublic)
            };


            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            LanguagePublic = map["language_is_public"].AsBoolean();
            Language = map["language"].AsString();
        }
    }

    /// <summary>
    /// An EventQueue message sent from the simulator to an agent when the agent
    /// leaves a group
    /// </summary>
    public class AgentDropGroupMessage : IMessage
    {
        /// <summary>An object containing the Agents UUID, and the Groups UUID</summary>
        public class AgentData
        {
            /// <summary>The ID of the Agent leaving the group</summary>
            public UUID AgentID;
            /// <summary>The GroupID the Agent is leaving</summary>
            public UUID GroupID;
        }

        /// <summary>
        /// An Array containing the AgentID and GroupID
        /// </summary>
        public AgentData[] AgentDataBlock;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1);

            OSDArray agentDataArray = new OSDArray(AgentDataBlock.Length);

            foreach (AgentData t in AgentDataBlock)
            {
                OSDMap agentMap = new OSDMap(2)
                {
                    ["AgentID"] = OSD.FromUUID(t.AgentID),
                    ["GroupID"] = OSD.FromUUID(t.GroupID)
                };
                agentDataArray.Add(agentMap);
            }
            map["AgentData"] = agentDataArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray agentDataArray = (OSDArray)map["AgentData"];

            AgentDataBlock = new AgentData[agentDataArray.Count];

            for (int i = 0; i < agentDataArray.Count; i++)
            {
                OSDMap agentMap = (OSDMap)agentDataArray[i];
                AgentData agentData = new AgentData
                {
                    AgentID = agentMap["AgentID"].AsUUID(),
                    GroupID = agentMap["GroupID"].AsUUID()
                };


                AgentDataBlock[i] = agentData;
            }
        }
    }

    public class AgentStateUpdateMessage : IMessage
    {
        public OSDMap RawData;
        public bool CanModifyNavmesh;
        public bool HasModifiedNavmesh;
        public string MaxAccess; // PG, M, A
        public bool AlterNavmeshObjects;
        public bool AlterPermanentObjects;
        public int GodLevel;
        public string Language;
        public bool LanguageIsPublic;

        public void Deserialize(OSDMap map)
        {
            RawData = map;
            CanModifyNavmesh = map["can_modify_navmesh"];
            HasModifiedNavmesh = map["has_modified_navmesh"];
            if (map["preferences"] is OSDMap)
            {
                OSDMap prefs = (OSDMap)map["preferences"];
                AlterNavmeshObjects = prefs["alter_navmesh_objects"];
                AlterPermanentObjects = prefs["alter_permanent_objects"];
                GodLevel = prefs["god_level"];
                Language = prefs["language"];
                LanguageIsPublic = prefs["language_is_public"];
                if (prefs["access_prefs"] is OSDMap)
                {
                    OSDMap access = (OSDMap)prefs["access_prefs"];
                    MaxAccess = access["max"];
                }
            }
        }

        public OSDMap Serialize()
        {
            RawData = new OSDMap
            {
                ["can_modify_navmesh"] = CanModifyNavmesh,
                ["has_modified_navmesh"] = HasModifiedNavmesh
            };

            OSDMap prefs = new OSDMap();
            {
                OSDMap access = new OSDMap();
                {
                    access["max"] = MaxAccess;
                }
                prefs["access_prefs"] = access;
                prefs["alter_navmesh_objects"] = AlterNavmeshObjects;
                prefs["alter_permanent_objects"] = AlterPermanentObjects;
                prefs["god_level"] = GodLevel;
                prefs["language"] = Language;
                prefs["language_is_public"] = LanguageIsPublic;
            }
            RawData["preferences"] = prefs;
            return RawData;
        }

    }

    /// <summary>Base class for Asset uploads/results via Capabilities</summary>
    public abstract class AssetUploaderBlock
    {
        /// <summary>
        /// The request state
        /// </summary>
        public string State;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public abstract OSDMap Serialize();

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public abstract void Deserialize(OSDMap map);
    }

    /// <summary>
    /// A message sent from the viewer to the simulator to request a temporary upload capability
    /// which allows an asset to be uploaded
    /// </summary>
    public class UploaderRequestUpload : AssetUploaderBlock
    {
        /// <summary>The Capability URL sent by the simulator to upload the baked texture to</summary>
        public Uri Url;

        public UploaderRequestUpload()
        {
            State = "upload";
        }

        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["state"] = OSD.FromString(State),
                ["uploader"] = OSD.FromUri(Url)
            };

            return map;
        }

        public override void Deserialize(OSDMap map)
        {
            Url = map["uploader"].AsUri();
            State = map["state"].AsString();
        }
    }

    /// <summary>
    /// A message sent from the simulator that will inform the agent the upload is complete, 
    /// and the UUID of the uploaded asset
    /// </summary>
    public class UploaderRequestComplete : AssetUploaderBlock
    {
        /// <summary>The uploaded texture asset ID</summary>
        public UUID AssetID;

        public UploaderRequestComplete()
        {
            State = "complete";
        }

        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["state"] = OSD.FromString(State),
                ["new_asset"] = OSD.FromUUID(AssetID)
            };

            return map;
        }

        public override void Deserialize(OSDMap map)
        {
            AssetID = map["new_asset"].AsUUID();
            State = map["state"].AsString();
        }
    }

    /// <summary>
    /// A message sent from the viewer to the simulator to request a temporary
    /// capability URI which is used to upload an agents baked appearance textures
    /// </summary>
    public class UploadBakedTextureMessage : IMessage
    {
        /// <summary>Object containing request or response</summary>
        public AssetUploaderBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("state") && map["state"].AsString().Equals("upload"))
                Request = new UploaderRequestUpload();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("complete"))
                Request = new UploaderRequestComplete();
            else
                Logger.Log("Unable to deserialize UploadBakedTexture: No message handler exists for state " + map["state"].AsString(), Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }
    #endregion

    #region Voice Messages
    /// <summary>
    /// A message sent from the simulator which indicates the minimum version required for 
    /// using voice chat
    /// </summary>
    public class RequiredVoiceVersionMessage : IMessage
    {
        /// <summary>Major Version Required</summary>
        public int MajorVersion;
        /// <summary>Minor version required</summary>
        public int MinorVersion;
        /// <summary>The name of the region sending the version requrements</summary>
        public string RegionName;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(4)
            {
                ["major_version"] = OSD.FromInteger(MajorVersion),
                ["minor_version"] = OSD.FromInteger(MinorVersion),
                ["region_name"] = OSD.FromString(RegionName)
            };

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            MajorVersion = map["major_version"].AsInteger();
            MinorVersion = map["minor_version"].AsInteger();
            RegionName = map["region_name"].AsString();
        }
    }

    /// <summary>
    /// A message sent from the simulator to the viewer containing the 
    /// voice server URI
    /// </summary>
    public class ParcelVoiceInfoRequestMessage : IMessage
    {
        /// <summary>The Parcel ID which the voice server URI applies</summary>
        public int ParcelID;
        /// <summary>The name of the region</summary>
        public string RegionName;
        /// <summary>A uri containing the server/channel information
        /// which the viewer can utilize to participate in voice conversations</summary>
        public Uri SipChannelUri;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3)
            {
                ["parcel_local_id"] = OSD.FromInteger(ParcelID),
                ["region_name"] = OSD.FromString(RegionName)
            };

            OSDMap vcMap = new OSDMap(1) {["channel_uri"] = OSD.FromUri(SipChannelUri)};

            map["voice_credentials"] = vcMap;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            ParcelID = map["parcel_local_id"].AsInteger();
            RegionName = map["region_name"].AsString();

            OSDMap vcMap = (OSDMap)map["voice_credentials"];
            SipChannelUri = vcMap["channel_uri"].AsUri();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ProvisionVoiceAccountRequestMessage : IMessage
    {
        /// <summary></summary>
        public string Password;
        /// <summary></summary>
        public string Username;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["username"] = OSD.FromString(Username),
                ["password"] = OSD.FromString(Password)
            };


            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            Username = map["username"].AsString();
            Password = map["password"].AsString();
        }
    }

    #endregion

    #region Script/Notecards Messages
    /// <summary>
    /// A message sent by the viewer to the simulator to request a temporary
    /// capability for a script contained with in a Tasks inventory to be updated
    /// </summary>
    public class UploadScriptTaskMessage : IMessage
    {
        /// <summary>Object containing request or response</summary>
        public AssetUploaderBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("state") && map["state"].Equals("upload"))
                Request = new UploaderRequestUpload();
            else if (map.ContainsKey("state") && map["state"].Equals("complete"))
                Request = new UploaderRequestComplete();
            else
                Logger.Log("Unable to deserialize UploadScriptTask: No message handler exists for state " + map["state"].AsString(), Helpers.LogLevel.Warning);

            Request.Deserialize(map);
        }
    }

    /// <summary>
    /// A message sent from the simulator to the viewer to indicate
    /// a Tasks scripts status.
    /// </summary>
    public class ScriptRunningReplyMessage : IMessage
    {
        /// <summary>The Asset ID of the script</summary>
        public UUID ItemID;
        /// <summary>True of the script is compiled/ran using the mono interpreter, false indicates it 
        /// uses the older less efficient lsl2 interprter</summary>
        public bool Mono;
        /// <summary>The Task containing the scripts <seealso cref="UUID"/></summary>
        public UUID ObjectID;
        /// <summary>true of the script is in a running state</summary>
        public bool Running;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2);

            OSDMap scriptMap = new OSDMap(4)
            {
                ["ItemID"] = OSD.FromUUID(ItemID),
                ["Mono"] = OSD.FromBoolean(Mono),
                ["ObjectID"] = OSD.FromUUID(ObjectID),
                ["Running"] = OSD.FromBoolean(Running)
            };

            OSDArray scriptArray = new OSDArray(1) {(OSD) scriptMap};

            map["Script"] = scriptArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray scriptArray = (OSDArray)map["Script"];

            OSDMap scriptMap = (OSDMap)scriptArray[0];

            ItemID = scriptMap["ItemID"].AsUUID();
            Mono = scriptMap["Mono"].AsBoolean();
            ObjectID = scriptMap["ObjectID"].AsUUID();
            Running = scriptMap["Running"].AsBoolean();
        }
    }

    /// <summary>
    /// A message containing the request/response used for updating a gesture
    /// contained with an agents inventory
    /// </summary>
    public class UpdateGestureAgentInventoryMessage : IMessage
    {
        /// <summary>Object containing request or response</summary>
        public AssetUploaderBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("item_id"))
                Request = new UpdateAgentInventoryRequestMessage();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("upload"))
                Request = new UploaderRequestUpload();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("complete"))
                Request = new UploaderRequestComplete();
            else
                Logger.Log("Unable to deserialize UpdateGestureAgentInventory: No message handler exists: " + map.AsString(), Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }

    /// <summary>
    /// A message request/response which is used to update a notecard contained within
    /// a tasks inventory
    /// </summary>
    public class UpdateNotecardTaskInventoryMessage : IMessage
    {
        /// <summary>The <seealso cref="UUID"/> of the Task containing the notecard asset to update</summary>
        public UUID TaskID;
        /// <summary>The notecard assets <seealso cref="UUID"/> contained in the tasks inventory</summary>
        public UUID ItemID;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1)
            {
                ["task_id"] = OSD.FromUUID(TaskID),
                ["item_id"] = OSD.FromUUID(ItemID)
            };

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            TaskID = map["task_id"].AsUUID();
            ItemID = map["item_id"].AsUUID();
        }
    }

    // TODO: Add Test
    /// <summary>
    /// A reusable class containing a message sent from the viewer to the simulator to request a temporary uploader capability
    /// which is used to update an asset in an agents inventory
    /// </summary>
    public class UpdateAgentInventoryRequestMessage : AssetUploaderBlock
    {
        /// <summary>
        /// The Notecard AssetID to replace
        /// </summary>
        public UUID ItemID;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1) {["item_id"] = OSD.FromUUID(ItemID)};

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            ItemID = map["item_id"].AsUUID();
        }
    }

    /// <summary>
    /// A message containing the request/response used for updating a notecard
    /// contained with an agents inventory
    /// </summary>
    public class UpdateNotecardAgentInventoryMessage : IMessage
    {
        /// <summary>Object containing request or response</summary>
        public AssetUploaderBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("item_id"))
                Request = new UpdateAgentInventoryRequestMessage();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("upload"))
                Request = new UploaderRequestUpload();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("complete"))
                Request = new UploaderRequestComplete();
            else
                Logger.Log("Unable to deserialize UpdateNotecardAgentInventory: No message handler exists for state " + map["state"].AsString(), Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }

    public class CopyInventoryFromNotecardMessage : IMessage
    {
        public int CallbackID;
        public UUID FolderID;
        public UUID ItemID;
        public UUID NotecardID;
        public UUID ObjectID;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(5)
            {
                ["callback-id"] = OSD.FromInteger(CallbackID),
                ["folder-id"] = OSD.FromUUID(FolderID),
                ["item-id"] = OSD.FromUUID(ItemID),
                ["notecard-id"] = OSD.FromUUID(NotecardID),
                ["object-id"] = OSD.FromUUID(ObjectID)
            };

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            CallbackID = map["callback-id"].AsInteger();
            FolderID = map["folder-id"].AsUUID();
            ItemID = map["item-id"].AsUUID();
            NotecardID = map["notecard-id"].AsUUID();
            ObjectID = map["object-id"].AsUUID();
        }
    }

    /// <summary>
    /// A message sent from the simulator to the viewer which indicates
    /// an error occurred while attempting to update a script in an agents or tasks 
    /// inventory
    /// </summary>
    public class UploaderScriptRequestError : AssetUploaderBlock
    {
        /// <summary>true of the script was successfully compiled by the simulator</summary>
        public bool Compiled;
        /// <summary>A string containing the error which occured while trying
        /// to update the script</summary>
        public string Error;
        /// <summary>A new AssetID assigned to the script</summary>
        public UUID AssetID;

        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(4)
            {
                ["state"] = OSD.FromString(State),
                ["new_asset"] = OSD.FromUUID(AssetID),
                ["compiled"] = OSD.FromBoolean(Compiled)
            };

            OSDArray errorsArray = new OSDArray {Error};
            map["errors"] = errorsArray;
            return map;
        }

        public override void Deserialize(OSDMap map)
        {
            AssetID = map["new_asset"].AsUUID();
            Compiled = map["compiled"].AsBoolean();
            State = map["state"].AsString();

            OSDArray errorsArray = (OSDArray)map["errors"];
            Error = errorsArray[0].AsString();
        }
    }

    /// <summary>
    /// A message sent from the viewer to the simulator
    /// requesting the update of an existing script contained
    /// within a tasks inventory
    /// </summary>
    public class UpdateScriptTaskUpdateMessage : AssetUploaderBlock
    {
        /// <summary>if true, set the script mode to running</summary>
        public bool ScriptRunning;
        /// <summary>The scripts InventoryItem ItemID to update</summary>
        public UUID ItemID;
        /// <summary>A lowercase string containing either "mono" or "lsl2" which 
        /// specifies the script is compiled and ran on the mono runtime, or the older
        /// lsl runtime</summary>
        public string Target; // mono or lsl2
        /// <summary>The tasks <see cref="UUID"/> which contains the script to update</summary>
        public UUID TaskID;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(4)
            {
                ["is_script_running"] = OSD.FromBoolean(ScriptRunning),
                ["item_id"] = OSD.FromUUID(ItemID),
                ["target"] = OSD.FromString(Target),
                ["task_id"] = OSD.FromUUID(TaskID)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            ScriptRunning = map["is_script_running"].AsBoolean();
            ItemID = map["item_id"].AsUUID();
            Target = map["target"].AsString();
            TaskID = map["task_id"].AsUUID();
        }
    }

    /// <summary>
    /// A message containing either the request or response used in updating a script inside
    /// a tasks inventory
    /// </summary>
    public class UpdateScriptTaskMessage : IMessage
    {
        /// <summary>Object containing request or response</summary>
        public AssetUploaderBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("task_id"))
                Request = new UpdateScriptTaskUpdateMessage();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("upload"))
                Request = new UploaderRequestUpload();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("complete")
                && map.ContainsKey("errors"))
                Request = new UploaderScriptRequestError();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("complete"))
                Request = new UploaderRequestScriptComplete();
            else
                Logger.Log("Unable to deserialize UpdateScriptTaskMessage: No message handler exists for state " + map["state"].AsString(), Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }

    /// <summary>
    /// Response from the simulator to notify the viewer the upload is completed, and
    /// the UUID of the script asset and its compiled status
    /// </summary>
    public class UploaderRequestScriptComplete : AssetUploaderBlock
    {
        /// <summary>The uploaded texture asset ID</summary>
        public UUID AssetID;
        /// <summary>true of the script was compiled successfully</summary>
        public bool Compiled;

        public UploaderRequestScriptComplete()
        {
            State = "complete";
        }

        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["state"] = OSD.FromString(State),
                ["new_asset"] = OSD.FromUUID(AssetID),
                ["compiled"] = OSD.FromBoolean(Compiled)
            };
            return map;
        }

        public override void Deserialize(OSDMap map)
        {
            AssetID = map["new_asset"].AsUUID();
            Compiled = map["compiled"].AsBoolean();
        }
    }

    /// <summary>
    /// A message sent from a viewer to the simulator requesting a temporary uploader capability
    /// used to update a script contained in an agents inventory
    /// </summary>
    public class UpdateScriptAgentRequestMessage : AssetUploaderBlock
    {
        /// <summary>The existing asset if of the script in the agents inventory to replace</summary>
        public UUID ItemID;
        /// <summary>The language of the script</summary>
        /// <remarks>Defaults to lsl version 2, "mono" might be another possible option</remarks>
        public string Target = "lsl2"; // lsl2

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["item_id"] = OSD.FromUUID(ItemID),
                ["target"] = OSD.FromString(Target)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            ItemID = map["item_id"].AsUUID();
            Target = map["target"].AsString();
        }
    }

    /// <summary>
    /// A message containing either the request or response used in updating a script inside
    /// an agents inventory
    /// </summary>
    public class UpdateScriptAgentMessage : IMessage
    {
        /// <summary>Object containing request or response</summary>
        public AssetUploaderBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("item_id"))
                Request = new UpdateScriptAgentRequestMessage();
            else if (map.ContainsKey("errors"))
                Request = new UploaderScriptRequestError();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("upload"))
                Request = new UploaderRequestUpload();
            else if (map.ContainsKey("state") && map["state"].AsString().Equals("complete"))
                Request = new UploaderRequestScriptComplete();
            else
                Logger.Log("Unable to deserialize UpdateScriptAgent: No message handler exists for state " + map["state"].AsString(), Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }


    public class SendPostcardMessage : IMessage
    {
        public string FromEmail;
        public string Message;
        public string FromName;
        public Vector3 GlobalPosition;
        public string Subject;
        public string ToEmail;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(6)
            {
                ["from"] = OSD.FromString(FromEmail),
                ["msg"] = OSD.FromString(Message),
                ["name"] = OSD.FromString(FromName),
                ["pos-global"] = OSD.FromVector3(GlobalPosition),
                ["subject"] = OSD.FromString(Subject),
                ["to"] = OSD.FromString(ToEmail)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            FromEmail = map["from"].AsString();
            Message = map["msg"].AsString();
            FromName = map["name"].AsString();
            GlobalPosition = map["pos-global"].AsVector3();
            Subject = map["subject"].AsString();
            ToEmail = map["to"].AsString();
        }
    }

    #endregion

    #region Grid/Maps

    /// <summary>Base class for Map Layers via Capabilities</summary>
    public abstract class MapLayerMessageBase
    {
        /// <summary></summary>
        public int Flags;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public abstract OSDMap Serialize();

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public abstract void Deserialize(OSDMap map);
    }

    /// <summary>
    /// Sent by an agent to the capabilities server to request map layers
    /// </summary>
    public class MapLayerRequestVariant : MapLayerMessageBase
    {
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1) {["Flags"] = OSD.FromInteger(Flags)};
            return map;
        }

        public override void Deserialize(OSDMap map)
        {
            Flags = map["Flags"].AsInteger();
        }
    }

    /// <summary>
    /// A message sent from the simulator to the viewer which contains an array of map images and their grid coordinates
    /// </summary>
    public class MapLayerReplyVariant : MapLayerMessageBase
    {
        /// <summary>
        /// An object containing map location details
        /// </summary>
        public class LayerData
        {
            /// <summary>The Asset ID of the regions tile overlay</summary>
            public UUID ImageID;
            /// <summary>The grid location of the southern border of the map tile</summary>
            public int Bottom;
            /// <summary>The grid location of the western border of the map tile</summary>
            public int Left;
            /// <summary>The grid location of the eastern border of the map tile</summary>
            public int Right;
            /// <summary>The grid location of the northern border of the map tile</summary>
            public int Top;
        }

        /// <summary>An array containing LayerData items</summary>
        public LayerData[] LayerDataBlocks;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2);
            OSDMap agentMap = new OSDMap(1) {["Flags"] = OSD.FromInteger(Flags)};
            map["AgentData"] = agentMap;

            OSDArray layerArray = new OSDArray(LayerDataBlocks.Length);

            foreach (LayerData t in LayerDataBlocks)
            {
                OSDMap layerMap = new OSDMap(5)
                {
                    ["ImageID"] = OSD.FromUUID(t.ImageID),
                    ["Bottom"] = OSD.FromInteger(t.Bottom),
                    ["Left"] = OSD.FromInteger(t.Left),
                    ["Top"] = OSD.FromInteger(t.Top),
                    ["Right"] = OSD.FromInteger(t.Right)
                };

                layerArray.Add(layerMap);
            }

            map["LayerData"] = layerArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            OSDMap agentMap = (OSDMap)map["AgentData"];
            Flags = agentMap["Flags"].AsInteger();

            OSDArray layerArray = (OSDArray)map["LayerData"];

            LayerDataBlocks = new LayerData[layerArray.Count];

            for (int i = 0; i < LayerDataBlocks.Length; i++)
            {
                OSDMap layerMap = (OSDMap)layerArray[i];

                LayerData layer = new LayerData
                {
                    ImageID = layerMap["ImageID"].AsUUID(),
                    Top = layerMap["Top"].AsInteger(),
                    Right = layerMap["Right"].AsInteger(),
                    Left = layerMap["Left"].AsInteger(),
                    Bottom = layerMap["Bottom"].AsInteger()
                };

                LayerDataBlocks[i] = layer;
            }
        }
    }

    public class MapLayerMessage : IMessage
    {
        /// <summary>Object containing request or response</summary>
        public MapLayerMessageBase Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("LayerData"))
                Request = new MapLayerReplyVariant();
            else if (map.ContainsKey("Flags"))
                Request = new MapLayerRequestVariant();
            else
                Logger.Log("Unable to deserialize MapLayerMessage: No message handler exists", Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }

    #endregion

    #region Session/Communication

    /// <summary>
    /// New as of 1.23 RC1, no details yet.
    /// </summary>
    public class ProductInfoRequestMessage : IMessage
    {
        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            throw new NotImplementedException();
        }
    }

    #region ChatSessionRequestMessage


    public abstract class SearchStatRequestBlock
    {
        public abstract OSDMap Serialize();
        public abstract void Deserialize(OSDMap map);
    }

    // variant A - the request to the simulator
    public class SearchStatRequestRequest : SearchStatRequestBlock
    {
        public UUID ClassifiedID;

        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1);
            map["classified_id"] = OSD.FromUUID(ClassifiedID);
            return map;
        }

        public override void Deserialize(OSDMap map)
        {
            ClassifiedID = map["classified_id"].AsUUID();
        }
    }

    public class SearchStatRequestReply : SearchStatRequestBlock
    {
        public int MapClicks;
        public int ProfileClicks;
        public int SearchMapClicks;
        public int SearchProfileClicks;
        public int SearchTeleportClicks;
        public int TeleportClicks;

        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(6);
            map["map_clicks"] = OSD.FromInteger(MapClicks);
            map["profile_clicks"] = OSD.FromInteger(ProfileClicks);
            map["search_map_clicks"] = OSD.FromInteger(SearchMapClicks);
            map["search_profile_clicks"] = OSD.FromInteger(SearchProfileClicks);
            map["search_teleport_clicks"] = OSD.FromInteger(SearchTeleportClicks);
            map["teleport_clicks"] = OSD.FromInteger(TeleportClicks);
            return map;
        }

        public override void Deserialize(OSDMap map)
        {
            MapClicks = map["map_clicks"].AsInteger();
            ProfileClicks = map["profile_clicks"].AsInteger();
            SearchMapClicks = map["search_map_clicks"].AsInteger();
            SearchProfileClicks = map["search_profile_clicks"].AsInteger();
            SearchTeleportClicks = map["search_teleport_clicks"].AsInteger();
            TeleportClicks = map["teleport_clicks"].AsInteger();
        }
    }

    public class SearchStatRequestMessage : IMessage
    {
        public SearchStatRequestBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("map_clicks"))
                Request = new SearchStatRequestReply();
            else if (map.ContainsKey("classified_id"))
                Request = new SearchStatRequestRequest();
            else
                Logger.Log("Unable to deserialize SearchStatRequest: No message handler exists for method " + map["method"].AsString(), Helpers.LogLevel.Warning);

            Request.Deserialize(map);
        }
    }

    public abstract class ChatSessionRequestBlock
    {
        /// <summary>A string containing the method used</summary>
        public string Method;

        public abstract OSDMap Serialize();
        public abstract void Deserialize(OSDMap map);
    }

    /// <summary>
    /// A request sent from an agent to the Simulator to begin a new conference.
    /// Contains a list of Agents which will be included in the conference
    /// </summary>    
    public class ChatSessionRequestStartConference : ChatSessionRequestBlock
    {
        /// <summary>An array containing the <see cref="UUID"/> of the agents invited to this conference</summary>
        public UUID[] AgentsBlock;
        /// <summary>The conferences Session ID</summary>
        public UUID SessionID;

        public ChatSessionRequestStartConference()
        {
            Method = "start conference";
        }

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3)
            {
                ["method"] = OSD.FromString(Method)
            };
            OSDArray agentsArray = new OSDArray();
            foreach (var uuid in AgentsBlock)
            {
                agentsArray.Add(OSD.FromUUID(uuid));
            }
            map["params"] = agentsArray;
            map["session-id"] = OSD.FromUUID(SessionID);

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            Method = map["method"].AsString();
            OSDArray agentsArray = (OSDArray)map["params"];

            AgentsBlock = new UUID[agentsArray.Count];

            for (int i = 0; i < agentsArray.Count; i++)
            {
                AgentsBlock[i] = agentsArray[i].AsUUID();
            }

            SessionID = map["session-id"].AsUUID();
        }
    }

    /// <summary>
    /// A moderation request sent from a conference moderator
    /// Contains an agent and an optional action to take
    /// </summary>    
    public class ChatSessionRequestMuteUpdate : ChatSessionRequestBlock
    {
        /// <summary>The Session ID</summary>
        public UUID SessionID;
        /// <summary></summary>
        public UUID AgentID;
        /// <summary>A list containing Key/Value pairs, known valid values:
        /// key: text value: true/false - allow/disallow specified agents ability to use text in session
        /// key: voice value: true/false - allow/disallow specified agents ability to use voice in session
        /// </summary>
        /// <remarks>"text" or "voice"</remarks>
        public string RequestKey;
        /// <summary></summary>
        public bool RequestValue;

        public ChatSessionRequestMuteUpdate()
        {
            Method = "mute update";
        }

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3);
            map["method"] = OSD.FromString(Method);

            OSDMap muteMap = new OSDMap(1);
            muteMap[RequestKey] = OSD.FromBoolean(RequestValue);

            OSDMap paramMap = new OSDMap(2);
            paramMap["agent_id"] = OSD.FromUUID(AgentID);
            paramMap["mute_info"] = muteMap;

            map["params"] = paramMap;
            map["session-id"] = OSD.FromUUID(SessionID);

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            Method = map["method"].AsString();
            SessionID = map["session-id"].AsUUID();

            OSDMap paramsMap = (OSDMap)map["params"];
            OSDMap muteMap = (OSDMap)paramsMap["mute_info"];

            AgentID = paramsMap["agent_id"].AsUUID();

            if (muteMap.ContainsKey("text"))
                RequestKey = "text";
            else if (muteMap.ContainsKey("voice"))
                RequestKey = "voice";

            RequestValue = muteMap[RequestKey].AsBoolean();
        }
    }

    /// <summary>
    /// A message sent from the agent to the simulator which tells the 
    /// simulator we've accepted a conference invitation
    /// </summary>
    public class ChatSessionAcceptInvitation : ChatSessionRequestBlock
    {
        /// <summary>The conference SessionID</summary>
        public UUID SessionID;

        public ChatSessionAcceptInvitation()
        {
            Method = "accept invitation";
        }

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2);
            map["method"] = OSD.FromString(Method);
            map["session-id"] = OSD.FromUUID(SessionID);
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            Method = map["method"].AsString();
            SessionID = map["session-id"].AsUUID();
        }
    }

    public class ChatSessionRequestMessage : IMessage
    {
        public ChatSessionRequestBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("method") && map["method"].AsString().Equals("start conference"))
                Request = new ChatSessionRequestStartConference();
            else if (map.ContainsKey("method") && map["method"].AsString().Equals("mute update"))
                Request = new ChatSessionRequestMuteUpdate();
            else if (map.ContainsKey("method") && map["method"].AsString().Equals("accept invitation"))
                Request = new ChatSessionAcceptInvitation();
            else
                Logger.Log("Unable to deserialize ChatSessionRequest: No message handler exists for method " + map["method"].AsString(), Helpers.LogLevel.Warning);

            Request.Deserialize(map);
        }
    }

    #endregion

    public class ChatterboxSessionEventReplyMessage : IMessage
    {
        public UUID SessionID;
        public bool Success;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["success"] = OSD.FromBoolean(Success),
                ["session_id"] = OSD.FromUUID(SessionID)
            };
            // FIXME: Verify this is correct map name

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            Success = map["success"].AsBoolean();
            SessionID = map["session_id"].AsUUID();
        }
    }

    public class ChatterBoxSessionStartReplyMessage : IMessage
    {
        public UUID SessionID;
        public UUID TempSessionID;
        public bool Success;

        public string SessionName;
        // FIXME: Replace int with an enum
        public int Type;
        public bool VoiceEnabled;
        public bool ModeratedVoice;

        /* Is Text moderation possible? */

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap moderatedMap = new OSDMap(1) {["voice"] = OSD.FromBoolean(ModeratedVoice)};

            OSDMap sessionMap = new OSDMap(4)
            {
                ["type"] = OSD.FromInteger(Type),
                ["session_name"] = OSD.FromString(SessionName),
                ["voice_enabled"] = OSD.FromBoolean(VoiceEnabled),
                ["moderated_mode"] = moderatedMap
            };

            OSDMap map = new OSDMap(4)
            {
                ["session_id"] = OSD.FromUUID(SessionID),
                ["temp_session_id"] = OSD.FromUUID(TempSessionID),
                ["success"] = OSD.FromBoolean(Success),
                ["session_info"] = sessionMap
            };

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            SessionID = map["session_id"].AsUUID();
            TempSessionID = map["temp_session_id"].AsUUID();
            Success = map["success"].AsBoolean();

            if (!Success) return;
            OSDMap sessionMap = (OSDMap)map["session_info"];
            SessionName = sessionMap["session_name"].AsString();
            Type = sessionMap["type"].AsInteger();
            VoiceEnabled = sessionMap["voice_enabled"].AsBoolean();

            OSDMap moderatedModeMap = (OSDMap)sessionMap["moderated_mode"];
            ModeratedVoice = moderatedModeMap["voice"].AsBoolean();
        }
    }

    public class ChatterBoxInvitationMessage : IMessage
    {
        /// <summary>Key of sender</summary>
        public UUID FromAgentID;
        /// <summary>Name of sender</summary>
        public string FromAgentName;
        /// <summary>Key of destination avatar</summary>
        public UUID ToAgentID;
        /// <summary>ID of originating estate</summary>
        public uint ParentEstateID;
        /// <summary>Key of originating region</summary>
        public UUID RegionID;
        /// <summary>Coordinates in originating region</summary>
        public Vector3 Position;
        /// <summary>Instant message type</summary>
        public InstantMessageDialog Dialog;
        /// <summary>Group IM session toggle</summary>
        public bool GroupIM;
        /// <summary>Key of IM session, for Group Messages, the groups UUID</summary>
        public UUID IMSessionID;
        /// <summary>Timestamp of the instant message</summary>
        public DateTime Timestamp;
        /// <summary>Instant message text</summary>
        public string Message;
        /// <summary>Whether this message is held for offline avatars</summary>
        public InstantMessageOnline Offline;
        /// <summary>Context specific packed data</summary>
        public byte[] BinaryBucket;
        /// <summary>Is this invitation for voice group/conference chat</summary>
        public bool Voice;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap dataMap = new OSDMap(3)
            {
                ["timestamp"] = OSD.FromDate(Timestamp),
                ["type"] = OSD.FromInteger((uint) Dialog),
                ["binary_bucket"] = OSD.FromBinary(BinaryBucket)
            };

            OSDMap paramsMap = new OSDMap(11)
            {
                ["from_id"] = OSD.FromUUID(FromAgentID),
                ["from_name"] = OSD.FromString(FromAgentName),
                ["to_id"] = OSD.FromUUID(ToAgentID),
                ["parent_estate_id"] = OSD.FromInteger(ParentEstateID),
                ["region_id"] = OSD.FromUUID(RegionID),
                ["position"] = OSD.FromVector3(Position),
                ["from_group"] = OSD.FromBoolean(GroupIM),
                ["id"] = OSD.FromUUID(IMSessionID),
                ["message"] = OSD.FromString(Message),
                ["offline"] = OSD.FromInteger((uint) Offline),
                ["data"] = dataMap
            };
            OSDMap imMap = new OSDMap(1) {["message_params"] = paramsMap};
            OSDMap map = new OSDMap(1) {["instantmessage"] = imMap};
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("voice"))
            {
                FromAgentID = map["from_id"].AsUUID();
                FromAgentName = map["from_name"].AsString();
                IMSessionID = map["session_id"].AsUUID();
                BinaryBucket = Utils.StringToBytes(map["session_name"].AsString());
                Voice = true;
            }
            else
            {
                OSDMap im = (OSDMap)map["instantmessage"];
                OSDMap msg = (OSDMap)im["message_params"];
                OSDMap msgdata = (OSDMap)msg["data"];

                FromAgentID = msg["from_id"].AsUUID();
                FromAgentName = msg["from_name"].AsString();
                ToAgentID = msg["to_id"].AsUUID();
                ParentEstateID = (uint)msg["parent_estate_id"].AsInteger();
                RegionID = msg["region_id"].AsUUID();
                Position = msg["position"].AsVector3();
                GroupIM = msg["from_group"].AsBoolean();
                IMSessionID = msg["id"].AsUUID();
                Message = msg["message"].AsString();
                Offline = (InstantMessageOnline)msg["offline"].AsInteger();
                Dialog = (InstantMessageDialog)msgdata["type"].AsInteger();
                BinaryBucket = msgdata["binary_bucket"].AsBinary();
                Timestamp = msgdata["timestamp"].AsDate();
                Voice = false;
            }
        }
    }

    public class RegionInfoMessage : IMessage
    {
        public int ParcelLocalID;
        public string RegionName;
        public string ChannelUri;

        #region IMessage Members

        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3)
            {
                ["parcel_local_id"] = OSD.FromInteger(ParcelLocalID),
                ["region_name"] = OSD.FromString(RegionName)
            };
            OSDMap voiceMap = new OSDMap(1) {["channel_uri"] = OSD.FromString(ChannelUri)};
            map["voice_credentials"] = voiceMap;
            return map;
        }

        public void Deserialize(OSDMap map)
        {
            this.ParcelLocalID = map["parcel_local_id"].AsInteger();
            this.RegionName = map["region_name"].AsString();
            OSDMap voiceMap = (OSDMap)map["voice_credentials"];
            this.ChannelUri = voiceMap["channel_uri"].AsString();
        }

        #endregion
    }

    /// <summary>
    /// Sent from the simulator to the viewer.
    /// 
    /// When an agent initially joins a session the AgentUpdatesBlock object will contain a list of session members including
    /// a boolean indicating they can use voice chat in this session, a boolean indicating they are allowed to moderate 
    /// this session, and lastly a string which indicates another agent is entering the session with the Transition set to "ENTER"
    /// 
    /// During the session lifetime updates on individuals are sent. During the update the booleans sent during the initial join are
    /// excluded with the exception of the Transition field. This indicates a new user entering or exiting the session with
    /// the string "ENTER" or "LEAVE" respectively.
    /// </summary>
    public class ChatterBoxSessionAgentListUpdatesMessage : IMessage
    {
        // initial when agent joins session
        // <llsd><map><key>events</key><array><map><key>body</key><map><key>agent_updates</key><map><key>32939971-a520-4b52-8ca5-6085d0e39933</key><map><key>info</key><map><key>can_voice_chat</key><boolean>1</boolean><key>is_moderator</key><boolean>1</boolean></map><key>transition</key><string>ENTER</string></map><key>ca00e3e1-0fdb-4136-8ed4-0aab739b29e8</key><map><key>info</key><map><key>can_voice_chat</key><boolean>1</boolean><key>is_moderator</key><boolean>0</boolean></map><key>transition</key><string>ENTER</string></map></map><key>session_id</key><string>be7a1def-bd8a-5043-5d5b-49e3805adf6b</string><key>updates</key><map><key>32939971-a520-4b52-8ca5-6085d0e39933</key><string>ENTER</string><key>ca00e3e1-0fdb-4136-8ed4-0aab739b29e8</key><string>ENTER</string></map></map><key>message</key><string>ChatterBoxSessionAgentListUpdates</string></map><map><key>body</key><map><key>agent_updates</key><map><key>32939971-a520-4b52-8ca5-6085d0e39933</key><map><key>info</key><map><key>can_voice_chat</key><boolean>1</boolean><key>is_moderator</key><boolean>1</boolean></map></map></map><key>session_id</key><string>be7a1def-bd8a-5043-5d5b-49e3805adf6b</string><key>updates</key><map /></map><key>message</key><string>ChatterBoxSessionAgentListUpdates</string></map></array><key>id</key><integer>5</integer></map></llsd>

        // a message containing only moderator updates
        // <llsd><map><key>events</key><array><map><key>body</key><map><key>agent_updates</key><map><key>ca00e3e1-0fdb-4136-8ed4-0aab739b29e8</key><map><key>info</key><map><key>mutes</key><map><key>text</key><boolean>1</boolean></map></map></map></map><key>session_id</key><string>be7a1def-bd8a-5043-5d5b-49e3805adf6b</string><key>updates</key><map /></map><key>message</key><string>ChatterBoxSessionAgentListUpdates</string></map></array><key>id</key><integer>7</integer></map></llsd>

        public UUID SessionID;

        public class AgentUpdatesBlock
        {
            public UUID AgentID;

            public bool CanVoiceChat;
            public bool IsModerator;
            // transition "transition" = "ENTER" or "LEAVE"
            public string Transition;   //  TODO: switch to an enum "ENTER" or "LEAVE"

            public bool MuteText;
            public bool MuteVoice;
        }

        public AgentUpdatesBlock[] Updates;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap();

            OSDMap agent_updatesMap = new OSDMap(1);
            foreach (AgentUpdatesBlock t in Updates)
            {
                OSDMap mutesMap = new OSDMap(2)
                {
                    ["text"] = OSD.FromBoolean(t.MuteText),
                    ["voice"] = OSD.FromBoolean(t.MuteVoice)
                };

                OSDMap infoMap = new OSDMap(4)
                {
                    ["can_voice_chat"] = OSD.FromBoolean((bool) t.CanVoiceChat),
                    ["is_moderator"] = OSD.FromBoolean((bool) t.IsModerator),
                    ["mutes"] = mutesMap
                };

                OSDMap imap = new OSDMap(1)
                {
                    ["info"] = infoMap,
                    ["transition"] = OSD.FromString(t.Transition)
                };

                agent_updatesMap.Add(t.AgentID.ToString(), imap);
            }

            map.Add("agent_updates", agent_updatesMap);

            map["session_id"] = OSD.FromUUID(SessionID);

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {

            OSDMap agent_updates = (OSDMap)map["agent_updates"];
            SessionID = map["session_id"].AsUUID();

            List<AgentUpdatesBlock> updatesList = new List<AgentUpdatesBlock>();

            foreach (KeyValuePair<string, OSD> kvp in agent_updates)
            {

                switch (kvp.Key)
                {
                    case "updates":
                        // This appears to be redundant and duplicated by the info block, more dumps will confirm this
                        /* <key>32939971-a520-4b52-8ca5-6085d0e39933</key>
                            <string>ENTER</string> */
                        break;
                    case "session_id":
                        // I am making the assumption that each osdmap will contain the information for a 
                        // single session. This is how the map appears to read however more dumps should be taken
                        // to confirm this.
                        /* <key>session_id</key>
                            <string>984f6a1e-4ceb-6366-8d5e-a18c6819c6f7</string> */
                        break;
                    default:
                        // should be the agents uuid as the key, and "info" as the datablock
                        /* <key>32939971-a520-4b52-8ca5-6085d0e39933</key>
                            <map>
                                <key>info</key>
                                    <map>
                                        <key>can_voice_chat</key>
                                            <boolean>1</boolean>
                                        <key>is_moderator</key>
                                            <boolean>1</boolean>
                                    </map>
                                <key>transition</key>
                                    <string>ENTER</string>
                            </map>*/
                        AgentUpdatesBlock block = new AgentUpdatesBlock();
                        block.AgentID = UUID.Parse(kvp.Key);

                        OSDMap infoMap = (OSDMap)agent_updates[kvp.Key];

                        OSDMap agentPermsMap = (OSDMap)infoMap["info"];

                        block.CanVoiceChat = agentPermsMap["can_voice_chat"].AsBoolean();
                        block.IsModerator = agentPermsMap["is_moderator"].AsBoolean();

                        block.Transition = infoMap["transition"].AsString();

                        if (agentPermsMap.ContainsKey("mutes"))
                        {
                            OSDMap mutesMap = (OSDMap)agentPermsMap["mutes"];
                            block.MuteText = mutesMap["text"].AsBoolean();
                            block.MuteVoice = mutesMap["voice"].AsBoolean();
                        }
                        updatesList.Add(block);
                        break;
                }
            }

            Updates = new AgentUpdatesBlock[updatesList.Count];

            for (int i = 0; i < updatesList.Count; i++)
            {
                AgentUpdatesBlock block = new AgentUpdatesBlock
                {
                    AgentID = updatesList[i].AgentID,
                    CanVoiceChat = updatesList[i].CanVoiceChat,
                    IsModerator = updatesList[i].IsModerator,
                    MuteText = updatesList[i].MuteText,
                    MuteVoice = updatesList[i].MuteVoice,
                    Transition = updatesList[i].Transition
                };
                Updates[i] = block;
            }
        }
    }

    /// <summary>
    /// An EventQueue message sent when the agent is forcibly removed from a chatterbox session
    /// </summary>
    public class ForceCloseChatterBoxSessionMessage : IMessage
    {
        /// <summary>
        /// A string containing the reason the agent was removed
        /// </summary>
        public string Reason;
        /// <summary>
        /// The ChatterBoxSession's SessionID
        /// </summary>
        public UUID SessionID;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["reason"] = OSD.FromString(Reason),
                ["session_id"] = OSD.FromUUID(SessionID)
            };

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            Reason = map["reason"].AsString();
            SessionID = map["session_id"].AsUUID();
        }
    }

    #endregion

    #region EventQueue

    public abstract class EventMessageBlock
    {
        public abstract OSDMap Serialize();
        public abstract void Deserialize(OSDMap map);
    }

    public class EventQueueAck : EventMessageBlock
    {
        public int AckID;
        public bool Done;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap
            {
                ["ack"] = OSD.FromInteger(AckID),
                ["done"] = OSD.FromBoolean(Done)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            AckID = map["ack"].AsInteger();
            Done = map["done"].AsBoolean();
        }
    }

    public class EventQueueEvent : EventMessageBlock
    {
        public class QueueEvent
        {
            public IMessage EventMessage;
            public string MessageKey;
        }

        public int Sequence;
        public QueueEvent[] MessageEvents;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1);

            OSDArray eventsArray = new OSDArray();

            foreach (QueueEvent t in MessageEvents)
            {
                OSDMap eventMap = new OSDMap(2)
                {
                    ["body"] = t.EventMessage.Serialize(),
                    ["message"] = OSD.FromString(t.MessageKey)
                };
                eventsArray.Add(eventMap);
            }

            map["events"] = eventsArray;
            map["id"] = OSD.FromInteger(Sequence);

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            Sequence = map["id"].AsInteger();
            OSDArray arrayEvents = (OSDArray)map["events"];

            MessageEvents = new QueueEvent[arrayEvents.Count];

            for (int i = 0; i < arrayEvents.Count; i++)
            {
                OSDMap eventMap = (OSDMap)arrayEvents[i];
                QueueEvent ev = new QueueEvent {MessageKey = eventMap["message"].AsString()};

                ev.EventMessage = MessageUtils.DecodeEvent(ev.MessageKey, (OSDMap)eventMap["body"]);
                MessageEvents[i] = ev;
            }
        }
    }

    public class EventQueueGetMessage : IMessage
    {
        public EventMessageBlock Messages;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Messages.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("ack"))
                Messages = new EventQueueAck();
            else if (map.ContainsKey("events"))
                Messages = new EventQueueEvent();
            else
                Logger.Log("Unable to deserialize EventQueueGetMessage: No message handler exists for event", Helpers.LogLevel.Warning);

            Messages.Deserialize(map);
        }
    }

    #endregion

    #region Stats Messages

    public class ViewerStatsMessage : IMessage
    {
        public int AgentsInView;
        public float AgentFPS;
        public string AgentLanguage;
        public float AgentMemoryUsed;
        public float MetersTraveled;
        public float AgentPing;
        public int RegionsVisited;
        public float AgentRuntime;
        public float SimulatorFPS;
        public DateTime AgentStartTime;
        public string AgentVersion;

        public float object_kbytes;
        public float texture_kbytes;
        public float world_kbytes;

        public float MiscVersion;
        public bool VertexBuffersEnabled;

        public UUID SessionID;

        public int StatsDropped;
        public int StatsFailedResends;
        public int FailuresInvalid;
        public int FailuresOffCircuit;
        public int FailuresResent;
        public int FailuresSendPacket;

        public int MiscInt1;
        public int MiscInt2;
        public string MiscString1;

        public int InCompressedPackets;
        public float InKbytes;
        public float InPackets;
        public float InSavings;

        public int OutCompressedPackets;
        public float OutKbytes;
        public float OutPackets;
        public float OutSavings;

        public string SystemCPU;
        public string SystemGPU;
        public int SystemGPUClass;
        public string SystemGPUVendor;
        public string SystemGPUVersion;
        public string SystemOS;
        public int SystemInstalledRam;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(5) {["session_id"] = OSD.FromUUID(SessionID)};

            OSDMap agentMap = new OSDMap(11)
            {
                ["agents_in_view"] = OSD.FromInteger(AgentsInView),
                ["fps"] = OSD.FromReal(AgentFPS),
                ["language"] = OSD.FromString(AgentLanguage),
                ["mem_use"] = OSD.FromReal(AgentMemoryUsed),
                ["meters_traveled"] = OSD.FromReal(MetersTraveled),
                ["ping"] = OSD.FromReal(AgentPing),
                ["regions_visited"] = OSD.FromInteger(RegionsVisited),
                ["run_time"] = OSD.FromReal(AgentRuntime),
                ["sim_fps"] = OSD.FromReal(SimulatorFPS),
                ["start_time"] = OSD.FromUInteger(Utils.DateTimeToUnixTime(AgentStartTime)),
                ["version"] = OSD.FromString(AgentVersion)
            };
            map["agent"] = agentMap;


            OSDMap downloadsMap = new OSDMap(3)
            {
                ["object_kbytes"] = OSD.FromReal(object_kbytes),
                ["texture_kbytes"] = OSD.FromReal(texture_kbytes),
                ["world_kbytes"] = OSD.FromReal(world_kbytes)
            }; // downloads
            map["downloads"] = downloadsMap;

            OSDMap miscMap = new OSDMap(2)
            {
                ["Version"] = OSD.FromReal(MiscVersion),
                ["Vertex Buffers Enabled"] = OSD.FromBoolean(VertexBuffersEnabled)
            };
            map["misc"] = miscMap;

            OSDMap statsMap = new OSDMap(2);

            OSDMap failuresMap = new OSDMap(6)
            {
                ["dropped"] = OSD.FromInteger(StatsDropped),
                ["failed_resends"] = OSD.FromInteger(StatsFailedResends),
                ["invalid"] = OSD.FromInteger(FailuresInvalid),
                ["off_circuit"] = OSD.FromInteger(FailuresOffCircuit),
                ["resent"] = OSD.FromInteger(FailuresResent),
                ["send_packet"] = OSD.FromInteger(FailuresSendPacket)
            };
            statsMap["failures"] = failuresMap;

            OSDMap statsMiscMap = new OSDMap(3)
            {
                ["int_1"] = OSD.FromInteger(MiscInt1),
                ["int_2"] = OSD.FromInteger(MiscInt2),
                ["string_1"] = OSD.FromString(MiscString1)
            };
            statsMap["misc"] = statsMiscMap;

            OSDMap netMap = new OSDMap(3);

            // in
            OSDMap netInMap = new OSDMap(4)
            {
                ["compressed_packets"] = OSD.FromInteger(InCompressedPackets),
                ["kbytes"] = OSD.FromReal(InKbytes),
                ["packets"] = OSD.FromReal(InPackets),
                ["savings"] = OSD.FromReal(InSavings)
            };
            netMap["in"] = netInMap;
            // out
            OSDMap netOutMap = new OSDMap(4)
            {
                ["compressed_packets"] = OSD.FromInteger(OutCompressedPackets),
                ["kbytes"] = OSD.FromReal(OutKbytes),
                ["packets"] = OSD.FromReal(OutPackets),
                ["savings"] = OSD.FromReal(OutSavings)
            };
            netMap["out"] = netOutMap;

            statsMap["net"] = netMap;

            //system
            OSDMap systemStatsMap = new OSDMap(7)
            {
                ["cpu"] = OSD.FromString(SystemCPU),
                ["gpu"] = OSD.FromString(SystemGPU),
                ["gpu_class"] = OSD.FromInteger(SystemGPUClass),
                ["gpu_vendor"] = OSD.FromString(SystemGPUVendor),
                ["gpu_version"] = OSD.FromString(SystemGPUVersion),
                ["os"] = OSD.FromString(SystemOS),
                ["ram"] = OSD.FromInteger(SystemInstalledRam)
            };
            map["system"] = systemStatsMap;

            map["stats"] = statsMap;
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            SessionID = map["session_id"].AsUUID();

            OSDMap agentMap = (OSDMap)map["agent"];
            AgentsInView = agentMap["agents_in_view"].AsInteger();
            AgentFPS = (float)agentMap["fps"].AsReal();
            AgentLanguage = agentMap["language"].AsString();
            AgentMemoryUsed = (float)agentMap["mem_use"].AsReal();
            MetersTraveled = agentMap["meters_traveled"].AsInteger();
            AgentPing = (float)agentMap["ping"].AsReal();
            RegionsVisited = agentMap["regions_visited"].AsInteger();
            AgentRuntime = (float)agentMap["run_time"].AsReal();
            SimulatorFPS = (float)agentMap["sim_fps"].AsReal();
            AgentStartTime = Utils.UnixTimeToDateTime(agentMap["start_time"].AsUInteger());
            AgentVersion = agentMap["version"].AsString();

            OSDMap downloadsMap = (OSDMap)map["downloads"];
            object_kbytes = (float)downloadsMap["object_kbytes"].AsReal();
            texture_kbytes = (float)downloadsMap["texture_kbytes"].AsReal();
            world_kbytes = (float)downloadsMap["world_kbytes"].AsReal();

            OSDMap miscMap = (OSDMap)map["misc"];
            MiscVersion = (float)miscMap["Version"].AsReal();
            VertexBuffersEnabled = miscMap["Vertex Buffers Enabled"].AsBoolean();

            OSDMap statsMap = (OSDMap)map["stats"];
            OSDMap failuresMap = (OSDMap)statsMap["failures"];
            StatsDropped = failuresMap["dropped"].AsInteger();
            StatsFailedResends = failuresMap["failed_resends"].AsInteger();
            FailuresInvalid = failuresMap["invalid"].AsInteger();
            FailuresOffCircuit = failuresMap["off_circuit"].AsInteger();
            FailuresResent = failuresMap["resent"].AsInteger();
            FailuresSendPacket = failuresMap["send_packet"].AsInteger();

            OSDMap statsMiscMap = (OSDMap)statsMap["misc"];
            MiscInt1 = statsMiscMap["int_1"].AsInteger();
            MiscInt2 = statsMiscMap["int_2"].AsInteger();
            MiscString1 = statsMiscMap["string_1"].AsString();
            OSDMap netMap = (OSDMap)statsMap["net"];
            // in
            OSDMap netInMap = (OSDMap)netMap["in"];
            InCompressedPackets = netInMap["compressed_packets"].AsInteger();
            InKbytes = netInMap["kbytes"].AsInteger();
            InPackets = netInMap["packets"].AsInteger();
            InSavings = netInMap["savings"].AsInteger();
            // out
            OSDMap netOutMap = (OSDMap)netMap["out"];
            OutCompressedPackets = netOutMap["compressed_packets"].AsInteger();
            OutKbytes = netOutMap["kbytes"].AsInteger();
            OutPackets = netOutMap["packets"].AsInteger();
            OutSavings = netOutMap["savings"].AsInteger();

            //system
            OSDMap systemStatsMap = (OSDMap)map["system"];
            SystemCPU = systemStatsMap["cpu"].AsString();
            SystemGPU = systemStatsMap["gpu"].AsString();
            SystemGPUClass = systemStatsMap["gpu_class"].AsInteger();
            SystemGPUVendor = systemStatsMap["gpu_vendor"].AsString();
            SystemGPUVersion = systemStatsMap["gpu_version"].AsString();
            SystemOS = systemStatsMap["os"].AsString();
            SystemInstalledRam = systemStatsMap["ram"].AsInteger();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class PlacesReplyMessage : IMessage
    {
        public UUID AgentID;
        public UUID QueryID;
        public UUID TransactionID;

        public class QueryData
        {
            public int ActualArea;
            public int BillableArea;
            public string Description;
            public float Dwell;
            public int Flags;
            public float GlobalX;
            public float GlobalY;
            public float GlobalZ;
            public string Name;
            public UUID OwnerID;
            public string SimName;
            public UUID SnapShotID;
            public string ProductSku;
            public int Price;
        }

        public QueryData[] QueryDataBlocks;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3);

            // add the AgentData map
            OSDMap agentIDmap = new OSDMap(2)
            {
                ["AgentID"] = OSD.FromUUID(AgentID),
                ["QueryID"] = OSD.FromUUID(QueryID)
            };

            OSDArray agentDataArray = new OSDArray {agentIDmap};

            map["AgentData"] = agentDataArray;

            // add the QueryData map
            OSDArray dataBlocksArray = new OSDArray(QueryDataBlocks.Length);
            foreach (QueryData t in QueryDataBlocks)
            {
                OSDMap queryDataMap = new OSDMap(14)
                {
                    ["ActualArea"] = OSD.FromInteger(t.ActualArea),
                    ["BillableArea"] = OSD.FromInteger(t.BillableArea),
                    ["Desc"] = OSD.FromString(t.Description),
                    ["Dwell"] = OSD.FromReal(t.Dwell),
                    ["Flags"] = OSD.FromInteger(t.Flags),
                    ["GlobalX"] = OSD.FromReal(t.GlobalX),
                    ["GlobalY"] = OSD.FromReal(t.GlobalY),
                    ["GlobalZ"] = OSD.FromReal(t.GlobalZ),
                    ["Name"] = OSD.FromString(t.Name),
                    ["OwnerID"] = OSD.FromUUID(t.OwnerID),
                    ["Price"] = OSD.FromInteger(t.Price),
                    ["SimName"] = OSD.FromString(t.SimName),
                    ["SnapshotID"] = OSD.FromUUID(t.SnapShotID),
                    ["ProductSKU"] = OSD.FromString(t.ProductSku)
                };
                dataBlocksArray.Add(queryDataMap);
            }

            map["QueryData"] = dataBlocksArray;

            // add the TransactionData map
            OSDMap transMap = new OSDMap(1) {["TransactionID"] = OSD.FromUUID(TransactionID)};
            OSDArray transArray = new OSDArray {transMap};
            map["TransactionData"] = transArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray agentDataArray = (OSDArray)map["AgentData"];

            OSDMap agentDataMap = (OSDMap)agentDataArray[0];
            AgentID = agentDataMap["AgentID"].AsUUID();
            QueryID = agentDataMap["QueryID"].AsUUID();


            OSDArray dataBlocksArray = (OSDArray)map["QueryData"];
            QueryDataBlocks = new QueryData[dataBlocksArray.Count];
            for (int i = 0; i < dataBlocksArray.Count; i++)
            {
                OSDMap dataMap = (OSDMap)dataBlocksArray[i];
                QueryData data = new QueryData
                {
                    ActualArea = dataMap["ActualArea"].AsInteger(),
                    BillableArea = dataMap["BillableArea"].AsInteger(),
                    Description = dataMap["Desc"].AsString(),
                    Dwell = (float) dataMap["Dwell"].AsReal(),
                    Flags = dataMap["Flags"].AsInteger(),
                    GlobalX = (float) dataMap["GlobalX"].AsReal(),
                    GlobalY = (float) dataMap["GlobalY"].AsReal(),
                    GlobalZ = (float) dataMap["GlobalZ"].AsReal(),
                    Name = dataMap["Name"].AsString(),
                    OwnerID = dataMap["OwnerID"].AsUUID(),
                    Price = dataMap["Price"].AsInteger(),
                    SimName = dataMap["SimName"].AsString(),
                    SnapShotID = dataMap["SnapshotID"].AsUUID(),
                    ProductSku = dataMap["ProductSKU"].AsString()
                };
                QueryDataBlocks[i] = data;
            }

            OSDArray transactionArray = (OSDArray)map["TransactionData"];
            OSDMap transactionDataMap = (OSDMap)transactionArray[0];
            TransactionID = transactionDataMap["TransactionID"].AsUUID();
        }
    }

    public class UpdateAgentInformationMessage : IMessage
    {
        public string MaxAccess; // PG, A, or M

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1);
            OSDMap prefsMap = new OSDMap(1) {["max"] = OSD.FromString(MaxAccess)};
            map["access_prefs"] = prefsMap;
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDMap prefsMap = (OSDMap)map["access_prefs"];
            MaxAccess = prefsMap["max"].AsString();
        }
    }

    [Serializable]
    public class DirLandReplyMessage : IMessage
    {
        public UUID AgentID;
        public UUID QueryID;

        [Serializable]
        public class QueryReply
        {
            public int ActualArea;
            public bool Auction;
            public bool ForSale;
            public string Name;
            public UUID ParcelID;
            public string ProductSku;
            public int SalePrice;
        }

        public QueryReply[] QueryReplies;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3);

            OSDMap agentMap = new OSDMap(1) {["AgentID"] = OSD.FromUUID(AgentID)};
            OSDArray agentDataArray = new OSDArray(1) {agentMap};
            map["AgentData"] = agentDataArray;

            OSDMap queryMap = new OSDMap(1) {["QueryID"] = OSD.FromUUID(QueryID)};
            OSDArray queryDataArray = new OSDArray(1) {queryMap};
            map["QueryData"] = queryDataArray;

            OSDArray queryReplyArray = new OSDArray();
            foreach (QueryReply t in QueryReplies)
            {
                OSDMap queryReply = new OSDMap(100)
                {
                    ["ActualArea"] = OSD.FromInteger(t.ActualArea),
                    ["Auction"] = OSD.FromBoolean(t.Auction),
                    ["ForSale"] = OSD.FromBoolean(t.ForSale),
                    ["Name"] = OSD.FromString(t.Name),
                    ["ParcelID"] = OSD.FromUUID(t.ParcelID),
                    ["ProductSKU"] = OSD.FromString(t.ProductSku),
                    ["SalePrice"] = OSD.FromInteger(t.SalePrice)
                };

                queryReplyArray.Add(queryReply);
            }
            map["QueryReplies"] = queryReplyArray;

            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray agentDataArray = (OSDArray)map["AgentData"];
            OSDMap agentDataMap = (OSDMap)agentDataArray[0];
            AgentID = agentDataMap["AgentID"].AsUUID();

            OSDArray queryDataArray = (OSDArray)map["QueryData"];
            OSDMap queryDataMap = (OSDMap)queryDataArray[0];
            QueryID = queryDataMap["QueryID"].AsUUID();

            OSDArray queryRepliesArray = (OSDArray)map["QueryReplies"];

            QueryReplies = new QueryReply[queryRepliesArray.Count];
            for (int i = 0; i < queryRepliesArray.Count; i++)
            {
                QueryReply reply = new QueryReply();
                OSDMap replyMap = (OSDMap)queryRepliesArray[i];
                reply.ActualArea = replyMap["ActualArea"].AsInteger();
                reply.Auction = replyMap["Auction"].AsBoolean();
                reply.ForSale = replyMap["ForSale"].AsBoolean();
                reply.Name = replyMap["Name"].AsString();
                reply.ParcelID = replyMap["ParcelID"].AsUUID();
                reply.ProductSku = replyMap["ProductSKU"].AsString();
                reply.SalePrice = replyMap["SalePrice"].AsInteger();

                QueryReplies[i] = reply;
            }
        }
    }

    #endregion

    #region Object Messages

    public class UploadObjectAssetMessage : IMessage
    {
        public class Object
        {
            public class Face
            {
                public Bumpiness Bump;
                public Color4 Color;
                public bool Fullbright;
                public float Glow;
                public UUID ImageID;
                public float ImageRot;
                public int MediaFlags;
                public float OffsetS;
                public float OffsetT;
                public float ScaleS;
                public float ScaleT;

                public OSDMap Serialize()
                {
                    OSDMap map = new OSDMap
                    {
                        ["bump"] = OSD.FromInteger((int) Bump),
                        ["colors"] = OSD.FromColor4(Color),
                        ["fullbright"] = OSD.FromBoolean(Fullbright),
                        ["glow"] = OSD.FromReal(Glow),
                        ["imageid"] = OSD.FromUUID(ImageID),
                        ["imagerot"] = OSD.FromReal(ImageRot),
                        ["media_flags"] = OSD.FromInteger(MediaFlags),
                        ["offsets"] = OSD.FromReal(OffsetS),
                        ["offsett"] = OSD.FromReal(OffsetT),
                        ["scales"] = OSD.FromReal(ScaleS),
                        ["scalet"] = OSD.FromReal(ScaleT)
                    };

                    return map;
                }

                public void Deserialize(OSDMap map)
                {
                    Bump = (Bumpiness)map["bump"].AsInteger();
                    Color = map["colors"].AsColor4();
                    Fullbright = map["fullbright"].AsBoolean();
                    Glow = (float)map["glow"].AsReal();
                    ImageID = map["imageid"].AsUUID();
                    ImageRot = (float)map["imagerot"].AsReal();
                    MediaFlags = map["media_flags"].AsInteger();
                    OffsetS = (float)map["offsets"].AsReal();
                    OffsetT = (float)map["offsett"].AsReal();
                    ScaleS = (float)map["scales"].AsReal();
                    ScaleT = (float)map["scalet"].AsReal();
                }
            }

            public class ExtraParam
            {
                public ExtraParamType Type;
                public byte[] ExtraParamData;

                public OSDMap Serialize()
                {
                    OSDMap map = new OSDMap
                    {
                        ["extra_parameter"] = OSD.FromInteger((int) Type),
                        ["param_data"] = OSD.FromBinary(ExtraParamData)
                    };

                    return map;
                }

                public void Deserialize(OSDMap map)
                {
                    Type = (ExtraParamType)map["extra_parameter"].AsInteger();
                    ExtraParamData = map["param_data"].AsBinary();
                }
            }

            public Face[] Faces;
            public ExtraParam[] ExtraParams;
            public UUID GroupID;
            public Material Material;
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public float PathBegin;
            public int PathCurve;
            public float PathEnd;
            public float RadiusOffset;
            public float Revolutions;
            public float ScaleX;
            public float ScaleY;
            public float ShearX;
            public float ShearY;
            public float Skew;
            public float TaperX;
            public float TaperY;
            public float Twist;
            public float TwistBegin;
            public float ProfileBegin;
            public int ProfileCurve;
            public float ProfileEnd;
            public float ProfileHollow;
            public UUID SculptID;
            public SculptType SculptType;

            public OSDMap Serialize()
            {
                OSDMap map = new OSDMap
                {
                    ["group-id"] = OSD.FromUUID(GroupID),
                    ["material"] = OSD.FromInteger((int) Material),
                    ["name"] = OSD.FromString(Name),
                    ["pos"] = OSD.FromVector3(Position),
                    ["rotation"] = OSD.FromQuaternion(Rotation),
                    ["scale"] = OSD.FromVector3(Scale)
                };
                // Extra params
                OSDArray extraParams = new OSDArray();
                if (ExtraParams != null)
                {
                    foreach (ExtraParam t in ExtraParams)
                        extraParams.Add(t.Serialize());
                }
                map["extra_parameters"] = extraParams;

                // Faces
                OSDArray faces = new OSDArray();
                if (Faces != null)
                {
                    foreach (Face face in Faces)
                        faces.Add(face.Serialize());
                }
                map["facelist"] = faces;

                // Shape
                OSDMap shape = new OSDMap();
                OSDMap path = new OSDMap
                {
                    ["begin"] = OSD.FromReal(PathBegin),
                    ["curve"] = OSD.FromInteger(PathCurve),
                    ["end"] = OSD.FromReal(PathEnd),
                    ["radius_offset"] = OSD.FromReal(RadiusOffset),
                    ["revolutions"] = OSD.FromReal(Revolutions),
                    ["scale_x"] = OSD.FromReal(ScaleX),
                    ["scale_y"] = OSD.FromReal(ScaleY),
                    ["shear_x"] = OSD.FromReal(ShearX),
                    ["shear_y"] = OSD.FromReal(ShearY),
                    ["skew"] = OSD.FromReal(Skew),
                    ["taper_x"] = OSD.FromReal(TaperX),
                    ["taper_y"] = OSD.FromReal(TaperY),
                    ["twist"] = OSD.FromReal(Twist),
                    ["twist_begin"] = OSD.FromReal(TwistBegin)
                };
                shape["path"] = path;
                OSDMap profile = new OSDMap
                {
                    ["begin"] = OSD.FromReal(ProfileBegin),
                    ["curve"] = OSD.FromInteger(ProfileCurve),
                    ["end"] = OSD.FromReal(ProfileEnd),
                    ["hollow"] = OSD.FromReal(ProfileHollow)
                };
                shape["profile"] = profile;
                OSDMap sculpt = new OSDMap
                {
                    ["id"] = OSD.FromUUID(SculptID),
                    ["type"] = OSD.FromInteger((int) SculptType)
                };
                shape["sculpt"] = sculpt;
                map["shape"] = shape;

                return map;
            }

            public void Deserialize(OSDMap map)
            {
                GroupID = map["group-id"].AsUUID();
                Material = (Material)map["material"].AsInteger();
                Name = map["name"].AsString();
                Position = map["pos"].AsVector3();
                Rotation = map["rotation"].AsQuaternion();
                Scale = map["scale"].AsVector3();

                // Extra params
                OSDArray extraParams = map["extra_parameters"] as OSDArray;
                if (extraParams != null)
                {
                    ExtraParams = new ExtraParam[extraParams.Count];
                    for (int i = 0; i < extraParams.Count; i++)
                    {
                        ExtraParam extraParam = new ExtraParam();
                        extraParam.Deserialize(extraParams[i] as OSDMap);
                        ExtraParams[i] = extraParam;
                    }
                }
                else
                {
                    ExtraParams = new ExtraParam[0];
                }

                // Faces
                OSDArray faces = map["facelist"] as OSDArray;
                if (faces != null)
                {
                    Faces = new Face[faces.Count];
                    for (int i = 0; i < faces.Count; i++)
                    {
                        Face face = new Face();
                        face.Deserialize(faces[i] as OSDMap);
                        Faces[i] = face;
                    }
                }
                else
                {
                    Faces = new Face[0];
                }

                // Shape
                OSDMap shape = map["shape"] as OSDMap;
                OSDMap path = shape["path"] as OSDMap;
                PathBegin = (float)path["begin"].AsReal();
                PathCurve = path["curve"].AsInteger();
                PathEnd = (float)path["end"].AsReal();
                RadiusOffset = (float)path["radius_offset"].AsReal();
                Revolutions = (float)path["revolutions"].AsReal();
                ScaleX = (float)path["scale_x"].AsReal();
                ScaleY = (float)path["scale_y"].AsReal();
                ShearX = (float)path["shear_x"].AsReal();
                ShearY = (float)path["shear_y"].AsReal();
                Skew = (float)path["skew"].AsReal();
                TaperX = (float)path["taper_x"].AsReal();
                TaperY = (float)path["taper_y"].AsReal();
                Twist = (float)path["twist"].AsReal();
                TwistBegin = (float)path["twist_begin"].AsReal();

                OSDMap profile = shape["profile"] as OSDMap;
                ProfileBegin = (float)profile["begin"].AsReal();
                ProfileCurve = profile["curve"].AsInteger();
                ProfileEnd = (float)profile["end"].AsReal();
                ProfileHollow = (float)profile["hollow"].AsReal();

                OSDMap sculpt = shape["sculpt"] as OSDMap;
                if (sculpt != null)
                {
                    SculptID = sculpt["id"].AsUUID();
                    SculptType = (SculptType)sculpt["type"].AsInteger();
                }
                else
                {
                    SculptID = UUID.Zero;
                    SculptType = 0;
                }
            }
        }

        public Object[] Objects;

        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap();
            OSDArray array = new OSDArray();

            if (Objects != null)
            {
                foreach (Object obj in Objects)
                    array.Add(obj.Serialize());
            }

            map["objects"] = array;
            return map;
        }

        public void Deserialize(OSDMap map)
        {
            OSDArray array = map["objects"] as OSDArray;

            if (array != null)
            {
                Objects = new Object[array.Count];

                for (int i = 0; i < array.Count; ++i)
                {
                    Object obj = new Object();
                    OSDMap objMap = array[i] as OSDMap;

                    if (objMap != null)
                        obj.Deserialize(objMap);

                    Objects[i] = obj;
                }
            }
            else
            {
                Objects = new Object[0];
            }
        }
    }

    /// <summary>
    /// Event Queue message describing physics engine attributes of a list of objects
    /// Sim sends these when object is selected
    /// </summary>
    public class ObjectPhysicsPropertiesMessage : IMessage
    {
        /// <summary> Array with the list of physics properties</summary>
        public Primitive.PhysicsProperties[] ObjectPhysicsProperties;

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>Serialized OSD</returns>
        public OSDMap Serialize()
        {
            OSDMap ret = new OSDMap();
            OSDArray array = new OSDArray();

            foreach (Primitive.PhysicsProperties prop in ObjectPhysicsProperties)
            {
                array.Add(prop.GetOSD());
            }

            ret["ObjectData"] = array;
            return ret;
        }

        /// <summary>
        /// Deserializes the message
        /// </summary>
        /// <param name="map">Incoming data to deserialize</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray array = map["ObjectData"] as OSDArray;
            if (array != null)
            {
                ObjectPhysicsProperties = new Primitive.PhysicsProperties[array.Count];

                for (int i = 0; i < array.Count; i++)
                {
                    ObjectPhysicsProperties[i] = Primitive.PhysicsProperties.FromOSD(array[i]);
                }
            }
            else
            {
                ObjectPhysicsProperties = new Primitive.PhysicsProperties[0];
            }
        }
    }

    public class RenderMaterialsMessage : IMessage
    {
        public OSD MaterialData;

        /// <summary>
        /// Deserializes the message
        /// </summary>
        /// <param name="map">Incoming data to deserialize</param>
        public void Deserialize(OSDMap map)
        {
            try
            {
                using (MemoryStream input = new MemoryStream(map["Zipped"].AsBinary()))
                {
                    using (MemoryStream output = new MemoryStream())
                    {
                        using (ZOutputStream zout = new ZOutputStream(output))
                        {
                            byte[] buffer = new byte[2048];
                            int len;
                            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                zout.Write(buffer, 0, len);
                            }
                            zout.Flush();
                            output.Seek(0, SeekOrigin.Begin);
                            MaterialData = OSDParser.DeserializeLLSDBinary(output);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to decode RenderMaterials message:", Helpers.LogLevel.Warning, ex);
                MaterialData = new OSDMap();
            }
        }

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>Serialized OSD</returns>
        public OSDMap Serialize()
        {
            return new OSDMap();
        }
    }

    public class GetObjectCostRequest : IMessage
    {
        /// <summary> Object IDs for which to request cost information </summary>
        public UUID[] ObjectIDs;

        /// <summary>
        /// Deserializes the message
        /// </summary>
        /// <param name="map">Incoming data to deserialize</param>
        public void Deserialize(OSDMap map)
        {
            OSDArray array = map["object_ids"] as OSDArray;
            if (array != null)
            {
                ObjectIDs = new UUID[array.Count];

                for (int i = 0; i < array.Count; i++)
                {
                    ObjectIDs[i] = array[i].AsUUID();
                }
            }
            else
            {
                ObjectIDs = new UUID[0];
            }
        }

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>Serialized OSD</returns>
        public OSDMap Serialize()
        {
            OSDMap ret = new OSDMap();
            OSDArray array = new OSDArray();

            foreach (UUID id in ObjectIDs)
            {
                array.Add(OSD.FromUUID(id));
            }

            ret["object_ids"] = array;
            return ret;
        }
    }

    public class GetObjectCostMessage : IMessage
    {
        public UUID object_id;
        public double link_cost;
        public double object_cost;
        public double physics_cost;
        public double link_physics_cost;

        /// <summary>
        /// Deserializes the message
        /// </summary>
        /// <param name="map">Incoming data to deserialize</param>
        public void Deserialize(OSDMap map)
        {
            if (map.Count != 1)
                Logger.Log("GetObjectCostMessage returned values for more than one object! Function needs to be fixed for that!", Helpers.LogLevel.Error);                    

            foreach (string key in map.Keys)
            {
                UUID.TryParse(key, out object_id);
                OSDMap values = (OSDMap)map[key];

                link_cost = values["linked_set_resource_cost"].AsReal();
                object_cost = values["resource_cost"].AsReal();
                physics_cost = values["physics_cost"].AsReal();
                link_physics_cost = values["linked_set_physics_cost"].AsReal();
                // value["resource_limiting_type"].AsString();
                return;
            }
        }

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>Serialized OSD</returns>
        public OSDMap Serialize()
        {
            OSDMap values = new OSDMap(4)
            {
                {"linked_set_resource_cost", OSD.FromReal(link_cost)},
                {"resource_cost", OSD.FromReal(object_cost)},
                {"physics_cost", OSD.FromReal(physics_cost)},
                {"linked_set_physics_cost", OSD.FromReal(link_physics_cost)}
            };

            OSDMap map = new OSDMap(1) {{OSD.FromUUID(object_id), values}};
            return map;
        }

        /// <summary>
        /// Detects which class handles deserialization of this message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        /// <returns>Object capable of decoding this message</returns>
        public static IMessage GetMessageHandler(OSDMap map)
        {
            if (map == null)
            {
                return null;
            }
            else if (map.ContainsKey("object_ids"))
            {
                return new GetObjectCostRequest();
            }
            else
            {
                return new GetObjectCostMessage();
            }
        }
    }

    #endregion Object Messages

    #region Object Media Messages
    /// <summary>
    /// A message sent from the viewer to the simulator which 
    /// specifies that the user has changed current URL
    /// of the specific media on a prim face
    /// </summary>
    public class ObjectMediaNavigateMessage : IMessage
    {
        /// <summary>
        /// New URL
        /// </summary>
        public string URL;

        /// <summary>
        /// Prim UUID where navigation occured
        /// </summary>
        public UUID PrimID;

        /// <summary>
        /// Face index
        /// </summary>
        public int Face;
        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(3)
            {
                ["current_url"] = OSD.FromString(URL),
                ["object_id"] = OSD.FromUUID(PrimID),
                ["texture_index"] = OSD.FromInteger(Face)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            URL = map["current_url"].AsString();
            PrimID = map["object_id"].AsUUID();
            Face = map["texture_index"].AsInteger();
        }
    }


    /// <summary>Base class used for the ObjectMedia message</summary>
    [Serializable]
    public abstract class ObjectMediaBlock
    {
        public abstract OSDMap Serialize();
        public abstract void Deserialize(OSDMap map);
    }

    /// <summary>
    /// Message used to retrive prim media data
    /// </summary>
    public class ObjectMediaRequest : ObjectMediaBlock
    {
        /// <summary>
        /// Prim UUID
        /// </summary>
        public UUID PrimID;

        /// <summary>
        /// Requested operation, either GET or UPDATE
        /// </summary>
        public string Verb = "GET"; // "GET" or "UPDATE"

        /// <summary>
        /// Serialize object
        /// </summary>
        /// <returns>Serialized object as OSDMap</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2)
            {
                ["object_id"] = OSD.FromUUID(PrimID),
                ["verb"] = OSD.FromString(Verb)
            };
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            PrimID = map["object_id"].AsUUID();
            Verb = map["verb"].AsString();
        }
    }


    /// <summary>
    /// Message used to update prim media data
    /// </summary>
    public class ObjectMediaResponse : ObjectMediaBlock
    {
        /// <summary>
        /// Prim UUID
        /// </summary>
        public UUID PrimID;

        /// <summary>
        /// Array of media entries indexed by face number
        /// </summary>
        public MediaEntry[] FaceMedia;

        /// <summary>
        /// Media version string
        /// </summary>
        public string Version; // String in this format: x-mv:0000000016/00000000-0000-0000-0000-000000000000

        /// <summary>
        /// Serialize object
        /// </summary>
        /// <returns>Serialized object as OSDMap</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2) {["object_id"] = OSD.FromUUID(PrimID)};

            if (FaceMedia == null)
            {
                map["object_media_data"] = new OSDArray();
            }
            else
            {
                OSDArray mediaData = new OSDArray(FaceMedia.Length);

                foreach (MediaEntry media in FaceMedia)
                {
                    mediaData.Add(media == null ? new OSD() : media.GetOSD());
                }

                map["object_media_data"] = mediaData;
            }

            map["object_media_version"] = OSD.FromString(Version);
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            PrimID = map["object_id"].AsUUID();

            if (map["object_media_data"].Type == OSDType.Array)
            {
                OSDArray mediaData = (OSDArray)map["object_media_data"];
                if (mediaData.Count > 0)
                {
                    FaceMedia = new MediaEntry[mediaData.Count];
                    for (int i = 0; i < mediaData.Count; i++)
                    {
                        if (mediaData[i].Type == OSDType.Map)
                        {
                            FaceMedia[i] = MediaEntry.FromOSD(mediaData[i]);
                        }
                    }
                }
            }
            Version = map["object_media_version"].AsString();
        }
    }


    /// <summary>
    /// Message used to update prim media data
    /// </summary>
    public class ObjectMediaUpdate : ObjectMediaBlock
    {
        /// <summary>
        /// Prim UUID
        /// </summary>
        public UUID PrimID;

        /// <summary>
        /// Array of media entries indexed by face number
        /// </summary>
        public MediaEntry[] FaceMedia;

        /// <summary>
        /// Requested operation, either GET or UPDATE
        /// </summary>
        public string Verb = "UPDATE"; // "GET" or "UPDATE"

        /// <summary>
        /// Serialize object
        /// </summary>
        /// <returns>Serialized object as OSDMap</returns>
        public override OSDMap Serialize()
        {
            OSDMap map = new OSDMap(2) {["object_id"] = OSD.FromUUID(PrimID)};

            if (FaceMedia == null)
            {
                map["object_media_data"] = new OSDArray();
            }
            else
            {
                OSDArray mediaData = new OSDArray(FaceMedia.Length);

                foreach (MediaEntry media in FaceMedia)
                {
                    mediaData.Add(media == null ? new OSD() : media.GetOSD());
                }

                map["object_media_data"] = mediaData;
            }

            map["verb"] = OSD.FromString(Verb);
            return map;
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            PrimID = map["object_id"].AsUUID();

            if (map["object_media_data"].Type == OSDType.Array)
            {
                OSDArray mediaData = (OSDArray)map["object_media_data"];
                if (mediaData.Count > 0)
                {
                    FaceMedia = new MediaEntry[mediaData.Count];
                    for (int i = 0; i < mediaData.Count; i++)
                    {
                        if (mediaData[i].Type == OSDType.Map)
                        {
                            FaceMedia[i] = MediaEntry.FromOSD(mediaData[i]);
                        }
                    }
                }
            }
            Verb = map["verb"].AsString();
        }
    }

    /// <summary>
    /// Message for setting or getting per face MediaEntry
    /// </summary>
    [Serializable]
    public class ObjectMediaMessage : IMessage
    {
        /// <summary>The request or response details block</summary>
        public ObjectMediaBlock Request;

        /// <summary>
        /// Serialize the object
        /// </summary>
        /// <returns>An <see cref="OSDMap"/> containing the objects data</returns>
        public OSDMap Serialize()
        {
            return Request.Serialize();
        }

        /// <summary>
        /// Deserialize the message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("verb"))
            {
                switch (map["verb"].AsString())
                {
                    case "GET":
                        Request = new ObjectMediaRequest();
                        break;
                    case "UPDATE":
                        Request = new ObjectMediaUpdate();
                        break;
                }
            }
            else if (map.ContainsKey("object_media_version"))
                Request = new ObjectMediaResponse();
            else
                Logger.Log("Unable to deserialize ObjectMedia: No message handler exists for method: " + map.AsString(), Helpers.LogLevel.Warning);

            Request?.Deserialize(map);
        }
    }
    #endregion Object Media Messages

    #region Resource usage
    /// <summary>Details about object resource usage</summary>
    public class ObjectResourcesDetail
    {
        /// <summary>Object UUID</summary>
        public UUID ID;
        /// <summary>Object name</summary>
        public string Name;
        /// <summary>Indicates if object is group owned</summary>
        public bool GroupOwned;
        /// <summary>Locatio of the object</summary>
        public Vector3d Location;
        /// <summary>Object owner</summary>
        public UUID OwnerID;
        /// <summary>Resource usage, keys are resource names, values are resource usage for that specific resource</summary>
        public Dictionary<string, int> Resources;

        /// <summary>
        /// Deserializes object from OSD
        /// </summary>
        /// <param name="obj">An <see cref="OSDMap"/> containing the data</param>
        public virtual void Deserialize(OSDMap obj)
        {
            ID = obj["id"].AsUUID();
            Name = obj["name"].AsString();
            Location = obj["location"].AsVector3d();
            GroupOwned = obj["is_group_owned"].AsBoolean();
            OwnerID = obj["owner_id"].AsUUID();
            OSDMap resources = (OSDMap)obj["resources"];
            Resources = new Dictionary<string, int>(resources.Keys.Count);
            foreach (KeyValuePair<string, OSD> kvp in resources)
            {
                Resources.Add(kvp.Key, kvp.Value.AsInteger());
            }
        }

        /// <summary>
        /// Makes an instance based on deserialized data
        /// </summary>
        /// <param name="osd"><see cref="OSD"/> serialized data</param>
        /// <returns>Instance containg deserialized data</returns>
        public static ObjectResourcesDetail FromOSD(OSD osd)
        {
            ObjectResourcesDetail res = new ObjectResourcesDetail();
            res.Deserialize((OSDMap)osd);
            return res;
        }
    }

    /// <summary>Details about parcel resource usage</summary>
    public class ParcelResourcesDetail
    {
        /// <summary>Parcel UUID</summary>
        public UUID ID;
        /// <summary>Parcel local ID</summary>
        public int LocalID;
        /// <summary>Parcel name</summary>
        public string Name;
        /// <summary>Indicates if parcel is group owned</summary>
        public bool GroupOwned;
        /// <summary>Parcel owner</summary>
        public UUID OwnerID;
        /// <summary>Array of <see cref="ObjectResourcesDetail"/> containing per object resource usage</summary>
        public ObjectResourcesDetail[] Objects;

        /// <summary>
        /// Deserializes object from OSD
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public virtual void Deserialize(OSDMap map)
        {
            ID = map["id"].AsUUID();
            LocalID = map["local_id"].AsInteger();
            Name = map["name"].AsString();
            GroupOwned = map["is_group_owned"].AsBoolean();
            OwnerID = map["owner_id"].AsUUID();

            OSDArray objectsOSD = (OSDArray)map["objects"];
            Objects = new ObjectResourcesDetail[objectsOSD.Count];

            for (int i = 0; i < objectsOSD.Count; i++)
            {
                Objects[i] = ObjectResourcesDetail.FromOSD(objectsOSD[i]);
            }
        }

        /// <summary>
        /// Makes an instance based on deserialized data
        /// </summary>
        /// <param name="osd"><see cref="OSD"/> serialized data</param>
        /// <returns>Instance containg deserialized data</returns>
        public static ParcelResourcesDetail FromOSD(OSD osd)
        {
            ParcelResourcesDetail res = new ParcelResourcesDetail();
            res.Deserialize((OSDMap)osd);
            return res;
        }
    }

    /// <summary>Resource usage base class, both agent and parcel resource
    /// usage contains summary information</summary>
    public abstract class BaseResourcesInfo : IMessage
    {
        /// <summary>Summary of available resources, keys are resource names,
        /// values are resource usage for that specific resource</summary>
        public Dictionary<string, int> SummaryAvailable;
        /// <summary>Summary resource usage, keys are resource names,
        /// values are resource usage for that specific resource</summary>
        public Dictionary<string, int> SummaryUsed;

        /// <summary>
        /// Serializes object
        /// </summary>
        /// <returns><see cref="OSDMap"/> serialized data</returns>
        public virtual OSDMap Serialize()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deserializes object from OSD
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public virtual void Deserialize(OSDMap map)
        {
            SummaryAvailable = new Dictionary<string, int>();
            SummaryUsed = new Dictionary<string, int>();

            OSDMap summary = (OSDMap)map["summary"];
            OSDArray available = (OSDArray)summary["available"];
            OSDArray used = (OSDArray)summary["used"];

            foreach (OSD av in available)
            {
                OSDMap limit = (OSDMap)av;
                SummaryAvailable.Add(limit["type"].AsString(), limit["amount"].AsInteger());
            }

            foreach (OSD t in used)
            {
                OSDMap limit = (OSDMap)t;
                SummaryUsed.Add(limit["type"].AsString(), limit["amount"].AsInteger());
            }
        }
    }

    /// <summary>Agent resource usage</summary>
    public class AttachmentResourcesMessage : BaseResourcesInfo
    {
        /// <summary>Per attachment point object resource usage</summary>
        public Dictionary<AttachmentPoint, ObjectResourcesDetail[]> Attachments;

        /// <summary>
        /// Deserializes object from OSD
        /// </summary>
        /// <param name="osd">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap osd)
        {
            base.Deserialize(osd);
            OSDArray attachments = (OSDArray)((OSDMap)osd)["attachments"];
            Attachments = new Dictionary<AttachmentPoint, ObjectResourcesDetail[]>();

            foreach (OSD t in attachments)
            {
                OSDMap attachment = (OSDMap)t;
                AttachmentPoint pt = Utils.StringToAttachmentPoint(attachment["location"].AsString());

                OSDArray objectsOSD = (OSDArray)attachment["objects"];
                ObjectResourcesDetail[] objects = new ObjectResourcesDetail[objectsOSD.Count];

                for (int j = 0; j < objects.Length; j++)
                {
                    objects[j] = ObjectResourcesDetail.FromOSD(objectsOSD[j]);
                }

                Attachments.Add(pt, objects);
            }
        }

        /// <summary>
        /// Makes an instance based on deserialized data
        /// </summary>
        /// <param name="osd"><see cref="OSD"/> serialized data</param>
        /// <returns>Instance containg deserialized data</returns>
        public static AttachmentResourcesMessage FromOSD(OSD osd)
        {
            AttachmentResourcesMessage res = new AttachmentResourcesMessage();
            res.Deserialize((OSDMap)osd);
            return res;
        }

        /// <summary>
        /// Detects which class handles deserialization of this message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        /// <returns>Object capable of decoding this message</returns>
        public static IMessage GetMessageHandler(OSDMap map)
        {
            return map == null ? null : new AttachmentResourcesMessage();
        }
    }

    /// <summary>Request message for parcel resource usage</summary>
    public class LandResourcesRequest : IMessage
    {
        /// <summary>UUID of the parel to request resource usage info</summary>
        public UUID ParcelID;

        /// <summary>
        /// Serializes object
        /// </summary>
        /// <returns><see cref="OSDMap"/> serialized data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1) {["parcel_id"] = OSD.FromUUID(ParcelID)};
            return map;
        }

        /// <summary>
        /// Deserializes object from OSD
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            ParcelID = map["parcel_id"].AsUUID();
        }
    }

    /// <summary>Response message for parcel resource usage</summary>
    public class LandResourcesMessage : IMessage
    {
        /// <summary>URL where parcel resource usage details can be retrieved</summary>
        public Uri ScriptResourceDetails;
        /// <summary>URL where parcel resource usage summary can be retrieved</summary>
        public Uri ScriptResourceSummary;

        /// <summary>
        /// Serializes object
        /// </summary>
        /// <returns><see cref="OSDMap"/> serialized data</returns>
        public OSDMap Serialize()
        {
            OSDMap map = new OSDMap(1);
            if (ScriptResourceSummary != null)
            {
                map["ScriptResourceSummary"] = OSD.FromString(ScriptResourceSummary.ToString());
            }

            if (ScriptResourceDetails != null)
            {
                map["ScriptResourceDetails"] = OSD.FromString(ScriptResourceDetails.ToString());
            }
            return map;
        }

        /// <summary>
        /// Deserializes object from OSD
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("ScriptResourceSummary"))
            {
                ScriptResourceSummary = new Uri(map["ScriptResourceSummary"].AsString());
            }
            if (map.ContainsKey("ScriptResourceDetails"))
            {
                ScriptResourceDetails = new Uri(map["ScriptResourceDetails"].AsString());
            }
        }

        /// <summary>
        /// Detects which class handles deserialization of this message
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        /// <returns>Object capable of decoding this message</returns>
        public static IMessage GetMessageHandler(OSDMap map)
        {
            if (map.ContainsKey("parcel_id"))
            {
                return new LandResourcesRequest();
            }
            if (map.ContainsKey("ScriptResourceSummary"))
            {
                return new LandResourcesMessage();
            }
            return null;
        }
    }

    /// <summary>Parcel resource usage</summary>
    public class LandResourcesInfo : BaseResourcesInfo
    {
        /// <summary>Array of <see cref="ParcelResourcesDetail"/> containing per percal resource usage</summary>
        public ParcelResourcesDetail[] Parcels;

        /// <summary>
        /// Deserializes object from OSD
        /// </summary>
        /// <param name="map">An <see cref="OSDMap"/> containing the data</param>
        public override void Deserialize(OSDMap map)
        {
            if (map.ContainsKey("summary"))
            {
                base.Deserialize(map);
            }
            else if (map.ContainsKey("parcels"))
            {
                OSDArray parcelsOSD = (OSDArray)map["parcels"];
                Parcels = new ParcelResourcesDetail[parcelsOSD.Count];

                for (int i = 0; i < parcelsOSD.Count; i++)
                {
                    Parcels[i] = ParcelResourcesDetail.FromOSD(parcelsOSD[i]);
                }
            }
        }
    }

    #endregion Resource usage

    #region Display names
    /// <summary>
    /// Reply to request for bunch if display names
    /// </summary>
    public class GetDisplayNamesMessage : IMessage
    {
        /// <summary> Current display name </summary>
        public AgentDisplayName[] Agents = new AgentDisplayName[0];

        /// <summary> Following UUIDs failed to return a valid display name </summary>
        public UUID[] BadIDs = new UUID[0];

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>OSD containting the messaage</returns>
        public OSDMap Serialize()
        {
            OSDArray agents = new OSDArray();

            if (Agents != null && Agents.Length > 0)
            {
                foreach (AgentDisplayName agent in Agents)
                {
                    agents.Add(agent.GetOSD());
                }
            }

            OSDArray badIDs = new OSDArray();
            if (BadIDs != null && BadIDs.Length > 0)
            {
                foreach (UUID bad in BadIDs)
                {
                    badIDs.Add(new OSDUUID(bad));
                }
            }

            OSDMap ret = new OSDMap
            {
                ["agents"] = agents,
                ["bad_ids"] = badIDs
            };
            return ret;
        }

        public void Deserialize(OSDMap map)
        {
            if (map["agents"].Type == OSDType.Array)
            {
                OSDArray osdAgents = (OSDArray)map["agents"];

                if (osdAgents.Count > 0)
                {
                    Agents = new AgentDisplayName[osdAgents.Count];

                    for (int i = 0; i < osdAgents.Count; i++)
                    {
                        Agents[i] = AgentDisplayName.FromOSD(osdAgents[i]);
                    }
                }
            }

            if (map["bad_ids"].Type == OSDType.Array)
            {
                OSDArray osdBadIDs = (OSDArray)map["bad_ids"];
                if (osdBadIDs.Count > 0)
                {
                    BadIDs = new UUID[osdBadIDs.Count];

                    for (int i = 0; i < osdBadIDs.Count; i++)
                    {
                        BadIDs[i] = osdBadIDs[i];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Message sent when requesting change of the display name
    /// </summary>
    public class SetDisplayNameMessage : IMessage
    {
        /// <summary> Current display name </summary>
        public string OldDisplayName;

        /// <summary> Desired new display name </summary>
        public string NewDisplayName;

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>OSD containting the messaage</returns>
        public OSDMap Serialize()
        {
            OSDArray names = new OSDArray(2) {OldDisplayName, NewDisplayName};

            OSDMap name = new OSDMap {["display_name"] = names};
            return name;
        }

        public void Deserialize(OSDMap map)
        {
            OSDArray names = (OSDArray)map["display_name"];
            OldDisplayName = names[0];
            NewDisplayName = names[1];
        }
    }

    /// <summary>
    /// Message recieved in response to request to change display name
    /// </summary>
    public class SetDisplayNameReplyMessage : IMessage
    {
        /// <summary> New display name </summary>
        public AgentDisplayName DisplayName;

        /// <summary> String message indicating the result of the operation </summary>
        public string Reason;

        /// <summary> Numerical code of the result, 200 indicates success </summary>
        public int Status;

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>OSD containting the messaage</returns>
        public OSDMap Serialize()
        {
            OSDMap agent = (OSDMap)DisplayName.GetOSD();
            OSDMap ret = new OSDMap
            {
                ["content"] = agent,
                ["reason"] = Reason,
                ["status"] = Status
            };
            return ret;
        }

        public void Deserialize(OSDMap map)
        {
            OSDMap agent = (OSDMap)map["content"];
            DisplayName = AgentDisplayName.FromOSD(agent);
            Reason = map["reason"];
            Status = map["status"];
        }
    }

    /// <summary>
    /// Message recieved when someone nearby changes their display name
    /// </summary>
    public class DisplayNameUpdateMessage : IMessage
    {
        /// <summary> Previous display name, empty string if default </summary>
        public string OldDisplayName;

        /// <summary> New display name </summary>
        public AgentDisplayName DisplayName;

        /// <summary>
        /// Serializes the message
        /// </summary>
        /// <returns>OSD containting the messaage</returns>
        public OSDMap Serialize()
        {
            OSDMap agent = (OSDMap)DisplayName.GetOSD();
            agent["old_display_name"] = OldDisplayName;
            OSDMap ret = new OSDMap {["agent"] = agent};
            return ret;
        }

        public void Deserialize(OSDMap map)
        {
            OSDMap agent = (OSDMap)map["agent"];
            DisplayName = AgentDisplayName.FromOSD(agent);
            OldDisplayName = agent["old_display_name"];
        }
    }
    #endregion Display names
}
