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
using System.Threading;
using System.Threading.Tasks;
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
        /// <param name="level">The severity of the log entry from <see cref="LogLevel"/></param>
        public delegate void LogCallback(object message, LogLevel level);

        /// <summary>Triggered whenever a message is logged. If this is left
        /// null, log messages will go to the console</summary>
        public static event LogCallback OnLogMessage;

        /// <summary>Logger instance</summary>
        private static ILogger _logger;
        private static ILoggerFactory _loggerFactory;
        private static readonly object _sync = new object();

#pragma warning disable CS0618 // Type or member is obsolete
        // Map library log levels to Microsoft.Extensions.Logging.LogLevel
        private static LogLevel MapLevel(Helpers.LogLevel level)
        {
            switch (level)
            {
                case Helpers.LogLevel.Debug:
                    return LogLevel.Debug;
                case Helpers.LogLevel.Info:
                    return LogLevel.Information;
                case Helpers.LogLevel.Warning:
                    return LogLevel.Warning;
                case Helpers.LogLevel.Error:
                    return LogLevel.Error;
                default:
                    return LogLevel.Trace;
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete

        static Logger()
        {
        }

        // Ensure logger factory and logger are initialized with defaults if not provided by consumer
        private static void EnsureInitialized()
        {
            if (_loggerFactory != null) return;

            lock (_sync)
            {
                if (_loggerFactory != null) return;
                try
                {
                    _loggerFactory = CreateDefaultConsoleLoggerFactory();
                    _logger = _loggerFactory.CreateLogger(MethodBase.GetCurrentMethod()?.DeclaringType?.FullName ?? "LibreMetaverse");
                    if (Settings.LOG_LEVEL != LogLevel.None)
                    {
                        _logger.LogInformation("Default console logger initialized");
                    }
                }
                catch
                {
                    // swallow any logging initialization errors to avoid breaking consumers
                }
            }
        }

        /// <summary>
        /// Allow consumers to configure a custom ILoggerFactory (e.g. to integrate with their host)
        /// </summary>
        public static void SetLoggerFactory(ILoggerFactory factory, string name)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            lock (_sync)
            {
                // dispose existing factory if different to avoid leaks
                try
                {
                    if (_loggerFactory != null && !ReferenceEquals(_loggerFactory, factory))
                    {
                        _loggerFactory.Dispose();
                    }
                }
                catch
                {
                    // ignore dispose errors
                }

                _loggerFactory = factory;
                _logger = _loggerFactory.CreateLogger(name);
            }
        }

        /// <summary>
        /// Create a recommended default ILoggerFactory configured to use ZLogger console provider on .NET 8+ (high-performance)
        /// or the standard console provider on other targets. Consumers can use this factory via SetLoggerFactory.
        /// </summary>
        public static ILoggerFactory CreateDefaultConsoleLoggerFactory()
        {
#if NET8_0_OR_GREATER
            return LoggerFactory.Create(builder =>
            {
                // Recommended minimum level can be adjusted by callers after creation
                builder.SetMinimumLevel(LogLevel.Debug);

                // Use ZLogger console provider for best performance on modern runtimes
                // Consumers who need custom formatters or sync/async options can configure the builder themselves.
                builder.AddZLoggerConsole();
            });
#else
            // Fallback for netstandard2.0: use the default console provider
            return LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });
