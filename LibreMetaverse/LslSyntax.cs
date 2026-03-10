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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Logger = OpenMetaverse.Logger;

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
        private const string VERSION_KEY = "llsd-lsl-syntax-version";
        private const string SYNTAX_FEATURE_IDENTIFIER = "LSLSyntaxId";
        private const string SYNTAX_CAPABILITY_IDENTIFIER = "LSLSyntax";
        
        private static Dictionary<string, LslKeyword> _keywords = new Dictionary<string, LslKeyword>();
        public static FrozenDictionary<string, LslKeyword> Keywords => _keywords.ToFrozenDictionary();

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

        /// <summary>Raised when the syntax tokens are updated</summary>
        public event EventHandler SyntaxChanged
        {
            add { lock (_syntaxChangedLock) { _syntaxChanged += value; } }
            remove { lock (_syntaxChangedLock) { _syntaxChanged -= value; } }
        }
        
        #endregion EVENTS

        public LslSyntax()
        {
            var keywordFile = Path.Combine(Settings.RESOURCE_DIR, KEYWORDS_DEFAULT);
            try
            {
                using (FileStream fs = new FileStream(keywordFile, FileMode.Open, FileAccess.Read))
                {
                    ParseFile(fs);
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

        public LslSyntax(GridClient client)
        {
            Register(client);
        }

        public void Register(GridClient client)
        {
            _client = client;
            // Use the non-nullable method parameter within this registration scope to help the
            // nullable analyzer — the field is set above but the analyzer may not track it.
            var clientLocal = client;

            var keywordFile = Path.Combine(Settings.RESOURCE_DIR, KEYWORDS_DEFAULT);
            var net = _client?.Network;
            if (net != null && net.Connected && net.CurrentSim?.Features != null)
            {
                var syntaxId = net.CurrentSim.Features.Get(SYNTAX_FEATURE_IDENTIFIER);
                if (syntaxId != null && syntaxId.Type == OSDType.UUID)
                {
                    var file = Path.Combine(clientLocal.Settings.ASSET_CACHE_DIR, $"keywords_lsl_{syntaxId}.xml");
                    if (File.Exists(file))
                    {
                        keywordFile = file;
                    }
                    else
                    {
                        var uri = net.CurrentSim?.Caps?.CapabilityURI(SYNTAX_CAPABILITY_IDENTIFIER);
                        if (uri != null)
                        {
                            var sim = net.CurrentSim;
                            if (sim != null)
                            {
                                Task fetch = sim.Client.HttpCapsClient.GetRequestAsync(uri,
                                    CancellationToken.None, (response, data, error) =>
                                {
                                    if (error != null)
                                    {
                                        Logger.Warn($"Failed to retrieve syntax file. Error: {error.Message}", clientLocal);
                                        return;
                                    }

                                        if (response == null || !response.IsSuccessStatusCode)
                                        {
                                            Logger.Warn($"Failed to retrieve syntax file. Status: {(response == null ? "null response" : response.StatusCode.ToString())}", _client);
                                            return;
                                        }

                                        if (data == null) return;

                                        var des = OSDParser.Deserialize(data ?? Utils.EmptyBytes);
                                        if (des.Type != OSDType.Map)
                                        {
                                            Logger.Warn("Invalid format for syntax file. Root element is not a map.");
                                            return;
                                        }

                                        Parse((OSDMap)des);
                                        _syntaxId = syntaxId.AsUUID();
                                        using (FileStream writer =
                                               new FileStream(
                                                   Path.Combine(clientLocal.Settings.ASSET_CACHE_DIR,
                                                       $"keywords_lsl_{syntaxId}.xml"),
                                                   FileMode.Create, FileAccess.Write))
                                        {
                                            var bytes = OSDParser.SerializeLLSDXmlBytes(des);
                                            writer.Write(bytes, 0, bytes.Length);
                                        }
                                    });
                                fetch.Wait(TimeSpan.FromSeconds(20));
                                return;
                            }
                        }
                    }
                }
            }

            try
            {
                using (FileStream fs = new FileStream(keywordFile, FileMode.Open, FileAccess.Read))
                {
                    ParseFile(fs);
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

            clientLocal.Network.SimChanged += Network_OnSimChanged;
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
            var caps = e.Simulator.Caps;
            var uri = caps?.CapabilityURI(SYNTAX_CAPABILITY_IDENTIFIER);
            if (uri == null || syntaxId == null || syntaxId.Type != OSDType.UUID) { return; }
            if (syntaxId.AsUUID() == _syntaxId) { return; }
            
            _ = e.Simulator.Client.HttpCapsClient.GetRequestAsync(uri, CancellationToken.None, (response, data, error) =>
            {
                if (error != null)
                {
                    Logger.Warn($"Failed to retrieve syntax file. Error: {error.Message}", _client!);
                    return;
                }
                if (response == null || !response.IsSuccessStatusCode)
                {
                    var status = response == null ? "null response" : $"{response.StatusCode} {response.ReasonPhrase}";
                    Logger.Warn($"Failed to retrieve syntax file. Status: {status}", _client!);
                    return;
                }
                if (data == null) return;
                OSD features = OSDParser.Deserialize(data ?? Utils.EmptyBytes);
                if (features.Type != OSDType.Map)
                {
                    Logger.Warn("Invalid format for syntax file. Root element is not a map.");
                    return;
                }
                Parse((OSDMap)features);
                _syntaxId = syntaxId.AsUUID();
                using (FileStream writer =
                       new FileStream(Path.Combine(_client!.Settings.ASSET_CACHE_DIR, $"keywords_lsl_{syntaxId}.xml"), 
                           FileMode.Create, FileAccess.Write))
                {
                    var bytes = OSDParser.SerializeLLSDXmlBytes(features);
                    writer.Write(bytes, 0, bytes.Length);
                }
            });
        }
        
        private void ParseFile(Stream stream)
        {
            using (XmlTextReader reader = new XmlTextReader(stream))
            {
                var deserialized = OSDParser.DeserializeLLSDXml(reader);
                if (deserialized.Type != OSDType.Map)
                {
                    Logger.Warn("Invalid format for syntax file. Root element is not a map.");
                    return;
                }
                Parse((OSDMap)deserialized);
            }
        }

        private void Parse(OSDMap map)
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
                        keywords.Add(kw.Keyword, kw);
                        ++added;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Syntax parser exception: {e.Message}");
            }
            Logger.Debug($"Parsed Syntax file, added {added}/{tokens} tokens.");
            lock(_keywords) { _keywords = keywords; }
            OnSyntaxChanged();
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
                            if (enumerable.Value is OSD value)
                            {
                                str.Append($"{value.AsString()} {(string)enumerable.Key}");
                                if (left-- > 1)
                                {
                                    str.Append(", ");
                                }
                            }
                        }
                    }
                }
            }

            return str.ToString();
        }
    }
}
