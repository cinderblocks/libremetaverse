/*
 * Copyright (c) 2019-2022, Sjofn LLC
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
  public abstract class SymbolsGen : GenBase
  {
    public bool m_lalrParser = true;
    public YyParser m_symbols = new YyParser();
    public ObjectList prods = new ObjectList();
    internal ObjectList actions = new ObjectList();
    public Lexer m_lexer;
    public int pno;
    public int m_trans;
    public int action;
    internal int action_num;
    public SymbolType stypes;
    internal int state;
    public SymbolSet lahead;

    protected SymbolsGen(ErrorHandler eh)
      : base(eh)
    {
    }

    public bool Find(CSymbol sym)
    {
      if (sym.yytext.Equals("Null") || sym.yytext[0] == '\'')
        return true;
      if (this.stypes == null)
        return false;
      return this.stypes._Find(sym.yytext) != null;
    }

    public abstract void ParserDirective();

    public abstract void Declare();

    public abstract void SetNamespace();

    public abstract void SetStartSymbol();

    public abstract void ClassDefinition(string s);

    public abstract void AssocType(Precedence.PrecType pt, int n);

    public abstract void CopySegment();

    public abstract void SimpleAction(ParserSimpleAction a);

    public abstract void OldAction(ParserOldAction a);
  }
}
