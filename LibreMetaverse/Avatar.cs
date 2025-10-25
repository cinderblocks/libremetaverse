/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using OpenMetaverse.StructuredData;
using System.Reflection;

namespace OpenMetaverse
{
    #region Enums

    /// <summary>
    /// Avatar profile flags
    /// </summary>
    [Flags]
    public enum ProfileFlags : uint
    {
        AllowPublish = 1,
        MaturePublish = 2,
        Identified = 4,
        Transacted = 8,
        Online = 16,
        AgeVerified = 32
    }

    #endregion Enums

    /// <summary>
    /// Represents an avatar (other than your own)
    /// </summary>
    public class Avatar : Primitive
    {
        #region Subclasses

        /// <summary>
        /// Positive and negative ratings
        /// </summary>
        public struct Statistics
        {
            /// <summary>Positive ratings for Behavior</summary>
            public int BehaviorPositive;
            /// <summary>Negative ratings for Behavior</summary>
            public int BehaviorNegative;
            /// <summary>Positive ratings for Appearance</summary>
            public int AppearancePositive;
            /// <summary>Negative ratings for Appearance</summary>
            public int AppearanceNegative;
            /// <summary>Positive ratings for Building</summary>
            public int BuildingPositive;
            /// <summary>Negative ratings for Building</summary>
            public int BuildingNegative;
            /// <summary>Positive ratings given by this avatar</summary>
            public int GivenPositive;
            /// <summary>Negative ratings given by this avatar</summary>
            public int GivenNegative;

            public OSD GetOSD()
            {
                OSDMap tex = new OSDMap(8)
                {
                    ["behavior_positive"] = OSD.FromInteger(BehaviorPositive),
                    ["behavior_negative"] = OSD.FromInteger(BehaviorNegative),
                    ["appearance_positive"] = OSD.FromInteger(AppearancePositive),
                    ["appearance_negative"] = OSD.FromInteger(AppearanceNegative),
                    ["buildings_positive"] = OSD.FromInteger(BuildingPositive),
                    ["buildings_negative"] = OSD.FromInteger(BuildingNegative),
                    ["given_positive"] = OSD.FromInteger(GivenPositive),
                    ["given_negative"] = OSD.FromInteger(GivenNegative)
                };
                return tex;
            }

            public static Statistics FromOSD(OSD O)
            {
                Statistics S = new Statistics();
                OSDMap tex = (OSDMap)O;

                S.BehaviorPositive = tex["behavior_positive"].AsInteger();
                S.BuildingNegative = tex["behavior_negative"].AsInteger();
                S.AppearancePositive = tex["appearance_positive"].AsInteger();
                S.AppearanceNegative = tex["appearance_negative"].AsInteger();
                S.BuildingPositive = tex["buildings_positive"].AsInteger();
                S.BuildingNegative = tex["buildings_negative"].AsInteger();
                S.GivenPositive = tex["given_positive"].AsInteger();
                S.GivenNegative = tex["given_negative"].AsInteger();


                return S;

            }
        }

        /// <summary>
        /// Avatar properties including about text, profile URL, image IDs and 
        /// publishing settings
        /// </summary>
        public struct AvatarProperties
        {
            /// <summary>First Life about text</summary>
            public string FirstLifeText;
            /// <summary>First Life image ID</summary>
            public UUID FirstLifeImage;
            /// <summary></summary>
            public UUID Partner;
            /// <summary></summary>
            public string AboutText;
            /// <summary></summary>
            public string BornOn;
            /// <summary></summary>
            public string CharterMember;
            /// <summary>Profile image ID</summary>
            public UUID ProfileImage;
            /// <summary>Flags of the profile</summary>
            public ProfileFlags Flags;
            /// <summary>Web URL for this profile</summary>
            public string ProfileURL;

            #region Properties

