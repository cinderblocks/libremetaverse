/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC
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
using System.Threading;
using LibreMetaverse.Packets;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Messages.Linden;

namespace LibreMetaverse
{
    /// <summary>
    /// Sends a <see cref="Parcel"/> to a simulator
    /// </summary>
    public static class ParcelExtensions
    {
        /// <summary>
        /// Update the simulator with any local changes to this Parcel object
        /// </summary>
        /// <param name="parcel">The parcel to send</param>
        /// <param name="client">Client message originates from</param>
        /// <param name="simulator">Simulator to send updates to</param>
        /// <param name="wantReply">Whether we want the simulator to confirm
        /// the update with a reply packet or not</param>
        public static void Update(this Parcel parcel, GridClient client, Simulator simulator, bool wantReply)
        {
            if (simulator == null) throw new ArgumentNullException(nameof(simulator));
            Uri? cap = simulator.Caps?.CapabilityURI("ParcelPropertiesUpdate");
            if (cap != null)
            {
                ParcelPropertiesUpdateMessage payload = new ParcelPropertiesUpdateMessage
                {
                    AuthBuyerID = parcel.AuthBuyerID,
                    Category = parcel.Category,
                    Desc = parcel.Desc,
                    GroupID = parcel.GroupID,
                    Landing = parcel.Landing,
                    LocalID = parcel.LocalID,
                    MediaAutoScale = parcel.Media.MediaAutoScale,
                    MediaDesc = parcel.Media.MediaDesc,
                    MediaHeight = parcel.Media.MediaHeight,
                    MediaID = parcel.Media.MediaID,
                    MediaLoop = parcel.Media.MediaLoop,
                    MediaType = parcel.Media.MediaType,
                    MediaURL = parcel.Media.MediaURL,
                    MediaWidth = parcel.Media.MediaWidth,
                    MusicURL = parcel.MusicURL,
                    Name = parcel.Name,
                    ObscureMedia = parcel.ObscureMedia,
                    ObscureMusic = parcel.ObscureMusic,
                    ParcelFlags = parcel.Flags,
                    PassHours = parcel.PassHours,
                    PassPrice = (uint) parcel.PassPrice,
                    SalePrice = (uint) parcel.SalePrice,
                    SnapshotID = parcel.SnapshotID,
                    UserLocation = parcel.UserLocation,
                    UserLookAt = parcel.UserLookAt,
                    SeeAVs = parcel.SeeAVs,
                    AnyAVSounds = parcel.AnyAVSounds,
                    GroupAVSounds = parcel.GroupAVSounds
                };

                _ = client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(),
                    CancellationToken.None);
            }
            else // lludp fallback
            {
                ParcelPropertiesUpdatePacket updatePacket = new ParcelPropertiesUpdatePacket
                {
                    AgentData =
                    {
                        AgentID = simulator.Client.Self.AgentID,
                        SessionID = simulator.Client.Self.SessionID
                    },
                    ParcelData =
                    {
                        LocalID = parcel.LocalID,
                        AuthBuyerID = parcel.AuthBuyerID,
                        Category = (byte) parcel.Category,
                        Desc = Utils.StringToBytes(parcel.Desc),
                        GroupID = parcel.GroupID,
                        LandingType = (byte) parcel.Landing,
                        MediaAutoScale = (parcel.Media.MediaAutoScale) ? (byte) 0x1 : (byte) 0x0,
                        MediaID = parcel.Media.MediaID,
                        MediaURL = Utils.StringToBytes(parcel.Media.MediaURL),
                        MusicURL = Utils.StringToBytes(parcel.MusicURL),
                        Name = Utils.StringToBytes(parcel.Name)
                    }
                };

                if (wantReply)
                    updatePacket.ParcelData.Flags = 1;
                
                updatePacket.ParcelData.ParcelFlags = (uint)parcel.Flags;
                updatePacket.ParcelData.PassHours = parcel.PassHours;
                updatePacket.ParcelData.PassPrice = parcel.PassPrice;
                updatePacket.ParcelData.SalePrice = parcel.SalePrice;
                updatePacket.ParcelData.SnapshotID = parcel.SnapshotID;
                updatePacket.ParcelData.UserLocation = parcel.UserLocation;
                updatePacket.ParcelData.UserLookAt = parcel.UserLookAt;

                simulator.SendPacket(updatePacket);
            }

            parcel.UpdateOtherCleanTime(simulator);
        }

        /// <summary>
        /// Set Autoreturn time
        /// </summary>
        /// <param name="parcel">The parcel whose auto-return time is sent</param>
        /// <param name="simulator">Simulator to send the update to</param>
        public static void UpdateOtherCleanTime(this Parcel parcel, Simulator simulator)
        {
            if (simulator == null) throw new ArgumentNullException(nameof(simulator));
            ParcelSetOtherCleanTimePacket request = new ParcelSetOtherCleanTimePacket
            {
                AgentData =
                {
                    AgentID = simulator.Client.Self.AgentID,
                    SessionID = simulator.Client.Self.SessionID
                },
                ParcelData =
                {
                    LocalID = parcel.LocalID,
                    OtherCleanTime = parcel.OtherCleanTime
                }
            };

            simulator.SendPacket(request);
        }
    }
}
