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
using System.Globalization;
using System.Text;

namespace LibreMetaverse.Voice
{
    /// <summary>
    /// Represents a single Voice Session to the Vivox service.
    /// </summary>
    public class VoiceSession
    {
        private static Dictionary<string, VoiceParticipant> _knownParticipants;
        public string RegionName;
        public bool IsSpatial { get; }

        public VoiceGateway Connector { get; }
        public string Handle { get; }

        public event System.EventHandler OnParticipantAdded;
        public event System.EventHandler OnParticipantUpdate;
        public event System.EventHandler OnParticipantRemoved;

        public VoiceSession(VoiceGateway conn, string handle)
        {
            Handle = handle;
            Connector = conn;

            IsSpatial = true;
            _knownParticipants = new Dictionary<string, VoiceParticipant>();
        }

        /// <summary>
        /// Close this session.
        /// </summary>
        internal void Close()
        {
            lock (_knownParticipants)
            {
                _knownParticipants.Clear();
            }
        }

        internal void ParticipantUpdate(string uri,
            bool isMuted,
            bool isSpeaking,
            int volume,
            float energy)
        {
            lock (_knownParticipants)
            {
                // Locate in this session
                var p = FindParticipant(uri);
                if (p == null) return;

                // Set properties
                p.SetProperties(isSpeaking, isMuted, energy);

                // Inform interested parties.
                OnParticipantUpdate?.Invoke(p, null);
            }
        }

        internal void AddParticipant(string uri)
        {
            lock (_knownParticipants)
            {
                var p = FindParticipant(uri);

                // We expect that to come back null.  If it is not
                // null, this is a duplicate
                if (p != null)
                {
                    return;
                }

                // It was not found, so add it.
                p = new VoiceParticipant(uri, this);
                _knownParticipants.Add(uri, p);

                /* TODO
                           // Fill in the name.
                           if (p.Name == null || p.Name.StartsWith("Loading..."))
                                   p.Name = control.instance.getAvatarName(p.ID);
                               return p;
               */

                // Inform interested parties.
                OnParticipantAdded?.Invoke(p, null);
            }
        }

        internal void RemoveParticipant(string uri)
        {
            lock (_knownParticipants)
            {
                var p = FindParticipant(uri);
                if (p == null) return;

                // Remove from list for this session.
                _knownParticipants.Remove(uri);

                // Inform interested parties.
                OnParticipantRemoved?.Invoke(p, null);
            }
        }

        /// <summary>
        /// Look up an existing Participants in this session
        /// </summary>
        /// <param name="puri"></param>
        /// <returns></returns>
        private VoiceParticipant FindParticipant(string puri)
        {
            if (_knownParticipants.ContainsKey(puri))
                return _knownParticipants[puri];

            return null;
        }

        public void Set3DPosition(VoicePosition speakerPosition, VoicePosition listenerPosition)
        {
            Connector.SessionSet3DPosition(Handle, speakerPosition, listenerPosition);
        }
    }

