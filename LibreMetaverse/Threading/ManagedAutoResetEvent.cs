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
	/// An auto reset event that uses only Monitor methods to work, avoiding
	/// operating system events.
	/// </summary>
	public sealed class ManagedAutoResetEvent:
		IAdvancedDisposable,
		IEventWait
	{
		private readonly object _lock = new object();
		private bool _value;
		private bool _wasDisposed;

		/// <summary>
		/// Creates a new event, not signaled.
		/// </summary>
		public ManagedAutoResetEvent()
		{
		}

		/// <summary>
		/// Creates a new event, letting you say if it starts signaled or not.
		/// </summary>
		public ManagedAutoResetEvent(bool initialState)
		{
			_value = initialState;
		}

		/// <summary>
		/// Disposes this event. After disposing, it is always set.
		/// Calling Reset will not work and it will not throw exceptions, so you can
		/// dispose it when there are threads waiting on it.
		/// </summary>
		public void Dispose()
		{
			lock(_lock)
			{
				_wasDisposed = true;
				_value = true;
				Monitor.PulseAll(_lock);
			}
		}

		/// <summary>
		/// Gets a value indicating if this event was disposed.
		/// </summary>
		public bool WasDisposed => _wasDisposed;

		/// <summary>
		/// Gets a value indicating if this auto-reset event is set.
		/// </summary>
		public bool IsSet => _value;

		/// <summary>
		/// Resets this event (makes it non-signaled).
		/// </summary>
		public void Reset()
		{
			lock(_lock)
			{
				if (_wasDisposed)
					return;

				_value = false;
			}
		}

		/// <summary>
		/// Signals the event, releasing one thread waiting on it.
		/// </summary>
		public void Set()
		{
			lock(_lock)
			{
				_value = true;
				Monitor.Pulse(_lock);
			}
		}

		/// <summary>
		/// Waits until this event is signaled.
		/// </summary>
		public void WaitOne()
		{
			lock(_lock)
			{
				while(!_value)
					Monitor.Wait(_lock);

				if (!_wasDisposed)
					_value = false;
			}
		}

		/// <summary>
		/// Waits until this event is signaled or until the timeout arrives.
		/// Return of true means it was signaled, false means timeout.
		/// </summary>
		public bool WaitOne(int millisecondsTimeout)
		{
			lock(_lock)
			{
				while(!_value)
					if (!Monitor.Wait(_lock, millisecondsTimeout))
						return false;

				if (!_wasDisposed)
					_value = false;
			}

			return true;
		}

		/// <summary>
		/// Waits until this event is signaled or until the timeout arrives.
		/// Return of true means it was signaled, false means timeout.
		/// </summary>
		public bool WaitOne(TimeSpan timeout)
		{
			lock(_lock)
			{
				while(!_value)
					if (!Monitor.Wait(_lock, timeout))
						return false;

				if (!_wasDisposed)
					_value = false;
			}

			return true;
		}
	}
}
