/*
 * Copyright (c) 2006, Clutch, Inc.
 * Copyright (c) 2026, Sjofn LLC.
 * Original Author: Jeff Cesnik
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
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace LibreMetaverse
{
    /// <summary>
    ///
    /// </summary>
    public abstract class UDPBase
    {
        // these abstract methods must be implemented in a derived class to actually do
        // something with the packets that are sent and received.
        protected abstract void PacketReceived(UDPPacketBuffer buffer);
        protected abstract void PacketSent(UDPPacketBuffer buffer, int bytesSent);

        /// <summary>
        /// Called when an incoming UDP packet is dropped because the receive queue is full.
        /// Override in a derived class to collect backpressure statistics.
        /// </summary>
        protected virtual void OnPacketDropped() { }

        // the port to listen on
        protected int udpPort;

        // the remote endpoint to communicate with
        protected IPEndPoint? remoteEndPoint = null;

        // the UDP socket
        private Socket? udpSocket = null;

        // the all important shutdownFlag.
        private volatile bool shutdownFlag = true;

        // Packet pool for default sized packets
        private readonly ObjectPool<UDPPacketBuffer> _packetPool =
            new DefaultObjectPool<UDPPacketBuffer>(new DefaultPooledObjectPolicy<UDPPacketBuffer>());

        // Channel between socket receive and packet decode — SingleWriter + SingleReader
        // avoids locking inside the channel on the hot path.
        private Channel<UDPPacketBuffer>? _rawChannel;
        private CancellationTokenSource? _udpCts;
        private Task? _receiveTask;
        private Task? _decodeTask;

        /// <summary>
        /// Initialize the UDP packet handler in server mode
        /// </summary>
        /// <param name="port">Port to listening for incoming UDP packets on</param>
        protected UDPBase(int port)
        {
            udpPort = port;
        }

        /// <summary>
        /// Initialize the UDP packet handler in client mode
        /// </summary>
        /// <param name="endPoint">Remote UDP server to connect to</param>
        protected UDPBase(IPEndPoint endPoint)
        {
            remoteEndPoint = endPoint;
            udpPort = 0;
        }

        /// <summary>
        /// Start UPD connection
        /// </summary>
        public void Start()
        {
            if (!shutdownFlag) return;

            IPEndPoint ipep = new IPEndPoint(Settings.BindAddress, udpPort);
            udpSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // this udp socket flag is not supported under mono,
                    // so we'll catch the exception and continue
                    const int SIO_UDP_CONNRESET = -1744830452;
                    udpSocket!.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
                }
                catch (Exception)
                {
                    Logger.DebugLog("UDP SIO_UDP_CONNRESET flag not supported on this platform");
                }
            }

            // On at least Mono 3.2.8, multiple UDP sockets can bind to the same port by default.  This means that
            // when running multiple connections, two can occasionally bind to the same port, leading to unexpected
            // errors as they intercept each others messages.  We need to prevent this.  This is not allowed by
            // default on Windows.
            udpSocket!.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

            udpSocket!.Bind(ipep);

            // we're not shutting down, we're starting up
            shutdownFlag = false;

            // Bounded, drop-write: when the decode loop falls behind the receive loop drops
            // the newest packet rather than blocking the socket receive loop or growing memory
            // without bound.  Capacity is configurable via Settings.UdpReceiveQueueCapacity.
            _rawChannel = Channel.CreateBounded<UDPPacketBuffer>(
                new BoundedChannelOptions(Settings.UdpReceiveQueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleWriter = true,
                    SingleReader = true,
                });

            _udpCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_udpCts.Token));
            _decodeTask  = Task.Run(() => DecodeLoopAsync(_udpCts.Token));
        }

        /// <summary>
        /// Stop UPD connection
        /// </summary>
        public void Stop()
        {
            if (shutdownFlag) return;

            shutdownFlag = true;

            // Cancel the loops first so ReceiveLoopAsync wakes up; closing the socket
            // will also cause the pending ReceiveFromAsync to throw ObjectDisposedException.
            _udpCts?.Cancel();
            udpSocket?.Close();
        }

        /// <summary>
        /// Is UDP connection up and running
        /// </summary>
        public bool IsRunning => !shutdownFlag;

        /// <summary>
        /// Continuously awaits incoming UDP datagrams and writes filled buffers into
        /// the raw channel for the decode loop to process.  One buffer per datagram —
        /// no synchronous work is done here beyond the pool allocation.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var writer = _rawChannel!.Writer;
            // Any non-null EndPoint is valid for ReceiveFromAsync when we don't yet know the sender.
            EndPoint anyEP = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var buf = _packetPool.Get();
                    try
                    {
                        var result = await udpSocket!.ReceiveFromAsync(
                            new ArraySegment<byte>(buf.Data),
                            SocketFlags.None,
                            anyEP).ConfigureAwait(false);

                        buf.DataLength = result.ReceivedBytes;
                        buf.RemoteEndPoint = result.RemoteEndPoint;

                        // TryWrite returns false when the channel is full (DropWrite) or has
                        // been completed (Stop()).  Either way the buffer goes back to the pool.
                        if (!writer.TryWrite(buf))
                        {
                            _packetPool.Return(buf);
                            OnPacketDropped();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _packetPool.Return(buf);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Socket was closed by Stop() — exit cleanly.
                        _packetPool.Return(buf);
                        break;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        // ICMP port-unreachable on Windows — not fatal, retry.
                        _packetPool.Return(buf);
                    }
                    catch (SocketException)
                    {
                        _packetPool.Return(buf);
                        if (shutdownFlag) break;
                        // Other transient socket errors — keep trying.
                    }
                }
            }
            finally
            {
                writer.TryComplete();
            }
        }

        /// <summary>
        /// Drains the raw channel and calls the abstract <see cref="PacketReceived"/> for
        /// each buffer.  Runs on its own thread so the socket receive loop is never blocked
        /// by packet decoding.
        /// </summary>
        private async Task DecodeLoopAsync(CancellationToken ct)
        {
            var reader = _rawChannel!.Reader;
            try
            {
                await foreach (var buf in reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        PacketReceived(buf);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("DecodeLoopAsync unhandled exception in PacketReceived: " + ex, ex);
                    }
                    finally
                    {
                        _packetPool.Return(buf);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public void AsyncBeginSend(UDPPacketBuffer buf)
        {
            if (shutdownFlag) return;
            try
            {
                // Profiling heavily loaded clients was showing better performance with
                // synchronous UDP packet sending
                var socket = udpSocket;
                if (socket == null) return;

                socket.SendTo(
                    buf.Data,
                    0,
                    buf.DataLength,
                    SocketFlags.None,
                    buf.RemoteEndPoint);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }
}
