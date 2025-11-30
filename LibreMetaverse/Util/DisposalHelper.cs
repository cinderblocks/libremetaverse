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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    /// <summary>
    /// Helper class for safely disposing resources
    /// </summary>
    public static class DisposalHelper
    {
        /// <summary>
        /// Safely dispose an object, catching and logging any exceptions
        /// </summary>
        public static void SafeDispose(IDisposable resource, string resourceName = null, Action<string, Exception> logger = null)
        {
            if (resource == null) return;

            try
            {
                resource.Dispose();
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrEmpty(resourceName)
                    ? "Error disposing resource"
                    : $"Error disposing {resourceName}";

                logger?.Invoke(message, ex);
            }
        }

        /// <summary>
        /// Safely dispose multiple resources
        /// </summary>
        public static void SafeDisposeAll(IEnumerable<IDisposable> resources, Action<string, Exception> logger = null)
        {
            if (resources == null) return;

            foreach (var resource in resources.Where(r => r != null))
            {
                SafeDispose(resource, logger: logger);
            }
        }

        /// <summary>
        /// Safely dispose all items in a collection and clear it
        /// </summary>
        public static void SafeDisposeClear<T>(ICollection<T> collection, Action<string, Exception> logger = null)
            where T : IDisposable
        {
            if (collection == null) return;

            try
            {
                SafeDisposeAll(collection.Cast<IDisposable>(), logger);
                collection.Clear();
            }
            catch (Exception ex)
            {
                logger?.Invoke("Error clearing collection", ex);
            }
        }

        /// <summary>
        /// Safely wait for and dispose a thread
        /// </summary>
        public static bool SafeJoinThread(Thread thread, TimeSpan timeout, Action<string, Exception> logger = null)
        {
            if (thread == null || !thread.IsAlive) return true;

            try
            {
                if (!thread.Join(timeout))
                {
                    logger?.Invoke($"Thread {thread.Name ?? "unnamed"} did not exit in time", null);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error waiting for thread {thread.Name ?? "unnamed"}", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely cancel and wait for a cancellation token source
        /// </summary>
        public static void SafeCancelAndDispose(CancellationTokenSource cts, Action<string, Exception> logger = null)
        {
            if (cts == null) return;

            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                logger?.Invoke("Error cancelling CancellationTokenSource", ex);
            }

            SafeDispose(cts, "CancellationTokenSource", logger);
        }

        /// <summary>
        /// Safely wait for a <see cref="Task"/> to complete with a timeout and observe exceptions.
        /// Returns true if the task completed within the timeout, false otherwise.
        /// </summary>
        public static bool SafeWaitTask(Task task, TimeSpan timeout, Action<string, Exception> logger = null)
        {
            if (task == null) return true;

            try
            {
                if (!task.Wait(timeout))
                {
                    logger?.Invoke("Task did not complete in time", null);
                    return false;
                }

                if (task.IsFaulted && task.Exception != null)
                {
                    // Unwrap AggregateException for logging
                    logger?.Invoke("Task faulted", task.Exception);
                }

                return true;
            }
            catch (AggregateException ae)
            {
                logger?.Invoke("Error waiting for task", ae);
                return false;
            }
            catch (Exception ex)
            {
                logger?.Invoke("Error waiting for task", ex);
                return false;
            }
        }

        /// <summary>
        /// Async-friendly variant that awaits a <see cref="Task"/> with a timeout and observes exceptions.
        /// Returns true if the task completed within the timeout, false otherwise.
        /// </summary>
        public static async Task<bool> SafeWaitTaskAsync(Task task, TimeSpan timeout, Action<string, Exception> logger = null)
        {
            if (task == null) return true;

            try
            {
                var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
                if (completed != task)
                {
                    logger?.Invoke("Task did not complete in time", null);
                    return false;
                }

                // Await again to propagate exceptions if any
                await task.ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                logger?.Invoke("Error awaiting task", ex);
                return false;
            }
        }

        /// <summary>
        /// Execute an action with automatic resource disposal
        /// </summary>
        public static TResult Using<TDisposable, TResult>(
            Func<TDisposable> factory,
            Func<TDisposable, TResult> action)
            where TDisposable : IDisposable
        {
            using (var resource = factory())
            {
                return action(resource);
            }
        }

        /// <summary>
        /// Execute an async action with automatic resource disposal
        /// </summary>
        public static async Task<TResult> UsingAsync<TDisposable, TResult>(
            Func<TDisposable> factory,
            Func<TDisposable, Task<TResult>> action)
            where TDisposable : IDisposable
        {
            using (var resource = factory())
            {
                return await action(resource);
            }
        }

        /// <summary>
        /// Guard for ensuring disposal even with exceptions
        /// </summary>
        public sealed class DisposalGuard : IDisposable
        {
            private readonly Action onDispose;
            private bool disposed;

            public DisposalGuard(Action onDispose)
            {
                this.onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                    onDispose();
                }
            }
        }

        /// <summary>
        /// Execute an action safely catching any exceptions and reporting via logger.
        /// </summary>
        public static void SafeAction(Action action, string actionName = null, Action<string, Exception> logger = null)
        {
            if (action == null) return;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrEmpty(actionName) ? "Error executing action" : $"Error executing {actionName}";
                logger?.Invoke(message, ex);
            }
        }
    }
}
