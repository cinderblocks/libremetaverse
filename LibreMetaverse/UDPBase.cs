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
using Microsoft.Extensions.ObjectPool;
using System.Collections.Concurrent;

namespace OpenMetaverse
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

        // the port to listen on
        protected int udpPort;

        // the remote endpoint to communicate with
        protected IPEndPoint remoteEndPoint = null;

        // the UDP socket
        private Socket udpSocket;

        // SocketAsyncEventArgs used for receive
        private readonly ConcurrentQueue<SocketAsyncEventArgs> _receiveEventArgsPool = new ConcurrentQueue<SocketAsyncEventArgs>();
        private const int MAX_RECEIVE_SAEA_POOL = 4;

        // the all important shutdownFlag.
        private volatile bool shutdownFlag = true;
        
        private readonly ObjectPool<UDPPacketBuffer> _packetPool =
            new DefaultObjectPool<UDPPacketBuffer>(new DefaultPooledObjectPolicy<UDPPacketBuffer>());

        // SocketAsyncEventArgs used for send
        private readonly ConcurrentQueue<SocketAsyncEventArgs> _sendEventArgsPool = new ConcurrentQueue<SocketAsyncEventArgs>();

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
            if (!shutdownFlag) { return; }

            IPEndPoint ipep = new IPEndPoint(Settings.BIND_ADDR, udpPort);
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
                    udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
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
            udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

            udpSocket.Bind(ipep);

            // we're not shutting down, we're starting up
            shutdownFlag = false;

            Logger.Info($"UDP listener starting on port {udpPort}");

            // kick off an async receive using SocketAsyncEventArgs
            PostReceive();
        }

        /// <summary>
        /// Stop UPD connection
        /// </summary>
        public void Stop()
        {
            if (shutdownFlag) { return; }

            Logger.Info($"Stopping UDP listener on port {udpPort}");
            // signal shutdown so no more receives are posted
            shutdownFlag = true;

            try
            {
                // close socket first to cancel any in-flight operations
                udpSocket.Close();
                Logger.Info($"UDP socket on port {udpPort} closed");
            }
            catch (ObjectDisposedException) { Logger.DebugLog($"UDP socket on port {udpPort} already disposed during Stop()"); }

            // dispose any pooled receive event args
            while (_receiveEventArgsPool.TryDequeue(out var sa))
            {
                try { sa.Completed -= ReceiveCompleted; sa.Dispose(); } catch { }
            }
        }

        private void PostReceive()
        {
            if (shutdownFlag) { return; }
            if (udpSocket == null) { return; }

            // get a buffer from the pool for this receive
            UDPPacketBuffer buf = _packetPool.Get();

            // get or create a SAEA for this receive
            var sa = GetReceiveEventArgs();
            sa.UserToken = buf;
            sa.RemoteEndPoint = buf.RemoteEndPoint ?? new IPEndPoint(Settings.BIND_ADDR, 0);
            sa.SetBuffer(buf.Data, 0, UDPPacketBuffer.DEFAULT_BUFFER_SIZE);

            bool willRaiseEvent = false;
            try
            {
                willRaiseEvent = udpSocket.ReceiveFromAsync(sa);
            }
            catch (SocketException ex) // salvage logic similar to previous behavior
            {
                Logger.Error($"ReceiveFromAsync SocketException on port {udpPort}: {ex.SocketErrorCode} {ex.Message}");
                // try again once synchronously
                try { udpSocket.ReceiveFromAsync(sa); }
                catch (Exception ex2) { Logger.DebugLog($"Second ReceiveFromAsync attempt failed: {ex2.Message}"); _packetPool.Return(buf); CleanupAndReturnReceiveEventArgs(sa); return; }
            }
            catch (ObjectDisposedException ex) { Logger.DebugLog($"ReceiveFromAsync aborted, socket disposed: {ex.Message}"); _packetPool.Return(buf); CleanupAndReturnReceiveEventArgs(sa); return; }

            // If the operation completed synchronously, process it now
            if (!willRaiseEvent)
            {
                ProcessReceive(sa);
            }
        }

        private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessReceive(e);
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            UDPPacketBuffer buf = (UDPPacketBuffer)e.UserToken;

            try
            {
                if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
                {
                    buf.DataLength = e.BytesTransferred;

                    // e.RemoteEndPoint contains the sender endpoint
                    buf.RemoteEndPoint = e.RemoteEndPoint;

                    PacketReceived(buf);
                }
            }
            catch (ObjectDisposedException ex) { Logger.DebugLog($"ProcessReceive: socket disposed while processing receive: {ex.Message}"); }
            catch (SocketException ex) { Logger.Error($"ProcessReceive SocketException: {ex.SocketErrorCode} {ex.Message}"); }
            finally
            {
                // return the buffer to the pool
                _packetPool.Return(buf);
            }

            // clean up and return the SAEA to the pool
            CleanupAndReturnReceiveEventArgs(e);

            // post another receive if still running
            if (!shutdownFlag)
            {
                try { PostReceive(); }
                catch (ObjectDisposedException) { }
            }
        }

        public void AsyncBeginSend(UDPPacketBuffer buf)
        {
            if (shutdownFlag) return;

            SocketAsyncEventArgs sendEventArgs = GetSendEventArgs();
            sendEventArgs.RemoteEndPoint = buf.RemoteEndPoint;
            sendEventArgs.SetBuffer(buf.Data, 0, buf.DataLength);
            sendEventArgs.UserToken = buf;

            bool willRaiseEvent = false;
            try
            {
                willRaiseEvent = udpSocket.SendToAsync(sendEventArgs);
            }
            catch (SocketException ex)
            {
                Logger.Error($"SendToAsync SocketException sending to {buf.RemoteEndPoint}: {ex.SocketErrorCode} {ex.Message}");
                // on error, clean up and return to pool
                CleanupAndReturnSendEventArgs(sendEventArgs);
                return;
            }
            catch (ObjectDisposedException ex)
            {
                Logger.DebugLog($"SendToAsync aborted, socket disposed: {ex.Message}");
                CleanupAndReturnSendEventArgs(sendEventArgs);
                return;
            }

            // If the operation completed synchronously, process completion now
            if (!willRaiseEvent)
            {
                try
                {
                    // Call the completion handler directly
                    SendCompleted(this, sendEventArgs);
                }
                catch
                {
                    // swallow to match previous behavior
                }
            }
        }

        private void SendCompleted(object sender, SocketAsyncEventArgs e)
        {
            // detach handler and capture token
            UDPPacketBuffer buf = (UDPPacketBuffer)e.UserToken;

            int bytesSent = 0;
            if (e.SocketError == SocketError.Success)
            {
                bytesSent = e.BytesTransferred;
            }
            else
            {
                Logger.Error($"Send failed to {buf.RemoteEndPoint}: {e.SocketError}");
            }

            try { PacketSent(buf, bytesSent); } catch (Exception ex) { Logger.Error($"PacketSent handler threw: {ex.Message}"); }

            // clean up and return to pool for reuse
            CleanupAndReturnSendEventArgs(e);
        }

        private SocketAsyncEventArgs GetSendEventArgs()
        {
            if (_sendEventArgsPool.TryDequeue(out var args)) return args;

            var sea = new SocketAsyncEventArgs();
            // Attach the completion handler once
            sea.Completed += SendCompleted;
            return sea;
        }

        private void CleanupAndReturnSendEventArgs(SocketAsyncEventArgs e)
        {
            try
            {
                // clear buffer and token to avoid holding references
                try { e.SetBuffer(null, 0, 0); } catch { }
                e.UserToken = null;
                e.RemoteEndPoint = null;

                _sendEventArgsPool.Enqueue(e);
                Logger.DebugLog("Send SocketAsyncEventArgs returned to pool");
            }
            catch { try { e.Dispose(); } catch { } }
        }

        private SocketAsyncEventArgs GetReceiveEventArgs()
        {
            if (_receiveEventArgsPool.TryDequeue(out var args)) return args;

            var sea = new SocketAsyncEventArgs();
            sea.Completed += ReceiveCompleted;
            return sea;
        }

        private void CleanupAndReturnReceiveEventArgs(SocketAsyncEventArgs e)
        {
            try
            {
                // clear buffer and token to avoid holding references
                try { e.SetBuffer(null, 0, 0); } catch { }
                e.UserToken = null;
                e.RemoteEndPoint = null;

                if (_receiveEventArgsPool.Count < MAX_RECEIVE_SAEA_POOL)
                {
                    _receiveEventArgsPool.Enqueue(e);
                    Logger.DebugLog("Receive SocketAsyncEventArgs returned to pool");
                }
                else
                {
                    e.Completed -= ReceiveCompleted;
                    Logger.DebugLog("Receive SocketAsyncEventArgs pool full, disposing instance");
                    e.Dispose();
                }
            }
            catch { try { e.Dispose(); } catch { } }
        }
    }
}
