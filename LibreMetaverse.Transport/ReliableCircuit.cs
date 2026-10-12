/*
 * Copyright (c) 2026, Sjofn LLC.
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
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    /// <summary>
    /// A datagram that has been built and is waiting to be sent, or to be acknowledged.
    /// </summary>
    public class OutgoingDatagram
    {
        /// <summary>The datagram as it goes on the wire</summary>
        public readonly UDPPacketBuffer Buffer;
        /// <summary>Sequence number of the datagram, given when it is first sent</summary>
        public uint SequenceNumber;
        /// <summary>Number of times this datagram has been resent</summary>
        public int ResendCount;
        /// <summary>The circuit's tick count when this datagram was last sent over the wire</summary>
        public int TickCount;
        /// <summary>Type of the packet</summary>
        public PacketType Type;
        /// <summary>The length of the datagram as it was first sent, before any acknowledgements were appended to it</summary>
        internal int OriginalLength;

        public OutgoingDatagram(UDPPacketBuffer buffer, PacketType type)
        {
            Buffer = buffer;
            Type = type;
        }
    }

    /// <summary>
    /// The reliable half of a Second Life UDP circuit, with no socket and no client in it: it numbers the datagrams sent,
    /// remembers the reliable ones until they are acknowledged and resends them, acknowledges the reliable ones received
    /// (appended to a packet going out, or in a PacketAck), and does not hand on one it has already had. Either end of a
    /// circuit can use it. It follows the viewer's circuit (indra/llmessage): a packet's sequence number is given when it is
    /// finally sent, at most 250 acknowledgements are appended to a packet and none to a resend, and a resend is the packet
    /// as it was first sent with the resent flag set.
    /// </summary>
    /// <remarks>
    /// The owner provides the socket (the transmit action), may provide the numbering, and decides the timing by calling
    /// <see cref="SendAcks"/> and <see cref="ResendUnacked"/> now and then. Settings are properties to override, read each
    /// time they are used, and what happens is reported through the <c>On...</c> methods.
    /// </remarks>
    public class ReliableCircuit
    {
        /// <summary>The most acknowledgements appended to one packet (the viewer's MAX_ACKS)</summary>
        public const int MaxAppendedAcks = 250;

        private readonly Action<UDPPacketBuffer> _transmit;
        private readonly Func<uint> _nextSequence;
        private readonly ILogContext? _log;
        private int _sequence;
        private volatile uint _oldestUnacked;
        private readonly ConcurrentQueue<uint> _pendingAcks = new ConcurrentQueue<uint>();
        private readonly SortedDictionary<uint, OutgoingDatagram> _needAck = new SortedDictionary<uint, OutgoingDatagram>();

        /// <summary>The other end of the circuit</summary>
        public IPEndPoint RemoteEndPoint { get; }

        /// <summary>The sequence numbers of the reliable packets received lately</summary>
        public IncomingPacketIDCollection PacketArchive { get; }

        /// <summary>
        /// The lowest sequence number sent that has not been acknowledged, or 0 if there is none
        /// </summary>
        public uint OldestUnacked => _oldestUnacked;

        /// <summary>The number of reliable packets sent that have not been acknowledged</summary>
        public int UnackedCount { get { lock (_needAck) return _needAck.Count; } }

        /// <summary>The sequence numbers of the reliable packets sent that have not been acknowledged, lowest first</summary>
        public IReadOnlyList<uint> GetUnackedSequences()
        {
            lock (_needAck) return _needAck.Keys.ToList();
        }

        /// <summary>The number of acknowledgements waiting to be sent</summary>
        public int PendingAckCount => _pendingAcks.Count;

        /// <summary>Milliseconds a packet waits for its acknowledgement before it is resent</summary>
        protected virtual int ResendTimeout => 4000;
        /// <summary>How many times a packet is resent before it is given up on</summary>
        protected virtual int MaxResendCount => 3;
        /// <summary>How many acknowledgements wait before they are sent without waiting for the next <see cref="SendAcks"/></summary>
        protected virtual int MaxPendingAcks => 10;
        /// <summary>Whether each resend is logged</summary>
        protected virtual bool LogResends => false;
        /// <summary>The clock the resend timeout is measured by, in milliseconds. It is 0 only for a packet that has not been sent.</summary>
        protected virtual int TickCount => Environment.TickCount;

        /// <summary>Makes the record of a packet to send; an owner that keeps more with each packet can return a subclass</summary>
        protected virtual OutgoingDatagram CreateDatagram(UDPPacketBuffer buffer, PacketType type) => new OutgoingDatagram(buffer, type);
        /// <summary>
        /// Called with a datagram before it is sent. Return true to take it (to queue it, or to throttle it), and send it
        /// later with <see cref="SendFinal"/>; false to have it sent now. PacketAck and LogoutRequest are never offered.
        /// </summary>
        protected virtual bool TryQueue(OutgoingDatagram datagram) => false;
        /// <summary>Called after a packet has been sent or queued, with the length of the datagram</summary>
        protected virtual void OnPacketPrepared(PacketType type, int dataLength) { }
        /// <summary>Called with each datagram that decodes, before anything is done with it</summary>
        protected virtual void OnDatagramReceived(int bytes, Packet packet) { }
        /// <summary>Called when a reliable packet is resent</summary>
        protected virtual void OnResent(OutgoingDatagram datagram) { }
        /// <summary>Called when a reliable packet has been resent as often as it is allowed to be and is dropped</summary>
        protected virtual void OnResendGivenUp(OutgoingDatagram datagram) { }
        /// <summary>Called when the other end acknowledges a reliable packet that was waiting</summary>
        protected virtual void OnAcknowledged(OutgoingDatagram datagram) { }

        /// <param name="remote">The other end, the address datagrams are sent to</param>
        /// <param name="transmit">Puts a datagram on the wire</param>
        /// <param name="nextSequence">Gives the next sequence number; the circuit counts from 1 itself by default</param>
        /// <param name="archiveSize">How many sequence numbers of packets received are kept for noticing duplicates (one fewer are held)</param>
        /// <param name="log">What log entries are tagged with</param>
        public ReliableCircuit(IPEndPoint remote, Action<UDPPacketBuffer> transmit, Func<uint>? nextSequence = null, int archiveSize = 1000, ILogContext? log = null)
        {
            RemoteEndPoint = remote;
            _transmit = transmit;
            _nextSequence = nextSequence ?? (() => (uint)Interlocked.Increment(ref _sequence));
            _log = log;
            PacketArchive = new IncomingPacketIDCollection(archiveSize);
        }

        #region Sending

        /// <summary>Serializes a packet, in as many datagrams as it takes, and sends it</summary>
        public void SendPacket(Packet packet)
        {
            if (packet.Header.AppendedAcks || (packet.Header.AckList != null && packet.Header.AckList.Length > 0))
                Logger.Error("Attempting to send packet " + packet.Type + " with ACKs appended before serialization", _log);

            if (packet.HasVariableBlocks)
            {
                byte[][] datas;
                try { datas = packet.ToBytesMultiple(); }
                catch (NullReferenceException)
                {
                    Logger.Error("Failed to serialize " + packet.Type + " packet to one or more payloads due to a missing block or field. StackTrace: " +
                        Environment.StackTrace, _log);
                    return;
                }
                int packetCount = datas.Length;

                if (packetCount > 1)
                    Logger.Debug("Split " + packet.Type + " packet into " + packetCount + " packets", _log);

                for (int i = 0; i < packetCount; i++)
                {
                    byte[] data = datas[i];
                    SendPacketData(data, data.Length, packet.Type, packet.Header.Zerocoded);
                }
            }
            else if (packet.UsesBufferPooling)
            {
                int size = 0;
                byte[] data = packet.ToBytes(ArrayPoolByteBufferPool.Shared, ref size);
                try
                {
                    SendPacketData(data, size, packet.Type, packet.Header.Zerocoded);
                }
                finally
                {
                    ArrayPoolByteBufferPool.Shared.ReturnBytes(data);
                }
            }
            else
            {
                byte[] data = packet.ToBytes();
                SendPacketData(data, data.Length, packet.Type, packet.Header.Zerocoded);
            }
        }

        /// <summary>
        /// Sends a serialized packet, zero-coding it if asked to. A packet that grows past the MTU when it is
        /// zero-coded is sent as it is, with the zero-coded flag taken off.
        /// </summary>
        public void SendPacketData(byte[] data, int dataLength, PacketType type, bool doZerocode)
        {
            UDPPacketBuffer buffer = new UDPPacketBuffer(RemoteEndPoint, Packet.MTU);

            if (doZerocode)
            {
                try
                {
                    dataLength = Helpers.ZeroEncode(data, dataLength, buffer.Data);
                }
                catch (IndexOutOfRangeException)
                {
                    // The packet grew larger than Packet.MTU bytes while zerocoding.
                    // Remove the MSG_ZEROCODED flag and send the unencoded data
                    // instead
                    data[0] = (byte)(data[0] & ~Helpers.MSG_ZEROCODED);
                    buffer.CopyFrom(data, dataLength);
                }
            }
            else
            {
                buffer.CopyFrom(data, dataLength);
            }
            buffer.DataLength = dataLength;

            OutgoingDatagram datagram = CreateDatagram(buffer, type);

            // Send ACK and logout packets directly, everything else may be queued
            if (type == PacketType.PacketAck || type == PacketType.LogoutRequest || !TryQueue(datagram))
            {
                SendFinal(datagram);
            }

            OnPacketPrepared(type, dataLength);
        }

        /// <summary>
        /// Puts a datagram on the wire: appends the acknowledgements waiting (to a first send, not to a resend), gives it
        /// its sequence number (not a resend), remembers it if it is reliable, and transmits it.
        /// </summary>
        public void SendFinal(OutgoingDatagram datagram)
        {
            UDPPacketBuffer buffer = datagram.Buffer;
            byte flags = buffer.Data[0];
            bool isResend = (flags & Helpers.MSG_RESENT) != 0;
            bool isReliable = (flags & Helpers.MSG_RELIABLE) != 0;

            // Keep track of when this packet was sent out (right now)
            datagram.TickCount = TickCount;

            int dataLength = buffer.DataLength;

            if (isResend)
            {
                // As the viewer does, send the packet again as it was first sent: without the acks that were
                // appended to it then, and without any that are waiting now.
                if (datagram.OriginalLength > 0)
                    dataLength = datagram.OriginalLength;
                buffer.Data[0] = (byte)(buffer.Data[0] & ~Helpers.MSG_APPENDED_ACKS);
            }
            else
            {
                datagram.OriginalLength = dataLength;

                // Keep appending ACKs until there is no room left in the packet or there are no more ACKs to
                // append. The viewer appends at most 250 (MAX_ACKS in message.cpp, below the 255 that the count
                // byte after them could hold).
                uint ackCount = 0;
                uint ack;
                while (ackCount < MaxAppendedAcks && dataLength + 5 < Packet.MTU && _pendingAcks.TryDequeue(out ack))
                {
                    Utils.UIntToBytesBig(ack, buffer.Data, dataLength);
                    dataLength += 4;
                    ++ackCount;
                }

                if (ackCount > 0)
                {
                    // Set the last byte of the packet equal to the number of appended ACKs
                    buffer.Data[dataLength++] = (byte)ackCount;
                    // Set the appended ACKs flag on this packet
                    buffer.Data[0] |= Helpers.MSG_APPENDED_ACKS;
                }
            }

            buffer.DataLength = dataLength;

            if (!isResend)
            {
                // Not a resend, assign a new sequence number
                uint sequenceNumber = _nextSequence();
                Utils.UIntToBytesBig(sequenceNumber, buffer.Data, 1);
                datagram.SequenceNumber = sequenceNumber;

                if (isReliable)
                {
                    // Add this packet to the list of ACK responses we are waiting on from the other end
                    lock (_needAck)
                    {
                        _needAck[sequenceNumber] = datagram;
                        if (_needAck.Count == 1)
                            _oldestUnacked = sequenceNumber;
                    }
                }
            }

            // Put the UDP payload on the wire
            _transmit(buffer);
        }

        /// <summary>
        /// Sends the acknowledgements that are waiting, in as many PacketAck packets as it takes
        /// </summary>
        /// <returns>Number of ACKs sent</returns>
        public int SendAcks()
        {
            int ackCount = 0;

            if (_pendingAcks.TryDequeue(out var ack))
            {
                List<PacketAckPacket.PacketsBlock> blocks = new List<PacketAckPacket.PacketsBlock>();
                PacketAckPacket.PacketsBlock block = new PacketAckPacket.PacketsBlock { ID = ack };
                blocks.Add(block);

                while (_pendingAcks.TryDequeue(out ack))
                {
                    block = new PacketAckPacket.PacketsBlock { ID = ack };
                    blocks.Add(block);
                }

                PacketAckPacket packet = new PacketAckPacket
                {
                    Header = { Reliable = false },
                    Packets = blocks.ToArray()
                };

                ackCount = blocks.Count;
                SendPacket(packet);
            }

            return ackCount;
        }

        /// <summary>
        /// Resends the reliable packets that have waited too long for their acknowledgement, and gives up on the ones
        /// that have been resent as often as they may be
        /// </summary>
        public void ResendUnacked()
        {
            OutgoingDatagram[] array;

            lock (_needAck)
            {
                if (_needAck.Count <= 0) return;

                // Create a temporary copy of the outgoing packets array to iterate over
                array = new OutgoingDatagram[_needAck.Count];
                _needAck.Values.CopyTo(array, 0);
            }

            int now = TickCount;

            // Resend packets
            foreach (OutgoingDatagram outgoing in array)
            {
                if (outgoing.TickCount == 0 || now - outgoing.TickCount <= ResendTimeout) continue;

                if (outgoing.ResendCount < MaxResendCount)
                {
                    if (LogResends)
                    {
                        Logger.Debug($"Resending {outgoing.Type} packet #{outgoing.SequenceNumber}, {now - outgoing.TickCount}ms have passed", _log);
                    }

                    // The TickCount will be set to the current time when the packet
                    // is actually sent out again
                    outgoing.TickCount = 0;

                    // Set the resent flag
                    outgoing.Buffer.Data[0] = (byte)(outgoing.Buffer.Data[0] | Helpers.MSG_RESENT);

                    Interlocked.Increment(ref outgoing.ResendCount);
                    OnResent(outgoing);

                    SendFinal(outgoing);
                }
                else
                {
                    Logger.Debug($"Dropping packet #{outgoing.SequenceNumber} after {outgoing.ResendCount} failed attempts", _log);

                    lock (_needAck) RemoveUnacked(outgoing.SequenceNumber);
                    OnResendGivenUp(outgoing);
                }
            }
        }

        #endregion Sending

        #region Receiving

        /// <summary>
        /// Decodes a datagram, takes the acknowledgements out of it, and queues an acknowledgement for it if it is
        /// reliable. Returns the packet to act on, or null if it is malformed or one that was already received.
        /// </summary>
        public Packet? Receive(UDPPacketBuffer buffer)
        {
            Packet? packet = null;

            int packetEnd = buffer.DataLength - 1;

            bool isZerocoded = (buffer.Data[0] & Helpers.MSG_ZEROCODED) != 0;
            byte[]? zeroBuffer = isZerocoded ? ArrayPool<byte>.Shared.Rent(8192) : null;
            try
            {
                packet = Packet.BuildPacket(buffer.Data, ref packetEnd, zeroBuffer);
            }
            catch (MalformedDataException ex)
            {
                // Anyone who can send us UDP can cause this, so keep what is logged for each one short
                int dumpLen = Math.Min(buffer.DataLength, 64);
                Logger.Error($"Malformed data, cannot parse {buffer.DataLength} byte packet: {ex.Message}\n" +
                             $"First {dumpLen} bytes: {Utils.BytesToHexString(buffer.Data, dumpLen, null)}", _log);
            }
            finally
            {
                if (zeroBuffer != null) ArrayPool<byte>.Shared.Return(zeroBuffer);
            }

            // Fail-safe check
            if (packet == null)
            {
                Logger.Warn("Couldn't build a message from the incoming data", _log);
                return null;
            }

            OnDatagramReceived(buffer.DataLength, packet);

            #region ACK Receiving

            // Handle appended ACKs
            if (packet.Header.AppendedAcks && packet.Header.AckList != null)
            {
                lock (_needAck)
                {
                    foreach (var t in packet.Header.AckList)
                        Acknowledge(t);
                }
            }

            // Handle PacketAck packets
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                lock (_needAck)
                {
                    foreach (var t in ackPacket.Packets)
                        Acknowledge(t.ID);
                }
            }

            #endregion ACK Receiving

            if (packet.Header.Reliable)
            {
                #region ACK Sending

                // Add this packet to the list of ACKs that need to be sent out
                _pendingAcks.Enqueue(packet.Header.Sequence);

                // Send out ACKs if we have a lot of them
                if (_pendingAcks.Count >= MaxPendingAcks)
                    SendAcks();

                #endregion ACK Sending

                // Check the archive of received packet IDs to see whether we already received this packet
                if (!PacketArchive.TryEnqueue(packet.Header.Sequence))
                {
                    if (packet.Header.Resent)
                        Logger.Debug($"Received a resend of already processed packet #{packet.Header.Sequence}, type: {packet.Type}", _log);
                    else
                        Logger.Warn($"Received a duplicate (not marked as resend) of packet #{packet.Header.Sequence}, type: {packet.Type}", _log);

                    // Avoid acting twice on the same packet
                    return null;
                }
            }

            return packet;
        }

        /// <summary>Must be called while holding the lock on the table</summary>
        private void Acknowledge(uint sequence)
        {
            if (_needAck.TryGetValue(sequence, out var datagram))
                OnAcknowledged(datagram);
            RemoveUnacked(sequence);
        }

        /// <summary>
        /// Remove a sequence number from the unacknowledged ones and update the cached oldest-unacked value.
        /// Must be called while holding the lock on the table.
        /// </summary>
        private void RemoveUnacked(uint sequenceNumber)
        {
            _needAck.Remove(sequenceNumber);
            if (_needAck.Count == 0)
            {
                _oldestUnacked = 0;
            }
            else if (sequenceNumber == _oldestUnacked)
            {
                // The oldest packet was just acked; find the new oldest by reading the first key.
                using var en = _needAck.Keys.GetEnumerator();
                en.MoveNext();
                _oldestUnacked = en.Current;
            }
        }

        #endregion Receiving
    }
}
