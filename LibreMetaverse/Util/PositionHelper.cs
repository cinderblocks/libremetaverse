/*
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

using OpenMetaverse;
using System.Collections.Generic;

namespace LibreMetaverse
{
    /// <summary>
    /// Helper class for position and location calculations
    /// </summary>
    /// <remarks>This may end up in LibreMetaverse</remarks>
    public static class PositionHelper
    {
        /// <summary>
        /// Convert simulator handle and local position to global position
        /// </summary>
        public static Vector3d ToGlobalPosition(ulong simHandle, Vector3 localPos)
        {
            Utils.LongToUInts(simHandle, out var globalX, out var globalY);

            return new Vector3d(
                globalX + localPos.X,
                globalY + localPos.Y,
                localPos.Z);
        }

        /// <summary>
        /// Convert global position to local position for a specific simulator
        /// </summary>
        public static Vector3 ToLocalPosition(ulong simHandle, Vector3d globalPos)
        {
            Utils.LongToUInts(simHandle, out var globalX, out var globalY);

            return new Vector3(
                (float)(globalPos.X - globalX),
                (float)(globalPos.Y - globalY),
                (float)globalPos.Z);
        }

        /// <summary>
        /// Get global position from a simulator and local position
        /// </summary>
        public static Vector3d GlobalPosition(Simulator sim, Vector3 localPos)
        {
            return sim == null ? Vector3d.Zero : ToGlobalPosition(sim.Handle, localPos);
        }

        /// <summary>
        /// Get global position from a primitive
        /// </summary>
        public static Vector3d GlobalPosition(Primitive prim, Simulator currentSim)
        {
            return (prim == null || currentSim == null) ? Vector3d.Zero
                : GlobalPosition(currentSim, prim.Position);
        }

        /// <summary>
        /// Get the region handle (aligned to region grid) for given global X/Y coordinates
        /// </summary>
        public static ulong RegionHandleFromGlobal(uint globalX, uint globalY)
        {
            uint x = (globalX / Simulator.DefaultRegionSizeX) * Simulator.DefaultRegionSizeX;
            uint y = (globalY / Simulator.DefaultRegionSizeY) * Simulator.DefaultRegionSizeY;
            return Utils.UIntsToLong(x, y);
        }

        /// <summary>
        /// Get the region handle for a global position
        /// </summary>
        public static ulong RegionHandleFromGlobalPosition(Vector3d globalPos)
        {
            return RegionHandleFromGlobal((uint)globalPos.X, (uint)globalPos.Y);
        }

        /// <summary>
        /// Calculate the position of an avatar accounting for parent prim (if sitting)
        /// </summary>
        public static Vector3 GetAvatarPosition(Simulator sim, Avatar avatar)
        {
            if (avatar == null) return Vector3.Zero;

            if (avatar.ParentID == 0)
            {
                return avatar.Position;
            }

            if (sim?.ObjectsPrimitives.TryGetValue(avatar.ParentID, out var prim) == true)
            {
                return prim.Position + avatar.Position * prim.Rotation;
            }

            return avatar.Position;
        }

        /// <summary>
        /// Calculate the position of a primitive accounting for parent prim
        /// </summary>
        public static Vector3 GetPrimPosition(Simulator sim, Primitive prim)
        {
            if (prim == null) return Vector3.Zero;

            if (prim.ParentID == 0)
            {
                return prim.Position;
            }

            if (sim?.ObjectsPrimitives.TryGetValue(prim.ParentID, out var parent) == true)
            {
                return parent.Position + prim.Position * parent.Rotation;
            }

            return prim.Position;
        }

        /// <summary>
        /// Format region coordinates as a display string
        /// </summary>
        public static string FormatRegionCoordinates(string regionName, int? x = null, int? y = null, int? z = null)
        {
            return regionName + FormatCoordinates(x, y, z);
        }

        /// <summary>
        /// Create a formatted coordinate string from optional X, Y, Z values
        /// </summary>
        public static string FormatCoordinates(int? x = null, int? y = null, int? z = null)
        {
            if (x == null && y == null && z == null) return string.Empty;

            var coords = new List<string>();
            if (x.HasValue) coords.Add(x.Value.ToString());
            if (y.HasValue) coords.Add(y.Value.ToString());
            if (z.HasValue) coords.Add(z.Value.ToString());

            return coords.Count > 0 ? $" ({string.Join(",", coords)})" : string.Empty;
        }
    }
}
