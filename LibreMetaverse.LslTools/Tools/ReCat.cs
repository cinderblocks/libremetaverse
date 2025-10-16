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

using System.IO;

namespace LibreMetaverse.LSLTools.Tools
{
  internal class ReCat : Regex
  {
    private Regex m_next;

    public ReCat(TokensGen tks, Regex sub, int p, string str)
    {
      m_sub = sub;
      m_next = new Regex(tks, p, str);
    }

    public override void Print(TextWriter s)
    {
      s.Write("(");
      if (m_sub != null)
        m_sub.Print(s);
      s.Write(")(");
      if (m_next != null)
        m_next.Print(s);
      s.Write(")");
    }

    public override int Match(string str, int pos, int max)
    {
      int num1 = -1;
      if (m_next == null)
        return base.Match(str, pos, max);
      if (m_sub == null)
        return m_next.Match(str, pos, max);
      int num2;
      for (int max1 = max; max1 >= 0; max1 = num2 - 1)
      {
        num2 = m_sub.Match(str, pos, max1);
        if (num2 >= 0)
        {
          int num3 = m_next.Match(str, pos + num2, max);
          if (num3 >= 0 && num2 + num3 > num1)
            num1 = num2 + num3;
        }
        else
          break;
      }
      return num1;
    }

    public override void Build(Nfa nfa)
    {
      if (m_next != null)
      {
        if (m_sub != null)
        {
          Nfa nfa1 = new Nfa(nfa.m_tks, m_sub);
          Nfa nfa2 = new Nfa(nfa.m_tks, m_next);
          nfa.AddEps(nfa1);
          nfa1.m_end.AddEps(nfa2);
          nfa2.m_end.AddEps(nfa.m_end);
        }
        else
          m_next.Build(nfa);
      }
      else
        base.Build(nfa);
    }
  }
}
