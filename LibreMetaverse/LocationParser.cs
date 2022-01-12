/*
 * Copyright (c) 2022, Sjofn, LLC
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

namespace OpenMetaverse
{
    public class LocationParser
    {
        public string Sim { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public LocationParser(string location)
        {
            if (location == null ) { throw new ArgumentNullException("Location cannot be null."); }
            if (location.Length == 0) { throw new ArgumentException("Location cannot be empty."); }

            string toParse;
            if (location.StartsWith("secondlife://")) { toParse = location.Substring(13); }
            else if (location.StartsWith("uri:")) { toParse = location.Substring(4); }
            else { toParse = location; }

            string[] elements = toParse.Split('/');
            Sim = elements[0];
            int parsed;
            X = (elements.Length > 1 && int.TryParse(elements[1], out parsed)) ? parsed : 128;
            Y = (elements.Length > 2 && int.TryParse(elements[2], out parsed)) ? parsed : 128;
            Z = (elements.Length > 3 && int.TryParse(elements[3], out parsed)) ? parsed : 0;
        }

        public string GetRawLocation()
        {
            return $"{Sim}/{X}/{Y}/{Z}";
        }

        public string GetSlurl()
        {
            return $"secondlife://{Sim}/{X}/{Y}/{Z}/";
        }

        public string GetStartLocationUri()
        {
            return $"uri:{Sim}&{X}&{Y}&{Z}";
        }
    }
}
