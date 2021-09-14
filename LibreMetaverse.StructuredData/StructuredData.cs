/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021, Sjofn LLC.
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenMetaverse.StructuredData
{
    public enum OSDType : byte
    {
        Unknown,
        Boolean,
        Integer,
        Real,
        String,
        UUID,
        Date,
        URI,
        Binary,
        Map,
        Array,
        LlsdXml
    }

    public enum OSDFormat
    {
        Xml = 0,
        Json,
        Binary
    }

    /// <inheritdoc />
    /// <summary>
    /// OSD Exception
    /// </summary>
    [Serializable]
    public class OSDException : Exception
    {
        public OSDException(string message) : base(message) { }
        public OSDException() { }
        public OSDException(string message, Exception innerException) : base(message, innerException) { }
        protected OSDException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }     
    }

    /// <summary>
    /// OSD element base class
    /// </summary>
    public partial class OSD
    {
        public virtual OSDType Type => OSDType.Unknown;

        public virtual bool AsBoolean() { return false; }
        public virtual int AsInteger() { return 0; }
        public virtual uint AsUInteger() { return 0; }
        public virtual long AsLong() { return 0; }
        public virtual ulong AsULong() { return 0; }
        public virtual double AsReal() { return 0d; }
        public virtual string AsString() { return string.Empty; }
        public virtual UUID AsUUID() { return UUID.Zero; }
        public virtual DateTime AsDate() { return Utils.Epoch; }
        public virtual Uri AsUri() { return null; }
        public virtual byte[] AsBinary() { return Utils.EmptyBytes; }
        public virtual Vector2 AsVector2() { return Vector2.Zero; }
        public virtual Vector3 AsVector3() { return Vector3.Zero; }
        public virtual Vector3d AsVector3d() { return Vector3d.Zero; }
        public virtual Vector4 AsVector4() { return Vector4.Zero; }
        public virtual Quaternion AsQuaternion() { return Quaternion.Identity; }
        public virtual Color4 AsColor4() { return Color4.Black; }
        public virtual OSD Copy() { return new OSD(); }

        public override string ToString() { return "undef"; }

        public static OSD FromBoolean(bool value) { return new OSDBoolean(value); }
        public static OSD FromInteger(int value) { return new OSDInteger(value); }
        public static OSD FromInteger(uint value) { return new OSDInteger((int)value); }
        public static OSD FromInteger(short value) { return new OSDInteger(value); }
        public static OSD FromInteger(ushort value) { return new OSDInteger(value); }
        public static OSD FromInteger(sbyte value) { return new OSDInteger(value); }
        public static OSD FromInteger(byte value) { return new OSDInteger(value); }
        public static OSD FromUInteger(uint value) { return new OSDBinary(value); }
        public static OSD FromLong(long value) { return new OSDBinary(value); }
        public static OSD FromULong(ulong value) { return new OSDBinary(value); }
        public static OSD FromReal(double value) { return new OSDReal(value); }
        public static OSD FromReal(float value) { return new OSDReal(value); }
        public static OSD FromString(string value) { return new OSDString(value); }
        public static OSD FromUUID(UUID value) { return new OSDUUID(value); }
        public static OSD FromDate(DateTime value) { return new OSDDate(value); }
        public static OSD FromUri(Uri value) { return new OSDUri(value); }
        public static OSD FromBinary(byte[] value) { return new OSDBinary(value); }

        public static OSD FromVector2(Vector2 value)
        {
            OSDArray array = new OSDArray
            {
                FromReal(value.X),
                FromReal(value.Y)
            };
            return array;
        }

        public static OSD FromVector3(Vector3 value)
        {
            OSDArray array = new OSDArray
            {
                FromReal(value.X),
                FromReal(value.Y),
                FromReal(value.Z)
            };
            return array;
        }

        public static OSD FromVector3d(Vector3d value)
        {
            OSDArray array = new OSDArray
            {
                FromReal(value.X),
                FromReal(value.Y),
                FromReal(value.Z)
            };
            return array;
        }

        public static OSD FromVector4(Vector4 value)
        {
            OSDArray array = new OSDArray
            {
                FromReal(value.X),
                FromReal(value.Y),
                FromReal(value.Z),
                FromReal(value.W)
            };
            return array;
        }

        public static OSD FromQuaternion(Quaternion value)
        {
            OSDArray array = new OSDArray
            {
                FromReal(value.X),
                FromReal(value.Y),
                FromReal(value.Z),
                FromReal(value.W)
            };
            return array;
        }

        public static OSD FromColor4(Color4 value)
        {
            OSDArray array = new OSDArray
            {
                FromReal(value.R),
                FromReal(value.G),
                FromReal(value.B),
                FromReal(value.A)
            };
            return array;
        }

        public static OSD FromObject(object value)
        {
            switch (value)
            {
                case null:
                    return new OSD();
                case bool b:
                    return new OSDBoolean(b);
                case int i:
                    return new OSDInteger(i);
                case uint u:
                    return new OSDBinary(u);
                case short s:
                    return new OSDInteger(s);
                case ushort us:
                    return new OSDInteger(us);
                case sbyte sb:
                    return new OSDInteger(sb);
                case byte by:
                    return new OSDInteger(by);
                case double d:
                    return new OSDReal(d);
                case float f:
                    return new OSDReal(f);
                case string str:
                    return new OSDString(str);
                case UUID uuid:
                    return new OSDUUID(uuid);
                case DateTime time:
                    return new OSDDate(time);
                case Uri uri:
                    return new OSDUri(uri);
                case byte[] bytes:
                    return new OSDBinary(bytes);
                case long l:
                    return new OSDBinary(l);
                case ulong ul:
                    return new OSDBinary(ul);
                case Vector2 vector2:
                    return FromVector2(vector2);
                case Vector3 vector3:
                    return FromVector3(vector3);
                case Vector3d vector3D:
                    return FromVector3d(vector3D);
                case Vector4 vector4:
                    return FromVector4(vector4);
                case Quaternion quaternion:
                    return FromQuaternion(quaternion);
                case Color4 color4:
                    return FromColor4(color4);
            }
            return new OSD();
        }

        public static object ToObject(Type type, OSD value)
        {
            if (type == typeof(ulong))
            {
                if (value.Type == OSDType.Binary)
                {
                    byte[] bytes = value.AsBinary();
                    return Utils.BytesToUInt64(bytes);
                }
                return (ulong)value.AsInteger();
            }
            if (type == typeof(uint))
            {
                if (value.Type == OSDType.Binary)
                {
                    byte[] bytes = value.AsBinary();
                    return Utils.BytesToUInt(bytes);
                }
                return (uint)value.AsInteger();
            }
            if (type == typeof(ushort))
            {
                return (ushort)value.AsInteger();
            }
            if (type == typeof(byte))
            {
                return (byte)value.AsInteger();
            }
            if (type == typeof(short))
            {
                return (short)value.AsInteger();
            }
            if (type == typeof(string))
            {
                return value.AsString();
            }
            if (type == typeof(bool))
            {
                return value.AsBoolean();
            }
            if (type == typeof(float))
            {
                return (float)value.AsReal();
            }
            if (type == typeof(double))
            {
                return value.AsReal();
            }
            if (type == typeof(int))
            {
                return value.AsInteger();
            }
            if (type == typeof(UUID))
            {
                return value.AsUUID();
            }
            if (type == typeof(Vector3))
            {
                if (value.Type == OSDType.Array)
                    return ((OSDArray)value).AsVector3();
                return Vector3.Zero;
            }
            if (type == typeof(Vector4))
            {
                if (value.Type == OSDType.Array)
                    return ((OSDArray)value).AsVector4();
                return Vector4.Zero;
            }
            if (type == typeof(Quaternion))
            {
                if (value.Type == OSDType.Array)
                    return ((OSDArray)value).AsQuaternion();
                return Quaternion.Identity;
            }
            if (type == typeof(OSDArray))
            {
                OSDArray newArray = new OSDArray();
                foreach (OSD o in (OSDArray)value)
                    newArray.Add(o);
                return newArray;
            }
            if (type == typeof(OSDMap))
            {
                OSDMap newMap = new OSDMap();
                foreach (KeyValuePair<string, OSD> o in (OSDMap)value)
                    newMap.Add(o);
                return newMap;
            }
            return null;
        }

        #region Implicit Conversions

        public static implicit operator OSD(bool value) { return new OSDBoolean(value); }
        public static implicit operator OSD(int value) { return new OSDInteger(value); }
        public static implicit operator OSD(uint value) { return new OSDInteger((int)value); }
        public static implicit operator OSD(short value) { return new OSDInteger(value); }
        public static implicit operator OSD(ushort value) { return new OSDInteger(value); }
        public static implicit operator OSD(sbyte value) { return new OSDInteger(value); }
        public static implicit operator OSD(byte value) { return new OSDInteger(value); }
        public static implicit operator OSD(long value) { return new OSDBinary(value); }
        public static implicit operator OSD(ulong value) { return new OSDBinary(value); }
        public static implicit operator OSD(double value) { return new OSDReal(value); }
        public static implicit operator OSD(float value) { return new OSDReal(value); }
        public static implicit operator OSD(string value) { return new OSDString(value); }
        public static implicit operator OSD(UUID value) { return new OSDUUID(value); }
        public static implicit operator OSD(DateTime value) { return new OSDDate(value); }
        public static implicit operator OSD(Uri value) { return new OSDUri(value); }
        public static implicit operator OSD(byte[] value) { return new OSDBinary(value); }
        public static implicit operator OSD(Vector2 value) { return FromVector2(value); }
        public static implicit operator OSD(Vector3 value) { return FromVector3(value); }
        public static implicit operator OSD(Vector3d value) { return FromVector3d(value); }
        public static implicit operator OSD(Vector4 value) { return FromVector4(value); }
        public static implicit operator OSD(Quaternion value) { return FromQuaternion(value); }
        public static implicit operator OSD(Color4 value) { return FromColor4(value); }

        public static implicit operator bool(OSD value) { return value.AsBoolean(); }
        public static implicit operator int(OSD value) { return value.AsInteger(); }
        public static implicit operator uint(OSD value) { return value.AsUInteger(); }
        public static implicit operator long(OSD value) { return value.AsLong(); }
        public static implicit operator ulong(OSD value) { return value.AsULong(); }
        public static implicit operator double(OSD value) { return value.AsReal(); }
        public static implicit operator float(OSD value) { return (float)value.AsReal(); }
        public static implicit operator string(OSD value) { return value.AsString(); }
        public static implicit operator UUID(OSD value) { return value.AsUUID(); }
        public static implicit operator DateTime(OSD value) { return value.AsDate(); }
        public static implicit operator Uri(OSD value) { return value.AsUri(); }
        public static implicit operator byte[](OSD value) { return value.AsBinary(); }
        public static implicit operator Vector2(OSD value) { return value.AsVector2(); }
        public static implicit operator Vector3(OSD value) { return value.AsVector3(); }
        public static implicit operator Vector3d(OSD value) { return value.AsVector3d(); }
        public static implicit operator Vector4(OSD value) { return value.AsVector4(); }
        public static implicit operator Quaternion(OSD value) { return value.AsQuaternion(); }
        public static implicit operator Color4(OSD value) { return value.AsColor4(); }

        #endregion Implicit Conversions

        /// <summary>
        /// Uses reflection to create an SDMap from all of the SD
        /// serializable types in an object
        /// </summary>
        /// <param name="obj">Class or struct containing serializable types</param>
        /// <returns>An SDMap holding the serialized values from the
        /// container object</returns>
        public static OSDMap SerializeMembers(object obj)
        {
            Type t = obj.GetType();
            FieldInfo[] fields = t.GetFields();

            OSDMap map = new OSDMap(fields.Length);

            foreach (FieldInfo field in fields)
            {
                if (!Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
                {
                    OSD serializedField = FromObject(field.GetValue(obj));

                    if (serializedField.Type != OSDType.Unknown
                        || field.FieldType == typeof(string)
                        || field.FieldType == typeof(byte[]))
                    {
                        map.Add(field.Name, serializedField);
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// Uses reflection to deserialize member variables in an object from
        /// an SDMap
        /// </summary>
        /// <param name="obj">Reference to an object to fill with deserialized
        /// values</param>
        /// <param name="serialized">Serialized values to put in the target
        /// object</param>
        public static void DeserializeMembers(ref object obj, OSDMap serialized)
        {
            Type t = obj.GetType();
            FieldInfo[] fields = t.GetFields();

            foreach (FieldInfo field in fields)
            {
                if (!Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
                {
                    OSD serializedField;
                    if (serialized.TryGetValue(field.Name, out serializedField))
                        field.SetValue(obj, ToObject(field.FieldType, serializedField));
                }
            }
        }
    }

    /// <summary>
    /// OSD Boolean Element
    /// </summary>
    public sealed class OSDBoolean : OSD
    {
        private readonly bool _mBool;

        private static readonly byte[] trueBinary = { 0x31 };
        private static readonly byte[] falseBinary = { 0x30 };

        public override OSDType Type => OSDType.Boolean;

        public OSDBoolean(bool value)
        {
            this._mBool = value;
        }

        public override bool AsBoolean() { return _mBool; }
        public override int AsInteger() { return _mBool ? 1 : 0; }
        public override double AsReal() { return _mBool ? 1d : 0d; }
        public override string AsString() { return _mBool ? "1" : "0"; }
        public override byte[] AsBinary() { return _mBool ? trueBinary : falseBinary; }
        public override OSD Copy() { return new OSDBoolean(_mBool); }

        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD Integer Element
    /// </summary>
    public sealed class OSDInteger : OSD
    {
        private readonly int _mInt;

        public override OSDType Type => OSDType.Integer;

        public OSDInteger(int value)
        {
            this._mInt = value;
        }

        public override bool AsBoolean() { return _mInt != 0; }
        public override int AsInteger() { return _mInt; }
        public override uint AsUInteger() { return (uint)_mInt; }
        public override long AsLong() { return _mInt; }
        public override ulong AsULong() { return (ulong)_mInt; }
        public override double AsReal() { return _mInt; }
        public override string AsString() { return _mInt.ToString(); }
        public override byte[] AsBinary() { return Utils.IntToBytesBig(_mInt); }
        public override OSD Copy() { return new OSDInteger(_mInt); }

        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD Floating point Element
    /// </summary>
    public sealed class OSDReal : OSD
    {
        private readonly double _mReal;

        public override OSDType Type => OSDType.Real;

        public OSDReal(double value)
        {
            this._mReal = value;
        }

        public override bool AsBoolean() { return (!double.IsNaN(_mReal) && _mReal != 0d); }
        public override OSD Copy() { return new OSDReal(_mReal); }

        public override int AsInteger()
        {
            if (double.IsNaN(_mReal))
                return 0;
            if (_mReal > Int32.MaxValue)
                return Int32.MaxValue;
            if (_mReal < Int32.MinValue)
                return Int32.MinValue;
            return (int)Math.Round(_mReal);
        }

        public override uint AsUInteger()
        {
            if (double.IsNaN(_mReal))
                return 0;
            if (_mReal > UInt32.MaxValue)
                return UInt32.MaxValue;
            if (_mReal < UInt32.MinValue)
                return UInt32.MinValue;
            return (uint)Math.Round(_mReal);
        }

        public override long AsLong()
        {
            if (double.IsNaN(_mReal))
                return 0;
            if (_mReal > Int64.MaxValue)
                return Int64.MaxValue;
            if (_mReal < Int64.MinValue)
                return Int64.MinValue;
            return (long)Math.Round(_mReal);
        }

        public override ulong AsULong()
        {
            if (double.IsNaN(_mReal))
                return 0;
            if (_mReal > UInt64.MaxValue)
                return Int32.MaxValue;
            if (_mReal < UInt64.MinValue)
                return UInt64.MinValue;
            return (ulong)Math.Round(_mReal);
        }

        public override double AsReal() { return _mReal; }
        // "r" ensures the dt will correctly round-trip back through Double.TryParse
        public override string AsString() { return _mReal.ToString("r", Utils.EnUsCulture); }
        public override byte[] AsBinary() { return Utils.DoubleToBytesBig(_mReal); }
        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD LLSD-XML Element
    /// </summary>
    public sealed class OSDLlsdXml : OSD
    {
        public readonly string value;
        public override OSDType Type => OSDType.LlsdXml;

        public override OSD Copy() { return new OSDLlsdXml(value); }

        public OSDLlsdXml(string value)
        {
            // Refuse to hold null pointers
            this.value = value ?? string.Empty;
        }

        public override string AsString() { return value; }
        public override byte[] AsBinary() { return Encoding.UTF8.GetBytes(value); }
        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD String Element
    /// </summary>
    public sealed class OSDString : OSD
    {
        private readonly string _mString;

        public override OSDType Type => OSDType.String;

        public override OSD Copy() { return new OSDString(_mString); }

        public OSDString(string value)
        {
            // Refuse to hold null pointers
            this._mString = value ?? string.Empty;
        }

        public override bool AsBoolean()
        {
            if (string.IsNullOrEmpty(_mString))
                return false;

            return _mString != "0" && !string.Equals(_mString, "false", StringComparison.OrdinalIgnoreCase);
        }

        public override int AsInteger()
        {
            double dbl;
            if (double.TryParse(_mString, out dbl))
                return (int)Math.Floor(dbl);
            return 0;
        }

        public override uint AsUInteger()
        {
            double dbl;
            if (double.TryParse(_mString, out dbl))
                return (uint)Math.Floor(dbl);
            return 0;
        }

        public override long AsLong()
        {
            double dbl;
            if (double.TryParse(_mString, out dbl))
                return (long)Math.Floor(dbl);
            return 0;
        }

        public override ulong AsULong()
        {
            double dbl;
            if (double.TryParse(_mString, out dbl))
                return (ulong)Math.Floor(dbl);
            return 0;
        }

        public override double AsReal()
        {
            double dbl;
            return Double.TryParse(_mString, out dbl) ? dbl : 0d;
        }

        public override string AsString() { return _mString; }
        public override byte[] AsBinary() { return Encoding.UTF8.GetBytes(_mString); }
        public override UUID AsUUID()
        {
            UUID uuid;
            return UUID.TryParse(_mString, out uuid) ? uuid : UUID.Zero;
        }
        public override DateTime AsDate()
        {
            DateTime dt;
            return DateTime.TryParse(_mString, out dt) ? dt : Utils.Epoch;
        }
        public override Uri AsUri()
        {
            Uri uri;
            return Uri.TryCreate(_mString, UriKind.RelativeOrAbsolute, out uri) ? uri : null;
        }

        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD UUID Element
    /// </summary>
    public sealed class OSDUUID : OSD
    {
        private UUID _id;

        public override OSDType Type => OSDType.UUID;

        public OSDUUID(UUID value)
        {
            _id = value;
        }

        public override OSD Copy() { return new OSDUUID(_id); }
        public override bool AsBoolean() { return (_id != UUID.Zero); }
        public override string AsString() { return _id.ToString(); }
        public override UUID AsUUID() { return _id; }
        public override byte[] AsBinary() { return _id.GetBytes(); }
        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD DateTime Element
    /// </summary>
    public sealed class OSDDate : OSD
    {
        private DateTime _dateTime;

        public override OSDType Type => OSDType.Date;

        public OSDDate(DateTime dt)
        {
            _dateTime = dt;
        }

        public override string AsString()
        {
            var format = _dateTime.Millisecond > 0 ? "yyyy-MM-ddTHH:mm:ss.ffZ" : "yyyy-MM-ddTHH:mm:ssZ";
            return _dateTime.ToUniversalTime().ToString(format);
        }

        public override int AsInteger()
        {
            return (int)Utils.DateTimeToUnixTime(_dateTime);
        }

        public override uint AsUInteger()
        {
            return Utils.DateTimeToUnixTime(_dateTime);
        }

        public override long AsLong()
        {
            return Utils.DateTimeToUnixTime(_dateTime);
        }

        public override ulong AsULong()
        {
            return Utils.DateTimeToUnixTime(_dateTime);
        }

        public override byte[] AsBinary()
        {
            TimeSpan ts = _dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Utils.DoubleToBytes(ts.TotalSeconds);
        }

        public override OSD Copy() { return new OSDDate(_dateTime); }
        public override DateTime AsDate() { return _dateTime; }
        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD Uri Element
    /// </summary>
    public sealed class OSDUri : OSD
    {
        private readonly Uri _mUri;

        public override OSDType Type => OSDType.URI;

        public OSDUri(Uri uri)
        {
            _mUri = uri;
        }

        public override string AsString()
        {
            if (_mUri == null) return string.Empty;
            return _mUri.IsAbsoluteUri ? _mUri.AbsoluteUri : _mUri.ToString();
        }

        public override OSD Copy() { return new OSDUri(_mUri); }
        public override Uri AsUri() { return _mUri; }
        public override byte[] AsBinary() { return Encoding.UTF8.GetBytes(AsString()); }
        public override string ToString() { return AsString(); }
    }

    /// <summary>
    /// OSD Binary Element
    /// </summary>
    public sealed class OSDBinary : OSD
    {
        private readonly byte[] _mBytes;

        public override OSDType Type => OSDType.Binary;

        public OSDBinary(byte[] value)
        {
            this._mBytes = value ?? Utils.EmptyBytes;
        }

        public OSDBinary(uint value)
        {
            this._mBytes = new[]
            {
                (byte)((value >> 24) % 256),
                (byte)((value >> 16) % 256),
                (byte)((value >> 8) % 256),
                (byte)(value % 256)
            };
        }

        public OSDBinary(long value)
        {
            this._mBytes = new[]
            {
                (byte)((value >> 56) % 256),
                (byte)((value >> 48) % 256),
                (byte)((value >> 40) % 256),
                (byte)((value >> 32) % 256),
                (byte)((value >> 24) % 256),
                (byte)((value >> 16) % 256),
                (byte)((value >> 8) % 256),
                (byte)(value % 256)
            };
        }

        public OSDBinary(ulong value)
        {
            this._mBytes = new[]
            {
                (byte)((value >> 56) % 256),
                (byte)((value >> 48) % 256),
                (byte)((value >> 40) % 256),
                (byte)((value >> 32) % 256),
                (byte)((value >> 24) % 256),
                (byte)((value >> 16) % 256),
                (byte)((value >> 8) % 256),
                (byte)(value % 256)
            };
        }

        public override OSD Copy() { return new OSDBinary(_mBytes); }
        public override string AsString() { return Convert.ToBase64String(_mBytes); }
        public override byte[] AsBinary() { return _mBytes; }

        public override uint AsUInteger()
        {
            return (uint)(
                (_mBytes[0] << 24) +
                (_mBytes[1] << 16) +
                (_mBytes[2] << 8) +
                (_mBytes[3] << 0));
        }

        public override long AsLong()
        {
            return ((long)_mBytes[0] << 56) +
                   ((long)_mBytes[1] << 48) +
                   ((long)_mBytes[2] << 40) +
                   ((long)_mBytes[3] << 32) +
                   ((long)_mBytes[4] << 24) +
                   ((long)_mBytes[5] << 16) +
                   ((long)_mBytes[6] << 8) +
                   ((long)_mBytes[7] << 0);
        }

        public override ulong AsULong()
        {
            return ((ulong)_mBytes[0] << 56) +
                   ((ulong)_mBytes[1] << 48) +
                   ((ulong)_mBytes[2] << 40) +
                   ((ulong)_mBytes[3] << 32) +
                   ((ulong)_mBytes[4] << 24) +
                   ((ulong)_mBytes[5] << 16) +
                   ((ulong)_mBytes[6] << 8) +
                   ((ulong)_mBytes[7] << 0);
        }

        public override string ToString()
        {
            return Utils.BytesToHexString(_mBytes, null);
        }
    }

    /// <summary>
    /// OSD Map Element
    /// </summary>
    public sealed class OSDMap : OSD, IDictionary<string, OSD>
    {
        private readonly Dictionary<string, OSD> _mMap;

        public override OSDType Type => OSDType.Map;

        public OSDMap()
        {
            _mMap = new Dictionary<string, OSD>();
        }

        public OSDMap(int capacity)
        {
            _mMap = new Dictionary<string, OSD>(capacity);
        }

        public OSDMap(Dictionary<string, OSD> value)
        {
            this._mMap = value ?? new Dictionary<string, OSD>();
        }

        public override bool AsBoolean() { return _mMap.Count > 0; }

        public override string ToString()
        {
            return OSDParser.SerializeJsonString(this, true);
        }

        public override OSD Copy()
        {
            return new OSDMap(new Dictionary<string, OSD>(_mMap));
        }

        #region IDictionary Implementation

        public int Count => _mMap.Count;
        public bool IsReadOnly => false;
        public ICollection<string> Keys => _mMap.Keys;
        public ICollection<OSD> Values => _mMap.Values;

        public OSD this[string key]
        {
            get
            {
                OSD llsd;
                return _mMap.TryGetValue(key, out llsd) ? llsd : new OSD();
            }
            set { _mMap[key] = value; }
        }

        public bool ContainsKey(string key)
        {
            return _mMap.ContainsKey(key);
        }

        public void Add(string key, OSD llsd)
        {
            _mMap.Add(key, llsd);
        }

        public void Add(KeyValuePair<string, OSD> kvp)
        {
            _mMap.Add(kvp.Key, kvp.Value);
        }

        public bool Remove(string key)
        {
            return _mMap.Remove(key);
        }

        public bool TryGetValue(string key, out OSD llsd)
        {
            return _mMap.TryGetValue(key, out llsd);
        }

        public void Clear()
        {
            _mMap.Clear();
        }

        public bool Contains(KeyValuePair<string, OSD> kvp)
        {
            // This is a bizarre function... we don't really implement it
            // properly, hopefully no one wants to use it
            return _mMap.ContainsKey(kvp.Key);
        }

        public void CopyTo(KeyValuePair<string, OSD>[] array, int index)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, OSD> kvp)
        {
            return _mMap.Remove(kvp.Key);
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return _mMap.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, OSD>> IEnumerable<KeyValuePair<string, OSD>>.GetEnumerator()
        {
            return null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _mMap.GetEnumerator();
        }

        #endregion IDictionary Implementation
    }

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
            throw new NotImplementedException();
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

    public partial class OSDParser
    {
        private const string LLSD_BINARY_HEADER = "<? llsd/binary ?>";
        private const string LLSD_XML_HEADER = "<llsd>";
        private const string LLSD_XML_ALT_HEADER = "<?xml";
        private const string LLSD_XML_ALT2_HEADER = "<? llsd/xml ?>";

        public static OSD Deserialize(byte[] data)
        {
            string header = Encoding.ASCII.GetString(data, 0, data.Length >= 17 ? 17 : data.Length);

            try
            {
                string uHeader = Encoding.UTF8.GetString(data, 0, data.Length >= 17 ? 17 : data.Length).TrimStart();
                if (uHeader.StartsWith(LLSD_XML_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                    uHeader.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                    uHeader.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.InvariantCultureIgnoreCase))
                {
                    return DeserializeLLSDXml(data);
                }
            }
            catch { }

            if (header.StartsWith(LLSD_BINARY_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDBinary(data);
            }
            if (header.StartsWith(LLSD_XML_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                header.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                header.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDXml(data);
            }
            return DeserializeJson(Encoding.UTF8.GetString(data));
        }

        public static OSD Deserialize(string data)
        {
            if (data.StartsWith(LLSD_BINARY_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDBinary(Encoding.UTF8.GetBytes(data));
            }
            if (data.StartsWith(LLSD_XML_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                data.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                data.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDXml(data);
            }
            return DeserializeJson(data);
        }

        public static OSD Deserialize(Stream stream)
        {
            if (!stream.CanSeek) { throw new OSDException("Cannot deserialize structured data from unseekable streams"); }

            byte[] headerData = new byte[14];
            stream.Read(headerData, 0, 14);
            stream.Seek(0, SeekOrigin.Begin);
            string header = Encoding.ASCII.GetString(headerData);

            if (header.StartsWith(LLSD_BINARY_HEADER))
                return DeserializeLLSDBinary(stream);
            if (header.StartsWith(LLSD_XML_HEADER) || header.StartsWith(LLSD_XML_ALT_HEADER) || header.StartsWith(LLSD_XML_ALT2_HEADER))
                return DeserializeLLSDXml(stream);
            return DeserializeJson(stream);
        }
    }
}
