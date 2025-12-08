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

using LibreMetaverse.Voice.WebRTC;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;

namespace WebRtcTest
{
    [Serializable]
    public class VoiceTestException : Exception
    {
        public bool LoggedIn = false;

        public VoiceTestException(string msg) : base(msg)
        {
        }

        public VoiceTestException(string msg, bool loggedIn) : base(msg)
        {
            LoggedIn = loggedIn;
        }
    }

    internal class WebRtcTest
    {
        private static readonly AutoResetEvent EventQueueRunningEvent = new AutoResetEvent(false);

        private static async Task Main(string[] args)
        {
            // Install native crash dumper to capture native heap corruption and write .dmp files
            NativeCrashDumper.InstallCrashHandler();

            if (args.Length < 3)
            {
                Console.WriteLine("Usage: WebRtcTest.exe [firstname] [lastname] [password]");
                return;
            }

            string firstName = args[0];
            string lastName = args[1];
            string password = args[2];

            Settings.LOG_LEVEL = Microsoft.Extensions.Logging.LogLevel.Debug;

            GridClient client = new GridClient
            {
                Settings =
                {
                    MULTIPLE_SIMS = false,
                    LOG_RESENDS = false,
                    STORE_LAND_PATCHES = true,
                    ALWAYS_DECODE_OBJECTS = true,
                    ALWAYS_REQUEST_OBJECTS = true,
                    SEND_AGENT_UPDATES = true
                }
            };
            client.Network.EventQueueRunning += client_OnEventQueueRunning;
            client.Network.LoginProgress += client_OnLoginProgress;

            string loginURI = client.Settings.LOGIN_SERVER;
            if (4 == args.Length)
            {
                loginURI = args[3];
            }

            VoiceManager voice = new VoiceManager(client);
            voice.PeerAudioUpdated += WebRtc_PeerAudioUpdated;
            voice.PeerPositionUpdatedTyped += WebRtc_PeerPositionUpdatedTyped;
            voice.MuteMapReceived += WebRtc_MuteMapReceived;
            voice.GainMapReceived += WebRtc_GainMapReceived;

            // SETUP!
            try
            {
                // Login
                Console.WriteLine($"Logging into the grid as {firstName} {lastName}...");
                LoginParams loginParams =
                    client.Network.DefaultLoginParams(firstName, lastName, password, "WebRtc Test", "1.0.0");
                loginParams.URI = loginURI;
                loginParams.LoginLocation = "WebRTC Voice 1/128/128/50";
                if (!client.Network.Login(loginParams))
                    throw new VoiceTestException("Login to SL failed: " + client.Network.LoginMessage);
                Console.WriteLine("Logged in: " + client.Network.LoginMessage);

                Console.WriteLine("Waiting for OnEventQueueRunning");
                if (!EventQueueRunningEvent.WaitOne(TimeSpan.FromSeconds(45), false))
                    throw new VoiceTestException("EventQueueRunning event did not occur", true);
                Console.WriteLine("EventQueue running");

                var cap = client.Network.CurrentSim.Caps?.CapabilityURI("ProvisionVoiceAccountRequest");

                if (cap == null)
                {
                    throw new VoiceTestException("No ProvisionVoiceAccountRequest capability available", true);
                }


                Console.WriteLine($"Requesting a provisional account from {client.Network.CurrentSim.Name}...");
                bool success = await voice.ConnectPrimaryRegion();
                if (!success)
                {
                    Console.WriteLine($"Failed to connect voice to '{client.Network.CurrentSim.Name}'.");
                }
                else
                {
                    Console.WriteLine($"Connected to voice in '{client.Network.CurrentSim.Name}'");
                }
                
                Console.WriteLine($"Connected Primary Region to voice {client.Network.CurrentSim.Name}...");

                // Example: play the raw PCM file in the WebRtcTest directory
                try
                {
                    // Try to locate the file relative to the running directory first
                    var candidates = new[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scarlet-fire-44100.pcm"),
                        Path.Combine(Directory.GetCurrentDirectory(), "scarlet-fire-44100.pcm"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\", "scarlet-fire-44100.pcm")
                    };
                    string pcmPath = candidates.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
                    if (!string.IsNullOrEmpty(pcmPath))
                    {
                        Console.WriteLine($"Playing raw PCM file as microphone: {pcmPath} (44100 Hz, 16-bit, mono)\nLooping... Press any key to stop playback and disconnect.");
                        // Play at 44100 Hz, mono, loop
                        voice.PlayRawFile(pcmPath, channels: 1, sampleRate: 44100, loop: true);
                    }
                    else
                    {
                        Console.WriteLine("scarlet-fire-44100.pcm not found in working directories; skipping example playback.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start example PCM playback: {ex.Message}");
                }

                Console.WriteLine("Press any key to disconnect...");
                Console.ReadKey();

                voice.Disconnect();

                client.Network.Logout();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (e is VoiceTestException exception && exception.LoggedIn)
                {
                    client.Network.Logout();
                }
            }
        }

        #region GridClient handlers

        private static void client_OnLoginProgress(object sender, LoginProgressEventArgs args)
        {
            if (args.Status == LoginStatus.Success)
            {

            }
        }

        private static void client_OnEventQueueRunning(object sender, EventQueueRunningEventArgs args)
        {
            EventQueueRunningEvent.Set();
        }

        #endregion GridClient handlers

        #region WebRTC handlers

        private static void WebRtc_PeerAudioUpdated(UUID id, VoiceSession.PeerAudioState state)
        {
            Logger.Info($"[Voice] PeerAudioUpdated {id} Power={state.Power} VAD={state.VoiceActive} JoinedPrimary={state.JoinedPrimary} Left={state.Left}");
        }

        private static void WebRtc_PeerPositionUpdatedTyped(UUID id, VoiceSession.AvatarPosition pos)
        {
            Logger.Info($"[Voice] PeerPosition {id} sp={pos.SenderPosition?.X},{pos.SenderPosition?.Y},{pos.SenderPosition?.Z}");
        }

        private static void WebRtc_MuteMapReceived(Dictionary<UUID, bool> m)
        {
            foreach (var kv in m) Logger.Info($"[Voice] Mute {kv.Key} = {kv.Value}");
        }

        private static void WebRtc_GainMapReceived(Dictionary<UUID, int> g)
        {
            foreach (var kv in g) Logger.Info($"[Voice] Gain {kv.Key} = {kv.Value}");
        }

        #endregion
    }
}