    public partial class VoiceGateway
    {
        /// <summary>
        /// Create a Session
        /// Sessions typically represent a connection to a media session with one or more
        /// participants. This is used to generate an �outbound� call to another user or
        /// channel. The specifics depend on the media types involved. A session handle is
        /// required to control the local user functions within the session (or remote
        /// users if the current account has rights to do so). Currently creating a
        /// session automatically connects to the audio media, there is no need to call
        /// Session.Connect at this time, this is reserved for future use.
        /// </summary>
        /// <param name="AccountHandle">Handle returned from successful Connector �create� request</param>
        /// <param name="session_uri">This is the URI of the terminating point of the session (ie who/what is being called)</param>
        /// <param name="Name">This is the display name of the entity being called (user or channel)</param>
        /// <param name="Password">Only needs to be supplied when the target URI is password protected</param>
        /// <param name="PasswordHashAlgorithm">This indicates the format of the password as passed in. This can either be
        /// �ClearText� or �SHA1UserName�. If this element does not exist, it is assumed to be �ClearText�. If it is
        /// �SHA1UserName�, the password as passed in is the SHA1 hash of the password and username concatenated together,
        /// then base64 encoded, with the final �=� character stripped off.</param>
        /// <param name="JoinAudio"></param>
        /// <param name="JoinText"></param>
        /// <returns></returns>
        public int SessionCreate(string AccountHandle, string session_uri, string Name, string Password,
            bool JoinAudio, bool JoinText, string PasswordHashAlgorithm)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("AccountHandle", AccountHandle));
            sb.Append(VoiceGateway.MakeXML("URI", session_uri));
            sb.Append(VoiceGateway.MakeXML("Name", Name));
            if (!string.IsNullOrEmpty(Password))
            {
                sb.Append(VoiceGateway.MakeXML("Password", Password));
                sb.Append(VoiceGateway.MakeXML("PasswordHashAlgorithm", PasswordHashAlgorithm));
            }
            sb.Append(VoiceGateway.MakeXML("ConnectAudio", JoinAudio ? "true" : "false"));
            sb.Append(VoiceGateway.MakeXML("ConnectText", JoinText ? "true" : "false"));
            sb.Append(VoiceGateway.MakeXML("JoinAudio", JoinAudio ? "true" : "false"));
            sb.Append(VoiceGateway.MakeXML("JoinText", JoinText ? "true" : "false"));
            sb.Append(VoiceGateway.MakeXML("VoiceFontID", "0"));

