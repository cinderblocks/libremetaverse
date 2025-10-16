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

namespace LibreMetaverse.LslTools
{
  public class ProdItem
  {
    public Production m_prod;
    public int m_pos;
    public bool m_done;
    private SymbolSet follow;

    public ProdItem(Production prod, int pos)
    {
      m_prod = prod;
      m_pos = pos;
      m_done = false;
    }

    public ProdItem()
    {
      m_prod = null;
      m_pos = 0;
      m_done = false;
    }

    public CSymbol Next()
    {
      if (m_pos < m_prod.m_rhs.Count)
        return (CSymbol) m_prod.m_rhs[m_pos];
      return null;
    }

    public bool IsReducingAction()
    {
      if (m_pos == m_prod.m_rhs.Count - 1)
        return Next().IsAction();
      return false;
    }

    public SymbolSet FirstOfRest(SymbolsGen syms)
    {
      if (follow != null)
        return follow;
      follow = new SymbolSet(syms);
      bool flag = false;
      int count = m_prod.m_rhs.Count;
      for (int index = m_pos + 1; index < count; ++index)
      {
        CSymbol rh = (CSymbol) m_prod.m_rhs[index];
        foreach (CSymbol key in rh.m_first.Keys)
          follow.CheckIn(key);
        if (!rh.IsNullable())
        {
          flag = true;
          break;
        }
      }
      if (!flag)
        follow.Add(m_prod.m_lhs.m_follow);
      follow = follow.Resolve();
      return follow;
    }

    public void Print()
    {
      Console.Write("   {0}    {1} : ", m_prod.m_pno, m_prod.m_lhs == null ? "$start" : (object) m_prod.m_lhs.yytext);
      int index;
      for (index = 0; index < m_prod.m_rhs.Count; ++index)
      {
          Console.Write(index == m_pos ? "_" : " ");
          string str = ((TOKEN) m_prod.m_rhs[index]).yytext;
        if (str.Equals("\n"))
          str = "\\n";
        Console.Write(str);
      }
      if (index == m_pos)
        Console.Write("_");
      Console.Write("  ");
    }
  }
}
