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

namespace LibreMetaverse.LslTools
{
  public class ParsingInfo
  {
    public Hashtable m_parsetable = new Hashtable();
    public string m_name;
    public int m_yynum;

    public ParsingInfo(string name, int num)
    {
      this.m_name = name;
      this.m_yynum = num;
    }

    private ParsingInfo()
    {
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new ParsingInfo();
      ParsingInfo parsingInfo = (ParsingInfo) o;
      if (s.Encode)
      {
        s.Serialise((object) parsingInfo.m_name);
        s.Serialise((object) parsingInfo.m_yynum);
        s.Serialise((object) parsingInfo.m_parsetable);
        return (object) null;
      }
      parsingInfo.m_name = (string) s.Deserialise();
      parsingInfo.m_yynum = (int) s.Deserialise();
      parsingInfo.m_parsetable = (Hashtable) s.Deserialise();
      return (object) parsingInfo;
    }
  }
}
