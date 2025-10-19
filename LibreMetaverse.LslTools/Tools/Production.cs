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

using System.Collections;

namespace LibreMetaverse.LslTools
{
  public class Production
  {
    public ObjectList m_rhs = new ObjectList();
    public Hashtable m_alias = new Hashtable();
    public int m_pno;
    public CSymbol m_lhs;
    public bool m_actionsOnly;
    public int m_prec;

    public Production(SymbolsGen syms)
    {
      m_lhs = null;
      m_prec = 0;
      m_pno = syms.pno++;
      m_actionsOnly = true;
      syms.prods.Add(this);
    }

    public Production(SymbolsGen syms, CSymbol lhs)
    {
      m_lhs = lhs;
      m_prec = 0;
      m_pno = syms.pno++;
      m_actionsOnly = true;
      syms.prods.Add(this);
      lhs.m_prods.Add(this);
    }

    private Production()
    {
    }

    public void AddToRhs(CSymbol s)
    {
      m_rhs.Add(s);
      m_actionsOnly = m_actionsOnly && s.IsAction();
    }

    public void AddFirst(CSymbol s, int j)
    {
      for (; j < m_rhs.Count; ++j)
      {
        CSymbol rh = (CSymbol) m_rhs[j];
        s.AddFollow(rh.m_first);
        if (!rh.IsNullable())
          break;
      }
    }

    public bool CouldBeEmpty(int j)
    {
      for (; j < m_rhs.Count; ++j)
      {
        if (!((CSymbol) m_rhs[j]).IsNullable())
          return false;
      }
      return true;
    }

    public CSymbol[] Prefix(int i)
    {
      CSymbol[] csymbolArray = new CSymbol[i];
      for (int index = 0; index < i; ++index)
        csymbolArray[index] = (CSymbol) m_rhs[index];
      return csymbolArray;
    }

    public void StackRef(ref string str, int ch, int ix)
    {
      int num = m_rhs.Count + 1;
      CSymbol rh = (CSymbol) m_rhs[ix - 1];
      str += $"\n\t(({(object)rh.TypeStr()})(yyq.StackAt({(object)(num - ix - 1)}).m_value))\n\t";
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return new Production();
      Production production = (Production) o;
      if (s.Encode)
      {
        s.Serialise(production.m_pno);
        return null;
      }
      production.m_pno = (int) s.Deserialise();
      return production;
    }
  }
}
