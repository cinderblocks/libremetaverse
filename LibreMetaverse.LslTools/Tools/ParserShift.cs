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
  public class ParserShift : ParserEntry
  {
    public ParseState m_next;

    public ParserShift()
    {
    }

    public ParserShift(ParserAction action, ParseState next)
      : base(action)
    {
      m_next = next;
    }

    public override void Pass(ref ParseStackEntry top)
    {
      Parser yyps = top.yyps;
      if (m_action == null)
      {
        yyps.Push(top);
        top = new ParseStackEntry(yyps, m_next.m_state, yyps.NextSym());
      }
      else
      {
        yyps.Push(new ParseStackEntry(yyps, top.m_state, m_action.Action(yyps)));
        top.m_state = m_next.m_state;
      }
    }

    public override string str
    {
      get
      {
        if (m_next == null)
          return "?? null shift";
        return $"shift {(object)m_next.m_state}";
      }
    }

    public new static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return new ParserShift();
      ParserShift parserShift = (ParserShift) o;
      if (s.Encode)
      {
        ParserEntry.Serialise(parserShift, s);
        s.Serialise(parserShift.m_next);
        return null;
      }
      ParserEntry.Serialise(parserShift, s);
      parserShift.m_next = (ParseState) s.Deserialise();
      return parserShift;
    }
  }
}
