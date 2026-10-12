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
using System.Runtime.CompilerServices;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    /// <summary>
    /// The packet decoder prints some fields by the names of enums that belong to the managers in this assembly,
    /// which the protocol assembly cannot refer to. They are given to it here, when this assembly is first used.
    /// </summary>
    internal static class PacketDecoderRegistrations
    {
        [ModuleInitializer]
        internal static void Register()
        {
            PacketDecoder.RegisterFieldDecoder("ScriptControlChange.Data.Controls", DecodeScriptControls);
            PacketDecoder.RegisterFieldDecoder("ControlFlags", DecodeControlFlags);
            PacketDecoder.RegisterFieldDecoder("DirClassifiedQuery.QueryData.QueryFlags", DecodeDirClassifiedQueryFlags);
            PacketDecoder.RegisterFieldDecoder("QueryData.QueryFlags", DecodeDirQueryFlags);
            PacketDecoder.RegisterFieldDecoder("QueryData.SearchType", DecodeSearchTypeFlags);
            PacketDecoder.RegisterFieldDecoder("ClassifiedFlags", DecodeDirClassifiedFlags);
            PacketDecoder.RegisterFieldDecoder("EventFlags", DecodeEventFlags);
        }

        private static string DecodeEventFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-3} {"(" + (DirectoryManager.EventFlags) (uint) fieldData + ")",-36} [EventFlags]";
        }

        private static string DecodeDirQueryFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.DirFindFlags) (uint) fieldData + ")",-29} [DirectoryManager.DirFindFlags]";
        }

        private static string DecodeDirClassifiedQueryFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.ClassifiedQueryFlags) (uint) fieldData + ")",-29} [ClassifiedQueryFlags]";
        }

        private static string DecodeDirClassifiedFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.ClassifiedFlags) (byte) fieldData + ")",-29} [ClassifiedFlags]";
        }

        private static string DecodeSearchTypeFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.SearchTypeFlags) (uint) fieldData + ")",-29} [DirectoryManager.SearchTypeFlags]";
        }

        private static string DecodeScriptControls(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(uint) fieldData,-10} {"(" + (AgentManager.ControlFlags) (uint) fieldData + ")",-29} [AgentManager.ControlFlags]";
        }

        private static string DecodeControlFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (AgentManager.ControlFlags) (uint) fieldData + ")",-29} [{fieldData.GetType().Name}]";
        }
    }
}
