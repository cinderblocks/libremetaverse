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
using LibreMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Http
{
    /// <summary>EventQueueClient manages the polling-based EventQueueGet capability</summary>
    public class EventQueueClient : IDisposable
    {
        private const string PROXY_TIMEOUT_RESPONSE = "502 Proxy Error";
        private const string MALFORMED_EMPTY_RESPONSE = "<llsd><undef /></llsd>";

        public delegate void ConnectedCallback();
        public delegate void EventCallback(string eventName, OSDMap body);

        public ConnectedCallback OnConnected;
        public EventCallback OnEvent;

        public bool Running => _queueCts != null && !_queueCts.IsCancellationRequested
                               && _eqTask != null && !_eqTask.IsCompleted;

        protected readonly Uri Address;
        protected readonly Simulator Simulator;
        private CancellationTokenSource _queueCts;
        private Task _eqTask;

        private readonly object _payloadLock = new object();
        private OSDMap _reqPayloadMap;
        private byte[] _reqPayloadBytes;

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

            _queueCts?.Dispose();
            _queueCts = null;
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
            _queueCts?.Cancel();
            _queueCts?.Dispose();
            _queueCts = new CancellationTokenSource();

            _eqTask = Repeat.IntervalAsync(TimeSpan.FromSeconds(1), ack, _queueCts.Token, true);

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

                    await Simulator.Client.HttpCapsClient.PostRequestAsync(
                        Address, OSDFormat.Xml, payloadSnapshot, _queueCts.Token, RequestCompletedHandler, null, ConnectedResponseHandler)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // noop, cancellation is expected when stopping
                }
                catch (Exception ex)
                {
                    Logger.Log($"Exception sending EventQueue POST to {Simulator}: {ex.Message}", Helpers.LogLevel.Error, ex);
                }
            }
        }

        /// <summary>
        /// Stop the event queue
        /// </summary>
        /// <param name="immediate">quite honestly does nothing.</param>
        public void Stop(bool immediate)
        {
            // do we need to POST one more request telling EQ we are done? i dunno!
            _queueCts.Cancel();
        }

        private void ConnectedResponseHandler(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode) { return; }

            // The event queue is starting up for the first time
            if (OnConnected == null) { return; }
            try
            {
                OnConnected();
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Error, ex);
            }
        }

        private void RequestCompletedHandler(HttpResponseMessage response, byte[] responseData, Exception error)
        {
            // Ignore anything if we're no longer connected to the sim.
            if (!Simulator.Connected) { return; }

            try
            {
                OSDArray events = null;
                OSD ack = new OSD();

                #region Error handling
                if (error != null)
                {
                    if (response == null) // This happens during a timeout (i.e. normal eventqueue operation.)
                    {
                        if (error is HttpRequestException exception)
                        {
#if NET5_0_OR_GREATER
                            if (exception.HttpRequestError != HttpRequestError.ResponseEnded)

#else
                            // ugly, but we can't get a status code
                            if (exception.Message.Equals("The response ended prematurely. (ResponseEnded)"))
#endif
                            {
                                Logger.Log($"Unable to parse response from {Simulator} event queue: " +
                                           error.Message, Helpers.LogLevel.Error);
                            }
                        }
                        else
                        {
                            Logger.Log($"Unable to parse response from {Simulator} event queue: " +
                                       error.Message, Helpers.LogLevel.Error);
                        }

                        return;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.NotFound:
                        case HttpStatusCode.Gone:
                            Logger.Log($"Closing event queue at {Simulator} due to missing caps URI",
                                Helpers.LogLevel.Info);

                            _queueCts.Cancel();
                            break;
                        case (HttpStatusCode)499: // weird error returned occasionally, ignore for now
                            Logger.Log($"Possible HTTP-out timeout error from {Simulator}, no need to continue",
                                Helpers.LogLevel.Debug);

                            _queueCts.Cancel();
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
                                        Logger.Log($"Full response was: {responseString}", Helpers.LogLevel.Debug, Simulator.Client);
                                    }
                                }
                                catch { /* ignore decode failures */ }
                            }

                            if (error.InnerException != null)
                            {
                                if (error.InnerException.Message.IndexOf(PROXY_TIMEOUT_RESPONSE, StringComparison.Ordinal) < 0)
                                {
                                    _queueCts.Cancel();
                                }
                            }
                            else
                            {
                                const bool WILLFULLY_IGNORE_LL_SPECS_ON_EVENT_QUEUE = true;

                                if (!WILLFULLY_IGNORE_LL_SPECS_ON_EVENT_QUEUE || !Simulator.Connected)
                                {
                                    _queueCts.Cancel();
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
                            Logger.Log($"Grid sent a Bad Gateway Error at {Simulator}; " +
                                       $"probably a time-out from the grid's EventQueue server (normal) -- ignoring and continuing",
                                Helpers.LogLevel.Debug);
                            break;
                        default:
                            // Try to log a meaningful error message
                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                Logger.Log($"Unrecognized caps connection problem from {Simulator}: {response.StatusCode} {response.ReasonPhrase}",
                                    Helpers.LogLevel.Warning);
                            }
                            else if (error.InnerException != null)
                            {
                                // see comment above (gwyneth 20220414)
                                Logger.Log($"Unrecognized internal caps exception from {Simulator}: '{error.InnerException.Message}'",
                                    Helpers.LogLevel.Warning);
                                Logger.Log($"Message ---\n{error.Message}", Helpers.LogLevel.Warning);
                                if (error.Data.Count > 0)
                                {
                                    Logger.Log("  Extra details:", Helpers.LogLevel.Warning);
                                    foreach (DictionaryEntry de in error.Data)
                                    {
                                        Logger.Log(string.Format("    Key: {0,-20}      Value: {1}",
                                                "'" + de.Key + "'", de.Value),
                                            Helpers.LogLevel.Warning);
                                    }
                                }
                            }
                            else
                            {
                                Logger.Log($"Unrecognized caps exception from {Simulator}: {error.Message}",
                                    Helpers.LogLevel.Warning);
                            }

                            break;
                    } // end switch
                }
                #endregion Error handling
                else if (responseData != null)
                {
                    // Got a response
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
                            Logger.Log($"Could not parse response (1) from {Simulator} event queue: \"" +
                                       responseString + "\"", Helpers.LogLevel.Warning);
                        }
                    }
                }

                #region Prepare the next ping

                lock (_payloadLock)
                {
                    if (_reqPayloadMap == null) _reqPayloadMap = new OSDMap();
                    _reqPayloadMap["ack"] = ack;

                    if (_queueCts.Token.IsCancellationRequested)
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
                        Logger.Log(ex.Message, Helpers.LogLevel.Error, ex);
                    }
                }

                #endregion Handle incoming events
            }

            catch (Exception e)
            {
                Logger.Log($"Exception in EventQueueGet handler; {e.Message}", Helpers.LogLevel.Warning, e);
            }
        }
    }
}
