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
      m_lexer = lexer;
      m_symbols = syms;
      m_symbols.erh = m_lexer.tokens.erh;
    }

    private void Create()
    {
      m_symbols.GetParser(m_lexer);
    }

    protected bool Error(ref ParseStackEntry top, string str)
    {
      var ns = (SYMBOL) new Error(this, top);
      if (m_debug)
        Console.WriteLine("Error encountered: " + str);
      ns.pos = top.m_value.pos;
      if (m_symbols.symbolInfo[0] != null && m_symbols.erh.counter < 1000)
      {
        while (top != null && m_stack.Count > 0)
        {
          if (m_debug)
            Console.WriteLine("Error recovery uncovers state {0}", top.m_state);
          if (ns.Pass(m_symbols, top.m_state, out var entry))
          {
            SYMBOL symbol1 = top.m_value;
            top.m_value = ns;
            entry.Pass(ref top);
            while (top.m_value != m_symbols.EOFSymbol && !top.m_value.Pass(m_symbols, top.m_state, out entry))
            {
              if (entry != null && entry.IsReduce())
              {
                SYMBOL symbol2 = null;
                if (entry.m_action != null)
                  symbol2 = entry.m_action.Action(this);
                m_ungot = top.m_value;
                Pop(ref top, ((ParserReduce) entry).m_depth, ns);
                symbol2.pos = top.m_value.pos;
                top.m_value = symbol2;
              }
              else
              {
                string yyname = top.m_value.yyname;
                if (m_debug)
                {
                  if (yyname == "TOKEN")
                    Console.WriteLine("Error recovery discards literal {0}", ((TOKEN) top.m_value).yytext);
                  else
                    Console.WriteLine("Error recovery discards token {0}", yyname);
                }
                top.m_value = NextSym();
              }
            }
            if (m_debug)
              Console.WriteLine("Recovery complete");
            ++m_symbols.erh.counter;
            return true;
          }
          Pop(ref top, 1, ns);
        }
      }
      m_symbols.erh.Error(new CSToolsException(13, m_lexer, ns.pos, "syntax error", str));
      top.m_value = ns;
      return false;
    }

    public SYMBOL Parse(StreamReader input)
    {
      m_lexer.Start(input);
      return Parse();
    }

    public SYMBOL Parse(CsReader inFile)
    {
      m_lexer.Start(inFile);
      return Parse();
    }

    public SYMBOL Parse(string buf)
    {
      m_lexer.Start(buf);
      return Parse();
    }

    private SYMBOL Parse()
    {
      Create();
      ParseStackEntry s = new ParseStackEntry(this, 0, NextSym());
      try
      {
        while (true)
        {
          do
          {
            string yyname = s.m_value.yyname;
            if (m_debug)
            {
                Console.WriteLine(
                    yyname.Equals("TOKEN")
                        ? $"State {(object)s.m_state} with {(object)yyname} \"{(object)((TOKEN)s.m_value).yytext}\""
                        : $"State {(object)s.m_state} with {(object)yyname}");
            }

            if (s.m_value != null && s.m_value.Pass(m_symbols, s.m_state, out var entry))
              entry.Pass(ref s);
            else if (s.m_value == m_symbols.EOFSymbol)
            {
              if (s.m_state == m_symbols.m_accept.m_state)
              {
                Pop(ref s, 1, m_symbols.m_startSymbol);
                if (m_symbols.erh.counter > 0)
                  return new recoveredError(this, s);
                SYMBOL symbol = s.m_value;
                s.m_value = null;
                return symbol;
              }
              if (!Error(ref s, "Unexpected EOF"))
                return s.m_value;
            }
            else if (!Error(ref s, "syntax error"))
              return s.m_value;
          }
          while (!m_debug);
          if (s.m_value != null)
          {
            object dollar = s.m_value.m_dollar;
            Console.WriteLine("In state {0} top {1} value {2}", s.m_state, s.m_value.yyname, dollar != null ? dollar.GetType().Name : (object) "null");
            if (dollar != null && dollar.GetType().Name.Equals("Int32"))
              Console.WriteLine((int) dollar);
            else
              s.m_value.Print();
          }
          else
            Console.WriteLine("In state {0} top NULL", s.m_state);
        }
      }
      catch (CSToolsStopException ex)
      {
        if (m_symbols.erh.throwExceptions)
          throw;
        m_symbols.erh.Report(ex);
      }
      return null;
    }

    internal void Push(ParseStackEntry elt)
    {
      m_stack.Push(elt);
    }

    internal void Pop(ref ParseStackEntry elt, int depth, SYMBOL ns)
    {
      for (; m_stack.Count > 0 && depth > 0; --depth)
      {
        elt = (ParseStackEntry) m_stack.Pop();
        if (m_symbols.m_concrete)
          ns.kids.Push(elt.m_value);
      }
      if (depth == 0)
        return;
      m_symbols.erh.Error(new CSToolsException(14, m_lexer, "Pop failed"));
    }

    public ParseStackEntry StackAt(int ix)
    {
      int count = m_stack.Count;
      if (m_stkdebug)
        Console.WriteLine("StackAt({0}),count {1}", ix, count);
      ParseStackEntry parseStackEntry = (ParseStackEntry) m_stack[ix];
      if (parseStackEntry == null)
        return new ParseStackEntry(this, 0, m_symbols.Special);
      if (parseStackEntry.m_value is Null)
        return new ParseStackEntry(this, parseStackEntry.m_state, null);
      if (m_stkdebug)
        Console.WriteLine(parseStackEntry.m_value.yyname);
      return parseStackEntry;
    }

    public SYMBOL NextSym()
    {
      SYMBOL ungot = m_ungot;
      if (ungot == null)
        return (SYMBOL) m_lexer.Next() ?? m_symbols.EOFSymbol;
      m_ungot = null;
      return ungot;
    }

    public void Error(int n, SYMBOL sym, string s)
    {
        m_symbols.erh.Error(sym != null
            ? new CSToolsException(n, sym.yylx, sym.pos, "", s)
            : new CSToolsException(n, s));
    }
  }
}
