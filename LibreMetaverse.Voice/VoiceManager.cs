/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2022, Sjofn LLC.
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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Xml;
using OpenMetaverse.StructuredData;
using OpenMetaverse;
using OpenMetaverse.Http;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;

namespace LibreMetaverse.Voice
{
    public enum VoiceStatus
    {
        StatusLoginRetry,
        StatusLoggedIn,
        StatusJoining,
        StatusJoined,
        StatusLeftChannel,
        BeginErrorStatus,
        ErrorChannelFull,
        ErrorChannelLocked,
        ErrorNotAvailable,
        ErrorUnknown
    }

    public enum VoiceServiceType
    {
        /// <summary>Unknown voice service level</summary>
        Unknown,
        /// <summary>Spatialized local chat</summary>
        TypeA,
        /// <summary>Remote multi-party chat</summary>
        TypeB,
        /// <summary>One-to-one and small group chat</summary>
        TypeC
    }

    public partial class VoiceManager
    {
        public const int VOICE_MAJOR_VERSION = 1;
        public const string DAEMON_ARGS = " -p tcp -h -c -ll ";
        public const int DAEMON_LOG_LEVEL = 1;
        public const int DAEMON_PORT = 44124;
        public const string VOICE_RELEASE_SERVER = "bhr.vivox.com";
        public const string VOICE_DEBUG_SERVER = "bhd.vivox.com";
        public const string REQUEST_TERMINATOR = "\n\n\n";

        public delegate void LoginStateChangeCallback(int cookie, string accountHandle, int statusCode, string statusString, int state);
        public delegate void NewSessionCallback(int cookie, string accountHandle, string eventSessionHandle, int state, string nameString, string uriString);
        public delegate void SessionStateChangeCallback(int cookie, string uriString, int statusCode, string statusString, string eventSessionHandle, int state, bool isChannel, string nameString);
        public delegate void ParticipantStateChangeCallback(int cookie, string uriString, int statusCode, string statusString, int state, string nameString, string displayNameString, int participantType);
        public delegate void ParticipantPropertiesCallback(int cookie, string uriString, int statusCode, string statusString, bool isLocallyMuted, bool isModeratorMuted, bool isSpeaking, int volume, float energy);
        public delegate void AuxAudioPropertiesCallback(int cookie, float energy);
        public delegate void BasicActionCallback(int cookie, int statusCode, string statusString);
        public delegate void ConnectorCreatedCallback(int cookie, int statusCode, string statusString, string connectorHandle);
        public delegate void LoginCallback(int cookie, int statusCode, string statusString, string accountHandle);
        public delegate void SessionCreatedCallback(int cookie, int statusCode, string statusString, string sessionHandle);
        public delegate void DevicesCallback(int cookie, int statusCode, string statusString, string currentDevice);
        public delegate void ProvisionAccountCallback(string username, string password);
        public delegate void ParcelVoiceInfoCallback(string regionName, int localID, string channelURI);

        public event LoginStateChangeCallback OnLoginStateChange;
        public event NewSessionCallback OnNewSession;
        public event SessionStateChangeCallback OnSessionStateChange;
        public event ParticipantStateChangeCallback OnParticipantStateChange;
        public event ParticipantPropertiesCallback OnParticipantProperties;
        public event AuxAudioPropertiesCallback OnAuxAudioProperties;
        public event ConnectorCreatedCallback OnConnectorCreated;
        public event LoginCallback OnLogin;
        public event SessionCreatedCallback OnSessionCreated;
        public event BasicActionCallback OnSessionConnected;
        public event BasicActionCallback OnAccountLogout;
        public event BasicActionCallback OnConnectorInitiateShutdown;
        public event BasicActionCallback OnAccountChannelGetList;
        public event BasicActionCallback OnSessionTerminated;
        public event DevicesCallback OnCaptureDevices;
        public event DevicesCallback OnRenderDevices;
        public event ProvisionAccountCallback OnProvisionAccount;
        public event ParcelVoiceInfoCallback OnParcelVoiceInfo;