#endif
        }

        /// <summary>
        /// Shutdown the logging system and dispose the logger factory to flush providers and release resources.
        /// Safe to call multiple times.
        /// </summary>
        public static void Shutdown()
        {
            lock (_sync)
            {
                try
                {
                    _loggerFactory?.Dispose();
                }
                catch
                {
                    // swallow dispose exceptions
                }

                _loggerFactory = null;
                _logger = null;
            }
        }

        /// <summary>
        /// Asynchronously shutdown the logging system and dispose the logger factory.
        /// If the factory supports IAsyncDisposable (on supported frameworks) its DisposeAsync will be awaited
        /// to allow providers to flush buffers. Safe to call multiple times.
        /// </summary>
        /// <param name="timeout">Optional timeout for the shutdown operation. If not specified, will wait indefinitely.</param>
        /// <param name="cancellationToken">Cancellation token (not currently used to cancel disposal).</param>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task<bool> ShutdownAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            ILoggerFactory factory = null;

            lock (_sync)
            {
                factory = _loggerFactory;
                _loggerFactory = null;
                _logger = null;
            }

            if (factory == null) return true;

            bool completedDispose = false;
            try
            {
#if NET8_0_OR_GREATER
                var asyncDisp = factory as IAsyncDisposable;
                if (asyncDisp != null)
                {
                    // If no timeout and no cancellation requested just await normally
                    if (!timeout.HasValue && !cancellationToken.CanBeCanceled)
                    {
                        await asyncDisp.DisposeAsync().ConfigureAwait(false);
                        return true;
                    }

                    // Await DisposeAsync but honor timeout/cancellation
                    var vt = asyncDisp.DisposeAsync();
                    var disposeTask = vt.AsTask();

                    Task waitTask;
                    if (timeout.HasValue)
                    {
                        waitTask = Task.Delay(timeout.Value, cancellationToken);
                    }
                    else
                    {
                        // wait only on cancellation
                        if (cancellationToken.CanBeCanceled)
                            waitTask = Task.Delay(-1, cancellationToken);
                        else
                            waitTask = Task.CompletedTask; // should not reach here
                    }

                    var completed = await Task.WhenAny(disposeTask, waitTask).ConfigureAwait(false);
                    if (completed == disposeTask)
                    {
                        await disposeTask.ConfigureAwait(false);
                        completedDispose = true;
                    }

                    return completedDispose;
                }
#endif
                if (factory is IDisposable disp)
                {
                    if (!timeout.HasValue && !cancellationToken.CanBeCanceled)
                    {
                        disp.Dispose();
                        return true;
                    }

                    // Run Dispose on thread-pool and wait with timeout/cancellation
                    var disposeTask = Task.Run(() => { try { disp.Dispose(); } catch { } });
                    Task waitTask = timeout.HasValue ? Task.Delay(timeout.Value, cancellationToken) : (cancellationToken.CanBeCanceled ? Task.Delay(-1, cancellationToken) : Task.CompletedTask);

                    var completed = await Task.WhenAny(disposeTask, waitTask).ConfigureAwait(false);
                    if (completed == disposeTask)
                    {
                        await disposeTask.ConfigureAwait(false);
                        completedDispose = true;
                    }
                }
                return completedDispose;
            }
            catch
            {
                // swallow exceptions during async shutdown
                return false;
            }
        }

        private static void LogWithLevel(LogLevel level, object message, Exception exception, string clientName = null)
        {
            EnsureInitialized();
            if (_logger == null) { return; }

            // Avoid formatting/allocations when the log level is disabled
            if (!_logger.IsEnabled(level)) { return; }

            // Use structured logging and pass object directly to avoid ToString allocation
            // Include client name in brackets when available
            if (exception != null)
            {
                if (!string.IsNullOrEmpty(clientName))
                    _logger.Log(level, exception, "[{Client}] {Message}", clientName, message);
                else
                    _logger.Log(level, exception, "{Message}", message);
            }
            else
            {
                if (!string.IsNullOrEmpty(clientName))
                    _logger.Log(level, "[{Client}] {Message}", clientName, message);
                else
                    _logger.Log(level, "{Message}", message);
            }
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="client">Instance of the client</param>
        [Obsolete("Use Microsoft.Extensions.Logging.LogLevel")]
        public static void Log(object message, Helpers.LogLevel level, GridClient client = null)
        {
            Log(message, MapLevel(level), client, null);
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="client">Instance of the client</param>
        public static void Log(object message, LogLevel level, GridClient client = null)
        {
            Log(message, level, client, null);
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="exception">Exception that was raised</param>
        [Obsolete("Use Microsoft.Extensions.Logging.LogLevel")]
        public static void Log(object message, Helpers.LogLevel level, Exception exception)
        {
            Log(message, MapLevel(level), null, exception);
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="exception">Exception that was raised</param>
        public static void Log(object message, LogLevel level, Exception exception)
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
        [Obsolete("Use Microsoft.Extensions.Logging.LogLevel")]
        public static void Log(object message, Helpers.LogLevel level, GridClient client, Exception exception)
        {
            Log(message, MapLevel(level), client, exception);
        }

        /// <summary>
        /// Send a log message to the logging engine
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">The severity of the log entry</param>
        /// <param name="client">Instance of the client</param>
        /// <param name="exception">Exception that was raised</param>
        public static void Log(object message, LogLevel level, GridClient client, Exception exception)
        {
            var clientName = (client != null && client.Settings.LOG_NAMES) ? client.Self?.Name : null;

            RaiseOnLogMessage(message, level);

            if (Settings.LOG_LEVEL == LogLevel.None) { return; }

            // enforce configured log level
            if (Settings.LOG_LEVEL > level) { return; }

            LogWithLevel(level, message, exception, clientName);
        }

        /// <summary>
        /// If the library is compiled with TRACE defined and
        /// <see cref="GridClient.Settings.TRACE" /> is true, an event will be
        /// fired if a <see cref="OnLogMessage" /> handler is registered and the
        /// message will be sent to the logging engine
        /// </summary>
        /// <param name="message">The message to log at the TRACE level to the
        /// current logging engine</param>
        /// <param name="client">Instance of the client</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLog(object message, GridClient client = null)
        {
            if (Settings.LOG_LEVEL > LogLevel.Debug) { return; }

            var clientName = (client != null && client.Settings.LOG_NAMES) ? client.Self?.Name : null;

            RaiseOnLogMessage(message, Settings.LOG_LEVEL);

            LogWithLevel(Settings.LOG_LEVEL, message, null, clientName);
        }

        /// <summary>
        /// Begin a logging scope with arbitrary state. Returns a disposable scope. If logging is not configured returns a no-op scope.
        /// </summary>
        public static IDisposable BeginScope(object state)
        {
            var logger = _logger;
            if (logger == null) { return NoopScope.Instance; }
            try
            {
                return logger.BeginScope(state);
            }
            catch
            {
                return NoopScope.Instance;
            }
        }

        /// <summary>
        /// Begin a logging scope containing client context (Client name) if available.
        /// </summary>
        public static IDisposable BeginClientScope(GridClient client)
        {
            var logger = _logger;
            if (logger == null) return NoopScope.Instance;

            string clientName = null;
            try
            {
                if (client?.Settings != null && client.Settings.LOG_NAMES)
                    clientName = client.Self?.Name;
            }
            catch
            {
                // ignore any errors while accessing client info
            }

            if (string.IsNullOrEmpty(clientName)) return NoopScope.Instance;

            try
            {
                return logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
                {
                    { "Client", clientName }
                });
            }
            catch
            {
                return NoopScope.Instance;
            }
        }

        /// <summary>
        /// Begin a logging scope containing region context (Region name/handle) if available.
        /// </summary>
        public static IDisposable BeginRegionScope(Simulator simulator)
        {
            var logger = _logger;
            if (logger == null) return NoopScope.Instance;

            string regionName = null;
            try
            {
                if (simulator != null)
                {
                    // Prefer human readable name, fall back to handle
                    regionName = !string.IsNullOrEmpty(simulator.Name) ? simulator.Name : simulator.Handle.ToString();
                }
            }
            catch
            {
                // ignore any errors while accessing simulator info
            }

            if (string.IsNullOrEmpty(regionName)) return NoopScope.Instance;

            try
            {
                return logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
                {
                    { "Region", regionName }
                });
            }
            catch
            {
                return NoopScope.Instance;
            }
        }

        /// <summary>
        /// Run an async action inside a logging scope containing client context (if available).
        /// Ensures the scope is disposed even if the action throws.
        /// </summary>
        public static async Task UseClientScopeAsync(GridClient client, Func<Task> action)
        {
            if (action == null) { throw new ArgumentNullException(nameof(action)); }

            IDisposable scope = null;
            try
            {
                scope = BeginClientScope(client);
                await action().ConfigureAwait(false);
            }
            finally
            {
                try { scope?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Run an async action inside a logging scope containing region context (if available).
        /// Ensures the scope is disposed even if the action throws.
        /// </summary>
        public static async Task UseRegionScopeAsync(Simulator simulator, Func<Task> action)
        {
            if (action == null) { throw new ArgumentNullException(nameof(action)); }

            IDisposable scope = null;
            try
            {
                scope = BeginRegionScope(simulator);
                await action().ConfigureAwait(false);
            }
            finally
            {
                try { scope?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Run an async action inside a logging scope with arbitrary state.
        /// Ensures the scope is disposed even if the action throws.
        /// </summary>
        public static async Task UseScopeAsync(object state, Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            IDisposable scope = null;
            try
            {
                scope = BeginScope(state);
                await action().ConfigureAwait(false);
            }
            finally
            {
                try { scope?.Dispose(); } catch { }
            }
        }

        // Simple no-op scope used when logging is not available
        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new NoopScope();
            private NoopScope() { }
            public void Dispose() { }
        }

        // Invoke OnLogMessage in a non-blocking way on the thread-pool
        private static void RaiseOnLogMessage(object message, LogLevel level)
        {
            var cb = OnLogMessage;
            if (cb == null) return;

            try
            {
                Task.Run(() =>
                {
                    try
                    {
                        cb(message, level);
                    }
                    catch
                    {
                        // swallow subscriber exceptions
                    }
                });
            }
            catch
            {
                // swallow Task.Run exceptions
            }
        }

        /// <summary>
        /// Convenience method for Trace level logging.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Trace(object message, GridClient client = null)
        {
            Log(message, LogLevel.Trace, client, null);
        }

        /// <summary>
        /// Convenience method for Trace level logging with an exception.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Trace(object message, Exception exception, GridClient client = null)
        {
            Log(message, LogLevel.Trace, client, exception);
        }

        /// <summary>
        /// Convenience method for Trace level logging using an explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Trace(object message, string clientName)
        {
            LogWithLevel(LogLevel.Trace, message, null, clientName);
        }

        /// <summary>
        /// Convenience method for Trace level logging with an exception and explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Trace(object message, Exception exception, string clientName)
        {
            LogWithLevel(LogLevel.Trace, message, exception, clientName);
        }

        /// <summary>
        /// Convenience method for Debug level logging.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Debug(object message, GridClient client = null)
        {
            Log(message, LogLevel.Debug, client, null);
        }

        /// <summary>
        /// Convenience method for Debug level logging with an exception.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Debug(object message, Exception exception, GridClient client = null)
        {
            Log(message, LogLevel.Debug, client, exception);
        }

        /// <summary>
        /// Convenience method for Debug level logging using an explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Debug(object message, string clientName)
        {
            LogWithLevel(LogLevel.Debug, message, null, clientName);
        }

        /// <summary>
        /// Convenience method for Debug level logging with an exception and explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Debug(object message, Exception exception, string clientName)
        {
            LogWithLevel(LogLevel.Debug, message, exception, clientName);
        }

        /// <summary>
        /// Convenience method for Information (Info) level logging.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Info(object message, GridClient client = null)
        {
            Log(message, LogLevel.Information, client, null);
        }

        /// <summary>
        /// Convenience method for Information (Info) level logging with an exception.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Info(object message, Exception exception, GridClient client = null)
        {
            Log(message, LogLevel.Information, client, exception);
        }

        /// <summary>
        /// Convenience method for Information (Info) level logging using an explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Info(object message, string clientName)
        {
            LogWithLevel(LogLevel.Information, message, null, clientName);
        }

        /// <summary>
        /// Convenience method for Information (Info) level logging with an exception and explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Info(object message, Exception exception, string clientName)
        {
            LogWithLevel(LogLevel.Information, message, exception, clientName);
        }

        /// <summary>
        /// Convenience method for Warning (Warn) level logging.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Warn(object message, GridClient client = null)
        {
            Log(message, LogLevel.Warning, client, null);
        }

        /// <summary>
        /// Convenience method for Warning (Warn) level logging with an exception.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Warn(object message, Exception exception, GridClient client = null)
        {
            Log(message, LogLevel.Warning, client, exception);
        }

        /// <summary>
        /// Convenience method for Warning (Warn) level logging using an explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Warn(object message, string clientName)
        {
            LogWithLevel(LogLevel.Warning, message, null, clientName);
        }

        /// <summary>
        /// Convenience method for Warning (Warn) level logging with an exception and explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Warn(object message, Exception exception, string clientName)
        {
            LogWithLevel(LogLevel.Warning, message, exception, clientName);
        }

        /// <summary>
        /// Convenience method for Error level logging.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Error(object message, GridClient client = null)
        {
            Log(message, LogLevel.Error, client, null);
        }

        /// <summary>
        /// Convenience method for Error level logging with an exception.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Error(object message, Exception exception, GridClient client = null)
        {
            Log(message, LogLevel.Error, client, exception);
        }

        /// <summary>
        /// Convenience method for Error level logging using an explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Error(object message, string clientName)
        {
            LogWithLevel(LogLevel.Error, message, null, clientName);
        }

        /// <summary>
        /// Convenience method for Error level logging with an exception and explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Error(object message, Exception exception, string clientName)
        {
            LogWithLevel(LogLevel.Error, message, exception, clientName);
        }

        /// <summary>
        /// Convenience method for Critical level logging.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Critical(object message, GridClient client = null)
        {
            Log(message, LogLevel.Critical, client, null);
        }

        /// <summary>
        /// Convenience method for Critical level logging with an exception.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="client">Optional GridClient whose name may be included in the log.</param>
        public static void Critical(object message, Exception exception, GridClient client = null)
        {
            Log(message, LogLevel.Critical, client, exception);
        }

        /// <summary>
        /// Convenience method for Critical level logging using an explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Critical(object message, string clientName)
        {
            LogWithLevel(LogLevel.Critical, message, null, clientName);
        }

        /// <summary>
        /// Convenience method for Critical level logging with an exception and explicit client name.
        /// </summary>
        /// <param name="message">Message object to log.</param>
        /// <param name="exception">Exception to include with the log entry.</param>
        /// <param name="clientName">Client name to include in the log scope.</param>
        public static void Critical(object message, Exception exception, string clientName)
        {
            LogWithLevel(LogLevel.Critical, message, exception, clientName);
        }

        /// <summary>
        /// Returns whether the configured logger is enabled for the specified level.
        /// Safe to call even if logging has not been initialized.
        /// </summary>
        public static bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level)
        {
            try
            {
                EnsureInitialized();
                return _logger != null && _logger.IsEnabled(level);
            }
            catch
            {
                return false;
            }
        }
    }
}
