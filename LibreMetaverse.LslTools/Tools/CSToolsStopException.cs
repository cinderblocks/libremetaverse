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

namespace Tools.Tools
{
  public class CSToolsStopException : CSToolsException
  {
    public CSToolsStopException(int n, string s)
      : base(n, s)
    {
    }

    public CSToolsStopException(int n, Lexer yl, string s)
      : base(n, yl, yl.yytext, s)
    {
    }

    public CSToolsStopException(int n, Lexer yl, string yy, string s)
      : base(n, yl, yl.m_pch, yy, s)
    {
    }

    public CSToolsStopException(int n, Lexer yl, int p, string y, string s)
      : base(n, yl, p, y, s)
    {
    }

    public CSToolsStopException(int n, TOKEN t, string s)
      : base(n, t, s)
    {
    }

    public CSToolsStopException(int n, SYMBOL t, string s)
      : base(n, t, s)
    {
    }

    public CSToolsStopException(int en, SourceLineInfo s, string y, string m)
      : base(en, s, y, m)
    {
    }

    public override void Handle(ErrorHandler erh)
    {
      throw this;
    }
  }
}
