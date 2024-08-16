using System;
using System.Collections.Generic;
using System.IO;
using CommandLine.Utility;

namespace OpenMetaverse.TestClient
{
    [Serializable]
    public class CommandLineArgumentsException : Exception
    {
        public CommandLineArgumentsException()
        {
        }

        public CommandLineArgumentsException(string message) : base(message)
        {
        }

        public CommandLineArgumentsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class Program
    {
        public static string LoginURI;

        private static void Usage()
        {
            Console.WriteLine("Usage: " + Environment.NewLine +
                    "TestClient.exe [--first firstname --last lastname --pass password] [--file userlist.txt] [--loginuri=\"uri\"] [--startpos \"sim/x/y/z\"] [--master \"master name\"] [--masterkey \"master uuid\"] [--gettextures] [--scriptfile \"filename\"]");
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            Arguments arguments = new Arguments(args);

            List<LoginDetails> accounts = new List<LoginDetails>();
            LoginDetails account;
            bool groupCommands = false;
            string masterName = string.Empty;
            UUID masterKey = UUID.Zero;
            string file = string.Empty;
            bool getTextures = false;
            bool noGUI = false; // true if to not prompt for input
            string scriptFile = string.Empty;

            if (arguments["groupcommands"] != null)
                groupCommands = true;

            if (arguments["masterkey"] != null)
                masterKey = UUID.Parse(arguments["masterkey"]);

            if (arguments["master"] != null)
                masterName = arguments["master"];

            if (arguments["loginuri"] != null)
                LoginURI = arguments["loginuri"];
            if (string.IsNullOrEmpty(LoginURI))
                LoginURI = Settings.AGNI_LOGIN_SERVER;
            Logger.Log("Using login URI " + LoginURI, Helpers.LogLevel.Info);

            if (arguments["gettextures"] != null)
                getTextures = true;

            if (arguments["nogui"] != null)
                noGUI = true;

            if (arguments["scriptfile"] != null)
            {
                scriptFile = arguments["scriptfile"];
                if (!File.Exists(scriptFile))
                {
                    Logger.Log($"File {scriptFile} Does not exist", Helpers.LogLevel.Error);
                    return;
                }
            }

            if (arguments["file"] != null)
            {
                file = arguments["file"];

                if (!File.Exists(file))
                {
                    Logger.Log($"File {file} Does not exist", Helpers.LogLevel.Error);
                    return;
                }

                // Loading names from a file
                try
                {
                    using (StreamReader reader = new StreamReader(file))
                    {
                        string line;
                        int lineNumber = 0;

                        while ((line = reader.ReadLine()) != null)
                        {
                            lineNumber++;
                            string[] tokens = line.Trim().Split(' ', ',');

                            if (tokens.Length >= 3)
                            {
                                account = new LoginDetails
                                {
                                    FirstName = tokens[0],
                                    LastName = tokens[1],
                                    Password = tokens[2]
                                };

                                if (tokens.Length >= 4) // Optional starting position
                                {
                                    const char sep = '/';
                                    string[] startbits = tokens[3].Split(sep);
                                    account.StartLocation = NetworkManager.StartLocation(startbits[0],
                                        int.Parse(startbits[1]),
                                        int.Parse(startbits[2]), int.Parse(startbits[3]));
                                }

                                accounts.Add(account);
                            }
                            else
                            {
                                Logger.Log("Invalid data on line " + lineNumber +
                                           ", must be in the format of: FirstName LastName Password [Sim/StartX/StartY/StartZ]",
                                    Helpers.LogLevel.Warning);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Error reading from " + args[1], Helpers.LogLevel.Error, ex);
                    return;
                }
            }
            else if (arguments["first"] != null && arguments["last"] != null && arguments["pass"] != null)
            {
                // Taking a single login off the command-line
                account = new LoginDetails
                {
                    FirstName = arguments["first"], 
                    LastName = arguments["last"], 
                    Password = arguments["pass"]
                };

                accounts.Add(account);
            }
            else if (arguments["help"] != null)
            {
                Usage();
                return;
            }

            foreach (LoginDetails a in accounts)
            {
                a.GroupCommands = groupCommands;
                a.MasterName = masterName;
                a.MasterKey = masterKey;
                a.URI = LoginURI;

                if (arguments["startpos"] != null)
                {
                    const char sep = '/';
                    string[] startbits = arguments["startpos"].Split(sep);
                    a.StartLocation = NetworkManager.StartLocation(startbits[0], int.Parse(startbits[1]),
                            int.Parse(startbits[2]), int.Parse(startbits[3]));
                }
            }

            // Login the accounts and run the input loop
            ClientManager.Instance.Start(accounts, getTextures);

            if (!string.IsNullOrEmpty(scriptFile))
                ClientManager.Instance.DoCommandAll("script " + scriptFile, UUID.Zero);

            // Then Run the ClientManager normally
            ClientManager.Instance.Run(noGUI);
        }
    }
}
