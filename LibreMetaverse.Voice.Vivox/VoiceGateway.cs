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
using System.IO;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Text;
using OpenMetaverse;

namespace LibreMetaverse.Voice.Vivox
{
    public partial class VoiceGateway
    {
        public delegate void DaemonRunningCallback();
        public delegate void DaemonExitedCallback();
        public delegate void DaemonCouldntRunCallback();
        public delegate void DaemonConnectedCallback();
        public delegate void DaemonDisconnectedCallback();
        public delegate void DaemonCouldntConnectCallback();

        public event DaemonRunningCallback OnDaemonRunning;
        public event DaemonExitedCallback OnDaemonExited;
        public event DaemonCouldntRunCallback OnDaemonCouldntRun;
        public event DaemonConnectedCallback OnDaemonConnected;
        public event DaemonDisconnectedCallback OnDaemonDisconnected;
        public event DaemonCouldntConnectCallback OnDaemonCouldntConnect;

        public bool DaemonIsRunning { get; private set; }
        public bool DaemonIsConnected { get; private set; }
        public int RequestId { get; private set; }

        private Process _daemonProcess;
        private readonly ManualResetEvent _daemonLoopSignal = new ManualResetEvent(false);
        private TCPPipe _daemonPipe;

        #region Daemon Management

        /// <summary>
        /// Starts a thread that keeps the daemon running
        /// </summary>
        /// <param name="path"></param>
        /// <param name="args"></param>
        public void StartDaemon(string path, string args)
        {
            StopDaemon();
            _daemonLoopSignal.Set();

            var thread = new Thread(new ThreadStart(delegate()
            {
                while (_daemonLoopSignal.WaitOne(500, false))
                {
                    _daemonProcess = new Process();
                    _daemonProcess.StartInfo.FileName = path;
                    _daemonProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(path);
                    _daemonProcess.StartInfo.Arguments = args;
                    _daemonProcess.StartInfo.UseShellExecute = false;
                    
                    if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        var ldPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty;

                        var newLdPath = _daemonProcess.StartInfo.WorkingDirectory;
                        if (!string.IsNullOrEmpty(ldPath))
                            newLdPath += ":" + ldPath;
                        _daemonProcess.StartInfo.EnvironmentVariables.Add("LD_LIBRARY_PATH", newLdPath);
                    }

                    Logger.DebugLog("Voice folder: " + _daemonProcess.StartInfo.WorkingDirectory);
                    Logger.DebugLog(path + " " + args);
                    var ok = File.Exists(path);

                    if (ok)
                    {
                        // Attempt to start the process
                        if (!_daemonProcess.Start())
                            ok = false;
                    }

                    if (!ok)
                    {
                        DaemonIsRunning = false;
                        _daemonLoopSignal.Reset();

                        if (OnDaemonCouldntRun != null)
                        {
                            try { OnDaemonCouldntRun(); }
                            catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, null, e); }
                        }

                        return;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        DaemonIsRunning = true;
                        if (OnDaemonRunning != null)
                        {
                            try { OnDaemonRunning(); }
                            catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, null, e); }
                        }

                        Logger.DebugLog("Started voice daemon, waiting for exit...");
                        _daemonProcess.WaitForExit();
                        Logger.DebugLog("Voice daemon exited");
                        DaemonIsRunning = false;

                        if (OnDaemonExited != null)
                        {
                            try { OnDaemonExited(); }
                            catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, null, e); }
                        }
                    }
                }
            }))
            {
                Name = "VoiceDaemonController",
                IsBackground = true
            };

            thread.Start();
        }

        /// <summary>
        /// Stops the daemon and the thread keeping it running
        /// </summary>
        public void StopDaemon()
        {
            _daemonLoopSignal.Reset();
            if (_daemonProcess == null) { return; }
            
            try
            {
                _daemonProcess.Kill();
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log("Failed to stop the voice daemon", Helpers.LogLevel.Error, ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public bool ConnectToDaemon(string address, int port)
        {
            DaemonIsConnected = false;

            _daemonPipe = new TCPPipe();
            _daemonPipe.OnDisconnected +=
                delegate(SocketException e)
                {
                    if (OnDaemonDisconnected == null) { return; }
                    
                    try { OnDaemonDisconnected(); }
                    catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, null, ex); }
                };
            _daemonPipe.OnReceiveLine += new TCPPipe.OnReceiveLineCallback(daemonPipe_OnReceiveLine);

            var se = _daemonPipe.Connect(address, port);
            if (se == null)
            {
                DaemonIsConnected = true;

                if (OnDaemonConnected != null)
                {
                    try { OnDaemonConnected(); }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, null, e); }
                }

                return true;
            }
            else
            {
                DaemonIsConnected = false;

                if (OnDaemonCouldntConnect != null)
                {
                    try { OnDaemonCouldntConnect(); }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, null, e); }
                }

                Logger.Log("Voice daemon connection failed: " + se.Message, Helpers.LogLevel.Error);
                return false;
            }
        }

        #endregion Daemon Management

        public int Request(string action)
        {
            return Request(action, null);
        }

        public int Request(string action, string requestXML)
        {
            var returnId = RequestId;
            if (DaemonIsConnected)
            {
                var sb = new StringBuilder();
                sb.Append($"<Request requestId=\"{RequestId++}\" action=\"{action}\"");
                if (string.IsNullOrEmpty(requestXML))
                {
                    sb.Append(" />");
                }
                else
                {
                    sb.Append('>');
                    sb.Append(requestXML);
                    sb.Append("</Request>");
                }
                sb.Append("\n\n\n");

#if DEBUG
                Logger.Log("Request: " + sb, Helpers.LogLevel.Debug);
#endif
                try
                {
                    _daemonPipe.SendData(Encoding.ASCII.GetBytes(sb.ToString()));
                }
                catch
                {
                    returnId = -1;
                }

                return returnId;
            }
            else
            {
                return -1;
            }
        }

        public static string MakeXML(string name, string text)
        {
            return string.IsNullOrEmpty(text) 
                ? $"<{name} />"
                : string.Format("<{0}>{1}</{0}>", name, text);
        }

        private void daemonPipe_OnReceiveLine(string line)
        {
#if DEBUG
            Logger.Log(line, Helpers.LogLevel.Debug);
#endif

            if (line.Substring(0, 10) == "<Response ")
            {
                VoiceResponse rsp = null;
                try
                {
                    rsp = (VoiceResponse)ResponseSerializer.Deserialize(new StringReader(line));
                }
                catch (Exception e)
                {
                    Logger.Log("Failed to deserialize voice daemon response", Helpers.LogLevel.Error, e);
                    return;
                }

                var genericResponse = ResponseType.None;

                if (rsp != null)
                {
                    switch (rsp.Action)
                    {
                        // These first responses carry useful information beyond simple status,
                        // so they each have a specific Event.
                        case "Connector.Create.1":
                            OnConnectorCreateResponse?.Invoke(
                                rsp.InputXml.Request,
                                new VoiceConnectorEventArgs(
                                    rsp.ReturnCode,
                                    rsp.Results.StatusCode,
                                    rsp.Results.StatusString,
                                    rsp.Results.VersionID,
                                    rsp.Results.ConnectorHandle));
                            break;
                        case "Aux.GetCaptureDevices.1":
                            CaptureDevices = new List<string>();

                            if (rsp.Results.CaptureDevices.Count == 0 || rsp.Results.CurrentCaptureDevice == null)
                                break;

                            foreach (var device in rsp.Results.CaptureDevices)
                                CaptureDevices.Add(device.Device);
                            _currentCaptureDevice = rsp.Results.CurrentCaptureDevice.Device;

                            if (OnAuxGetCaptureDevicesResponse != null && rsp.Results.CaptureDevices.Count > 0)
                            {
                                OnAuxGetCaptureDevicesResponse(
                                    rsp.InputXml.Request,
                                    new VoiceDevicesEventArgs(
                                        ResponseType.GetCaptureDevices,
                                        rsp.ReturnCode,
                                        rsp.Results.StatusCode,
                                        rsp.Results.StatusString,
                                        rsp.Results.CurrentCaptureDevice.Device,
                                        CaptureDevices));
                            }

                            break;
                        case "Aux.GetRenderDevices.1":
                            PlaybackDevices = new List<string>();

                            if (rsp.Results.RenderDevices.Count == 0 || rsp.Results.CurrentRenderDevice == null)
                                break;

                            foreach (var device in rsp.Results.RenderDevices)
                                PlaybackDevices.Add(device.Device);


                            _currentPlaybackDevice = rsp.Results.CurrentRenderDevice.Device;

                            OnAuxGetRenderDevicesResponse?.Invoke(
                                rsp.InputXml.Request,
                                new VoiceDevicesEventArgs(
                                    ResponseType.GetCaptureDevices,
                                    rsp.ReturnCode,
                                    rsp.Results.StatusCode,
                                    rsp.Results.StatusString,
                                    rsp.Results.CurrentRenderDevice.Device,
                                    PlaybackDevices));

                            break;

                        case "Account.Login.1":
                            OnAccountLoginResponse?.Invoke(rsp.InputXml.Request,
                                new VoiceAccountEventArgs(
                                    rsp.ReturnCode,
                                    rsp.Results.StatusCode,
                                    rsp.Results.StatusString,
                                    rsp.Results.AccountHandle));

                            break;

                        case "Session.Create.1":
                            OnSessionCreateResponse?.Invoke(
                                rsp.InputXml.Request,
                                new VoiceSessionEventArgs(
                                    rsp.ReturnCode,
                                    rsp.Results.StatusCode,
                                    rsp.Results.StatusString,
                                    rsp.Results.SessionHandle));

                            break;

                        // All the remaining responses below this point just report status,
                        // so they all share the same Event.  Most are useful only for
                        // detecting coding errors.
                        case "Connector.InitiateShutdown.1":
                            genericResponse = ResponseType.ConnectorInitiateShutdown;
                            break;
                        case "Aux.SetRenderDevice.1":
                            genericResponse = ResponseType.SetRenderDevice;
                            break;
                        case "Connector.MuteLocalMic.1":
                            genericResponse = ResponseType.MuteLocalMic;
                            break;
                        case "Connector.MuteLocalSpeaker.1":
                            genericResponse = ResponseType.MuteLocalSpeaker;
                            break;
                        case "Connector.SetLocalMicVolume.1":
                            genericResponse = ResponseType.SetLocalMicVolume;
                            break;
                        case "Connector.SetLocalSpeakerVolume.1":
                            genericResponse = ResponseType.SetLocalSpeakerVolume;
                            break;
                        case "Aux.SetCaptureDevice.1":
                            genericResponse = ResponseType.SetCaptureDevice;
                            break;
                        case "Session.RenderAudioStart.1":
                            genericResponse = ResponseType.RenderAudioStart;
                            break;
                        case "Session.RenderAudioStop.1":
                            genericResponse = ResponseType.RenderAudioStop;
                            break;
                        case "Aux.CaptureAudioStart.1":
                            genericResponse = ResponseType.CaptureAudioStart;
                            break;
                        case "Aux.CaptureAudioStop.1":
                            genericResponse = ResponseType.CaptureAudioStop;
                            break;
                        case "Aux.SetMicLevel.1":
                            genericResponse = ResponseType.SetMicLevel;
                            break;
                        case "Aux.SetSpeakerLevel.1":
                            genericResponse = ResponseType.SetSpeakerLevel;
                            break;
                        case "Account.Logout.1":
                            genericResponse = ResponseType.AccountLogout;
                            break;
                        case "Session.Connect.1":
                            genericResponse = ResponseType.SessionConnect;
                            break;
                        case "Session.Terminate.1":
                            genericResponse = ResponseType.SessionTerminate;
                            break;
                        case "Session.SetParticipantVolumeForMe.1":
                            genericResponse = ResponseType.SetParticipantVolumeForMe;
                            break;
                        case "Session.SetParticipantMuteForMe.1":
                            genericResponse = ResponseType.SetParticipantMuteForMe;
                            break;
                        case "Session.Set3DPosition.1":
                            genericResponse = ResponseType.Set3DPosition;
                            break;
                        default:
                            Logger.Log("Unimplemented response from the voice daemon: " + line, Helpers.LogLevel.Error);
                            break;
                    }

                    // Send the Response Event for all the simple cases.
                    if (genericResponse != ResponseType.None && OnVoiceResponse != null)
                    {
                        OnVoiceResponse(rsp.InputXml.Request,
                            new VoiceResponseEventArgs(
                                genericResponse,
                                rsp.ReturnCode,
                                rsp.Results.StatusCode,
                                rsp.Results.StatusString));
                    }
                }
            }
            else if (line.Substring(0, 7) == "<Event ")
            {
                VoiceEvent evt = null;
                try
                {
                    evt = (VoiceEvent)EventSerializer.Deserialize(new StringReader(line));
                }
                catch (Exception e)
                {
                    Logger.Log("Failed to deserialize voice daemon event", Helpers.LogLevel.Error, e);
                    return;
                }

                if (evt != null)
                    switch (evt.Type)
                    {
                        case "LoginStateChangeEvent":
                        case "AccountLoginStateChangeEvent":
                            OnAccountLoginStateChangeEvent?.Invoke(this,
                                new AccountLoginStateChangeEventArgs(
                                    evt.AccountHandle,
                                    int.Parse(evt.StatusCode),
                                    evt.StatusString,
                                    (LoginState)int.Parse(evt.State)));
                            break;

                        case "SessionNewEvent":
                            OnSessionNewEvent?.Invoke(this,
                                new NewSessionEventArgs(
                                    evt.AccountHandle,
                                    evt.SessionHandle,
                                    evt.URI,
                                    bool.Parse(evt.IsChannel),
                                    evt.Name,
                                    evt.AudioMedia));

                            break;

                        case "SessionStateChangeEvent":
                            OnSessionStateChangeEvent?.Invoke(this,
                                new SessionStateChangeEventArgs(
                                    evt.SessionHandle,
                                    int.Parse(evt.StatusCode),
                                    evt.StatusString,
                                    (SessionState)int.Parse(evt.State),
                                    evt.URI,
                                    bool.Parse(evt.IsChannel),
                                    evt.ChannelName));

                            break;

                        case "ParticipantAddedEvent":
                            Logger.Log("Add participant " + evt.ParticipantUri, Helpers.LogLevel.Debug);
                            OnSessionParticipantAddedEvent?.Invoke(this,
                                new ParticipantAddedEventArgs(
                                    evt.SessionGroupHandle,
                                    evt.SessionHandle,
                                    evt.ParticipantUri,
                                    evt.AccountName,
                                    evt.DisplayName,
                                    (ParticipantType)int.Parse(evt.ParticipantType),
                                    evt.Application));

                            break;

                        case "ParticipantRemovedEvent":
                            OnSessionParticipantRemovedEvent?.Invoke(this,
                                new ParticipantRemovedEventArgs(
                                    evt.SessionGroupHandle,
                                    evt.SessionHandle,
                                    evt.ParticipantUri,
                                    evt.AccountName,
                                    evt.Reason));

                            break;

                        case "ParticipantStateChangeEvent":
                            // Useful in person-to-person calls
                            OnSessionParticipantStateChangeEvent?.Invoke(this,
                                new ParticipantStateChangeEventArgs(
                                    evt.SessionHandle,
                                    int.Parse(evt.StatusCode),
                                    evt.StatusString,
                                    (ParticipantState)int.Parse(evt.State), // Ringing, Connected, etc
                                    evt.ParticipantUri,
                                    evt.AccountName,
                                    evt.DisplayName,
                                    (ParticipantType)int.Parse(evt.ParticipantType)));

                            break;

                        case "ParticipantPropertiesEvent":
                            OnSessionParticipantPropertiesEvent?.Invoke(this,
                                new ParticipantPropertiesEventArgs(
                                    evt.SessionHandle,
                                    evt.ParticipantUri,
                                    bool.Parse(evt.IsLocallyMuted),
                                    bool.Parse(evt.IsModeratorMuted),
                                    bool.Parse(evt.IsSpeaking),
                                    int.Parse(evt.Volume),
                                    float.Parse(evt.Energy)));

                            break;

                        case "ParticipantUpdatedEvent":
                            OnSessionParticipantUpdatedEvent?.Invoke(this,
                                new ParticipantUpdatedEventArgs(
                                    evt.SessionHandle,
                                    evt.ParticipantUri,
                                    bool.Parse(evt.IsModeratorMuted),
                                    bool.Parse(evt.IsSpeaking),
                                    int.Parse(evt.Volume),
                                    float.Parse(evt.Energy)));

                            break;

                        case "SessionGroupAddedEvent":
                            OnSessionGroupAddedEvent?.Invoke(this,
                                new SessionGroupAddedEventArgs(
                                    evt.AccountHandle,
                                    evt.SessionGroupHandle,
                                    evt.Type));

                            break;

                        case "SessionAddedEvent":
                            OnSessionAddedEvent?.Invoke(this,
                                new SessionAddedEventArgs(
                                    evt.SessionGroupHandle,
                                    evt.SessionHandle,
                                    evt.Uri,
                                    bool.Parse(evt.IsChannel),
                                    bool.Parse(evt.Incoming)));

                            break;

                        case "SessionRemovedEvent":
                            OnSessionRemovedEvent?.Invoke(this,
                                new SessionRemovedEventArgs(
                                    evt.SessionGroupHandle,
                                    evt.SessionHandle,
                                    evt.Uri));

                            break;

                        case "SessionUpdatedEvent":
                            if (OnSessionRemovedEvent != null)
                            {
                                OnSessionUpdatedEvent(this,
                                    new SessionUpdatedEventArgs(
                                        evt.SessionGroupHandle,
                                        evt.SessionHandle,
                                        evt.Uri,
                                        int.Parse(evt.IsMuted) != 0,
                                        int.Parse(evt.Volume),
                                        int.Parse(evt.TransmitEnabled) != 0,
                                        +int.Parse(evt.IsFocused) != 0));
                            }

                            break;

                        case "AuxAudioPropertiesEvent":
                            OnAuxAudioPropertiesEvent?.Invoke(this,
                                new AudioPropertiesEventArgs(
                                    bool.Parse(evt.MicIsActive),
                                    float.Parse(evt.MicEnergy),
                                    int.Parse(evt.MicVolume),
                                    int.Parse(evt.SpeakerVolume)));

                            break;

                        case "SessionMediaEvent":
                            OnSessionMediaEvent?.Invoke(this,
                                new SessionMediaEventArgs(
                                    evt.SessionHandle,
                                    bool.Parse(evt.HasText),
                                    bool.Parse(evt.HasAudio),
                                    bool.Parse(evt.HasVideo),
                                    bool.Parse(evt.Terminated)));

                            break;

                        case "BuddyAndGroupListChangedEvent":
                            // TODO   * <AccountHandle>c1_m1000xrjiQgi95QhCzH_D6ZJ8c5A==</AccountHandle><Buddies /><Groups />
                            break;

                        case "MediaStreamUpdatedEvent":
                            // TODO <SessionGroupHandle>c1_m1000xrjiQgi95QhCzH_D6ZJ8c5A==_sg0</SessionGroupHandle>
                            // <SessionHandle>c1_m1000xrjiQgi95QhCzH_D6ZJ8c5A==0</SessionHandle>
                            //<StatusCode>0</StatusCode><StatusString /><State>1</State><Incoming>false</Incoming>

                            break;

                        default:
                            Logger.Log("Unimplemented event from the voice daemon: " + line, Helpers.LogLevel.Error);
                            break;
                    }
            }
            else
            {
                Logger.Log("Unrecognized data from the voice daemon: " + line, Helpers.LogLevel.Error);
            }
        }
    }
}
