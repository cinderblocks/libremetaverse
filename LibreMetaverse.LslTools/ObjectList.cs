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

namespace LibreMetaverse
{

    public class ObjectList
    {
        private ObjectList.Link head;
        private ObjectList.Link last;
        private int count;

        private void Add0(ObjectList.Link a)
        {
            if (this.head == null)
                this.head = this.last = a;
            else
                this.last = this.last.next = a;
        }

        private object Get0(ObjectList.Link a, int x)
        {
            if (a == null || x < 0)
                return (object)null;
            if (x == 0)
                return a.it;
            return this.Get0(a.next, x - 1);
        }

        public void Add(object o)
        {
            this.Add0(new ObjectList.Link(o, (ObjectList.Link)null));
            ++this.count;
        }

        public void Push(object o)
        {
            this.head = new ObjectList.Link(o, this.head);
            ++this.count;
        }

        public object Pop()
        {
            object it = this.head.it;
            this.head = this.head.next;
            --this.count;
            return it;
        }

        public object Top => this.head.it;

        public int Count => this.count;

        public object this[int ix] => this.Get0(this.head, ix);

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator)new ObjectList.OListEnumerator(this);
        }

        private class Link
        {
            internal object it;
            internal ObjectList.Link next;

            internal Link(object o, ObjectList.Link x)
            {
                this.it = o;
                this.next = x;
            }
        }

        public class OListEnumerator : IEnumerator
        {
            private ObjectList list;
            private ObjectList.Link cur;

            public OListEnumerator(ObjectList o)
            {
                this.list = o;
            }

            public object Current => this.cur.it;

            public bool MoveNext()
            {
                this.cur = this.cur != null ? this.cur.next : this.list.head;
                return this.cur != null;
            }

            public void Reset()
            {
                this.cur = (ObjectList.Link)null;
            }
        }
    }
}