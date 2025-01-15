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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Http
{
    /// <summary>EventQueueClient manages the polling-based EventQueueGet capability</summary>
    public class EventQueueClient
    {
        private const string PROXY_TIMEOUT_RESPONSE = "502 Proxy Error";
        private const string MALFORMED_EMPTY_RESPONSE = "<llsd><undef /></llsd>";

        public delegate void ConnectedCallback();
        public delegate void EventCallback(string eventName, OSDMap body);

        public ConnectedCallback OnConnected;
        public EventCallback OnEvent;

        public bool Running => _eqTask != null 
                               && !_eqTask.IsCompleted 
                               && (_eqTask.Status.Equals(TaskStatus.Running) 
                                   || _eqTask.Status.Equals(TaskStatus.WaitingToRun)
                                   || _eqTask.Status.Equals(TaskStatus.WaitingForActivation));

        protected readonly Uri Address;
        protected readonly Simulator Simulator;
        private CancellationTokenSource _queueCts;
        private Task _eqTask;
        private OSD _reqPayload;

        public EventQueueClient(Uri eventQueueLocation, Simulator sim)
        {
            Address = eventQueueLocation;
            Simulator = sim;
            _queueCts = new CancellationTokenSource();
        }

        ~EventQueueClient()
        {
            _queueCts?.Dispose();
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
            _reqPayload = new OSDMap { ["ack"] = new OSD(), ["done"] = OSD.FromBoolean(false) };

            _queueCts = new CancellationTokenSource();
            _eqTask = Repeat.Interval(TimeSpan.FromSeconds(30), async () =>
            {
                await Simulator.Client.HttpCapsClient.PostRequestAsync(Address, OSDFormat.Xml, _reqPayload, _queueCts.Token,
                    RequestCompletedHandler, null, ConnectedResponseHandler);
            }, _queueCts.Token, true);
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
                    else switch (response.StatusCode)
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
#if NET5_0_OR_GREATER
                            var responseString = response.Content.ReadAsStringAsync(_queueCts.Token).Result;
#else
                            var responseString = response.Content.ReadAsStringAsync().Result;
#endif
                            if (!responseString.Contains(PROXY_TIMEOUT_RESPONSE))
                            {
                                Logger.Log($"Grid sent a {response.StatusCode} : {response.ReasonPhrase} at {Simulator}", Helpers.LogLevel.Debug, Simulator.Client);

                                if (!string.IsNullOrEmpty(responseString))
                                {
                                    Logger.Log($"Full response was: {responseString}", Helpers.LogLevel.Debug, Simulator.Client);
                                }

                                if (error.InnerException != null)
                                {
                                    if (!error.InnerException.Message.Contains(PROXY_TIMEOUT_RESPONSE))
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
                        if (!responseString.Contains(PROXY_TIMEOUT_RESPONSE)
                            && !responseString.Contains(MALFORMED_EMPTY_RESPONSE))
                        {
                            Logger.Log($"Could not parse response (1) from {Simulator} event queue: \"" +
                                       responseString + "\"", Helpers.LogLevel.Warning);
                        }
                    }
                }

                #region Prepare the next ping

                if (_queueCts.Token.IsCancellationRequested)
                {
                    // We will fire off one more POST to tell the simulator, that's it we're done.
                    // Not sure if this even necessary. Only our dark lords know what 'done' does.
                    _reqPayload = new OSDMap
                    {
                        ["ack"] = ack,
                        ["done"] = OSD.FromBoolean(true)
                    };
                }
                else
                {
                    _reqPayload = new OSDMap
                    {
                        ["ack"] = ack,
                        ["done"] = OSD.FromBoolean(!Simulator.Connected)
                    };
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
                        OnEvent(msg, body);
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