        public GridClient Client;
        public string VoiceServer = VOICE_RELEASE_SERVER;
        public bool Enabled;

        protected Voice.TCPPipe _DaemonPipe;
        protected VoiceStatus _Status;
        protected int _CommandCookie = 0;
        protected string _TuningSoundFile = string.Empty;
        protected Dictionary<string, string> _ChannelMap = new Dictionary<string, string>();
        protected List<string> _CaptureDevices = new List<string>();
        protected List<string> _RenderDevices = new List<string>();

        #region Response Processing Variables

        private bool isEvent;
        private bool isChannel;
        private bool isLocallyMuted;
        private bool isModeratorMuted;
        private bool isSpeaking;
        private int cookie;
        //private int returnCode;
        private int statusCode;
        private int volume;
        private int state;
        private int participantType;
        private float energy;
        private string statusString = string.Empty;
        //private string uuidString = string.Empty;
        private string actionString = string.Empty;
        private string connectorHandle = string.Empty;
        private string accountHandle = string.Empty;
        private string sessionHandle = string.Empty;
        private string eventSessionHandle = string.Empty;
        private string eventTypeString = string.Empty;
        private string uriString = string.Empty;
        private string nameString = string.Empty;
        //private string audioMediaString = string.Empty;
        private string displayNameString = string.Empty;

        #endregion Response Processing Variables

        public VoiceManager(GridClient client)
        {
            Client = client;
            Client.Network.RegisterEventCallback("RequiredVoiceVersion", RequiredVoiceVersionEventHandler);

            // Register callback handlers for the blocking functions
            RegisterCallbacks();

            Enabled = true;
        }

        public bool IsDaemonRunning()
        {
            throw new NotImplementedException();
        }

        public bool StartDaemon()
        {
            throw new NotImplementedException();
        }

        public void StopDaemon()
        {
            throw new NotImplementedException();
        }

        public bool ConnectToDaemon()
        {
            if (!Enabled) return false;

            return ConnectToDaemon("127.0.0.1", DAEMON_PORT);
        }

        public bool ConnectToDaemon(string address, int port)
        {
            if (!Enabled) return false;

            _DaemonPipe = new Voice.TCPPipe();
            _DaemonPipe.OnDisconnected += _DaemonPipe_OnDisconnected;
            _DaemonPipe.OnReceiveLine += _DaemonPipe_OnReceiveLine;

            var se = _DaemonPipe.Connect(address, port);

            if (se == null)
            {
                return true;
            }
            Console.WriteLine("Connection failed: " + se.Message);
            return false;
        }

        public Dictionary<string, string> GetChannelMap()
        {
            return new Dictionary<string, string>(_ChannelMap);
        }

        public List<string> CurrentCaptureDevices()
        {
            return new List<string>(_CaptureDevices);
        }

        public List<string> CurrentRenderDevices()
        {
            return new List<string>(_RenderDevices);
        }

        public string VoiceAccountFromUUID(UUID id)
        {
            var result = "x" + Convert.ToBase64String(id.GetBytes());
            return result.Replace('+', '-').Replace('/', '_');
        }

        public UUID UUIDFromVoiceAccount(string accountName)
        {
            if (accountName.Length == 25 && accountName[0] == 'x' && accountName[23] == '=' && accountName[24] == '=')
            {
                accountName = accountName.Replace('/', '_').Replace('+', '-');
                var idBytes = Convert.FromBase64String(accountName);

                return idBytes.Length == 16 ? new UUID(idBytes, 0) : UUID.Zero;
            }
            return UUID.Zero;
        }

        public string SIPURIFromVoiceAccount(string account)
        {
            return $"sip:{account}@{VoiceServer}";
        }

        public int RequestCaptureDevices()
        {
            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie++}\" action=\"Aux.GetCaptureDevices.1\"></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            Logger.Log("VoiceManager.RequestCaptureDevices() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
            return -1;
        }

