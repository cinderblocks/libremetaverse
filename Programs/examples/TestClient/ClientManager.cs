/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2024, Sjofn, LLC
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
using System.Linq;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    public class LoginDetails
    {
        public string FirstName;
        public string LastName;
        public string Password;
        public string StartLocation;
        public bool GroupCommands;
        public string MasterName;
        public UUID MasterKey;
        public string URI;
    }

    public sealed class ClientManager
    {
        const string VERSION = "1.0.0";

        class Singleton { internal static readonly ClientManager Instance = new ClientManager(); }
        public static ClientManager Instance => Singleton.Instance;

        public Dictionary<UUID, TestClient> Clients = new Dictionary<UUID, TestClient>();
        public Dictionary<Simulator, Dictionary<uint, Primitive>> SimPrims = new Dictionary<Simulator, Dictionary<uint, Primitive>>();

        public bool Running = true;
        public bool GetTextures = false;
        public volatile int PendingLogins = 0;
        public string onlyAvatar = string.Empty;

        ClientManager()
        {
        }

        public void Start(List<LoginDetails> accounts, bool getTextures)
        {
            GetTextures = getTextures;

            foreach (LoginDetails account in accounts)
                Login(account);
        }

        /// <summary>
        /// Login command with required args
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public TestClient Login(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: login firstname lastname password [simname] [login server url]");
                return null;
            }

            LoginDetails account = new LoginDetails
            {
                FirstName = args[0], 
                LastName = args[1], 
                Password = args[2]
            };

            if (args.Length > 3)
            {
                // If it looks like a full starting position was specified, parse it
                if (args[3].StartsWith("http"))
                {
                    account.URI = args[3];
                }
                else
                {
                    if (args[3].IndexOf('/') >= 0)
                    {
                        char sep = '/';
                        string[] startbits = args[3].Split(sep);
                        try
                        {
                            account.StartLocation = NetworkManager.StartLocation(startbits[0], int.Parse(startbits[1]),
                              int.Parse(startbits[2]), int.Parse(startbits[3]));
                        }
                        catch (FormatException) { }
                    }

                    // Otherwise, use the center of the named region
                    if (account.StartLocation == null)
                    {
                        account.StartLocation = NetworkManager.StartLocation(args[3], 128, 128, 40);
                    }
                }
            }

            if (args.Length > 4)
                if (args[4].StartsWith("http"))
                    account.URI = args[4];

            if (string.IsNullOrEmpty(account.URI))
                account.URI = Program.LoginURI;
            Logger.Log($"Using login URI {account.URI}", Helpers.LogLevel.Info);

            return Login(account);
        }

        /// <summary>
        /// Login account with provided <seealso cref="LoginDetails"/> 
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public TestClient Login(LoginDetails account)
        {
            // Check if this client is already logged in
            foreach (var c in Clients.Values.Where(
                         tc => tc.Self.FirstName == account.FirstName
                                   && tc.Self.LastName == account.LastName))
            {
                Logout(c);
                break;
            }

            ++PendingLogins;

            TestClient client = new TestClient(this)
            {
                Settings = { MFA_ENABLED = true }
            };
            client.Network.LoginProgress +=
                delegate(object sender, LoginProgressEventArgs e)
                {
                    Logger.Log($"Login {e.Status}: {e.Message}", Helpers.LogLevel.Info, client);

                    if (e.Status == LoginStatus.Success)
                    {
                        Clients[client.Self.AgentID] = client;

                        if (client.MasterKey == UUID.Zero)
                        {
                            UUID query = UUID.Zero;

                            void PeopleDirCallback(object sender2, DirPeopleReplyEventArgs dpe)
                            {
                                if (dpe.QueryID != query) { return; }
                                if (dpe.MatchedPeople.Count != 1)
                                {
                                    Logger.Log($"Unable to resolve master key from {client.MasterName}", Helpers.LogLevel.Warning);
                                }
                                else
                                {
                                    client.MasterKey = dpe.MatchedPeople[0].AgentID;
                                    Logger.Log($"Master key resolved to {client.MasterKey}", Helpers.LogLevel.Info);
                                }
                            }

                            client.Directory.DirPeopleReply += PeopleDirCallback;
                            query = client.Directory.StartPeopleSearch(client.MasterName, 0);
                        }

                        Logger.Log($"Logged in {client}", Helpers.LogLevel.Info);
                        --PendingLogins;
                    }
                    else if (e.Status == LoginStatus.Failed)
                    {
                        Logger.Log($"Failed to login {account.FirstName} {account.LastName}: {client.Network.LoginMessage}", 
                            Helpers.LogLevel.Warning);
                        --PendingLogins;
                    }
                };

            // Optimize the throttle
            client.Throttle.Wind = 0;
            client.Throttle.Cloud = 0;
            client.Throttle.Land = 1000000;
            client.Throttle.Task = 1000000;

            client.GroupCommands = account.GroupCommands;
			client.MasterName = account.MasterName;
            client.MasterKey = account.MasterKey;
            client.AllowObjectMaster = client.MasterKey != UUID.Zero; // Require UUID for object master.

            LoginParams loginParams = client.Network.DefaultLoginParams(
                    account.FirstName, account.LastName, account.Password, "TestClient", VERSION);

            if (!string.IsNullOrEmpty(account.StartLocation))
                loginParams.Start = account.StartLocation;

            if (!string.IsNullOrEmpty(account.URI))
                loginParams.URI = account.URI;

            client.Network.BeginLogin(loginParams);
            return client;
        }

        /// <summary>
        /// Begin running client
        /// </summary>
        /// <param name="noGUI">Run with Gui or nah</param>
        public void Run(bool noGUI)
        {
            if (noGUI)
            {
                while (Running)
                {
                    Thread.Sleep(2 * 1000);
                }
            }
            else {
                Console.WriteLine("Type quit to exit.  Type help for a command list.");

                while (Running)
                {
                    PrintPrompt();
                    string input = Console.ReadLine();
                    DoCommandAll(input, UUID.Zero);
                }
            }

            foreach (var client in Clients.Values.Cast<GridClient>().Where(client => client.Network.Connected))
            {
                client.Network.Logout();
            }
        }

        /// <summary>
        /// Print GUI prompt to screen
        /// </summary>
        private void PrintPrompt()
        {
            int online = Clients.Values.Cast<GridClient>().Count(client => client.Network.Connected);

            Console.Write($"{online} avatars online> ");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="fromAgentID"></param>
        /// <param name="imSessionID"></param>
        public void DoCommandAll(string cmd, UUID fromAgentID)
        {
            if (cmd == null)
                return;
            string[] tokens = cmd.Trim().Split(' ', '\t');
            if (tokens.Length == 0)
                return;
            
            string firstToken = tokens[0].ToLower();
            if (string.IsNullOrEmpty(firstToken))
                return;

            // Allow for comments when cmdline begins with ';' or '#'
            if (firstToken[0] == ';' || firstToken[0] == '#')
                return;

            if ('@' == firstToken[0]) {
                onlyAvatar = string.Empty;
                if (tokens.Length == 3) {
                    onlyAvatar = tokens[1]+" "+tokens[2];
                    bool found = Clients.Values.Any(client => (client.ToString() == onlyAvatar) && (client.Network.Connected));

                    Logger.Log(
                        found
                            ? $"Commanding only {onlyAvatar} now"
                            : $"Commanding nobody now. Avatar {onlyAvatar} is offline", Helpers.LogLevel.Info);
                } else {
                    Logger.Log("Commanding all avatars now", Helpers.LogLevel.Info);
                }
                return;
            }
            
            string[] args = new string[tokens.Length - 1];
            if (args.Length > 0)
                Array.Copy(tokens, 1, args, 0, args.Length);

            if (firstToken == "login")
            {
                Login(args);
            }
            else if (firstToken == "quit")
            {
                Quit();
                Logger.Log("All clients logged out and program finished running.", Helpers.LogLevel.Info);
            }
            else if (firstToken == "help")
            {
                if (Clients.Count > 0)
                {
                    foreach (TestClient client in Clients.Values)
                    {
                        Console.WriteLine(client.Commands["help"].Execute(args, UUID.Zero));
                        break;
                    }
                }
                else
                {
                    Console.WriteLine("You must login at least one bot to use the help command");
                }
            }
            else if (firstToken == "script")
            {
                // No reason to pass this to all bots, and we also want to allow it when there are no bots
                ScriptCommand command = new ScriptCommand(null);
                Logger.Log(command.Execute(args, UUID.Zero), Helpers.LogLevel.Info);
            }
            else if (firstToken == "waitforlogin")
            {
                // Special exception to allow this to run before any bots have logged in
                if (ClientManager.Instance.PendingLogins > 0)
                {
                    WaitForLoginCommand command = new WaitForLoginCommand(null);
                    Logger.Log(command.Execute(args, UUID.Zero), Helpers.LogLevel.Info);
                }
                else
                {
                    Logger.Log("No pending logins", Helpers.LogLevel.Info);
                }
            }
            else
            {
                // Make an immutable copy of the Clients dictionary to safely iterate over
                Dictionary<UUID, TestClient> clientsCopy = new Dictionary<UUID, TestClient>(Clients);

                int completed = 0;

                foreach (TestClient client in clientsCopy.Values)
                {
                    ThreadPool.QueueUserWorkItem(
                        delegate(object state)
                        {
                            TestClient testClient = (TestClient)state;
                            if ((string.Empty == onlyAvatar) || (testClient.ToString() == onlyAvatar)) {
                                if (testClient.Commands.ContainsKey(firstToken)) {
                                    string result;
                                    try {
                                        result = testClient.Commands[firstToken].Execute(args, fromAgentID);
                                        Logger.Log(result, Helpers.LogLevel.Info, testClient);
                                    } catch(Exception e) {
                                        Logger.Log($"{firstToken} raised exception {e}",
                                                   Helpers.LogLevel.Error,
                                                   testClient);
                                    }
                                } else
                                    Logger.Log($"Unknown command {firstToken}", Helpers.LogLevel.Warning);
                            }

                            ++completed;
                        },
                        client);
                }

                while (completed < clientsCopy.Count)
                    Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Logout specified <seealso cref="TestClient"/> instance
        /// </summary>
        /// <param name="client">Client to perform logout</param>
        public void Logout(TestClient client)
        {
            Clients.Remove(client.Self.AgentID);
            client.Network.Logout();
        }

        /// <summary>
        /// Exit the program
        /// </summary>
        public void Quit()
        {
            Running = false;
            // TODO: It would be really nice if we could figure out a way to abort the ReadLine here in so that Run() will exit.
        }
    }
}
