/*
 * Copyright (c) 2025, Sjofn LLC
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

using Microsoft.Extensions.Logging;
using OpenMetaverse;

namespace LibreMetaverse.Voice.WebRTC
{
    public class OpenMetaverseVoiceLogger : IVoiceLogger
    {
        private const string _prefix = "[Voice.WebRTC] ";

        private string FormatMessage(string message, GridClient client)
        {
            if (client != null && client.Settings.LOG_NAMES && client.Self?.Name != null)
            {
                return $"{_prefix}[{client.Self.Name}] {message}";
            }
            return _prefix + message;
        }

        public void Log(string message, LogLevel level, GridClient client = null)
        {
            Logger.Log(FormatMessage(message, client), level, (GridClient)null, (System.Exception)null);
        }

        public void Info(string message, GridClient client = null)
        {
            Logger.Info(FormatMessage(message, client), (GridClient)null);
        }

        public void Warn(string message, GridClient client = null)
        {
            Logger.Warn(FormatMessage(message, client), (GridClient)null);
        }

        public void Debug(string message, GridClient client = null)
        {
            Logger.Debug(FormatMessage(message, client), (GridClient)null);
        }

        public void Error(string message, GridClient client = null)
        {
            Logger.Error(FormatMessage(message, client), (GridClient)null);
        }
    }
}