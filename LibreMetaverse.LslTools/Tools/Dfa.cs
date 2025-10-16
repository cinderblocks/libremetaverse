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

namespace LibreMetaverse.LslTools
{
  public class Dfa : LNode
  {
    public Hashtable m_map = new Hashtable();
    public string m_tokClass = "";
    public int m_reswds = -1;
    internal NList m_nfa = new NList();
    private YyLexer m_tokens;
    public Action m_actions;

    private Dfa()
    {
    }

    public Dfa(TokensGen tks)
      : base(tks)
    {
      m_tokens = tks.m_tokens;
    }

    public Dfa(Nfa nfa)
      : base(nfa.m_tks)
    {
      m_tokens = m_tks.m_tokens;
      AddNfaNode(nfa);
      Closure();
      AddActions();
    }

    public static void SetTokens(YyLexer tks, Hashtable h)
    {
      foreach (Dfa dfa in h.Values)
      {
        if (dfa.m_tokens == null)
        {
          dfa.m_tokens = tks;
          SetTokens(tks, dfa.m_map);
        }
      }
    }

    private void AddAction(int act)
    {
      m_actions = new Action(act, m_actions);
    }

    private void MakeLastAction(int act)
    {
      while (m_actions != null && m_actions.a_act >= act)
        m_actions = m_actions.a_next;
      AddAction(act);
    }

    internal bool AddNfaNode(NfaNode nfa)
    {
      if (!m_nfa.Add(nfa))
        return false;
      if (nfa.m_sTerminal != "")
      {
        int length = 0;
        string str1 = "";
        string sTerminal = nfa.m_sTerminal;
        if (sTerminal[0] == '%')
        {
          length = 0;
          int index = 1;
          while (index < sTerminal.Length && (sTerminal[index] != ' ' && sTerminal[index] != '\t') && (sTerminal[index] != '\n' && sTerminal[index] != '{' && sTerminal[index] != ':'))
          {
            ++index;
            ++length;
          }
          str1 = nfa.m_sTerminal.Substring(1, length);
        }
        if (length > 0 && length + 1 < sTerminal.Length)
        {
          string str2 = nfa.m_sTerminal.Substring(length + 1).Trim();
          if (str2.Length > 0 && str2.StartsWith("%except"))
          {
            m_reswds = nfa.m_state;
            m_tks.m_tokens.reswds[nfa.m_state] = ResWds.New(m_tks, str2.Substring(7));
          }
        }
        if (str1 == "")
        {
          if (m_tokClass == "" || m_actions.a_act > nfa.m_state)
            AddAction(nfa.m_state);
        }
        else if (m_actions == null || m_actions.a_act > nfa.m_state)
        {
          MakeLastAction(nfa.m_state);
          m_tokClass = str1;
        }
      }
      return true;
    }

    internal void AddActions()
    {
      m_tks.states.Add(this);
      foreach (Charset charset in m_tks.m_tokens.cats.Values)
      {
        foreach (char key in charset.m_chars.Keys)
        {
          Dfa dfa = Target(key);
          if (dfa != null)
            m_map[key] = dfa;
        }
      }
    }

    internal Dfa Target(char ch)
    {
      Dfa dfa = new Dfa(m_tks);
      for (NList nlist = m_nfa; !nlist.AtEnd; nlist = nlist.m_next)
        nlist.m_node.AddTarget(ch, dfa);
      if (dfa.m_nfa.AtEnd)
        return null;
      dfa.Closure();
      foreach (var t in m_tks.states)
      {
          if (((Dfa) t).SameAs(dfa))
              return (Dfa) t;
      }
      dfa.AddActions();
      return dfa;
    }

    private void Closure()
    {
      for (NList nlist = m_nfa; !nlist.AtEnd; nlist = nlist.m_next)
        ClosureAdd(nlist.m_node);
    }

