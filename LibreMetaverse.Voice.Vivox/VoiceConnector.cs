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

using System.Text;

namespace LibreMetaverse.Voice.Vivox
{
    public partial class VoiceGateway
    {
        /// <summary>
        /// This is used to initialize and stop the Connector as a whole. The Connector
        /// Create call must be completed successfully before any other requests are made
        /// (typically during application initialization). The shutdown should be called
        /// when the application is shutting down to gracefully release resources
        /// </summary>
        /// <param name="clientName">A string value indicting the Application name</param>
        /// <param name="accountManagementServer">URL for the management server</param>
        /// <param name="logging">LoggingSettings</param>
        /// <param name="maximumPort"></param>
        /// <param name="minimumPort"></param>
        public int ConnectorCreate(string clientName, string accountManagementServer, ushort minimumPort,
            ushort maximumPort, VoiceLoggingSettings logging)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("ClientName", clientName));
            sb.Append(VoiceGateway.MakeXML("AccountManagementServer", accountManagementServer));
            sb.Append(VoiceGateway.MakeXML("MinimumPort", minimumPort.ToString()));
            sb.Append(VoiceGateway.MakeXML("MaximumPort", maximumPort.ToString()));
            sb.Append(VoiceGateway.MakeXML("Mode", "Normal"));
            sb.Append("<Logging>");
            sb.Append(VoiceGateway.MakeXML("Enabled", logging.Enabled ? "true" : "false"));
            sb.Append(VoiceGateway.MakeXML("Folder", logging.Folder));
            sb.Append(VoiceGateway.MakeXML("FileNamePrefix", logging.FileNamePrefix));
            sb.Append(VoiceGateway.MakeXML("FileNameSuffix", logging.FileNameSuffix));
            sb.Append(VoiceGateway.MakeXML("LogLevel", logging.LogLevel.ToString()));
            sb.Append("</Logging>");
            return Request("Connector.Create.1", sb.ToString());
        }

        /// <summary>
        /// Shutdown Connector -- Should be called when the application is shutting down
        /// to gracefully release resources
        /// </summary>
        /// <param name="connectorHandle">Handle returned from successful Connector �create� request</param>
        public int ConnectorInitiateShutdown(string connectorHandle)
        {
            var requestXml = VoiceGateway.MakeXML("ConnectorHandle", connectorHandle);
            return Request("Connector.InitiateShutdown.1", requestXml);
        }

        /// <summary>
        /// Mute or unmute the microphone
        /// </summary>
        /// <param name="connectorHandle">Handle returned from successful Connector �create� request</param>
        /// <param name="mute">true (mute) or false (unmute)</param>
        public int ConnectorMuteLocalMic(string connectorHandle, bool mute)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("ConnectorHandle", connectorHandle));
            sb.Append(VoiceGateway.MakeXML("Value", mute ? "true" : "false"));
            return Request("Connector.MuteLocalMic.1", sb.ToString());
        }

        /// <summary>
        /// Mute or unmute the speaker
        /// </summary>
        /// <param name="connectorHandle">Handle returned from successful Connector �create� request</param>
        /// <param name="mute">true (mute) or false (unmute)</param>
        public int ConnectorMuteLocalSpeaker(string connectorHandle, bool mute)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("ConnectorHandle", connectorHandle));
            sb.Append(VoiceGateway.MakeXML("Value", mute ? "true" : "false"));
            return Request("Connector.MuteLocalSpeaker.1", sb.ToString());
        }

        /// <summary>
        /// Set microphone volume
        /// </summary>
        /// <param name="connectorHandle">Handle returned from successful Connector �create� request</param>
        /// <param name="value">The level of the audio, a number between -100 and 100 where
        /// 0 represents �normal� speaking volume</param>
        public int ConnectorSetLocalMicVolume(string connectorHandle, int value)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("ConnectorHandle", connectorHandle));
            sb.Append(VoiceGateway.MakeXML("Value", value.ToString()));
            return Request("Connector.SetLocalMicVolume.1", sb.ToString());
        }

        /// <summary>
        /// Set local speaker volume
        /// </summary>
        /// <param name="connectorHandle">Handle returned from successful Connector �create� request</param>
        /// <param name="value">The level of the audio, a number between -100 and 100 where
        /// 0 represents �normal� speaking volume</param>
        public int ConnectorSetLocalSpeakerVolume(string connectorHandle, int value)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("ConnectorHandle", connectorHandle));
            sb.Append(VoiceGateway.MakeXML("Value", value.ToString()));
            return Request("Connector.SetLocalSpeakerVolume.1", sb.ToString());
        }
    }
}
