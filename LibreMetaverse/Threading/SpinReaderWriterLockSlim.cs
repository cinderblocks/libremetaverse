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

using System.Threading;
using LockIntegralType = System.Int32;

// I really want to use 64 bit variables, but in my computer (32 bit) it is 50% slower.
// So, I am keeping the 32 bit one. On 64 bits, the writeBitShift can be 48, the upgradeBitShift is 32.

namespace LibreMetaverse.Threading
{
    /// <summary>
    /// A "real slim" reader writer lock.
    /// Many readers can read at a time and only one writer is allowed.
    /// Reads can be recursive, but a try to a recursive write will cause a dead-lock.
    /// Note that this is a struct, so don't assign it to a local variable.
    /// </summary>
    public struct SpinReaderWriterLockSlim:
		IReaderWriterLockSlim
    {
	    #region Consts
		    private const int WRITE_BIT_SHIFT = 24;
		    private const int UPGRADE_BIT_SHIFT = 16;

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
	    #endregion

	    #region EnterReadLock
		    /// <summary>
		    /// Enters a read lock.
		    /// </summary>
		    public void EnterReadLock()
		    {
                var spinWait = new SpinWait();
			    while(true)
			    {
				    LockIntegralType result = Interlocked.Increment(ref _lockValue);
				    if ((result >> WRITE_BIT_SHIFT) == 0)
					    return;

				    Interlocked.Decrement(ref _lockValue);

				    while(true)
				    {
    				    spinWait.SpinOnce();

					    result = Interlocked.CompareExchange(ref _lockValue, 1, 0);
					    if (result == 0)
						    return;

					    if ((result >> WRITE_BIT_SHIFT) == 0)
						    break;
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
			    Interlocked.Decrement(ref _lockValue);
		    }
	    #endregion

	    #region EnterUpgradeableLock
		    /// <summary>
		    /// Enters an upgradeable lock (it is a read lock, but it can be upgraded).
		    /// Only one upgradeable lock is allowed at a time.
		    /// </summary>
		    public void EnterUpgradeableLock()
		    {
                var spinWait = new SpinWait();
			    while(true)
			    {
				    LockIntegralType result = Interlocked.Add(ref _lockValue, UPGRADE_LOCK_VALUE);
				    if ((result >> UPGRADE_BIT_SHIFT) == 1)
					    return;

				    Interlocked.Add(ref _lockValue, UPGRADE_UNLOCK_VALUE);

				    while(true)
				    {
					    spinWait.SpinOnce();

					    result = Interlocked.CompareExchange(ref _lockValue, UPGRADE_LOCK_VALUE, 0);
					    if (result == 0)
						    return;

					    if ((result >> UPGRADE_BIT_SHIFT) == 0)
						    break;
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
			    Interlocked.Add(ref _lockValue, UPGRADE_UNLOCK_VALUE);
		    }

            /// <summary>
            /// Exits a previously entered upgradeable lock.
            /// </summary>
		    public void ExitUpgradeableLock(bool upgraded)
		    {
			    if (upgraded)
				    Interlocked.Add(ref _lockValue, SOME_EXCLUSIVE_UNLOCK_VALUE);
			    else
				    Interlocked.Add(ref _lockValue, UPGRADE_UNLOCK_VALUE);
		    }
	    #endregion

	    #region UpgradeToWriteLock
		    /// <summary>
		    /// upgrades to write-lock. You must already own a Upgradeable lock and you must first exit the write lock then the Upgradeable lock.
		    /// </summary>
		    public void UncheckedUpgradeToWriteLock()
		    {
                var spinWait = new SpinWait();
			    LockIntegralType result = Interlocked.Add(ref _lockValue, WRITE_LOCK_VALUE);

			    while((result & ALL_READS_VALUE) != 0)
			    {
				    spinWait.SpinOnce();

				    result = Interlocked.CompareExchange(ref _lockValue, 0, 0);
			    }
		    }
		    /// <summary>
		    /// upgrades to write-lock. You must already own a Upgradeable lock and you must first exit the write lock then the Upgradeable lock.
		    /// </summary>
		    public void UpgradeToWriteLock(ref bool upgraded)
		    {
                if (upgraded)
                    return;

                var spinWait = new SpinWait();
			    LockIntegralType result = Interlocked.Add(ref _lockValue, WRITE_LOCK_VALUE);

			    while((result & ALL_READS_VALUE) != 0)
			    {
				    spinWait.SpinOnce();

				    result = Interlocked.CompareExchange(ref _lockValue, 0, 0);
			    }

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
		    }
	    #endregion

	    #region EnterWriteLock
		    /// <summary>
		    /// Enters write-lock.
		    /// </summary>
		    public void EnterWriteLock()
		    {
				LockIntegralType result = Interlocked.CompareExchange(ref _lockValue, WRITE_LOCK_VALUE, 0);
				if (result == 0)
					return;

                var spinWait = new SpinWait();
			    // we need to try again.
			    for(int i=0; i<100; i++)
			    {
				    spinWait.SpinOnce();

				    result = Interlocked.CompareExchange(ref _lockValue, WRITE_LOCK_VALUE, 0);
				    if (result == 0)
					    return;

				    // try to be the first locker.
				    if ((result >> WRITE_BIT_SHIFT) == 0)
					    break;
			    }

			    // From this moment, we have priority.
			    while(true)
			    {
				    result = Interlocked.Add(ref _lockValue, WRITE_LOCK_VALUE);
				    if (result == WRITE_LOCK_VALUE)
					    return;

				    if ((result >> WRITE_BIT_SHIFT) == 1)
				    {
					    // we obtained the write lock, but there may be readers,
					    // so we wait until they release the lock.
					    while(true)
					    {
						    spinWait.SpinOnce();

						    result = Interlocked.CompareExchange(ref _lockValue, 0, 0);
						    if (result == WRITE_LOCK_VALUE)
							    return;
					    }
				    }
				    else
				    {
					    // we need to try again.
					    Interlocked.Add(ref _lockValue, WRITE_UNLOCK_VALUE);
					    while(true)
					    {
						    spinWait.SpinOnce();

						    result = Interlocked.CompareExchange(ref _lockValue, WRITE_LOCK_VALUE, 0);
						    if (result == 0)
							    return;

						    // try to be the first locker.
						    if ((result >> WRITE_BIT_SHIFT) == 0)
							    break;
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
		    }
	    #endregion
    }
}
