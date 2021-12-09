using System;
using System.Net;

namespace OpenMetaverse
{
    // this class encapsulates a single packet that
    // is either sent or received by a UDP socket
    public class UDPPacketBuffer
    {
        /// <summary>Size of the byte array used to store raw packet data</summary>
        public const int DEFAULT_BUFFER_SIZE = 4096;

        /// <summary>Raw packet data buffer</summary>
        public readonly byte[] Data;

        /// <summary>Length of the data to transmit</summary>
        public int DataLength;

        /// <summary>EndPoint of the remote host</summary>
        public EndPoint RemoteEndPoint;
        
        /// <summary>
        /// Create an allocated UDP packet buffer for receiving a packet
        /// </summary>
        public UDPPacketBuffer()
        {
            Data = new byte[DEFAULT_BUFFER_SIZE];
            // Will be modified later by BeginReceiveFrom()
            RemoteEndPoint = new IPEndPoint(Settings.BIND_ADDR, 0);
        }

        /// <summary>
        /// Create an allocated UDP packet buffer for sending a packet
        /// </summary>
        /// <param name="endPoint">EndPoint of the remote host</param>
        public UDPPacketBuffer(IPEndPoint endPoint)
        {
            Data = new byte[DEFAULT_BUFFER_SIZE];
            RemoteEndPoint = endPoint;
        }

        /// <summary>
        /// Create an allocated UDP packet buffer for sending a packet
        /// </summary>
        /// <param name="endPoint">EndPoint of the remote host</param>
        /// <param name="bufferSize">Size of the buffer to allocate for packet data</param>
        public UDPPacketBuffer(IPEndPoint endPoint, int bufferSize)
        {
            Data = new byte[bufferSize];
            RemoteEndPoint = endPoint;
        }

        /// <summary>
        /// Create an allocated UDP packet buffer for sending a packet
        /// </summary>
        public UDPPacketBuffer(byte[] buffer, int bufferSize, IPEndPoint destination, int category)
        {
            Data = new byte[bufferSize];
            this.CopyFrom(buffer, bufferSize);
            DataLength = bufferSize;

            RemoteEndPoint = destination;
        }

        /// <summary>
        /// Create an allocated UDP packet buffer for sending a packet
        /// </summary>
        /// <param name="endPoint">EndPoint of the remote host</param>
        /// <param name="data">The actual buffer to use for packet data (no allocation).</param>
        public UDPPacketBuffer(IPEndPoint endPoint, byte[] data)
        {
            Data = data;
            RemoteEndPoint = endPoint;
        }

        public void CopyFrom(Array src, int length)
        {
            Buffer.BlockCopy(src, 0, this.Data, 0, length);
        }

        public void CopyFrom(Array src)
        {
            this.CopyFrom(src, src.Length);
        }

        public void ResetEndpoint()
        {
            RemoteEndPoint = new IPEndPoint(Settings.BIND_ADDR, 0);
        }
    }
}
