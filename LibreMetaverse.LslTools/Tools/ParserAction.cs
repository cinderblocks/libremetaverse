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
  public class ParserAction : CSymbol
  {
    public CSymbol m_sym;
    public int m_len;

    public ParserAction(SymbolsGen yyp)
      : base(yyp)
    {
    }

    protected ParserAction()
    {
    }

    public virtual SYMBOL Action(Parser yyp)
    {
      SYMBOL symbol1 = (SYMBOL) Sfactory.create(this.m_sym.yytext, yyp);
      if (symbol1.yyname == this.m_sym.yytext)
      {
        SYMBOL symbol2 = yyp.StackAt(this.m_len - 1).m_value;
        symbol1.m_dollar = this.m_len == 0 || symbol2 == null ? (object) null : symbol2.m_dollar;
      }
      return symbol1;
    }

    public override void Print()
    {
      Console.Write(this.m_sym.yytext);
    }

    public override bool IsAction()
    {
      return true;
    }

    public virtual int ActNum()
    {
      return 0;
    }

    public new static object Serialise(object o, Serialiser s)
    {
      ParserAction parserAction = (ParserAction) o;
      if (s.Encode)
      {
        CSymbol.Serialise((object) parserAction, s);
        s.Serialise((object) parserAction.m_sym);
        s.Serialise((object) parserAction.m_len);
        return (object) null;
      }
      CSymbol.Serialise((object) parserAction, s);
      parserAction.m_sym = (CSymbol) s.Deserialise();
      parserAction.m_len = (int) s.Deserialise();
      return (object) parserAction;
    }
  }
}
