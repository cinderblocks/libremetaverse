/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn LLC.
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
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.Http
{
    /// <summary>EventQueueClient manages the polling-based EventQueueGet capability</summary>
    public class EventQueueClient : IDisposable
    {
        private const string PROXY_TIMEOUT_RESPONSE = "502 Proxy Error";
        private const string MALFORMED_EMPTY_RESPONSE = "<llsd><undef /></llsd>";

        // Exponential backoff bounds for transient HTTP/network errors.
        private const int InitialEqRetryDelayMs = 1_000;
        private const int MaxEqRetryDelayMs = 30_000;

        // Milliseconds to wait before the next request; written by RequestCompletedHandler,
        // read and reset by the polling loop.  Accessed only from the single EQ task so no
        // Interlocked is needed, but volatile prevents stale reads across the await boundary.
        private volatile int _pendingRetryDelayMs;

        public delegate void ConnectedCallback();
        public delegate void EventCallback(string eventName, OSDMap body);

        public ConnectedCallback? OnConnected;
        public EventCallback? OnEvent;

        public bool Running => _queueCts != null && !_queueCts.IsCancellationRequested
                               && _eqTask != null && !_eqTask.IsCompleted;

        protected readonly Uri Address;
        protected readonly Simulator Simulator;
        private CancellationTokenSource? _queueCts;
        private Task? _eqTask;

        private readonly object _payloadLock = new object();
        private OSDMap? _reqPayloadMap;
        private byte[]? _reqPayloadBytes;

        public EventQueueClient(Uri eventQueueLocation, Simulator sim)
        {
            Address = eventQueueLocation;
            Simulator = sim;
            _queueCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Dispose resources deterministically
        /// </summary>
        public void Dispose()
        {
            try
            {
                Stop(true);
            }
            catch { /* noop */ }

            // Ensure task is observed/cleaned up
            try
            {
                if (_eqTask != null)
                {
                    if (_eqTask.IsFaulted && _eqTask.Exception != null)
                    {
                        Logger.Error($"EventQueueClient background task faulted during dispose: {_eqTask.Exception}");
                    }
                }
            }
            catch { /* noop */ }

            // Atomically take ownership and dispose
            var oldCts = Interlocked.Exchange(ref _queueCts, null);
            DisposalHelper.SafeCancelAndDispose(oldCts);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts event queue polling if it isn't already running.
        /// </summary>
        public void Start()
        {
            if (!Running)
            {
                Create();
            }
        }

        /// <summary>
        /// Returns the next backoff delay using binary exponential back-off with a small
        /// jitter derived from Environment.TickCount so multiple clients don't thunderbird
        /// at the same moment.
        /// </summary>
        private static int NextRetryDelay(int currentMs)
        {
            int next = currentMs == 0 ? InitialEqRetryDelayMs
                                      : Math.Min(MaxEqRetryDelayMs, currentMs * 2);
            // Add 0–12.5% jitter using TickCount as a cheap pseudo-random source.
            next += Math.Abs(Environment.TickCount) % (next / 8 + 1);
            return next;
        }

        private void Create()
        {
            // Create an EventQueueGet request
            var eqAck = new EventQueueAck { Done = false };
            var initial = eqAck.Serialize() as OSDMap ?? new OSDMap { ["done"] = OSD.FromBoolean(false) };

            lock (_payloadLock)
            {
                _reqPayloadMap = initial;
                _reqPayloadBytes = OSDParser.SerializeLLSDXmlBytes(_reqPayloadMap);
            }

            // Atomically replace previous CTS and dispose it
            var newCts = new CancellationTokenSource();
            var prev = Interlocked.Exchange(ref _queueCts, newCts);
            DisposalHelper.SafeCancelAndDispose(prev);

            _pendingRetryDelayMs = 0;

            _eqTask = Task.Run(async () =>
            {
                try
                {
                    // First request is immediate.
                    await ack().ConfigureAwait(false);

                    while (!newCts.Token.IsCancellationRequested)
                    {
                        int delayMs = _pendingRetryDelayMs;
                        _pendingRetryDelayMs = 0;

                        if (delayMs > 0)
                            await Task.Delay(delayMs, newCts.Token).ConfigureAwait(false);

                        if (newCts.Token.IsCancellationRequested) break;

                        await ack().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
            }, newCts.Token);

            async Task ack()
            {
                try
                {
                    byte[] payloadSnapshot;
                    lock (_payloadLock)
                    {
                        payloadSnapshot = _reqPayloadBytes;
                    }

                    // Fallback if for some reason payload is null
                    if (payloadSnapshot == null)
                    {
                        // serialize under lock
                        lock (_payloadLock)
                        {
                            _reqPayloadBytes = OSDParser.SerializeLLSDXmlBytes(_reqPayloadMap ?? new OSDMap());
                            payloadSnapshot = _reqPayloadBytes;
                        }
                    }

                    try
                    {
                        var (response, data) = await Simulator.Client.HttpCapsClient.PostAsync(
                            Address, OSDFormat.Xml, payloadSnapshot, newCts.Token).ConfigureAwait(false);
                        ConnectedResponseHandler(response);
                        RequestCompletedHandler(response, data, null);
                    }
                    catch (Exception innerEx) when (!(innerEx is OperationCanceledException))
                    {
                        RequestCompletedHandler(null, null, innerEx);
                    }
                }
                catch (OperationCanceledException)
                {
                    // noop, cancellation is expected when stopping
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception sending EventQueue POST to {Simulator}: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Stop the event queue
        /// </summary>
        /// <param name="immediate">quite honestly does nothing.</param>
        public void Stop(bool immediate)
        {
            // Atomically take ownership and cancel/dispose the CTS
            var old = Interlocked.Exchange(ref _queueCts, null);
            DisposalHelper.SafeCancelAndDispose(old);

            // Wait a short time for the background task to finish so resources are cleaned up
            try
            {
                if (_eqTask != null)
                {
                    // Wait up to 2 seconds for the repeating task to stop and observe exceptions
                    DisposalHelper.SafeWaitTask(_eqTask, TimeSpan.FromSeconds(2), (m, ex) =>
                    {
                        if (ex == null)
                        {
                            Logger.Debug($"{m} for {Simulator}");
                        }
                        else
                        {
                            Logger.Error(m, ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception while waiting for EventQueueClient task to stop: {ex.Message}", ex);
            }
        }

        private void ConnectedResponseHandler(HttpResponseMessage? response)
        {
            if (response?.IsSuccessStatusCode != true) { return; }

            // The event queue is starting up for the first time
            if (OnConnected == null) { return; }
            try
            {
                OnConnected();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Determine whether an HTTP response payload is likely LLSD/XML and therefore safe to attempt LLSD parsing.
        /// Checks the Content-Type header first (if present) and otherwise peeks up to the first 256 bytes
        /// of the payload to detect HTML/DOCTYPE bodies or LLSD/XML markers. Centralized to keep parsing
        /// decision logic in one place and to provide consistent logging behaviour.
        /// </summary>
        /// <param name="response">The HTTP response message (might be null).</param>
        /// <param name="data">The response body bytes.</param>
        /// <returns>True if the payload should be treated as LLSD/XML and can be parsed; false otherwise.</returns>
        private static bool IsLikelyLLSD(HttpResponseMessage? response, byte[]? data)
        {
            if (data == null || data.Length == 0) return false;

            // Prefer a canonical content-type check when available
            string? mediaType = null;
            try { mediaType = response?.Content?.Headers?.ContentType?.MediaType; } catch { mediaType = null; }
            if (!string.IsNullOrEmpty(mediaType))
            {
                var mt = mediaType!.ToLowerInvariant();
                if (mt.Contains("xml") || mt.Contains("llsd"))
                    return true;
                // Content type explicitly present and not XML-like -> avoid parsing as LLSD
                return false;
            }

            // No content-type header: peek at the start of the body
            string prefix;
            try { prefix = System.Text.Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 256)).TrimStart(); } catch { return false; }

            // Common non-LLSD payloads we want to reject quickly
            if (prefix.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase))
                return false;

            // Common LLSD/XML markers
            if (prefix.StartsWith("<? LLSD/", StringComparison.Ordinal) ||
                prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("<llsd", StringComparison.OrdinalIgnoreCase))
                return true;

            // If it starts with a '<' and none of the HTML doctype matches above, treat as XML-like
            if (prefix.StartsWith("<"))
                return true;

            return false;
        }

        private void RequestCompletedHandler(HttpResponseMessage? response, byte[]? responseData, Exception? error)
        {
            // Ignore anything if we're no longer connected to the sim.
            if (!Simulator.Connected) { return; }

            try
            {
                OSDArray? events = null;
                OSD ack = new OSD();

                #region Error handling
                if (error != null)
                {
                    if (response == null) // This happens during a timeout (i.e. normal eventqueue operation.)
                    {
                        if (error is HttpRequestException exception)
                        {
                            bool isNormalTimeout;
#if NET5_0_OR_GREATER
                            isNormalTimeout = exception.HttpRequestError == HttpRequestError.ResponseEnded;
#else
                            // On older runtimes, HttpRequestException.Message is always the generic
                            // "An error occurred while sending the request." wrapper regardless of cause;
                            // the real reason lives in InnerException. The normal way a long-poll ends
                            // when idle is the server closing the held-open connection, which surfaces
                            // as a WebException with Status ConnectionClosed or KeepAliveFailure.
                            isNormalTimeout = exception.InnerException is WebException webEx &&
                                (webEx.Status == WebExceptionStatus.ConnectionClosed ||
                                 webEx.Status == WebExceptionStatus.KeepAliveFailure);
#endif
                            if (!isNormalTimeout)
                            {
                                Logger.Error($"Unable to parse response from {Simulator} event queue: " +
                                           error.Message);
                                _pendingRetryDelayMs = NextRetryDelay(_pendingRetryDelayMs);
                            }
                        }
                        else
                        {
                            Logger.Error($"Unable to parse response from {Simulator} event queue: " +
                                       error.Message);
                            _pendingRetryDelayMs = NextRetryDelay(_pendingRetryDelayMs);
                        }

                        return;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.NotFound:
                        case HttpStatusCode.Gone:
                            Logger.Info($"Closing event queue at {Simulator} due to missing caps URI");

                            // Cancel safely on snapshot
                            var ctsSnapshot1 = Volatile.Read(ref _queueCts);
                            if (ctsSnapshot1 != null)
                            {
                                try { ctsSnapshot1.Cancel(); } catch (ObjectDisposedException) { }
                            }
                            break;
                        case (HttpStatusCode)499: // weird error returned occasionally, ignore for now
                            Logger.Debug($"Possible HTTP-out timeout error from {Simulator}, no need to continue");

                            var ctsSnapshot2 = Volatile.Read(ref _queueCts);
                            if (ctsSnapshot2 != null)
                            {
                                try { ctsSnapshot2.Cancel(); } catch (ObjectDisposedException) { }
                            }
                            break;

                        case HttpStatusCode.InternalServerError:
                        {
                            // If responseData already buffered, log it directly
                            if (responseData != null)
                            {
                                try
                                {
                                    var responseString = System.Text.Encoding.UTF8.GetString(responseData);
                                    if (!string.IsNullOrEmpty(responseString) &&
                                        responseString.IndexOf(PROXY_TIMEOUT_RESPONSE, StringComparison.Ordinal) < 0)
                                    {
                                        Logger.Debug($"Full response was: {responseString}", Simulator.Client);
                                    }
                                }
                                catch { /* ignore decode failures */ }
                            }

                            if (error.InnerException != null)
                            {
                                if (error.InnerException.Message.IndexOf(PROXY_TIMEOUT_RESPONSE, StringComparison.Ordinal) < 0)
                                {
                                    var ctsSnapshot3 = Volatile.Read(ref _queueCts);
                                    if (ctsSnapshot3 != null)
                                    {
                                        try { ctsSnapshot3.Cancel(); } catch (ObjectDisposedException) { }
                                    }
                                }
                                else
                                {
                                    // Proxy timeout wrapped in a 500 — server is stressed, back off.
                                    _pendingRetryDelayMs = NextRetryDelay(_pendingRetryDelayMs);
                                }
                            }
                            else
                            {
                                const bool WILLFULLY_IGNORE_LL_SPECS_ON_EVENT_QUEUE = true;

                                if (!WILLFULLY_IGNORE_LL_SPECS_ON_EVENT_QUEUE || !Simulator.Connected)
                                {
                                    var ctsSnapshot4 = Volatile.Read(ref _queueCts);
                                    if (ctsSnapshot4 != null)
                                    {
                                        try { ctsSnapshot4.Cancel(); } catch (ObjectDisposedException) { }
                                    }
                                }
                                else
                                {
                                    // Ignoring spec's "stop on 500" — back off before retrying.
                                    _pendingRetryDelayMs = NextRetryDelay(_pendingRetryDelayMs);
                                }
                            }
                        }
                            break;

                        case HttpStatusCode.BadGateway:
                            // This is not good (server) protocol design, but it's normal.
                            // The EventQueue server is a proxy that connects to a Squid
                            // cache which will time out periodically. The EventQueue server
                            // interprets this as a generic error and returns a 502 to us
                            // that we ignore
                            //
                            // Note: if this condition persists, it _might_ be the grid trying to request
                            // that the client closes the connection, as per LL's specs (gwyneth 20220414)
                            Logger.Debug($"Grid sent a Bad Gateway Error at {Simulator}; " +
                                       $"probably a time-out from the grid's EventQueue server (normal) -- ignoring and continuing");
                            break;
                        default:
                            // Try to log a meaningful error message
                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                Logger.Warn($"Unrecognized caps connection problem from {Simulator}: {response.StatusCode} {response.ReasonPhrase}");
                                _pendingRetryDelayMs = NextRetryDelay(_pendingRetryDelayMs);
                            }
                            else if (error.InnerException != null)
                            {
                                // see comment above (gwyneth 20220414)
                                Logger.Warn($"Unrecognized internal caps exception from {Simulator}: '{error.InnerException.Message}'");
                                Logger.Warn($"Message ---\n{error.Message}");
                                if (error.Data.Count > 0)
                                {
                                    Logger.Warn("  Extra details:");
                                    foreach (DictionaryEntry de in error.Data)
                                    {
                                        Logger.Warn($"    Key: {"'" + de.Key + "'",-20}      Value: {de.Value}");
                                    }
                                }
                            }
                            else
                            {
                                Logger.Warn($"Unrecognized caps exception from {Simulator}: {error.Message}");
                            }

                            break;
                    } // end switch
                }
                #endregion Error handling
                else if (responseData != null)
                {
                    // Got a proper HTTP response — clear any pending backoff regardless of whether
                    // the body parses cleanly.  The server is reachable and answering.
                    _pendingRetryDelayMs = 0;

                    // Got a response. Validate that the payload is likely LLSD/XML before attempting to parse.
                    if (!IsLikelyLLSD(response, responseData))
                    {
                        var responseString = System.Text.Encoding.UTF8.GetString(responseData);
                        if (responseString.IndexOf(PROXY_TIMEOUT_RESPONSE, StringComparison.Ordinal) < 0
                            && responseString.IndexOf(MALFORMED_EMPTY_RESPONSE, StringComparison.Ordinal) < 0)
                        {
                            var preview = responseString.Length > 200 ? responseString.Substring(0, 200) : responseString;
                            Logger.Warn($"Skipping LLSD parsing; server returned non-LLSD response from {Simulator}: \"{preview}\"");
                        }
                    }
                    else
                    {
                        if (OSDParser.DeserializeLLSDXml(responseData) is OSDMap result)
                        {
                            events = result["events"] as OSDArray;
                            ack = result["id"];
                        }
                        else
                        {
                            var responseString = System.Text.Encoding.UTF8.GetString(responseData);

                            // We might get a ghost Gateway 502 in the message body, or we may get a 
                            // badly-formed Undefined LLSD response. It's just par for the course for
                            // EventQueueGet and we take it in stride
                            if (responseString.IndexOf(PROXY_TIMEOUT_RESPONSE, StringComparison.Ordinal) < 0
                                && responseString.IndexOf(MALFORMED_EMPTY_RESPONSE, StringComparison.Ordinal) < 0)
                            {
                                Logger.Warn($"Could not parse response (1) from {Simulator} event queue: \"" +
                                           responseString + "\"");
                            }
                        }
                    }
                }

                #region Prepare the next ping

                lock (_payloadLock)
                {
                    if (_reqPayloadMap == null) _reqPayloadMap = new OSDMap();
                    _reqPayloadMap["ack"] = ack;

                    var ctsLocal = Volatile.Read(ref _queueCts);
                    if (ctsLocal == null || ctsLocal.IsCancellationRequested)
                    {
                        // We will fire off one more POST to tell the simulator, that's it we're done.
                        // Not sure if this even necessary. Only our dark lords know what 'done' does.
                        _reqPayloadMap["done"] = OSD.FromBoolean(true);
                    }
                    else
                    {
                        _reqPayloadMap["done"] = OSD.FromBoolean(!Simulator.Connected);
                    }

                    // reserialize for next request
                    try
                    {
                        _reqPayloadBytes = OSDParser.SerializeLLSDXmlBytes(_reqPayloadMap);
                    }
                    catch
                    {
                        // If serialization fails for any reason, clear bytes so caller will fallback
                        _reqPayloadBytes = null;
                    }
                }

                #endregion Prepare the next ping

                #region Handle incoming events

                if (OnEvent == null || events == null || events.Count <= 0) { return; }
                // Fire callbacks for each event received
                foreach (var osd in events)
                {
                    var evt = (OSDMap)osd;
                    var msg = evt["message"].AsString();
                    var body = (OSDMap)evt["body"];

                    try
                    {
                        // Run handlers on the thread pool to avoid blocking the event loop
                        Task.Run(() => OnEvent(msg, body));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.Message, ex);
                    }
                }

                #endregion Handle incoming events
            }

            catch (Exception e)
            {
                Logger.Warn($"Exception in EventQueueGet handler; {e.Message}", e);
            }
        }
    }
}

