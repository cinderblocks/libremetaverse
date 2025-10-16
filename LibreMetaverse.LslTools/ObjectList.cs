/*
 * Copyright (c) 2019-2024, Sjofn LLC
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

using System.Collections;

namespace LibreMetaverse.LSLTools
{

    public class ObjectList
    {
        private Link head;
        private Link last;

        private void Add0(Link a)
        {
            if (head == null)
                head = last = a;
            else
                last = last.next = a;
        }

        private object Get0(Link a, int x)
        {
            if (a == null || x < 0)
                return null;
            return x == 0 ? a.it : Get0(a.next, x - 1);
        }

        public void Add(object o)
        {
            Add0(new Link(o, null));
            ++Count;
        }

        public void Push(object o)
        {
            head = new Link(o, head);
            ++Count;
        }

        public object Pop()
        {
            object it = head.it;
            head = head.next;
            --Count;
            return it;
        }

        public object Top => head.it;

        public int Count { get; private set; }

        public object this[int ix] => Get0(head, ix);

        public IEnumerator GetEnumerator()
        {
            return new OListEnumerator(this);
        }

        private class Link
        {
            internal object it;
            internal Link next;

            internal Link(object o, Link x)
            {
                it = o;
                next = x;
            }
        }

        public class OListEnumerator : IEnumerator
        {
            private ObjectList list;
            private Link cur;

            public OListEnumerator(ObjectList o)
            {
                list = o;
            }

            public object Current => cur.it;

            public bool MoveNext()
            {
                cur = cur != null ? cur.next : list.head;
                return cur != null;
            }

            public void Reset()
            {
                cur = null;
            }
        }
    }
}