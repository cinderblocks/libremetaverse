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

namespace LibreMetaverse.LslTools
{
  public class CSymbol : TOKEN
  {
    public int m_yynum = -1;
    public ObjectList m_prods = new ObjectList();
    public string m_initialisation = "";
    public CSymbol.SymType m_symtype;
    public SymbolsGen m_parser;
    public Precedence m_prec;
    public SymbolSet m_first;
    public SymbolSet m_follow;
    private object isNullable;
    public CSymbol m_refSymbol;
    public bool m_defined;
    public bool m_emitted;
    public Production m_prod;

    public CSymbol(Lexer yyl)
      : base(yyl)
    {
    }

    public CSymbol(SymbolsGen yyp)
      : base(yyp.m_lexer)
    {
      this.m_parser = yyp;
      this.m_symtype = CSymbol.SymType.unknown;
      this.m_prec = (Precedence) null;
      this.m_prod = (Production) null;
      this.m_refSymbol = (CSymbol) null;
      this.m_first = new SymbolSet(yyp);
      this.m_follow = new SymbolSet(yyp);
    }

    protected CSymbol()
    {
    }

    public override bool IsTerminal()
    {
      return this.m_symtype == CSymbol.SymType.terminal;
    }

    public virtual CSymbol Resolve()
    {
      if (this.yytext == "EOF")
        this.m_yynum = 2;
      CSymbol symbol = (CSymbol) this.m_parser.m_symbols.symbols[(object) this.yytext];
      if (symbol != null)
        return symbol;
      if (this.m_yynum < 0)
        this.m_yynum = ++this.m_parser.LastSymbol;
      this.m_parser.m_symbols.symbols[(object) this.yytext] = (object) this;
      return this;
    }

    public override bool Matches(string s)
    {
      return false;
    }

    internal ParseState Next(ParseState p)
    {
      if (!p.m_transitions.Contains((object) this.yytext))
        return (ParseState) null;
      return ((Transition) p.m_transitions[(object) this.yytext]).m_next?.m_next;
    }

    internal Hashtable Reduce(ParseState p)
    {
      if (!p.m_transitions.Contains((object) this.yytext))
        return (Hashtable) null;
      return ((Transition) p.m_transitions[(object) this.yytext]).m_reduce;
    }

    public virtual string TypeStr()
    {
      return this.yytext;
    }

    public Precedence.PrecType ShiftPrecedence(Production prod, ParseState ps)
    {
      if (prod == null || !prod.m_lhs.m_follow.Contains(this))
        return Precedence.PrecType.left;
      if (this.m_prec == null)
      {
        Console.WriteLine("Shift/Reduce conflict {0} on reduction {1} in state {2}", (object) this.yytext, (object) prod.m_pno, (object) ps.m_state);
        return Precedence.PrecType.left;
      }
      if (this.m_prec.m_type == Precedence.PrecType.nonassoc)
        return Precedence.PrecType.nonassoc;
      int num = Precedence.Check(this, prod, 0);
      if (num == 0)
        return Precedence.Check(this.m_prec, Precedence.PrecType.right, 0) != 0 ? Precedence.PrecType.left : Precedence.PrecType.right;
      return num > 0 ? Precedence.PrecType.left : Precedence.PrecType.right;
    }

    public bool AddFollow(SymbolSet map)
    {
      bool flag = false;
      foreach (CSymbol key in (IEnumerable) map.Keys)
        flag |= this.m_follow.CheckIn(key);
      return flag;
    }

    public void AddStartItems(ParseState pstate, SymbolSet follows)
    {
        foreach (var p in this.m_prods)
        {
            Production prod = (Production) p;
            pstate.MaybeAdd(new ProdItem(prod, 0));
        }
    }

    public bool IsNullable()
    {
      if (this.isNullable == null)
      {
        switch (this.m_symtype)
        {
          case CSymbol.SymType.terminal:
            this.isNullable = (object) false;
            break;
          case CSymbol.SymType.nonterminal:
            this.isNullable = (object) false;
            IEnumerator enumerator = this.m_prods.GetEnumerator();
            try
            {
              while (enumerator.MoveNext())
              {
                Production current = (Production) enumerator.Current;
                bool flag = true;
                foreach (CSymbol rh in current.m_rhs)
                {
                  if (!rh.IsNullable())
                  {
                    flag = false;
                    break;
                  }
                }
                if (flag)
                {
                  this.isNullable = (object) true;
                  break;
                }
              }
              break;
            }
            finally
            {
              (enumerator as IDisposable)?.Dispose();
            }
          case CSymbol.SymType.oldaction:
            this.isNullable = (object) true;
            break;
          case CSymbol.SymType.simpleaction:
            this.isNullable = (object) true;
            break;
          case CSymbol.SymType.eofsymbol:
            this.isNullable = (object) false;
            break;
          default:
            throw new Exception("unexpected symbol type");
        }
      }
      return (bool) this.isNullable;
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new CSymbol();
      CSymbol csymbol = (CSymbol) o;
      if (s.Encode)
      {
        s.Serialise((object) csymbol.yytext);
        s.Serialise((object) csymbol.m_yynum);
        s.Serialise((object) (int) csymbol.m_symtype);
        return (object) null;
      }
      csymbol.yytext = (string) s.Deserialise();
      csymbol.m_yynum = (int) s.Deserialise();
      csymbol.m_symtype = (CSymbol.SymType) s.Deserialise();
      return (object) csymbol;
    }

    public enum SymType
    {
      unknown,
      terminal,
      nonterminal,
      nodesymbol,
      oldaction,
      simpleaction,
      eofsymbol,
    }
  }
}
