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

namespace LibreMetaverse.LSLTools.Tools
{
  public class ParseState
  {
    private bool m_changed = true;
    public Hashtable m_transitions = new Hashtable();
    public int m_state;
    public CSymbol m_accessingSymbol;
    public SymbolsGen m_sgen;
    internal ProdItemList m_items;

    public ParseState(SymbolsGen syms, CSymbol acc)
    {
      m_sgen = syms;
      m_state = syms.state++;
      m_accessingSymbol = acc;
      m_items = new ProdItemList();
    }

    private ParseState()
    {
    }

    public Transition GetTransition(CSymbol s)
    {
      return (Transition) m_transitions[s.yytext] ?? new Transition(this, s);
    }

    public bool Accessor(CSymbol[] x)
    {
      return new Path(x).Top == this;
    }

    public bool Lookback(Production pr, ParseState p)
    {
      return new Path(this, pr.Prefix(pr.m_rhs.Count)).Top == this;
    }

    public void MaybeAdd(ProdItem item)
    {
      if (!m_items.Add(item))
        return;
      m_changed = true;
    }

    public void Closure()
    {
      while (m_changed)
      {
        m_changed = false;
        for (ProdItemList prodItemList = m_items; prodItemList.m_pi != null; prodItemList = prodItemList.m_next)
          CheckClosure(prodItemList.m_pi);
      }
    }

    public void CheckClosure(ProdItem item)
    {
      CSymbol csymbol = item.Next();
      if (csymbol == null)
        return;
      csymbol.AddStartItems(this, item.FirstOfRest(csymbol.m_parser));
      if (!item.IsReducingAction())
        return;
      MaybeAdd(new ProdItem(item.m_prod, item.m_pos + 1));
    }

    public void AddEntries()
    {
      for (ProdItemList prodItemList = m_items; prodItemList.m_pi != null; prodItemList = prodItemList.m_next)
      {
        ProdItem pi1 = prodItemList.m_pi;
        if (!pi1.m_done)
        {
          CSymbol csymbol = pi1.Next();
          if (csymbol != null && !pi1.IsReducingAction())
          {
            ParseState parseState = new ParseState(m_sgen, csymbol);
            parseState.MaybeAdd(new ProdItem(pi1.m_prod, pi1.m_pos + 1));
            for (ProdItemList next = prodItemList.m_next; next != null && next.m_pi != null; next = next.m_next)
            {
              ProdItem pi2 = next.m_pi;
              if (csymbol == pi2.Next())
              {
                parseState.MaybeAdd(new ProdItem(pi2.m_prod, pi2.m_pos + 1));
                pi2.m_done = true;
              }
            }
            if (!m_items.AtEnd)
            {
              if (csymbol.IsAction())
              {
                ParseState next = parseState.CheckExists();
                foreach (CSymbol key in csymbol.m_follow.Keys)
                {
                  if (key != m_sgen.m_symbols.EOFSymbol)
                    GetTransition(key).m_next = new ParserShift((ParserAction) csymbol, next);
                }
              }
              else
                GetTransition(csymbol).m_next = new ParserShift(null, parseState.CheckExists());
            }
          }
        }
      }
    }

    public void ReduceStates()
    {
      for (ProdItemList prodItemList = m_items; prodItemList.m_pi != null; prodItemList = prodItemList.m_next)
      {
        ProdItem pi = prodItemList.m_pi;
        if (pi.Next() == null)
        {
          Production prod = pi.m_prod;
          if (prod.m_pno != 0)
          {
            int count = prod.m_rhs.Count;
            CSymbol rh;
            ParserReduce parserReduce;
            if (count > 0 && (rh = (CSymbol) prod.m_rhs[count - 1]) != null && rh.IsAction())
            {
              ParserAction action = (ParserAction) rh;
              action.m_len = count;
              parserReduce = new ParserReduce(action, count - 1, prod);
            }
            else
            {
              m_sgen.m_lexer.yytext = "%" + prod.m_lhs.yytext;
              m_sgen.m_prod = prod;
              var parserSimpleAction = new ParserSimpleAction(m_sgen)
              {
                  m_sym = prod.m_lhs,
                  m_len = count
              };
              parserReduce = new ParserReduce(parserSimpleAction, count, prod);
            }
            foreach (CSymbol key in pi.m_prod.m_lhs.m_follow.Keys)
              GetTransition(key).m_reduce[prod] = parserReduce;
          }
        }
      }
    }

    public bool SameAs(ParseState p)
    {
      if (m_accessingSymbol != p.m_accessingSymbol)
        return false;
      ProdItemList prodItemList1 = m_items;
      ProdItemList prodItemList2;
      for (prodItemList2 = p.m_items; !prodItemList1.AtEnd && !prodItemList2.AtEnd && (prodItemList1.m_pi.m_prod == prodItemList2.m_pi.m_prod && prodItemList1.m_pi.m_pos == prodItemList2.m_pi.m_pos); prodItemList2 = prodItemList2.m_next)
        prodItemList1 = prodItemList1.m_next;
      if (prodItemList1.AtEnd)
        return prodItemList2.AtEnd;
      return false;
    }

    public ParseState CheckExists()
    {
      Closure();
      foreach (ParseState p in m_sgen.m_symbols.m_states.Values)
      {
        if (SameAs(p))
          return p;
      }
      m_sgen.m_symbols.m_states[m_state] = this;
      AddEntries();
      return this;
    }

    ~ParseState()
    {
      if (m_sgen == null || m_state != m_sgen.state - 1)
        return;
      --m_sgen.state;
    }

    public void Print()
    {
      Console.WriteLine();
      if (m_state == 0)
        Console.WriteLine("state 0");
      else
        Console.WriteLine("state {0} accessed by {1}", m_state, m_accessingSymbol.yytext);
      if (m_items != null)
      {
        for (ProdItemList prodItemList = m_items; prodItemList.m_pi != null; prodItemList = prodItemList.m_next)
        {
          prodItemList.m_pi.Print();
          prodItemList.m_pi.m_prod.m_lhs.m_follow.Print();
        }
      }
      foreach (Transition transition in m_transitions.Values)
        transition.Print0();
    }

    public void Print0()
    {
      Console.WriteLine();
      if (m_state == 0)
        Console.WriteLine("state 0");
      else
        Console.WriteLine("state {0} accessed by {1}", m_state, m_accessingSymbol.yytext);
      if (m_items != null)
      {
        for (ProdItemList prodItemList = m_items; prodItemList.m_pi != null; prodItemList = prodItemList.m_next)
        {
          prodItemList.m_pi.Print();
          Console.WriteLine();
        }
      }
      Console.WriteLine();
      foreach (ParsingInfo pi in m_sgen.m_symbols.symbolInfo.Values)
        PrintTransition(pi);
    }

    private void PrintTransition(ParsingInfo pi)
    {
      ParserEntry parserEntry = (ParserEntry) pi.m_parsetable[m_state];
      if (parserEntry == null)
        return;
      Console.Write("        {0}  {1}  ", pi.m_name, parserEntry.str);
      if (parserEntry.m_action != null)
        parserEntry.m_action.Print();
      Console.WriteLine();
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return new ParseState();
      ParseState parseState = (ParseState) o;
      if (s.Encode)
      {
        s.Serialise(parseState.m_state);
        s.Serialise(parseState.m_accessingSymbol);
        s.Serialise(parseState.m_changed);
        return true;
      }
      parseState.m_state = (int) s.Deserialise();
      parseState.m_accessingSymbol = (CSymbol) s.Deserialise();
      parseState.m_changed = (bool) s.Deserialise();
      return parseState;
    }
  }
}