        public int RequestRenderDevices()
        {
            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie++}\" action=\"Aux.GetRenderDevices.1\"></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            Logger.Log("VoiceManager.RequestRenderDevices() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
            return -1;
        }

        public int RequestCreateConnector()
        {
            return RequestCreateConnector(VoiceServer);
        }

        public int RequestCreateConnector(string voiceServer)
        {
            if (_DaemonPipe.Connected)
            {
                VoiceServer = voiceServer;

                var accountServer = $"https://www.{VoiceServer}/api2/";
                var logPath = ".";

                var request = new StringBuilder();
                request.Append($"<Request requestId=\"{_CommandCookie++}\" action=\"Connector.Create.1\">");
                request.Append("<ClientName>V2 SDK</ClientName>");
                request.Append($"<AccountManagementServer>{accountServer}</AccountManagementServer>");
                request.Append("<Logging>");
                request.Append("<Enabled>false</Enabled>");
                request.Append($"<Folder>{logPath}</Folder>");
                request.Append("<FileNamePrefix>vivox-gateway</FileNamePrefix>");
                request.Append("<FileNameSuffix>.log</FileNameSuffix>");
                request.Append("<LogLevel>0</LogLevel>");
                request.Append("</Logging>");
                request.Append("</Request>");
                request.Append(REQUEST_TERMINATOR);

                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(request.ToString()));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.CreateConnector() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        private bool RequestVoiceInternal(string me, CapsClient.CompleteCallback callback, string capsName)
        {
            if (Enabled && Client.Network.Connected)
            {
                if (Client.Network.CurrentSim != null && Client.Network.CurrentSim.Caps != null)
                {
                    var url = Client.Network.CurrentSim.Caps.CapabilityURI(capsName);

                    if (url != null)
                    {
                        var request = new CapsClient(url);
                        var body = new OSDMap();
                        request.OnComplete += callback;
                        request.PostRequestAsync(body, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);

                        return true;
                    }
                    Logger.Log("VoiceManager." + me + "(): " + capsName + " capability is missing",
                        Helpers.LogLevel.Info, Client);
                    return false;
                }
            }

            Logger.Log("VoiceManager.RequestVoiceInternal(): Voice system is currently disabled", 
                       Helpers.LogLevel.Info, Client);
            return false;
            
        }

        public bool RequestProvisionAccount()
        {
            return RequestVoiceInternal("RequestProvisionAccount", ProvisionCapsResponse, "ProvisionVoiceAccountRequest");
        }

        public bool RequestParcelVoiceInfo()
        {
            return RequestVoiceInternal("RequestParcelVoiceInfo", ParcelVoiceInfoResponse, "ParcelVoiceInfoRequest");
        }

        public int RequestLogin(string accountName, string password, string connectorHandle)
        {
            if (_DaemonPipe.Connected)
            {
                var request = new StringBuilder();
                request.Append($"<Request requestId=\"{_CommandCookie++}\" action=\"Account.Login.1\">");
                request.Append($"<ConnectorHandle>{connectorHandle}</ConnectorHandle>");
                request.Append($"<AccountName>{accountName}</AccountName>");
                request.Append($"<AccountPassword>{password}</AccountPassword>");
                request.Append("<AudioSessionAnswerMode>VerifyAnswer</AudioSessionAnswerMode>");
                request.Append("<AccountURI />");
                request.Append("<ParticipantPropertyFrequency>10</ParticipantPropertyFrequency>");
                request.Append("<EnableBuddiesAndPresence>false</EnableBuddiesAndPresence>");
                request.Append("</Request>");
                request.Append(REQUEST_TERMINATOR);

                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(request.ToString()));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.Login() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        public int RequestSetRenderDevice(string deviceName)
        {
            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie}\" action=\"Aux.SetRenderDevice.1\"><RenderDeviceSpecifier>{deviceName}</RenderDeviceSpecifier></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestSetRenderDevice() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        public int RequestStartTuningMode(int duration)
        {
            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie}\" action=\"Aux.CaptureAudioStart.1\"><Duration>{duration}</Duration></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestStartTuningMode() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        public int RequestStopTuningMode()
        {
            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie}\" action=\"Aux.CaptureAudioStop.1\"></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestStopTuningMode() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return _CommandCookie - 1;
            }
        }

        public int RequestSetSpeakerVolume(int volume_)
        {
            if (volume_ < 0 || volume_ > 100)
                throw new ArgumentException("volume must be between 0 and 100", nameof(volume_));

            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie}\" action=\"Aux.SetSpeakerLevel.1\"><Level>{volume_}</Level></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestSetSpeakerVolume() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        public int RequestSetCaptureVolume(int volume_)
        {
            if (volume_ < 0 || volume_ > 100)
                throw new ArgumentException("volume must be between 0 and 100", nameof(volume_));

            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie}\" action=\"Aux.SetMicLevel.1\"><Level>{volume_}</Level></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestSetCaptureVolume() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        /// <summary>
        /// Does not appear to be working
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="loop"></param>
        public int RequestRenderAudioStart(string fileName, bool loop)
        {
            if (_DaemonPipe.Connected)
            {
                _TuningSoundFile = fileName;

                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie++}\" action=\"Aux.RenderAudioStart.1\"><SoundFilePath>{_TuningSoundFile}</SoundFilePath><Loop>{(loop ? "1" : "0")}</Loop></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestRenderAudioStart() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        public int RequestRenderAudioStop()
        {
            if (_DaemonPipe.Connected)
            {
                _DaemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_CommandCookie++}\" action=\"Aux.RenderAudioStop.1\"><SoundFilePath>{_TuningSoundFile}</SoundFilePath></Request>{REQUEST_TERMINATOR}"));

                return _CommandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestRenderAudioStop() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, Client);
                return -1;
            }
        }

        #region Callbacks

        private void RequiredVoiceVersionEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            var msg = (RequiredVoiceVersionMessage)message;
            
            if (VOICE_MAJOR_VERSION != msg.MajorVersion)
            {
                Logger.Log(
                    $"Voice version mismatch! Got {msg.MajorVersion}, expecting {VOICE_MAJOR_VERSION}. Disabling the voice manager", Helpers.LogLevel.Error, Client);
                Enabled = false;
            }
            else
            {
                Logger.DebugLog("Voice version " + msg.MajorVersion + " verified", Client);
            }
        }

        private void ProvisionCapsResponse(CapsClient client, OSD response, Exception error)
        {
            if (!(response is OSDMap respMap)) return;

            if (OnProvisionAccount == null) return;
            try { OnProvisionAccount(respMap["username"].AsString(), respMap["password"].AsString()); }
            catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
        }

        private void ParcelVoiceInfoResponse(CapsClient client, OSD response, Exception error)
        {
            if (!(response is OSDMap respMap)) return;

            var regionName = respMap["region_name"].AsString();
            var localID = respMap["parcel_local_id"].AsInteger();

            string channelURI = null;
            if (respMap["voice_credentials"] is OSDMap)
            {
                var creds = (OSDMap)respMap["voice_credentials"];
                channelURI = creds["channel_uri"].AsString();
            }

            OnParcelVoiceInfo?.Invoke(regionName, localID, channelURI);
        }

        private static void _DaemonPipe_OnDisconnected(SocketException se)
        {
            if (se != null) Console.WriteLine("Disconnected! " + se.Message);
            else Console.WriteLine("Disconnected!");
        }

        private void _DaemonPipe_OnReceiveLine(string line)
        {
            var reader = new XmlTextReader(new StringReader(line));

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                    {
                        if (reader.Depth == 0)
                        {
                            isEvent = (reader.Name == "Event");

                            if (isEvent || reader.Name == "Response")
                            {
                                for (var i = 0; i < reader.AttributeCount; i++)
                                {
                                    reader.MoveToAttribute(i);

                                    if (reader.Name == "action")
                                        actionString = reader.Value;
                                    else if (reader.Name == "type")
                                        eventTypeString = reader.Value;
                                }
                            }
                        }
                        else
                        {
                            switch (reader.Name)
                            {
                                case "InputXml":
                                    cookie = -1;

                                    // Parse through here to get the cookie value
                                    reader.Read();
                                    if (reader.Name == "Request")
                                    {
                                        for (var i = 0; i < reader.AttributeCount; i++)
                                        {
                                            reader.MoveToAttribute(i);

                                            if (reader.Name != "requestId") continue;
                                            int.TryParse(reader.Value, out cookie);
                                            break;
                                        }
                                    }

                                    if (cookie == -1)
                                    {
                                        Logger.Log(
                                            "VoiceManager._DaemonPipe_OnReceiveLine(): Failed to parse InputXml for the cookie",
                                            Helpers.LogLevel.Warning, Client);
                                    }
                                    break;
                                case "CaptureDevices":
                                    _CaptureDevices.Clear();
                                    break;
                                case "RenderDevices":
                                    _RenderDevices.Clear();
                                    break;
//                                 case "ReturnCode":
//                                     returnCode = reader.ReadElementContentAsInt();
//                                     break;
                                case "StatusCode":
                                    statusCode = reader.ReadElementContentAsInt();
                                    break;
                                case "StatusString":
                                    statusString = reader.ReadElementContentAsString();
                                    break;
                                case "State":
                                    state = reader.ReadElementContentAsInt();
                                    break;
                                case "ConnectorHandle":
                                    connectorHandle = reader.ReadElementContentAsString();
                                    break;
                                case "AccountHandle":
                                    accountHandle = reader.ReadElementContentAsString();
                                    break;
                                case "SessionHandle":
                                    sessionHandle = reader.ReadElementContentAsString();
                                    break;
                                case "URI":
                                    uriString = reader.ReadElementContentAsString();
                                    break;
                                case "IsChannel":
                                    isChannel = reader.ReadElementContentAsBoolean();
                                    break;
                                case "Name":
                                    nameString = reader.ReadElementContentAsString();
                                    break;
//                                 case "AudioMedia":
//                                     audioMediaString = reader.ReadElementContentAsString();
//                                     break;
                                case "ChannelName":
                                    nameString = reader.ReadElementContentAsString();
                                    break;
                                case "ParticipantURI":
                                    uriString = reader.ReadElementContentAsString();
                                    break;
                                case "DisplayName":
                                    displayNameString = reader.ReadElementContentAsString();
                                    break;
                                case "AccountName":
                                    nameString = reader.ReadElementContentAsString();
                                    break;
                                case "ParticipantType":
                                    participantType = reader.ReadElementContentAsInt();
                                    break;
                                case "IsLocallyMuted":
                                    isLocallyMuted = reader.ReadElementContentAsBoolean();
                                    break;
                                case "IsModeratorMuted":
                                    isModeratorMuted = reader.ReadElementContentAsBoolean();
                                    break;
                                case "IsSpeaking":
                                    isSpeaking = reader.ReadElementContentAsBoolean();
                                    break;
                                case "Volume":
                                    volume = reader.ReadElementContentAsInt();
                                    break;
                                case "Energy":
                                    energy = reader.ReadElementContentAsFloat();
                                    break;
                                case "MicEnergy":
                                    energy = reader.ReadElementContentAsFloat();
                                    break;
                                case "ChannelURI":
                                    uriString = reader.ReadElementContentAsString();
                                    break;
                                case "ChannelListResult":
                                    _ChannelMap[nameString] = uriString;
                                    break;
                                case "CaptureDevice":
                                    reader.Read();
                                    _CaptureDevices.Add(reader.ReadElementContentAsString());
                                    break;
                                case "CurrentCaptureDevice":
                                    reader.Read();
                                    nameString = reader.ReadElementContentAsString();
                                    break;
                                case "RenderDevice":
                                    reader.Read();
                                    _RenderDevices.Add(reader.ReadElementContentAsString());
                                    break;
                                case "CurrentRenderDevice":
                                    reader.Read();
                                    nameString = reader.ReadElementContentAsString();
                                    break;
                            }
                        }

                        break;
                    }
                    case XmlNodeType.EndElement:
                        if (reader.Depth == 0)
                            ProcessEvent();
                        break;
                    case XmlNodeType.None:
                        break;
                    case XmlNodeType.Attribute:
                        break;
                    case XmlNodeType.Text:
                        break;
                    case XmlNodeType.CDATA:
                        break;
                    case XmlNodeType.EntityReference:
                        break;
                    case XmlNodeType.Entity:
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        break;
                    case XmlNodeType.Comment:
                        break;
                    case XmlNodeType.Document:
                        break;
                    case XmlNodeType.DocumentType:
                        break;
                    case XmlNodeType.DocumentFragment:
                        break;
                    case XmlNodeType.Notation:
                        break;
                    case XmlNodeType.Whitespace:
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    case XmlNodeType.EndEntity:
                        break;
                    case XmlNodeType.XmlDeclaration:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (isEvent)
            {
            }

            //Client.DebugLog("VOICE: " + line);
        }

        private void ProcessEvent()
        {
            if (isEvent)
            {
                switch (eventTypeString)
                {
                    case "LoginStateChangeEvent":
                        OnLoginStateChange?.Invoke(cookie, accountHandle, statusCode, statusString, state);
                        break;
                    case "SessionNewEvent":
                        OnNewSession?.Invoke(cookie, accountHandle, eventSessionHandle, state, nameString, uriString);
                        break;
                    case "SessionStateChangeEvent":
                        OnSessionStateChange?.Invoke(cookie, uriString, statusCode, statusString, eventSessionHandle, state, isChannel, nameString);
                        break;
                    case "ParticipantStateChangeEvent":
                        OnParticipantStateChange?.Invoke(cookie, uriString, statusCode, statusString, state, nameString, displayNameString, participantType);
                        break;
                    case "ParticipantPropertiesEvent":
                        OnParticipantProperties?.Invoke(cookie, uriString, statusCode, statusString, isLocallyMuted, isModeratorMuted, isSpeaking, volume, energy);
                        break;
                    case "AuxAudioPropertiesEvent":
                        OnAuxAudioProperties?.Invoke(cookie, energy);
                        break;
                }
            }
            else
            {
                switch (actionString)
                {
                    case "Connector.Create.1":
                        OnConnectorCreated?.Invoke(cookie, statusCode, statusString, connectorHandle);
                        break;
                    case "Account.Login.1":
                        OnLogin?.Invoke(cookie, statusCode, statusString, accountHandle);
                        break;
                    case "Session.Create.1":
                        OnSessionCreated?.Invoke(cookie, statusCode, statusString, sessionHandle);
                        break;
                    case "Session.Connect.1":
                        OnSessionConnected?.Invoke(cookie, statusCode, statusString);
                        break;
                    case "Session.Terminate.1":
                        OnSessionTerminated?.Invoke(cookie, statusCode, statusString);
                        break;
                    case "Account.Logout.1":
                        OnAccountLogout?.Invoke(cookie, statusCode, statusString);
                        break;
                    case "Connector.InitiateShutdown.1":
                        OnConnectorInitiateShutdown?.Invoke(cookie, statusCode, statusString);
                        break;
                    case "Account.ChannelGetList.1":
                        OnAccountChannelGetList?.Invoke(cookie, statusCode, statusString);
                        break;
                    case "Aux.GetCaptureDevices.1":
                        OnCaptureDevices?.Invoke(cookie, statusCode, statusString, nameString);
                        break;
                    case "Aux.GetRenderDevices.1":
                        OnRenderDevices?.Invoke(cookie, statusCode, statusString, nameString);
                        break;
                }
            }
        }

        #endregion Callbacks
    }
}
