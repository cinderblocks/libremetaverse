/*
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Threading.Tasks;

namespace LibreMetaverse
{
    /// <summary>
    /// Helper for managing event subscriptions with timeout and cancellation support
    /// </summary>
    /// <remarks>This may end up in LibreMetaverse</remarks>
    public static class EventSubscriptionHelper
    {
        /// <summary>
        /// Wait for an event to fire with timeout support using ManualResetEvent
        /// </summary>
        public static TResult WaitForEvent<TEventArgs, TResult>(
            Action<EventHandler<TEventArgs>> subscribe,
            Action<EventHandler<TEventArgs>> unsubscribe,
            Func<TEventArgs, bool> filter,
            Func<TEventArgs, TResult> resultSelector,
            int timeoutMs,
            TResult defaultValue = default)
        {
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<TEventArgs> handler = (sender, e) =>
            {
                if (filter == null || filter(e))
                {
                    tcs.TrySetResult(resultSelector(e));
                }
            };

            subscribe(handler);
            try
            {
                return tcs.Task.Wait(timeoutMs) ? tcs.Task.Result : defaultValue;
            }
            finally
            {
                unsubscribe(handler);
            }
        }

        /// <summary>
        /// Wait for an event to fire with timeout support using async/await
        /// </summary>
        public static async Task<TResult> WaitForEventAsync<TEventArgs, TResult>(
            Action<EventHandler<TEventArgs>> subscribe,
            Action<EventHandler<TEventArgs>> unsubscribe,
            Func<TEventArgs, bool> filter,
            Func<TEventArgs, TResult> resultSelector,
            int timeoutMs,
            CancellationToken cancellationToken = default,
            TResult defaultValue = default)
        {
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<TEventArgs> handler = (sender, e) =>
            {
                if (filter == null || filter(e))
                {
                    tcs.TrySetResult(resultSelector(e));
                }
            };

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                subscribe(handler);
                try
                {
                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cancellationToken));
                    if (completedTask == tcs.Task)
                    {
                        return await tcs.Task;
                    }

                    return defaultValue;
                }
                finally
                {
                    unsubscribe(handler);
                }
            }
        }

        /// <summary>
        /// Subscribe to an event temporarily to wait for a condition
        /// </summary>
        public static void WaitForCondition<TEventArgs>(
            Action<EventHandler<TEventArgs>> subscribe,
            Action<EventHandler<TEventArgs>> unsubscribe,
            Func<TEventArgs, bool> condition,
            int timeoutMs)
         {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<TEventArgs> handler = (sender, e) =>
            {
                if (condition(e))
                {
                    tcs.TrySetResult(true);
                }
            };

            subscribe(handler);
            try
            {
                tcs.Task.Wait(timeoutMs);
            }
            finally
            {
                unsubscribe(handler);
            }
         }
    }

    /// <summary>
    /// Disposable event subscription that automatically unsubscribes
    /// </summary>
    public class EventSubscription<TEventArgs> : IDisposable
    {
        private readonly Action<EventHandler<TEventArgs>> unsubscribe;
        private readonly EventHandler<TEventArgs> handler;
        private bool disposed;

        public EventSubscription(
            Action<EventHandler<TEventArgs>> subscribe,
            Action<EventHandler<TEventArgs>> unsubscribe,
            EventHandler<TEventArgs> handler)
        {
            this.unsubscribe = unsubscribe;
            this.handler = handler;
            subscribe(handler);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                unsubscribe(handler);
            }
        }
    }
}
