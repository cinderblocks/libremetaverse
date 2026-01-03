/**
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2026, Sjofn LLC
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
using System.Threading.Tasks;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    /// <summary>
    /// AgentManager partial class - Profile
    /// </summary>
    public partial class AgentManager
    {
        #region Profile

        /// <summary>
        /// Update agent profile
        /// </summary>
        /// <param name="profile"><see cref="Avatar.AvatarProperties"/> struct containing updated 
        /// profile information</param>
        /// <remarks>
        /// The behavior between LLUDP and Http Capability differs. See each method's remarks
        /// </remarks>
        /// <seealso cref="UpdateProfileUdp"/>
        /// <seealso cref="UpdateProfileHttp"/>
        public void UpdateProfile(Avatar.AvatarProperties profile)
        {
            if (Client?.Network?.CurrentSim?.Caps?.CapabilityURI(AGENT_PROFILE_CAP) != null)
            {
                _ = UpdateProfileHttp(profile);
            }
            else
            {
                UpdateProfileUdp(profile);
            }
        }

        /// <summary>
        /// Update agent profile via simulator LLUDP
        /// </summary>
        /// <param name="profile"><see cref="Avatar.AvatarProperties"/> struct containing updated 
        /// profile information</param>
        public void UpdateProfileUdp(Avatar.AvatarProperties profile)
        {
            AvatarPropertiesUpdatePacket apup = new AvatarPropertiesUpdatePacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = SessionID
                },
                PropertiesData =
                {
                    AboutText = Utils.StringToBytes(profile.AboutText),
                    AllowPublish = profile.AllowPublish,
                    FLAboutText = Utils.StringToBytes(profile.FirstLifeText),
                    FLImageID = profile.FirstLifeImage,
                    ImageID = profile.ProfileImage,
                    MaturePublish = profile.MaturePublish,
                    ProfileURL = Utils.StringToBytes(profile.ProfileURL)
                }
            };

            Client.Network.SendPacket(apup);
        }

        /// <summary>
        /// Update agent profile
        /// </summary>
        /// <param name="profile"><see cref="Avatar.AvatarProperties"/> struct containing updated
        /// profile information</param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// Only updates about text fields, profile url, and allow_publish.
        /// Does not update image UUID, etc. like the legacy LLUDP request.
        /// </remarks>
        public async Task UpdateProfileHttp(Avatar.AvatarProperties profile, CancellationToken cancellationToken = default)
        {
            var payload = new OSDMap
            {
                ["sl_about_text"] = profile.AboutText,
                ["fl_about_text"] = profile.FirstLifeText,
                ["profile_url"] = profile.ProfileURL,
                ["allow_publish"] = profile.AllowPublish,
                ["sl_image_id"] = profile.ProfileImage,
                ["fl_image_id"] = profile.FirstLifeImage
            };

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (Client?.Network?.CurrentSim?.Caps == null)
                {
                    Logger.Warn("Not connected to simulator or capabilities unavailable, cannot update profile.", Client);
                    return;
                }

                var capability = Client.Network.CurrentSim.Caps.CapabilityURI(AGENT_PROFILE_CAP);
                if (capability == null)
                {
                    Logger.Warn("AgentProfile capability not available, cannot update profile.", Client);
                    return;
                }

                var uri = new Uri($"{capability}/{AgentID}");

                await Client.HttpCapsClient.PutRequestAsync(uri, OSDFormat.Xml, payload, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"AgentProfile update failed: {error.Message}", Client);
                            return;
                        }

                        if (response == null)
                        {
                            Logger.Warn("AgentProfile update failed: no response from server.", Client);
                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"AgentProfile update returned non-success status: {response.StatusCode}", Client);
                            return;
                        }

                        Logger.Debug("AgentProfile update succeeded.", Client);
                    }).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("AgentProfile update failed", ex, Client);
                throw;
            }
        }

        /// <summary>
        /// Update agent's private notes for target avatar
        /// </summary>
        /// <param name="target">target avatar for notes</param>
        /// <param name="notes">notes to store</param>
        /// <seealso cref="UpdateProfileHttp"/>
        /// <seealso cref="UpdateProfileUdp"/>
        public void UpdateProfileNotes(UUID target, string notes)
        {
            if (Client?.Network?.CurrentSim?.Caps?.CapabilityURI(AGENT_PROFILE_CAP) != null)
            {
                _ = UpdateProfileNotesHttp(target, notes);
            }
            else
            {
                UpdateProfileNotesUdp(target, notes);
            }
        }

        private void UpdateProfileNotesUdp(UUID target, string notes)
        {
            AvatarNotesUpdatePacket anup = new AvatarNotesUpdatePacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = SessionID
                },
                Data =
                {
                    TargetID = target,
                    Notes = Utils.StringToBytes(notes)
                }
            };
            Client.Network.SendPacket(anup);
        }

        /// <summary>
        /// Update agent's private notes for target avatar using HTTP capability system
        /// </summary>
        /// <param name="target">target avatar for notes</param>
        /// <param name="notes">notes to store</param>
        /// <param name="cancellationToken"></param>
        public async Task UpdateProfileNotesHttp(UUID target, string notes, CancellationToken cancellationToken = default)
        {
            var payload = new OSDMap { ["notes"] = notes };

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (Client?.Network?.CurrentSim?.Caps == null)
                {
                    Logger.Warn("Not connected to simulator or capabilities unavailable, cannot update profile notes.", Client);
                    return;
                }

                var capability = Client.Network.CurrentSim.Caps.CapabilityURI(AGENT_PROFILE_CAP);
                if (capability == null)
                {
                    Logger.Warn("AgentProfile capability not available, cannot update profile notes.", Client);
                    return;
                }

                var uri = new Uri($"{capability}/{target}");

                await Client.HttpCapsClient.PutRequestAsync(uri, OSDFormat.Xml, payload, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"AgentProfile notes update failed: {error.Message}", Client);
                            return;
                        }

                        if (response == null)
                        {
                            Logger.Warn("AgentProfile notes update failed: no response from server.", Client);
                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"AgentProfile notes update returned non-success status: {response.StatusCode}", Client);
                            return;
                        }

                        Logger.Debug("AgentProfile notes update succeeded.", Client);
                    }).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("AgentProfile notes update failed", ex, Client);
                throw;
            }
        }

        /// <summary>
        /// Update agent's profile interests
        /// </summary>
        /// <param name="interests">selection of interests from <see cref="Avatar.Interests"/> struct</param>
        public void UpdateInterests(Avatar.Interests interests)
        {
            AvatarInterestsUpdatePacket aiup = new AvatarInterestsUpdatePacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = SessionID
                },
                PropertiesData =
                {
                    LanguagesText = Utils.StringToBytes(interests.LanguagesText),
                    SkillsMask = interests.SkillsMask,
                    SkillsText = Utils.StringToBytes(interests.SkillsText),
                    WantToMask = interests.WantToMask,
                    WantToText = Utils.StringToBytes(interests.WantToText)
                }
            };

            Client.Network.SendPacket(aiup);
        }

        /// <summary>
        /// Create or update profile pick
        /// </summary>
        /// <param name="pickID">UUID of the pick to update, or random UUID to create a new pick</param>
        /// <param name="topPick">Is this a top pick? (typically false)</param>
        /// <param name="parcelID">UUID of the parcel (UUID.Zero for the current parcel)</param>
        /// <param name="name">Name of the pick</param>
        /// <param name="globalPosition">Global position of the pick landmark</param>
        /// <param name="textureID">UUID of the image displayed with the pick</param>
        /// <param name="description">Long description of the pick</param>
        public void PickInfoUpdate(UUID pickID, bool topPick, UUID parcelID, string name, Vector3d globalPosition, UUID textureID, string description)
        {
            PickInfoUpdatePacket pick = new PickInfoUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data =
                {
                    PickID = pickID,
                    Desc = Utils.StringToBytes(description),
                    CreatorID = Client.Self.AgentID,
                    TopPick = topPick,
                    ParcelID = parcelID,
                    Name = Utils.StringToBytes(name),
                    SnapshotID = textureID,
                    PosGlobal = globalPosition,
                    SortOrder = 0,
                    Enabled = false
                }
            };

            Client.Network.SendPacket(pick);
        }

        /// <summary>
        /// Delete profile pick
        /// </summary>
        /// <param name="pickID">UUID of the pick to delete</param>
        public void PickDelete(UUID pickID)
        {
            PickDeletePacket delete = new PickDeletePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data = {PickID = pickID}
            };

            Client.Network.SendPacket(delete);
        }

        /// <summary>
        /// Create or update profile Classified
        /// </summary>
        /// <param name="classifiedID">UUID of the classified to update, or random UUID to create a new classified</param>
        /// <param name="category">Defines what category the classified is in</param>
        /// <param name="snapshotID">UUID of the image displayed with the classified</param>
        /// <param name="price">Price that the classified will cost to place for a week</param>
        /// <param name="position">Global position of the classified landmark</param>
        /// <param name="name">Name of the classified</param>
        /// <param name="desc">Long description of the classified</param>
        /// <param name="autoRenew">if true, auto-renew classified after expiration</param>
        public void UpdateClassifiedInfo(UUID classifiedID, DirectoryManager.ClassifiedCategories category,
            UUID snapshotID, int price, Vector3d position, string name, string desc, bool autoRenew)
        {
            ClassifiedInfoUpdatePacket classified = new ClassifiedInfoUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data =
                {
                    ClassifiedID = classifiedID,
                    Category = (uint) category,
                    ParcelID = UUID.Zero,
                    ParentEstate = 0,
                    SnapshotID = snapshotID,
                    PosGlobal = position,
                    ClassifiedFlags = autoRenew ? (byte) 32 : (byte) 0,
                    PriceForListing = price,
                    Name = Utils.StringToBytes(name),
                    Desc = Utils.StringToBytes(desc)
                }
            };

            Client.Network.SendPacket(classified);
        }

        /// <summary>
        /// Create or update profile Classified
        /// </summary>
        /// <param name="classifiedID">UUID of the classified to update, or random UUID to create a new classified</param>
        /// <param name="category">Defines what category the classified is in</param>
        /// <param name="snapshotID">UUID of the image displayed with the classified</param>
        /// <param name="price">Price that the classified will cost to place for a week</param>
        /// <param name="name">Name of the classified</param>
        /// <param name="desc">Long description of the classified</param>
        /// <param name="autoRenew">if true, auto-renew classified after expiration</param>
        public void UpdateClassifiedInfo(UUID classifiedID, DirectoryManager.ClassifiedCategories category, UUID snapshotID, int price, string name, string desc, bool autoRenew)
        {
            UpdateClassifiedInfo(classifiedID, category, snapshotID, price, Client.Self.GlobalPosition, name, desc, autoRenew);
        }

        /// <summary>
        /// Delete a classified ad
        /// </summary>
        /// <param name="classifiedID">The classified ads ID</param>
        public void DeleteClassified(UUID classifiedID)
        {
            ClassifiedDeletePacket classified = new ClassifiedDeletePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data = {ClassifiedID = classifiedID}
            };

            Client.Network.SendPacket(classified);
        }

        #endregion Profile
    }
}
