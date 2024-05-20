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

namespace LibreMetaverse.LslTools
{
  public class ParserOldAction : ParserAction
  {
    public int m_action;

    public ParserOldAction(SymbolsGen yyp)
      : base(yyp)
    {
      this.m_action = yyp.action_num++;
      yyp.actions.Add((object) this);
      this.m_sym = (CSymbol) null;
      this.m_symtype = CSymbol.SymType.oldaction;
      yyp.OldAction(this);
    }

    private ParserOldAction()
    {
    }

    public override SYMBOL Action(Parser yyp)
    {
      SYMBOL yysym = base.Action(yyp);
      object obj = yyp.m_symbols.Action(yyp, yysym, this.m_action);
      if (obj != null)
        yysym.m_dollar = obj;
      return yysym;
    }

    public override int ActNum()
    {
      return this.m_action;
    }

    public new static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new ParserOldAction();
      ParserOldAction parserOldAction = (ParserOldAction) o;
      if (s.Encode)
      {
        ParserAction.Serialise((object) parserOldAction, s);
        s.Serialise((object) parserOldAction.m_action);
        return (object) null;
      }
      ParserAction.Serialise((object) parserOldAction, s);
      parserOldAction.m_action = (int) s.Deserialise();
      return (object) parserOldAction;
    }
  }
}
