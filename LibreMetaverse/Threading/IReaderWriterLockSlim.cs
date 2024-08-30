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

namespace LibreMetaverse.Threading
{
	/// <summary>
	/// Interface that must be implemented by reader-writer lock
	/// classes that are "slim". That is, they have Enter/Exit
	/// methods instead of returning disposable instances (which
	/// makes them faster, but more error prone).
	/// </summary>
	public interface IReaderWriterLockSlim
	{
		/// <summary>
		/// Enters a read lock. Many readers can enter
		/// the read lock at the same time.
		/// </summary>
		void EnterReadLock();

		/// <summary>
		/// Exits a previously entered read lock.
		/// </summary>
		void ExitReadLock();

		/// <summary>
		/// Enters an upgradeable read lock. Many read locks can
		/// be obtained at the same time that a single upgradeable
		/// read lock is active, but two upgradeable or an
		/// upgradeable and an write lock are not permitted.
		/// </summary>
		void EnterUpgradeableLock();

		/// <summary>
		/// Exits a previously entered upgradeable read lock.
		/// You should pass the boolean telling if the lock was
		/// upgraded or not.
		/// </summary>
		/// <param name="upgraded"></param>
		void ExitUpgradeableLock(bool upgraded);

		/// <summary>
		/// Exits an upgradeable read lock, considering it was never
		/// upgraded.
		/// </summary>
		void UncheckedExitUpgradeableLock();

		/// <summary>
		/// Upgraded a previously obtained upgradeable lock to a write
		/// lock, but does not check if the lock was already upgraded.
		/// </summary>
		void UncheckedUpgradeToWriteLock();

		/// <summary>
		/// Upgraded the upgradeable lock to a write lock, checking if
		/// it was already upgraded or not (and also updating the upgraded
		/// boolean). To upgrade, the lock will wait all readers to end.
		/// </summary>
		void UpgradeToWriteLock(ref bool upgraded);

		/// <summary>
		/// Exits an upgradeable lock that was also upgraded in a single
		/// task.
		/// </summary>
		void UncheckedExitUpgradedLock();

		/// <summary>
		/// Enters a write lock. That is, the lock will only be obtained when
		/// there are no readers, be them upgradeable or not.
		/// </summary>
		void EnterWriteLock();

		/// <summary>
		/// Exits a previously obtained write lock.
		/// </summary>
		void ExitWriteLock();
	}
}
