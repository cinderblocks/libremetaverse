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

namespace Tools.Tools
{
  public class Transition
  {
    public Hashtable m_reduce = new Hashtable();
    private Hashtable m_reads = new Hashtable();
    private Hashtable m_includes = new Hashtable();
    internal Hashtable m_lookbackOf = new Hashtable();
    public int m_tno;
    public ParseState m_ps;
    public CSymbol m_A;
    public ParserShift m_next;
    private SymbolSet m_DR;
    private SymbolSet m_Read;
    private SymbolSet m_Follow;

    public Transition(ParseState p, CSymbol a)
    {
      this.m_ps = p;
      this.m_A = a;
      this.m_tno = p.m_sgen.m_trans++;
      p.m_transitions[(object) a.yytext] = (object) this;
    }

    private ParsingInfo ParsingInfo => this.m_ps.m_sgen.m_symbols.GetSymbolInfo(this.m_A.yytext, this.m_A.m_yynum);

    public static Hashtable reads(Transition a)
    {
      return a.m_reads;
    }

    public static Hashtable includes(Transition a)
    {
      return a.m_includes;
    }

    public static SymbolSet DR(Transition a)
    {
      return a.m_DR;
    }

    public static SymbolSet Read(Transition a)
    {
      return a.m_Read;
    }

    public static SymbolSet Follow(Transition a)
    {
      return a.m_Follow;
    }

    public static void AddToRead(Transition a, SymbolSet s)
    {
      a.m_Read.Add(s);
    }

    public static void AddToFollow(Transition a, SymbolSet s)
    {
      a.m_Follow.Add(s);
    }

    public static void BuildDR(Transition t)
    {
      SymbolsGen sgen = t.m_ps.m_sgen;
      t.m_DR = new SymbolSet(sgen);
      if (t.m_next == null)
        return;
      foreach (Transition transition in (IEnumerable) t.m_next.m_next.m_transitions.Values)
      {
        if (transition.m_next != null && (transition.m_A.m_symtype == CSymbol.SymType.terminal || transition.m_A.m_symtype == CSymbol.SymType.eofsymbol))
          t.m_DR.AddIn(transition.m_A);
      }
    }

    public static void Final(Transition t)
    {
      t.m_DR.AddIn(t.m_ps.m_sgen.m_symbols.EOFSymbol);
    }

    public static void BuildReads(Transition t)
    {
      t.m_Read = new SymbolSet(t.m_ps.m_sgen);
      ParseState parseState = t.m_A.Next(t.m_ps);
      if (parseState == null)
        return;
      foreach (Transition transition in (IEnumerable) parseState.m_transitions.Values)
      {
        if (transition.m_A.IsNullable())
          t.m_reads[(object) transition] = (object) true;
      }
    }

    public static void BuildIncludes(Transition t)
    {
      t.m_Follow = new SymbolSet(t.m_ps.m_sgen);
      foreach (Production prod in t.m_A.m_prods)
      {
        for (int i = prod.m_rhs.Count - 1; i >= 0; --i)
        {
          CSymbol rh = (CSymbol) prod.m_rhs[i];
          if (rh.m_symtype == CSymbol.SymType.nonterminal)
            ((Transition) (i <= 0 ? t.m_ps : new Path(t.m_ps, prod.Prefix(i)).Top).m_transitions[(object) rh.yytext]).m_includes[(object) t] = (object) true;
          if (!rh.IsNullable())
            break;
        }
      }
    }

    public static void BuildLookback(Transition t)
    {
      foreach (ParserReduce parserReduce in (IEnumerable) t.m_reduce.Values)
        parserReduce.BuildLookback(t);
    }

    public static void BuildLA(Transition t)
    {
      foreach (ParserReduce key in (IEnumerable) t.m_lookbackOf.Keys)
        key.m_lookAhead.Add(t.m_Follow);
    }

    public static void BuildParseTable(Transition t)
    {
      YyParser symbols = t.m_ps.m_sgen.m_symbols;
      ParsingInfo parsingInfo = t.ParsingInfo;
      ParserReduce parserReduce1 = (ParserReduce) null;
      foreach (ParserReduce parserReduce2 in (IEnumerable) t.m_reduce.Values)
      {
        if ((!t.m_ps.m_sgen.m_lalrParser ? (parserReduce2.m_prod.m_lhs.m_follow.Contains(t.m_A) ? 1 : 0) : (parserReduce2.m_lookAhead.Contains(t.m_A) ? 1 : 0)) != 0)
        {
          if (parserReduce1 != null)
            symbols.erh.Error(new CSToolsException(12,
              $"reduce/reduce conflict {(object)parserReduce1.m_prod.m_pno} vs {(object)parserReduce2.m_prod.m_pno}" +
              $" state {(object)t.m_ps.m_state} on {(object)t.m_A.yytext}"));
          parserReduce1 = parserReduce2;
        }
      }
      if (t.m_next != null && t.m_A != symbols.EOFSymbol)
      {
        if (parserReduce1 == null)
        {
          parsingInfo.m_parsetable[(object) t.m_ps.m_state] = (object) t.m_next;
        }
        else
        {
          switch (t.m_A.ShiftPrecedence(parserReduce1.m_prod, t.m_ps))
          {
            case Precedence.PrecType.left:
              parsingInfo.m_parsetable[(object) t.m_ps.m_state] = (object) t.m_next;
              break;
            case Precedence.PrecType.right:
              parsingInfo.m_parsetable[(object) t.m_ps.m_state] = (object) parserReduce1;
              break;
          }
        }
      }
      else
      {
        if (parserReduce1 == null)
          return;
        parsingInfo.m_parsetable[(object) t.m_ps.m_state] = (object) parserReduce1;
      }
    }

    public void Print0()
    {
      Console.Write("    " + this.m_A.yytext);
      if (this.m_next != null)
        Console.Write("  shift " + (object) this.m_next.m_next.m_state);
      foreach (Production key in (IEnumerable) this.m_reduce.Keys)
        Console.Write("  reduce (" + (object) key.m_pno + ")");
      Console.WriteLine();
    }

    public void Print(SymbolSet x, string s)
    {
      Console.Write("Transition (" + (object) this.m_ps.m_state + "," + this.m_A.yytext + ") " + s + " ");
      x.Print();
    }
  }
}
