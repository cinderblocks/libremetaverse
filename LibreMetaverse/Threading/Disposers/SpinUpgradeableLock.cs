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

namespace LibreMetaverse.Threading.Disposers
{
	/// <summary>
	/// Class returned by UpgradeableLock method.
	/// </summary>
	public struct SpinUpgradeableLock:
		IUpgradeableLock
	{
		private SpinReaderWriterLock _lock;
		private bool _upgraded;

		internal SpinUpgradeableLock(SpinReaderWriterLock yieldLock)
		{
			_lock = yieldLock;
			_upgraded = false;
		}

		/// <summary>
		/// Upgrades this lock to a write-lock.
		/// </summary>
		public SpinWriteLock DisposableUpgrade()
		{
			var yieldLock = _lock;
			if (yieldLock == null)
				throw new ObjectDisposedException(GetType().FullName);

			yieldLock.Lock.UncheckedUpgradeToWriteLock();
			return new SpinWriteLock(yieldLock);
		}

		/// <summary>
		/// Upgrades the lock to a write-lock.
		/// Returns true if the lock was upgraded, false if it was
		/// already upgraded before.
		/// </summary>
		public bool Upgrade()
		{
			var yieldLock = _lock;
			if (yieldLock == null)
				throw new ObjectDisposedException(GetType().FullName);

			if (_upgraded)
				return false;

			yieldLock.Lock.UncheckedUpgradeToWriteLock();
			_upgraded = true;
			return true;
		}

		/// <summary>
		/// Releases the lock.
		/// </summary>
		public void Dispose()
		{
			var yieldLock = _lock;

			if (yieldLock != null)
			{
				_lock = null;

			    yieldLock.Lock.ExitUpgradeableLock(_upgraded);
			}
		}

		IDisposable IUpgradeableLock.DisposableUpgrade()
		{
			return DisposableUpgrade();
		}
	}
}
