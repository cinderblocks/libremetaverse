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
  public class SymbolType
  {
    private string m_name;
    private SymbolType m_next;

    public SymbolType(SymbolsGen yyp, string name)
      : this(yyp, name, false)
    {
    }

    public SymbolType(SymbolsGen yyp, string name, bool defined)
    {
      Lexer lexer = yyp.m_lexer;
      int length = name.IndexOf('+');
      int num = 0;
      if (length > 0)
      {
        num = int.Parse(name.Substring(length + 1));
        if (num > yyp.LastSymbol)
          yyp.LastSymbol = num;
        name = name.Substring(0, length);
      }
      lexer.yytext = name;
      CSymbol csymbol1 = new CSymbol(yyp);
      if (num > 0)
        csymbol1.m_yynum = num;
      CSymbol csymbol2 = csymbol1.Resolve();
      if (defined)
        csymbol2.m_defined = true;
      m_name = name;
      m_next = yyp.stypes;
      yyp.stypes = this;
    }

    public SymbolType _Find(string name)
    {
      if (name.Equals(m_name))
        return this;
      if (m_next == null)
        return null;
      return m_next._Find(name);
    }
  }
}