    private void ClosureAdd(NfaNode nfa)
    {
        foreach (var t in nfa.m_eps)
        {
            NfaNode ep = (NfaNode) t;
            if (AddNfaNode(ep))
                ClosureAdd(ep);
        }
    }

    internal bool SameAs(Dfa dfa)
    {
      NList nlist1 = m_nfa;
      NList nlist2;
      for (nlist2 = dfa.m_nfa; nlist1.m_node == nlist2.m_node && !nlist1.AtEnd; nlist2 = nlist2.m_next)
        nlist1 = nlist1.m_next;
      return nlist1.m_node == nlist2.m_node;
    }

    public int Match(string str, int ix, ref int action)
    {
      Dfa dfa;
      int num;
      if (ix < str.Length && (dfa = (Dfa) m_map[m_tokens.Filter(str[ix])]) != null && (num = dfa.Match(str, ix + 1, ref action)) >= 0)
        return num + 1;
      if (m_actions == null)
        return -1;
      action = m_actions.a_act;
      return 0;
    }

    public void Print()
    {
      Console.Write("{0}:", m_state);
      if (m_actions != null)
      {
        Console.Write(" (");
        for (Action action = m_actions; action != null; action = action.a_next)
          Console.Write("{0} <", action.a_act);
        if (m_tokClass != "")
          Console.Write(m_tokClass);
        Console.Write(">)");
      }
      Console.WriteLine();
      Hashtable hashtable = new Hashtable();
      IDictionaryEnumerator enumerator1 = m_map.GetEnumerator();
      int count = m_map.Count;
      while (count-- > 0)
      {
        enumerator1.MoveNext();
        char key1 = (char) enumerator1.Key;
        Dfa dfa1 = (Dfa) enumerator1.Value;
        if (!hashtable.Contains(key1))
        {
          hashtable[key1] = true;
          Console.Write("  {0}  ", dfa1.m_state);
          int num1 = key1;
          if (num1 >= 32 && num1 < 128)
            Console.Write(key1);
          else
            Console.Write(" #{0} ", num1);
          IDictionaryEnumerator enumerator2 = m_map.GetEnumerator();
          do
          {
            enumerator2.MoveNext();
          }
          while ((Dfa) enumerator2.Value != dfa1);
          for (int index = count; index > 0; --index)
          {
            enumerator2.MoveNext();
            char key2 = (char) enumerator2.Key;
            Dfa dfa2 = (Dfa) enumerator2.Value;
            if (dfa1 == dfa2)
            {
              hashtable[key2] = true;
              int num2 = key2;
              if (num2 >= 32 && num2 < 128)
                Console.Write(key2);
              else
                Console.Write(" #{0} ", num2);
            }
          }
          Console.WriteLine();
        }
      }
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return new Dfa();
      Dfa dfa = (Dfa) o;
      if (s.Encode)
      {
        s.Serialise(dfa.m_state);
        s.Serialise(dfa.m_map);
        s.Serialise(dfa.m_actions);
        s.Serialise(dfa.m_tokClass);
        s.Serialise(dfa.m_reswds);
        return null;
      }
      dfa.m_state = (int) s.Deserialise();
      dfa.m_map = (Hashtable) s.Deserialise();
      dfa.m_actions = (Action) s.Deserialise();
      dfa.m_tokClass = (string) s.Deserialise();
      dfa.m_reswds = (int) s.Deserialise();
      return dfa;
    }

    public class Action
    {
      public int a_act;
      public Action a_next;

      public Action(int act, Action next)
      {
        a_act = act;
        a_next = next;
      }

      private Action()
      {
      }

      public static object Serialise(object o, Serialiser s)
      {
        if (s == null)
          return new Action();
        Action action = (Action) o;
        if (s.Encode)
        {
          s.Serialise(action.a_act);
          s.Serialise(action.a_next);
          return null;
        }
        action.a_act = (int) s.Deserialise();
        action.a_next = (Action) s.Deserialise();
        return action;
      }
    }
  }
}
