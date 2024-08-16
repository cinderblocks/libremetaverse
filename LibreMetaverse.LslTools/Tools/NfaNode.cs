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
  public class NfaNode : LNode
  {
    public string m_sTerminal = "";
    public ObjectList m_arcs = new ObjectList();
    public ObjectList m_eps = new ObjectList();

    public NfaNode(TokensGen tks)
      : base(tks)
    {
    }

    public void AddArc(char ch, NfaNode next)
    {
      this.m_arcs.Add((object) new Arc(ch, next));
    }

    public void AddUArc(char ch, NfaNode next)
    {
      this.m_arcs.Add((object) new UArc(ch, next));
    }

    public void AddArcEx(Regex re, NfaNode next)
    {
      this.m_arcs.Add((object) new ArcEx(re, next));
    }

    public void AddEps(NfaNode next)
    {
      this.m_eps.Add((object) next);
    }

    public void AddTarget(char ch, Dfa next)
    {
        foreach (var t in this.m_arcs)
        {
            Arc arc = (Arc) t;
            if (arc.Match(ch))
                next.AddNfaNode(arc.m_next);
        }
    }
  }
}
