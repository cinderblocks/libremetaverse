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

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;

namespace LibreMetaverse.LSLTools.Tools
{
    public class Serialiser
    {
        private static Hashtable tps = new Hashtable();
        private static Hashtable srs = new Hashtable();
        private Hashtable obs = new Hashtable();
        private int id = 100;
        private const string Version = "4.5";
        private TextWriter f;
        private int[] b;
        private int pos;
        private int cl;

        public Serialiser(TextWriter ff)
        {
            f = ff;
        }

        public Serialiser(int[] bb)
        {
            b = bb;
        }

        static Serialiser()
        {
            srs[SerType.Null] = new ObjectSerialiser(NullSerialise);
            tps[typeof(int)] = SerType.Int;
            srs[SerType.Int] = new ObjectSerialiser(IntSerialise);
            tps[typeof(string)] = SerType.String;
            srs[SerType.String] = new ObjectSerialiser(StringSerialise);
            tps[typeof(Hashtable)] = SerType.Hashtable;
            srs[SerType.Hashtable] = new ObjectSerialiser(HashtableSerialise);
            tps[typeof(char)] = SerType.Char;
            srs[SerType.Char] = new ObjectSerialiser(CharSerialise);
            tps[typeof(bool)] = SerType.Bool;
            srs[SerType.Bool] = new ObjectSerialiser(BoolSerialise);
            tps[typeof(Encoding)] = SerType.Encoding;
            srs[SerType.Encoding] = new ObjectSerialiser(EncodingSerialise);
            tps[typeof(UnicodeCategory)] = SerType.UnicodeCategory;
            srs[SerType.UnicodeCategory] = new ObjectSerialiser(UnicodeCategorySerialise);
            tps[typeof(CSymbol.SymType)] = SerType.Symtype;
            srs[SerType.Symtype] = new ObjectSerialiser(SymtypeSerialise);
            tps[typeof(Charset)] = SerType.Charset;
            srs[SerType.Charset] = new ObjectSerialiser(Charset.Serialise);
            tps[typeof(TokClassDef)] = SerType.TokClassDef;
            srs[SerType.TokClassDef] = new ObjectSerialiser(TokClassDef.Serialise);
            tps[typeof(Dfa)] = SerType.Dfa;
            srs[SerType.Dfa] = new ObjectSerialiser(Dfa.Serialise);
            tps[typeof(ResWds)] = SerType.ResWds;
            srs[SerType.ResWds] = new ObjectSerialiser(ResWds.Serialise);
            tps[typeof(Dfa.Action)] = SerType.Action;
            srs[SerType.Action] = new ObjectSerialiser(Dfa.Action.Serialise);
            tps[typeof(ParserOldAction)] = SerType.ParserOldAction;
            srs[SerType.ParserOldAction] = new ObjectSerialiser(ParserOldAction.Serialise);
            tps[typeof(ParserSimpleAction)] = SerType.ParserSimpleAction;
            srs[SerType.ParserSimpleAction] = new ObjectSerialiser(ParserSimpleAction.Serialise);
            tps[typeof(ParserShift)] = SerType.ParserShift;
            srs[SerType.ParserShift] = new ObjectSerialiser(ParserShift.Serialise);
            tps[typeof(ParserReduce)] = SerType.ParserReduce;
            srs[SerType.ParserReduce] = new ObjectSerialiser(ParserReduce.Serialise);
            tps[typeof(ParseState)] = SerType.ParseState;
            srs[SerType.ParseState] = new ObjectSerialiser(ParseState.Serialise);
            tps[typeof(ParsingInfo)] = SerType.ParsingInfo;
            srs[SerType.ParsingInfo] = new ObjectSerialiser(ParsingInfo.Serialise);
            tps[typeof(CSymbol)] = SerType.CSymbol;
            srs[SerType.CSymbol] = new ObjectSerialiser(CSymbol.Serialise);
            tps[typeof(Literal)] = SerType.Literal;
            srs[SerType.Literal] = new ObjectSerialiser(Literal.Serialise);
            tps[typeof(Production)] = SerType.Production;
            srs[SerType.Production] = new ObjectSerialiser(Production.Serialise);
            tps[typeof(EOF)] = SerType.EOF;
            srs[SerType.EOF] = new ObjectSerialiser(EOF.Serialise);
        }

        public void VersionCheck()
        {
            if (Encode)
            {
                Serialise("4.5");
            }
            else
            {
                string str = Deserialise() as string;
                if (str == null)
                    throw new LslToolsException("Serialisation error - found data from version 4.4 or earlier");
                if (str != "4.5")
                    throw new LslToolsException("Serialisation error - expected version 4.5, found data from version " + str);
            }
        }

        public bool Encode => f != null;

        private void _Write(SerType t)
        {
            _Write((int)t);
        }

        public void _Write(int i)
        {
            if (cl == 5)
            {
                f.WriteLine();
                cl = 0;
            }
            ++cl;
            f.Write(i);
            f.Write(",");
        }

        public int _Read()
        {
            return b[pos++];
        }

        private static object NullSerialise(object o, Serialiser s)
        {
            return null;
        }

