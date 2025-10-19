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
    private State state;
    private int pos;

    public CsReader(string data)
    {
      m_stream = new StringReader(data);
      state = State.copy;
      back = -1;
    }

    public CsReader(string fileName, Encoding enc)
    {
      fname = fileName;
      m_stream = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read), enc);
      state = State.copy;
      back = -1;
    }

    public CsReader(CsReader inf, Encoding enc)
    {
      fname = inf.fname;
      m_stream = !(inf.m_stream is StreamReader reader) ? new StreamReader(inf.m_stream.ReadToEnd()) : (TextReader) new StreamReader(reader.BaseStream, enc);
      state = State.copy;
      back = -1;
    }

    public bool Eof()
    {
      return state == State.at_eof;
    }

    public int Read(char[] arr, int offset, int count)
    {
      int num1 = 0;
      while (count > 0)
      {
        int num2 = Read();
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
        num1 = Read();
        if ((ushort) num1 != 13)
        {
          if (num1 >= 0 && (ushort) num1 != 10)
            chArray[length++] = (char) num1;
          else
            break;
        }
      }
      if (num1 < 0)
        state = State.at_eof;
      return new string(chArray, 0, length);
    }

    public int Read()
    {
      int len = 0;
      if (state == State.at_eof)
        return -1;
      int num;
      while (true)
      {
        do
        {
          do
          {
            if (back >= 0)
            {
              num = back;
              back = -1;
            }
            else
              num = state != State.at_eof ? m_stream.Read() : -1;
          }
          while (num == 13);
          while (sol && num == 35)
          {
            while (num != 32)
              num = m_stream.Read();
            lm.lines = 0;
            while (num == 32)
              num = m_stream.Read();
            for (; num >= 48 && num <= 57; num = m_stream.Read())
              lm.lines = lm.lines * 10 + (num - 48);
            while (num == 32)
              num = m_stream.Read();
            if (num == 34)
            {
              fname = "";
              for (num = m_stream.Read(); num != 34; num = m_stream.Read())
                fname += num.ToString();
            }
            while (num != 10)
              num = m_stream.Read();
            if (num == 13)
              num = m_stream.Read();
          }
          if (num < 0)
          {
            if (state == State.sol)
              num = 47;
            state = State.at_eof;
            ++pos;
            return num;
          }
          sol = false;
          switch (state)
          {
            case State.copy:
              switch (num)
              {
                case 10:
                  lm.newline(pos);
                  sol = true;
                  break;
                case 47:
                  state = State.sol;
                  continue;
              }
              ++pos;
              return num;
            case State.sol:
              switch (num)
              {
                case 42:
                  state = State.c_com;
                  continue;
                case 47:
                  len = 2;
                  state = State.cpp_com;
                  continue;
                default:
                  back = num;
                  state = State.copy;
                  ++pos;
                  return 47;
              }
            case State.c_com:
              ++len;
              if (num == 10)
              {
                lm.newline(pos);
                len = 0;
                sol = true;
              }
              continue;
            case State.cpp_com:
              goto label_45;
            case State.c_star:
              goto label_41;
            default:
              continue;
          }
        }
        while (num != 42);
        state = State.c_star;
        continue;
label_41:
        ++len;
        switch (num)
        {
          case 42:
            state = State.c_star;
            continue;
          case 47:
            lm.comment(pos, len);
            state = State.copy;
            continue;
          default:
            state = State.c_com;
            continue;
        }
label_45:
        if (num != 10)
          ++len;
        else
          break;
      }
      state = State.copy;
      sol = true;
      ++pos;
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
