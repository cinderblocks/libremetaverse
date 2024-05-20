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

namespace LibreMetaverse.LslTools
{
  public class TokClassDef
  {
    public string m_refToken = "";
    public string m_initialisation = "";
    public string m_implement = "";
    public string m_name = "";
    public int m_yynum;

    public TokClassDef(GenBase gbs, string name, string bas)
    {
      if (gbs is TokensGen)
      {
        TokensGen tokensGen = (TokensGen) gbs;
        this.m_name = name;
        tokensGen.m_tokens.tokens[(object) name] = (object) this;
        this.m_refToken = bas;
      }
      this.m_yynum = ++gbs.LastSymbol;
    }

    private TokClassDef()
    {
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new TokClassDef();
      TokClassDef tokClassDef = (TokClassDef) o;
      if (s.Encode)
      {
        s.Serialise((object) tokClassDef.m_name);
        s.Serialise((object) tokClassDef.m_yynum);
        return (object) null;
      }
      tokClassDef.m_name = (string) s.Deserialise();
      tokClassDef.m_yynum = (int) s.Deserialise();
      return (object) tokClassDef;
    }
  }
}
