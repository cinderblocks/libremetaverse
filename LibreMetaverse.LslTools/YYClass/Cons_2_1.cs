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

using LibreMetaverse.LslTools;

namespace YYClass
{
  public class Cons_2_1 : Cons_2
  {
    public Cons_2_1(Parser yyq)
      : base(yyq)
    {
      cs0syntax cs0syntax = (cs0syntax) yyq;
      if (((TOKEN) yyq.StackAt(4).m_value).yytext.Trim() != cs0syntax.Cls)
      {
        this.yytext = ((TOKEN) yyq.StackAt(4).m_value).yytext + "(" + ((TOKEN) yyq.StackAt(2).m_value).yytext + ")";
      }
      else
      {
        if (((TOKEN) yyq.StackAt(2).m_value).yytext.Length == 0)
        {
          this.yytext = ((TOKEN) yyq.StackAt(4).m_value).yytext + "(" + cs0syntax.Ctx + ")";
          cs0syntax.defconseen = true;
        }
        else
          this.yytext = ((TOKEN) yyq.StackAt(4).m_value).yytext + "(" + cs0syntax.Ctx + "," + ((TOKEN) yyq.StackAt(2).m_value).yytext + ")";
        if (((TOKEN) yyq.StackAt(0).m_value).yytext.Length == 0)
        {
          Cons_2_1 cons21 = this;
          cons21.yytext = cons21.yytext + ":base(" + cs0syntax.Par + ")";
        }
        else
        {
          Cons_2_1 cons21 = this;
          cons21.yytext = cons21.yytext + ":" + ((TOKEN) yyq.StackAt(0).m_value).yytext.Substring(0, 4) + "(" + cs0syntax.Par + "," + ((TOKEN) yyq.StackAt(0).m_value).yytext.Substring(4) + ")";
        }
      }
    }
  }
}
