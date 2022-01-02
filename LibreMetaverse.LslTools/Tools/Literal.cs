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

namespace LibreMetaverse.LslTools
{
  public class Literal : CSymbol
  {
    public Literal(SymbolsGen yyp)
      : base(yyp)
    {
      this.m_symtype = CSymbol.SymType.terminal;
    }

    private Literal()
    {
    }

    public override CSymbol Resolve()
    {
      int length = this.yytext.Length;
      string str = "";
      for (int index = 1; index + 1 < length; ++index)
      {
        if (this.yytext[index] == '\\')
        {
          if (index + 1 < length)
            ++index;
          if (this.yytext[index] >= '0' && this.yytext[index] <= '7')
          {
            int num;
            for (num = (int) this.yytext[index++] - 48; index < length && this.yytext[index] >= '0' && this.yytext[index] <= '7'; ++index)
              num = num * 8 + (int) this.yytext[index] - 48;
            str += (string) (object) (char) num;
          }
          else
          {
            char ch = this.yytext[index];
            switch (ch)
            {
              case 'r':
                str += (string) (object) '\r';
                continue;
              case 't':
                str += (string) (object) '\t';
                continue;
              default:
                str = ch == 'n' ? str + (object) '\n' : str + (object) this.yytext[index];
                continue;
            }
          }
        }
        else
          str += (string) (object) this.yytext[index];
      }
      this.yytext = str;
      CSymbol literal = (CSymbol) this.m_parser.m_symbols.literals[(object) this.yytext];
      if (literal != null)
        return literal;
      this.m_yynum = ++this.m_parser.LastSymbol;
      this.m_parser.m_symbols.literals[(object) this.yytext] = (object) this;
      this.m_parser.m_symbols.symbolInfo[(object) this.m_yynum] = (object) new ParsingInfo(this.yytext, this.m_yynum);
      return (CSymbol) this;
    }

    public bool CouldStart(CSymbol nonterm)
    {
      return false;
    }

    public override string TypeStr()
    {
      return "TOKEN";
    }

    public new static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new Literal();
      return CSymbol.Serialise(o, s);
    }
  }
}