            /// <summary>Should this profile be published on the web</summary>
            public bool AllowPublish
            {
                get { return ((Flags & ProfileFlags.AllowPublish) != 0); }
                set
                {
                    if (value)
                        Flags |= ProfileFlags.AllowPublish;
                    else
                        Flags &= ~ProfileFlags.AllowPublish;
                }
            }
            /// <summary>Avatar Online Status</summary>
            public bool Online
            {
                get { return ((Flags & ProfileFlags.Online) != 0); }
                set
                {
                    if (value)
                        Flags |= ProfileFlags.Online;
                    else
                        Flags &= ~ProfileFlags.Online;
                }
            }
            /// <summary>Is this a mature profile</summary>
            public bool MaturePublish
            {
                get { return ((Flags & ProfileFlags.MaturePublish) != 0); }
                set
                {
                    if (value)
                        Flags |= ProfileFlags.MaturePublish;
                    else
                        Flags &= ~ProfileFlags.MaturePublish;
                }
            }
            /// <summary></summary>
            public bool Identified
            {
                get { return ((Flags & ProfileFlags.Identified) != 0); }
                set
                {
                    if (value)
                        Flags |= ProfileFlags.Identified;
                    else
                        Flags &= ~ProfileFlags.Identified;
                }
            }
            /// <summary></summary>
            public bool Transacted
            {
                get { return ((Flags & ProfileFlags.Transacted) != 0); }
                set
                {
                    if (value)
                        Flags |= ProfileFlags.Transacted;
                    else
                        Flags &= ~ProfileFlags.Transacted;
                }
            }

            public OSD GetOSD()
            {
                OSDMap tex = new OSDMap(9)
                {
                    ["first_life_text"] = OSD.FromString(FirstLifeText),
                    ["first_life_image"] = OSD.FromUUID(FirstLifeImage),
                    ["partner"] = OSD.FromUUID(Partner),
                    ["about_text"] = OSD.FromString(AboutText),
                    ["born_on"] = OSD.FromString(BornOn),
                    ["charter_member"] = OSD.FromString(CharterMember),
                    ["profile_image"] = OSD.FromUUID(ProfileImage),
                    ["flags"] = OSD.FromInteger((byte) Flags),
                    ["profile_url"] = OSD.FromString(ProfileURL)
                };
                return tex;
            }

            public static AvatarProperties FromOSD(OSD O)
            {
                AvatarProperties A = new AvatarProperties();
                OSDMap tex = (OSDMap)O;

                A.FirstLifeText = tex["first_life_text"].AsString();
                A.FirstLifeImage = tex["first_life_image"].AsUUID();
                A.Partner = tex["partner"].AsUUID();
                A.AboutText = tex["about_text"].AsString();
                A.BornOn = tex["born_on"].AsString();
                A.CharterMember = tex["chart_member"].AsString();
                A.ProfileImage = tex["profile_image"].AsUUID();
                A.Flags = (ProfileFlags)tex["flags"].AsInteger();
                A.ProfileURL = tex["profile_url"].AsString();

                return A;

            }

            #endregion Properties
        }

        /// <summary>
        /// Avatar interests including spoken languages, skills, and "want to"
        /// choices
        /// </summary>
        public struct Interests
        {
            /// <summary>Languages profile field</summary>
            public string LanguagesText;
            /// <summary></summary>
            // FIXME:
            public uint SkillsMask;
            /// <summary></summary>
            public string SkillsText;
            /// <summary></summary>
            // FIXME:
            public uint WantToMask;
            /// <summary></summary>
            public string WantToText;

            public OSD GetOSD()
            {
                OSDMap InterestsOSD = new OSDMap(5)
                {
                    ["languages_text"] = OSD.FromString(LanguagesText),
                    ["skills_mask"] = OSD.FromUInteger(SkillsMask),
                    ["skills_text"] = OSD.FromString(SkillsText),
                    ["want_to_mask"] = OSD.FromUInteger(WantToMask),
                    ["want_to_text"] = OSD.FromString(WantToText)
                };
                return InterestsOSD;
            }

            public static Interests FromOSD(OSD O)
            {
                Interests I = new Interests();
                OSDMap tex = (OSDMap)O;

                I.LanguagesText = tex["languages_text"].AsString();
                I.SkillsMask = tex["skills_mask"].AsUInteger();
                I.SkillsText = tex["skills_text"].AsString();
                I.WantToMask = tex["want_to_mask"].AsUInteger();
                I.WantToText = tex["want_to_text"].AsString();

                return I;
            }
        }

