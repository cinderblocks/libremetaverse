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

using System;
using Microsoft.Extensions.Logging;

namespace LibreMetaverse.Voice.WebRTC
{
    // Simple ILogger implementation that forwards to IVoiceLogger
    internal class VoiceLogger : ILogger
    {
        private readonly IVoiceLogger _voiceLogger;
        private readonly string _category;

        public VoiceLogger(IVoiceLogger voiceLogger, string category)
        {
            _voiceLogger = voiceLogger ?? throw new ArgumentNullException(nameof(voiceLogger));
            _category = category ?? "SIPSorcery";
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                var msg = formatter(state, exception);
                if (!string.IsNullOrEmpty(_category)) msg = $"[{_category}] " + msg;
                _voiceLogger.Log(msg, logLevel, null);
                if (exception != null)
                {
                    _voiceLogger.Error(exception.ToString());
                }
            }
            catch { /* noop */ }
        }
    }

    internal class VoiceLoggerProvider : ILoggerProvider
    {
        private readonly IVoiceLogger _voiceLogger;

        public VoiceLoggerProvider(IVoiceLogger voiceLogger)
        {
            _voiceLogger = voiceLogger ?? throw new ArgumentNullException(nameof(voiceLogger));
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new VoiceLogger(_voiceLogger, categoryName);
        }

        public void Dispose() { }
    }
}
