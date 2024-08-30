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

namespace LibreMetaverse.Threading
{
    /// <summary>
    /// A real slim SpinWait (the Microsoft version is slower than the normal use of
    /// the lock keyword for uncontended locks).
    /// This lock never verifies ownership, so it will dead-lock if you try
    /// to enter it twice and it will allow one thread to enter the lock and
    /// another thread to release it (if you want that, it will be great... if not,
    /// you will be causing bugs).
    /// It should only be used in situations where the lock is expected to be
    /// held for very short times and when performance is really critical.
    /// This is a struct, so if you for some reason need to pass it as a parameter,
    /// use it as a ref, or else you will end-up using a copy of the lock instead
    /// of working on the real one.
    /// </summary>
    public struct SpinLockSlim
    {
        private int _locked;

        /// <summary>
        /// Enters the lock. So you can do your actions in a safe manner.
        /// </summary>
        public void Enter()
        {
            if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
                return;

            SpinWait spinWait = new SpinWait();
            while(true)
            {
                spinWait.SpinOnce();

                if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
                    return;
            }
        }

        /// <summary>
        /// Exits the lock. If the same thread exits and enters the lock constantly, it will
        /// probably got the lock many times before letting other threads get it, even if those
        /// other threads started to wait before the actual thread releases the lock. Fairness
        /// is not a strong point of this lock.
        /// </summary>
        public void Exit()
        {
            // There is no need to use a "volatile" write as all .NET writes 
            // have "release" semantics.
            _locked = 0;
        }
    }
}
