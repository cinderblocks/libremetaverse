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
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;

namespace LibreMetaverse.LslTools
{
  public class YyLexer
  {
    public Encoding m_encoding = Encoding.ASCII;
    public Hashtable cats = new Hashtable();
    public Hashtable starts = new Hashtable();
    public Hashtable types = new Hashtable();
    public Hashtable tokens = new Hashtable();
    public Hashtable reswds = new Hashtable();
    public bool usingEOF;
    public bool toupper;
    public UnicodeCategory m_gencat;
    protected int[] arr;
    public ErrorHandler erh;

    public YyLexer(ErrorHandler eh)
    {
      erh = eh;
      UsingCat(UnicodeCategory.OtherPunctuation);
      m_gencat = UnicodeCategory.OtherPunctuation;
      Tfactory tfactory = new Tfactory(this, "TOKEN", TokenFactory);
    }

    public void GetDfa()
    {
      if (tokens.Count > 0)
        return;
      Serialiser serialiser = new Serialiser(arr);
      serialiser.VersionCheck();
      m_encoding = (Encoding) serialiser.Deserialise();
      toupper = (bool) serialiser.Deserialise();
      cats = (Hashtable) serialiser.Deserialise();
      m_gencat = (UnicodeCategory) serialiser.Deserialise();
      usingEOF = (bool) serialiser.Deserialise();
      starts = (Hashtable) serialiser.Deserialise();
      Dfa.SetTokens(this, starts);
      tokens = (Hashtable) serialiser.Deserialise();
      reswds = (Hashtable) serialiser.Deserialise();
    }

    public void EmitDfa(TextWriter outFile)
    {
      Console.WriteLine("Serializing the lexer");
      Serialiser serialiser = new Serialiser(outFile);
      serialiser.VersionCheck();
      serialiser.Serialise(m_encoding);
      serialiser.Serialise(toupper);
      serialiser.Serialise(cats);
      serialiser.Serialise(m_gencat);
      serialiser.Serialise(usingEOF);
      serialiser.Serialise(starts);
      serialiser.Serialise(tokens);
      serialiser.Serialise(reswds);
      outFile.WriteLine("0};");
    }

    public string InputEncoding
    {
      set => m_encoding = Charset.GetEncoding(value, ref toupper, erh);
    }

    protected object TokenFactory(Lexer yyl)
    {
      return new TOKEN(yyl);
    }

    public Charset UsingCat(UnicodeCategory cat)
    {
      if (cat == m_gencat)
      {
        for (int index = 0; index < 28; ++index)
        {
          if (Enum.IsDefined(typeof (UnicodeCategory), index))
          {
            UnicodeCategory cat1 = (UnicodeCategory) index;
            if (cat1 != UnicodeCategory.Surrogate && cats[cat1] == null)
            {
              UsingCat(cat1);
              m_gencat = cat1;
            }
          }
        }
        return (Charset) cats[cat];
      }
      if (cats[cat] != null)
        return (Charset) cats[cat];
      Charset charset = new Charset(cat);
      cats[cat] = charset;
      return charset;
    }

    internal void UsingChar(char ch)
    {
      Charset charset = UsingCat(char.GetUnicodeCategory(ch));
      if (charset.m_generic == ch)
      {
        while (charset.m_generic != char.MaxValue)
        {
          ++charset.m_generic;
          if (char.GetUnicodeCategory(charset.m_generic) == charset.m_cat && !charset.m_chars.Contains(charset.m_generic))
          {
            charset.m_chars[charset.m_generic] = true;
            return;
          }
        }
        charset.m_generic = ch;
      }
      else
        charset.m_chars[ch] = true;
    }

    internal char Filter(char ch)
    {
      Charset charset = (Charset) cats[char.GetUnicodeCategory(ch)] ?? (Charset) cats[m_gencat];
      if (charset.m_chars.Contains(ch))
        return ch;
      return charset.m_generic;
    }

    private bool testEOF(char ch)
    {
      return char.GetUnicodeCategory(ch) == UnicodeCategory.OtherNotAssigned;
    }

    private bool CharIsSymbol(char c)
    {
      UnicodeCategory unicodeCategory = char.GetUnicodeCategory(c);
      switch (unicodeCategory)
      {
        case UnicodeCategory.CurrencySymbol:
        case UnicodeCategory.ModifierSymbol:
        case UnicodeCategory.OtherSymbol:
          return true;
        default:
          return unicodeCategory == UnicodeCategory.MathSymbol;
      }
    }

