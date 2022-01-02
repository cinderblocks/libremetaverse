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
  public class SourceLineInfo
  {
    public int lineNumber;
    public int charPosition;
    public int startOfLine;
    public int endOfLine;
    public int rawCharPosition;
    public Lexer lxr;

    public SourceLineInfo(int pos)
    {
      this.lineNumber = 1;
      this.startOfLine = 0;
      this.endOfLine = this.rawCharPosition = this.charPosition = pos;
    }

    public SourceLineInfo(LineManager lm, int pos)
    {
      this.lineNumber = lm.lines;
      this.startOfLine = 0;
      this.endOfLine = lm.end;
      this.charPosition = pos;
      this.rawCharPosition = pos;
      LineList lineList = lm.list;
      while (lineList != null)
      {
        if (lineList.head > pos)
        {
          this.endOfLine = lineList.head;
          lineList = lineList.tail;
          --this.lineNumber;
        }
        else
        {
          this.startOfLine = lineList.head + 1;
          this.rawCharPosition = lineList.getpos(pos);
          this.charPosition = pos - this.startOfLine + 1;
          break;
        }
      }
    }

    public SourceLineInfo(Lexer lx, int pos)
      : this(lx.m_LineManager, pos)
    {
      this.lxr = lx;
    }

    public override string ToString()
    {
      return "Line " + (object) this.lineNumber + ", char " + (object) this.rawCharPosition;
    }

    public string sourceLine
    {
      get
      {
        if (this.lxr == null)
          return "";
        return this.lxr.sourceLine(this);
      }
    }
  }
}
