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
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// A seed capability that cannot be reached used to make the handler of the failure call itself until the stack was
    /// full, which ends the process. It is now asked for again after a pause, a few times, and not after the system is disconnected.
    /// </summary>
    [TestFixture]
    [Category("Caps")]
    public class CapsSeedRetryTests
    {
        private static readonly FieldInfo Attempts = typeof(Caps).GetField("_seedAttempts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo Connected = typeof(NetworkManager).GetField("<Connected>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static (GridClient client, Caps caps) ConnectedCapsToNowhere()
        {
            var client = new GridClient();
            Connected.SetValue(client.Network, true);
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);
            var caps = new Caps(sim, new Uri("http://127.0.0.1:9/seed"));   // nothing listens on the discard port
            return (client, caps);
        }

        private static int AttemptsOf(Caps caps) => (int)Attempts.GetValue(caps)!;

        [Test]
        public void ASeedCapabilityThatCannotBeReached_IsRetriedAfterAPause_NotRecursively()
        {
            var (_, caps) = ConnectedCapsToNowhere();
            try
            {
                Assert.That(SpinWait.SpinUntil(() => AttemptsOf(caps) >= 1, 10000), Is.True, "the failure was noticed, and the process is still here");
            }
            finally { caps.Disconnect(true); }
        }

        [Test]
        public async Task OnceDisconnected_TheSeedIsNotAskedForAgain()
        {
            var (_, caps) = ConnectedCapsToNowhere();
            Assert.That(SpinWait.SpinUntil(() => AttemptsOf(caps) >= 1, 10000), Is.True);

            caps.Disconnect(true);
            int attempts = AttemptsOf(caps);
            await Task.Delay(2500);

            Assert.That(AttemptsOf(caps), Is.EqualTo(attempts));
        }
    }
}
