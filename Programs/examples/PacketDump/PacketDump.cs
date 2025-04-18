/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace PacketDump
{
    internal class PacketDump
	{
        private static bool LoginSuccess = false;
        private static AutoResetEvent LoginEvent = new AutoResetEvent(false);

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
        private static void Main(string[] args)
		{
            if (args.Length != 4)
			{
				Console.WriteLine("Usage: LMV.PacketDump [firstname] [lastname] [password] [seconds (0 for infinite)]");
				return;
			}

            var client = new GridClient
            {
                Settings = { MULTIPLE_SIMS = false },
                Throttle =
                {
                    // Throttle packets that we don't want all the way down
                    Land = 0,
                    Wind = 0,
                    Cloud = 0
                }
            };

            // Setup a packet callback that is called for every packet (PacketType.Default)
            client.Network.RegisterCallback(PacketType.Default, DefaultHandler);
            
            // Register handlers for when we login, and when we are disconnected
            client.Network.LoginProgress += LoginHandler;
            client.Network.Disconnected += DisconnectHandler;

            // Start the login process
            client.Network.BeginLogin(client.Network.DefaultLoginParams(args[0], args[1], args[2], "PacketDump", "1.0.0"));

            // Wait until LoginEvent is set in the LoginHandler callback, or we time out
            if (LoginEvent.WaitOne(TimeSpan.FromSeconds(20), false))
            {
                if (LoginSuccess)
                {
                    // Network.LoginMessage is set after a successful login
                    Logger.Log("Message of the day: " + client.Network.LoginMessage, Helpers.LogLevel.Info);

                    // Determine how long to run for
                    var start = Environment.TickCount;
                    var milliseconds = int.Parse(args[3]) * 1000;
                    var forever = (milliseconds <= 0);

                    // Packet handling is done with asynchronous callbacks. Run a sleeping loop in the main
                    // thread until we run out of time or the program is closed
                    while (true)
                    {
                        System.Threading.Thread.Sleep(100);

                        if (!forever && Environment.TickCount - start > milliseconds)
                            break;
                    }

                    // Finished running, log out
                    client.Network.Logout();
                }
                else
                {
                    Logger.Log("Login failed: " + client.Network.LoginMessage, Helpers.LogLevel.Error);
                }
            }
            else
            {
                Logger.Log("Login timed out", Helpers.LogLevel.Error);
            }
		}

        private static void LoginHandler(object sender, LoginProgressEventArgs e)
        {
            Logger.Log($"Login: {e.Status} ({e.Message})", Helpers.LogLevel.Info);

            switch (e.Status)
            {
                case LoginStatus.Failed:
                    LoginEvent.Set();
                    break;
                case LoginStatus.Success:
                    LoginSuccess = true;
                    LoginEvent.Set();
                    break;
            }
        }

        public static void DisconnectHandler(object sender, DisconnectedEventArgs e)
        {
            if (e.Reason == NetworkManager.DisconnectType.NetworkTimeout)
            {
                Console.WriteLine("Network connection timed out, disconnected");
            }
            else if (e.Reason == NetworkManager.DisconnectType.ServerInitiated)
            {
                Console.WriteLine("Server disconnected us: " + e.Message);
            }
        }

        public static void DefaultHandler(object sender, PacketReceivedEventArgs e)
        {
            Logger.Log(e.Packet.ToString(), Helpers.LogLevel.Info);
        }
	}
}
