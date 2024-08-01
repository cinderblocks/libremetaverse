/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenMetaverse.Packets;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;

namespace OpenMetaverse
{
    /// <summary>
    /// NetworkManager is responsible for managing the network layer of 
    /// OpenMetaverse. It tracks all the server connections, serializes 
    /// outgoing traffic and deserializes incoming traffic, and provides
    /// instances of delegates for network-related events.
    /// </summary>
    public partial class NetworkManager
    {
        // TODO: Implement throttle class for incoming and outgoing packets
        
        
        #region Enums

        /// <summary>
        /// Explains why a simulator or the grid disconnected from us
        /// </summary>
        public enum DisconnectType
        {
            /// <summary>The client requested the logout or simulator disconnect</summary>
            ClientInitiated,
            /// <summary>The server notified us that it is disconnecting</summary>
            ServerInitiated,
            /// <summary>Either a socket was closed or network traffic timed out</summary>
            NetworkTimeout,
            /// <summary>The last active simulator shut down</summary>
            SimShutdown
        }

        #endregion Enums

        #region Structs

        /// <summary>
        /// Holds a simulator reference and a decoded packet, these structs are put in
        /// the packet inbox for event handling
        /// </summary>
        public class IncomingPacket
        {
            /// <summary>Reference to the simulator that this packet came from</summary>
            public Simulator Simulator;

            /// <summary>Packet that needs to be processed</summary>
            public Packet Packet;
        }
        
        /// <summary>
        /// Holds a simulator reference and a serialized packet, these structs are put in
        /// the packet outbox for sending
        /// </summary>
        public class OutgoingPacket
        {
            /// <summary>Reference to the simulator this packet is destined for</summary>
            public readonly Simulator Simulator;
            /// <summary>Packet that needs to be sent</summary>
            public readonly UDPPacketBuffer Buffer;
            /// <summary>Sequence number of the wrapped packet</summary>
            public uint SequenceNumber;
            /// <summary>Number of times this packet has been resent</summary>
            public int ResendCount;
            /// <summary>Environment.TickCount when this packet was last sent over the wire</summary>
            public int TickCount;
            /// <summary>Type of the packet</summary>
            public PacketType Type;

            public OutgoingPacket(Simulator simulator, UDPPacketBuffer buffer, PacketType type)
            {
                Simulator = simulator;
                Buffer = buffer;
                SequenceNumber = 0;
                ResendCount = 0;
                TickCount = 0;
                Type = type;
            }
        }

        #endregion Structs

        #region Delegates

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<PacketSentEventArgs> m_PacketSent;

