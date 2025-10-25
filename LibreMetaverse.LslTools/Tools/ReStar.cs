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

namespace LibreMetaverse.LslTools
{
  internal class ReStar : Regex
  {
    public ReStar(Regex sub)
    {
      m_sub = sub;
    }

    public override void Print(TextWriter s)
    {
      m_sub.Print(s);
      s.Write("*");
    }

    public override int Match(string str, int pos, int max)
    {
      int num1 = m_sub.Match(str, pos, max);
      if (num1 < 0)
        return -1;
      int num2 = 0;
      while (num1 > 0)
      {
        num1 = m_sub.Match(str, pos + num2, max);
        if (num1 >= 0)
          num2 += num1;
        else
          break;
      }
      return num2;
    }

    public override void Build(Nfa nfa)
    {
      Nfa nfa1 = new Nfa(nfa.m_tks, m_sub);
      nfa.AddEps(nfa1);
      nfa.AddEps(nfa.m_end);
      nfa1.m_end.AddEps(nfa);
    }
  }
}
