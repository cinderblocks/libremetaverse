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
  public class TOKEN : SYMBOL
  {
    private int num = 1;
    private string m_str;

    public TOKEN(Parser yyp)
      : base(yyp)
    {
    }

    public TOKEN(Lexer yyl)
      : base(yyl)
    {
      if (yyl == null)
        return;
      this.m_str = yyl.yytext;
    }

    public TOKEN(Lexer yyl, string s)
      : base(yyl)
    {
      this.m_str = s;
    }

    protected TOKEN()
    {
    }

    public string yytext
    {
      get => this.m_str;
      set => this.m_str = value;
    }

    public override bool IsTerminal()
    {
      return true;
    }

    public override bool Pass(YyParser syms, int snum, out ParserEntry entry)
    {
      if (this.yynum == 1)
      {
        Literal literal = (Literal) syms.literals[(object) this.yytext];
        if (literal != null)
          this.num = literal.m_yynum;
      }
      ParsingInfo parsingInfo = (ParsingInfo) syms.symbolInfo[(object) this.yynum];
      if (parsingInfo == null)
      {
        string s = string.Format("Parser does not recognise literal {0}", (object) this.yytext);
        syms.erh.Error((CSToolsException) new CSToolsFatalException(9, this.yylx, this.yyname, s));
      }
      bool flag = parsingInfo.m_parsetable.Contains((object) snum);
      entry = !flag ? (ParserEntry) null : (ParserEntry) parsingInfo.m_parsetable[(object) snum];
      return flag;
    }

    public override string yyname => nameof (TOKEN);

    public override int yynum => this.num;

    public override bool Matches(string s)
    {
      return s.Equals(this.m_str);
    }

    public override string ToString()
    {
      return this.yyname + "<" + this.yytext + ">";
    }

    public override void Print()
    {
      Console.WriteLine(this.ToString());
    }
  }
}
