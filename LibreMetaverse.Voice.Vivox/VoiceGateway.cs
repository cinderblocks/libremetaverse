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

        public event DaemonRunningCallback? OnDaemonRunning;
        public event DaemonExitedCallback? OnDaemonExited;
        public event DaemonCouldntRunCallback? OnDaemonCouldntRun;
        public event DaemonConnectedCallback? OnDaemonConnected;
        public event DaemonDisconnectedCallback? OnDaemonDisconnected;
        public event DaemonCouldntConnectCallback? OnDaemonCouldntConnect;

        public bool DaemonIsRunning { get; private set; }
        public bool DaemonIsConnected { get; private set; }
        public int RequestId { get; private set; }

        private Process? _daemonProcess;
        private readonly ManualResetEvent _daemonLoopSignal = new ManualResetEvent(false);
        private TCPPipe? _daemonPipe;

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
                            catch (Exception e) { Logger.Error(e.Message, e); }
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
                            catch (Exception e) { Logger.Error(e.Message, e); }
                        }

                        Logger.DebugLog("Started voice daemon, waiting for exit...");
                        _daemonProcess.WaitForExit();
                        Logger.DebugLog("Voice daemon exited");
                        DaemonIsRunning = false;

                        if (OnDaemonExited != null)
                        {
                            try { OnDaemonExited(); }
                            catch (Exception e) { Logger.Error(e.Message, e); }
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
                Logger.Error("Failed to stop the voice daemon", ex);
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
                    catch (Exception ex) { Logger.Error(ex.Message, ex); }
                };
            _daemonPipe.OnReceiveLine += new TCPPipe.OnReceiveLineCallback(daemonPipe_OnReceiveLine);

            var se = _daemonPipe.Connect(address, port);
            if (se == null)
            {
                DaemonIsConnected = true;

                if (OnDaemonConnected != null)
                {
                    try { OnDaemonConnected(); }
                    catch (Exception e) { Logger.Error(e.Message, e); }
                }

                return true;
            }
            else
            {
                DaemonIsConnected = false;

                if (OnDaemonCouldntConnect != null)
                {
                    try { OnDaemonCouldntConnect(); }
                    catch (Exception e) { Logger.Error(e.Message, e); }
                }

                Logger.Error("Voice daemon connection failed: " + se.Message);
                return false;
            }
        }

        #endregion Daemon Management

        public int Request(string action)
        {
            return Request(action, null);
        }

        public int Request(string action, string? requestXML)
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
                Logger.Debug("Request: " + sb);
