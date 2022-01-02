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

using System;

namespace LibreMetaverse.LslTools
{
  public class Tfactory
  {
    public Tfactory(YyLexer tks, string cls_name, TCreator cr)
    {
      tks.types[(object) cls_name] = (object) cr;
    }

    public static object create(string cls_name, Lexer yyl)
    {
      TCreator type1 = (TCreator) yyl.tokens.types[(object) cls_name];
      if (type1 == null)
        yyl.tokens.erh.Error(new CSToolsException(6, yyl, cls_name, string.Format("no factory for {0}", (object) cls_name)));
      try
      {
        return type1(yyl);
      }
      catch (CSToolsException ex)
      {
        yyl.tokens.erh.Error(ex);
      }
      catch (Exception ex)
      {
        yyl.tokens.erh.Error(new CSToolsException(7, yyl, cls_name, string.Format("Line {0}: Create of {1} failed ({2})", (object) yyl.Saypos(yyl.m_pch), (object) cls_name, (object) ex.Message)));
      }
      int length = cls_name.LastIndexOf('_');
      if (length > 0)
      {
        TCreator type2 = (TCreator) yyl.tokens.types[(object) cls_name.Substring(0, length)];
        if (type2 != null)
          return type2(yyl);
      }
      return (object) null;
    }
  }
}
