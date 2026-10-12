/*
 * Copyright (c) 2026, Sjofn LLC.
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


using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    public class AccountLevelBenefits
    {
        public int MeshUploadCost { get; init; }
        public int OneTimeEventAllowed { get; init; }
        public int RepeatingEventsCost { get; init; }
        public string EstateAccessToken { get; init; }
        public int LastnameChangeRate { get; init; }
        public int MarketplaceConciergeSupport { get; init; }
        public int PhoneSupport { get; init; }
        public double LindenBuyFee { get; init; }
        public int PartnerFee { get; init; }
        public int CreateRepeatingEvents { get; init; }
        public int TextureUploadCost { get; init; }
        public int PriorityEntry { get; init; }
        public int ObjectAccountLevel { get; init; }
        public int UseAnimesh { get; init; }
        public double LastnameChangeCost { get; init; }
        public int BetaGridLand { get; init; }
        public int CreateGroupCost { get; init; }
        public int LiveChat { get; init; }
        public int LandAuctionsAllowed { get; init; }
        public int MainlandTier { get; init; }
        public int LocalExperiences { get; init; }
        public int OneTimeEventCost { get; init; }
        public int MarketplacePleLimit { get; init; }
        public int UnpartnerFee { get; init; }
        public int GroupMembershipLimit { get; init; }
        public int AnimationUploadCost { get; init; }
        public int ScriptLimit { get; init; }
        public int TransactionHistoryLimit { get; init; }
        public List<int> LargeTextureUploadCost { get; init; }
        public int AnimatedObjectLimit { get; init; }
        public int GridwideExperienceLimit { get; init; }
        public int LastnameChangeAllowed { get; init; }
        public int SoundUploadCost { get; init; }
        public int PremiumAlts { get; init; }
        public int PicksLimit { get; init; }
        public int PlacePages { get; init; }
        public int PremiumAccess { get; init; }
        public int MarketplaceListingLimit { get; init; }
        public int PremiumGifts { get; init; }
        public int VoiceMorphing { get; init; }
        public int StoredImLimit { get; init; }
        public int Stipend { get; init; }
        public int SignupBonus { get; init; }
        public int AttachmentLimit { get; init; }

        /// <summary>
        /// Benefits that say nothing: -1 for each number that is not given, and the 38 attachments everyone may wear.
        /// Set the ones a login server grants.
        /// </summary>
        public AccountLevelBenefits()
        {
            MeshUploadCost = -1;
            OneTimeEventAllowed = -1;
            RepeatingEventsCost = -1;
            EstateAccessToken = string.Empty;
            LastnameChangeRate = -1;
            MarketplaceConciergeSupport = -1;
            PhoneSupport = -1;
            LindenBuyFee = -1.0;
            PartnerFee = -1;
            CreateRepeatingEvents = -1;
            TextureUploadCost = -1;
            PriorityEntry = -1;
            ObjectAccountLevel = -1;
            UseAnimesh = -1;
            LastnameChangeCost = -1.0;
            BetaGridLand = -1;
            CreateGroupCost = -1;
            LiveChat = -1;
            LandAuctionsAllowed = -1;
            MainlandTier = -1;
            LocalExperiences = -1;
            OneTimeEventCost = -1;
            MarketplacePleLimit = -1;
            UnpartnerFee = -1;
            GroupMembershipLimit = -1;
            AnimationUploadCost = -1;
            ScriptLimit = -1;
            TransactionHistoryLimit = -1;
            AnimatedObjectLimit = -1;
            GridwideExperienceLimit = -1;
            LastnameChangeAllowed = -1;
            SoundUploadCost = -1;
            PremiumAlts = -1;
            PicksLimit = -1;
            PlacePages = -1;
            PremiumAccess = -1;
            MarketplaceListingLimit = -1;
            PremiumGifts = -1;
            VoiceMorphing = -1;
            StoredImLimit = -1;
            Stipend = -1;
            SignupBonus = -1;
            AttachmentLimit = 38;
            LargeTextureUploadCost = new List<int>();
        }

        /// <summary>
        /// The benefits as the login server sends them, in the form the <see cref="AccountLevelBenefits(OSDMap)"/> constructor reads.
        /// </summary>
        public OSDMap ToOSD()
        {
            return new OSDMap
            {
                ["mesh_upload_cost"] = OSD.FromInteger(MeshUploadCost),
                ["one_time_event_allowed"] = OSD.FromInteger(OneTimeEventAllowed),
                ["repeating_events_cost"] = OSD.FromInteger(RepeatingEventsCost),
                ["estate_access_token"] = OSD.FromString(EstateAccessToken),
                ["lastname_change_rate"] = OSD.FromInteger(LastnameChangeRate),
                ["marketplace_concierge_support"] = OSD.FromInteger(MarketplaceConciergeSupport),
                ["phone_support"] = OSD.FromInteger(PhoneSupport),
                ["linden_buy_fee"] = OSD.FromReal(LindenBuyFee),
                ["partner_fee"] = OSD.FromInteger(PartnerFee),
                ["create_repeating_events"] = OSD.FromInteger(CreateRepeatingEvents),
                ["texture_upload_cost"] = OSD.FromInteger(TextureUploadCost),
                ["priority_entry"] = OSD.FromInteger(PriorityEntry),
                ["object_account_level"] = OSD.FromInteger(ObjectAccountLevel),
                ["use_animesh"] = OSD.FromInteger(UseAnimesh),
                ["lastname_change_cost"] = OSD.FromReal(LastnameChangeCost),
                ["beta_grid_land"] = OSD.FromInteger(BetaGridLand),
                ["create_group_cost"] = OSD.FromInteger(CreateGroupCost),
                ["live_chat"] = OSD.FromInteger(LiveChat),
                ["land_auctions_allowed"] = OSD.FromInteger(LandAuctionsAllowed),
                ["mainland_tier"] = OSD.FromInteger(MainlandTier),
                ["local_experiences"] = OSD.FromInteger(LocalExperiences),
                ["one_time_event_cost"] = OSD.FromInteger(OneTimeEventCost),
                ["marketplace_ple_limit"] = OSD.FromInteger(MarketplacePleLimit),
                ["unpartner_fee"] = OSD.FromInteger(UnpartnerFee),
                ["group_membership_limit"] = OSD.FromInteger(GroupMembershipLimit),
                ["animation_upload_cost"] = OSD.FromInteger(AnimationUploadCost),
                ["script_limit"] = OSD.FromInteger(ScriptLimit),
                ["transaction_history_limit"] = OSD.FromInteger(TransactionHistoryLimit),
                ["animated_object_limit"] = OSD.FromInteger(AnimatedObjectLimit),
                ["gridwide_experience_limit"] = OSD.FromInteger(GridwideExperienceLimit),
                ["lastname_change_allowed"] = OSD.FromInteger(LastnameChangeAllowed),
                ["sound_upload_cost"] = OSD.FromInteger(SoundUploadCost),
                ["premium_alts"] = OSD.FromInteger(PremiumAlts),
                ["picks_limit"] = OSD.FromInteger(PicksLimit),
                ["place_pages"] = OSD.FromInteger(PlacePages),
                ["premium_access"] = OSD.FromInteger(PremiumAccess),
                ["marketplace_listing_limit"] = OSD.FromInteger(MarketplaceListingLimit),
                ["premium_gifts"] = OSD.FromInteger(PremiumGifts),
                ["voice_morphing"] = OSD.FromInteger(VoiceMorphing),
                ["stored_im_limit"] = OSD.FromInteger(StoredImLimit),
                ["stipend"] = OSD.FromInteger(Stipend),
                ["signup_bonus"] = OSD.FromInteger(SignupBonus),
                ["attachment_limit"] = OSD.FromInteger(AttachmentLimit),
                ["large_texture_upload_cost"] = ToArray(LargeTextureUploadCost)
            };
        }

        private static OSDArray ToArray(IEnumerable<int> values)
        {
            var array = new OSDArray();
            foreach (int value in values) array.Add(OSD.FromInteger(value));
            return array;
        }

        public AccountLevelBenefits(OSDMap reply)
        {
            MeshUploadCost = GetIntFromMap(reply, "mesh_upload_cost", -1);
            OneTimeEventAllowed = GetIntFromMap(reply, "one_time_event_allowed", -1);
            RepeatingEventsCost = GetIntFromMap(reply, "repeating_events_cost", -1);
            EstateAccessToken = GetStringFromMap(reply, "estate_access_token", string.Empty);
            LastnameChangeRate = GetIntFromMap(reply, "lastname_change_rate", -1);
            MarketplaceConciergeSupport = GetIntFromMap(reply, "marketplace_concierge_support", -1);
            PhoneSupport = GetIntFromMap(reply, "phone_support", -1);
            LindenBuyFee = GetRealFromMap(reply, "linden_buy_fee", -1.0);
            PartnerFee = GetIntFromMap(reply, "partner_fee", -1);
            CreateRepeatingEvents = GetIntFromMap(reply, "create_repeating_events", -1);
            TextureUploadCost = GetIntFromMap(reply, "texture_upload_cost", -1);
            PriorityEntry = GetIntFromMap(reply, "priority_entry", -1);
            ObjectAccountLevel = GetIntFromMap(reply, "object_account_level", -1);
            UseAnimesh = GetIntFromMap(reply, "use_animesh", -1);
            LastnameChangeCost = GetRealFromMap(reply, "lastname_change_cost", -1.0);
            BetaGridLand = GetIntFromMap(reply, "beta_grid_land", -1);
            CreateGroupCost = GetIntFromMap(reply, "create_group_cost", -1);
            LiveChat = GetIntFromMap(reply, "live_chat", -1);
            LandAuctionsAllowed = GetIntFromMap(reply, "land_auctions_allowed", -1);
            MainlandTier = GetIntFromMap(reply, "mainland_tier", -1);
            LocalExperiences = GetIntFromMap(reply, "local_experiences", -1);
            OneTimeEventCost = GetIntFromMap(reply, "one_time_event_cost", -1);
            MarketplacePleLimit = GetIntFromMap(reply, "marketplace_ple_limit", -1);
            UnpartnerFee = GetIntFromMap(reply, "unpartner_fee", -1);
            GroupMembershipLimit = GetIntFromMap(reply, "group_membership_limit", -1);
            AnimationUploadCost = GetIntFromMap(reply, "animation_upload_cost", -1);
            ScriptLimit = GetIntFromMap(reply, "script_limit", -1);
            TransactionHistoryLimit = GetIntFromMap(reply, "transaction_history_limit", -1);
            LargeTextureUploadCost = GetIntListFromMap(reply, "large_texture_upload_cost");
            AnimatedObjectLimit = GetIntFromMap(reply, "animated_object_limit", -1);
            GridwideExperienceLimit = GetIntFromMap(reply, "gridwide_experience_limit", -1);
            LastnameChangeAllowed = GetIntFromMap(reply, "lastname_change_allowed", -1);
            SoundUploadCost = GetIntFromMap(reply, "sound_upload_cost", -1);
            PremiumAlts = GetIntFromMap(reply, "premium_alts", -1);
            PicksLimit = GetIntFromMap(reply, "picks_limit", -1);
            PlacePages = GetIntFromMap(reply, "place_pages", -1);
            PremiumAccess = GetIntFromMap(reply, "premium_access", -1);
            MarketplaceListingLimit = GetIntFromMap(reply, "marketplace_listing_limit", -1);
            PremiumGifts = GetIntFromMap(reply, "premium_gifts", -1);
            VoiceMorphing = GetIntFromMap(reply, "voice_morphing", -1);
            StoredImLimit = GetIntFromMap(reply, "stored_im_limit", -1);
            Stipend = GetIntFromMap(reply, "stipend", -1);
            SignupBonus = GetIntFromMap(reply, "signup_bonus", -1);
            AttachmentLimit = GetIntFromMap(reply, "attachment_limit", 38);

            if (LargeTextureUploadCost.Count == 0)
            {
                LargeTextureUploadCost = new List<int>()
                {
                    TextureUploadCost
                };
            }
            else
            {
                LargeTextureUploadCost = LargeTextureUploadCost
                    .OrderBy(n => n)
                    .ToList();
            }
        }

        public AccountLevelBenefits(Hashtable reply)
        {
            MeshUploadCost = GetValueFromMap(reply, "mesh_upload_cost", -1);
            OneTimeEventAllowed = GetValueFromMap(reply, "one_time_event_allowed", -1);
            RepeatingEventsCost = GetValueFromMap(reply, "repeating_events_cost", -1);
            EstateAccessToken = GetValueFromMap(reply, "estate_access_token", string.Empty);
            LastnameChangeRate = GetValueFromMap(reply, "lastname_change_rate", -1);
            MarketplaceConciergeSupport = GetValueFromMap(reply, "marketplace_concierge_support", -1);
            PhoneSupport = GetValueFromMap(reply, "phone_support", -1);
            LindenBuyFee = GetValueFromMap(reply, "linden_buy_fee", -1.0);
            PartnerFee = GetValueFromMap(reply, "partner_fee", -1);
            CreateRepeatingEvents = GetValueFromMap(reply, "create_repeating_events", -1);
            TextureUploadCost = GetValueFromMap(reply, "texture_upload_cost", -1);
            PriorityEntry = GetValueFromMap(reply, "priority_entry", -1);
            ObjectAccountLevel = GetValueFromMap(reply, "object_account_level", -1);
            UseAnimesh = GetValueFromMap(reply, "use_animesh", -1);
            LastnameChangeCost = GetValueFromMap(reply, "lastname_change_cost", -1.0);
            BetaGridLand = GetValueFromMap(reply, "beta_grid_land", -1);
            CreateGroupCost = GetValueFromMap(reply, "create_group_cost", -1);
            LiveChat = GetValueFromMap(reply, "live_chat", -1);
            LandAuctionsAllowed = GetValueFromMap(reply, "land_auctions_allowed", -1);
            MainlandTier = GetValueFromMap(reply, "mainland_tier", -1);
            LocalExperiences = GetValueFromMap(reply, "local_experiences", -1);
            OneTimeEventCost = GetValueFromMap(reply, "one_time_event_cost", -1);
            MarketplacePleLimit = GetValueFromMap(reply, "marketplace_ple_limit", -1);
            UnpartnerFee = GetValueFromMap(reply, "unpartner_fee", -1);
            GroupMembershipLimit = GetValueFromMap(reply, "group_membership_limit", -1);
            AnimationUploadCost = GetValueFromMap(reply, "animation_upload_cost", -1);
            ScriptLimit = GetValueFromMap(reply, "script_limit", -1);
            TransactionHistoryLimit = GetValueFromMap(reply, "transaction_history_limit", -1);
            LargeTextureUploadCost = GetListFromMap<int>(reply, "large_texture_upload_cost");
            AnimatedObjectLimit = GetValueFromMap(reply, "animated_object_limit", -1);
            GridwideExperienceLimit = GetValueFromMap(reply, "gridwide_experience_limit", -1);
            LastnameChangeAllowed = GetValueFromMap(reply, "lastname_change_allowed", -1);
            SoundUploadCost = GetValueFromMap(reply, "sound_upload_cost", -1);
            PremiumAlts = GetValueFromMap(reply, "premium_alts", -1);
            PicksLimit = GetValueFromMap(reply, "picks_limit", -1);
            PlacePages = GetValueFromMap(reply, "place_pages", -1);
            PremiumAccess = GetValueFromMap(reply, "premium_access", -1);
            MarketplaceListingLimit = GetValueFromMap(reply, "marketplace_listing_limit", -1);
            PremiumGifts = GetValueFromMap(reply, "premium_gifts", -1);
            VoiceMorphing = GetValueFromMap(reply, "voice_morphing", -1);
            StoredImLimit = GetValueFromMap(reply, "stored_im_limit", -1);
            Stipend = GetValueFromMap(reply, "stipend", -1);
            SignupBonus = GetValueFromMap(reply, "signup_bonus", -1);
            AttachmentLimit = GetValueFromMap(reply, "attachment_limit", 38);

            if (LargeTextureUploadCost.Count == 0)
            {
                LargeTextureUploadCost = new List<int>()
                {
                    TextureUploadCost
                };
            }
            else
            {
                LargeTextureUploadCost = LargeTextureUploadCost
                    .OrderBy(n => n)
                    .ToList();
            }
        }

        private static T GetValueFromMap<T>(Hashtable reply, string key, T defaultValue)
        {
            if (reply.ContainsKey(key))
            {
                if (reply[key] is T result)
                {
                    return result;
                }
            }

            return defaultValue;
        }

        private static List<T> GetListFromMap<T>(Hashtable reply, string key)
        {
            if (!reply.ContainsKey(key) || !(reply[key] is ArrayList valArray))
            {
                return new List<T>();
            }

            return valArray.OfType<T>().ToList();
        }

        private static int GetIntFromMap(OSDMap reply, string key, int defaultValue)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsInteger() : defaultValue;
        }

        private static double GetRealFromMap(OSDMap reply, string key, double defaultValue)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsReal() : defaultValue;
        }

        private static string GetStringFromMap(OSDMap reply, string key, string defaultValue)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsString() : defaultValue;
        }

        private static List<int> GetIntListFromMap(OSDMap reply, string key)
        {
            if (!reply.TryGetValue(key, out var osd) || !(osd is OSDArray osdArray))
            {
                return new List<int>();
            }

            return osdArray.Select(item => item.AsInteger()).ToList();
        }
    }
}
