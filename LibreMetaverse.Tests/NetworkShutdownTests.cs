/*
 * Copyright (c) 2021-2026, Sjofn LLC
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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// A shutdown does not wait for a simulator which is stuck disconnecting, so that Disconnected is still raised
    /// </summary>
    [TestFixture]
    public class NetworkShutdownTests
    {
        [Test]
        public async Task RunBoundedAsync_ActionsWhichFinishAreReportedAsFinished()
        {
            var ran = 0;
            var finished = await NetworkManager.RunBoundedAsync(
                new Action[] { () => Interlocked.Increment(ref ran), () => Interlocked.Increment(ref ran) },
                TimeSpan.FromSeconds(5));

            Assert.That(finished, Is.EqualTo(new[] { true, true }));
            Assert.That(ran, Is.EqualTo(2));
        }

        [Test]
        public async Task RunBoundedAsync_AStuckActionDoesNotHoldBackTheOthersOrTheCaller()
        {
            using var release = new ManualResetEventSlim(false);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var finished = await NetworkManager.RunBoundedAsync(
                    new Action[] { () => release.Wait(), () => { } },
                    TimeSpan.FromMilliseconds(200));

                Assert.That(finished, Is.EqualTo(new[] { false, true }));
                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                release.Set();
            }
        }

        [Test]
        public async Task RunBoundedAsync_AnActionWhichThrowsCountsAsFinished()
        {
            var finished = await NetworkManager.RunBoundedAsync(
                new Action[] { () => throw new InvalidOperationException("boom") },
                TimeSpan.FromSeconds(5));

            Assert.That(finished, Is.EqualTo(new[] { true }));
        }

        [Test]
        public async Task RunBoundedAsync_NoActionsFinishAtOnce()
        {
            var finished = await NetworkManager.RunBoundedAsync(Array.Empty<Action>(), TimeSpan.FromSeconds(5));

            Assert.That(finished, Is.Empty);
        }
    }
}