    private bool CharIsSeparator(char c)
    {
      UnicodeCategory unicodeCategory = char.GetUnicodeCategory(c);
      switch (unicodeCategory)
      {
        case UnicodeCategory.LineSeparator:
        case UnicodeCategory.ParagraphSeparator:
          return true;
        default:
          return unicodeCategory == UnicodeCategory.SpaceSeparator;
      }
    }

    internal ChTest GetTest(string name)
    {
      try
      {
          object obj = Enum.Parse(typeof (UnicodeCategory), name);
          {
              UnicodeCategory unicodeCategory = (UnicodeCategory) obj;
              UsingCat(unicodeCategory);
              return new CatTest(unicodeCategory).Test;
          }
      }
      catch (Exception)
      {
      }
      string str1 = name;
      if (str1 != null)
      {
        string str2 = string.IsInterned(str1);
        if (str2 == (object) "Symbol")
        {
          UsingCat(UnicodeCategory.OtherSymbol);
          UsingCat(UnicodeCategory.ModifierSymbol);
          UsingCat(UnicodeCategory.CurrencySymbol);
          UsingCat(UnicodeCategory.MathSymbol);
          return CharIsSymbol;
        }
        if (str2 == (object) "Punctuation")
        {
          UsingCat(UnicodeCategory.OtherPunctuation);
          UsingCat(UnicodeCategory.FinalQuotePunctuation);
          UsingCat(UnicodeCategory.InitialQuotePunctuation);
          UsingCat(UnicodeCategory.ClosePunctuation);
          UsingCat(UnicodeCategory.OpenPunctuation);
          UsingCat(UnicodeCategory.DashPunctuation);
          UsingCat(UnicodeCategory.ConnectorPunctuation);
          return char.IsPunctuation;
        }
        if (str2 == (object) "Separator")
        {
          UsingCat(UnicodeCategory.ParagraphSeparator);
          UsingCat(UnicodeCategory.LineSeparator);
          UsingCat(UnicodeCategory.SpaceSeparator);
          return CharIsSeparator;
        }
        if (str2 == (object) "WhiteSpace")
        {
          UsingCat(UnicodeCategory.Control);
          UsingCat(UnicodeCategory.ParagraphSeparator);
          UsingCat(UnicodeCategory.LineSeparator);
          UsingCat(UnicodeCategory.SpaceSeparator);
          return char.IsWhiteSpace;
        }
        if (str2 == (object) "Number")
        {
          UsingCat(UnicodeCategory.OtherNumber);
          UsingCat(UnicodeCategory.LetterNumber);
          UsingCat(UnicodeCategory.DecimalDigitNumber);
          return char.IsNumber;
        }
        if (str2 == (object) "Digit")
        {
          UsingCat(UnicodeCategory.DecimalDigitNumber);
          return char.IsDigit;
        }
        if (str2 == (object) "Letter")
        {
          UsingCat(UnicodeCategory.OtherLetter);
          UsingCat(UnicodeCategory.ModifierLetter);
          UsingCat(UnicodeCategory.TitlecaseLetter);
          UsingCat(UnicodeCategory.LowercaseLetter);
          UsingCat(UnicodeCategory.UppercaseLetter);
          return char.IsLetter;
        }
        if (str2 == (object) "Lower")
        {
          UsingCat(UnicodeCategory.LowercaseLetter);
          return char.IsLower;
        }
        if (str2 == (object) "Upper")
        {
          UsingCat(UnicodeCategory.UppercaseLetter);
          return char.IsUpper;
        }
        if (str2 == (object) "EOF")
        {
          UsingCat(UnicodeCategory.OtherNotAssigned);
          UsingChar(char.MaxValue);
          usingEOF = true;
          return testEOF;
        }
      }
      erh.Error(new CSToolsException(24, "No such Charset " + name));
      return char.IsControl;
    }

    public virtual TOKEN OldAction(Lexer yyl, ref string yytext, int action, ref bool reject)
    {
      return null;
    }

    public IEnumerator GetEnumerator()
    {
      return tokens.Values.GetEnumerator();
    }
  }
}
