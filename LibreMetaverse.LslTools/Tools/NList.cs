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

namespace Tools.Tools
{
  internal class NList
  {
    public NfaNode m_node;
    public NList m_next;

    public NList()
    {
      this.m_node = (NfaNode) null;
      this.m_next = (NList) null;
    }

    private NList(NfaNode nd, NList nx)
    {
      this.m_node = nd;
      this.m_next = nx;
    }

    public bool Add(NfaNode n)
    {
      if (this.m_node == null)
      {
        this.m_next = new NList();
        this.m_node = n;
      }
      else if (this.m_node.m_state < n.m_state)
      {
        this.m_next = new NList(this.m_node, this.m_next);
        this.m_node = n;
      }
      else
      {
        if (this.m_node.m_state == n.m_state)
          return false;
        return this.m_next.Add(n);
      }
      return true;
    }

    public bool AtEnd => this.m_node == null;
  }
}
