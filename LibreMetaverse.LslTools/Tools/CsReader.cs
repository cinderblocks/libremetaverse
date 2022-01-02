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

using System.IO;
using System.Text;

namespace LibreMetaverse.LslTools
{
  public class CsReader
  {
    public string fname = "";
    public LineManager lm = new LineManager();
    private bool sol = true;
    private TextReader m_stream;
    private int back;
    private CsReader.State state;
    private int pos;

    public CsReader(string data)
    {
      this.m_stream = (TextReader) new StringReader(data);
      this.state = CsReader.State.copy;
      this.back = -1;
    }

    public CsReader(string fileName, Encoding enc)
    {
      this.fname = fileName;
      this.m_stream = (TextReader) new StreamReader((Stream) new FileStream(fileName, FileMode.Open, FileAccess.Read), enc);
      this.state = CsReader.State.copy;
      this.back = -1;
    }

    public CsReader(CsReader inf, Encoding enc)
    {
      this.fname = inf.fname;
      this.m_stream = !(inf.m_stream is StreamReader) ? (TextReader) new StreamReader(inf.m_stream.ReadToEnd()) : (TextReader) new StreamReader(((StreamReader) inf.m_stream).BaseStream, enc);
      this.state = CsReader.State.copy;
      this.back = -1;
    }

    public bool Eof()
    {
      return this.state == CsReader.State.at_eof;
    }

    public int Read(char[] arr, int offset, int count)
    {
      int num1 = 0;
      while (count > 0)
      {
        int num2 = this.Read();
        if (num2 >= 0)
        {
          arr[offset + num1] = (char) num2;
          --count;
          ++num1;
        }
        else
          break;
      }
      return num1;
    }

    public string ReadLine()
    {
      int num1 = 0;
      char[] chArray = new char[1024];
      int num2 = 1024;
      int length = 0;
      for (; num2 > 0; --num2)
      {
        num1 = this.Read();
        if ((ushort) num1 != (ushort) 13)
        {
          if (num1 >= 0 && (ushort) num1 != (ushort) 10)
            chArray[length++] = (char) num1;
          else
            break;
        }
      }
      if (num1 < 0)
        this.state = CsReader.State.at_eof;
      return new string(chArray, 0, length);
    }

    public int Read()
    {
      int len = 0;
      if (this.state == CsReader.State.at_eof)
        return -1;
      int num;
      while (true)
      {
        do
        {
          do
          {
            if (this.back >= 0)
            {
              num = this.back;
              this.back = -1;
            }
            else
              num = this.state != CsReader.State.at_eof ? this.m_stream.Read() : -1;
          }
          while (num == 13);
          while (this.sol && num == 35)
          {
            while (num != 32)
              num = this.m_stream.Read();
            this.lm.lines = 0;
            while (num == 32)
              num = this.m_stream.Read();
            for (; num >= 48 && num <= 57; num = this.m_stream.Read())
              this.lm.lines = this.lm.lines * 10 + (num - 48);
            while (num == 32)
              num = this.m_stream.Read();
            if (num == 34)
            {
              this.fname = "";
              for (num = this.m_stream.Read(); num != 34; num = this.m_stream.Read())
                this.fname += (string) (object) num;
            }
            while (num != 10)
              num = this.m_stream.Read();
            if (num == 13)
              num = this.m_stream.Read();
          }
          if (num < 0)
          {
            if (this.state == CsReader.State.sol)
              num = 47;
            this.state = CsReader.State.at_eof;
            ++this.pos;
            return num;
          }
          this.sol = false;
          switch (this.state)
          {
            case CsReader.State.copy:
              switch (num)
              {
                case 10:
                  this.lm.newline(this.pos);
                  this.sol = true;
                  break;
                case 47:
                  this.state = CsReader.State.sol;
                  continue;
              }
              ++this.pos;
              return num;
            case CsReader.State.sol:
              switch (num)
              {
                case 42:
                  this.state = CsReader.State.c_com;
                  continue;
                case 47:
                  len = 2;
                  this.state = CsReader.State.cpp_com;
                  continue;
                default:
                  this.back = num;
                  this.state = CsReader.State.copy;
                  ++this.pos;
                  return 47;
              }
            case CsReader.State.c_com:
              ++len;
              if (num == 10)
              {
                this.lm.newline(this.pos);
                len = 0;
                this.sol = true;
              }
              continue;
            case CsReader.State.cpp_com:
              goto label_45;
            case CsReader.State.c_star:
              goto label_41;
            default:
              continue;
          }
        }
        while (num != 42);
        this.state = CsReader.State.c_star;
        continue;
label_41:
        ++len;
        switch (num)
        {
          case 42:
            this.state = CsReader.State.c_star;
            continue;
          case 47:
            this.lm.comment(this.pos, len);
            this.state = CsReader.State.copy;
            continue;
          default:
            this.state = CsReader.State.c_com;
            continue;
        }
label_45:
        if (num != 10)
          ++len;
        else
          break;
      }
      this.state = CsReader.State.copy;
      this.sol = true;
      ++this.pos;
      return num;
    }

    private enum State
    {
      copy,
      sol,
      c_com,
      cpp_com,
      c_star,
      at_eof,
      transparent,
    }
  }
}