            return Request("Session.Create.1", sb.ToString());
        }

        /// <summary>
        /// Used to accept a call
        /// </summary>
        /// <param name="SessionHandle">SessionHandle such as received from SessionNewEvent</param>
        /// <param name="AudioMedia">"default"</param>
        /// <returns></returns>
        public int SessionConnect(string SessionHandle, string AudioMedia)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("SessionHandle", SessionHandle));
            sb.Append(VoiceGateway.MakeXML("AudioMedia", AudioMedia));
            return Request("Session.Connect.1", sb.ToString());
        }

        /// <summary>
        /// This command is used to start the audio render process, which will then play
        /// the passed in file through the selected audio render device. This command
        /// should not be issued if the user is on a call.
        /// </summary>
        /// <param name="soundFilePath">The fully qualified path to the sound file.</param>
        /// <param name="Loop">True if the file is to be played continuously and false if it is should be played once.</param>
        /// <returns></returns>
        public int SessionRenderAudioStart(string soundFilePath, bool Loop)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("SoundFilePath", soundFilePath));
            sb.Append(VoiceGateway.MakeXML("Loop", Loop ? "1" : "0"));
            return Request("Session.RenderAudioStart.1", sb.ToString());
        }

        /// <summary>
        /// This command is used to stop the audio render process.
        /// </summary>
        /// <param name="SoundFilePath">The fully qualified path to the sound file issued in the start render command.</param>
        /// <returns></returns>
        public int SessionRenderAudioStop(string SoundFilePath)
        {
            var requestXml = VoiceGateway.MakeXML("SoundFilePath", SoundFilePath);
            return Request("Session.RenderAudioStop.1", requestXml);
        }

        /// <summary>
        /// This is used to �end� an established session (i.e. hang-up or disconnect).
        /// </summary>
        /// <param name="SessionHandle">Handle returned from successful Session �create� request or a SessionNewEvent</param>
        /// <returns></returns>
        public int SessionTerminate(string SessionHandle)
        {
            var requestXml = VoiceGateway.MakeXML("SessionHandle", SessionHandle);
            return Request("Session.Terminate.1", requestXml);
        }

        /// <summary>
        /// Set the combined speaking and listening position in 3D space.
        /// </summary>
        /// <param name="SessionHandle">Handle returned from successful Session �create� request or a SessionNewEvent</param>
        /// <param name="SpeakerPosition">Speaking position</param>
        /// <param name="ListenerPosition">Listening position</param>
        /// <returns></returns>
        public int SessionSet3DPosition(string SessionHandle, VoicePosition SpeakerPosition, VoicePosition ListenerPosition)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("SessionHandle", SessionHandle));
            sb.Append("<SpeakerPosition>");
            sb.Append("<Position>");
            sb.Append(VoiceGateway.MakeXML("X", SpeakerPosition.Position.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", SpeakerPosition.Position.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", SpeakerPosition.Position.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</Position>");
            sb.Append("<Velocity>");
            sb.Append(VoiceGateway.MakeXML("X", SpeakerPosition.Velocity.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", SpeakerPosition.Velocity.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", SpeakerPosition.Velocity.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</Velocity>");
            sb.Append("<AtOrientation>");
            sb.Append(VoiceGateway.MakeXML("X", SpeakerPosition.AtOrientation.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", SpeakerPosition.AtOrientation.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", SpeakerPosition.AtOrientation.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</AtOrientation>");
            sb.Append("<UpOrientation>");
            sb.Append(VoiceGateway.MakeXML("X", SpeakerPosition.UpOrientation.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", SpeakerPosition.UpOrientation.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", SpeakerPosition.UpOrientation.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</UpOrientation>");
            sb.Append("<LeftOrientation>");
            sb.Append(VoiceGateway.MakeXML("X", SpeakerPosition.LeftOrientation.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", SpeakerPosition.LeftOrientation.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", SpeakerPosition.LeftOrientation.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</LeftOrientation>");
            sb.Append("</SpeakerPosition>");
            sb.Append("<ListenerPosition>");
            sb.Append("<Position>");
            sb.Append(VoiceGateway.MakeXML("X", ListenerPosition.Position.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", ListenerPosition.Position.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", ListenerPosition.Position.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</Position>");
            sb.Append("<Velocity>");
            sb.Append(VoiceGateway.MakeXML("X", ListenerPosition.Velocity.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", ListenerPosition.Velocity.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", ListenerPosition.Velocity.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</Velocity>");
            sb.Append("<AtOrientation>");
            sb.Append(VoiceGateway.MakeXML("X", ListenerPosition.AtOrientation.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", ListenerPosition.AtOrientation.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", ListenerPosition.AtOrientation.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</AtOrientation>");
            sb.Append("<UpOrientation>");
            sb.Append(VoiceGateway.MakeXML("X", ListenerPosition.UpOrientation.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", ListenerPosition.UpOrientation.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", ListenerPosition.UpOrientation.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</UpOrientation>");
            sb.Append("<LeftOrientation>");
            sb.Append(VoiceGateway.MakeXML("X", ListenerPosition.LeftOrientation.X.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Y", ListenerPosition.LeftOrientation.Y.ToString(CultureInfo.InvariantCulture)));
            sb.Append(VoiceGateway.MakeXML("Z", ListenerPosition.LeftOrientation.Z.ToString(CultureInfo.InvariantCulture)));
            sb.Append("</LeftOrientation>");
            sb.Append("</ListenerPosition>");
            return Request("Session.Set3DPosition.1", sb.ToString());
        }

        /// <summary>
        /// Set User Volume for a particular user. Does not affect how other users hear that user.
        /// </summary>
        /// <param name="SessionHandle">Handle returned from successful Session �create� request or a SessionNewEvent</param>
        /// <param name="ParticipantURI"></param>
        /// <param name="Volume">The level of the audio, a number between -100 and 100 where 0 represents �normal� speaking volume</param>
        /// <returns></returns>
        public int SessionSetParticipantVolumeForMe(string SessionHandle, string ParticipantURI, int Volume)
        {
            var sb = new StringBuilder();
            sb.Append(VoiceGateway.MakeXML("SessionHandle", SessionHandle));
            sb.Append(VoiceGateway.MakeXML("ParticipantURI", ParticipantURI));
            sb.Append(VoiceGateway.MakeXML("Volume", Volume.ToString()));
            return Request("Session.SetParticipantVolumeForMe.1", sb.ToString());
        }
    }
}
