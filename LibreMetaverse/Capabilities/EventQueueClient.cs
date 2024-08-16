/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022, Sjofn LLC.
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
        public static int REQUEST_BACKOFF_SECONDS = 5 * 1000; // 5 seconds start
        public static int REQUEST_BACKOFF_SECONDS_INC = 5 * 1000; // 5 seconds increase
        public static int REQUEST_BACKOFF_SECONDS_MAX = 1 * 30 * 1000; // 30 seconds

        public delegate void ConnectedCallback();

        public delegate void EventCallback(string eventName, OSDMap body);

        public ConnectedCallback OnConnected;
        public EventCallback OnEvent;

        public bool Running => _Running;

        protected readonly Uri _Address;
        protected readonly Simulator _Simulator;
        protected bool _Dead;
        protected bool _Broken;
        protected bool _Running;
        protected string _LastError = "Undefined";
        private CancellationTokenSource _httpCancellationTokenSource;

        /// <summary>Number of times we've received an unknown CAPS exception in series.</summary>
        private int _errorCount;

        private const bool DETAILED_LOGGING = false;

        public EventQueueClient(Uri eventQueueLocation, Simulator sim)
        {
            _Address = eventQueueLocation;
            _Simulator = sim;
        }

        ~EventQueueClient()
        {
            _httpCancellationTokenSource?.Dispose();
        }

        public void RestartIfDead()
        {
            if (_Dead)
            {
                Start();
            }
        }

        public void Start()
        {
            _Dead = false;
            _Running = true;

            // Create an EventQueueGet request
            OSDMap payload = new OSDMap { ["ack"] = new OSD(), ["done"] = OSD.FromBoolean(false) };

            _httpCancellationTokenSource = new CancellationTokenSource();
            Task req = _Simulator.Client.HttpCapsClient.PostRequestAsync(_Address, OSDFormat.Xml, payload,
                                                                         _httpCancellationTokenSource.Token,
                                                                         RequestCompletedHandler, null,
                                                                         ConnectedResponseHandler);
        }

        public void Stop(bool immediate)
        {
            _Dead = true;

            if (immediate)
            {
                _Running = false;
            }

            _httpCancellationTokenSource.Cancel();
        }

        void ConnectedResponseHandler(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            _Running = true;

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
            if (!_Simulator.Connected) // Ignore anything if we're no longer connected to the sim.
                return;

            try
            {
                OSDArray events = null;
                int ack = 0;

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
                            if (DETAILED_LOGGING)
                            {
                                Logger.Log($"Undefined response. Closing event queue connection. [{response.StatusCode}] (3)", Helpers.LogLevel.Debug);
                            }

                            _Running = false;
                            _Dead = true;
                            _LastError = responseString;
                        }
                        else
                        {
                            Logger.Log($"Got an unparseable response (1) from {_Simulator} event queue: \"" +
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
                            Logger.Log($"Got an unparseable response (2) from {_Simulator} event queue: \"{error}\"", Helpers.LogLevel.Error);
                        }
                    } 
                    else switch (response.StatusCode)
                    {
                        case HttpStatusCode.NotFound:
                        case HttpStatusCode.Gone:
                            Logger.Log($"Closing event queue at {_Simulator} due to missing caps URI",
                                       Helpers.LogLevel.Info);

                            _Running = false;
                            _Dead = true;
                            _LastError = "Missing Caps URI";
                            break;
                        case (HttpStatusCode)499: // weird error returned occasionally, ignore for now
                            // I believe this is the timeout error invented by LL for LSL HTTP-out requests (gwyneth 20220413)
                            Logger.Log($"Possible HTTP-out timeout error from {_Simulator}, no need to continue",
                                       Helpers.LogLevel.Debug);

                            _Running = false;
                            _Dead = true;
                            _LastError = "HTTP Timeout";
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
                                    catch (Exception e)
                                    {
                                        // Swallow
                                    }
                                }

                                if (!responseString.Contains("502 Proxy Error"))
                                {
                                    Logger.Log($"Grid sent a {response.StatusCode} : {response.ReasonPhrase} at {_Simulator}", Helpers.LogLevel.Debug, _Simulator.Client);

                                    if (!string.IsNullOrEmpty(responseString))
                                    {
                                        Logger.Log("Full response was: " + responseString, Helpers.LogLevel.Debug, _Simulator.Client);
                                    }

                                    if (error.InnerException != null)
                                    {
                                        if (!error.InnerException.Message.Contains("502 Proxy Error"))
                                        {
                                            if (DETAILED_LOGGING)
                                                    Logger.Log($"Closing event queue connection. (1)", Helpers.LogLevel.Debug);

                                            _Running = false;
                                            _Dead = true;
                                            _LastError = error.ToString();
                                        }
                                    }
                                    else
                                    {
                                        const bool willfullyIgnoreLLSpecsOnEventQueue = true;

                                        if (!willfullyIgnoreLLSpecsOnEventQueue || !_Simulator.Connected)
                                        {
                                            if (DETAILED_LOGGING)
                                                Logger.Log($"Closing event queue connection. (2)", Helpers.LogLevel.Debug);

                                            _Running = false;
                                            _Dead = true;
                                            _LastError = error.ToString();
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
                                Logger.Log($"Grid sent a {response.StatusCode} at {_Simulator}", Helpers.LogLevel.Debug);
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
                            Logger.Log($"Grid sent a Bad Gateway Error at {_Simulator}; " +
                                       $"probably a time-out from the grid's EventQueue server (normal) -- ignoring and continuing",
                                       Helpers.LogLevel.Debug);
                            break;
                        default:
                            ++_errorCount;

                            // Try to log a meaningful error message
                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                Logger.Log($"Unrecognized caps connection problem from {_Simulator}: {response.StatusCode} {response.ReasonPhrase}",
                                           Helpers.LogLevel.Warning);
                            }
                            else if (error.InnerException != null)
                            {
                                // see comment above (gwyneth 20220414)
                                Logger.Log($"Unrecognized internal caps exception from {_Simulator}: '{error.InnerException.Message}'",
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
                                Logger.Log($"Unrecognized caps exception from {_Simulator}: {error.Message}",
                                           Helpers.LogLevel.Warning);
                            }

                            break;
                    } // end switch

                    #endregion Error handling
                }
                else
                {
                    ++_errorCount;

                    Logger.Log($"No response from {_Simulator} event queue but no reported error either",
                               Helpers.LogLevel.Warning);
                }

#pragma warning disable CS0164 // This label has not been referenced
                HandlingDone:

                #region Resume the connection

                if (_Running)
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

                    bool simShutdown = _Simulator.Connected;

                    payload["done"] = OSD.FromBoolean(!simShutdown /*_Dead*/);

                    if (_errorCount > 0)
                    {
                        // Exponentially back off, so we don't hammer the CPU
                        Thread.Sleep(Math.Min(REQUEST_BACKOFF_SECONDS + _errorCount * REQUEST_BACKOFF_SECONDS_INC,
                                              REQUEST_BACKOFF_SECONDS_MAX));
                    }

                    // Resume the connection.
                    Task req = _Simulator.Client.HttpCapsClient.PostRequestAsync(_Address, OSDFormat.Xml, payload,
                                                                                 _httpCancellationTokenSource.Token,
                                                                                 RequestCompletedHandler);

                    // If the event queue is dead at this point, turn it off since
                    // that was the last thing we want to do
                    if (_Dead)
                    {
                        _Running = false;
                        Logger.DebugLog($"Sent event queue shutdown message for {_Simulator}");
                    }
                }
                else
                {
                    if (_Dead && _Simulator.Connected && false)
                    {
                        Thread.Sleep(5000);
                        if (_Dead && _Simulator.Connected)
                        {
                            RestartIfDead();
                        }
                    }
                }
#pragma warning restore CS0164 // This label has not been referenced

                if (_Simulator.Connected && _Dead && DETAILED_LOGGING)
                {
                    Logger.Log("Error: Event queue has died! Last error was " + _LastError.Trim() + " (" + _Simulator.Name + ")", Helpers.LogLevel.Error, _Simulator.Client);
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
