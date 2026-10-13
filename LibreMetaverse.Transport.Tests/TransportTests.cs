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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The UDP transport on its own: the socket loop, the datagram buffer, the throttle and the record of received packet
    /// numbers. Nothing here refers to the LibreMetaverse client.
    /// </summary>
    [TestFixture]
    [Category("Transport")]
    public class TransportTests
    {
        #region The socket loop

        private sealed class TestUdp : UDPBase
        {
            public readonly BlockingCollection<(byte[] Data, IPEndPoint From)> Received = new BlockingCollection<(byte[], IPEndPoint)>();
            public int Dropped;
            public int Capacity = 512;
            public IPAddress Bind = IPAddress.Loopback;
            public ManualResetEventSlim Gate = new ManualResetEventSlim(true);
            public Action<byte[]> OnReceive = _ => { };

            public TestUdp(IPEndPoint remote) : base(remote) { }

            protected override IPAddress BindAddress => Bind;
            protected override int ReceiveQueueCapacity => Capacity;

            protected override void PacketReceived(UDPPacketBuffer buffer)
            {
                Gate.Wait();
                // The buffer goes back to the pool after this returns, so what is kept is a copy
                var data = buffer.Data.Take(buffer.DataLength).ToArray();
                OnReceive(data);
                Received.Add((data, (IPEndPoint)buffer.RemoteEndPoint));
            }

            protected override void PacketSent(UDPPacketBuffer buffer, int bytesSent) { }
            protected override void OnPacketDropped() => Interlocked.Increment(ref Dropped);

            public void Send(byte[] data) => AsyncBeginSend(new UDPPacketBuffer(remoteEndPoint!, data) { DataLength = data.Length });
        }

        private sealed class FarEnd : IDisposable
        {
            public readonly UdpClient Socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            public IPEndPoint EndPoint => (IPEndPoint)Socket.Client.LocalEndPoint!;
            public (byte[] Data, IPEndPoint From)? Read(int timeoutMs = 1000)
            {
                if (!Socket.Client.Poll(timeoutMs * 1000, SelectMode.SelectRead)) return null;
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                var buffer = new byte[2048];
                int n = Socket.Client.ReceiveFrom(buffer, ref from);
                return (buffer.Take(n).ToArray(), (IPEndPoint)from);
            }
            public void Dispose() => Socket.Dispose();
        }

        [Test]
        public void ADatagramSent_ReachesTheFarEnd_AndTheReplyComesBack()
        {
            using var far = new FarEnd();
            var udp = new TestUdp(far.EndPoint);
            udp.Start();
            try
            {
                udp.Send(new byte[] { 1, 2, 3 });
                var seen = far.Read()!.Value;
                far.Socket.Send(new byte[] { 9, 8 }, 2, seen.From);

                Assert.That(udp.Received.TryTake(out var reply, 2000), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(seen.Data, Is.EqualTo(new byte[] { 1, 2, 3 }));
                    Assert.That(reply.Data, Is.EqualTo(new byte[] { 9, 8 }));
                    Assert.That(reply.From.Address, Is.EqualTo(IPAddress.Loopback));
                    Assert.That(reply.From.Port, Is.EqualTo(far.EndPoint.Port));
                });
            }
            finally { udp.Stop(); }
        }

        [Test]
        public void TheSocketIsBoundToTheAddressTheDerivedClassGives()
        {
            using var far = new FarEnd();
            var udp = new TestUdp(far.EndPoint) { Bind = IPAddress.Loopback };
            udp.Start();
            try
            {
                udp.Send(new byte[] { 1 });

                Assert.That(far.Read()!.Value.From.Address, Is.EqualTo(IPAddress.Loopback));
            }
            finally { udp.Stop(); }
        }

        [Test]
        public void ABindAddressThatIsNotOnThisMachine_FailsToStart()
        {
            // Proves the address from the derived class is the one used: 192.0.2.1 is reserved for documentation
            using var far = new FarEnd();
            var udp = new TestUdp(far.EndPoint) { Bind = IPAddress.Parse("192.0.2.1") };

            Assert.Throws<SocketException>(() => udp.Start());
            Assert.That(udp.IsRunning, Is.False);
        }

        [Test]
        public void TheDefaultBindAddress_IsEveryInterface_AndTheDefaultQueueIs512()
        {
            using var far = new FarEnd();
            var udp = new DefaultsUdp(far.EndPoint);

            Assert.Multiple(() =>
            {
                Assert.That(udp.Address, Is.EqualTo(IPAddress.Any));
                Assert.That(udp.Capacity, Is.EqualTo(512));
            });
        }

        private sealed class DefaultsUdp : UDPBase
        {
            public DefaultsUdp(IPEndPoint remote) : base(remote) { }
            public IPAddress Address => BindAddress;
            public int Capacity => ReceiveQueueCapacity;
            protected override void PacketReceived(UDPPacketBuffer buffer) { }
            protected override void PacketSent(UDPPacketBuffer buffer, int bytesSent) { }
        }

        [Test]
        public void StartAndStop_AreIdempotent_AndSendingAfterStopDoesNothing()
        {
            using var far = new FarEnd();
            var udp = new TestUdp(far.EndPoint);

            Assert.That(udp.IsRunning, Is.False);
            udp.Start();
            udp.Start();
            Assert.That(udp.IsRunning, Is.True);
            udp.Stop();
            udp.Stop();
            Assert.That(udp.IsRunning, Is.False);

            Assert.DoesNotThrow(() => udp.Send(new byte[] { 1 }));
            Assert.That(far.Read(100), Is.Null);
        }

        [Test]
        public void AnExceptionInPacketReceived_DoesNotStopTheLoop()
        {
            using var far = new FarEnd();
            int calls = 0;
            var udp = new TestUdp(far.EndPoint) { OnReceive = _ => { if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("first one fails"); } };
            udp.Start();
            try
            {
                udp.Send(new byte[] { 1 });
                var from = far.Read()!.Value.From;
                far.Socket.Send(new byte[] { 1 }, 1, from);
                far.Socket.Send(new byte[] { 2 }, 1, from);

                Assert.That(udp.Received.TryTake(out var second, 2000), Is.True);
                Assert.That(second.Data, Is.EqualTo(new byte[] { 2 }));
            }
            finally { udp.Stop(); }
        }

        [Test]
        public void WhenTheReceiveQueueIsFull_TheNewestDatagramsAreDropped_AndTheOwnerIsToldSoWithoutBlockingTheSocket()
        {
            using var far = new FarEnd();
            var udp = new TestUdp(far.EndPoint) { Capacity = 2 };
            udp.Gate.Reset();
            udp.Start();
            try
            {
                udp.Send(new byte[] { 0 });
                var from = far.Read()!.Value.From;
                for (int i = 0; i < 100; i++) far.Socket.Send(new byte[] { (byte)i }, 1, from);

                SpinWait.SpinUntil(() => Volatile.Read(ref udp.Dropped) > 0, 3000);
                int dropped = Volatile.Read(ref udp.Dropped);
                udp.Gate.Set();
                SpinWait.SpinUntil(() => udp.Received.Count >= 1, 2000);

                Assert.Multiple(() =>
                {
                    Assert.That(dropped, Is.GreaterThan(0));
                    Assert.That(udp.Received.Count, Is.GreaterThan(0));
                    Assert.That(udp.Received.Count + dropped, Is.LessThanOrEqualTo(100));
                });
            }
            finally { udp.Gate.Set(); udp.Stop(); }
        }

        #endregion The socket loop

        #region The datagram buffer

        [Test]
        public void ADatagramBuffer_HoldsADefaultSizeOrTheSizeGiven()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 1234);

            Assert.Multiple(() =>
            {
                Assert.That(new UDPPacketBuffer().Data, Has.Length.EqualTo(UDPPacketBuffer.DEFAULT_BUFFER_SIZE));
                Assert.That(new UDPPacketBuffer(endpoint).Data, Has.Length.EqualTo(UDPPacketBuffer.DEFAULT_BUFFER_SIZE));
                Assert.That(new UDPPacketBuffer(endpoint, 1200).Data, Has.Length.EqualTo(1200));
                Assert.That(new UDPPacketBuffer(endpoint).RemoteEndPoint, Is.EqualTo(endpoint));
            });
        }

        [Test]
        public void ABufferWithNoEndpointYet_AwaitsOneOnAnyAddress_AndCanBeResetToThat()
        {
            var buffer = new UDPPacketBuffer();
            Assert.That(buffer.RemoteEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Any, 0)));

            buffer.RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 5);
            buffer.ResetEndpoint();

            Assert.That(buffer.RemoteEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Any, 0)));
        }

        [Test]
        public void CopyFrom_FillsTheBufferFromTheStart()
        {
            var buffer = new UDPPacketBuffer(new IPEndPoint(IPAddress.Loopback, 1), 16);

            buffer.CopyFrom(new byte[] { 1, 2, 3, 4, 5 }, 3);
            buffer.CopyFrom(new byte[] { 9, 9 });

            Assert.That(buffer.Data.Take(4), Is.EqualTo(new byte[] { 9, 9, 3, 0 }));
        }

        [Test]
        public void ABufferMadeFromBytes_IsACopyOfThemOnTheDestination()
        {
            var source = new byte[] { 5, 6, 7, 8 };
            var destination = new IPEndPoint(IPAddress.Loopback, 77);

            var buffer = new UDPPacketBuffer(source, 4, destination, 0);
            source[0] = 99;

            Assert.Multiple(() =>
            {
                Assert.That(buffer.Data.Take(4), Is.EqualTo(new byte[] { 5, 6, 7, 8 }));
                Assert.That(buffer.DataLength, Is.EqualTo(4));
                Assert.That(buffer.RemoteEndPoint, Is.EqualTo(destination));
            });
        }

        #endregion The datagram buffer

        #region The rates

        [Test]
        public void EachRate_IsKeptInsideTheRangeTheSimulatorAccepts()
        {
            var rates = new ThrottleRates();
            (Func<float> get, Action<float> set, float min, float max)[] cases =
            {
                (() => rates.Resend, v => rates.Resend = v, 10000f, 150000f),
                (() => rates.Land, v => rates.Land = v, 0f, 170000f),
                (() => rates.Wind, v => rates.Wind = v, 0f, 34000f),
                (() => rates.Cloud, v => rates.Cloud = v, 0f, 34000f),
                (() => rates.Task, v => rates.Task = v, 4000f, 446000f * 3),
                (() => rates.Texture, v => rates.Texture = v, 4000f, 446000f),
                (() => rates.Asset, v => rates.Asset = v, 10000f, 220000f),
            };

            Assert.Multiple(() =>
            {
                foreach (var (get, set, min, max) in cases)
                {
                    set(-5f); Assert.That(get(), Is.EqualTo(min));
                    set(1e9f); Assert.That(get(), Is.EqualTo(max));
                    set((min + max) / 2f); Assert.That(get(), Is.EqualTo((min + max) / 2f));
                }
            });
        }

        [Test]
        public void TheTotal_IsSpreadOverTheRates_AndThenClampedByEach()
        {
            var rates = new ThrottleRates { Total = 1536000f };

            Assert.Multiple(() =>
            {
                Assert.That(rates.Resend, Is.EqualTo(150000f), "153600 clamped");
                Assert.That(rates.Land, Is.EqualTo(170000f), "266240 clamped");
                Assert.That(rates.Wind, Is.EqualTo(34000f), "76800 clamped");
                Assert.That(rates.Cloud, Is.EqualTo(34000f));
                Assert.That(rates.Task, Is.EqualTo(360448f).Within(0.5f));
                Assert.That(rates.Texture, Is.EqualTo(360448f).Within(0.5f));
                Assert.That(rates.Asset, Is.EqualTo(220000f), "247808 clamped");
                Assert.That(rates.Total, Is.EqualTo(rates.Resend + rates.Land + rates.Wind + rates.Cloud + rates.Task + rates.Texture + rates.Asset));
            });
        }

        [Test]
        public void TheRatesAreSevenLittleEndianFloats_InTheOrderOfAnAgentThrottlePacket()
        {
            var rates = new ThrottleRates { Resend = 20000f, Land = 1000f, Wind = 2000f, Cloud = 3000f, Task = 50000f, Texture = 60000f, Asset = 70000f };

            var bytes = rates.ToBytes();
            var back = new ThrottleRates(bytes, 0);
            var offset = new byte[] { 0xAA, 0xBB }.Concat(bytes).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(bytes, Has.Length.EqualTo(28));
                Assert.That(BitConverter.ToSingle(bytes.Select((b, i) => BitConverter.IsLittleEndian ? b : bytes[i]).Take(4).ToArray(), 0), Is.EqualTo(20000f));
                Assert.That(new[] { back.Resend, back.Land, back.Wind, back.Cloud, back.Task, back.Texture, back.Asset },
                    Is.EqualTo(new[] { 20000f, 1000f, 2000f, 3000f, 50000f, 60000f, 70000f }));
                Assert.That(new ThrottleRates(offset, 2).Asset, Is.EqualTo(70000f), "from an offset");
            });
        }

        [Test]
        public void RatesDecodedFromABadPacket_AreClampedToo()
        {
            var bytes = new ThrottleRates().ToBytes();
            BitConverter.GetBytes(float.MaxValue).CopyTo(bytes, 16);

            Assert.That(new ThrottleRates(bytes, 0).Task, Is.EqualTo(446000f * 3));
        }

        #endregion The rates

        #region The throttle

        [TestCase(PacketType.UseCircuitCode, "Unthrottled")]
        [TestCase(PacketType.CompleteAgentMovement, "Unthrottled")]
        [TestCase(PacketType.AgentThrottle, "Unthrottled")]
        [TestCase(PacketType.LogoutRequest, "Unthrottled")]
        [TestCase(PacketType.PacketAck, "Unthrottled")]
        [TestCase(PacketType.StartPingCheck, "Unthrottled")]
        [TestCase(PacketType.CompletePingCheck, "Unthrottled")]
        [TestCase(PacketType.CloseCircuit, "Unthrottled")]
        [TestCase(PacketType.RequestImage, "Texture")]
        [TestCase(PacketType.TransferRequest, "Asset")]
        [TestCase(PacketType.AbortXfer, "Asset")]
        [TestCase(PacketType.ChatFromViewer, "Task")]
        [TestCase(PacketType.AgentUpdate, "Task")]
        public void ThePacketsAClientSends_AreSortedIntoTheTrafficTheyBelongTo(PacketType type, string expected)
        {
            Assert.That(UdpThrottle.Classify(type).ToString(), Is.EqualTo(expected));
        }

        private static ThrottleRates Slow() => new ThrottleRates { Task = 4000f, Texture = 4000f, Asset = 10000f };

        [Test]
        public async Task UnthrottledTraffic_NeverWaits()
        {
            using var throttle = new UdpThrottle(Slow());
            var watch = Stopwatch.StartNew();

            for (int i = 0; i < 50; i++) await throttle.AcquireAsync(UdpThrottleCategory.Unthrottled, 100000, CancellationToken.None);

            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500));
        }

        [Test]
        public async Task ABucketAllowsABurstAndThenMakesTrafficWait()
        {
            // 4000 bits a second is 200 bytes a tenth of a second at the least, and four tenths can be saved up. The time is
            // counted from before the throttle was made: a pause only lengthens it, and the bucket cannot refill sooner
            var watch = Stopwatch.StartNew();
            using var throttle = new UdpThrottle(Slow());
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 800, CancellationToken.None);

            await throttle.AcquireAsync(UdpThrottleCategory.Task, 200, CancellationToken.None);

            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(80).And.LessThan(3000));
        }

        [Test]
        public async Task ABucketSavesUpFourTenthsOfASecond()
        {
            // 200 bytes a tenth of a second, so 800 can be taken at once and no more
            var watch = Stopwatch.StartNew();
            using var throttle = new UdpThrottle(Slow());

            for (int i = 0; i < 4; i++) await throttle.AcquireAsync(UdpThrottleCategory.Task, 200, CancellationToken.None);
            long burst = watch.ElapsedMilliseconds;
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 200, CancellationToken.None);

            // Without the saved-up burst, four takes of 200 bytes would cost three refills (300 ms or more)
            Assert.That(burst, Is.LessThan(250), "the four were taken at once");
            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(80), "the fifth waited for the bucket to refill");
        }

        [Test]
        public async Task EachKindOfTrafficHasABucketOfItsOwn()
        {
            using var throttle = new UdpThrottle(Slow());
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 800, CancellationToken.None);

            var watch = Stopwatch.StartNew();
            await throttle.AcquireAsync(UdpThrottleCategory.Texture, 800, CancellationToken.None);

            // An empty bucket would make it wait for four refills, 400 ms
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(250), "the texture bucket was full");
        }

        [Test]
        public async Task ADatagramBiggerThanTheBurst_IsLetThroughOnceTheBucketIsFull()
        {
            using var throttle = new UdpThrottle(Slow());
            var watch = Stopwatch.StartNew();

            await throttle.AcquireAsync(UdpThrottleCategory.Task, 1_000_000, CancellationToken.None);

            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500));
        }

        [Test]
        public async Task NewRates_ReplaceTheBuckets()
        {
            using var throttle = new UdpThrottle(Slow());
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 800, CancellationToken.None);

            throttle.Update(new ThrottleRates { Task = 446000f * 3 });
            var watch = Stopwatch.StartNew();
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 20000, CancellationToken.None);

            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500), "a fresh, much bigger bucket");
        }

        [Test]
        public async Task ACancelledWait_Throws()
        {
            using var throttle = new UdpThrottle(Slow());
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 800, CancellationToken.None);
            using var cts = new CancellationTokenSource(50);

            Assert.That(async () => await throttle.AcquireAsync(UdpThrottleCategory.Task, 800, cts.Token), Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task ADisposedThrottle_LetsEverythingThrough_AndCanBeDisposedAgain()
        {
            var throttle = new UdpThrottle(Slow());
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 800, CancellationToken.None);
            throttle.Dispose();

            var watch = Stopwatch.StartNew();
            await throttle.AcquireAsync(UdpThrottleCategory.Task, 800, CancellationToken.None);

            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(250), "a disposed throttle does not wait");
            Assert.DoesNotThrow(() => throttle.Dispose());
        }

        #endregion The throttle

        #region The archive of received sequences

        [Test]
        public void TheArchive_AcceptsASequenceOnce()
        {
            var archive = new IncomingPacketIDCollection(10);

            Assert.Multiple(() =>
            {
                Assert.That(archive.TryEnqueue(5), Is.True);
                Assert.That(archive.TryEnqueue(5), Is.False);
                Assert.That(archive.TryEnqueue(6), Is.True);
            });
        }

        [Test]
        public void TheArchiveHoldsOneFewerThanItsCapacity_ForgettingTheOldestFirst()
        {
            var archive = new IncomingPacketIDCollection(4);
            archive.TryEnqueue(1);
            archive.TryEnqueue(2);
            archive.TryEnqueue(3);
            Assert.That(archive.TryEnqueue(2), Is.False, "three fit");

            archive.TryEnqueue(4);

            Assert.Multiple(() =>
            {
                Assert.That(archive.TryEnqueue(2), Is.False, "2 is still held");
                Assert.That(archive.TryEnqueue(1), Is.True, "1 was forgotten when the fourth went in");
            });
        }

        [Test]
        public void TheArchive_CanBeUsedFromSeveralThreads()
        {
            var archive = new IncomingPacketIDCollection(100000);
            int accepted = 0;

            Parallel.For(0, 8, _ =>
            {
                for (uint i = 0; i < 5000; i++)
                    if (archive.TryEnqueue(i)) Interlocked.Increment(ref accepted);
            });

            Assert.That(accepted, Is.EqualTo(5000), "each sequence was accepted by exactly one thread");
        }

        #endregion The archive of received sequences
    }
}
