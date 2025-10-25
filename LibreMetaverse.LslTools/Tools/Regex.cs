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
  public class Regex
  {
    public Regex m_sub;

    public Regex(TokensGen tks, int p, string str)
    {
      int length = str.Length;
      int num1 = 0;
      int num2 = 0;
      int num3 = 0;
      m_sub = null;
      if (length == 0)
        return;
      int startIndex;
      if (str[0] == '(')
      {
        int index;
        for (index = 1; index < length; ++index)
        {
          if (str[index] == '\\')
            ++index;
          else if (str[index] == ']' && num2 > 0)
            num2 = 0;
          else if (num2 <= 0)
          {
            if (str[index] == '"' || str[index] == '\'')
            {
              if (num3 == str[index])
                num3 = 0;
              else if (num3 == 0)
                num3 = str[index];
            }
            else if (num3 <= 0)
            {
              if (str[index] == '[')
                ++num2;
              else if (str[index] == '(')
                ++num1;
              else if (str[index] == ')' && num1-- == 0)
                break;
            }
          }
        }
        if (index != length)
        {
          m_sub = new Regex(tks, p + 1, str.Substring(1, index - 1));
          startIndex = index + 1;
        }
        else
          goto label_99;
      }
      else if (str[0] == '[')
      {
        int index;
        for (index = 1; index < length && str[index] != ']'; ++index)
        {
          if (str[index] == '\\')
            ++index;
        }
        if (index != length)
        {
          m_sub = new ReRange(tks, str.Substring(0, index + 1));
          startIndex = index + 1;
        }
        else
          goto label_99;
      }
      else if (str[0] == '\'' || str[0] == '"')
      {
        StringBuilder stringBuilder = new StringBuilder();
        int index;
        for (index = 1; index < length && str[index] != str[0]; ++index)
        {
          if (str[index] == '\\')
          {
            char ch = str[++index];
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
                switch (ch)
                {
                  case '\n':
                    continue;
                  case '"':
                    stringBuilder.Append('"');
                    continue;
                  case '\'':
                    stringBuilder.Append('\'');
                    continue;
                  case '0':
                    stringBuilder.Append(char.MinValue);
                    continue;
                  case '\\':
                    stringBuilder.Append('\\');
                    continue;
                  case 'n':
                    stringBuilder.Append('\n');
                    continue;
                  default:
                    stringBuilder.Append(str[index]);
                    continue;
                }
            }
          }
          else
            stringBuilder.Append(str[index]);
        }
        if (index != length)
        {
          startIndex = index + 1;
          m_sub = new ReStr(tks, stringBuilder.ToString());
        }
        else
          goto label_99;
      }
      else if (str.StartsWith("U\"") || str.StartsWith("U'"))
      {
        StringBuilder stringBuilder = new StringBuilder();
        int index;
        for (index = 2; index < length && str[index] != str[1]; ++index)
        {
          if (str[index] == '\\')
          {
            char ch = str[++index];
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
                switch (ch)
                {
                  case '\n':
                    continue;
                  case '"':
                    stringBuilder.Append('"');
                    continue;
                  case '\'':
                    stringBuilder.Append('\'');
                    continue;
                  case '\\':
                    stringBuilder.Append('\\');
                    continue;
                  case 'n':
                    stringBuilder.Append('\n');
                    continue;
                  default:
                    stringBuilder.Append(str[index]);
                    continue;
                }
            }
          }
          else
            stringBuilder.Append(str[index]);
        }
        if (index != length)
        {
          startIndex = index + 1;
          m_sub = new ReUStr(tks, stringBuilder.ToString());
        }
        else
          goto label_99;
      }
      else if (str[0] == '\\')
      {
        char ch1;
        char ch2 = ch1 = str[1];
        switch (ch2)
        {
          case 'r':
            ch1 = '\r';
            break;
          case 't':
            ch1 = '\t';
            break;
          case 'v':
            ch1 = '\v';
            break;
          default:
            if (ch2 == 'n')
            {
              ch1 = '\n';
              break;
            }
            break;
        }
        m_sub = new ReStr(tks, ch1);
        startIndex = 2;
      }
      else if (str[0] == '{')
      {
        int index = 1;
        while (index < length && str[index] != '}')
          ++index;
        if (index != length)
        {
          string str1 = str.Substring(1, index - 1);
          string define = (string) tks.defines[str1];
          m_sub = define != null ? new Regex(tks, p + 1, define) : new ReCategory(tks, str1);
          startIndex = index + 1;
        }
        else
          goto label_99;
      }
      else
      {
        this.m_sub = str[0] != '.' ? (Regex) new ReStr(tks, str[0]) : (Regex) new ReRange(tks, "[^\n]");
        startIndex = 1;
      }
      if (startIndex >= length)
        return;
      if (str[startIndex] == '?')
      {
        m_sub = new ReOpt(m_sub);
        ++startIndex;
      }
      else if (str[startIndex] == '*')
      {
        m_sub = new ReStar(m_sub);
        ++startIndex;
      }
      else if (str[startIndex] == '+')
      {
        m_sub = new RePlus(m_sub);
        ++startIndex;
      }
      if (startIndex >= length)
        return;
      if (str[startIndex] == '|')
      {
        m_sub = new ReAlt(tks, m_sub, p + startIndex + 1, str.Substring(startIndex + 1, length - startIndex - 1));
        return;
      }
      if (startIndex >= length)
        return;
      m_sub = new ReCat(tks, m_sub, p + startIndex, str.Substring(startIndex, length - startIndex));
      return;
label_99:
      tks.erh.Error(new CSToolsFatalException(1, tks.sourceLineInfo(p), str, "ill-formed regular expression " + str));
    }

    protected Regex()
    {
    }

    public virtual void Print(TextWriter s)
    {
      if (m_sub == null)
        return;
      m_sub.Print(s);
    }

    public virtual bool Match(char ch)
    {
      return false;
    }

    public int Match(string str)
    {
      return Match(str, 0, str.Length);
    }

    public virtual int Match(string str, int pos, int max)
    {
      if (max < 0)
        return -1;
      if (m_sub != null)
        return m_sub.Match(str, pos, max);
      return 0;
    }

    public virtual void Build(Nfa nfa)
    {
      if (m_sub != null)
      {
        Nfa nfa1 = new Nfa(nfa.m_tks, m_sub);
        nfa.AddEps(nfa1);
        nfa1.m_end.AddEps(nfa.m_end);
      }
      else
        nfa.AddEps(nfa.m_end);
    }
  }
}
