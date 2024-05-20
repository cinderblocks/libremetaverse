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
using System.IO;

namespace LibreMetaverse.LslTools
{
  public class Parser
  {
    internal ObjectList m_stack = new ObjectList();
    public YyParser m_symbols;
    public bool m_debug;
    public bool m_stkdebug;
    public Lexer m_lexer;
    internal SYMBOL m_ungot;

    public Parser(YyParser syms, Lexer lexer)
    {
      this.m_lexer = lexer;
      this.m_symbols = syms;
      this.m_symbols.erh = this.m_lexer.tokens.erh;
    }

    private void Create()
    {
      this.m_symbols.GetParser(this.m_lexer);
    }

    protected bool Error(ref ParseStackEntry top, string str)
    {
      SYMBOL ns = (SYMBOL) new error(this, top);
      if (this.m_debug)
        Console.WriteLine("Error encountered: " + str);
      ns.pos = top.m_value.pos;
      if (this.m_symbols.symbolInfo[(object) 0] != null && this.m_symbols.erh.counter < 1000)
      {
        while (top != null && this.m_stack.Count > 0)
        {
          if (this.m_debug)
            Console.WriteLine("Error recovery uncovers state {0}", (object) top.m_state);
          ParserEntry entry;
          if (ns.Pass(this.m_symbols, top.m_state, out entry))
          {
            SYMBOL symbol1 = top.m_value;
            top.m_value = ns;
            entry.Pass(ref top);
            while (top.m_value != this.m_symbols.EOFSymbol && !top.m_value.Pass(this.m_symbols, top.m_state, out entry))
            {
              if (entry != null && entry.IsReduce())
              {
                SYMBOL symbol2 = (SYMBOL) null;
                if (entry.m_action != null)
                  symbol2 = entry.m_action.Action(this);
                this.m_ungot = top.m_value;
                this.Pop(ref top, ((ParserReduce) entry).m_depth, ns);
                symbol2.pos = top.m_value.pos;
                top.m_value = symbol2;
              }
              else
              {
                string yyname = top.m_value.yyname;
                if (this.m_debug)
                {
                  if (yyname == "TOKEN")
                    Console.WriteLine("Error recovery discards literal {0}", (object) ((TOKEN) top.m_value).yytext);
                  else
                    Console.WriteLine("Error recovery discards token {0}", (object) yyname);
                }
                top.m_value = this.NextSym();
              }
            }
            if (this.m_debug)
              Console.WriteLine("Recovery complete");
            ++this.m_symbols.erh.counter;
            return true;
          }
          this.Pop(ref top, 1, ns);
        }
      }
      this.m_symbols.erh.Error(new CSToolsException(13, this.m_lexer, ns.pos, "syntax error", str));
      top.m_value = ns;
      return false;
    }

    public SYMBOL Parse(StreamReader input)
    {
      this.m_lexer.Start(input);
      return this.Parse();
    }

    public SYMBOL Parse(CsReader inFile)
    {
      this.m_lexer.Start(inFile);
      return this.Parse();
    }

    public SYMBOL Parse(string buf)
    {
      this.m_lexer.Start(buf);
      return this.Parse();
    }

    private SYMBOL Parse()
    {
      this.Create();
      ParseStackEntry s = new ParseStackEntry(this, 0, this.NextSym());
      try
      {
        while (true)
        {
          do
          {
            string yyname = s.m_value.yyname;
            if (this.m_debug)
            {
              if (yyname.Equals("TOKEN"))
                Console.WriteLine(string.Format("State {0} with {1} \"{2}\"", (object) s.m_state, (object) yyname, (object) ((TOKEN) s.m_value).yytext));
              else
                Console.WriteLine(string.Format("State {0} with {1}", (object) s.m_state, (object) yyname));
            }
            ParserEntry entry;
            if (s.m_value != null && s.m_value.Pass(this.m_symbols, s.m_state, out entry))
              entry.Pass(ref s);
            else if (s.m_value == this.m_symbols.EOFSymbol)
            {
              if (s.m_state == this.m_symbols.m_accept.m_state)
              {
                this.Pop(ref s, 1, (SYMBOL) this.m_symbols.m_startSymbol);
                if (this.m_symbols.erh.counter > 0)
                  return (SYMBOL) new recoveredError(this, s);
                SYMBOL symbol = s.m_value;
                s.m_value = (SYMBOL) null;
                return symbol;
              }
              if (!this.Error(ref s, "Unexpected EOF"))
                return s.m_value;
            }
            else if (!this.Error(ref s, "syntax error"))
              return s.m_value;
          }
          while (!this.m_debug);
          if (s.m_value != null)
          {
            object dollar = s.m_value.m_dollar;
            Console.WriteLine("In state {0} top {1} value {2}", (object) s.m_state, (object) s.m_value.yyname, dollar != null ? (object) dollar.GetType().Name : (object) "null");
            if (dollar != null && dollar.GetType().Name.Equals("Int32"))
              Console.WriteLine((int) dollar);
            else
              s.m_value.Print();
          }
          else
            Console.WriteLine("In state {0} top NULL", (object) s.m_state);
        }
      }
      catch (CSToolsStopException ex)
      {
        if (this.m_symbols.erh.throwExceptions)
          throw;
        this.m_symbols.erh.Report((CSToolsException) ex);
      }
      return (SYMBOL) null;
    }

    internal void Push(ParseStackEntry elt)
    {
      this.m_stack.Push((object) elt);
    }

    internal void Pop(ref ParseStackEntry elt, int depth, SYMBOL ns)
    {
      for (; this.m_stack.Count > 0 && depth > 0; --depth)
      {
        elt = (ParseStackEntry) this.m_stack.Pop();
        if (this.m_symbols.m_concrete)
          ns.kids.Push((object) elt.m_value);
      }
      if (depth == 0)
        return;
      this.m_symbols.erh.Error(new CSToolsException(14, this.m_lexer, "Pop failed"));
    }

    public ParseStackEntry StackAt(int ix)
    {
      int count = this.m_stack.Count;
      if (this.m_stkdebug)
        Console.WriteLine("StackAt({0}),count {1}", (object) ix, (object) count);
      ParseStackEntry parseStackEntry = (ParseStackEntry) this.m_stack[ix];
      if (parseStackEntry == null)
        return new ParseStackEntry(this, 0, (SYMBOL) this.m_symbols.Special);
      if (parseStackEntry.m_value is Null)
        return new ParseStackEntry(this, parseStackEntry.m_state, (SYMBOL) null);
      if (this.m_stkdebug)
        Console.WriteLine(parseStackEntry.m_value.yyname);
      return parseStackEntry;
    }

    public SYMBOL NextSym()
    {
      SYMBOL ungot = this.m_ungot;
      if (ungot == null)
        return (SYMBOL) this.m_lexer.Next() ?? (SYMBOL) this.m_symbols.EOFSymbol;
      this.m_ungot = (SYMBOL) null;
      return ungot;
    }

    public void Error(int n, SYMBOL sym, string s)
    {
      if (sym != null)
        this.m_symbols.erh.Error(new CSToolsException(n, sym.yylx, sym.pos, "", s));
      else
        this.m_symbols.erh.Error(new CSToolsException(n, s));
    }
  }
}
