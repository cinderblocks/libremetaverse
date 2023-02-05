/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2023, Sjofn LLC.
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
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Http
{
    public class EventQueueClient
    {
        /// <summary>For exponential backoff on error.</summary>
        private const int REQUEST_BACKOFF_SECONDS = 15 * 1000; // 15 seconds start
        private const int REQUEST_BACKOFF_SECONDS_INC = 5 * 1000; // 5 seconds increase
        private const int REQUEST_BACKOFF_SECONDS_MAX = 5 * 60 * 1000; // 5 minutes

        public delegate void ConnectedCallback();
        public delegate void EventCallback(string eventName, OSDMap body);

        public ConnectedCallback OnConnected;
        public EventCallback OnEvent;

        public bool Running { get; private set; }

        private readonly Uri _Address;
        private readonly Simulator _Simulator;
        private readonly CancellationTokenSource _queueCts;
        private bool _Dead;
        private OSD _AckPayload;


        /// <summary>Number of times we've received an unknown CAPS exception in series.</summary>
        private int _errorCount;

        public EventQueueClient(Uri eventQueueLocation, Simulator sim)
        {
            _Address = eventQueueLocation;
            _Simulator = sim;
            _queueCts = new CancellationTokenSource();
        }

        public void Start()
        {
            _Dead = false;
            Running = true;

            Task.Factory.StartNew(() =>
            {
                // Initial request
                _AckPayload = new OSDMap { ["ack"] = new OSD(), ["done"] = OSD.FromBoolean(false) };
                var initialTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_queueCts.Token);
                initialTimeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                Task initialReq = _Simulator.Client.HttpCapsClient.PostRequestAsync(_Address, OSDFormat.Xml, _AckPayload,
                    initialTimeoutCts.Token, RequestCompletedHandler, null, ConnectedResponseHandler);

                initialReq.Wait(initialTimeoutCts.Token);
                initialTimeoutCts.Dispose();

                while (Running && !_queueCts.Token.IsCancellationRequested)
                {
                    if (_errorCount > 0)
                    { // Exponentially back off, so we don't hammer the CPU
                        while (!_queueCts.Token.IsCancellationRequested)
                        {
                            _queueCts.Token.WaitHandle.WaitOne(
                                Math.Min(REQUEST_BACKOFF_SECONDS + _errorCount * REQUEST_BACKOFF_SECONDS_INC,
                                    REQUEST_BACKOFF_SECONDS_MAX));
                        }
                    }
                    // Resume the connection.
                    if (!_queueCts.Token.IsCancellationRequested)
                    {
                        using (CancellationTokenSource timeoutCts =
                               CancellationTokenSource.CreateLinkedTokenSource(_queueCts.Token))
                        {
                            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                            try
                            {
                                Task req = _Simulator.Client.HttpCapsClient.PostRequestAsync(_Address, OSDFormat.Xml,
                                    _AckPayload, timeoutCts.Token, RequestCompletedHandler);
                                req.Wait(timeoutCts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                /*no-op*/
                            }
                        }
                    }


                    // If the event queue is dead at this point, turn it off since
                    // that was the last thing we want to do
                    if (_Dead)
                    {
                        Running = false;
                        Logger.DebugLog($"Sent event queue shutdown message for {_Simulator}");
                    }
                }
                
            }, _queueCts.Token); // TOOD: TaskCreationOptions.LongRunning?

        }

        public void Stop(bool immediate)
        {
            _Dead = true;

            if (immediate)
            {
                Running = false;
            }

            _queueCts.Cancel();
        }

        void ConnectedResponseHandler(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode) { return; }

            Running = true;

            // The event queue is starting up for the first time
            if (OnConnected != null)
            {
                try { OnConnected(); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, ex); }
            }
        }

        void RequestCompletedHandler(HttpResponseMessage response, byte[] responseData, Exception error)
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
                    Logger.Log($"Received an invalid response from {_Simulator} event queue: \"" +
                        System.Text.Encoding.UTF8.GetString(responseData) + "\"", Helpers.LogLevel.Warning);
                }
            }
            else if (response != null && error != null)
            {
                #region Error handling
                
                switch (response.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                    case HttpStatusCode.Gone:
                        Logger.Log($"Closing event queue at {_Simulator} due to missing caps URI", Helpers.LogLevel.Info, _Simulator.Client);

                        Running = false;
                        _Dead = true;
                        break;
                    case (HttpStatusCode)499: // weird error returned occasionally, ignore for now
						// I believe this is the timeout error invented by LL for LSL HTTP-out requests (gwyneth 20220413)
						Logger.Log($"Possible HTTP-out timeout error from {_Simulator}, no need to continue", Helpers.LogLevel.Debug, _Simulator.Client);

						Running = false;
						_Dead = true;
						break;
					case HttpStatusCode.InternalServerError:
						// As per LL's instructions, we ought to consider this a
						// 'request to close client' (gwyneth 20220413)
						Logger.Log($"Grid sent a {response.StatusCode} at {_Simulator}, closing connection", Helpers.LogLevel.Debug, _Simulator.Client);

						// ... but do we happen to have an InnerException? Log it!
						if (error.InnerException != null)
						{
							// unravel the whole inner error message, so we finally figure out what it is!
							// (gwyneth 20220414)
							Logger.Log($"Unrecognized internal caps exception from {_Simulator}: '{error.InnerException.Message}'",	
                                Helpers.LogLevel.Warning, _Simulator.Client);
							Logger.Log($"\nMessage ---\n{error.Message}", Helpers.LogLevel.Warning, _Simulator.Client);
							if (error.Data.Count > 0)
							{
								Logger.Log("  Extra details:", Helpers.LogLevel.Warning);
								foreach (DictionaryEntry de in error.Data)
                                {
                                    Logger.Log(String.Format("    Key: {0,-20}      Value: '{1}'",
										de.Key, de.Value),
										Helpers.LogLevel.Warning, _Simulator.Client);
                                }
                            }
							// but we'll nevertheless close this connection (gwyneth 20220414)
						}

						Running = false;
						_Dead = true;
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
                            Helpers.LogLevel.Debug, _Simulator.Client);
                        break;
                    default:
                        ++_errorCount;

                        // Try to log a meaningful error message
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            Logger.Log($"Unrecognized caps connection problem from {_Simulator}: {response.StatusCode} {response.ReasonPhrase}",
                                Helpers.LogLevel.Warning, _Simulator.Client);
                        }
                        else if (error.InnerException != null)
                        {
							// see comment above (gwyneth 20220414)
							Logger.Log($"Unrecognized internal caps exception from {_Simulator}: '{error.InnerException.Message}'", 
                                Helpers.LogLevel.Warning, _Simulator.Client);
							Logger.Log($"Message ---\n{error.Message}", Helpers.LogLevel.Warning, _Simulator.Client);
							if (error.Data.Count > 0)
							{
								Logger.Log("  Extra details:", Helpers.LogLevel.Warning, _Simulator.Client);
								foreach (DictionaryEntry de in error.Data)
                                {
                                    Logger.Log(string.Format("    Key: {0,-20}      Value: {1}",
										"'" + de.Key + "'", de.Value),
										Helpers.LogLevel.Warning, _Simulator.Client);
                                }
                            }
                        }
                        else
                        {
                            Logger.Log($"Unrecognized caps exception from {_Simulator}: {error.Message}",
                                Helpers.LogLevel.Warning, _Simulator.Client);
                        }
                        break;
                }	// end switch

                #endregion Error handling
            }
            else if (error != null)
            {
                ++_errorCount;
                Logger.Log($"Exception thrown from {_Simulator} event queue: " +
                           $"{(error.InnerException != null ? error.InnerException.Message : error.Message)}", Helpers.LogLevel.Warning, _Simulator.Client);
            }
            else
            {
                ++_errorCount;
                Logger.Log($"No response from {_Simulator} event queue with no reported error", Helpers.LogLevel.Warning, _Simulator.Client);
            }
            
            if (Running)
            {
                OSDMap payload = new OSDMap();
                if (ack != 0) { payload["ack"] = OSD.FromInteger(ack); }
                else { payload["ack"] = new OSD(); }
                payload["done"] = OSD.FromBoolean(_Dead);
                _AckPayload = payload;
            }

            #region Handle incoming events

            if (OnEvent == null || events == null || events.Count <= 0) return;
            // Fire callbacks for each event received
            foreach (var osd in events)
            {
                var evt = (OSDMap) osd;
                string msg = evt["message"].AsString();
                OSDMap body = (OSDMap)evt["body"];

                try { OnEvent(msg, body); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, ex); }
            }

            #endregion Handle incoming events
        }
    }
}
