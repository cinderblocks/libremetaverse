/*
 * Copyright (c) 2019-2022, Sjofn LLC
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

namespace LibreMetaverse.LslTools
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
      this.f = ff;
    }

    public Serialiser(int[] bb)
    {
      this.b = bb;
    }

    static Serialiser()
    {
      Serialiser.srs[(object) Serialiser.SerType.Null] = (object) new Serialiser.ObjectSerialiser(Serialiser.NullSerialise);
      Serialiser.tps[(object) typeof (int)] = (object) Serialiser.SerType.Int;
      Serialiser.srs[(object) Serialiser.SerType.Int] = (object) new Serialiser.ObjectSerialiser(Serialiser.IntSerialise);
      Serialiser.tps[(object) typeof (string)] = (object) Serialiser.SerType.String;
      Serialiser.srs[(object) Serialiser.SerType.String] = (object) new Serialiser.ObjectSerialiser(Serialiser.StringSerialise);
      Serialiser.tps[(object) typeof (Hashtable)] = (object) Serialiser.SerType.Hashtable;
      Serialiser.srs[(object) Serialiser.SerType.Hashtable] = (object) new Serialiser.ObjectSerialiser(Serialiser.HashtableSerialise);
      Serialiser.tps[(object) typeof (char)] = (object) Serialiser.SerType.Char;
      Serialiser.srs[(object) Serialiser.SerType.Char] = (object) new Serialiser.ObjectSerialiser(Serialiser.CharSerialise);
      Serialiser.tps[(object) typeof (bool)] = (object) Serialiser.SerType.Bool;
      Serialiser.srs[(object) Serialiser.SerType.Bool] = (object) new Serialiser.ObjectSerialiser(Serialiser.BoolSerialise);
      Serialiser.tps[(object) typeof (Encoding)] = (object) Serialiser.SerType.Encoding;
      Serialiser.srs[(object) Serialiser.SerType.Encoding] = (object) new Serialiser.ObjectSerialiser(Serialiser.EncodingSerialise);
      Serialiser.tps[(object) typeof (UnicodeCategory)] = (object) Serialiser.SerType.UnicodeCategory;
      Serialiser.srs[(object) Serialiser.SerType.UnicodeCategory] = (object) new Serialiser.ObjectSerialiser(Serialiser.UnicodeCategorySerialise);
      Serialiser.tps[(object) typeof (CSymbol.SymType)] = (object) Serialiser.SerType.Symtype;
      Serialiser.srs[(object) Serialiser.SerType.Symtype] = (object) new Serialiser.ObjectSerialiser(Serialiser.SymtypeSerialise);
      Serialiser.tps[(object) typeof (Charset)] = (object) Serialiser.SerType.Charset;
      Serialiser.srs[(object) Serialiser.SerType.Charset] = (object) new Serialiser.ObjectSerialiser(Charset.Serialise);
      Serialiser.tps[(object) typeof (TokClassDef)] = (object) Serialiser.SerType.TokClassDef;
      Serialiser.srs[(object) Serialiser.SerType.TokClassDef] = (object) new Serialiser.ObjectSerialiser(TokClassDef.Serialise);
      Serialiser.tps[(object) typeof (Dfa)] = (object) Serialiser.SerType.Dfa;
      Serialiser.srs[(object) Serialiser.SerType.Dfa] = (object) new Serialiser.ObjectSerialiser(Dfa.Serialise);
      Serialiser.tps[(object) typeof (ResWds)] = (object) Serialiser.SerType.ResWds;
      Serialiser.srs[(object) Serialiser.SerType.ResWds] = (object) new Serialiser.ObjectSerialiser(ResWds.Serialise);
      Serialiser.tps[(object) typeof (Dfa.Action)] = (object) Serialiser.SerType.Action;
      Serialiser.srs[(object) Serialiser.SerType.Action] = (object) new Serialiser.ObjectSerialiser(Dfa.Action.Serialise);
      Serialiser.tps[(object) typeof (ParserOldAction)] = (object) Serialiser.SerType.ParserOldAction;
      Serialiser.srs[(object) Serialiser.SerType.ParserOldAction] = (object) new Serialiser.ObjectSerialiser(ParserOldAction.Serialise);
      Serialiser.tps[(object) typeof (ParserSimpleAction)] = (object) Serialiser.SerType.ParserSimpleAction;
      Serialiser.srs[(object) Serialiser.SerType.ParserSimpleAction] = (object) new Serialiser.ObjectSerialiser(ParserSimpleAction.Serialise);
      Serialiser.tps[(object) typeof (ParserShift)] = (object) Serialiser.SerType.ParserShift;
      Serialiser.srs[(object) Serialiser.SerType.ParserShift] = (object) new Serialiser.ObjectSerialiser(ParserShift.Serialise);
      Serialiser.tps[(object) typeof (ParserReduce)] = (object) Serialiser.SerType.ParserReduce;
      Serialiser.srs[(object) Serialiser.SerType.ParserReduce] = (object) new Serialiser.ObjectSerialiser(ParserReduce.Serialise);
      Serialiser.tps[(object) typeof (ParseState)] = (object) Serialiser.SerType.ParseState;
      Serialiser.srs[(object) Serialiser.SerType.ParseState] = (object) new Serialiser.ObjectSerialiser(ParseState.Serialise);
      Serialiser.tps[(object) typeof (ParsingInfo)] = (object) Serialiser.SerType.ParsingInfo;
      Serialiser.srs[(object) Serialiser.SerType.ParsingInfo] = (object) new Serialiser.ObjectSerialiser(ParsingInfo.Serialise);
      Serialiser.tps[(object) typeof (CSymbol)] = (object) Serialiser.SerType.CSymbol;
      Serialiser.srs[(object) Serialiser.SerType.CSymbol] = (object) new Serialiser.ObjectSerialiser(CSymbol.Serialise);
      Serialiser.tps[(object) typeof (Literal)] = (object) Serialiser.SerType.Literal;
      Serialiser.srs[(object) Serialiser.SerType.Literal] = (object) new Serialiser.ObjectSerialiser(Literal.Serialise);
      Serialiser.tps[(object) typeof (Production)] = (object) Serialiser.SerType.Production;
      Serialiser.srs[(object) Serialiser.SerType.Production] = (object) new Serialiser.ObjectSerialiser(Production.Serialise);
      Serialiser.tps[(object) typeof (EOF)] = (object) Serialiser.SerType.EOF;
      Serialiser.srs[(object) Serialiser.SerType.EOF] = (object) new Serialiser.ObjectSerialiser(EOF.Serialise);
    }

    public void VersionCheck()
    {
      if (this.Encode)
      {
        this.Serialise((object) "4.5");
      }
      else
      {
        string str = this.Deserialise() as string;
        if (str == null)
          throw new Exception("Serialisation error - found data from version 4.4 or earlier");
        if (str != "4.5")
          throw new Exception("Serialisation error - expected version 4.5, found data from version " + str);
      }
    }

    public bool Encode => this.f != null;

    private void _Write(Serialiser.SerType t)
    {
      this._Write((int) t);
    }

    public void _Write(int i)
    {
      if (this.cl == 5)
      {
        this.f.WriteLine();
        this.cl = 0;
      }
      ++this.cl;
      this.f.Write(i);
      this.f.Write(",");
    }

    public int _Read()
    {
      return this.b[this.pos++];
    }

    private static object NullSerialise(object o, Serialiser s)
    {
      return (object) null;
    }

    private static object IntSerialise(object o, Serialiser s)
    {
      if (!s.Encode)
        return (object) s._Read();
      s._Write((int) o);
      return (object) null;
    }

    private static object StringSerialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) "";
      Encoding encoding = (Encoding) new UnicodeEncoding();
      if (s.Encode)
      {
        byte[] bytes = encoding.GetBytes((string) o);
        s._Write(bytes.Length);
        foreach (var t in bytes)
            s._Write((int) t);

        return (object) null;
      }
      int count = s._Read();
      byte[] bytes1 = new byte[count];
      for (int index = 0; index < count; ++index)
        bytes1[index] = (byte) s._Read();
      return (object) encoding.GetString(bytes1, 0, count);
    }

    private static object HashtableSerialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new Hashtable();
      Hashtable hashtable = (Hashtable) o;
      if (s.Encode)
      {
        s._Write(hashtable.Count);
        foreach (DictionaryEntry dictionaryEntry in hashtable)
        {
          s.Serialise(dictionaryEntry.Key);
          s.Serialise(dictionaryEntry.Value);
        }
        return (object) null;
      }
      int num = s._Read();
      for (int index1 = 0; index1 < num; ++index1)
      {
        object index2 = s.Deserialise();
        object obj = s.Deserialise();
        hashtable[index2] = obj;
      }
      return (object) hashtable;
    }

    private static object CharSerialise(object o, Serialiser s)
    {
      Encoding encoding = (Encoding) new UnicodeEncoding();
      if (s.Encode)
      {
        byte[] bytes = encoding.GetBytes(new string((char) o, 1));
        s._Write((int) bytes[0]);
        s._Write((int) bytes[1]);
        return (object) null;
      }
      byte[] bytes1 = new byte[2]
      {
        (byte) s._Read(),
        (byte) s._Read()
      };
      return (object) encoding.GetString(bytes1, 0, 2)[0];
    }

    private static object BoolSerialise(object o, Serialiser s)
    {
      if (!s.Encode)
        return (object) (s._Read() != 0);
      s._Write(!(bool) o ? 0 : 1);
      return (object) null;
    }

    private static object EncodingSerialise(object o, Serialiser s)
    {
      if (s.Encode)
      {
        Encoding encoding = (Encoding) o;
        s.Serialise((object) encoding.WebName);
        return (object) null;
      }
      string name = (string) s.Deserialise();
      string str1 = name;
      if (str1 != null)
      {
        string str2 = string.IsInterned(str1);
        if ((object) str2 == (object) "us-ascii")
          return (object) Encoding.ASCII;
        if ((object) str2 == (object) "utf-16")
          return (object) Encoding.Unicode;
        if ((object) str2 == (object) "utf-7")
#pragma warning disable CS0618
          return (object) Encoding.UTF7;
#pragma warning restore CS0618
        if ((object) str2 == (object) "utf-8")
          return (object) Encoding.UTF8;
      }
      try
      {
        return (object) Encoding.GetEncoding(name);
      }
      catch (Exception)
      {
        throw new Exception("Unknown encoding");
      }
    }

    private static object UnicodeCategorySerialise(object o, Serialiser s)
    {
      if (!s.Encode)
        return (object) (UnicodeCategory) s._Read();
      s._Write((int) o);
      return (object) null;
    }

    private static object SymtypeSerialise(object o, Serialiser s)
    {
      if (!s.Encode)
        return (object) (CSymbol.SymType) s._Read();
      s._Write((int) o);
      return (object) null;
    }

    public void Serialise(object o)
    {
      if (o == null)
        this._Write(Serialiser.SerType.Null);
      else if (o is Encoding)
      {
        this._Write(Serialiser.SerType.Encoding);
        Serialiser.EncodingSerialise(o, this);
      }
      else
      {
        Type type = o.GetType();
        if (type.IsClass)
        {
          object ob = this.obs[o];
          if (ob != null)
          {
            this._Write((int) ob);
            return;
          }
          int i = ++this.id;
          this._Write(i);
          this.obs[o] = (object) i;
        }
        object tp = Serialiser.tps[(object) type];
        if (tp == null)
          throw new Exception("unknown type " + type.FullName);
        Serialiser.SerType t = (Serialiser.SerType) tp;
        this._Write(t);
        object obj = ((Serialiser.ObjectSerialiser) Serialiser.srs[(object) t])(o, this);
      }
    }

    public object Deserialise()
    {
      int num1 = this._Read();
      int num2 = 0;
      if (num1 > 100)
      {
        num2 = num1;
        if (num2 <= this.obs.Count + 100)
          return this.obs[(object) num2];
        num1 = this._Read();
      }
      Serialiser.ObjectSerialiser sr = (Serialiser.ObjectSerialiser) Serialiser.srs[(object) (Serialiser.SerType) num1];
      if (sr == null)
        throw new Exception("unknown type " + (object) num1);
      if (num2 <= 0)
        return sr((object) null, this);
      object o = sr((object) null, (Serialiser) null);
      this.obs[(object) num2] = o;
      object obj = sr(o, this);
      this.obs[(object) num2] = obj;
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
