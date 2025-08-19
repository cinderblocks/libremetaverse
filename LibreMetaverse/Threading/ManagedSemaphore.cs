/*
 * Copyright (c) 2024, Sjofn LLC
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
using LibreMetaverse.Threading.Disposers;

namespace LibreMetaverse.Threading
{
	/// <summary>
	/// A semaphore class that uses only Monitor class for synchronization, avoiding
	/// operating system events.
	/// </summary>
	public sealed class ManagedSemaphore:
		IAdvancedDisposable
	{
		private readonly object _lock = new object();
		private int _availableCount;

		/// <summary>
		/// Creates a new semaphore with the given availableCount.
		/// </summary>
		public ManagedSemaphore(int availableCount)
		{
			if (availableCount < 1)
				throw new ArgumentException("availableCount must be at least 1.", nameof(availableCount));

			_availableCount = availableCount;
		}

		/// <summary>
		/// Disposes this semaphore.
		/// If you try to enter or exit it after this, the action will always return immediately.
		/// </summary>
		public void Dispose()
		{
			lock(_lock)
			{
				_availableCount = -1;
				Monitor.PulseAll(_lock);
			}
		}

		/// <summary>
		/// Gets a value indicating if this semaphore was disposed.
		/// </summary>
		public bool WasDisposed => _availableCount == -1;

		/// <summary>
		/// Enters the actual semaphore.
		/// </summary>
		public void Enter()
		{
			lock(_lock)
			{
				while(true)
				{
					if (_availableCount == -1)
						return;

					if (_availableCount > 0)
					{
						_availableCount--;
						return;
					}

					Monitor.Wait(_lock);
				}
			}
		}

		/// <summary>
		/// Enters the actual semaphore with the given count value.
		/// If you pass a value higher than the one used to create it, you will dead-lock (at least until
		/// the semaphore is disposed).
		/// </summary>
		public void Enter(int count)
		{
			if (count <= 0)
				throw new ArgumentException("count of semaphores to enter must be at least 1.", nameof(count));

			lock(_lock)
			{
				while(true)
				{
					if (_availableCount == -1)
						return;

					if (_availableCount >= count)
					{
						_availableCount -= count;
						return;
					}

					Monitor.Wait(_lock);
				}
			}
		}

		/// <summary>
		/// Exits the semaphore. One thread can enter it and another one exit it. There is no check for that.
		/// </summary>
		public void Exit()
		{
			lock(_lock)
			{
				if (_availableCount == -1)
					return;

				_availableCount++;
				Monitor.Pulse(_lock);
			}
		}

		/// <summary>
		/// Exits the semaphore the given amount. One thread can enter it and another one exit it. There is no check for that.
		/// </summary>
		public void Exit(int count)
		{
			if (count <= 0)
				throw new ArgumentException("count of semaphores to exit must be at least 1.", nameof(count));

			if (count == -1)
			{
				Exit();
				return;
			}

			lock(_lock)
			{
				if (_availableCount == -1)
					return;

				_availableCount += count;
				Monitor.PulseAll(_lock);
			}
		}
	}
}
