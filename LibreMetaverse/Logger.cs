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
using System.Reflection;
using Microsoft.Extensions.Logging;
#if NET8_0_OR_GREATER
using ZLogger;
#endif

namespace OpenMetaverse
{
    /// <summary>
    /// Singleton logging class for the entire library
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Callback used for client apps to receive log messages from
        /// the library
        /// </summary>
        /// <param name="message">Data being logged</param>
        /// <param name="level">The severity of the log entry from <see cref="Helpers.LogLevel"/></param>
        public delegate void LogCallback(object message, Helpers.LogLevel level);

        /// <summary>Triggered whenever a message is logged. If this is left
        /// null, log messages will go to the console</summary>
        public static event LogCallback OnLogMessage;

        /// <summary>Logger instance</summary>
        private static ILogger _logger;
        private static ILoggerFactory _loggerFactory;

        static Logger()
        {
            try
            {
                // set up a default logger factory
                _loggerFactory = LoggerFactory.Create(builder =>
                {
#if NET8_0_OR_GREATER
                    // Use ZLogger console provider for .NET 8+ for higher performance
                    builder.AddZLoggerConsole();
#else
                    // Fall back to Microsoft Console provider for netstandard2.0 and older targets
                    builder.AddConsole();
#endif
                });

                _logger = _loggerFactory.CreateLogger(MethodBase.GetCurrentMethod()?.DeclaringType?.FullName ?? "LibreMetaverse");

                if (Settings.LOG_LEVEL != Helpers.LogLevel.None)
                {
                    _logger.LogInformation("Default console logger initialized");
                }
            }
            catch
            {
                // swallow any logging initialization errors to avoid breaking consumers
            }
        }

        /// <summary>
        /// Allow consumers to configure a custom ILoggerFactory (e.g. to integrate with their host)
        /// </summary>
        public static void SetLoggerFactory(ILoggerFactory factory)
        {
            _loggerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = _loggerFactory.CreateLogger("LibreMetaverse");
        }

        private static void LogWithLevel(Helpers.LogLevel level, object message, Exception exception)
        {
            if (_logger == null) return;

            var msg = message?.ToString() ?? string.Empty;

            switch (level)
            {
                case Helpers.LogLevel.Debug when exception != null:
                    _logger.LogDebug(exception, "{Message}", msg);
                    break;
                case Helpers.LogLevel.Debug:
                    _logger.LogDebug("{Message}", msg);
                    break;
                case Helpers.LogLevel.Info when exception != null:
                    _logger.LogInformation(exception, "{Message}", msg);
                    break;
                case Helpers.LogLevel.Info:
                    _logger.LogInformation("{Message}", msg);
                    break;
                case Helpers.LogLevel.Warning when exception != null:
                    _logger.LogWarning(exception, "{Message}", msg);
                    break;
                case Helpers.LogLevel.Warning:
                    _logger.LogWarning("{Message}", msg);
                    break;
                case Helpers.LogLevel.Error when exception != null:
                    _logger.LogError(exception, "{Message}", msg);
                    break;
                case Helpers.LogLevel.Error:
                    _logger.LogError("{Message}", msg);
                    break;
                default:
                {
                    if (exception != null)
                        _logger.LogTrace(exception, "{Message}", msg);
                    else
                        _logger.LogTrace("{Message}", msg);
                    break;
                }
            }
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        public static void Log(object message, Helpers.LogLevel level)
        {
            Log(message, level, null, null);
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="client">Instance of the client</param>
        public static void Log(object message, Helpers.LogLevel level, GridClient client)
        {
            Log(message, level, client, null);
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="exception">Exception that was raised</param>
        public static void Log(object message, Helpers.LogLevel level, Exception exception)
        {
            Log(message, level, null, exception);
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="client">Instance of the client</param>
        /// <param name="exception">Exception that was raised</param>
        public static void Log(object message, Helpers.LogLevel level, GridClient client, Exception exception)
        {
            if (client != null && client.Settings.LOG_NAMES)
                message = $"<{client.Self.Name}>: {message}";

            // OnLogMessage may be subscribed and expect the library's enum; keep behavior
            OnLogMessage?.Invoke(message, level);

            if (Settings.LOG_LEVEL == Helpers.LogLevel.None) { return; }

            // enforce configured log level
            bool shouldLog = false;
            switch (level)
            {
                case Helpers.LogLevel.Debug:
                    shouldLog = (Settings.LOG_LEVEL == Helpers.LogLevel.Debug);
                    break;
                case Helpers.LogLevel.Info:
                    shouldLog = (Settings.LOG_LEVEL == Helpers.LogLevel.Debug || Settings.LOG_LEVEL == Helpers.LogLevel.Info);
                    break;
                case Helpers.LogLevel.Warning:
                    shouldLog = (Settings.LOG_LEVEL == Helpers.LogLevel.Debug || Settings.LOG_LEVEL == Helpers.LogLevel.Info || Settings.LOG_LEVEL == Helpers.LogLevel.Warning);
                    break;
                case Helpers.LogLevel.Error:
                    shouldLog = (Settings.LOG_LEVEL == Helpers.LogLevel.Debug || Settings.LOG_LEVEL == Helpers.LogLevel.Info || Settings.LOG_LEVEL == Helpers.LogLevel.Warning || Settings.LOG_LEVEL == Helpers.LogLevel.Error);
                    break;
            }

            if (!shouldLog) { return; }

            LogWithLevel(level, message, exception);
        }

        /// <summary>
        /// If the library is compiled with DEBUG defined, an event will be
        /// fired if a <see cref="OnLogMessage" /> handler is registered and the
        /// message will be sent to the logging engine
        /// </summary>
        /// <param name="message">The message to log at the DEBUG level to the
        /// current logging engine</param>
        public static void DebugLog(object message)
        {
            DebugLog(message, null);
        }

        /// <summary>
        /// If the library is compiled with DEBUG defined and
        /// <see cref="GridClient.Settings.DEBUG" /> is true, an event will be
        /// fired if a <see cref="OnLogMessage" /> handler is registered and the
        /// message will be sent to the logging engine
        /// </summary>
        /// <param name="message">The message to log at the DEBUG level to the
        /// current logging engine</param>
        /// <param name="client">Instance of the client</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLog(object message, GridClient client)
        {
            if (Settings.LOG_LEVEL != Helpers.LogLevel.Debug) return;

            if (client != null && client.Settings.LOG_NAMES)
                message = $"<{client.Self.Name}>: {message}";

            OnLogMessage?.Invoke(message, Helpers.LogLevel.Debug);

            LogWithLevel(Helpers.LogLevel.Debug, message, null);
        }
    }
}
