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

namespace LibreMetaverse.LslTools
{
  public class Sfactory
  {
    public Sfactory(YyParser syms, string cls_name, SCreator cr)
    {
      syms.types[cls_name] = cr;
    }

    public static object create(string cls_name, Parser yyp)
    {
      SCreator type1 = (SCreator) yyp.m_symbols.types[cls_name];
      if (type1 == null)
        yyp.m_symbols.erh.Error(new CSToolsException(16, yyp.m_lexer, "no factory for {" + cls_name + ")"));
      try
      {
        return type1(yyp);
      }
      catch (CSToolsException ex)
      {
        yyp.m_symbols.erh.Error(ex);
      }
      catch (Exception ex)
      {
        yyp.m_symbols.erh.Error(new CSToolsException(17, yyp.m_lexer,
          $"Create of {(object)cls_name} failed ({(object)ex.Message})"));
      }
      int length = cls_name.LastIndexOf('_');
      if (length > 0)
      {
        SCreator type2 = (SCreator) yyp.m_symbols.types[cls_name.Substring(0, length)];
        if (type2 != null)
        {
          SYMBOL symbol = (SYMBOL) type2(yyp);
          symbol.m_dollar = 0;
          return symbol;
        }
      }
      return null;
    }
  }
}
