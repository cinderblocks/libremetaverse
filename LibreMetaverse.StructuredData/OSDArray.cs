/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OpenMetaverse.StructuredData
{
    /// <summary>
    /// OSD Array Element
    /// </summary>
    public sealed class OSDArray : OSD, IList<OSD>
    {
        private readonly List<OSD> _mArray;

        public override OSDType Type => OSDType.Array;

        public OSDArray()
        {
            _mArray = new List<OSD>();
        }

        public OSDArray(int capacity)
        {
            _mArray = new List<OSD>(capacity);
        }

        public OSDArray(List<OSD> value)
        {
            this._mArray = value ?? new List<OSD>();
        }

        public override byte[] AsBinary()
        {
            byte[] binary = new byte[_mArray.Count];

            for (int i = 0; i < _mArray.Count; i++)
                binary[i] = (byte)_mArray[i].AsInteger();

            return binary;
        }

        public override long AsLong()
        {
            OSDBinary binary = new OSDBinary(AsBinary());
            return binary.AsLong();
        }

        public override ulong AsULong()
        {
            OSDBinary binary = new OSDBinary(AsBinary());
            return binary.AsULong();
        }

        public override uint AsUInteger()
        {
            OSDBinary binary = new OSDBinary(AsBinary());
            return binary.AsUInteger();
        }

        public override Vector2 AsVector2()
        {
            Vector2 vector = Vector2.Zero;

            if (Count == 2)
            {
                vector.X = (float)this[0].AsReal();
                vector.Y = (float)this[1].AsReal();
            }

            return vector;
        }

        public override Vector3 AsVector3()
        {
            Vector3 vector = Vector3.Zero;

            if (Count == 3)
            {
                vector.X = (float)this[0].AsReal();
                vector.Y = (float)this[1].AsReal();
                vector.Z = (float)this[2].AsReal();
            }

            return vector;
        }

        public override Vector3d AsVector3d()
        {
            Vector3d vector = Vector3d.Zero;

            if (Count == 3)
            {
                vector.X = this[0].AsReal();
                vector.Y = this[1].AsReal();
                vector.Z = this[2].AsReal();
            }

            return vector;
        }

        public override Vector4 AsVector4()
        {
            Vector4 vector = Vector4.Zero;

            if (Count == 4)
            {
                vector.X = (float)this[0].AsReal();
                vector.Y = (float)this[1].AsReal();
                vector.Z = (float)this[2].AsReal();
                vector.W = (float)this[3].AsReal();
            }

            return vector;
        }

        public override Quaternion AsQuaternion()
        {
            Quaternion quaternion = Quaternion.Identity;

            if (Count == 4)
            {
                quaternion.X = (float)this[0].AsReal();
                quaternion.Y = (float)this[1].AsReal();
                quaternion.Z = (float)this[2].AsReal();
                quaternion.W = (float)this[3].AsReal();
            }

            return quaternion;
        }

        public override Color4 AsColor4()
        {
            Color4 color = Color4.Black;

            if (Count == 4)
            {
                color.R = (float)this[0].AsReal();
                color.G = (float)this[1].AsReal();
                color.B = (float)this[2].AsReal();
                color.A = (float)this[3].AsReal();
            }

            return color;
        }

        public override OSD Copy()
        {
            return new OSDArray(new List<OSD>(_mArray));
        }

        public override bool AsBoolean() { return _mArray.Count > 0; }

        public override string ToString()
        {
            return OSDParser.SerializeJsonString(this, true);
        }

        public ArrayList ToArrayList()
        {
            return new ArrayList(_mArray);
        }

        #region IList Implementation

        public int Count => _mArray.Count;
        public bool IsReadOnly => false;

        public OSD this[int index]
        {
            get => _mArray[index];
            set => _mArray[index] = value;
        }

        public int IndexOf(OSD item)
        {
            return _mArray.IndexOf(item);
        }

        public void Insert(int index, OSD item)
        {
            _mArray.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            _mArray.RemoveAt(index);
        }

        public void Add(OSD llsd)
        {
            _mArray.Add(llsd);
        }

        public void Clear()
        {
            _mArray.Clear();
        }

        public bool Contains(OSD llsd)
        {
            return _mArray.Contains(llsd);
        }

        public bool Contains(string element)
        {
            return _mArray.Any(t => t.Type == OSDType.String && t.AsString() == element);
        }

        public void CopyTo(OSD[] array, int index)
        {
            if (array == null) { throw new ArgumentNullException(nameof(array)); }
            if (array.Rank != 1) { throw new ArgumentException("Multi-dimensional arrays not supported"); }
            if (index < 0) { throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be a negative number"); }

            for (var i = index; i < array.Length; i++)
            {
                _mArray.Add(array[i]);
            }
        }

        public bool Remove(OSD llsd)
        {
            return _mArray.Remove(llsd);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _mArray.GetEnumerator();
        }

        IEnumerator<OSD> IEnumerable<OSD>.GetEnumerator()
        {
            return _mArray.GetEnumerator();
        }

        #endregion IList Implementation
    }
}