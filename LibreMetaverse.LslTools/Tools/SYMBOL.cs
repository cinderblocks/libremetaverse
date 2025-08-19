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

namespace LibreMetaverse.LslTools
{
  public class SYMBOL
  {
    public ObjectList kids = new ObjectList();
    public object m_dollar;
    public int pos;
    public Lexer yylx;
    public Parser yyps;

    protected SYMBOL()
    {
    }

    public SYMBOL(Lexer yyl)
    {
      this.yylx = yyl;
    }

    public SYMBOL(Parser yyp)
    {
      this.yyps = yyp;
      this.yylx = yyp.m_lexer;
    }

    public int Line => this.yylx.sourceLineInfo(this.pos).lineNumber;

    public int Position => this.yylx.sourceLineInfo(this.pos).rawCharPosition;

    public string Pos => this.yylx.Saypos(this.pos);

    public object yylval
    {
      get => this.m_dollar;
      set => this.m_dollar = value;
    }

    public virtual int yynum => 0;

    public virtual bool IsTerminal()
    {
      return false;
    }

    public virtual bool IsAction()
    {
      return false;
    }

    public virtual bool IsCSymbol()
    {
      return false;
    }

    public YyParser yyact
    {
      get
      {
        if (this.yyps != null)
          return this.yyps.m_symbols;
        return (YyParser) null;
      }
    }

    public virtual bool Pass(YyParser syms, int snum, out ParserEntry entry)
    {
      ParsingInfo parsingInfo = (ParsingInfo) syms.symbolInfo[(object) this.yynum];
      if (parsingInfo == null)
      {
        string s = $"No parsinginfo for symbol {(object)this.yyname} {(object)this.yynum}";
        syms.erh.Error((CSToolsException) new CSToolsFatalException(9, this.yylx, this.yyname, s));
      }
      bool flag = parsingInfo.m_parsetable.Contains((object) snum);
      entry = !flag ? (ParserEntry) null : (ParserEntry) parsingInfo.m_parsetable[(object) snum];
      return flag;
    }

    public virtual string yyname => nameof (SYMBOL);

    public override string ToString()
    {
      return this.yyname;
    }

    public virtual bool Matches(string s)
    {
      return false;
    }

    public virtual void Print()
    {
      Console.WriteLine(this.ToString());
    }

    private void ConcreteSyntaxTree(string n)
    {
      if (this is Error)
        Console.WriteLine(n + " " + this.ToString());
      else
        Console.WriteLine(n + "-" + this.ToString());
      int num = 0;
      foreach (SYMBOL kid in this.kids)
        kid.ConcreteSyntaxTree(n + (num++ != this.kids.Count - 1 ? " |" : "  "));
    }

    public virtual void ConcreteSyntaxTree()
    {
      this.ConcreteSyntaxTree("");
    }

    public static implicit operator int(SYMBOL s)
    {
      object dollar;
      for (; (dollar = s.m_dollar) is SYMBOL; s = (SYMBOL) dollar)
      {
        if (dollar == null)
          break;
      }
      try
      {
        return (int) dollar;
      }
      catch (Exception)
      {
        Console.WriteLine("attempt to convert from " + (object) s.m_dollar.GetType());
        throw;
      }
    }
  }
}
