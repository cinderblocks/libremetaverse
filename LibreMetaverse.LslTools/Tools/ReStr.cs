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
  internal class ReStr : Regex
  {
    public string m_str = "";

    public ReStr()
    {
    }

    public ReStr(TokensGen tks, string str)
    {
        this.m_str = str;
        foreach (var c in str)
            tks.m_tokens.UsingChar(c);
    }

    public ReStr(TokensGen tks, char ch)
    {
      this.m_str = new string(ch, 1);
      tks.m_tokens.UsingChar(ch);
    }

    public override void Print(TextWriter s)
    {
      s.Write($"(\"{(object)this.m_str}\")");
    }

    public override int Match(string str, int pos, int max)
    {
      int length = this.m_str.Length;
      if (length > max || length > max - pos)
        return -1;
      for (int index = 0; index < length; ++index)
      {
        if ((int) str[index] != (int) this.m_str[index])
          return -1;
      }
      return length;
    }

    public override void Build(Nfa nfa)
    {
      int length = this.m_str.Length;
      NfaNode nfaNode = (NfaNode) nfa;
      for (int index = 0; index < length; ++index)
      {
        NfaNode next = new NfaNode(nfa.m_tks);
        nfaNode.AddArc(this.m_str[index], next);
        nfaNode = next;
      }
      nfaNode.AddEps(nfa.m_end);
    }
  }
}
