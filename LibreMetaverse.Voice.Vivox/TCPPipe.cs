/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2024, Sjofn LLC.
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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace LibreMetaverse.Voice.Vivox
{
    public class TCPPipe
    {
        private class SocketPacket
        {
            public Socket TcpSocket;
            public readonly byte[] DataBuffer = new byte[1];
        }

        public delegate void OnReceiveLineCallback(string line);
        public delegate void OnDisconnectedCallback(SocketException se);

        public event OnReceiveLineCallback OnReceiveLine;
        public event OnDisconnectedCallback OnDisconnected;

        private Socket _tcpSocket;
        protected IAsyncResult _result;
        private AsyncCallback _callback;
        private string _buffer = string.Empty;

        public bool Connected => _tcpSocket != null && _tcpSocket.Connected;

        public SocketException Connect(string address, int port)
        {
            if (_tcpSocket != null && _tcpSocket.Connected)
                Disconnect();

            try
            {
                IPAddress ip;
                if (!IPAddress.TryParse(address, out ip))
                {
                    var ips = Dns.GetHostAddresses(address);
                    ip = ips[0];
                }
                _tcpSocket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                var ipEndPoint = new IPEndPoint(ip, port);
                _tcpSocket.Connect(ipEndPoint);
                if (_tcpSocket.Connected)
                {
                    WaitForData();
                    return null;
                }
                else
                {
                    return new SocketException(10000);
                }
            }
            catch (SocketException se)
            {
                return se;
            }
        }

        public void Disconnect()
        {
            _tcpSocket.Disconnect(true);
        }

        public void SendData(byte[] data)
        {
            if (Connected)
                _tcpSocket.Send(data);
            else
                throw new InvalidOperationException("socket is not connected");
        }

        public void SendLine(string message)
        {
            if (Connected)
            {
                var byData = System.Text.Encoding.ASCII.GetBytes(message + "\n");
                _tcpSocket.Send(byData);
            }
            else
            {
                throw new InvalidOperationException("socket is not connected");
            }
        }

        void WaitForData()
        {
            try
            {
                if (_callback == null) _callback = new AsyncCallback(OnDataReceived);
                var packet = new SocketPacket
                {
                    TcpSocket = _tcpSocket
                };
                _result = _tcpSocket.BeginReceive(packet.DataBuffer, 0, packet.DataBuffer.Length, SocketFlags.None, _callback, packet);
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
            }
        }

        static readonly char[] SplitNull = { '\0' };
        static readonly string[] SplitLines = { "\r", "\n", "\r\n" };

        private void ReceiveData(string data)
        {
            if (OnReceiveLine == null) return;

            //string[] splitNull = { "\0" };
            var line = data.Split(SplitNull, StringSplitOptions.None);
            _buffer += line[0];
            //string[] splitLines = { "\r\n", "\r", "\n" };
            var lines = _buffer.Split(SplitLines, StringSplitOptions.None);
            if (lines.Length > 1)
            {
                int i;
                for (i = 0; i < lines.Length - 1; i++)
                {
                    if (lines[i].Trim().Length > 0) OnReceiveLine(lines[i]);
                }
                _buffer = lines[i];
            }
        }

        private void OnDataReceived(IAsyncResult asyn)
        {
            try
            {
                var packet = (SocketPacket)asyn.AsyncState;
                Debug.Assert(packet != null, nameof(packet) + " != null");
                var end = packet.TcpSocket.EndReceive(asyn);
                var chars = new char[end + 1];
                var d = System.Text.Encoding.UTF8.GetDecoder();
                d.GetChars(packet.DataBuffer, 0, end, chars, 0);
                var data = new string(chars);
                ReceiveData(data);
                WaitForData();
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("WARNING: Socket closed unexpectedly");
            }
            catch (SocketException se)
            {
                if (!_tcpSocket.Connected)
                {
                    OnDisconnected?.Invoke(se);
                }
            }
        }

    }
}
