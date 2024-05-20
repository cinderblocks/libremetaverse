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
using System.Text;

namespace LibreMetaverse.LslTools
{
  public class Charset
  {
    internal Hashtable m_chars = new Hashtable();
    internal UnicodeCategory m_cat;
    internal char m_generic;

    private Charset()
    {
    }

    internal Charset(UnicodeCategory cat)
    {
      this.m_cat = cat;
      this.m_generic = char.MinValue;
      while (char.GetUnicodeCategory(this.m_generic) != cat)
        ++this.m_generic;
      this.m_chars[(object) this.m_generic] = (object) true;
    }

    public static Encoding GetEncoding(string enc, ref bool toupper, ErrorHandler erh)
    {
      string str1 = enc;
      if (str1 != null)
      {
        string str2 = string.IsInterned(str1);
        if ((object) str2 == (object) "")
          return Encoding.Default;
        if ((object) str2 == (object) "ASCII")
          return Encoding.ASCII;
        if ((object) str2 == (object) "ASCIICAPS")
        {
          toupper = true;
          return Encoding.ASCII;
        }
        if ((object) str2 == (object) "UTF7")
#pragma warning disable CS0618
            return Encoding.UTF7;
#pragma warning restore CS0618
        if ((object) str2 == (object) "UTF8")
          return Encoding.UTF8;
        if ((object) str2 == (object) "Unicode")
          return Encoding.Unicode;
      }
      try
      {
        if (char.IsDigit(enc[0]))
          return Encoding.GetEncoding(int.Parse(enc));
        return Encoding.GetEncoding(enc);
      }
      catch (Exception)
      {
        erh.Error(new CSToolsException(43, "Warning: Encoding " + enc + " unknown: ignored"));
      }
      return Encoding.ASCII;
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new Charset();
      Charset charset = (Charset) o;
      if (s.Encode)
      {
        s.Serialise((object) (int) charset.m_cat);
        s.Serialise((object) charset.m_generic);
        s.Serialise((object) charset.m_chars);
        return (object) null;
      }
      charset.m_cat = (UnicodeCategory) s.Deserialise();
      charset.m_generic = (char) s.Deserialise();
      charset.m_chars = (Hashtable) s.Deserialise();
      return (object) charset;
    }
  }
}
