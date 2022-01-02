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

using System;
using System.IO;

namespace LibreMetaverse.LslTools
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
      this.m_state = "YYINITIAL";
      this.tokens = tks;
    }

    public SourceLineInfo sourceLineInfo(int pos)
    {
      return new SourceLineInfo(this, pos);
    }

    public string sourceLine(SourceLineInfo s)
    {
      return this.m_buf.Substring(s.startOfLine, s.endOfLine - s.startOfLine);
    }

    public string Saypos(int pos)
    {
      return this.sourceLineInfo(pos).ToString();
    }

    public Dfa m_start => (Dfa) this.m_tokens.starts[(object) this.m_state];

    public YyLexer tokens
    {
      get => this.m_tokens;
      set
      {
        this.m_tokens = value;
        this.m_tokens.GetDfa();
      }
    }

    public int yypos => this.m_pch;

    public void yy_begin(string newstate)
    {
      this.m_state = newstate;
    }

    private bool Match(ref TOKEN tok, Dfa dfa)
    {
      char ch = this.PeekChar();
      int pch = this.m_pch;
      int mark = 0;
      if (this.m_debug)
      {
        Console.Write("state {0} with ", (object) dfa.m_state);
        if (char.IsLetterOrDigit(ch) || char.IsPunctuation(ch))
          Console.WriteLine(ch);
        else
          Console.WriteLine("#" + (object) (int) ch);
      }
      if (dfa.m_actions != null)
        mark = this.Mark();
      Dfa dfa1;
      if ((dfa1 = (Dfa) dfa.m_map[(object) this.m_tokens.Filter(ch)]) == null)
      {
        if (this.m_debug)
          Console.Write("{0} no arc", (object) dfa.m_state);
        if (dfa.m_actions != null)
        {
          if (this.m_debug)
            Console.WriteLine(" terminal");
          return this.TryActions(dfa, ref tok);
        }
        if (this.m_debug)
          Console.WriteLine(" fails");
        return false;
      }
      this.Advance();
      if (!this.Match(ref tok, dfa1))
      {
        if (this.m_debug)
          Console.WriteLine("back to {0} with {1}", (object) dfa.m_state, (object) ch);
        if (dfa.m_actions != null)
        {
          if (this.m_debug)
            Console.WriteLine("{0} succeeds", (object) dfa.m_state);
          this.Restore(mark);
          return this.TryActions(dfa, ref tok);
        }
        if (this.m_debug)
          Console.WriteLine("{0} fails", (object) dfa.m_state);
        return false;
      }
      if (dfa.m_reswds >= 0)
        ((ResWds) this.m_tokens.reswds[(object) dfa.m_reswds]).Check(this, ref tok);
      if (this.m_debug)
      {
        Console.Write("{0} matched ", (object) dfa.m_state);
        if (this.m_pch <= this.m_buf.Length)
          Console.WriteLine(this.m_buf.Substring(pch, this.m_pch - pch));
        else
          Console.WriteLine(this.m_buf.Substring(pch));
      }
      return true;
    }

    public void Start(StreamReader inFile)
    {
      this.m_state = "YYINITIAL";
      this.m_LineManager.lines = 1;
      this.m_LineManager.list = (LineList) null;
      inFile = new StreamReader(inFile.BaseStream, this.m_tokens.m_encoding);
      this.m_buf = inFile.ReadToEnd();
      if (this.m_tokens.toupper)
        this.m_buf = this.m_buf.ToUpper();
      for (this.m_pch = 0; this.m_pch < this.m_buf.Length; ++this.m_pch)
      {
        if (this.m_buf[this.m_pch] == '\n')
          this.m_LineManager.newline(this.m_pch);
      }
      this.m_pch = 0;
    }

    public void Start(CsReader inFile)
    {
      this.m_state = "YYINITIAL";
      inFile = new CsReader(inFile, this.m_tokens.m_encoding);
      this.m_LineManager = inFile.lm;
      if (!inFile.Eof())
      {
        this.m_buf = inFile.ReadLine();
        while (!inFile.Eof())
        {
          this.m_buf += "\n";
          this.m_buf += inFile.ReadLine();
        }
      }
      if (this.m_tokens.toupper)
        this.m_buf = this.m_buf.ToUpper();
      this.m_pch = 0;
    }

    public void Start(string buf)
    {
      this.m_state = "YYINITIAL";
      this.m_LineManager.lines = 1;
      this.m_LineManager.list = (LineList) null;
      this.m_buf = buf + "\n";
      for (this.m_pch = 0; this.m_pch < this.m_buf.Length; ++this.m_pch)
      {
        if (this.m_buf[this.m_pch] == '\n')
          this.m_LineManager.newline(this.m_pch);
      }
      if (this.m_tokens.toupper)
        this.m_buf = this.m_buf.ToUpper();
      this.m_pch = 0;
    }

    public TOKEN Next()
    {
      TOKEN tok = (TOKEN) null;
      while (this.PeekChar() != char.MinValue)
      {
        this.Matching(true);
        if (!this.Match(ref tok, (Dfa) this.m_tokens.starts[(object) this.m_state]))
        {
          if (this.yypos == 0)
            Console.Write("Check text encoding.. ");
          int num = (int) this.PeekChar();
          this.m_tokens.erh.Error((CSToolsException) new CSToolsStopException(2, this, "illegal character <" + (object) (char) num + "> " + (object) num));
          return (TOKEN) null;
        }
        this.Matching(false);
        if (tok != null)
        {
          tok.pos = this.m_pch - this.yytext.Length;
          return tok;
        }
      }
      return (TOKEN) null;
    }

    private bool TryActions(Dfa dfa, ref TOKEN tok)
    {
      int length = this.m_pch - this.m_startMatch;
      if (length == 0)
        return false;
      this.yytext = this.m_startMatch + length > this.m_buf.Length ? this.m_buf.Substring(this.m_startMatch) : this.m_buf.Substring(this.m_startMatch, length);
      Dfa.Action action = dfa.m_actions;
      bool reject = true;
      while (reject && action != null)
      {
        int aAct = action.a_act;
        reject = false;
        action = action.a_next;
        if (action == null && dfa.m_tokClass != "")
        {
          if (this.m_debug)
            Console.WriteLine("creating a " + dfa.m_tokClass);
          tok = (TOKEN) Tfactory.create(dfa.m_tokClass, this);
        }
        else
        {
          tok = this.m_tokens.OldAction(this, ref this.yytext, aAct, ref reject);
          if (this.m_debug && !reject)
            Console.WriteLine("Old action " + (object) aAct);
        }
      }
      return !reject;
    }

    public char PeekChar()
    {
      if (this.m_pch < this.m_buf.Length)
        return this.m_buf[this.m_pch];
      return this.m_pch == this.m_buf.Length && this.m_tokens.usingEOF ? char.MaxValue : char.MinValue;
    }

    public void Advance()
    {
      ++this.m_pch;
    }

    public virtual int GetChar()
    {
      int num = (int) this.PeekChar();
      ++this.m_pch;
      return num;
    }

    public void UnGetChar()
    {
      if (this.m_pch <= 0)
        return;
      --this.m_pch;
    }

    private int Mark()
    {
      return this.m_pch - this.m_startMatch;
    }

    private void Restore(int mark)
    {
      this.m_pch = this.m_startMatch + mark;
    }

    private void Matching(bool b)
    {
      this.m_matching = b;
      if (!b)
        return;
      this.m_startMatch = this.m_pch;
    }

    public Lexer._Enumerator GetEnumerator()
    {
      return new Lexer._Enumerator(this);
    }

    public void Reset()
    {
      this.m_pch = 0;
      this.m_LineManager.backto(0);
    }

    public class _Enumerator
    {
      private Lexer lxr;
      private TOKEN t;

      public _Enumerator(Lexer x)
      {
        this.lxr = x;
        this.t = (TOKEN) null;
      }

      public bool MoveNext()
      {
        this.t = this.lxr.Next();
        return this.t != null;
      }

      public TOKEN Current => this.t;

      public void Reset()
      {
        this.lxr.Reset();
      }
    }
  }
}
