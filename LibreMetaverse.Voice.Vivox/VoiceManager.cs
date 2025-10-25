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

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;
using System.Xml;
using OpenMetaverse.StructuredData;
using OpenMetaverse;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using System.Net.Http;

namespace LibreMetaverse.Voice.Vivox
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
        public delegate void ParcelVoiceInfoCallback(string regionName, int localId, string channelUri);

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

        public string VoiceServer = VOICE_RELEASE_SERVER;
        
        private readonly GridClient _client;
        private bool _enabled;
        private TCPPipe _daemonPipe;
        protected VoiceStatus Status;
        private int _commandCookie;
        private string _tuningSoundFile = string.Empty;
        private readonly Dictionary<string, string> _channelMap = new Dictionary<string, string>();
        private readonly List<string> _captureDevices = new List<string>();
        private readonly List<string> _renderDevices = new List<string>();

        #region Response Processing Variables

        private bool _isEvent;
        private bool _isChannel;
        private bool _isLocallyMuted;
        private bool _isModeratorMuted;
        private bool _isSpeaking;
        private int _cookie;
        //private int _returnCode;
        private int _statusCode;
        private int _volume;
        private int _state;
        private int _participantType;
        private float _energy;
        private string _statusString = string.Empty;
        //private string _uuidString = string.Empty;
        private string _actionString = string.Empty;
        private string _connectorHandle = string.Empty;
        private string _accountHandle = string.Empty;
        private string _sessionHandle = string.Empty;
        private string _eventSessionHandle = string.Empty;
        private string _eventTypeString = string.Empty;
        private string _uriString = string.Empty;
        private string _nameString = string.Empty;
        //private string audioMediaString = string.Empty;
        private string _displayNameString = string.Empty;

        #endregion Response Processing Variables

        public VoiceManager(GridClient client)
        {
            _client = client;
            _client.Network.RegisterEventCallback("RequiredVoiceVersion", RequiredVoiceVersionEventHandler);

            // Register callback handlers for the blocking functions
            RegisterCallbacks();

            _enabled = true;
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
            return _enabled && ConnectToDaemon("127.0.0.1", DAEMON_PORT);
        }

        public bool ConnectToDaemon(string address, int port)
        {
            if (!_enabled) return false;

            _daemonPipe = new TCPPipe();
            _daemonPipe.OnDisconnected += _DaemonPipe_OnDisconnected;
            _daemonPipe.OnReceiveLine += _DaemonPipe_OnReceiveLine;

            var se = _daemonPipe.Connect(address, port);

            if (se == null)
            {
                return true;
            }
            Console.WriteLine("Connection failed: " + se.Message);
            return false;
        }

        public Dictionary<string, string> GetChannelMap()
        {
            return new Dictionary<string, string>(_channelMap);
        }

        public List<string> CurrentCaptureDevices()
        {
            return new List<string>(_captureDevices);
        }

        public List<string> CurrentRenderDevices()
        {
            return new List<string>(_renderDevices);
        }

        public string VoiceAccountFromUuid(UUID id)
        {
            var result = "x" + Convert.ToBase64String(id.GetBytes());
            return result.Replace('+', '-').Replace('/', '_');
        }

        public UUID UuidFromVoiceAccount(string accountName)
        {
            if (accountName.Length == 25 && accountName[0] == 'x' && accountName[23] == '=' && accountName[24] == '=')
            {
                accountName = accountName.Replace('/', '_').Replace('+', '-');
                var idBytes = Convert.FromBase64String(accountName);

                return idBytes.Length == 16 ? new UUID(idBytes, 0) : UUID.Zero;
            }
            return UUID.Zero;
        }

        public string SipuriFromVoiceAccount(string account)
        {
            return $"sip:{account}@{VoiceServer}";
        }

        public int RequestCaptureDevices()
        {
            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie++}\" action=\"Aux.GetCaptureDevices.1\"></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            Logger.Log("VoiceManager.RequestCaptureDevices() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, _client);
            return -1;
        }

        public int RequestRenderDevices()
        {
            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie++}\" action=\"Aux.GetRenderDevices.1\"></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            Logger.Log("VoiceManager.RequestRenderDevices() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, _client);
            return -1;
        }

        public int RequestCreateConnector()
        {
            return RequestCreateConnector(VoiceServer);
        }

        public int RequestCreateConnector(string voiceServer)
        {
            if (_daemonPipe.Connected)
            {
                VoiceServer = voiceServer;

                var accountServer = $"https://www.{VoiceServer}/api2/";
                var logPath = ".";

                var request = new StringBuilder();
                request.Append($"<Request requestId=\"{_commandCookie++}\" action=\"Connector.Create.1\">");
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

                _daemonPipe.SendData(Encoding.ASCII.GetBytes(request.ToString()));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.CreateConnector() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, _client);
                return -1;
            }
        }

        private bool RequestVoiceInternal(string me, DownloadCompleteHandler callback, string capsName)
        {
            if (_enabled && _client.Network.Connected)
            {
                if (_client.Network.CurrentSim?.Caps != null)
                {
                    var cap = _client.Network.CurrentSim.Caps.CapabilityURI(capsName);

                    if (cap != null)
                    {
                        var req = _client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, new OSDMap(),
                            CancellationToken.None, callback);

                        return true;
                    }
                    Logger.Log($"VoiceManager.{me}(): {capsName} capability is missing",
                        Helpers.LogLevel.Info, _client);
                    return false;
                }
            }

            Logger.Log("VoiceManager.RequestVoiceInternal(): Voice system is currently disabled", 
                       Helpers.LogLevel.Info, _client);
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

        public int RequestLogin(string accountName, string password, string connHandle)
        {
            if (_daemonPipe.Connected)
            {
                var request = new StringBuilder();
                request.Append($"<Request requestId=\"{_commandCookie++}\" action=\"Account.Login.1\">");
                request.Append($"<ConnectorHandle>{connHandle}</ConnectorHandle>");
                request.Append($"<AccountName>{accountName}</AccountName>");
                request.Append($"<AccountPassword>{password}</AccountPassword>");
                request.Append("<AudioSessionAnswerMode>VerifyAnswer</AudioSessionAnswerMode>");
                request.Append("<AccountURI />");
                request.Append("<ParticipantPropertyFrequency>10</ParticipantPropertyFrequency>");
                request.Append("<EnableBuddiesAndPresence>false</EnableBuddiesAndPresence>");
                request.Append("</Request>");
                request.Append(REQUEST_TERMINATOR);

                _daemonPipe.SendData(Encoding.ASCII.GetBytes(request.ToString()));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.Login() called when the daemon pipe is disconnected", 
                    Helpers.LogLevel.Error, _client);
                return -1;
            }
        }

        public int RequestSetRenderDevice(string deviceName)
        {
            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie}\" action=\"Aux.SetRenderDevice.1\"><RenderDeviceSpecifier>{deviceName}</RenderDeviceSpecifier></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestSetRenderDevice() called when the daemon pipe is disconnected", Helpers.LogLevel.Error, _client);
                return -1;
            }
        }

        public int RequestStartTuningMode(int duration)
        {
            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie}\" action=\"Aux.CaptureAudioStart.1\"><Duration>{duration}</Duration></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestStartTuningMode() called when the daemon pipe is disconnected",
                    Helpers.LogLevel.Error, _client);
                return -1;
            }
        }

        public int RequestStopTuningMode()
        {
            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie}\" action=\"Aux.CaptureAudioStop.1\"></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestStopTuningMode() called when the daemon pipe is disconnected", 
                    Helpers.LogLevel.Error, _client);
                return _commandCookie - 1;
            }
        }

        public int RequestSetSpeakerVolume(int volume)
        {
            if (volume < 0 || volume > 100)
                throw new ArgumentException("volume must be between 0 and 100", nameof(volume));

            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie}\" action=\"Aux.SetSpeakerLevel.1\"><Level>{volume}</Level></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestSetSpeakerVolume() called when the daemon pipe is disconnected",
                    Helpers.LogLevel.Error, _client);
                return -1;
            }
        }

        public int RequestSetCaptureVolume(int volume)
        {
            if (volume < 0 || volume > 100)
                throw new ArgumentException("volume must be between 0 and 100", nameof(volume));

            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie}\" action=\"Aux.SetMicLevel.1\"><Level>{volume}</Level></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestSetCaptureVolume() called when the daemon pipe is disconnected",
                    Helpers.LogLevel.Error, _client);
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
            if (_daemonPipe.Connected)
            {
                _tuningSoundFile = fileName;

                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie++}\" action=\"Aux.RenderAudioStart.1\"><SoundFilePath>{_tuningSoundFile}</SoundFilePath><Loop>{(loop ? "1" : "0")}</Loop></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestRenderAudioStart() called when the daemon pipe is disconnected", 
                    Helpers.LogLevel.Error, _client);
                return -1;
            }
        }

        public int RequestRenderAudioStop()
        {
            if (_daemonPipe.Connected)
            {
                _daemonPipe.SendData(Encoding.ASCII.GetBytes(
                    $"<Request requestId=\"{_commandCookie++}\" action=\"Aux.RenderAudioStop.1\"><SoundFilePath>{_tuningSoundFile}</SoundFilePath></Request>{REQUEST_TERMINATOR}"));

                return _commandCookie - 1;
            }
            else
            {
                Logger.Log("VoiceManager.RequestRenderAudioStop() called when the daemon pipe is disconnected", 
                    Helpers.LogLevel.Error, _client);
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
                    $"Voice version mismatch! Got {msg.MajorVersion}, expecting {VOICE_MAJOR_VERSION}. Disabling the voice manager", Helpers.LogLevel.Error, _client);
                _enabled = false;
            }
            else
            {
                Logger.DebugLog("Voice version " + msg.MajorVersion + " verified", _client);
            }
        }

        private void ProvisionCapsResponse(HttpResponseMessage httpResponse, byte[] responseData, Exception error)
        {
            if (error != null)
            {
                Logger.Log("Failed to provision voice capability", Helpers.LogLevel.Warning, _client, error);
                return;
            }
            var response = OSDParser.Deserialize(responseData);
            if (!(response is OSDMap respMap)) return;

            if (OnProvisionAccount == null) return;
            try { OnProvisionAccount(respMap["username"].AsString(), respMap["password"].AsString()); }
            catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, _client, e); }
        }

        private void ParcelVoiceInfoResponse(HttpResponseMessage httpResponse, byte[] responseData, Exception error)
        {
            if (error != null)
            {
                Logger.Log("Failed to retrieve voice info", Helpers.LogLevel.Warning, _client, error);
                return;
            }
            var response = OSDParser.Deserialize(responseData);
            if (!(response is OSDMap respMap)) return;

            var regionName = respMap["region_name"].AsString();
            var localId = respMap["parcel_local_id"].AsInteger();

            string channelUri = null;
            if (respMap["voice_credentials"] is OSDMap)
            {
                var creds = (OSDMap)respMap["voice_credentials"];
                channelUri = creds["channel_uri"].AsString();
            }

            OnParcelVoiceInfo?.Invoke(regionName, localId, channelUri);
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
                            _isEvent = (reader.Name == "Event");

                            if (_isEvent || reader.Name == "Response")
                            {
                                for (var i = 0; i < reader.AttributeCount; i++)
                                {
                                    reader.MoveToAttribute(i);

                                    if (reader.Name == "action")
                                        _actionString = reader.Value;
                                    else if (reader.Name == "type")
                                        _eventTypeString = reader.Value;
                                }
                            }
                        }
                        else
                        {
                            switch (reader.Name)
                            {
                                case "InputXml":
                                    _cookie = -1;

                                    // Parse through here to get the cookie value
                                    reader.Read();
                                    if (reader.Name == "Request")
                                    {
                                        for (var i = 0; i < reader.AttributeCount; i++)
                                        {
                                            reader.MoveToAttribute(i);

                                            if (reader.Name != "requestId") continue;
                                            int.TryParse(reader.Value, out _cookie);
                                            break;
                                        }
                                    }

                                    if (_cookie == -1)
                                    {
                                        Logger.Log(
                                            "VoiceManager._DaemonPipe_OnReceiveLine(): Failed to parse InputXml for the cookie",
                                            Helpers.LogLevel.Warning, _client);
                                    }
                                    break;
                                case "CaptureDevices":
                                    _captureDevices.Clear();
                                    break;
                                case "RenderDevices":
                                    _renderDevices.Clear();
                                    break;
//                                 case "ReturnCode":
//                                     returnCode = reader.ReadElementContentAsInt();
//                                     break;
                                case "StatusCode":
                                    _statusCode = reader.ReadElementContentAsInt();
                                    break;
                                case "StatusString":
                                    _statusString = reader.ReadElementContentAsString();
                                    break;
                                case "State":
                                    _state = reader.ReadElementContentAsInt();
                                    break;
                                case "ConnectorHandle":
                                    _connectorHandle = reader.ReadElementContentAsString();
                                    break;
                                case "AccountHandle":
                                    _accountHandle = reader.ReadElementContentAsString();
                                    break;
                                case "SessionHandle":
                                    _sessionHandle = reader.ReadElementContentAsString();
                                    break;
                                case "URI":
                                    _uriString = reader.ReadElementContentAsString();
                                    break;
                                case "IsChannel":
                                    _isChannel = reader.ReadElementContentAsBoolean();
                                    break;
                                case "Name":
                                    _nameString = reader.ReadElementContentAsString();
                                    break;
//                                 case "AudioMedia":
//                                     audioMediaString = reader.ReadElementContentAsString();
//                                     break;
                                case "ChannelName":
                                    _nameString = reader.ReadElementContentAsString();
                                    break;
                                case "ParticipantURI":
                                    _uriString = reader.ReadElementContentAsString();
                                    break;
                                case "DisplayName":
                                    _displayNameString = reader.ReadElementContentAsString();
                                    break;
                                case "AccountName":
                                    _nameString = reader.ReadElementContentAsString();
                                    break;
                                case "ParticipantType":
                                    _participantType = reader.ReadElementContentAsInt();
                                    break;
                                case "IsLocallyMuted":
                                    _isLocallyMuted = reader.ReadElementContentAsBoolean();
                                    break;
                                case "IsModeratorMuted":
                                    _isModeratorMuted = reader.ReadElementContentAsBoolean();
                                    break;
                                case "IsSpeaking":
                                    _isSpeaking = reader.ReadElementContentAsBoolean();
                                    break;
                                case "Volume":
                                    _volume = reader.ReadElementContentAsInt();
                                    break;
                                case "Energy":
                                    _energy = reader.ReadElementContentAsFloat();
                                    break;
                                case "MicEnergy":
                                    _energy = reader.ReadElementContentAsFloat();
                                    break;
                                case "ChannelURI":
                                    _uriString = reader.ReadElementContentAsString();
                                    break;
                                case "ChannelListResult":
                                    _channelMap[_nameString] = _uriString;
                                    break;
                                case "CaptureDevice":
                                    reader.Read();
                                    _captureDevices.Add(reader.ReadElementContentAsString());
                                    break;
                                case "CurrentCaptureDevice":
                                    reader.Read();
                                    _nameString = reader.ReadElementContentAsString();
                                    break;
                                case "RenderDevice":
                                    reader.Read();
                                    _renderDevices.Add(reader.ReadElementContentAsString());
                                    break;
                                case "CurrentRenderDevice":
                                    reader.Read();
                                    _nameString = reader.ReadElementContentAsString();
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
                        throw new ArgumentOutOfRangeException(nameof(reader.NodeType));
                }
            }

            if (_isEvent)
            {
            }

            //Client.DebugLog("VOICE: " + line);
        }

        private void ProcessEvent()
        {
            if (_isEvent)
            {
                switch (_eventTypeString)
                {
                    case "LoginStateChangeEvent":
                        OnLoginStateChange?.Invoke(_cookie, _accountHandle, _statusCode, _statusString, _state);
                        break;
                    case "SessionNewEvent":
                        OnNewSession?.Invoke(_cookie, _accountHandle, _eventSessionHandle, _state, _nameString, _uriString);
                        break;
                    case "SessionStateChangeEvent":
                        OnSessionStateChange?.Invoke(_cookie, _uriString, _statusCode, _statusString, _eventSessionHandle, _state, _isChannel, _nameString);
                        break;
                    case "ParticipantStateChangeEvent":
                        OnParticipantStateChange?.Invoke(_cookie, _uriString, _statusCode, _statusString, _state, _nameString, _displayNameString, _participantType);
                        break;
                    case "ParticipantPropertiesEvent":
                        OnParticipantProperties?.Invoke(_cookie, _uriString, _statusCode, _statusString, _isLocallyMuted, _isModeratorMuted, _isSpeaking, _volume, _energy);
                        break;
                    case "AuxAudioPropertiesEvent":
                        OnAuxAudioProperties?.Invoke(_cookie, _energy);
                        break;
                }
            }
            else
            {
                switch (_actionString)
                {
                    case "Connector.Create.1":
                        OnConnectorCreated?.Invoke(_cookie, _statusCode, _statusString, _connectorHandle);
                        break;
                    case "Account.Login.1":
                        OnLogin?.Invoke(_cookie, _statusCode, _statusString, _accountHandle);
                        break;
                    case "Session.Create.1":
                        OnSessionCreated?.Invoke(_cookie, _statusCode, _statusString, _sessionHandle);
                        break;
                    case "Session.Connect.1":
                        OnSessionConnected?.Invoke(_cookie, _statusCode, _statusString);
                        break;
                    case "Session.Terminate.1":
                        OnSessionTerminated?.Invoke(_cookie, _statusCode, _statusString);
                        break;
                    case "Account.Logout.1":
                        OnAccountLogout?.Invoke(_cookie, _statusCode, _statusString);
                        break;
                    case "Connector.InitiateShutdown.1":
                        OnConnectorInitiateShutdown?.Invoke(_cookie, _statusCode, _statusString);
                        break;
                    case "Account.ChannelGetList.1":
                        OnAccountChannelGetList?.Invoke(_cookie, _statusCode, _statusString);
                        break;
                    case "Aux.GetCaptureDevices.1":
                        OnCaptureDevices?.Invoke(_cookie, _statusCode, _statusString, _nameString);
                        break;
                    case "Aux.GetRenderDevices.1":
                        OnRenderDevices?.Invoke(_cookie, _statusCode, _statusString, _nameString);
                        break;
                }
            }
        }

        #endregion Callbacks
    }
}
