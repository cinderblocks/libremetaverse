/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2026, Sjofn LLC
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

#nullable enable

namespace LibreMetaverse
{
    /// <summary>
    /// The values of the <c>target</c> a script is compiled for when it is uploaded to a simulator, which also
    /// says which virtual machine the script runs on and in which language it is written.
    /// </summary>
    public static class ScriptCompileTarget
    {
        /// <summary>LSL source on the older LSO2 virtual machine</summary>
        public const string Lsl2 = "lsl2";
        /// <summary>LSL source on the Mono virtual machine</summary>
        public const string Mono = "mono";
        /// <summary>LSL source on the Luau virtual machine, the "LSL: 2025 VM"</summary>
        public const string LslLuau = "lsl-luau";
        /// <summary>Lua (SLua) source on the Luau virtual machine</summary>
        public const string Luau = "luau";

        /// <summary>The target for LSL source: <see cref="Mono"/> if it is true, otherwise <see cref="Lsl2"/></summary>
        /// <param name="mono">True if the script runs on the Mono virtual machine</param>
        public static string FromMono(bool mono) => mono ? Mono : Lsl2;

        /// <summary>
        /// The target a simulator reports a script as compiled for, in its <c>ScriptRunningReply</c>
        /// </summary>
        /// <param name="mono">The script runs on the Mono virtual machine</param>
        /// <param name="luau">The script runs on the Luau virtual machine</param>
        /// <param name="luauLanguage">The script is written in Lua rather than LSL; only meaningful if <paramref name="luau"/></param>
        public static string FromReply(bool mono, bool luau, bool luauLanguage)
        {
            if (luau) return luauLanguage ? Luau : LslLuau;
            return FromMono(mono);
        }

        /// <summary>
        /// Whether source compiled for a target is written in Lua, as opposed to LSL
        /// </summary>
        /// <param name="target">One of the targets of this class</param>
        public static bool IsLuaSource(string? target) =>
            string.Equals(target, Luau, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Whether a target runs on the Luau virtual machine, whatever language the source is in
        /// </summary>
        /// <param name="target">One of the targets of this class</param>
        public static bool IsLuauRuntime(string? target) =>
            IsLuaSource(target) || string.Equals(target, LslLuau, System.StringComparison.OrdinalIgnoreCase);
    }
}