        private static object IntSerialise(object o, Serialiser s)
        {
            if (!s.Encode)
                return s._Read();
            s._Write((int)o);
            return null;
        }

        private static object StringSerialise(object o, Serialiser s)
        {
            if (s == null)
                return "";
            var encoding = new UnicodeEncoding();
            if (s.Encode)
            {
                byte[] bytes = encoding.GetBytes((string)o);
                s._Write(bytes.Length);
                foreach (var t in bytes)
                    s._Write(t);

                return null;
            }
            int count = s._Read();
            byte[] bytes1 = new byte[count];
            for (int index = 0; index < count; ++index)
                bytes1[index] = (byte)s._Read();
            return encoding.GetString(bytes1, 0, count);
        }

        private static object HashtableSerialise(object o, Serialiser s)
        {
            if (s == null)
                return new Hashtable();
            Hashtable hashtable = (Hashtable)o;
            if (s.Encode)
            {
                s._Write(hashtable.Count);
                foreach (DictionaryEntry dictionaryEntry in hashtable)
                {
                    s.Serialise(dictionaryEntry.Key);
                    s.Serialise(dictionaryEntry.Value);
                }
                return null;
            }
            int num = s._Read();
            for (int index1 = 0; index1 < num; ++index1)
            {
                object index2 = s.Deserialise();
                object obj = s.Deserialise();
                hashtable[index2] = obj;
            }
            return hashtable;
        }

        private static object CharSerialise(object o, Serialiser s)
        {
            var encoding = new UnicodeEncoding();
            if (s.Encode)
            {
                byte[] bytes = encoding.GetBytes(new string((char)o, 1));
                s._Write(bytes[0]);
                s._Write(bytes[1]);
                return null;
            }
            byte[] bytes1 = new byte[2]
            {
        (byte) s._Read(),
        (byte) s._Read()
            };
            return encoding.GetString(bytes1, 0, 2)[0];
        }

        private static object BoolSerialise(object o, Serialiser s)
        {
            if (!s.Encode)
                return s._Read() != 0;
            s._Write(!(bool)o ? 0 : 1);
            return null;
        }

        private static object EncodingSerialise(object o, Serialiser s)
        {
            if (s.Encode)
            {
                Encoding encoding = (Encoding)o;
                s.Serialise(encoding.WebName);
                return null;
            }
            string name = (string)s.Deserialise();
            string str1 = name;
            if (str1 != null)
            {
                string str2 = string.IsInterned(str1);
                if (str2 == (object)"us-ascii")
                    return Encoding.ASCII;
                if (str2 == (object)"utf-16")
                    return Encoding.Unicode;
                if (str2 == (object)"utf-7")
#pragma warning disable CS0618
                    return Encoding.UTF7;
#pragma warning restore CS0618
                if (str2 == (object)"utf-8")
                    return Encoding.UTF8;
            }
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (Exception ex)
            {
                throw new LslToolsException("Unknown encoding " + ex.Message);
            }
        }

        private static object UnicodeCategorySerialise(object o, Serialiser s)
        {
            if (!s.Encode)
                return (UnicodeCategory)s._Read();
            s._Write((int)o);
            return null;
        }

        private static object SymtypeSerialise(object o, Serialiser s)
        {
            if (!s.Encode)
                return (CSymbol.SymType)s._Read();
            s._Write((int)o);
            return null;
        }

        public void Serialise(object o)
        {
            if (o == null)
                _Write(SerType.Null);
            else if (o is Encoding)
            {
                _Write(SerType.Encoding);
                EncodingSerialise(o, this);
            }
            else
            {
                Type type = o.GetType();
                if (type.IsClass)
                {
                    object ob = obs[o];
                    if (ob != null)
                    {
                        _Write((int)ob);
                        return;
                    }
                    int i = ++id;
                    _Write(i);
                    obs[o] = i;
                }
                object tp = tps[type];
                if (tp == null)
                    throw new LslToolsException("unknown type " + type.FullName);
                SerType t = (SerType)tp;
                _Write(t);
                object obj = ((ObjectSerialiser)srs[t])(o, this);
            }
        }

        public object Deserialise()
        {
            int num1 = _Read();
            int num2 = 0;
            if (num1 > 100)
            {
                num2 = num1;
                if (num2 <= obs.Count + 100)
                    return obs[num2];
                num1 = _Read();
            }
            ObjectSerialiser sr = (ObjectSerialiser)srs[(SerType)num1];
            if (sr == null)
                throw new LslToolsException("unknown type " + num1);
            if (num2 <= 0)
                return sr(null, this);
            object o = sr(null, null);
            obs[num2] = o;
            object obj = sr(o, this);
            obs[num2] = obj;
            return obj;
        }

        private enum SerType
        {
            Null,
            Int,
            Bool,
            Char,
            String,
            Hashtable,
            Encoding,
            UnicodeCategory,
            Symtype,
            Charset,
            TokClassDef,
            Action,
            Dfa,
            ResWds,
            ParserOldAction,
            ParserSimpleAction,
            ParserShift,
            ParserReduce,
            ParseState,
            ParsingInfo,
            CSymbol,
            Literal,
            Production,
            EOF,
        }

        private delegate object ObjectSerialiser(object o, Serialiser s);
    }
}
