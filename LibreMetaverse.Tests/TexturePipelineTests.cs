/*
 * Copyright (c) 2026, Sjofn LLC
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using LibreMetaverse.Assets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for two bugs found together in <see cref="TexturePipeline"/>: (1) terminal-state
    /// callbacks (Aborted/Timeout) were fired twice per request -- once by a manual loop, once by
    /// <c>CompleteTransfer</c>'s own loop over the same list -- and (2) that list
    /// (<c>TaskInfo.Callbacks</c>) was a plain, unsynchronized <see cref="List{T}"/> read and
    /// written from multiple threads (a caller thread via <c>RequestTexture</c>, and the download
    /// worker/packet-handler threads via <c>FireCallbacks</c>), which is a documented
    /// <see cref="InvalidOperationException"/> ("Collection was modified") hazard.
    /// </summary>
    [TestFixture]
    public class TexturePipelineTests
    {
        [Test]
        public void AbortTextureRequest_FiresCallbackExactlyOnce()
        {
            var client = new FakeGridClient();
            try
            {
                var pipeline = new TexturePipeline(client);
                var textureId = UUID.Random();

                var invocations = new List<TextureRequestState>();
                pipeline.RequestTexture(textureId, ImageType.Normal, 1f, 0, 0,
                    (state, tex) => invocations.Add(state), false);

                pipeline.AbortTextureRequest(textureId);

                Assert.That(invocations, Has.Count.EqualTo(1),
                    "AbortTextureRequest must deliver exactly one terminal callback, not one from a manual loop plus one from CompleteTransfer");
                Assert.That(invocations[0], Is.EqualTo(TextureRequestState.Aborted));
            }
            finally
            {
                try { client.Dispose(); } catch { /* best effort */ }
            }
        }

        [Test]
        public void RequestTexture_ConcurrentAddAndFireCallbacks_DoesNotThrow()
        {
            var client = new FakeGridClient();
            try
            {
                var pipeline = new TexturePipeline(client);
                var textureId = UUID.Random();

                // Registers the transfer and creates its TaskInfo (client has no cached asset for a
                // random UUID, so this takes the "not cached" path and adds to the internal transfer map).
                pipeline.RequestTexture(textureId, ImageType.Normal, 1f, 0, 0, (state, tex) => { }, false);

                // TaskInfo and FireCallbacks are private; reach them via reflection to exercise the
                // exact race RequestTexture's Add and FireCallbacks' locked snapshot are meant to guard,
                // without needing a live network connection to drive real packet handlers.
                var transfersField = typeof(TexturePipeline).GetField("_Transfers", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var transfers = (IDictionary)transfersField.GetValue(pipeline)!;
                var task = transfers[textureId]!;

                var fireCallbacks = typeof(TexturePipeline).GetMethod("FireCallbacks", BindingFlags.NonPublic | BindingFlags.Instance)!;

                Exception? caught = null;
                var stop = false;

                Exception? adderCaught = null;
                var adder = new Thread(() =>
                {
                    try
                    {
                        while (!Volatile.Read(ref stop))
                        {
                            pipeline.RequestTexture(textureId, ImageType.Normal, 1f, 0, 0, (state, tex) => { }, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        // A write-side throw (e.g. List<T>.Add racing another Add) would otherwise
                        // die silently on this thread while the reader thread finishes and passes.
                        adderCaught = ex;
                        Volatile.Write(ref stop, true);
                    }
                });

                var firer = new Thread(() =>
                {
                    try
                    {
                        for (var i = 0; i < 5000 && !Volatile.Read(ref stop); i++)
                        {
                            fireCallbacks.Invoke(pipeline,
                                new object?[] { task, TextureRequestState.Progress, new AssetTexture(textureId, Array.Empty<byte>()) });
                        }
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                    finally
                    {
                        Volatile.Write(ref stop, true);
                    }
                });

                adder.Start();
                firer.Start();
                firer.Join();
                Volatile.Write(ref stop, true);
                adder.Join();

                Assert.That(caught, Is.Null,
                    $"Concurrent RequestTexture (Add) and FireCallbacks (iterate) must not race (reader side): {caught}");
                Assert.That(adderCaught, Is.Null,
                    $"Concurrent RequestTexture (Add) and FireCallbacks (iterate) must not race (writer side): {adderCaught}");
            }
            finally
            {
                try { client.Dispose(); } catch { /* best effort */ }
            }
        }
    }
}
