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

namespace LibreMetaverse
{
    /// <summary>Describes tasks returned in LandStatReply</summary>
    public class EstateTask
    {
        public Vector3 Position;
        public float Score;
        public float MonoScore;
        public UUID TaskID;
        public uint TaskLocalID;
        public string TaskName = string.Empty;
        public string OwnerName = string.Empty;
    }

    /// <summary>Weekday flags for a weekly region restart schedule, matching the reference viewer's
    /// day-letter scheme (llfloaterregionrestartschedule.cpp CHECKBOX_PREFIXES: s,m,t,w,r,f,a).</summary>
    [Flags]
    public enum RegionRestartDays
    {
        None = 0,
        Sunday = 1 << 0,
        Monday = 1 << 1,
        Tuesday = 1 << 2,
        Wednesday = 1 << 3,
        Thursday = 1 << 4,
        Friday = 1 << 5,
        Saturday = 1 << 6,
        All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday
    }

    /// <summary>A region's automatic restart schedule, as returned/set by the RegionSchedule capability.</summary>
    public class RegionRestartSchedule
    {
        /// <summary>True for a daily restart; false for a weekly schedule on specific <see cref="Days"/>.</summary>
        public bool IsDaily { get; set; }
        /// <summary>Days of the week the region restarts on. Only meaningful when <see cref="IsDaily"/>
        /// is false; an empty set with IsDaily false clears/disables the schedule.</summary>
        public RegionRestartDays Days { get; set; }
        /// <summary>Time of day the restart occurs, relative to midnight UTC.</summary>
        public TimeSpan Time { get; set; }
    }
}
