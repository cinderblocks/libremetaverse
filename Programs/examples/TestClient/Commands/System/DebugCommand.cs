using Microsoft.Extensions.Logging;
using OpenMetaverse;

namespace TestClient.Commands.System
{
    public class DebugCommand : Command
    {
        public DebugCommand(TestClient testClient)
        {
            Name = "debug";
            Description = "Turn debug messages on or off. Usage: debug [level] where level is one of None, Debug, Error, Info, Warn";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
            {
                return "Usage: debug [level] where level is one of None, Debug, Error, Info, Warn";
            }

            switch (args[0].ToLower())
            {
                case "trace":
                    Settings.LOG_LEVEL = LogLevel.Trace;
                    return "Logging is set to Trace";
                case "debug":
                    Settings.LOG_LEVEL = LogLevel.Debug;
                    return "Logging is set to Debug";
                case "none":
                    Settings.LOG_LEVEL = LogLevel.None;
                    return "Logging is set to None";
                case "warn":
                    Settings.LOG_LEVEL = LogLevel.Warning;
                    return "Logging is set to level Warning";
                case "info":
                    Settings.LOG_LEVEL = LogLevel.Information;
                    return "Logging is set to level Info";
                case "error":
                    Settings.LOG_LEVEL = LogLevel.Error;
                    return "Logging is set to level Error";
                case "critical":
                    Settings.LOG_LEVEL = LogLevel.Critical;
                    return "Logging is set to Critical";
                default:
                    return "Usage: debug [level] where level is one of None, Trace, Debug, Info, Warn, Error, Critical";
            }
        }
    }
}
