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

namespace LibreMetaverse.LSLTools.Tools
{
  public class ResWds
  {
    public Hashtable m_wds = new Hashtable();
    public bool m_upper;

    public static ResWds New(TokensGen tks, string str)
    {
      ResWds resWds = new ResWds();
      str = str.Trim();
      if (str[0] == 'U')
      {
        resWds.m_upper = true;
        str = str.Substring(1).Trim();
      }
      if (str[0] == '{' && str[str.Length - 1] == '}')
      {
        str = str.Substring(1, str.Length - 2).Trim();
        string str1 = str;
        char[] chArray = new char[1]{ ',' };
        foreach (string str2 in str1.Split(chArray))
        {
          string str3 = str2.Trim();
          string name = str3;
          int num = str3.IndexOf(' ');
          if (num > 0)
          {
            name = str3.Substring(num).Trim();
            str3 = str3.Substring(0, num);
          }
          resWds.m_wds[str3] = name;
          if (tks.m_tokens.tokens[name] == null)
          {
            TokClassDef tokClassDef = new TokClassDef(tks, name, "TOKEN");
            tks.m_outFile.WriteLine("//%{0}+{1}", name, tokClassDef.m_yynum);
            tks.m_outFile.Write("public class {0} : TOKEN", name);
            tks.m_outFile.WriteLine("{ public override string yyname { get { return \"" + name + "\";}}");
            tks.m_outFile.WriteLine("public override int yynum { get { return " + tokClassDef.m_yynum + "; }}");
            tks.m_outFile.WriteLine(" public " + name + "(Lexer yyl):base(yyl) {}}");
          }
        }
        return resWds;
      }
      tks.m_tokens.erh.Error(new CSToolsException(47, "bad ResWds element"));
      return null;
    }

    public void Check(Lexer yyl, ref TOKEN tok)
    {
      string str = tok.yytext;
      if (m_upper)
        str = str.ToUpper();
      object wd = m_wds[str];
      if (wd == null)
        return;
      tok = (TOKEN) Tfactory.create((string) wd, yyl);
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return new ResWds();
      ResWds resWds = (ResWds) o;
      if (s.Encode)
      {
        s.Serialise(resWds.m_upper);
        s.Serialise(resWds.m_wds);
        return null;
      }
      resWds.m_upper = (bool) s.Deserialise();
      resWds.m_wds = (Hashtable) s.Deserialise();
      return resWds;
    }
  }
}
