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
      this.erh = eh;
      this.UsingCat(UnicodeCategory.OtherPunctuation);
      this.m_gencat = UnicodeCategory.OtherPunctuation;
      Tfactory tfactory = new Tfactory(this, "TOKEN", new TCreator(this.Tokenfactory));
    }

    public void GetDfa()
    {
      if (this.tokens.Count > 0)
        return;
      Serialiser serialiser = new Serialiser(this.arr);
      serialiser.VersionCheck();
      this.m_encoding = (Encoding) serialiser.Deserialise();
      this.toupper = (bool) serialiser.Deserialise();
      this.cats = (Hashtable) serialiser.Deserialise();
      this.m_gencat = (UnicodeCategory) serialiser.Deserialise();
      this.usingEOF = (bool) serialiser.Deserialise();
      this.starts = (Hashtable) serialiser.Deserialise();
      Dfa.SetTokens(this, this.starts);
      this.tokens = (Hashtable) serialiser.Deserialise();
      this.reswds = (Hashtable) serialiser.Deserialise();
    }

    public void EmitDfa(TextWriter outFile)
    {
      Console.WriteLine("Serializing the lexer");
      Serialiser serialiser = new Serialiser(outFile);
      serialiser.VersionCheck();
      serialiser.Serialise((object) this.m_encoding);
      serialiser.Serialise((object) this.toupper);
      serialiser.Serialise((object) this.cats);
      serialiser.Serialise((object) this.m_gencat);
      serialiser.Serialise((object) this.usingEOF);
      serialiser.Serialise((object) this.starts);
      serialiser.Serialise((object) this.tokens);
      serialiser.Serialise((object) this.reswds);
      outFile.WriteLine("0};");
    }

    public string InputEncoding
    {
      set => this.m_encoding = Charset.GetEncoding(value, ref this.toupper, this.erh);
    }

    protected object Tokenfactory(Lexer yyl)
    {
      return (object) new TOKEN(yyl);
    }

    public Charset UsingCat(UnicodeCategory cat)
    {
      if (cat == this.m_gencat)
      {
        for (int index = 0; index < 28; ++index)
        {
          if (Enum.IsDefined(typeof (UnicodeCategory), (object) index))
          {
            UnicodeCategory cat1 = (UnicodeCategory) index;
            if (cat1 != UnicodeCategory.Surrogate && this.cats[(object) cat1] == null)
            {
              this.UsingCat(cat1);
              this.m_gencat = cat1;
            }
          }
        }
        return (Charset) this.cats[(object) cat];
      }
      if (this.cats[(object) cat] != null)
        return (Charset) this.cats[(object) cat];
      Charset charset = new Charset(cat);
      this.cats[(object) cat] = (object) charset;
      return charset;
    }

    internal void UsingChar(char ch)
    {
      Charset charset = this.UsingCat(char.GetUnicodeCategory(ch));
      if ((int) charset.m_generic == (int) ch)
      {
        while (charset.m_generic != char.MaxValue)
        {
          ++charset.m_generic;
          if (char.GetUnicodeCategory(charset.m_generic) == charset.m_cat && !charset.m_chars.Contains((object) charset.m_generic))
          {
            charset.m_chars[(object) charset.m_generic] = (object) true;
            return;
          }
        }
        charset.m_generic = ch;
      }
      else
        charset.m_chars[(object) ch] = (object) true;
    }

    internal char Filter(char ch)
    {
      Charset charset = (Charset) this.cats[(object) char.GetUnicodeCategory(ch)] ?? (Charset) this.cats[(object) this.m_gencat];
      if (charset.m_chars.Contains((object) ch))
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
        if (obj != null)
        {
          UnicodeCategory unicodeCategory = (UnicodeCategory) obj;
          this.UsingCat(unicodeCategory);
          return new ChTest(new CatTest(unicodeCategory).Test);
        }
      }
      catch (Exception)
      {
      }
      string str1 = name;
      if (str1 != null)
      {
        string str2 = string.IsInterned(str1);
        if ((object) str2 == (object) "Symbol")
        {
          this.UsingCat(UnicodeCategory.OtherSymbol);
          this.UsingCat(UnicodeCategory.ModifierSymbol);
          this.UsingCat(UnicodeCategory.CurrencySymbol);
          this.UsingCat(UnicodeCategory.MathSymbol);
          return new ChTest(this.CharIsSymbol);
        }
        if ((object) str2 == (object) "Punctuation")
        {
          this.UsingCat(UnicodeCategory.OtherPunctuation);
          this.UsingCat(UnicodeCategory.FinalQuotePunctuation);
          this.UsingCat(UnicodeCategory.InitialQuotePunctuation);
          this.UsingCat(UnicodeCategory.ClosePunctuation);
          this.UsingCat(UnicodeCategory.OpenPunctuation);
          this.UsingCat(UnicodeCategory.DashPunctuation);
          this.UsingCat(UnicodeCategory.ConnectorPunctuation);
          return new ChTest(char.IsPunctuation);
        }
        if ((object) str2 == (object) "Separator")
        {
          this.UsingCat(UnicodeCategory.ParagraphSeparator);
          this.UsingCat(UnicodeCategory.LineSeparator);
          this.UsingCat(UnicodeCategory.SpaceSeparator);
          return new ChTest(this.CharIsSeparator);
        }
        if ((object) str2 == (object) "WhiteSpace")
        {
          this.UsingCat(UnicodeCategory.Control);
          this.UsingCat(UnicodeCategory.ParagraphSeparator);
          this.UsingCat(UnicodeCategory.LineSeparator);
          this.UsingCat(UnicodeCategory.SpaceSeparator);
          return new ChTest(char.IsWhiteSpace);
        }
        if ((object) str2 == (object) "Number")
        {
          this.UsingCat(UnicodeCategory.OtherNumber);
          this.UsingCat(UnicodeCategory.LetterNumber);
          this.UsingCat(UnicodeCategory.DecimalDigitNumber);
          return new ChTest(char.IsNumber);
        }
        if ((object) str2 == (object) "Digit")
        {
          this.UsingCat(UnicodeCategory.DecimalDigitNumber);
          return new ChTest(char.IsDigit);
        }
        if ((object) str2 == (object) "Letter")
        {
          this.UsingCat(UnicodeCategory.OtherLetter);
          this.UsingCat(UnicodeCategory.ModifierLetter);
          this.UsingCat(UnicodeCategory.TitlecaseLetter);
          this.UsingCat(UnicodeCategory.LowercaseLetter);
          this.UsingCat(UnicodeCategory.UppercaseLetter);
          return new ChTest(char.IsLetter);
        }
        if ((object) str2 == (object) "Lower")
        {
          this.UsingCat(UnicodeCategory.LowercaseLetter);
          return new ChTest(char.IsLower);
        }
        if ((object) str2 == (object) "Upper")
        {
          this.UsingCat(UnicodeCategory.UppercaseLetter);
          return new ChTest(char.IsUpper);
        }
        if ((object) str2 == (object) "EOF")
        {
          this.UsingCat(UnicodeCategory.OtherNotAssigned);
          this.UsingChar(char.MaxValue);
          this.usingEOF = true;
          return new ChTest(this.testEOF);
        }
      }
      this.erh.Error(new CSToolsException(24, "No such Charset " + name));
      return new ChTest(char.IsControl);
    }

    public virtual TOKEN OldAction(Lexer yyl, ref string yytext, int action, ref bool reject)
    {
      return (TOKEN) null;
    }

    public IEnumerator GetEnumerator()
    {
      return this.tokens.Values.GetEnumerator();
    }
  }
}
