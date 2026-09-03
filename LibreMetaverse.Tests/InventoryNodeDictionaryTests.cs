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
using System.Threading;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="InventoryNodeDictionary"/> is built to be thread-safe -- every accessor locks on
    /// <see cref="InventoryNodeDictionary.SyncRoot"/> except (previously) the indexer getter and
    /// <c>Count</c>, which read the backing <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>
    /// unprotected. That matters in practice: <c>InventoryManager</c>'s packet handlers (e.g. for
    /// InventoryDescendents, which streams a folder's contents in during login) call
    /// <see cref="InventoryNodeDictionary.Add"/> from ThreadPool worker threads while application
    /// code reading a known item out of that same folder via <c>folder.Nodes[itemId]</c> -- the only
    /// way to fetch a single node by key, there being no <c>TryGetValue</c> -- runs on its own
    /// thread. A plain <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/> is documented
    /// as unsafe for concurrent read+write even with a single reader and a single writer.
    /// </summary>
    [TestFixture]
    public class InventoryNodeDictionaryTests
    {
        [Test]
        public void Indexer_ConcurrentAddAndGet_DoesNotThrow()
        {
            var parent = new InventoryNode();
            var dict = new InventoryNodeDictionary(parent);

            var seedId = UUID.Random();
            dict.Add(seedId, new InventoryNode());

            Exception? readerCaught = null;
            Exception? writerCaught = null;
            var stop = false;

            var writer = new Thread(() =>
            {
                try
                {
                    for (var i = 0; i < 20000 && !Volatile.Read(ref stop); i++)
                    {
                        dict.Add(UUID.Random(), new InventoryNode());
                    }
                }
                catch (Exception ex)
                {
                    writerCaught = ex;
                }
                finally
                {
                    Volatile.Write(ref stop, true);
                }
            });

            var reader = new Thread(() =>
            {
                try
                {
                    while (!Volatile.Read(ref stop))
                    {
                        _ = dict[seedId];
                    }
                }
                catch (Exception ex)
                {
                    readerCaught = ex;
                }
                finally
                {
                    Volatile.Write(ref stop, true);
                }
            });

            writer.Start();
            reader.Start();
            writer.Join();
            Volatile.Write(ref stop, true);
            reader.Join();

            Assert.That(readerCaught, Is.Null,
                $"Concurrent indexer get must not race against Add (reader side): {readerCaught}");
            Assert.That(writerCaught, Is.Null,
                $"Concurrent indexer get must not race against Add (writer side): {writerCaught}");
        }
    }
}
