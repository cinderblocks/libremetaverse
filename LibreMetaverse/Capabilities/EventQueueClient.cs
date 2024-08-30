/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2024, Sjofn LLC.
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
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Http
{
    public class EventQueueClient
    {
        /// <summary>For exponential backoff on error.</summary>
        public static int RequestBackoffSeconds = 15 * 1000; // 15 seconds start
        public static int RequestBackoffSecondsInc = 5 * 1000; // 5 seconds increase
        public static int RequestBackoffSecondsMax = 5 * 60 * 1000; // 5 minutes

        public delegate void ConnectedCallback();
        public delegate void EventCallback(string eventName, OSDMap body);

        public ConnectedCallback OnConnected;
        public EventCallback OnEvent;

        public bool Running { get; private set; }

        protected readonly Uri Address;
        protected readonly Simulator Simulator;
        protected string LastError = "Undefined";
        private CancellationTokenSource _httpCts;
        protected bool Dead;

        /// <summary>Number of times we've received an unknown CAPS exception in series.</summary>
        private int _errorCount;

        public EventQueueClient(Uri eventQueueLocation, Simulator sim)
        {
            Address = eventQueueLocation;
            Simulator = sim;
            _httpCts = new CancellationTokenSource();
        }

        ~EventQueueClient()
        {
            _httpCts?.Dispose();
        }
        
        public void RestartIfDead()
        {
            if (Dead)
            {
                Start();
            }
        }

        public void Start()
        {
            Dead = false;
            Running = true;

            // Create an EventQueueGet request
            OSDMap payload = new OSDMap { ["ack"] = new OSD(), ["done"] = OSD.FromBoolean(false) };

            _httpCts = new CancellationTokenSource();
            Task req = Simulator.Client.HttpCapsClient.PostRequestAsync(Address, OSDFormat.Xml, payload,
                                                                         _httpCts.Token,
                                                                         RequestCompletedHandler, null,
                                                                         ConnectedResponseHandler);
        }

        public void Stop(bool immediate)
        {
            Dead = true;

            if (immediate)
            {
                Running = false;
            }

            _httpCts.Cancel();
        }

        void ConnectedResponseHandler(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            Running = true;

            // The event queue is starting up for the first time
            if (OnConnected != null)
            {
                try
                {
                    OnConnected();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, ex);
                }
            }
        }

        void RequestCompletedHandler(HttpResponseMessage response, byte[] responseData, Exception error)
        {
            // Ignore anything if we're no longer connected to the sim.
            if (!Simulator.Connected) { return; }

            try
            {
                OSDArray events = null;
                var ack = 0;

                if (responseData != null)
                {
                    _errorCount = 0;
                    // Got a response
                    if (OSDParser.DeserializeLLSDXml(responseData) is OSDMap result)
                    {
                        events = result["events"] as OSDArray;
                        ack = result["id"].AsInteger();
                    }
                    else
                    {
                        var responseString = System.Text.Encoding.UTF8.GetString(responseData);

                        if (responseString.Contains("502 Proxy Error"))
                        {
                            // LL's "go ask again" message.
                        } 
                        else if (responseString.Contains("<llsd><undef /></llsd>"))
                        {
                            Running = false;
                            Dead = true;
                            LastError = responseString;
                        }
                        else
                        {
                            Logger.Log($"Got an unparseable response (1) from {Simulator} event queue: \"" +
                                       responseString + "\"", Helpers.LogLevel.Warning);
                        }
                    }
                }
                else if (error != null)
                {
                    #region Error handling

                    if (response == null) // This happens during a timeout (i.e. normal eventqueue operation.)
                    {
                        if (error is IOException || error is HttpRequestException)
                        {
                            // Ignore.
                        }
                        else
                        {
                            Logger.Log($"Got an unparseable response (2) from {Simulator} event queue: \"{error}\"", Helpers.LogLevel.Error);
                        }
                    } 
                    else switch (response.StatusCode)
                    {
                        case HttpStatusCode.NotFound:
                        case HttpStatusCode.Gone:
                            Logger.Log($"Closing event queue at {Simulator} due to missing caps URI",
                                       Helpers.LogLevel.Info);

                            Running = false;
                            Dead = true;
                            LastError = "Missing Caps URI";
                            break;
                        case (HttpStatusCode)499: // weird error returned occasionally, ignore for now
                            // I believe this is the timeout error invented by LL for LSL HTTP-out requests (gwyneth 20220413)
                            Logger.Log($"Possible HTTP-out timeout error from {Simulator}, no need to continue",
                                       Helpers.LogLevel.Debug);

                            Running = false;
                            Dead = true;
                            LastError = "HTTP Timeout";
                            break;

                        case HttpStatusCode.InternalServerError:
                            if (error != null)
                            {
                                var responseString = (responseData == null) ? string.Empty : System.Text.Encoding.UTF8.GetString(responseData);
                                if (responseData == null || responseData.Length == 0)
                                {
                                    try
                                    {
                                        responseString = response.Content.ReadAsStringAsync().Result;
                                    }
                                    catch (Exception) { /* no-op */ }
                                }

                                if (!responseString.Contains("502 Proxy Error"))
                                {
                                    Logger.Log($"Grid sent a {response.StatusCode} : {response.ReasonPhrase} at {Simulator}", Helpers.LogLevel.Debug, Simulator.Client);

                                    if (!string.IsNullOrEmpty(responseString))
                                    {
                                        Logger.Log("Full response was: " + responseString, Helpers.LogLevel.Debug, Simulator.Client);
                                    }

                                    if (error.InnerException != null)
                                    {
                                        if (!error.InnerException.Message.Contains("502 Proxy Error"))
                                        {
                                            Running = false;
                                            Dead = true;
                                            LastError = error.ToString();
                                        }
                                    }
                                    else
                                    {
                                        const bool WILLFULLY_IGNORE_LL_SPECS_ON_EVENT_QUEUE = true;

                                        if (!WILLFULLY_IGNORE_LL_SPECS_ON_EVENT_QUEUE || !Simulator.Connected)
                                        {
                                            Running = false;
                                            Dead = true;
                                            LastError = error.ToString();
                                        }
                                    }
                                }
                                else
                                {
                                    // It's the typical re-queue for event queue get
                                }
                            }
                            else
                            {
                                Logger.Log($"Grid sent a {response.StatusCode} at {Simulator}", Helpers.LogLevel.Debug);
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
                            ++_errorCount;

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

                    #endregion Error handling
                }
                else
                {
                    ++_errorCount;

                    Logger.Log($"No response from {Simulator} event queue but no reported error either",
                               Helpers.LogLevel.Warning);
                }

#pragma warning disable CS0164 // This label has not been referenced
                HandlingDone:

                #region Resume the connection

                if (Running)
                {
                    OSDMap payload = new OSDMap();
                    if (ack != 0)
                    {
                        payload["ack"] = OSD.FromInteger(ack);
                    }
                    else
                    {
                        payload["ack"] = new OSD();
                    }

                    bool simShutdown = Simulator.Connected;

                    payload["done"] = OSD.FromBoolean(!simShutdown /*_Dead*/);

                    if (_errorCount > 0)
                    {
                        // Exponentially back off, so we don't hammer the CPU
                        Thread.Sleep(Math.Min(RequestBackoffSeconds + _errorCount * RequestBackoffSecondsInc,
                                              RequestBackoffSecondsMax));
                    }

                    // Resume the connection.
                    Task req = Simulator.Client.HttpCapsClient.PostRequestAsync(Address, OSDFormat.Xml, payload,
                                                                                 _httpCts.Token,
                                                                                 RequestCompletedHandler);

                    // If the event queue is dead at this point, turn it off since
                    // that was the last thing we want to do
                    if (Dead)
                    {
                        Running = false;
                        Logger.DebugLog($"Sent event queue shutdown message for {Simulator}");
                    }
                }
                else
                {
                    //if (Dead && Simulator.Connected && false)
                    //{
                    //    Thread.Sleep(5000);
                    //    if (Dead && Simulator.Connected)
                    //    {
                    //        RestartIfDead();
                    //    }
                    //}
                }

                #endregion Resume the connection

                #region Handle incoming events

                if (OnEvent == null || events == null || events.Count <= 0) return;
                // Fire callbacks for each event received
                foreach (var osd in events)
                {
                    var evt = (OSDMap)osd;
                    string msg = evt["message"].AsString();
                    OSDMap body = (OSDMap)evt["body"];

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
                Logger.Log($"Exception in EventQueueGet handler; {e.Message}", Helpers.LogLevel.Error, e);
            }
        }
    }
}
