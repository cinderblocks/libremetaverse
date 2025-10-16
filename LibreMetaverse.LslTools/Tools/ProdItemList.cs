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
  internal class ProdItemList
  {
    public ProdItem m_pi;
    public ProdItemList m_next;

    public ProdItemList(ProdItem pi, ProdItemList n)
    {
      m_pi = pi;
      m_next = n;
    }

    public ProdItemList()
    {
      m_pi = null;
      m_next = null;
    }

    public bool Add(ProdItem pi)
    {
      if (m_pi == null)
      {
        m_next = new ProdItemList();
        m_pi = pi;
      }
      else if (m_pi.m_prod.m_pno < pi.m_prod.m_pno || m_pi.m_prod.m_pno == pi.m_prod.m_pno && m_pi.m_pos < pi.m_pos)
      {
        m_next = new ProdItemList(m_pi, m_next);
        m_pi = pi;
      }
      else
      {
        if (m_pi.m_prod.m_pno == pi.m_prod.m_pno && m_pi.m_pos == pi.m_pos)
          return false;
        return m_next.Add(pi);
      }
      return true;
    }

    public bool AtEnd => m_pi == null;
  }
}
