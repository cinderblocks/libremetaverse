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
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    public class LslSyntax
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
        private const string KEYWORDS_LUA_DEFAULT = "keywords_lua_default.xml";
        private const string VERSION_KEY = "llsd-lsl-syntax-version";
        private const string SYNTAX_FEATURE_IDENTIFIER = "LSLSyntaxId";
        private const string SYNTAX_CAPABILITY_IDENTIFIER = "LSLSyntax";
        private const string DEFINITIONS_CAPABILITY_IDENTIFIER = "ScriptDefinitions";
        private const string DEFINITIONS_FILE_LSL = "lsl_keywords.xml";
        private const string DEFINITIONS_FILE_LUA = "lua_keywords.xml";
        
        private static volatile FrozenDictionary<string, LslKeyword>? _frozenKeywords;
        private static volatile FrozenDictionary<string, LslKeyword>? _frozenLuaKeywords;

        /// <summary>The keywords of LSL</summary>
        public static FrozenDictionary<string, LslKeyword> Keywords => _frozenKeywords ?? FrozenDictionary<string, LslKeyword>.Empty;

        /// <summary>The keywords of Lua (SLua), such as <c>ll.Say</c> or <c>integer.maxsigned</c>. A name that is both a type and
        /// a function, such as <c>vector</c>, is the type here.</summary>
        public static FrozenDictionary<string, LslKeyword> LuaKeywords => _frozenLuaKeywords ?? FrozenDictionary<string, LslKeyword>.Empty;

        /// <summary>The keywords of a language</summary>
        /// <param name="lua">True for Lua, false for LSL</param>
        public static FrozenDictionary<string, LslKeyword> GetKeywords(bool lua) => lua ? LuaKeywords : Keywords;

        private GridClient? _client;
        private UUID _syntaxId;

        #region EVENTS
        
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler? _syntaxChanged;

        ///<summary>Raises the SyntaxChanged Event</summary>
        protected void OnSyntaxChanged()
        {
            EventHandler? handler = _syntaxChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object _syntaxChangedLock = new object();

        /// <summary>Raised when the syntax tokens are updated, whether those of LSL or of Lua</summary>
        public event EventHandler SyntaxChanged
        {
            add { lock (_syntaxChangedLock) { _syntaxChanged += value; } }
            remove { lock (_syntaxChangedLock) { _syntaxChanged -= value; } }
        }
        
        #endregion EVENTS

        public LslSyntax()
        {
            LoadDefaults();
        }

        public LslSyntax(GridClient client)
        {
            Register(client);
        }

        private void LoadDefaults()
        {
            LoadDefaultFile(KEYWORDS_DEFAULT, false);
            LoadDefaultFile(KEYWORDS_LUA_DEFAULT, true);
        }

        private void LoadDefaultFile(string name, bool lua)
        {
            var keywordFile = Path.Combine(Settings.ResourceDir, name);
            try
            {
                using (FileStream fs = new FileStream(keywordFile, FileMode.Open, FileAccess.Read))
                {
                    ParseFile(fs, lua);
                }
            }
            catch (FileNotFoundException)
            {
                Logger.Warn($"Failed to find {keywordFile}.");
            }
            catch (IOException e)
            {
                Logger.Warn($"Failed to read {keywordFile}: {e.Message}");
            }
        }

        public void Register(GridClient client)
        {
            _client = client;

            LoadDefaults();

            var net = client.Network;
            var sim = net.CurrentSim;
            if (net.Connected && sim?.Features != null)
            {
                var syntaxId = sim.Features.Get(SYNTAX_FEATURE_IDENTIFIER);
                if (syntaxId != null && syntaxId.Type == OSDType.UUID)
                {
                    var id = syntaxId.AsUUID();
                    // a cache from LSLSyntax alone has no Lua, which is not complete if the simulator can give it
                    var wantsLua = sim.Caps?.CapabilityURI(DEFINITIONS_CAPABILITY_IDENTIFIER) != null;
                    if (LoadCache(client, id, wantsLua))
                    {
                        _syntaxId = id;
                    }
                    else if (sim.Caps != null)
                    {
                        Task fetch = Task.Run(() => FetchDefinitionsAsync(sim, id));
                        fetch.Wait(TimeSpan.FromSeconds(20));
                    }
                }
            }

            client.Network.SimChanged -= Network_OnSimChanged;
            client.Network.SimChanged += Network_OnSimChanged;
        }

        private static string CacheFile(GridClient client, UUID syntaxId, bool lua) =>
            Path.Combine(client.Settings.AssetCache.Dir, $"keywords_{(lua ? "lua" : "lsl")}_{syntaxId}.xml");

        /// <summary>
        /// Use the definitions cached for a syntax ID
        /// </summary>
        /// <param name="needLua">True if the cache is not complete without Lua</param>
        /// <returns>True if the cache was there, and complete</returns>
        private bool LoadCache(GridClient client, UUID syntaxId, bool needLua)
        {
            var lslFile = CacheFile(client, syntaxId, false);
            var luaFile = CacheFile(client, syntaxId, true);
            var haveLua = File.Exists(luaFile);
            if (!File.Exists(lslFile) || (needLua && !haveLua)) { return false; }

            try
            {
                using (FileStream fs = new FileStream(lslFile, FileMode.Open, FileAccess.Read))
                {
                    ParseFile(fs, false);
                }
                if (haveLua)
                {
                    using (FileStream fs = new FileStream(luaFile, FileMode.Open, FileAccess.Read))
                    {
                        ParseFile(fs, true);
                    }
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is XmlException)
            {
                Logger.Warn($"Failed to read the cached syntax files for {syntaxId}: {e.Message}");
                return false;
            }
        }

        private void Network_OnSimChanged(object? sender, SimChangedEventArgs e)
        {
            var sim = _client?.Network?.CurrentSim;
            if (sim?.Caps != null)
            {
                sim.Caps.CapabilitiesReceived += Simulator_OnCapabilitiesReceived;
            }
        }

        private void Simulator_OnCapabilitiesReceived(object? sender, CapabilitiesReceivedEventArgs e)
        {
            e.Simulator.Caps?.CapabilitiesReceived -= Simulator_OnCapabilitiesReceived;

            var syntaxId = e.Simulator.Features.Get(SYNTAX_FEATURE_IDENTIFIER);
            if (syntaxId == null || syntaxId.Type != OSDType.UUID) { return; }
            if (syntaxId.AsUUID() == _syntaxId) { return; }
            
            _ = FetchDefinitionsAsync(e.Simulator, syntaxId.AsUUID());
        }

        /// <summary>
        /// Get the definitions of the simulator: from the ScriptDefinitions capability, which gives both those of LSL
        /// and those of Lua, otherwise from LSLSyntax, which gives those of LSL.
        /// </summary>
        private async Task FetchDefinitionsAsync(Simulator sim, UUID syntaxId)
        {
            var client = _client;
            var caps = sim.Caps;
            if (client == null || caps == null) { return; }

            try
            {
                var definitionsUri = caps.CapabilityURI(DEFINITIONS_CAPABILITY_IDENTIFIER);
                var uri = definitionsUri ?? caps.CapabilityURI(SYNTAX_CAPABILITY_IDENTIFIER);
                if (uri == null) { return; }

                var (response, data) = await client.HttpCapsClient.GetAsync(uri, CancellationToken.None).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var status = $"{response.StatusCode} {response.ReasonPhrase}";
                    Logger.Warn($"Failed to retrieve syntax file. Status: {status}", client);
                    return;
                }
                if (data == null) { return; }
                OSD features = OSDParser.Deserialize(data);
                if (features.Type != OSDType.Map)
                {
                    Logger.Warn("Invalid format for syntax file. Root element is not a map.");
                    return;
                }

                if (definitionsUri != null)
                {
                    if (!ApplyDefinitionFiles((OSDMap)features, client, syntaxId)) { return; }
                }
                else
                {
                    Parse((OSDMap)features, false);
                    WriteCache(CacheFile(client, syntaxId, false), (OSDMap)features);
                }
                _syntaxId = syntaxId;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to retrieve syntax file. Error: {ex.Message}", client);
            }
        }

        /// <summary>
        /// Use the response of the ScriptDefinitions capability: a map <c>files</c> of the keyword files by name,
        /// <c>lsl_keywords.xml</c> and <c>lua_keywords.xml</c>. A language the simulator leaves out keeps the
        /// definitions it has.
        /// </summary>
        /// <param name="client">Where to cache the files, none if null</param>
        /// <returns>True if the response had definitions</returns>
        internal bool ApplyDefinitionFiles(OSDMap response, GridClient? client, UUID syntaxId)
        {
            if (!response.TryGetValue("files", out var filesOsd) || !(filesOsd is OSDMap files))
            {
                Logger.Warn("Malformed syntax definitions response, missing 'files' map.");
                return false;
            }

            var applied = false;
            foreach (var name in files.Keys)
            {
                bool lua;
                if (name == DEFINITIONS_FILE_LSL) { lua = false; }
                else if (name == DEFINITIONS_FILE_LUA) { lua = true; }
                else { continue; }

                if (!TryReadDefinitions(files[name], out var map))
                {
                    Logger.Warn($"Syntax definitions file {name} is not in a format that can be read.");
                    continue;
                }
                Parse(map, lua);
                applied = true;
                if (client != null) { WriteCache(CacheFile(client, syntaxId, lua), map); }
            }
            return applied;
        }

        /// <summary>
        /// A file of definitions as the ScriptDefinitions capability has it: either the map itself, or the bytes of
        /// the file
        /// </summary>
        internal static bool TryReadDefinitions(OSD contents, out OSDMap map)
        {
            var osd = contents.Type == OSDType.Binary ? OSDParser.Deserialize(contents.AsBinary()) : contents;
            if (osd is OSDMap result)
            {
                map = result;
                return true;
            }
            map = new OSDMap();
            return false;
        }

        private static void WriteCache(string file, OSDMap map)
        {
            try
            {
                File.WriteAllBytes(file, OSDParser.SerializeLLSDXmlBytes(map));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Logger.Warn($"Failed to cache the syntax file {file}: {e.Message}");
            }
        }

        private void ParseFile(Stream stream, bool lua)
        {
            using (XmlTextReader reader = new XmlTextReader(stream))
            {
                var deserialized = OSDParser.DeserializeLLSDXml(reader);
                if (deserialized.Type != OSDType.Map)
                {
                    Logger.Warn("Invalid format for syntax file. Root element is not a map.");
                    return;
                }
                Parse((OSDMap)deserialized, lua);
            }
        }

        private void Parse(OSDMap map, bool lua)
        {
            var keywords = ParseKeywords(map);
            if (lua) { _frozenLuaKeywords = keywords.ToFrozenDictionary(); }
            else { _frozenKeywords = keywords.ToFrozenDictionary(); }
            OnSyntaxChanged();
        }

        internal static Dictionary<string, LslKeyword> ParseKeywords(OSDMap map)
        {
            if (!map.TryGetValue(VERSION_KEY, out var version))
            {
                Logger.Warn("Syntax file does not contain a version key. Contents may not parse correctly.");
            } 
            else if (version.AsInteger() != 2)
            {
                Logger.Warn($"Syntax file version {version.AsInteger()} is incompatible. Contents may not parse correctly.");
            }

            int tokens = 0, added = 0;
            var keywords = new Dictionary<string, LslKeyword>();
            try
            {
                var groupEnumerator = map.GetEnumerator();
                while (groupEnumerator.MoveNext())
                {
                    if (!(groupEnumerator.Key is string group)) { continue; }
                    if (group == VERSION_KEY) { continue; }

                    if (!(groupEnumerator.Value is OSDMap items)) { continue; }

                    var enumerator = items.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        ++tokens;
                        StringBuilder tooltip = new StringBuilder();
                        LslCategory category = LslCategory.Unknown;
                        if (!(enumerator.Key is string key)) { continue; }
                        if (!(enumerator.Value is OSDMap attr)) { continue; }

                        if (attr.TryGetValue("tooltip", out var tip))
                            tooltip.AppendLine(tip?.AsString() ?? string.Empty);
                        
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
                                var typestr = string.Empty;
                                var valstr = string.Empty;
                                if (attr.TryGetValue("type", out var ttype)) typestr = ttype?.AsString() ?? string.Empty;
                                if (attr.TryGetValue("value", out var tvalue)) valstr = tvalue?.AsString() ?? string.Empty;
                                tooltip.Append($" Type: {typestr}-{valstr}");
                                break;
                            case "events":
                                category = LslCategory.Event;
                                tooltip.Append($"{key} ({ParseArguments(attr.TryGetValue("arguments", out var a) ? a : new OSDArray())})");
                                break;
                            case "functions":
                                category = LslCategory.Function;
                                var returnStr = attr.TryGetValue("return", out var r) ? r?.AsString() ?? string.Empty : string.Empty;
                                tooltip.AppendLine($"{returnStr} {key} ({ParseArguments(attr.TryGetValue("arguments", out var aa) ? aa : new OSDArray())})");
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
                        // Lua has names in more than one group, such as vector, a type and a function; the first one keeps it
                        if (!keywords.ContainsKey(kw.Keyword))
                        {
                            keywords.Add(kw.Keyword, kw);
                            ++added;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Syntax parser exception: {e.Message}");
            }
            Logger.Debug($"Parsed Syntax file, added {added}/{tokens} tokens.");
            return keywords;
        }
        
        /// <summary>
        /// The arguments of a function or event as <c>type name, type name</c>, from an array with a map for each
        /// argument, <c>{name: {type, tooltip}}</c>
        /// </summary>
        private static string ParseArguments(OSD args)
        {
            var arguments = new List<string>();
            if (args is OSDArray array)
            {
                foreach (var osd in array)
                {
                    if (!(osd is OSDMap map)) { continue; }
                    foreach (var name in map.Keys)
                    {
                        var type = map[name] is OSDMap attr && attr.TryGetValue("type", out var t) ? t.AsString() : string.Empty;
                        arguments.Add(type.Length > 0 ? $"{type} {name}" : name);
                    }
                }
            }

            return string.Join(", ", arguments);
        }
    }
}