        ///<summary>Raises the PacketSent Event</summary>
        /// <param name="e">A PacketSentEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnPacketSent(PacketSentEventArgs e)
        {
            EventHandler<PacketSentEventArgs> handler = m_PacketSent;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_PacketSentLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<PacketSentEventArgs> PacketSent
        {
            add { lock (m_PacketSentLock) { m_PacketSent += value; } }
            remove { lock (m_PacketSentLock) { m_PacketSent -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<LoggedOutEventArgs> m_LoggedOut;

        ///<summary>Raises the LoggedOut Event</summary>
        /// <param name="e">A LoggedOutEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnLoggedOut(LoggedOutEventArgs e)
        {
            EventHandler<LoggedOutEventArgs> handler = m_LoggedOut;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_LoggedOutLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<LoggedOutEventArgs> LoggedOut
        {
            add { lock (m_LoggedOutLock) { m_LoggedOut += value; } }
            remove { lock (m_LoggedOutLock) { m_LoggedOut -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<SimConnectingEventArgs> m_SimConnecting;

        ///<summary>Raises the SimConnecting Event</summary>
        /// <param name="e">A SimConnectingEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnSimConnecting(SimConnectingEventArgs e)
        {
            EventHandler<SimConnectingEventArgs> handler = m_SimConnecting;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_SimConnectingLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<SimConnectingEventArgs> SimConnecting
        {
            add { lock (m_SimConnectingLock) { m_SimConnecting += value; } }
            remove { lock (m_SimConnectingLock) { m_SimConnecting -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<SimConnectedEventArgs> m_SimConnected;

        ///<summary>Raises the SimConnected Event</summary>
        /// <param name="e">A SimConnectedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnSimConnected(SimConnectedEventArgs e)
        {
            EventHandler<SimConnectedEventArgs> handler = m_SimConnected;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_SimConnectedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<SimConnectedEventArgs> SimConnected
        {
            add { lock (m_SimConnectedLock) { m_SimConnected += value; } }
            remove { lock (m_SimConnectedLock) { m_SimConnected -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<SimDisconnectedEventArgs> m_SimDisconnected;

        ///<summary>Raises the SimDisconnected Event</summary>
        /// <param name="e">A SimDisconnectedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnSimDisconnected(SimDisconnectedEventArgs e)
        {
            EventHandler<SimDisconnectedEventArgs> handler = m_SimDisconnected;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_SimDisconnectedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<SimDisconnectedEventArgs> SimDisconnected
        {
            add { lock (m_SimDisconnectedLock) { m_SimDisconnected += value; } }
            remove { lock (m_SimDisconnectedLock) { m_SimDisconnected -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<DisconnectedEventArgs> m_Disconnected;

        ///<summary>Raises the Disconnected Event</summary>
        /// <param name="e">A DisconnectedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnDisconnected(DisconnectedEventArgs e)
        {
            EventHandler<DisconnectedEventArgs> handler = m_Disconnected;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_DisconnectedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<DisconnectedEventArgs> Disconnected
        {
            add { lock (m_DisconnectedLock) { m_Disconnected += value; } }
            remove { lock (m_DisconnectedLock) { m_Disconnected -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<SimChangedEventArgs> m_SimChanged;

        ///<summary>Raises the SimChanged Event</summary>
        /// <param name="e">A SimChangedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnSimChanged(SimChangedEventArgs e)
        {
            EventHandler<SimChangedEventArgs> handler = m_SimChanged;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_SimChangedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<SimChangedEventArgs> SimChanged
        {
            add { lock (m_SimChangedLock) { m_SimChanged += value; } }
            remove { lock (m_SimChangedLock) { m_SimChanged -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<EventQueueRunningEventArgs> m_EventQueueRunning;

        ///<summary>Raises the EventQueueRunning Event</summary>
        /// <param name="e">A EventQueueRunningEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnEventQueueRunning(EventQueueRunningEventArgs e)
        {
            EventHandler<EventQueueRunningEventArgs> handler = m_EventQueueRunning;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_EventQueueRunningLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<EventQueueRunningEventArgs> EventQueueRunning
        {
            add { lock (m_EventQueueRunningLock) { m_EventQueueRunning += value; } }
            remove { lock (m_EventQueueRunningLock) { m_EventQueueRunning -= value; } }
        }

        #endregion Delegates

        #region Properties

        /// <summary>Unique identifier associated with our connections to
        /// simulators</summary>
        public uint CircuitCode { get; set; }

        /// <summary>The simulator that the logged in avatar is currently
        /// occupying</summary>
        public Simulator CurrentSim { get; set; }

        /// <summary>Shows whether the network layer is logged in to the
        /// grid or not</summary>
        public bool Connected { get; private set; }

        /// <summary>Number of packets in the incoming queue</summary>
        public int InboxCount => _packetInboxCount;

        /// <summary>Number of packets in the outgoing queue</summary>
        public int OutboxCount => _packetOutboxCount;

        #endregion Properties

        /// <summary>All of the simulators we are currently connected to</summary>
        public List<Simulator> Simulators = new List<Simulator>();

        /// <summary>Handlers for incoming capability events</summary>
        internal CapsEventDictionary CapsEvents;
        /// <summary>Handlers for incoming packets</summary>
        internal PacketEventDictionary PacketEvents;

        /// <summary>Incoming packets that are awaiting handling</summary>
        private Channel<IncomingPacket> _packetInbox;

        private int _packetInboxCount = 0;

        /// <summary>Outgoing packets that are awaiting handling</summary>
        private Channel<OutgoingPacket> _packetOutbox;

        private int _packetOutboxCount = 0;

        private readonly GridClient Client;
        private Timer DisconnectTimer;

        private long lastpacketwarning = 0;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">Reference to the GridClient object</param>
        public NetworkManager(GridClient client)
        {
            Client = client;

            PacketEvents = new PacketEventDictionary(client);
            CapsEvents = new CapsEventDictionary(client);

            // Register internal CAPS callbacks
            RegisterEventCallback("EnableSimulator", EnableSimulatorHandler);

            // Register the internal callbacks
            RegisterCallback(PacketType.RegionHandshake, RegionHandshakeHandler);
            RegisterCallback(PacketType.StartPingCheck, StartPingCheckHandler, false);
            RegisterCallback(PacketType.DisableSimulator, DisableSimulatorHandler);
            RegisterCallback(PacketType.KickUser, KickUserHandler);
            RegisterCallback(PacketType.LogoutReply, LogoutReplyHandler);
            RegisterCallback(PacketType.CompletePingCheck, CompletePingCheckHandler, false);
            RegisterCallback(PacketType.SimStats, SimStatsHandler, false);
            RegisterCallback(PacketType.GenericMessage, GenericMessageHandler);

            // GLOBAL SETTING: Don't force Expect-100: Continue headers on HTTP POST calls
            ServicePointManager.Expect100Continue = false;
        }

        private void GenericMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            if (e.Packet is GenericMessagePacket message)
            {
                string method = Utils.BytesToString(message.MethodData.Method);
                Logger.Log("Received Unhandled Generic Message: " + method, Helpers.LogLevel.Info, Client);
            }
        }

        /// <summary>
        /// Register an event handler for a packet. This is a low level event
        /// interface and should only be used if you are doing something not
        /// supported in the library
        /// </summary>
        /// <param name="type">Packet type to trigger events for</param>
        /// <param name="callback">Callback to fire when a packet of this type
        /// is received</param>
        public void RegisterCallback(PacketType type, EventHandler<PacketReceivedEventArgs> callback)
        {
            RegisterCallback(type, callback, true);
        }

        /// <summary>
        /// Register an event handler for a packet. This is a low level event
        /// interface and should only be used if you are doing something not
        /// supported in the library
        /// </summary>
        /// <param name="type">Packet type to trigger events for</param>
        /// <param name="callback">Callback to fire when a packet of this type
        /// is received</param>
        /// <param name="isAsync">True if the callback should be ran 
        /// asynchronously. Only set this to false (synchronous for callbacks 
        /// that will always complete quickly)</param>
        /// <remarks>If any callback for a packet type is marked as 
        /// asynchronous, all callbacks for that packet type will be fired
        /// asynchronously</remarks>
        public void RegisterCallback(PacketType type, EventHandler<PacketReceivedEventArgs> callback, bool isAsync)
        {
            PacketEvents.RegisterEvent(type, callback, isAsync);
        }

        /// <summary>
        /// Unregister an event handler for a packet. This is a low level event
        /// interface and should only be used if you are doing something not 
        /// supported in the library
        /// </summary>
        /// <param name="type">Packet type this callback is registered with</param>
        /// <param name="callback">Callback to stop firing events for</param>
        public void UnregisterCallback(PacketType type, EventHandler<PacketReceivedEventArgs> callback)
        {
            PacketEvents.UnregisterEvent(type, callback);
        }

        /// <summary>
        /// Register a CAPS event handler. This is a low level event interface
        /// and should only be used if you are doing something not supported in
        /// the library
        /// </summary>
        /// <param name="capsEvent">Name of the CAPS event to register a handler for</param>
        /// <param name="callback">Callback to fire when a CAPS event is received</param>
        public void RegisterEventCallback(string capsEvent, Caps.EventQueueCallback callback)
        {
            CapsEvents.RegisterEvent(capsEvent, callback);
        }

        /// <summary>
        /// Unregister a CAPS event handler. This is a low level event interface
        /// and should only be used if you are doing something not supported in
        /// the library
        /// </summary>
        /// <param name="capsEvent">Name of the CAPS event this callback is
        /// registered with</param>
        /// <param name="callback">Callback to stop firing events for</param>
        public void UnregisterEventCallback(string capsEvent, Caps.EventQueueCallback callback)
        {
            CapsEvents.UnregisterEvent(capsEvent, callback);
        }

        /// <summary>
        /// Send a packet to the simulator the avatar is currently occupying
        /// </summary>
        /// <param name="packet">Packet to send</param>
        public void SendPacket(Packet packet)
        {
            SendPacket(packet, CurrentSim);
        }

        /// <summary>
        /// Send a packet to a specified simulator
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <param name="simulator">Simulator to send the packet to</param>
        public void SendPacket(Packet packet, Simulator simulator)
        {
            if (simulator == null && Client.Network.Simulators.Count >= 1)
            {
                Logger.DebugLog("simulator object was null, using first found connected simulator", Client);
                simulator = Client.Network.Simulators[0];
            }
            if (simulator != null)
            {
                simulator.SendPacket(packet);
            }
            else
            {
                NetworkInvaildWarning("simulator", "SendPacket");
            }
        }

        /// <summary>
        /// Add a packet to the Inbox queue to process
        /// </summary>
        /// <param name="packet">Incoming packet to process</param>
        public void EnqueueIncoming(IncomingPacket packet)
        {
            if (_packetInbox != null)
            {
                if (_packetInbox.Writer.TryWrite(packet))
                    Interlocked.Increment(ref _packetInboxCount);
            }
            else
            {
                NetworkInvaildWarning("_packetInbox", "EnqueueIncoming");
            }
        }

        /// <summary>
        /// adds a debug message when you try to access a network item
        /// while they are still null
        /// </summary>
        /// <param name="source">what</param>
        /// <param name="function">where</param>
        protected void NetworkInvaildWarning(string source,string function)
        {
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            long dif = lastpacketwarning - now;
            if (dif > 10)
            {
                lastpacketwarning = now;
                Logger.Log(source+" is null (Are we disconnected?) - from: "+ function,
                    Helpers.LogLevel.Debug);
            }
        }
        
        /// <summary>
        /// Add a packet to the Inbox queue to process
        /// </summary>
        /// <param name="packet">Incoming packet to process</param>
        public void EnqueueOutgoing(OutgoingPacket packet)
        {
            if (_packetOutbox != null)
            {
                if (_packetOutbox.Writer.TryWrite(packet))
                    Interlocked.Increment(ref _packetOutboxCount);
            }
            else
            {
                NetworkInvaildWarning("_packetOutbox", "EnqueueOutgoing");
            }
        }

        /// <summary>
        /// Connect to a simulator
        /// </summary>
        /// <param name="ip">IP address to connect to</param>
        /// <param name="port">Port to connect to</param>
        /// <param name="handle">Handle for this simulator, to identify its
        /// location in the grid</param>
        /// <param name="setDefault">Whether to set CurrentSim to this new
        /// connection, use this if the avatar is moving in to this simulator</param>
        /// <param name="seedcaps">URL of the capabilities server to use for
        /// this sim connection</param>
        /// <returns>A Simulator object on success, otherwise null</returns>
        public Simulator Connect(IPAddress ip, ushort port, ulong handle, bool setDefault, Uri seedcaps)
        {
            IPEndPoint endPoint = new IPEndPoint(ip, port);
            return Connect(endPoint, handle, setDefault, seedcaps);
        }

        /// <summary>
        /// Connect to a simulator
        /// </summary>
        /// <param name="endPoint">IP address and port to connect to</param>
        /// <param name="handle">Handle for this simulator, to identify its
        /// location in the grid</param>
        /// <param name="setDefault">Whether to set CurrentSim to this new
        /// connection, use this if the avatar is moving in to this simulator</param>
        /// <param name="seedcaps">URL of the capabilities server to use for
        /// this sim connection</param>
        /// <returns>A Simulator object on success, otherwise null</returns>
        public Simulator Connect(IPEndPoint endPoint, ulong handle, bool setDefault, Uri seedcaps)
        {
            Simulator simulator = FindSimulator(endPoint);

            if (simulator == null)
            {
                // We're not tracking this sim, create a new Simulator object
                simulator = new Simulator(Client, endPoint, handle);

                // Immediately add this simulator to the list of current sims. It will be removed if the
                // connection fails
                lock (Simulators) Simulators.Add(simulator);
            }
            
            if (_packetInbox == null || _packetOutbox == null)
            {
                var options = new UnboundedChannelOptions() {SingleReader = true};
                
                _packetInbox = Channel.CreateUnbounded<IncomingPacket>(options);
                _packetOutbox = Channel.CreateUnbounded<OutgoingPacket>(options);

                Task.Run(IncomingPacketHandler);
                Task.Run(OutgoingPacketHandler);
            }

            if (!simulator.Connected)
            {
                // Mark that we are connecting/connected to the grid
                // 
                Connected = true;

                // raise the SimConnecting event and allow any event
                // subscribers to cancel the connection
                if (m_SimConnecting != null)
                {
                    SimConnectingEventArgs args = new SimConnectingEventArgs(simulator);
                    OnSimConnecting(args);

                    if (args.Cancel)
                    {
                        // Callback is requesting that we abort this connection
                        lock (Simulators)
                        {
                            Simulators.Remove(simulator);
                        }
                        return null;
                    }
                }

                // Attempt to establish a connection to the simulator
                if (simulator.Connect(setDefault))
                {
                    if (DisconnectTimer == null)
                    {
                        // Start a timer that checks if we've been disconnected
                        DisconnectTimer = new Timer(DisconnectTimer_Elapsed, null,
                            Client.Settings.SIMULATOR_TIMEOUT, Client.Settings.SIMULATOR_TIMEOUT);
                    }

                    if (setDefault)
                    {
                        SetCurrentSim(simulator, seedcaps);
                    }

                    // Raise the SimConnected event
                    if (m_SimConnected != null)
                    {
                        OnSimConnected(new SimConnectedEventArgs(simulator));
                    }
                    
                    // If enabled, send an AgentThrottle packet to the server to increase our bandwidth
                    if (Client.Settings.SEND_AGENT_THROTTLE)
                    {
                        Client.Throttle.Set(simulator);
                    }

                    return simulator;
                }
                else
                {
                    // Connection failed, remove this simulator from our list and destroy it
                    lock (Simulators)
                    {
                        Simulators.Remove(simulator);
                    }                    

                    return null;
                }
            }
            else if (setDefault)
            {
                Logger.Log("Moving to another simulator; sending CompleteAgentMovement to " + simulator.Name, Helpers.LogLevel.Info, Client);
                // Move in to this simulator
                simulator.handshakeComplete = false;
                simulator.UseCircuitCode(true);
                Client.Self.CompleteAgentMovement(simulator);

                // We're already connected to this server, but need to set it to the default
                SetCurrentSim(simulator, seedcaps);

                // Send an initial AgentUpdate to complete our movement in to the sim
                if (Client.Settings.SEND_AGENT_UPDATES)
                {
                    Client.Self.Movement.SendUpdate(true, simulator);
                }

                return simulator;
            }
            else
            {
                // Already connected to this simulator and wasn't asked to set it as the default,
                // just return a reference to the existing object
                return simulator;
            }
        }

        private System.Timers.Timer logoutReplyTimeout;
        /// <summary>
        /// Begins the non-blocking logout. Makes sure that the LoggedOut event is
        /// called even if the server does not send a logout reply, and Shutdown()
        /// is properly called.
        /// </summary>
        public void BeginLogout()
        {
            // Wait for a logout response (by way of the LoggedOut event. If the
            // response is received, shutdown will be fired in the callback.
            // Otherwise we fire it manually with a NetworkTimeout type after LOGOUT_TIMEOUT
            logoutReplyTimeout = new System.Timers.Timer();

            logoutReplyTimeout.Interval = Client.Settings.LOGOUT_TIMEOUT;
            logoutReplyTimeout.Elapsed += delegate
            {
                logoutReplyTimeout.Stop();
                Shutdown(DisconnectType.NetworkTimeout);
                OnLoggedOut(new LoggedOutEventArgs(new List<UUID>()));
            };
            logoutReplyTimeout.Start();

            // Send the packet requesting a clean logout
            RequestLogout();

        }

        /// <summary>
        /// Initiate a blocking logout request. This will return when the logout
        /// handshake has completed or when <code>Settings.LOGOUT_TIMEOUT</code>
        /// has expired and the network layer is manually shut down
        /// </summary>
        public void Logout()
        {
            AutoResetEvent logoutEvent = new AutoResetEvent(false);
            EventHandler<LoggedOutEventArgs> callback = delegate { logoutEvent.Set(); };

            LoggedOut += callback;

            // Send the packet requesting a clean logout
            RequestLogout();

            // Wait for a logout response. If the response is received, shutdown
            // will be fired in the callback. Otherwise we fire it manually with
            // a NetworkTimeout type
            if (!logoutEvent.WaitOne(Client.Settings.LOGOUT_TIMEOUT, false))
                Shutdown(DisconnectType.NetworkTimeout);

            LoggedOut -= callback;
        }

        /// <summary>
        /// Initiate the logout process. The <code>Shutdown()</code> function
        /// needs to be manually called.
        /// </summary>
        public void RequestLogout()
        {
            // No need to run the disconnect timer any more
            if (DisconnectTimer != null)
            {
                DisconnectTimer.Dispose();
                DisconnectTimer = null;
            }

            // This will catch a Logout when the client is not logged in
            if (CurrentSim == null || !Connected)
            {
                Logger.Log("Ignoring RequestLogout(), client is already logged out", Helpers.LogLevel.Warning, Client);
                return;
            }

            Logger.Log("Logging out", Helpers.LogLevel.Info, Client);

            // Send a logout request to the current sim
            LogoutRequestPacket logout = new LogoutRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };
            SendPacket(logout);
        }

        /// <summary>
        /// Close a connection to the given simulator
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="sendCloseCircuit"></param>
        public void DisconnectSim(Simulator simulator, bool sendCloseCircuit)
        {
            if (simulator != null)
            {
                bool wasConnected = simulator.Connected;

                simulator.Disconnect(sendCloseCircuit);

                // Fire the SimDisconnected event if a handler is registered
                if (m_SimDisconnected != null)
                {
                    OnSimDisconnected(new SimDisconnectedEventArgs(simulator, DisconnectType.NetworkTimeout, wasConnected));
                }

                int simulatorsCount;
                lock (Simulators)
                {
                    Simulators.Remove(simulator);
                    simulatorsCount = Simulators.Count;
                }

                if (simulatorsCount == 0) Shutdown(DisconnectType.SimShutdown);
            }
            else
            {
                NetworkInvaildWarning("simulator", "DisconnectSim");
            }
        }


        /// <summary>
        /// Shutdown will disconnect all the sims except for the current sim
        /// first, and then kill the connection to CurrentSim. This should only
        /// be called if the logout process times out on <code>RequestLogout</code>
        /// </summary>
        /// <param name="type">Type of shutdown</param>
        public void Shutdown(DisconnectType type)
        {
            Shutdown(type, type.ToString());
        }

        /// <summary>
        /// Shutdown will disconnect all the sims except for the current sim
        /// first, and then kill the connection to CurrentSim. This should only
        /// be called if the logout process times out on <code>RequestLogout</code>
        /// </summary>
        /// <param name="type">Type of shutdown</param>
        /// <param name="message">Shutdown message</param>
        public void Shutdown(DisconnectType type, string message)
        {
            Logger.Log($"NetworkManager shutdown initiated for {message} due to {type}", Helpers.LogLevel.Info, Client);

            // Send a CloseCircuit packet to simulators if we are initiating the disconnect
            bool sendCloseCircuit = (type == DisconnectType.ClientInitiated || type == DisconnectType.NetworkTimeout);

            lock (Simulators)
            {
                // Disconnect all simulators except the current one
                foreach (Simulator t in Simulators)
                {
                    if (t != null && t != CurrentSim)
                    {
                        bool wasConnected = t.Connected;

                        t.Disconnect(sendCloseCircuit);

                        // Fire the SimDisconnected event if a handler is registered
                        if (m_SimDisconnected != null)
                        {
                            OnSimDisconnected(new SimDisconnectedEventArgs(t, type, wasConnected));
                        }
                    }
                }

                Simulators.Clear();
            }

            if (CurrentSim != null)
            {
                bool wasConnected = CurrentSim.Connected;

                // Kill the connection to the curent simulator
                CurrentSim.Disconnect(sendCloseCircuit);

                // Fire the SimDisconnected event if a handler is registered
                if (m_SimDisconnected != null)
                {
                    OnSimDisconnected(new SimDisconnectedEventArgs(CurrentSim, type, wasConnected));
                }
            }
            
            _packetInbox?.Writer?.Complete();
            _packetOutbox?.Writer?.Complete();

            _packetInbox = null;
            _packetOutbox = null;
            
            Interlocked.Exchange(ref _packetInboxCount, 0);
            Interlocked.Exchange(ref _packetOutboxCount, 0);

            Connected = false;

            // Fire the disconnected callback
            if (m_Disconnected != null)
            {
                OnDisconnected(new DisconnectedEventArgs(type, message));
            }
        }

        /// <summary>
        /// Searches through the list of currently connected simulators to find
        /// one attached to the given IPEndPoint
        /// </summary>
        /// <param name="endPoint">IPEndPoint of the Simulator to search for</param>
        /// <returns>A Simulator reference on success, otherwise null</returns>
        public Simulator FindSimulator(IPEndPoint endPoint)
        {
            lock (Simulators)
            {
                foreach (Simulator t in Simulators)
                {
                    if (t.IPEndPoint.Equals(endPoint))
                        return t;
                }
            }

            return null;
        }

        public Simulator FindSimulator(ulong handle)
        {
            lock (Simulators)
            {
                foreach (Simulator t in Simulators)
                {
                    if (t.Handle == handle)
                        return t;
                }
            }

            return null;
        }

        internal void RaisePacketSentEvent(byte[] data, int bytesSent, Simulator simulator)
        {
            if (m_PacketSent != null)
            {
                OnPacketSent(new PacketSentEventArgs(data, bytesSent, simulator));
            }
        }

        /// <summary>
        /// Fire an event when an event queue connects for capabilities
        /// </summary>
        /// <param name="simulator">Simulator the event queue is attached to</param>
        internal void RaiseConnectedEvent(Simulator simulator)
        {
            if (m_EventQueueRunning != null)
            {
                OnEventQueueRunning(new EventQueueRunningEventArgs(simulator));
            }
        }

        private async Task OutgoingPacketHandler()
        {
            if (_packetOutbox != null)
            {
                var reader = _packetOutbox.Reader;

                // FIXME: This is kind of ridiculous. Port the HTB code from Simian over ASAP!	
                var stopwatch = new System.Diagnostics.Stopwatch();

                while (await reader.WaitToReadAsync() && Connected)
                {
                    while (reader.TryRead(out var outgoingPacket))
                    {
                        Interlocked.Decrement(ref _packetOutboxCount);

                        var simulator = outgoingPacket.Simulator;

                        stopwatch.Stop();
                        if (stopwatch.ElapsedMilliseconds < 10)
                        {
                            //Logger.DebugLog(String.Format("Rate limiting, last packet was {0}ms ago", ms));	
                            Thread.Sleep(10 - (int)stopwatch.ElapsedMilliseconds);
                        }

                        simulator.SendPacketFinal(outgoingPacket);
                        stopwatch.Start();
                    }
                }
            }
            else
            {
                NetworkInvaildWarning("_packetOutbox", "OutgoingPacketHandler");
            }

        }

        private async Task IncomingPacketHandler()
        {
            if (_packetInbox != null)
            {
                var reader = _packetInbox.Reader;

                while (await reader.WaitToReadAsync() && Connected)
                {
                    while (reader.TryRead(out var incomingPacket))
                    {
                        Interlocked.Decrement(ref _packetInboxCount);

                        var packet = incomingPacket.Packet;
                        var simulator = incomingPacket.Simulator;

                        if (packet == null) continue;

                        // Skip blacklisted packets
                        if (UDPBlacklist.Contains(packet.Type.ToString()))
                        {
                            Logger.Log($"Discarding Blacklisted packet {packet.Type} from {simulator.IPEndPoint}",
                                Helpers.LogLevel.Warning);
                            return;
                        }

                        // Fire the callback(s), if any
                        PacketEvents.RaiseEvent(packet.Type, packet, simulator);
                    }
                }
            }
            else
            {
                NetworkInvaildWarning("_packetInbox", "IncomingPacketHandler");
            }
        }

        private void SetCurrentSim(Simulator simulator, Uri seedcaps)
        {
            if (simulator == CurrentSim) return;

            Simulator oldSim = CurrentSim;
            lock (Simulators) CurrentSim = simulator; // CurrentSim is synchronized against Simulators

            simulator.SetSeedCaps(seedcaps, oldSim != simulator);

            // If the current simulator changed fire the callback
            if (m_SimChanged != null && simulator != oldSim)
            {
                OnSimChanged(new SimChangedEventArgs(oldSim));
            }
        }

        #region Timers

        private void DisconnectTimer_Elapsed(object obj)
        {
            if (!Connected || CurrentSim == null)
            {
                if (DisconnectTimer != null)
                {
                    DisconnectTimer.Dispose();
                    DisconnectTimer = null;
                }
                Connected = false;
            }
            else if (CurrentSim.DisconnectCandidate)
            {
                // The currently occupied simulator hasn't sent us any traffic in a while, shutdown
                Logger.Log($"Network timeout for the current simulator ({CurrentSim}), logging out",
                    Helpers.LogLevel.Warning, Client);

                if (DisconnectTimer != null)
                {
                    DisconnectTimer.Dispose();
                    DisconnectTimer = null;
                }

                Connected = false;

                // Shutdown the network layer
                Shutdown(DisconnectType.NetworkTimeout);
            }
            else
            {
                // Mark the current simulator as potentially disconnected each time this timer fires.
                // If the timer is fired again before any packets are received, an actual disconnect
                // sequence will be triggered
                CurrentSim.DisconnectCandidate = true;
            }
        }

        #endregion Timers

        #region Packet Callbacks

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void LogoutReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            LogoutReplyPacket logout = (LogoutReplyPacket)e.Packet;

            if ((logout.AgentData.SessionID == Client.Self.SessionID) && (logout.AgentData.AgentID == Client.Self.AgentID))
            {
                Logger.DebugLog("Logout reply received", Client);
                logoutReplyTimeout?.Stop();

                // Deal with callbacks, if any
                if (m_LoggedOut != null)
                {
                    var itemIDs = logout.InventoryData.Select(inventoryData => inventoryData.ItemID).ToList();

                    OnLoggedOut(new LoggedOutEventArgs(itemIDs));
                }

                // If we are receiving a LogoutReply packet assume this is a client initiated shutdown
                Shutdown(DisconnectType.ClientInitiated);
            }
            else
            {
                Logger.Log("Invalid Session or Agent ID received in Logout Reply... ignoring", Helpers.LogLevel.Warning, Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void StartPingCheckHandler(object sender, PacketReceivedEventArgs e)
        {
            StartPingCheckPacket incomingPing = (StartPingCheckPacket)e.Packet;
            CompletePingCheckPacket ping = new CompletePingCheckPacket
            {
                PingID = {PingID = incomingPing.PingID.PingID},
                Header = {Reliable = false}
            };
            // TODO: We can use OldestUnacked to correct transmission errors
            //   I don't think that's right.  As far as I can tell, the Viewer
            //   only uses this to prune its duplicate-checking buffer. -bushing

            SendPacket(ping, e.Simulator);
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void CompletePingCheckHandler(object sender, PacketReceivedEventArgs e)
        {
            CompletePingCheckPacket pong = (CompletePingCheckPacket)e.Packet;
            //String retval = "Pong2: " + (Environment.TickCount - e.Simulator.Stats.LastPingSent);
            //if ((pong.PingID.PingID - e.Simulator.Stats.LastPingID + 1) != 0)
            //    retval += " (gap of " + (pong.PingID.PingID - e.Simulator.Stats.LastPingID + 1) + ")";

            e.Simulator.Stats.LastLag = Environment.TickCount - e.Simulator.Stats.LastPingSent;
            e.Simulator.Stats.ReceivedPongs++;
            //			Client.Log(retval, Helpers.LogLevel.Info);
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void SimStatsHandler(object sender, PacketReceivedEventArgs e)
        {
            if (!Client.Settings.ENABLE_SIMSTATS)
            {
                return;
            }
            SimStatsPacket stats = (SimStatsPacket)e.Packet;
            foreach (SimStatsPacket.StatBlock s in stats.Stat)
            {
                switch (s.StatID)
                {
                    case 0:
                        e.Simulator.Stats.Dilation = s.StatValue;
                        break;
                    case 1:
                        e.Simulator.Stats.FPS = Convert.ToInt32(s.StatValue);
                        break;
                    case 2:
                        e.Simulator.Stats.PhysicsFPS = s.StatValue;
                        break;
                    case 3:
                        e.Simulator.Stats.AgentUpdates = s.StatValue;
                        break;
                    case 4:
                        e.Simulator.Stats.FrameTime = s.StatValue;
                        break;
                    case 5:
                        e.Simulator.Stats.NetTime = s.StatValue;
                        break;
                    case 6:
                        e.Simulator.Stats.OtherTime = s.StatValue;
                        break;
                    case 7:
                        e.Simulator.Stats.PhysicsTime = s.StatValue;
                        break;
                    case 8:
                        e.Simulator.Stats.AgentTime = s.StatValue;
                        break;
                    case 9:
                        e.Simulator.Stats.ImageTime = s.StatValue;
                        break;
                    case 10:
                        e.Simulator.Stats.ScriptTime = s.StatValue;
                        break;
                    case 11:
                        e.Simulator.Stats.Objects = Convert.ToInt32(s.StatValue);
                        break;
                    case 12:
                        e.Simulator.Stats.ScriptedObjects = Convert.ToInt32(s.StatValue);
                        break;
                    case 13:
                        e.Simulator.Stats.Agents = Convert.ToInt32(s.StatValue);
                        break;
                    case 14:
                        e.Simulator.Stats.ChildAgents = Convert.ToInt32(s.StatValue);
                        break;
                    case 15:
                        e.Simulator.Stats.ActiveScripts = Convert.ToInt32(s.StatValue);
                        break;
                    case 16:
                        e.Simulator.Stats.LSLIPS = Convert.ToInt32(s.StatValue);
                        break;
                    case 17:
                        e.Simulator.Stats.INPPS = Convert.ToInt32(s.StatValue);
                        break;
                    case 18:
                        e.Simulator.Stats.OUTPPS = Convert.ToInt32(s.StatValue);
                        break;
                    case 19:
                        e.Simulator.Stats.PendingDownloads = Convert.ToInt32(s.StatValue);
                        break;
                    case 20:
                        e.Simulator.Stats.PendingUploads = Convert.ToInt32(s.StatValue);
                        break;
                    case 21:
                        e.Simulator.Stats.VirtualSize = Convert.ToInt32(s.StatValue);
                        break;
                    case 22:
                        e.Simulator.Stats.ResidentSize = Convert.ToInt32(s.StatValue);
                        break;
                    case 23:
                        e.Simulator.Stats.PendingLocalUploads = Convert.ToInt32(s.StatValue);
                        break;
                    case 24:
                        e.Simulator.Stats.UnackedBytes = Convert.ToInt32(s.StatValue);
                        break;
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void RegionHandshakeHandler(object sender, PacketReceivedEventArgs e)
        {
            RegionHandshakePacket handshake = (RegionHandshakePacket)e.Packet;
            Simulator simulator = e.Simulator;
            e.Simulator.ID = handshake.RegionInfo.CacheID;

            simulator.IsEstateManager = handshake.RegionInfo.IsEstateManager;
            simulator.Name = Utils.BytesToString(handshake.RegionInfo.SimName);
            simulator.SimOwner = handshake.RegionInfo.SimOwner;
            simulator.TerrainBase0 = handshake.RegionInfo.TerrainBase0;
            simulator.TerrainBase1 = handshake.RegionInfo.TerrainBase1;
            simulator.TerrainBase2 = handshake.RegionInfo.TerrainBase2;
            simulator.TerrainBase3 = handshake.RegionInfo.TerrainBase3;
            simulator.TerrainDetail0 = handshake.RegionInfo.TerrainDetail0;
            simulator.TerrainDetail1 = handshake.RegionInfo.TerrainDetail1;
            simulator.TerrainDetail2 = handshake.RegionInfo.TerrainDetail2;
            simulator.TerrainDetail3 = handshake.RegionInfo.TerrainDetail3;
            simulator.TerrainHeightRange00 = handshake.RegionInfo.TerrainHeightRange00;
            simulator.TerrainHeightRange01 = handshake.RegionInfo.TerrainHeightRange01;
            simulator.TerrainHeightRange10 = handshake.RegionInfo.TerrainHeightRange10;
            simulator.TerrainHeightRange11 = handshake.RegionInfo.TerrainHeightRange11;
            simulator.TerrainStartHeight00 = handshake.RegionInfo.TerrainStartHeight00;
            simulator.TerrainStartHeight01 = handshake.RegionInfo.TerrainStartHeight01;
            simulator.TerrainStartHeight10 = handshake.RegionInfo.TerrainStartHeight10;
            simulator.TerrainStartHeight11 = handshake.RegionInfo.TerrainStartHeight11;
            simulator.WaterHeight = handshake.RegionInfo.WaterHeight;
            simulator.Flags = (RegionFlags)handshake.RegionInfo.RegionFlags;
            simulator.BillableFactor = handshake.RegionInfo.BillableFactor;
            simulator.Access = (SimAccess)handshake.RegionInfo.SimAccess;

            simulator.RegionID = handshake.RegionInfo2.RegionID;
            simulator.ColoLocation = Utils.BytesToString(handshake.RegionInfo3.ColoName);
            simulator.CPUClass = handshake.RegionInfo3.CPUClassID;
            simulator.CPURatio = handshake.RegionInfo3.CPURatio;
            simulator.ProductName = Utils.BytesToString(handshake.RegionInfo3.ProductName);
            simulator.ProductSku = Utils.BytesToString(handshake.RegionInfo3.ProductSKU);

            if (handshake.RegionInfo4 != null && handshake.RegionInfo4.Length > 0)
            {
                simulator.Protocols = (RegionProtocols)handshake.RegionInfo4[0].RegionProtocols;
                // Yes, overwrite region flags if we have extended version of them
                simulator.Flags = (RegionFlags)handshake.RegionInfo4[0].RegionFlagsExtended;
            }

            // Send a RegionHandshakeReply
            RegionHandshakeReplyPacket reply = new RegionHandshakeReplyPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                RegionInfo = { Flags = 0x1 | 0x2 | 0x4 } // 0x3 == 
            };
            SendPacket(reply, simulator);

            // We're officially connected to this sim
            simulator.connected = true;
            simulator.handshakeComplete = true;
            simulator.ConnectedEvent.Set();
        }

        protected void EnableSimulatorHandler(string capsKey, IMessage message, Simulator simulator)
        {
            if (!Client.Settings.MULTIPLE_SIMS) return;

            EnableSimulatorMessage msg = (EnableSimulatorMessage)message;

            foreach (EnableSimulatorMessage.SimulatorInfoBlock t in msg.Simulators)
            {
                IPAddress ip = t.IP;
                ushort port = (ushort)t.Port;
                ulong handle = t.RegionHandle;

                IPEndPoint endPoint = new IPEndPoint(ip, port);

                if (FindSimulator(endPoint) != null) return;

                if (Connect(ip, port, handle, false, null) == null)
                {
                    Logger.Log($"Unable to connect to new sim {ip}:{port}",
                        Helpers.LogLevel.Error, Client);
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void DisableSimulatorHandler(object sender, PacketReceivedEventArgs e)
        {
            DisconnectSim(e.Simulator, false);
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void KickUserHandler(object sender, PacketReceivedEventArgs e)
        {
            string message = Utils.BytesToString(((KickUserPacket)e.Packet).UserInfo.Reason);

            // Shutdown the network layer
            Shutdown(DisconnectType.ServerInitiated, message);
        }

        #endregion Packet Callbacks
    }
    #region EventArgs

    public class PacketReceivedEventArgs : EventArgs
    {
        public Packet Packet { get; }

        public Simulator Simulator { get; }

        public PacketReceivedEventArgs(Packet packet, Simulator simulator)
        {
            Packet = packet;
            Simulator = simulator;
        }
    }

    public class LoggedOutEventArgs : EventArgs
    {
        private readonly List<UUID> m_InventoryItems;
        public List<UUID> InventoryItems;

        public LoggedOutEventArgs(List<UUID> inventoryItems)
        {
            this.m_InventoryItems = inventoryItems;
        }
    }

    public class PacketSentEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public int SentBytes { get; }
        public Simulator Simulator { get; }

        public PacketSentEventArgs(byte[] data, int bytesSent, Simulator simulator)
        {
            Data = data;
            SentBytes = bytesSent;
            Simulator = simulator;
        }
    }

    public class SimConnectingEventArgs : EventArgs
    {
        public Simulator Simulator { get; }

        public bool Cancel { get; set; }

        public SimConnectingEventArgs(Simulator simulator)
        {
            Simulator = simulator;
            Cancel = false;
        }
    }

    public class SimConnectedEventArgs : EventArgs
    {
        public Simulator Simulator { get; }

        public SimConnectedEventArgs(Simulator simulator)
        {
            Simulator = simulator;
        }
    }

    public class SimDisconnectedEventArgs : EventArgs
    {
        public Simulator Simulator { get; }

        public NetworkManager.DisconnectType Reason { get; }

        public bool WasConnected { get; }

        public SimDisconnectedEventArgs(Simulator simulator, NetworkManager.DisconnectType reason, bool wasConnected)
        {
            Simulator = simulator;
            Reason = reason;
            WasConnected = wasConnected;
        }
    }

    public class DisconnectedEventArgs : EventArgs
    {
        public NetworkManager.DisconnectType Reason { get; }

        public string Message { get; }

        public DisconnectedEventArgs(NetworkManager.DisconnectType reason, string message)
        {
            Reason = reason;
            Message = message;
        }
    }

    public class SimChangedEventArgs : EventArgs
    {
        public Simulator PreviousSimulator { get; }

        public SimChangedEventArgs(Simulator previousSimulator)
        {
            PreviousSimulator = previousSimulator;
        }
    }

    public class EventQueueRunningEventArgs : EventArgs
    {
        public Simulator Simulator { get; }

        public EventQueueRunningEventArgs(Simulator simulator)
        {
            Simulator = simulator;
        }
    }
    #endregion
}
