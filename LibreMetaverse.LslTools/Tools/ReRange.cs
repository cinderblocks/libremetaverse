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

using System.Collections;
using System.IO;
using System.Text;

namespace LibreMetaverse.LSLTools.Tools
{
  internal class ReRange : Regex
  {
    public Hashtable m_map = new Hashtable();
    public bool m_invert;

    public ReRange(TokensGen tks, string str)
    {
      StringBuilder stringBuilder = new StringBuilder();
      int num1 = str.Length - 1;
      for (int index = 1; index < num1; ++index)
      {
        if (str[index] == '\\')
        {
          if (index + 1 < num1)
            ++index;
          if (str[index] >= '0' && str[index] <= '7')
          {
            int num2;
            for (num2 = str[index++] - 48; index < num1 && str[index] >= '0' && str[index] <= '7'; ++index)
              num2 = num2 * 8 + str[index] - 48;
            stringBuilder.Append((char) num2);
          }
          else
          {
            char ch = str[index];
            switch (ch)
            {
              case 'r':
                stringBuilder.Append('\r');
                continue;
              case 't':
                stringBuilder.Append('\t');
                continue;
              case 'v':
                stringBuilder.Append('\v');
                continue;
              default:
                if (ch == 'n')
                {
                  stringBuilder.Append('\n');
                  continue;
                }
                stringBuilder.Append(str[index]);
                continue;
            }
          }
        }
        else
          stringBuilder.Append(str[index]);
      }
      int length = stringBuilder.Length;
      if (length > 0 && stringBuilder[0] == '^')
      {
        m_invert = true;
        stringBuilder.Remove(0, 1).Append(char.MinValue).Append(char.MaxValue);
      }
      for (int index1 = 0; index1 < length; ++index1)
      {
        if (index1 + 1 < length && stringBuilder[index1 + 1] == '-')
        {
          for (int index2 = stringBuilder[index1]; index2 <= stringBuilder[index1 + 2]; ++index2)
            Set(tks, (char) index2);
          index1 += 2;
        }
        else
          Set(tks, stringBuilder[index1]);
      }
    }

    public override void Print(TextWriter s)
    {
      s.Write("[");
      if (m_invert)
        s.Write("^");
      foreach (char key in m_map.Keys)
        s.Write(key);
      s.Write("]");
    }

    private void Set(TokensGen tks, char ch)
    {
      m_map[ch] = true;
      tks.m_tokens.UsingChar(ch);
    }

    public override bool Match(char ch)
    {
      if (m_invert)
        return !m_map.Contains(ch);
      return m_map.Contains(ch);
    }

    public override int Match(string str, int pos, int max)
    {
      return max < pos || !Match(str[pos]) ? -1 : 1;
    }

    public override void Build(Nfa nfa)
    {
      nfa.AddArcEx(this, nfa.m_end);
    }
  }
}
