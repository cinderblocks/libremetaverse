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

namespace LibreMetaverse.LSLTools.Tools
{
  public class Path
  {
    public bool valid = true;
    private ParseState[] m_states;

    public Path(ParseState[] s)
    {
      m_states = s;
    }

    public Path(ParseState q, CSymbol[] x)
    {
      m_states = new ParseState[x.Length + 1];
      ParseState parseState = m_states[0] = q;
      for (int index1 = 0; index1 < x.Length; ++index1)
      {
        int index2 = index1;
        while (index2 < x.Length && x[index2].IsAction())
          ++index2;
        if (index2 >= x.Length)
        {
          m_states[index1 + 1] = parseState;
        }
        else
        {
          Transition transition = (Transition) parseState.m_transitions[x[index2].yytext];
          if (transition == null || transition.m_next == null)
          {
            valid = false;
            break;
          }
          parseState = m_states[index1 + 1] = transition.m_next.m_next;
        }
      }
    }

    public Path(CSymbol[] x)
      : this((ParseState) x[0].m_parser.m_symbols.m_states[0], x)
    {
    }

    public CSymbol[] Spelling
    {
      get
      {
        CSymbol[] csymbolArray = new CSymbol[m_states.Length - 1];
        for (int index = 0; index < csymbolArray.Length; ++index)
          csymbolArray[index] = m_states[index].m_accessingSymbol;
        return csymbolArray;
      }
    }

    public ParseState Top => m_states[m_states.Length - 1];
  }
}
