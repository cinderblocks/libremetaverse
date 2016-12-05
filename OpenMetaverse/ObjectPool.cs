using System;
using System.Collections.Generic;
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
        /// Was the buffer leased from a pool?
        /// </summary>
        public bool BytesLeasedFromPool;

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
        public UDPPacketBuffer(byte[] buffer, int bufferSize, IPEndPoint destination, int category, bool fromBufferPool)
        {
            Data = new byte[bufferSize];
            this.CopyFrom(buffer, bufferSize);
            DataLength = bufferSize;

            RemoteEndPoint = destination;
            BytesLeasedFromPool = fromBufferPool;
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

    /// <summary>
    /// Object pool for packet buffers. This is used to allocate memory for all
    /// incoming and outgoing packets, and zerocoding buffers for those packets
    /// </summary>
    public class PacketBufferPool : ObjectPoolBase<UDPPacketBuffer>
    {
        private IPEndPoint _endPoint;

        /// <summary>
        /// Initialize the object pool in client mode
        /// </summary>
        /// <param name="endPoint">Server to connect to</param>
        /// <param name="itemsPerSegment"></param>
        /// <param name="minSegments"></param>
        public PacketBufferPool(IPEndPoint endPoint, int itemsPerSegment, int minSegments)
            : base()
        {
            _endPoint = endPoint;
            Initialize(itemsPerSegment, minSegments, true, 1000 * 60 * 5);
        }

        /// <summary>
        /// Initialize the object pool in server mode
        /// </summary>
        /// <param name="itemsPerSegment"></param>
        /// <param name="minSegments"></param>
        public PacketBufferPool(int itemsPerSegment, int minSegments)
        {
            _endPoint = null;
            Initialize(itemsPerSegment, minSegments, true, 1000 * 60 * 5);
        }

        /// <summary>
        /// Returns a packet buffer with EndPoint set if the buffer is in
        /// client mode, or with EndPoint set to null in server mode
        /// </summary>
        /// <returns>Initialized UDPPacketBuffer object</returns>
        protected override UDPPacketBuffer GetObjectInstance()
        {
            return _endPoint != null ? new UDPPacketBuffer(_endPoint) : new UDPPacketBuffer();
        }
    }

    public static class Pool
    {
        public static PacketBufferPool PoolInstance;

        /// <summary>
        /// Default constructor
        /// </summary>
        static Pool()
        {
            PoolInstance = new PacketBufferPool(new IPEndPoint(Settings.BIND_ADDR, 0), 16, 1);
        }

        /// <summary>
        /// Check a packet buffer out of the pool
        /// </summary>
        /// <returns>A packet buffer object</returns>
        public static WrappedObject<UDPPacketBuffer> CheckOut()
        {
            return PoolInstance.CheckOut();
        }
    }
}
