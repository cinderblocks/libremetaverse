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
using LockIntegralType = System.Int32;

// I am using direct reads that are non-atomic if 64-bits variables are used on 32-bit computers.

namespace LibreMetaverse.Threading
{
	/// <summary>
	/// An optimistic reader writer lock that is as fast as the 
	/// SpinReaderWriterLock/SpinReaderWriterLockSlim when the lock
	/// can be obtained immediately. But, if that's not the case, instead
	/// of spinning it will enter a real wait state. So, this one is preferable
	/// if the locks are expected to be of large duration (100 milliseconds or more)
	/// while the SpinReaderWriterLock is preferable if the waits are usually
	/// very small.
	/// Note that this class has both the Enter/Exit pairs (slim version) and the 
	/// methods that return a disposable object to release the lock (the non-slim
	/// version).
	/// </summary>
	public sealed class OptimisticReaderWriterLock:
		IReaderWriterLockSlim,
		IReaderWriterLock
	{
		#region Consts
			private const LockIntegralType WRITE_BIT_SHIFT = 24;
			private const LockIntegralType UPGRADE_BIT_SHIFT = 16;

			private const LockIntegralType WRITE_LOCK_VALUE = 1 << WRITE_BIT_SHIFT;
			private const LockIntegralType WRITE_UNLOCK_VALUE = -WRITE_LOCK_VALUE;
			private const LockIntegralType UPGRADE_LOCK_VALUE = 1 << UPGRADE_BIT_SHIFT;
			private const LockIntegralType UPGRADE_UNLOCK_VALUE = -UPGRADE_LOCK_VALUE;
			private const LockIntegralType ALL_READS_VALUE = UPGRADE_LOCK_VALUE-1;
			private const LockIntegralType SOME_EXCLUSIVE_LOCK_VALUE = WRITE_LOCK_VALUE | UPGRADE_LOCK_VALUE;
			private const LockIntegralType SOME_EXCLUSIVE_UNLOCK_VALUE = -SOME_EXCLUSIVE_LOCK_VALUE;
		#endregion

		#region Fields
			private LockIntegralType _lockValue;
			private LockIntegralType _waitingValue;
		#endregion

		#region ReadLock
			/// <summary>
			/// Acquires a read lock that must be used in a using clause.
			/// </summary>
			public OptimisticReadLock ReadLock()
			{
				EnterReadLock();
				return new OptimisticReadLock(this);
			}
		#endregion
		#region UpgradeableLock
			/// <summary>
			/// Acquires a upgradeable read lock that must be used in a using clause.
			/// </summary>
			public OptimisticUpgradeableLock UpgradeableLock()
			{
				EnterUpgradeableLock();
				return new OptimisticUpgradeableLock(this);
			}
		#endregion
		#region WriteLock
			/// <summary>
			/// Acquires a write lock that must be used in a using clause.
			/// If you are using a UpgradeableLock use the Upgrade method of the
			/// YieldUpgradeableLock instead or you will cause a dead-lock.
			/// </summary>
			public OptimisticWriteLock WriteLock()
			{
				EnterWriteLock();
				return new OptimisticWriteLock(this);
			}
		#endregion

		#region EnterReadLock
			/// <summary>
			/// Enters a read lock.
			/// </summary>
			public void EnterReadLock()
			{
				while(true)
				{
					LockIntegralType result = Interlocked.Increment(ref _lockValue);
					if (result < WRITE_LOCK_VALUE)
						return;

					lock(this)
					{
						// here we can read directly.						
						// also, if everything is OK we can return
						// directly as we still hold the readers count
						// at +1.
						if (_lockValue < WRITE_LOCK_VALUE)
							return;

						_waitingValue++;
						result = Interlocked.Decrement(ref _lockValue);
						if (result < WRITE_LOCK_VALUE)
						{
							_waitingValue--;
							continue;
						}

						while(true)
						{
							Monitor.Wait(this);

							// again, we can read directly.
							if (_lockValue < WRITE_LOCK_VALUE)
							{
								_waitingValue--;
								break;
							}
						}
					}
				}
			}
		#endregion
		#region ExitReadLock
			/// <summary>
			/// Exits a read-lock. Take care not to exit more times than you entered, as there is no check for that.
			/// </summary>
			public void ExitReadLock()
			{
				int result = Interlocked.Decrement(ref _lockValue);
				if ((result & ALL_READS_VALUE) == 0)
					if (_waitingValue > 0)
						lock(this)
							Monitor.PulseAll(this);

				// We need to Pulse all threads when there are no more readers
				// because we may have a thread waiting to obtain a write lock and an
				// upgradeable lock trying to upgrade. A simple pulse will free the
				// thread that wants the write lock, but it will not be able to get
				// it because there's the upgradeable lock already acquired.
			}
		#endregion

		#region EnterUpgradeableLock
			/// <summary>
			/// Enters an upgradeable lock (it is a read lock, but it can be upgraded).
			/// Only one upgradeable lock is allowed at a time.
			/// </summary>
			public void EnterUpgradeableLock()
			{
				while(true)
				{
					LockIntegralType result = Interlocked.Add(ref _lockValue, UPGRADE_LOCK_VALUE);
					if ((result >> UPGRADE_BIT_SHIFT) == 1)
						return;

					lock(this)
					{
						// here we can read directly.						
						// also, if everything is OK we can return
						// directly as we still hold the upgradeable count
						// at +1.
						if ((_lockValue >> UPGRADE_BIT_SHIFT) == 1)
							return;

						_waitingValue ++;
						result = Interlocked.Add(ref _lockValue, UPGRADE_UNLOCK_VALUE);
						if ((result >> UPGRADE_BIT_SHIFT) == 0)
						{
							_waitingValue --;
							continue;
						}

						// maybe we just forbid the thread that has the
						// upgradeable lock from upgrading to the write lock.
						// Also, as this may not be the next thread waiting, we
						// need to pulse all.
						if ((result >> UPGRADE_BIT_SHIFT) == 1)
							Monitor.PulseAll(this);

						while(true)
						{
							Monitor.Wait(this);

							// again, we can read directly.
							if ((_lockValue >> UPGRADE_BIT_SHIFT) == 0)
							{
								_waitingValue --;
								break;
							}
						}
					}
				}
			}
		#endregion
		#region ExitUpgradeableLock
			/// <summary>
			/// Exits a previously obtained upgradeable lock without
			/// verifying if it was upgraded or not.
			/// </summary>
			public void UncheckedExitUpgradeableLock()
			{
				var result = Interlocked.Add(ref _lockValue, UPGRADE_UNLOCK_VALUE);

				if ((result & ALL_READS_VALUE) == 0)
					if (_waitingValue > 0)
						lock(this)
							Monitor.Pulse(this);

				// Here we pulse, instead of PulseAll, because we never block
				// readers and it is guaranteed that there was no other thread with
				// an upgradeable lock trying to upgrade it. So, if there's a thread
				// trying to obtain either an upgradeable or writeable lock, we pulse
				// that single thread.
			}

			/// <summary>
			/// Exits a previously entered upgradeable lock.
			/// </summary>
			public void ExitUpgradeableLock(bool upgraded)
			{
				if (upgraded)
					UncheckedExitUpgradedLock();
				else
					UncheckedExitUpgradeableLock();
			}
		#endregion

		#region UpgradeToWriteLock
			/// <summary>
			/// Upgrades to write-lock. You must already own a Upgradeable lock and you must first exit the write lock then the upgradeable lock.
			/// </summary>
			public void UncheckedUpgradeToWriteLock()
			{
				while(true)
				{
					LockIntegralType result = Interlocked.CompareExchange(ref _lockValue, SOME_EXCLUSIVE_LOCK_VALUE, UPGRADE_LOCK_VALUE);
					if (result == UPGRADE_LOCK_VALUE)
						return;

					lock(this)
					{
						if (_lockValue == UPGRADE_LOCK_VALUE)
							continue;

						_waitingValue ++;

						Monitor.Wait(this);
						while(true)
						{
							if (_lockValue == UPGRADE_LOCK_VALUE)
							{
								_waitingValue --;
								break;
							}

							Monitor.Wait(this);
						}
					}
				}
			}
			/// <summary>
			/// upgrades to write-lock. You must already own a Upgradeable lock and you must first exit the write lock then the Upgradeable lock.
			/// </summary>
			public void UpgradeToWriteLock(ref bool upgraded)
			{
				if (upgraded)
					return;

				UncheckedUpgradeToWriteLock();
				upgraded = true;
			}
		#endregion
		#region UncheckedExitUpgradedLock
			/// <summary>
			/// Releases the Upgradeable lock and the upgraded version of it (the write lock)
			/// at the same time.
			/// Releasing the write lock and the upgradeable lock has the same effect, but
			/// it's slower.
			/// </summary>
			public void UncheckedExitUpgradedLock()
			{
				Interlocked.Add(ref _lockValue, SOME_EXCLUSIVE_UNLOCK_VALUE);

				if (_waitingValue > 0)
					lock(this)
						Monitor.PulseAll(this);
			}
		#endregion

		#region EnterWriteLock
			/// <summary>
			/// Enters write-lock.
			/// </summary>
			public void EnterWriteLock()
			{
				while(true)
				{
					LockIntegralType result = Interlocked.CompareExchange(ref _lockValue, WRITE_LOCK_VALUE, 0);
					if (result == 0)
						return;

					lock(this)
					{
						if (_lockValue == 0)
							continue;

						_waitingValue ++;

						Monitor.Wait(this);
						while(true)
						{
							if (_lockValue == 0)
							{
								_waitingValue --;
								break;
							}

							Monitor.Wait(this);
						}
					}
				}
			}
		#endregion
		#region ExitWriteLock
			/// <summary>
			/// Exits write lock. Take care to exit only when you entered, as there is no check for that.
			/// </summary>
			public void ExitWriteLock()
			{
				Interlocked.Add(ref _lockValue, WRITE_UNLOCK_VALUE);

				if (_waitingValue > 0)
					lock(this)
						Monitor.PulseAll(this);
			}
		#endregion

		#region IReaderWriterLock Private Implementation
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
