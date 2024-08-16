/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2024, Sjofn LLC.
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

using System.Collections.Generic;
using System.Threading;

namespace LibreMetaverse.Voice
{
    public partial class VoiceManager
    {
        /// <summary>Amount of time to wait for the voice daemon to respond.
        /// The value needs to stay relatively high because some of the calls
        /// require the voice daemon to make remote queries before replying</summary>
        public readonly int BlockingTimeout = 30 * 1000;

        private readonly Dictionary<int, AutoResetEvent> _events = new Dictionary<int, AutoResetEvent>();

        public List<string> CaptureDevices()
        {
            var evt = new AutoResetEvent(false);
            _events[_commandCookie] = evt;

            if (RequestCaptureDevices() != -1)
                return evt.WaitOne(BlockingTimeout, false) ? CurrentCaptureDevices() : new List<string>();
            
            _events.Remove(_commandCookie);
            return new List<string>();

        }

        public List<string> RenderDevices()
        {
            var evt = new AutoResetEvent(false);
            _events[_commandCookie] = evt;

            if (RequestRenderDevices() != -1)
                return evt.WaitOne(BlockingTimeout, false) ? CurrentRenderDevices() : new List<string>();
            
            _events.Remove(_commandCookie);
            return new List<string>();

        }

        public string CreateConnector(out int status)
        {
            status = 0;

            var evt = new AutoResetEvent(false);
            _events[_commandCookie] = evt;

            if (RequestCreateConnector() == -1)
            {
                _events.Remove(_commandCookie);
                return string.Empty;
            }

            var success = evt.WaitOne(BlockingTimeout, false);
            status = _statusCode;

            return success && _statusCode == 0 ? _connectorHandle : string.Empty;
        }

        public string Login(string accountName, string password, string connectorHandle, out int status)
        {
            status = 0;

            var evt = new AutoResetEvent(false);
            _events[_commandCookie] = evt;

            if (RequestLogin(accountName, password, connectorHandle) == -1)
            {
                _events.Remove(_commandCookie);
                return string.Empty;
            }

            var success = evt.WaitOne(BlockingTimeout, false);
            status = _statusCode;

            return success && _statusCode == 0 ? _accountHandle : string.Empty;
        }

        private void RegisterCallbacks()
        {
            OnCaptureDevices += VoiceManager_OnCaptureDevices;
            OnRenderDevices += VoiceManager_OnRenderDevices;
            OnConnectorCreated += VoiceManager_OnConnectorCreated;
            OnLogin += VoiceManager_OnLogin;
        }

        #region Callbacks

        private void VoiceManager_OnCaptureDevices(int cookie, int statusCode, string statusString, string currentDevice)
        {
            if (_events.TryGetValue(cookie, out AutoResetEvent value))
                value.Set();
        }

        private void VoiceManager_OnRenderDevices(int cookie, int statusCode, string statusString, string currentDevice)
        {
            if (_events.TryGetValue(cookie, out AutoResetEvent value))
                value.Set();
        }

        private void VoiceManager_OnConnectorCreated(int cookie, int statusCode, string statusString, string connectorHandle)
        {
            if (_events.TryGetValue(cookie, out AutoResetEvent value))
                value.Set();
        }

        private void VoiceManager_OnLogin(int cookie, int statusCode, string statusString, string accountHandle)
        {
            if (_events.TryGetValue(cookie, out AutoResetEvent value))
                value.Set();
        }

        #endregion Callbacks
    }
}