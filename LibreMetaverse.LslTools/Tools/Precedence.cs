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

namespace LibreMetaverse.LSLTools.Tools
{
  public class Precedence
  {
    public PrecType m_type;
    public int m_prec;
    public Precedence m_next;

    public Precedence(PrecType t, int p, Precedence next)
    {
      if (CheckType(next, t, 0) != 0)
        Console.WriteLine("redeclaration of precedence");
      m_next = next;
      m_type = t;
      m_prec = p;
    }

    private static int CheckType(Precedence p, PrecType t, int d)
    {
      if (p == null)
        return 0;
      if (p.m_type == t || p.m_type <= PrecType.nonassoc && t <= PrecType.nonassoc)
        return p.m_prec;
      return Check(p.m_next, t, d + 1);
    }

    public static int Check(Precedence p, PrecType t, int d)
    {
      if (p == null)
        return 0;
      if (p.m_type == t)
        return p.m_prec;
      return Check(p.m_next, t, d + 1);
    }

    public static int Check(CSymbol s, Production p, int d)
    {
      if (s.m_prec == null)
        return 0;
      int num1 = CheckType(s.m_prec, PrecType.after, d + 1);
      int num2 = CheckType(s.m_prec, PrecType.left, d + 1);
      if (num1 > num2)
        return num1 - p.m_prec;
      return num2 - p.m_prec;
    }

    public static void Check(Production p)
    {
      int count = p.m_rhs.Count;
      while (count > 1 && ((SYMBOL) p.m_rhs[count - 1]).IsAction())
        --count;
      switch (count)
      {
        case 2:
          if ((CSymbol) p.m_rhs[0] == p.m_lhs)
          {
            int num = Check(((CSymbol) p.m_rhs[1]).m_prec, PrecType.after, 0);
            if (num == 0)
              break;
            p.m_prec = num;
            break;
          }
          if ((CSymbol) p.m_rhs[1] != p.m_lhs)
            break;
          int num1 = Check(((CSymbol) p.m_rhs[0]).m_prec, PrecType.before, 0);
          if (num1 == 0)
            break;
          p.m_prec = num1;
          break;
        case 3:
          int num2 = CheckType(((CSymbol) p.m_rhs[1]).m_prec, PrecType.left, 0);
          if (num2 == 0 || (CSymbol) p.m_rhs[2] != p.m_lhs)
            break;
          p.m_prec = num2;
          break;
      }
    }

    public enum PrecType
    {
      left,
      right,
      nonassoc,
      before,
      after,
    }
  }
}
