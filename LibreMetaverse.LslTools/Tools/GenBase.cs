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
using System.IO;
using System.Text;

namespace LibreMetaverse.LslTools
{
  public class GenBase
  {
    protected Encoding m_scriptEncoding = Encoding.ASCII;
    public string m_outname = "tokens";
    public int LastSymbol = 2;
    public ErrorHandler erh;
    public TextWriter m_outFile;
    protected bool toupper;
    public Production m_prod;

    protected GenBase(ErrorHandler eh)
    {
      erh = eh;
    }

    protected string ScriptEncoding
    {
      set => m_scriptEncoding = Charset.GetEncoding(value, ref toupper, erh);
    }

    protected int Braces(int a, string b, ref int p, int max)
    {
      int num1 = a;
      int num2 = 0;
      while (p < max)
      {
        if (b[p] == '\\')
          ++p;
        else if (num2 == 0 && b[p] == '{')
          ++num1;
        else if (num2 == 0 && b[p] == '}')
        {
          if (--num1 == 0)
          {
            ++p;
            break;
          }
        }
        else if (b[p] == num2)
          num2 = 0;
        else if (num2 == 0 && (b[p] == '\'' || b[p] == '"'))
          num2 = b[p];
        ++p;
      }
      return num1;
    }

    protected string ToBraceIfFound(ref string buf, ref int p, ref int max, CsReader inf)
    {
      int num = p;
      int a = Braces(0, buf, ref p, max);
      string str1 = buf.Substring(num, p - num);
      while (inf != null && a > 0)
      {
        buf = inf.ReadLine();
        if (buf == null || p == 0)
          Error(47, num, "EOF in action or class def??");
        max = buf.Length;
        p = 0;
        string str2 = str1 + '\n';
        a = Braces(a, buf, ref p, max);
        str1 = str2 + buf.Substring(0, p);
      }
      return str1;
    }

    public bool White(string buf, ref int offset, int max)
    {
      while (offset < max && (buf[offset] == ' ' || buf[offset] == '\t'))
        ++offset;
      return offset < max;
    }

    public bool NonWhite(string buf, ref int offset, int max)
    {
      while (offset < max && buf[offset] != ' ' && buf[offset] != '\t')
        ++offset;
      return offset < max;
    }

    public int EmitClassDefin(
      string b,
      ref int p,
      int max,
      CsReader inf,
      string defbas,
      out string bas,
      out string name,
      bool lx)
    {
      bool flag = false;
      name = "";
      bas = defbas;
      if (lx)
        NonWhite(b, ref p, max);
      White(b, ref p, max);
      while (p < max && b[p] != '{' && (b[p] != ':' && b[p] != ';') && (b[p] != ' ' && b[p] != '\t' && b[p] != '\n'))
      {
        name += (string) (object) b[p];
        ++p;
      }
      White(b, ref p, max);
      if (b[p] == ':')
      {
        ++p;
        White(b, ref p, max);
        bas = "";
        while (p < max && b[p] != ' ' && (b[p] != '{' && b[p] != '\t') && (b[p] != ';' && b[p] != '\n'))
        {
          bas += (string) (object) b[p];
          ++p;
        }
      }
      int yynum = new TokClassDef(this, name, bas).m_yynum;
      m_outFile.WriteLine("//%+{0}+{1}", name, yynum);
      m_outFile.Write("public class ");
      m_outFile.Write(name);
      m_outFile.Write(" : " + bas);
      m_outFile.WriteLine("{");
      do
      {
        if (p >= max)
        {
          b += inf.ReadLine();
          max = b.Length;
        }
        White(b, ref p, max);
      }
      while (p >= max);
      if (b[p] != ';')
      {
        cs0syntax cs0syntax = new cs0syntax(new yycs0syntax(), erh);
        ((cs0tokens) cs0syntax.m_lexer).Out = m_outname;
        cs0syntax.Cls = name;
        cs0syntax.Out = m_outname;
        if (lx)
        {
          cs0syntax.Ctx = "Lexer yyl";
          cs0syntax.Par = "yym";
        }
        else
        {
          cs0syntax.Ctx = "Parser yyp";
          cs0syntax.Par = "yyq";
        }
        string braceIfFound = ToBraceIfFound(ref b, ref p, ref max, inf);
        TOKEN token = null;
        try
        {
          token = (TOKEN) cs0syntax.Parse(braceIfFound);
        }
        catch (Exception)
        {
        }
        if (token == null)
        {
          Error(48, p, "Bad class definition for " + name);
          return -1;
        }
        token.yytext = token.yytext.Replace("yyq", "((" + m_outname + ")yyp)");
        token.yytext = token.yytext.Replace("yym", "((" + m_outname + ")yyl)");
        string yytext = token.yytext;
        char[] chArray = new char[1]{ '\n' };
        foreach (string str in yytext.Split(chArray))
          m_outFile.WriteLine(str);
        flag = cs0syntax.defconseen;
      }
      m_outFile.WriteLine("public override string yyname { get { return \"" + name + "\"; }}");
      m_outFile.WriteLine("public override int yynum { get { return " + yynum + "; }}");
      if (!flag)
      {
        if (lx)
          m_outFile.Write("public " + name + "(Lexer yyl):base(yyl){}");
        else
          m_outFile.Write("public " + name + "(Parser yyp):base(yyp){}");
      }
      m_outFile.WriteLine("}");
      return yynum;
    }

    public void Error(int n, int p, string str)
    {
      Console.WriteLine("" + sourceLineInfo(p) + ": " + str);
      if (m_outFile != null)
      {
        m_outFile.WriteLine();
        m_outFile.WriteLine("#error Generator failed earlier. Fix the parser script and run ParserGenerator again.");
      }
      erh.Error(new CSToolsFatalException(n, sourceLineInfo(p), "", str));
    }

    public virtual SourceLineInfo sourceLineInfo(int pos)
    {
      return new SourceLineInfo(pos);
    }

    public int line(int pos)
    {
      return sourceLineInfo(pos).lineNumber;
    }

    public int position(int pos)
    {
      return sourceLineInfo(pos).rawCharPosition;
    }

    public string Saypos(int pos)
    {
      return sourceLineInfo(pos).ToString();
    }
  }
}