        /// <summary>
        /// The simulator can send down a list of attachments the avatar is wearing,
        /// this can be helpful in knowing when the avatar is ready to render.
        ///
        /// See also: Primitive.ChildCount
        /// </summary>
        public struct Attachment : IEquatable<Attachment>
        {
            /// <summary>
            /// The attachment point (see avatar_lad.xml for a list)
            /// </summary>
            public byte AttachmentPoint;
            /// <summary>
            /// The UUID (global) of the attachment on this point.
            /// </summary>
            public UUID AttachmentID;

            /// <summary>
            /// Determine if this is the same as another attachment object.
            /// </summary>
            /// <param name="other">Target for comparison</param>
            /// <returns>Are these attachments the same object?</returns>
            public bool Equals(Attachment other)
            {
                return other.AttachmentPoint == AttachmentPoint && other.AttachmentID == AttachmentID;
            }

            public override bool Equals(object obj)
            {
                return obj is Attachment objA && Equals(objA);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (AttachmentPoint.GetHashCode() * 397) ^ AttachmentID.GetHashCode();
                }
            }
        }

        #endregion Subclasses

        #region Public Members

        /// <summary>Groups that this avatar is a member of</summary>
        public List<UUID> Groups = new List<UUID>();
        /// <summary>Positive and negative ratings</summary>
        public Statistics ProfileStatistics;
        /// <summary>Avatar properties including about text, profile URL, image IDs and 
        /// publishing settings</summary>
        public AvatarProperties ProfileProperties;
        /// <summary>Avatar interests including spoken languages, skills, and "want to"
        /// choices</summary>
        public Interests ProfileInterests;
        /// <summary>Movement control flags for avatars. Typically not set or used by
        /// clients. To move your avatar, use Client.Self.Movement instead</summary>
        public AgentManager.ControlFlags ControlFlags;

        /// <summary>
        /// Contains the visual parameters describing the deformation of the avatar
        /// </summary>
        public byte[] VisualParameters = null;

        /// <summary>
        /// The avatars hover height (as indicated by the simulator)
        /// </summary>
        public Vector3 HoverHeight = Vector3.Zero;

        /// <summary>
        /// Appearance version. Value greater than 0 indicates using server side baking
        /// </summary>
        public byte AppearanceVersion = 0;

        /// <summary>
        /// Version of the Current Outfit Folder that the appearance is based on
        /// </summary>
        public int COFVersion = 0;

        /// <summary>
        /// Appearance flags. Introduced with server side baking, currently unused.
        /// </summary>
        public AppearanceFlags AppearanceFlags = AppearanceFlags.None;

        /// <summary>
        /// List of current avatar animations
        /// </summary>
        public List<Animation> Animations;

        /// <summary>
        /// List of (known) attachments, hinted by the simulator. See: https://jira.secondlife.com/browse/SL-20635
        /// </summary>
        public List<Attachment> Attachments;

        #endregion Public Members

        internal string _cachedName;
        internal string _cachedGroupName;

        #region Properties

        /// <summary>First name</summary>
        public string FirstName
        {
            get
            {
                for (int i = 0; i < NameValues.Length; i++)
                {
                    if (NameValues[i].Name == "FirstName" && NameValues[i].Type == NameValue.ValueType.String)
                        return (string)NameValues[i].Value;
                }

                return string.Empty;
            }
        }

        /// <summary>Last name</summary>
        public string LastName
        {
            get
            {
                for (int i = 0; i < NameValues.Length; i++)
                {
                    if (NameValues[i].Name == "LastName" && NameValues[i].Type == NameValue.ValueType.String)
                        return (string)NameValues[i].Value;
                }

                return string.Empty;
            }
        }

