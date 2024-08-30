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
	/// Defines the contract that ReaderWriterLocks must follow.
	/// Note that if you want an Enter/Exit pair, you should
	/// use the IReaderWriterLockSlim.
	/// </summary>
	public interface IReaderWriterLock
	{
		/// <summary>
		/// Obtains a read lock. You should use it in a using clause
		/// so at the end the lock is released.
		/// </summary>
		IDisposable ReadLock();

		/// <summary>
		/// Obtains an upgradeable lock. You can use the returned
		/// value to upgrade the lock. Also, you should dispose
		/// the returned object (an using block is preferreable) to
		/// release the lock.
		/// </summary>
		IUpgradeableLock UpgradeableLock();

		/// <summary>
		/// Obtains an write lock. Call this method in a using clause
		/// so the lock is released at the end.
		/// </summary>
		IDisposable WriteLock();
	}
}
