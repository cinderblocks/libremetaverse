using System;
using System.Text;

namespace OpenMetaverse
{
    public abstract class LogWriter
    {
        public static void LogMessage(StringBuilder messageBuilder, Helpers.LogLevel Level)
        {
            var date = DateTime.Now;
            string Message = messageBuilder.ToString();
            messageBuilder.Clear();
            messageBuilder.Append("[");
            if (date.Hour < 10) messageBuilder.Append("0");
            messageBuilder.Append(date.Hour.ToString());
            messageBuilder.Append(":");
            if (date.Minute < 10) messageBuilder.Append("0");
            messageBuilder.Append(date.Minute.ToString());
            messageBuilder.Append("] ");
            switch (Level)
            {
                case Helpers.LogLevel.Debug:
                    {
                        messageBuilder.Append("Debug - ");
                        break;
                    }
                case Helpers.LogLevel.Info:
                    {
                        messageBuilder.Append("Status - ");
                        break;
                    }
                case Helpers.LogLevel.Warning:
                    {
                        messageBuilder.Append("Warn - ");
                        break;
                    }
                case Helpers.LogLevel.Error:
                    {
                        messageBuilder.Append("Error - ");
                        break;
                    }
                default:
                    break;
            }
            messageBuilder.Append(Message);
            WriteMessage(Message);
        }
        protected static void WriteMessage(string message)
        {

        }
    }
    public class NoLogWriter : LogWriter
    {
    }
    public class ConsoleWriter : LogWriter
    {
        public new static void WriteMessage(string message)
        {
            Console.WriteLine(message);
        }
    }
    
    public static class Logger
    {
        // Warn,Error,Log,Debug functions
        #region Message only methods without Levels
        public static void Warn(string message)
        {
            Log(message, Helpers.LogLevel.Warning);
        }
        public static void Error(string message)
        {
            Log(message, Helpers.LogLevel.Error);
        }
        public static void Info(string message)
        {
            Log(message, Helpers.LogLevel.Info);
        }
        public static void DebugLog(string message)
        {
            Log(message, Helpers.LogLevel.Debug);
        }
        #endregion
        #region Message+Exception without levels
        public static void Warn(string message, Exception e)
        {
            Log(message, Helpers.LogLevel.Warning, null, e);
        }
        public static void Error(string message, Exception e)
        {
            Log(message, Helpers.LogLevel.Error, null, e);
        }
        public static void Info(string message, Exception e)
        {
            Log(message, Helpers.LogLevel.Info, null, e);
        }
        public static void DebugLog(string message, Exception e)
        {
            Log(message, Helpers.LogLevel.Debug, null, e);
        }
        #endregion
        #region Message+Client without levels
        public static void Warn(string message, GridClient client)
        {
            Log(message, Helpers.LogLevel.Warning,client);
        }
        public static void Error(string message, GridClient client)
        {
            Log(message, Helpers.LogLevel.Error, client);
        }
        public static void Info(string message, GridClient client)
        {
            Log(message, Helpers.LogLevel.Info, client);
        }
        public static void DebugLog(string message, GridClient client)
        {
            Log(message, Helpers.LogLevel.Debug, client);
        }
        #endregion
        #region Message+Client+Exeption without levels [Anything else call Log Directly]
        public static void Warn(string message, GridClient client, Exception exception)
        {
            Log(message, Helpers.LogLevel.Warning, client, exception);
        }
        public static void Error(string message, GridClient client, Exception exception)
        {
            Log(message, Helpers.LogLevel.Error, client, exception);
        }
        public static void Info(string message, GridClient client, Exception exception)
        {
            Log(message, Helpers.LogLevel.Info, client, exception);
        }
        public static void DebugLog(string message, GridClient client, Exception exception)
        {
            Log(message, Helpers.LogLevel.Debug, client, exception);
        }
        #endregion

        public static void Log(string message, Helpers.LogLevel Level, GridClient client, Exception exception)
        {
            if (Settings.LOG_LEVEL >= Level)
            {
                StringBuilder NewMessage = new StringBuilder();
                if (client != null)
                {
                    if (client.Settings.LOG_NAMES)
                    {
                        NewMessage.Append("{");
                        NewMessage.Append(client.Self.Name);
                        NewMessage.Append("} ");
                    }
                }
                NewMessage.Append(message);
                switch (Settings.LOG_WRITER)
                {
                    case "NoLogWriter":
                        {
                            break;
                        }
                    default:
                        {
                            ConsoleWriter.LogMessage(NewMessage, Level);
                            break;
                        }
                }
            }
        }
        public static void LogFormat(string Message, Helpers.LogLevel Level, int Value)
        {
            LogFormat(Message, new[] { Value.ToString() });
        }
        public static void LogFormat(string Message, string[] Values)
        {
            LogFormat(Message, Helpers.LogLevel.Debug,Values);
        }
        public static void LogFormat(string Message, Helpers.LogLevel Level, string[] Values)
        {
            Log(String.Format(Message, Values), Level);
        }

        #region Log overloads
        public static void Log(string message)
        {
            Log(message, Helpers.LogLevel.Debug);
        }
        public static void Log(string message, Helpers.LogLevel Level)
        {
            Log(message, Level, null, null);
        }
        public static void Log(string message, Helpers.LogLevel Level, GridClient client)
        {
            Log(message, Level, null, null);
        }
        public static void Log(string message, Helpers.LogLevel Level, Exception exception)
        {
            Log(message, Level, null, exception);
        }
        #endregion
    }
}
