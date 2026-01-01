/*
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
using System.Threading.Tasks;
using OpenMetaverse;

namespace SimpleBot
{
    /// <summary>
    /// A simple bot that responds to instant messages and can perform basic actions.
    /// Demonstrates event handling, chat, movement, and async operations.
    /// </summary>
    internal class SimpleBot
    {
        private static GridClient? client;
        private static readonly Random random = new Random();

        static async Task<int> Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("SimpleBot - A simple interactive bot");
                Console.WriteLine();
                Console.WriteLine("Usage: SimpleBot [firstname] [lastname] [password]");
                Console.WriteLine();
                Console.WriteLine("Commands via IM:");
                Console.WriteLine("  help      - Show available commands");
                Console.WriteLine("  where     - Report current location");
                Console.WriteLine("  sit       - Sit on ground");
                Console.WriteLine("  stand     - Stand up");
                Console.WriteLine("  dance     - Dance!");
                Console.WriteLine("  fly       - Start flying");
                Console.WriteLine("  walk      - Stop flying");
                Console.WriteLine("  jump      - Jump");
                return 1;
            }

            client = new GridClient();
            
            client.Network.LoginProgress += Network_LoginProgress;
            client.Network.Disconnected += Network_Disconnected;
            client.Self.IM += Self_IM;
            client.Self.ChatFromSimulator += Self_ChatFromSimulator;

            Console.WriteLine("Logging in...");
            var loginParams = client.Network.DefaultLoginParams(args[0], args[1], args[2], 
                "SimpleBot", "1.0.0");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            try
            {
                var success = await client.Network.LoginAsync(loginParams, cts.Token);
                
                if (!success)
                {
                    Console.WriteLine($"Login failed: {client.Network.LoginMessage}");
                    return 1;
                }

                Console.WriteLine($"Logged in to {client.Network.CurrentSim.Name}");
                Console.WriteLine($"Position: {client.Self.SimPosition}");
                Console.WriteLine();
                Console.WriteLine("Bot is ready! Send me an IM with 'help' to see commands.");
                Console.WriteLine("Press Enter to logout...");
                
                Console.ReadLine();

                client.Network.Logout();
                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Login timed out");
                return 1;
            }
        }

        private static void Self_IM(object? sender, InstantMessageEventArgs e)
        {
            if (client == null || e.IM.FromAgentID == client.Self.AgentID)
                return;

            var message = e.IM.Message.Trim().ToLowerInvariant();
            var fromName = e.IM.FromAgentName;

            Console.WriteLine($"[IM] {fromName}: {e.IM.Message}");

            string response = message switch
            {
                "help" or "?" => 
                    "Commands: help, where, sit, stand, dance, fly, walk, jump",
                
                "where" or "location" => 
                    $"I'm in {client.Network.CurrentSim.Name} at {client.Self.SimPosition}",
                
                "sit" => ExecuteCommand(() => {
                    client.Self.SitOnGround();
                    return "Sitting down...";
                }),
                
                "stand" => ExecuteCommand(() => {
                    client.Self.Stand();
                    return "Standing up!";
                }),
                
                "dance" => ExecuteCommand(() => {
                    client.Self.AnimationStart(Animations.DANCE1, true);
                    return "?? Dancing!";
                }),
                
                "fly" => ExecuteCommand(() => {
                    client.Self.Fly(true);
                    return "Taking off! ??";
                }),
                
                "walk" => ExecuteCommand(() => {
                    client.Self.Fly(false);
                    return "Walking now.";
                }),
                
                "jump" => ExecuteCommand(() => {
                    client.Self.Jump(true);
                    Task.Delay(500).ContinueWith(_ => client.Self.Jump(false));
                    return "Wheee! ??";
                }),
                
                "hello" or "hi" or "hey" => 
                    $"Hello, {fromName}!",
                
                _ => 
                    "I don't understand that command. Try 'help' for a list of commands."
            };

            client.Self.InstantMessage(e.IM.FromAgentID, response);
            Console.WriteLine($"[IM] -> {fromName}: {response}");
        }

        private static string ExecuteCommand(Func<string> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private static void Self_ChatFromSimulator(object? sender, ChatEventArgs e)
        {
            if (client == null || e.SourceID == client.Self.AgentID)
                return;

            Console.WriteLine($"[Chat] {e.FromName}: {e.Message}");
            
            // Respond to greetings in local chat
            var msg = e.Message.ToLowerInvariant();
            if (msg.Contains("hello") || msg.Contains("hi "))
            {
                Task.Delay(random.Next(500, 1500)).ContinueWith(_ => 
                    client.Self.Chat($"Hello, {e.FromName}!", 0, ChatType.Normal));
            }
        }

        private static void Network_LoginProgress(object? sender, LoginProgressEventArgs e)
        {
            if (e.Status == LoginStatus.Success)
            {
                Console.WriteLine("Login successful");
            }
            else if (e.Status == LoginStatus.Failed)
            {
                Console.WriteLine($"Login failed: {e.Message}");
            }
        }

        private static void Network_Disconnected(object? sender, DisconnectedEventArgs e)
        {
            Console.WriteLine($"Disconnected: {e.Reason} - {e.Message}");
        }
    }
}
