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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Logger = OpenMetaverse.Logger;

namespace LibreMetaverse
{
    public class LslSyntaxId
    {
        public enum LslCategory
        {
            Function,
            Control,
            Event,
            Datatype,
            Constant,
            Flow,
            Unknown = -1,
        }
        
        public struct LslKeyword
        {
            public LslCategory Category;
            public string Keyword;
            public string Tooltip;
            public bool Deprecated;
            public bool GodMode;
        } 
        
        private const string KEYWORDS_DEFAULT = "keywords_lsl_default.xml";
        private const string VERSION_KEY = "llsd-lsl-syntax-version";
        private Dictionary<string, LslKeyword> _keywords = new Dictionary<string, LslKeyword>();

        public FrozenDictionary<string, LslKeyword> Keywords => _keywords.ToFrozenDictionary();

        private readonly GridClient Client;

        public LslSyntaxId(GridClient client)
        {
            Client = client;
            var defaultKeywords = Path.Combine(Settings.RESOURCE_DIR, KEYWORDS_DEFAULT);
            using (FileStream fs = new FileStream(defaultKeywords, FileMode.Open, FileAccess.Read))
            {
                ParseFile(fs);
            }
        }

        private void ParseFile(Stream stream)
        {
            using (XmlTextReader reader = new XmlTextReader(stream))
            {
                var deserialized = OSDParser.DeserializeLLSDXml(reader);
                if (deserialized.Type != OSDType.Map)
                {
                    Logger.Log("Invalid format for syntax file. Root element is not a map.", Helpers.LogLevel.Warning);
                    return;
                }

                var map = (OSDMap)deserialized;
                if (!map.TryGetValue(VERSION_KEY, out var version))
                {
                    Logger.Log("Syntax file does not contain a version key. Contents may not parse correctly.", 
                        Helpers.LogLevel.Warning);
                } 
                else if (version.AsInteger() != 2)
                {
                    Logger.Log($"Syntax file version {version.AsInteger()} is incompatible. Contents may not parse correctly.",
                        Helpers.LogLevel.Warning);
                }

                int tokens = 0, added = 0;
                var keywords = new Dictionary<string, LslKeyword>();
                try
                {
                    var groupEnumerator = map.GetEnumerator();
                    while (groupEnumerator.MoveNext())
                    {
                        var group = (string)groupEnumerator.Key;
                        if (group == VERSION_KEY) { continue; }
                        
                        var items = (OSDMap)groupEnumerator.Value;

                        var enumerator = items.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            ++tokens;
                            StringBuilder tooltip = new StringBuilder();
                            LslCategory category = LslCategory.Unknown;
                            var key = (string)enumerator.Key;
                            var attr = (OSDMap)enumerator.Value;
                            
                            tooltip.AppendLine(attr["tooltip"].AsString());
                            
                            switch (group)
                            {
                                case "controls":
                                    category = LslCategory.Control;
                                    break;
                                case "types":
                                    category = LslCategory.Datatype;
                                    break;
                                case "constants":
                                    category = LslCategory.Constant;
                                    tooltip.Append($" Type: {attr["type"]}-{attr["value"]}");
                                    break;
                                case "events":
                                    category = LslCategory.Event;
                                    tooltip.Append($"{key} ({ParseArguments(attr["arguments"])})");
                                    break;
                                case "functions":
                                    category = LslCategory.Function;
                                    tooltip.AppendLine(
                                        $"{attr["return"]} {key} ({ParseArguments(attr["arguments"])})");
                                    tooltip.Append(
                                        $"Energy: {(attr.ContainsKey("energy") ? attr["energy"].AsString() : "0.0")}");
                                    if (attr.TryGetValue("sleep", out var sleep))
                                        tooltip.Append($", Sleep: {sleep}");
                                    break;
                            }
                            
                            if (category == LslCategory.Unknown) { continue; }

                            var kw = new LslKeyword
                            {
                                Category = category,
                                Keyword = key,
                                Deprecated = attr.ContainsKey("deprecated") && attr["deprecated"].AsBoolean(),
                                GodMode = attr.ContainsKey("god-mode") && attr["god-mode"].AsBoolean(),
                                Tooltip = tooltip.ToString(),
                            };
                            keywords.Add(kw.Keyword, kw);
                            ++added;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log($"Syntax parser exception: {e.Message}", Helpers.LogLevel.Warning);
                }
                Logger.Log($"Parsed Syntax file, added {added}/{tokens} tokens.", Helpers.LogLevel.Debug);
                _keywords = keywords;
            }
        }

        private string ParseArguments(OSD args)
        {
            var str = new StringBuilder();
            if (args.Type == OSDType.Array)
            {
                foreach (var osd in (OSDArray)args)
                {
                    if (osd.Type == OSDType.Map)
                    {
                        var map = (OSDMap)osd;
                        var left = map.Count;
                        var enumerable = map.GetEnumerator();
                        while (enumerable.MoveNext())
                        {
                            var value = (OSD)enumerable.Value;
                            str.Append($"{value.AsString()} {(string)enumerable.Key}");
                            if (left-- > 1)
                            {
                                str.Append(", ");
                            }
                        }
                    }
                }
            }

            return str.ToString();
        }
    }
}