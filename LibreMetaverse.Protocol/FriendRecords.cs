/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
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

namespace LibreMetaverse
{
    [Flags]
    public enum FriendRights : int
    {
        /// <summary>The avatar has no rights</summary>
        None = 0,
        /// <summary>The avatar can see the online status of the target avatar</summary>
        CanSeeOnline = 1,
        /// <summary>The avatar can see the location of the target avatar on the map</summary>
        CanSeeOnMap = 2,
        /// <summary>The avatar can modify the objects of the target avatar </summary>
        CanModifyObjects = 4
    }

    /// <summary>
    /// This class holds information about an avatar in the friends list.  There are two ways 
    /// to interface to this class.  The first is through the set of boolean properties.  This is the typical
    /// way clients of this class will use it.  The second interface is through two bitflag properties,
    /// TheirFriendsRights and MyFriendsRights
    /// </summary>
    public class FriendInfo
    {
        private bool m_canSeeMeOnline;
        private bool m_canSeeMeOnMap;

        #region Properties

        /// <summary>
        /// System ID of the avatar
        /// </summary>
        public UUID UUID { get; }

        /// <summary>
        /// full name of the avatar
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// True if the avatar is online
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// True if the friend can see if I am online
        /// </summary>
        public bool CanSeeMeOnline
        {
            get { return m_canSeeMeOnline; }
            set
            {
                m_canSeeMeOnline = value;

                // if I can't see them online, then I can't see them on the map
                if (!m_canSeeMeOnline)
                    m_canSeeMeOnMap = false;
            }
        }

        /// <summary>
        /// True if the friend can see me on the map 
        /// </summary>
        public bool CanSeeMeOnMap
        {
            get { return m_canSeeMeOnMap; }
            set
            {
                // if I can't see them online, then I can't see them on the map
                if (m_canSeeMeOnline)
                    m_canSeeMeOnMap = value;
            }
        }

        /// <summary>
        /// True if the freind can modify my objects
        /// </summary>
        public bool CanModifyMyObjects { get; set; }

        /// <summary>
        /// True if I can see if my friend is online
        /// </summary>
        public bool CanSeeThemOnline { get; private set; }

        /// <summary>
        /// True if I can see if my friend is on the map
        /// </summary>
        public bool CanSeeThemOnMap { get; private set; }

        /// <summary>
        /// True if I can modify my friend's objects
        /// </summary>
        public bool CanModifyTheirObjects { get; private set; }

        /// <summary>
        /// My friend's rights represented as bitmapped flags
        /// </summary>
        public FriendRights TheirFriendRights
        {
            get
            {
                FriendRights results = FriendRights.None;
                if (m_canSeeMeOnline)
                    results |= FriendRights.CanSeeOnline;
                if (m_canSeeMeOnMap)
                    results |= FriendRights.CanSeeOnMap;
                if (CanModifyMyObjects)
                    results |= FriendRights.CanModifyObjects;

                return results;
            }
            set
            {
                m_canSeeMeOnline = (value & FriendRights.CanSeeOnline) != 0;
                m_canSeeMeOnMap = (value & FriendRights.CanSeeOnMap) != 0;
                CanModifyMyObjects = (value & FriendRights.CanModifyObjects) != 0;
            }
        }

        /// <summary>
        /// My rights represented as bitmapped flags
        /// </summary>
        public FriendRights MyFriendRights
        {
            get
            {
                FriendRights results = FriendRights.None;
                if (CanSeeThemOnline)
                    results |= FriendRights.CanSeeOnline;
                if (CanSeeThemOnMap)
                    results |= FriendRights.CanSeeOnMap;
                if (CanModifyTheirObjects)
                    results |= FriendRights.CanModifyObjects;

                return results;
            }
            set
            {
                CanSeeThemOnline = (value & FriendRights.CanSeeOnline) != 0;
                CanSeeThemOnMap = (value & FriendRights.CanSeeOnMap) != 0;
                CanModifyTheirObjects = (value & FriendRights.CanModifyObjects) != 0;
            }
        }

        #endregion Properties

        /// <summary>
        /// Used internally when building the initial list of friends at login time
        /// </summary>
        /// <param name="id">System ID of the avatar being represented</param>
        /// <param name="theirRights">Rights the friend has to see you online and to modify your objects</param>
        /// <param name="myRights">Rights you have to see your friend online and to modify their objects</param>
        internal FriendInfo(UUID id, FriendRights theirRights, FriendRights myRights)
        {
            UUID = id;
            m_canSeeMeOnline = (theirRights & FriendRights.CanSeeOnline) != 0;
            m_canSeeMeOnMap = (theirRights & FriendRights.CanSeeOnMap) != 0;
            CanModifyMyObjects = (theirRights & FriendRights.CanModifyObjects) != 0;

            CanSeeThemOnline = (myRights & FriendRights.CanSeeOnline) != 0;
            CanSeeThemOnMap = (myRights & FriendRights.CanSeeOnMap) != 0;
            CanModifyTheirObjects = (myRights & FriendRights.CanModifyObjects) != 0;
        }

        /// <summary>
        /// FriendInfo represented as a string
        /// </summary>
        /// <returns><see cref="string"/> representation of both my rights and my friends rights</returns>
        public override string ToString()
        {
            return !string.IsNullOrEmpty(Name) 
                ? $"{Name} (Their Rights: {TheirFriendRights}, My Rights: {MyFriendRights})" 
                : $"{UUID} (Their Rights: {TheirFriendRights}, My Rights: {MyFriendRights})";
        }
    }
}
