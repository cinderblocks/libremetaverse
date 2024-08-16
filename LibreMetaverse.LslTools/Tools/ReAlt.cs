﻿/*
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
  internal class ReAlt : Regex
  {
    public Regex m_alt;

    public ReAlt(TokensGen tks, Regex sub, int p, string str)
    {
      this.m_sub = sub;
      this.m_alt = new Regex(tks, p, str);
    }

    public override void Print(TextWriter s)
    {
      s.Write("(");
      if (this.m_sub != null)
        this.m_sub.Print(s);
      s.Write("|");
      if (this.m_alt != null)
        this.m_alt.Print(s);
      s.Write(")");
    }

    public override int Match(string str, int pos, int max)
    {
      int num1 = -1;
      int num2 = -1;
      if (this.m_sub != null)
        num1 = this.m_sub.Match(str, pos, max);
      if (this.m_alt != null)
        num2 = this.m_sub.Match(str, pos, max);
      if (num1 > num2)
        return num1;
      return num2;
    }

    public override void Build(Nfa nfa)
    {
      if (this.m_alt != null)
      {
        Nfa nfa1 = new Nfa(nfa.m_tks, this.m_alt);
        nfa.AddEps((NfaNode) nfa1);
        nfa1.m_end.AddEps(nfa.m_end);
      }
      base.Build(nfa);
    }
  }
}
