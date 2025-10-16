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
  public class ParserReduce : ParserEntry
  {
    public int m_depth;
    public Production m_prod;
    public SymbolSet m_lookAhead;

    public ParserReduce(ParserAction action, int depth, Production prod)
      : base(action)
    {
      m_depth = depth;
      m_prod = prod;
    }

    private ParserReduce()
    {
    }

    public void BuildLookback(Transition a)
    {
      SymbolsGen sgen = a.m_ps.m_sgen;
      if (m_lookAhead != null)
        return;
      m_lookAhead = new SymbolSet(sgen);
      foreach (ParseState q in sgen.m_symbols.m_states.Values)
      {
        Transition transition = (Transition) q.m_transitions[m_prod.m_lhs.yytext];
        if (transition != null)
        {
          Path path = new Path(q, m_prod.Prefix(m_prod.m_rhs.Count));
          if (path.valid && path.Top == a.m_ps)
            transition.m_lookbackOf[this] = true;
        }
      }
    }

    public override void Pass(ref ParseStackEntry top)
    {
      Parser yyps = top.yyps;
      SYMBOL ns = m_action.Action(yyps);
      yyps.m_ungot = top.m_value;
      if (yyps.m_debug)
        Console.WriteLine("about to pop {0} count is {1}", m_depth, yyps.m_stack.Count);
      yyps.Pop(ref top, m_depth, ns);
      if (ns.pos == 0)
      {
          ns.pos = top.m_value.pos;
      }
      top.m_value = ns;
    }

    public override bool IsReduce()
    {
      return true;
    }

    public override string str
    {
      get
      {
        if (m_prod == null)
          return "?? null reduce";
        return $"reduce {(object)m_prod.m_pno}";
      }
    }

    public new static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return new ParserReduce();
      ParserReduce parserReduce = (ParserReduce) o;
      if (s.Encode)
      {
        ParserEntry.Serialise(parserReduce, s);
        s.Serialise(parserReduce.m_depth);
        s.Serialise(parserReduce.m_prod);
        return null;
      }
      ParserEntry.Serialise(parserReduce, s);
      parserReduce.m_depth = (int) s.Deserialise();
      parserReduce.m_prod = (Production) s.Deserialise();
      return parserReduce;
    }
  }
}
