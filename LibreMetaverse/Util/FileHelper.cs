/*
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Linq;

namespace LibreMetaverse
{
    public static class FileHelper
    {
        private static readonly char[] InvalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
        private static readonly char[] InvalidPathChars = System.IO.Path.GetInvalidPathChars();

        public static string SafeFileName(string original)
        {
            if (string.IsNullOrEmpty(original)) return string.Empty;

            return string.Concat(original.Select(c =>
                InvalidFileNameChars.Contains(c) ? '_' : c));
        }

        public static string SafeDirName(string original)
        {
            if (string.IsNullOrEmpty(original)) return string.Empty;

            return string.Concat(original.Select(c =>
                InvalidPathChars.Contains(c) ? '_' : c));
        }

        /// <summary>
        /// Sanitize a string by replacing invalid characters with a replacement character
        /// </summary>
        public static string Sanitize(string input, char[] invalidChars, char replacement = '_')
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return string.Concat(input.Select(c =>
                Array.IndexOf(invalidChars, c) >= 0 ? replacement : c));
        }

        /// <summary>
        /// Try parse two names from a string (like "First Last")
        /// </summary>
        public static bool TryParseTwoNames(string input, out string first, out string last)
        {
            first = null;
            last = null;
            if (string.IsNullOrEmpty(input)) return false;

            int len = input.Length;
            int i = 0;

            // skip leading whitespace
            while (i < len && char.IsWhiteSpace(input[i])) i++;
            if (i >= len) return false;

            int j = i;
            // find end of first token
            while (j < len && !char.IsWhiteSpace(input[j])) j++;
            if (j == i) return false;

            // skip spaces between first and second
            int k = j;
            while (k < len && char.IsWhiteSpace(input[k])) k++;
            if (k >= len) return false;

            int l = k;
            // find end of second token
            while (l < len && !char.IsWhiteSpace(input[l])) l++;
            if (l == k) return false;

            // ensure no non-space content after second token
            int m = l;
            while (m < len && char.IsWhiteSpace(input[m])) m++;
            if (m != len) return false;

            first = input.Substring(i, j - i);
            last = input.Substring(k, l - k);
            return true;
        }
    }
}