#endif
                try
                {
                    _daemonPipe!.SendData(Encoding.ASCII.GetBytes(sb.ToString()));
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
            Logger.Debug(line);
#endif

            if (line.Substring(0, 10) == "<Response ")
            {
                VoiceResponse? rsp = null;
                try
                {
                    var tmp = ResponseSerializer.Deserialize(new StringReader(line));
                    rsp = tmp as VoiceResponse;
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to deserialize voice daemon response", e);
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
                                rsp.InputXml?.Request?.RequestId ?? string.Empty,
                                new VoiceConnectorEventArgs(
                                    rsp.ReturnCode,
                                    rsp.Results?.StatusCode ?? 0,
                                    rsp.Results?.StatusString ?? string.Empty,
                                    rsp.Results?.VersionID ?? string.Empty,
                                    rsp.Results?.ConnectorHandle ?? string.Empty));
                            break;
                        case "Aux.GetCaptureDevices.1":
                            CaptureDevices = new List<string>();

                            if (rsp.Results == null || rsp.Results.CaptureDevices == null || rsp.Results.CaptureDevices.Count == 0 || rsp.Results.CurrentCaptureDevice == null)
                                break;

                            foreach (var device in rsp.Results.CaptureDevices)
                                CaptureDevices.Add(device?.Device ?? string.Empty);
                            _currentCaptureDevice = rsp.Results.CurrentCaptureDevice?.Device ?? string.Empty;

                            if (OnAuxGetCaptureDevicesResponse != null && rsp.Results.CaptureDevices.Count > 0)
                            {
                                OnAuxGetCaptureDevicesResponse(
                                    rsp.InputXml?.Request?.RequestId ?? string.Empty,
                                    new VoiceDevicesEventArgs(
                                        ResponseType.GetCaptureDevices,
                                        rsp.ReturnCode,
                                        rsp.Results?.StatusCode ?? 0,
                                        rsp.Results?.StatusString ?? string.Empty,
                                        rsp.Results?.CurrentCaptureDevice?.Device ?? string.Empty,
                                        CaptureDevices));
                            }

                            break;
                        case "Aux.GetRenderDevices.1":
                            PlaybackDevices = new List<string>();

                            if (rsp.Results == null || rsp.Results.RenderDevices == null || rsp.Results.RenderDevices.Count == 0 || rsp.Results.CurrentRenderDevice == null)
                                break;

                            foreach (var device in rsp.Results.RenderDevices)
                                PlaybackDevices.Add(device?.Device ?? string.Empty);


                            _currentPlaybackDevice = rsp.Results.CurrentRenderDevice?.Device ?? string.Empty;

                            OnAuxGetRenderDevicesResponse?.Invoke(
                                rsp.InputXml?.Request?.RequestId ?? string.Empty,
                                new VoiceDevicesEventArgs(
                                    ResponseType.GetCaptureDevices,
                                    rsp.ReturnCode,
                                    rsp.Results?.StatusCode ?? 0,
                                    rsp.Results?.StatusString ?? string.Empty,
                                    rsp.Results?.CurrentRenderDevice?.Device ?? string.Empty,
                                    PlaybackDevices));

                            break;

                        case "Account.Login.1":
                            OnAccountLoginResponse?.Invoke(rsp.InputXml?.Request?.RequestId ?? string.Empty,
                                new VoiceAccountEventArgs(
                                    rsp.ReturnCode,
                                    rsp.Results?.StatusCode ?? 0,
                                    rsp.Results?.StatusString ?? string.Empty,
                                    rsp.Results?.AccountHandle ?? string.Empty));

                            break;

                        case "Session.Create.1":
                            OnSessionCreateResponse?.Invoke(
                                rsp.InputXml?.Request?.RequestId ?? string.Empty,
                                new VoiceSessionEventArgs(
                                    rsp.ReturnCode,
                                    rsp.Results?.StatusCode ?? 0,
                                    rsp.Results?.StatusString ?? string.Empty,
                                    rsp.Results?.SessionHandle ?? string.Empty));

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
                            Logger.Error("Unimplemented response from the voice daemon: " + line);
                            break;
                    }

                    // Send the Response Event for all the simple cases.
                    if (genericResponse != ResponseType.None && OnVoiceResponse != null)
                    {
                        OnVoiceResponse?.Invoke(this,
                            new VoiceResponseEventArgs(
                                genericResponse,
                                rsp.ReturnCode,
                                rsp.Results?.StatusCode ?? 0,
                                rsp.Results?.StatusString ?? string.Empty));
                    }
                }
            }
            else if (line.Substring(0, 7) == "<Event ")
            {
                VoiceEvent? evt = null;
                try
                {
                    var tmp = EventSerializer.Deserialize(new StringReader(line));
                    evt = tmp as VoiceEvent;
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to deserialize voice daemon event", e);
                    return;
                }

                if (evt != null)
                    switch (evt.Type)
                    {
                        case "LoginStateChangeEvent":
                        case "AccountLoginStateChangeEvent":
                            {
                                int.TryParse(evt.StatusCode, out var statusCodeVal);
                                int.TryParse(evt.State, out var stateVal);
                                OnAccountLoginStateChangeEvent?.Invoke(this,
                                    new AccountLoginStateChangeEventArgs(
                                        evt.AccountHandle ?? string.Empty,
                                        statusCodeVal,
                                        evt.StatusString ?? string.Empty,
                                        (LoginState)stateVal));
                            }
                            break;

                        case "SessionNewEvent":
                            {
                                var isChannelVal = bool.TryParse(evt.IsChannel, out var chVal) && chVal;
                                OnSessionNewEvent?.Invoke(this,
                                    new NewSessionEventArgs(
                                        evt.AccountHandle ?? string.Empty,
                                        evt.SessionHandle ?? string.Empty,
                                        evt.URI ?? string.Empty,
                                        isChannelVal,
                                        evt.Name ?? string.Empty,
                                        evt.AudioMedia ?? string.Empty));
                            }

                            break;

                        case "SessionStateChangeEvent":
                            {
                                int.TryParse(evt.StatusCode, out var statusCodeVal);
                                int.TryParse(evt.State, out var stateVal);
                                var isChannelVal = bool.TryParse(evt.IsChannel, out var isCh) && isCh;
                                OnSessionStateChangeEvent?.Invoke(this,
                                    new SessionStateChangeEventArgs(
                                        evt.SessionHandle ?? string.Empty,
                                        statusCodeVal,
                                        evt.StatusString ?? string.Empty,
                                        (SessionState)stateVal,
                                        evt.URI ?? string.Empty,
                                        isChannelVal,
                                        evt.ChannelName ?? string.Empty));
                            }

                            break;

                        case "ParticipantAddedEvent":
                            Logger.Debug("Add participant " + (evt.ParticipantUri ?? string.Empty));
                            {
                                int.TryParse(evt.ParticipantType, out var pTypeVal);
                                OnSessionParticipantAddedEvent?.Invoke(this,
                                    new ParticipantAddedEventArgs(
                                        evt.SessionGroupHandle ?? string.Empty,
                                        evt.SessionHandle ?? string.Empty,
                                        evt.ParticipantUri ?? string.Empty,
                                        evt.AccountName ?? string.Empty,
                                        evt.DisplayName ?? string.Empty,
                                        (ParticipantType)pTypeVal,
                                        evt.Application ?? string.Empty));
                            }

                            break;

                        case "ParticipantRemovedEvent":
                            OnSessionParticipantRemovedEvent?.Invoke(this,
                                new ParticipantRemovedEventArgs(
                                    evt.SessionGroupHandle ?? string.Empty,
                                    evt.SessionHandle ?? string.Empty,
                                    evt.ParticipantUri ?? string.Empty,
                                    evt.AccountName ?? string.Empty,
                                    evt.Reason ?? string.Empty));

                            break;

                        case "ParticipantStateChangeEvent":
                            // Useful in person-to-person calls
                            {
                                int.TryParse(evt.StatusCode, out var statusCodeVal);
                                int.TryParse(evt.State, out var stateVal);
                                int.TryParse(evt.ParticipantType, out var participantTypeVal);
                                OnSessionParticipantStateChangeEvent?.Invoke(this,
                                    new ParticipantStateChangeEventArgs(
                                        evt.SessionHandle ?? string.Empty,
                                        statusCodeVal,
                                        evt.StatusString ?? string.Empty,
                                        (ParticipantState)stateVal, // Ringing, Connected, etc
                                        evt.ParticipantUri ?? string.Empty,
                                        evt.AccountName ?? string.Empty,
                                        evt.DisplayName ?? string.Empty,
                                        (ParticipantType)participantTypeVal));
                            }

                            break;

                        case "ParticipantPropertiesEvent":
                            {
                                var isLocallyMuted = bool.TryParse(evt.IsLocallyMuted, out var lm) && lm;
                                var isModeratorMuted = bool.TryParse(evt.IsModeratorMuted, out var mm) && mm;
                                var isSpeaking = bool.TryParse(evt.IsSpeaking, out var spk) && spk;
                                var volume = int.TryParse(evt.Volume, out var vol) ? vol : 0;
                                var energy = float.TryParse(evt.Energy, out var en) ? en : 0.0f;
                                OnSessionParticipantPropertiesEvent?.Invoke(this,
                                    new ParticipantPropertiesEventArgs(
                                        evt.SessionHandle ?? string.Empty,
                                        evt.ParticipantUri ?? string.Empty,
                                        isLocallyMuted,
                                        isModeratorMuted,
                                        isSpeaking,
                                        volume,
                                        energy));
                            }

                            break;

                        case "ParticipantUpdatedEvent":
                            {
                                var isModeratorMuted2 = bool.TryParse(evt.IsModeratorMuted, out var mm2) && mm2;
                                var isSpeaking2 = bool.TryParse(evt.IsSpeaking, out var spk2) && spk2;
                                var volume2 = int.TryParse(evt.Volume, out var vol2) ? vol2 : 0;
                                var energy2 = float.TryParse(evt.Energy, out var en2) ? en2 : 0.0f;
                                OnSessionParticipantUpdatedEvent?.Invoke(this,
                                    new ParticipantUpdatedEventArgs(
                                        evt.SessionHandle ?? string.Empty,
                                        evt.ParticipantUri ?? string.Empty,
                                        isModeratorMuted2,
                                        isSpeaking2,
                                        volume2,
                                        energy2));
                            }

                            break;

                        case "SessionGroupAddedEvent":
                            OnSessionGroupAddedEvent?.Invoke(this,
                                new SessionGroupAddedEventArgs(
                                    evt.AccountHandle ?? string.Empty,
                                    evt.SessionGroupHandle ?? string.Empty,
                                    evt.Type ?? string.Empty));

                            break;

                        case "SessionAddedEvent":
                            {
                                var isChannel = bool.TryParse(evt.IsChannel, out var ch) && ch;
                                var incoming = bool.TryParse(evt.Incoming, out var inc) && inc;
                                OnSessionAddedEvent?.Invoke(this,
                                    new SessionAddedEventArgs(
                                        evt.SessionGroupHandle ?? string.Empty,
                                        evt.SessionHandle ?? string.Empty,
                                        evt.Uri ?? string.Empty,
                                        isChannel,
                                        incoming));
                            }

                            break;

                        case "SessionRemovedEvent":
                            OnSessionRemovedEvent?.Invoke(this,
                                new SessionRemovedEventArgs(
                                    evt.SessionGroupHandle ?? string.Empty,
                                    evt.SessionHandle ?? string.Empty,
                                    evt.Uri ?? string.Empty));

                            break;

                        case "SessionUpdatedEvent":
                            // Use null-conditional invoke to avoid dereferencing a possibly-null event
                            // and to match the nullable event declarations.
                            if (OnSessionUpdatedEvent != null)
                            {
                                int.TryParse(evt.IsMuted, out var isMutedVal);
                                int.TryParse(evt.Volume, out var volumeVal);
                                int.TryParse(evt.TransmitEnabled, out var transmitVal);
                                int.TryParse(evt.IsFocused, out var focusedVal);

                                OnSessionUpdatedEvent?.Invoke(this,
                                    new SessionUpdatedEventArgs(
                                        evt.SessionGroupHandle ?? string.Empty,
                                        evt.SessionHandle ?? string.Empty,
                                        evt.Uri ?? string.Empty,
                                        isMutedVal != 0,
                                        volumeVal,
                                        transmitVal != 0,
                                        focusedVal != 0));
                            }

                            break;

                        case "AuxAudioPropertiesEvent":
                            {
                                var micIsActive = bool.TryParse(evt.MicIsActive, out var micActive) && micActive;
                                var micEnergy = float.TryParse(evt.MicEnergy, out var micE) ? micE : 0.0f;
                                var micVolume = int.TryParse(evt.MicVolume, out var micV) ? micV : 0;
                                var speakerVol = int.TryParse(evt.SpeakerVolume, out var spV) ? spV : 0;
                                OnAuxAudioPropertiesEvent?.Invoke(this,
                                    new AudioPropertiesEventArgs(micIsActive, micEnergy, micVolume, speakerVol));
                            }

                            break;

                        case "SessionMediaEvent":
                            {
                                var hasText = bool.TryParse(evt.HasText, out var t) && t;
                                var hasAudio = bool.TryParse(evt.HasAudio, out var a) && a;
                                var hasVideo = bool.TryParse(evt.HasVideo, out var v) && v;
                                var terminated = bool.TryParse(evt.Terminated, out var term) && term;
                                OnSessionMediaEvent?.Invoke(this,
                                    new SessionMediaEventArgs(evt.SessionHandle ?? string.Empty, hasText, hasAudio, hasVideo, terminated));
                            }

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
                            Logger.Error("Unimplemented event from the voice daemon: " + line);
                            break;
                    }
            }
            else
            {
                Logger.Error("Unrecognized data from the voice daemon: " + line);
            }
        }
    }
}

