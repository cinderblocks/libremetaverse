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

using System;
using System.Collections;

namespace LibreMetaverse.LslTools
{
  public class SymbolSet
  {
    private Hashtable m_set = new Hashtable();
    public SymbolsGen m_symbols;
    public SymbolSet m_next;

    public SymbolSet(SymbolsGen syms)
    {
      this.m_symbols = syms;
    }

    public SymbolSet(SymbolSet s)
      : this(s.m_symbols)
    {
      this.Add(s);
    }

    public bool Contains(CSymbol a)
    {
      return this.m_set.Contains((object) a);
    }

    public ICollection Keys => this.m_set.Keys;

    public IDictionaryEnumerator GetEnumerator()
    {
      return this.m_set.GetEnumerator();
    }

    public int Count => this.m_set.Count;

    public bool CheckIn(CSymbol a)
    {
      if (this.Contains(a))
        return false;
      this.AddIn(a);
      return true;
    }

    public SymbolSet Resolve()
    {
      return this.find(this.m_symbols.lahead);
    }

    private SymbolSet find(SymbolSet h)
    {
      if (h == null)
      {
        this.m_next = this.m_symbols.lahead;
        this.m_symbols.lahead = this;
        return this;
      }
      if (SymbolSet.Equals(h, this))
        return h;
      return this.find(h.m_next);
    }

    private static bool Equals(SymbolSet s, SymbolSet t)
    {
      if (s.m_set.Count != t.m_set.Count)
        return false;
      IDictionaryEnumerator enumerator1 = s.GetEnumerator();
      IDictionaryEnumerator enumerator2 = t.GetEnumerator();
      for (int index = 0; index < s.Count; ++index)
      {
        enumerator1.MoveNext();
        enumerator2.MoveNext();
        if (enumerator1.Key != enumerator2.Key)
          return false;
      }
      return true;
    }

    public void AddIn(CSymbol t)
    {
      this.m_set[(object) t] = (object) true;
    }

    public void Add(SymbolSet s)
    {
      if (s == this)
        return;
      foreach (CSymbol key in (IEnumerable) s.Keys)
        this.AddIn(key);
    }

    public void Print()
    {
      string str = "[";
      int num = 0;
      foreach (CSymbol key in (IEnumerable) this.Keys)
      {
        ++num;
        str = !key.yytext.Equals("\n") ? str + key.yytext : str + "\\n";
        if (num < this.Count)
          str += ",";
      }
      Console.WriteLine(str + "]");
    }

    public static SymbolSet operator +(SymbolSet s, SymbolSet t)
    {
      SymbolSet symbolSet = new SymbolSet(s);
      symbolSet.Add(t);
      return symbolSet.Resolve();
    }
  }
}
