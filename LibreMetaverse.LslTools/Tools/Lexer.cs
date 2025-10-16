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

namespace LibreMetaverse.LSLTools.Tools
{
  public class Lexer
  {
    internal LineManager m_LineManager = new LineManager();
    public string m_state = "YYINITIAL";
    public bool m_debug;
    public string m_buf;
    private YyLexer m_tokens;
    public string yytext;
    public int m_pch;
    private bool m_matching;
    private int m_startMatch;

    public Lexer(YyLexer tks)
    {
      m_state = "YYINITIAL";
      tokens = tks;
    }

    public SourceLineInfo sourceLineInfo(int pos)
    {
      return new SourceLineInfo(this, pos);
    }

    public string sourceLine(SourceLineInfo s)
    {
      return m_buf.Substring(s.startOfLine, s.endOfLine - s.startOfLine);
    }

    public string Saypos(int pos)
    {
      return sourceLineInfo(pos).ToString();
    }

    public Dfa m_start => (Dfa) m_tokens.starts[m_state];

    public YyLexer tokens
    {
      get => m_tokens;
      set
      {
        m_tokens = value;
        m_tokens.GetDfa();
      }
    }

    public int yypos => m_pch;

    public void yy_begin(string newstate)
    {
      m_state = newstate;
    }

    private bool Match(ref TOKEN tok, Dfa dfa)
    {
      char ch = PeekChar();
      int pch = m_pch;
      int mark = 0;
      if (m_debug)
      {
        Console.Write("state {0} with ", dfa.m_state);
        if (char.IsLetterOrDigit(ch) || char.IsPunctuation(ch))
          Console.WriteLine(ch);
        else
          Console.WriteLine("#" + (int) ch);
      }
      if (dfa.m_actions != null)
        mark = Mark();
      Dfa dfa1;
      if ((dfa1 = (Dfa) dfa.m_map[m_tokens.Filter(ch)]) == null)
      {
        if (m_debug)
          Console.Write("{0} no arc", dfa.m_state);
        if (dfa.m_actions != null)
        {
          if (m_debug)
            Console.WriteLine(" terminal");
          return TryActions(dfa, ref tok);
        }
        if (m_debug)
          Console.WriteLine(" fails");
        return false;
      }
      Advance();
      if (!Match(ref tok, dfa1))
      {
        if (m_debug)
          Console.WriteLine("back to {0} with {1}", dfa.m_state, ch);
        if (dfa.m_actions != null)
        {
          if (m_debug)
            Console.WriteLine("{0} succeeds", dfa.m_state);
          Restore(mark);
          return TryActions(dfa, ref tok);
        }
        if (m_debug)
          Console.WriteLine("{0} fails", dfa.m_state);
        return false;
      }
      if (dfa.m_reswds >= 0)
        ((ResWds) m_tokens.reswds[dfa.m_reswds]).Check(this, ref tok);
      if (m_debug)
      {
          Console.Write("{0} matched ", dfa.m_state);
          Console.WriteLine(m_pch <= m_buf.Length
              ? m_buf.Substring(pch, m_pch - pch)
              : m_buf.Substring(pch));
      }
      return true;
    }

    public void Start(StreamReader inFile)
    {
      m_state = "YYINITIAL";
      m_LineManager.lines = 1;
      m_LineManager.list = null;
      inFile = new StreamReader(inFile.BaseStream, m_tokens.m_encoding);
      m_buf = inFile.ReadToEnd();
      if (m_tokens.toupper)
        m_buf = m_buf.ToUpper();
      for (m_pch = 0; m_pch < m_buf.Length; ++m_pch)
      {
        if (m_buf[m_pch] == '\n')
          m_LineManager.newline(m_pch);
      }
      m_pch = 0;
    }

    public void Start(CsReader inFile)
    {
      m_state = "YYINITIAL";
      inFile = new CsReader(inFile, m_tokens.m_encoding);
      m_LineManager = inFile.lm;
      if (!inFile.Eof())
      {
        m_buf = inFile.ReadLine();
        while (!inFile.Eof())
        {
          m_buf += "\n";
          m_buf += inFile.ReadLine();
        }
      }
      if (m_tokens.toupper)
        m_buf = m_buf.ToUpper();
      m_pch = 0;
    }

    public void Start(string buf)
    {
      m_state = "YYINITIAL";
      m_LineManager.lines = 1;
      m_LineManager.list = null;
      m_buf = buf + "\n";
      for (m_pch = 0; m_pch < m_buf.Length; ++m_pch)
      {
        if (m_buf[m_pch] == '\n')
          m_LineManager.newline(m_pch);
      }
      if (m_tokens.toupper)
        m_buf = m_buf.ToUpper();
      m_pch = 0;
    }

    public TOKEN Next()
    {
      TOKEN tok = null;
      while (PeekChar() != char.MinValue)
      {
        Matching(true);
        if (!Match(ref tok, (Dfa) m_tokens.starts[m_state]))
        {
          if (yypos == 0)
            Console.Write("Check text encoding.. ");
          int num = PeekChar();
          m_tokens.erh.Error(new CSToolsStopException(2, this, "illegal character <" + (char) num + "> " + num));
          return null;
        }
        Matching(false);
        if (tok != null)
        {
          tok.pos = m_pch - yytext.Length;
          return tok;
        }
      }
      return null;
    }

    private bool TryActions(Dfa dfa, ref TOKEN tok)
    {
      int length = m_pch - m_startMatch;
      if (length == 0)
        return false;
      yytext = m_startMatch + length > m_buf.Length ? m_buf.Substring(m_startMatch) : m_buf.Substring(m_startMatch, length);
      Dfa.Action action = dfa.m_actions;
      bool reject = true;
      while (reject && action != null)
      {
        int aAct = action.a_act;
        reject = false;
        action = action.a_next;
        if (action == null && dfa.m_tokClass != "")
        {
          if (m_debug)
            Console.WriteLine("creating a " + dfa.m_tokClass);
          tok = (TOKEN) Tfactory.create(dfa.m_tokClass, this);
        }
        else
        {
          tok = m_tokens.OldAction(this, ref yytext, aAct, ref reject);
          if (m_debug && !reject)
            Console.WriteLine("Old action " + aAct);
        }
      }
      return !reject;
    }

    public char PeekChar()
    {
      if (m_pch < m_buf.Length)
        return m_buf[m_pch];
      return m_pch == m_buf.Length && m_tokens.usingEOF ? char.MaxValue : char.MinValue;
    }

    public void Advance()
    {
      ++m_pch;
    }

    public virtual int GetChar()
    {
      int num = PeekChar();
      ++m_pch;
      return num;
    }

    public void UnGetChar()
    {
      if (m_pch <= 0)
        return;
      --m_pch;
    }

    private int Mark()
    {
      return m_pch - m_startMatch;
    }

    private void Restore(int mark)
    {
      m_pch = m_startMatch + mark;
    }

    private void Matching(bool b)
    {
      m_matching = b;
      if (!b)
        return;
      m_startMatch = m_pch;
    }

    public _Enumerator GetEnumerator()
    {
      return new _Enumerator(this);
    }

    public void Reset()
    {
      m_pch = 0;
      m_LineManager.backto(0);
    }

    public class _Enumerator
    {
      private Lexer lxr;

      public _Enumerator(Lexer x)
      {
        lxr = x;
        Current = null;
      }

      public bool MoveNext()
      {
        Current = lxr.Next();
        return Current != null;
      }

      public TOKEN Current { get; private set; }

      public void Reset()
      {
        lxr.Reset();
      }
    }
  }
}
