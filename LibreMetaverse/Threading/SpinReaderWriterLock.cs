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
using LibreMetaverse.Threading.Disposers;

namespace LibreMetaverse.Threading
{
	/// <summary>
	/// A reader writer lock that uses SpinWait if it does not have the lock. If the locks are held for
    /// to much time, this is CPU consuming.
	/// In my general tests, it is about 20 times faster than ReaderWriterLockSlim class and two times
    /// faster than the YieldReaderWriterLock.
	/// </summary>
	public sealed class SpinReaderWriterLock:
		IReaderWriterLock
	{
		#region Fields
			internal SpinReaderWriterLockSlim Lock;
		#endregion

		#region ReadLock
			/// <summary>
			/// Acquires a read lock that must be used in a using clause.
			/// </summary>
			public SpinReadLock ReadLock()
			{
				Lock.EnterReadLock();
				return new SpinReadLock(this);
			}
		#endregion
		#region UpgradeableLock
			/// <summary>
			/// Acquires a upgradeable read lock that must be used in a using clause.
			/// </summary>
			public SpinUpgradeableLock UpgradeableLock()
			{
				Lock.EnterUpgradeableLock();
				return new SpinUpgradeableLock(this);
			}
		#endregion
		#region WriteLock
			/// <summary>
			/// Acquires a write lock that must be used in a using clause.
			/// If you are using a UpgradeableLock use the Upgrade method of the
			/// YieldUpgradeableLock instead or you will cause a dead-lock.
			/// </summary>
			public SpinWriteLock WriteLock()
			{
				Lock.EnterWriteLock();
				return new SpinWriteLock(this);
			}
		#endregion

		#region IReaderWriterLock Members
			IDisposable IReaderWriterLock.ReadLock()
			{
				return ReadLock();
			}

			IUpgradeableLock IReaderWriterLock.UpgradeableLock()
			{
				return UpgradeableLock();
			}

			IDisposable IReaderWriterLock.WriteLock()
			{
				return WriteLock();
			}
		#endregion
	}
}
