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

using OpenMetaverse;
using System;
using LibreMetaverse.Voice.WebRTC;
using System.Collections.Generic;
using System.Threading;

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
        private static readonly AutoResetEvent ProvisionEvent = new AutoResetEvent(false);
        private static readonly AutoResetEvent ParcelVoiceInfoEvent = new AutoResetEvent(false);
        private static string VoiceAccount = string.Empty;
        private static string VoicePassword = string.Empty;
        private static string VoiceRegionName = string.Empty;
        private static int VoiceLocalID = 0;
        private static string VoiceChannelURI = string.Empty;

        private static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: WebRtcTest.exe [firstname] [lastname] [password]");
                return;
            }

            string firstName = args[0];
            string lastName = args[1];
            string password = args[2];

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
            Settings.LOG_LEVEL = Helpers.LogLevel.Debug;

            string loginURI = client.Settings.LOGIN_SERVER;
            if (4 == args.Length)
            {
                loginURI = args[3];
            }

            VoiceManager voice = new VoiceManager(client);
            //voice.OnProvisionAccount += voice_OnProvisionAccount;
            //voice.OnParcelVoiceInfo += voice_OnParcelVoiceInfo;

            client.Network.EventQueueRunning += client_OnEventQueueRunning;

            // SETUP!
            try
            {
                /*List<string> captureDevices = voice.CaptureDevices();

                Console.WriteLine("Capture Devices:");
                for (int i = 0; i < captureDevices.Count; i++)
                    Console.WriteLine("{0}. \"{1}\"", i, captureDevices[i]);
                Console.WriteLine();

                List<string> renderDevices = voice.RenderDevices();

                Console.WriteLine("Render Devices:");
                for (int i = 0; i < renderDevices.Count; i++)
                    Console.WriteLine("{0}. \"{1}\"", i, renderDevices[i]);
                Console.WriteLine();*/


                // Login
                Console.WriteLine("Logging into the grid as " + firstName + " " + lastName + "...");
                LoginParams loginParams =
                    client.Network.DefaultLoginParams(firstName, lastName, password, "WebRtc Test", "1.0.0");
                loginParams.URI = loginURI;
                if (!client.Network.Login(loginParams))
                    throw new VoiceTestException("Login to SL failed: " + client.Network.LoginMessage);
                Console.WriteLine("Logged in: " + client.Network.LoginMessage);

                Console.WriteLine("Waiting for OnEventQueueRunning");
                if (!EventQueueRunningEvent.WaitOne(45 * 1000, false))
                    throw new VoiceTestException("EventQueueRunning event did not occur", true);
                Console.WriteLine("EventQueue running");

                var cap = client.Network.CurrentSim.Caps?.CapabilityURI("ProvisionVoiceAccountRequest");

                if (cap == null)
                {
                    throw new VoiceTestException($"No ProvisionVoiceAccountRequest capability available", true);
                }


                Console.WriteLine($"Requesting a provisional account from {client.Network.CurrentSim.Name}...");
                bool success = voice.ConnectPrimaryRegion().Result;
                if (!success)
                {
                    Console.WriteLine($"Failed to connect voice to '{client.Network.CurrentSim.Name}'.");
                }
                else
                {
                    Console.WriteLine($"Connected to voice in '{client.Network.CurrentSim.Name}'");
                    //Console.WriteLine($"==== LOCAL ====\n{voice.sdpLocal}\n ==== REMOTE ====\n{voice.sdpRemote}");
                }
                

                /*if (!voice.RequestParcelVoiceInfo())
                    throw new Exception("Failed to request parcel voice info");
                if (!ParcelVoiceInfoEvent.WaitOne(45 * 1000, false))
                    throw new VoiceTestException("Failed to obtain parcel info voice", true);


                Console.WriteLine("Parcel Voice Info obtained. Region name {0}, local parcel ID {1}, channel URI {2}",
                    VoiceRegionName, VoiceLocalID, VoiceChannelURI);*/

                Console.WriteLine($"Connected Primary Region to voice {client.Network.CurrentSim.Name}...");

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

        private static void client_OnEventQueueRunning(object sender, EventQueueRunningEventArgs e)
        {
            EventQueueRunningEvent.Set();
        }

        private static void voice_OnProvisionAccount(string username, string password)
        {
            VoiceAccount = username;
            VoicePassword = password;

            ProvisionEvent.Set();
        }

        private static void voice_OnParcelVoiceInfo(string regionName, int localID, string channelURI)
        {
            VoiceRegionName = regionName;
            VoiceLocalID = localID;
            VoiceChannelURI = channelURI;

            ParcelVoiceInfoEvent.Set();
        }
    }
}