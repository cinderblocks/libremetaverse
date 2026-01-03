using System;
using System.Collections.Generic;
using System.Text;

namespace TestClient
{
    internal static class CommandLineParser
    {
        /// <summary>
        /// Tokenize a command line string supporting quoted tokens (single or double quotes)
        /// and simple backslash escapes. Returns an array of tokens.
        /// </summary>
        public static string[] Tokenize(string input)
        {
            if (string.IsNullOrEmpty(input)) return Array.Empty<string>();

            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            char quoteChar = '\0';

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (inQuotes)
                {
                    if (c == quoteChar)
                    {
                        // End quoted token
                        inQuotes = false;
                        quoteChar = '\0';
                        // Do not automatically flush token here; allow adjacent text
                    }
                    else if (c == '\\' && i + 1 < input.Length)
                    {
                        // Simple escape handling: allow escaping of quote and backslash
                        i++;
                        sb.Append(input[i]);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (char.IsWhiteSpace(c))
                    {
                        if (sb.Length > 0)
                        {
                            tokens.Add(sb.ToString());
                            sb.Clear();
                        }
                    }
                    else if (c == '"' || c == '\'')
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens.ToArray();
        }
    }
}