        /// <summary>Full name</summary>
        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedName))
                {
                    return _cachedName;
                }
                if (NameValues != null && NameValues.Length > 0)
                {
                    lock (NameValues)
                    {
                        string firstName = string.Empty;
                        string lastName = string.Empty;

                        for (int i = 0; i < NameValues.Length; i++)
                        {
                            if (NameValues[i].Name == "FirstName" && NameValues[i].Type == NameValue.ValueType.String)
                                firstName = (string)NameValues[i].Value;
                            else if (NameValues[i].Name == "LastName" && NameValues[i].Type == NameValue.ValueType.String)
                                lastName = (string)NameValues[i].Value;
                        }

                        if (firstName != string.Empty && lastName != string.Empty)
                        {
                            _cachedName = $"{firstName} {lastName}";
                            return _cachedName;
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>Active group</summary>
        public string GroupName
        {
            get
            {
                if (_cachedGroupName != null)
                {
                    return _cachedGroupName;
                }
                if (NameValues == null || NameValues.Length == 0)
                {
                    return _cachedGroupName = string.Empty;
                }
                else
                {
                    lock (NameValues)
                    {
                        for (int i = 0; i < NameValues.Length; i++)
                        {
                            if (NameValues[i].Name == "Title" && NameValues[i].Type == NameValue.ValueType.String)
                            {
                                _cachedGroupName = (string)NameValues[i].Value;
                                return _cachedGroupName;
                            }
                        }
                    }
                    return _cachedGroupName = string.Empty;
                }
            }
        }

        public override OSD GetOSD()
        {
            OSDMap Avi = (OSDMap)base.GetOSD();

            OSDArray grp = new OSDArray();
            Groups.ForEach(delegate(UUID u) { grp.Add(OSD.FromUUID(u)); });

            OSDArray vp = new OSDArray();

            foreach (byte t in VisualParameters)
            {
                vp.Add(OSD.FromInteger(t));
            }

            Avi["groups"] = grp;
            Avi["profile_statistics"] = ProfileStatistics.GetOSD();
            Avi["profile_properties"] = ProfileProperties.GetOSD();
            Avi["profile_interest"] = ProfileInterests.GetOSD();
            Avi["control_flags"] = OSD.FromInteger((byte)ControlFlags);
            Avi["visual_parameters"] = vp;
            Avi["first_name"] = OSD.FromString(FirstName);
            Avi["last_name"] = OSD.FromString(LastName);
            Avi["group_name"] = OSD.FromString(GroupName);

            return Avi;

        }

        public new static Avatar FromOSD(OSD O)
        {

            OSDMap tex = (OSDMap)O;

            Avatar A = new Avatar();
            
            Primitive P = Primitive.FromOSD(O);

            Type Prim = typeof(Primitive);

            FieldInfo[] Fields = Prim.GetFields();

            foreach (FieldInfo info in Fields)
            {
                Logger.Log("Field Matched in FromOSD: "+info.Name, Helpers.LogLevel.Debug);
                info.SetValue(A, info.GetValue(P));
            }            

            A.Groups = new List<UUID>();

            foreach (OSD U in (OSDArray)tex["groups"])
            {
                A.Groups.Add(U.AsUUID());
            }

            A.ProfileStatistics = Statistics.FromOSD(tex["profile_statistics"]);
            A.ProfileProperties = AvatarProperties.FromOSD(tex["profile_properties"]);
            A.ProfileInterests = Interests.FromOSD(tex["profile_interest"]);
            A.ControlFlags = (AgentManager.ControlFlags)tex["control_flags"].AsInteger();

            OSDArray vp = (OSDArray)tex["visual_parameters"];
            A.VisualParameters = new byte[vp.Count];

            for (int i = 0; i < vp.Count; i++)
            {
                A.VisualParameters[i] = (byte)vp[i].AsInteger();
            }

            // *********************From Code Above *******************************
            /*if (NameValues[i].Name == "FirstName" && NameValues[i].Type == NameValue.ValueType.String)
                              firstName = (string)NameValues[i].Value;
                          else if (NameValues[i].Name == "LastName" && NameValues[i].Type == NameValue.ValueType.String)
                              lastName = (string)NameValues[i].Value;*/
            // ********************************************************************

            A.NameValues = new NameValue[3];

            NameValue First = new NameValue
            {
                Name = "FirstName",
                Type = NameValue.ValueType.String,
                Value = tex["first_name"].AsString()
            };

            NameValue Last = new NameValue
            {
                Name = "LastName",
                Type = NameValue.ValueType.String,
                Value = tex["last_name"].AsString()
            };

            // ***************From Code Above***************
            // if (NameValues[i].Name == "Title" && NameValues[i].Type == NameValue.ValueType.String)
            // *********************************************

            NameValue Group = new NameValue
            {
                Name = "Title",
                Type = NameValue.ValueType.String,
                Value = tex["group_name"].AsString()
            };



            A.NameValues[0] = First;
            A.NameValues[1] = Last;
            A.NameValues[2] = Group;

            return A;


        }

        #endregion Properties

    }
}
