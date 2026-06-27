/*
 * Copyright (c) 2025, Sjofn LLC
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
 * CONSEQUENTIAL DAMAGES ( INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.Voice.WebRTC
{
    public class PeerManager
    {
        private readonly ConcurrentDictionary<UUID, OSDMap> Peers = new ConcurrentDictionary<UUID, OSDMap>();
        private readonly ConcurrentDictionary<uint, UUID> SsrcToPeer = new ConcurrentDictionary<uint, UUID>();
        private readonly ConcurrentDictionary<UUID, uint> PeerToSsrc = new ConcurrentDictionary<UUID, uint>();
        private readonly AudioDevice _audioDevice;
        private readonly GridClient _client;
        private readonly IVoiceLogger _log;

        public event Action<UUID>? PeerJoined;
        public event Action<UUID>? PeerLeft;
        public event Action<UUID, OSDMap>? PeerPositionUpdated;
        public event Action<List<UUID>>? PeerListUpdated;
        public event Action<UUID, VoiceSession.PeerAudioState>? PeerAudioUpdated;
        public event Action<Dictionary<UUID, bool>>? MuteMapReceived;
        public event Action<Dictionary<UUID, int>>? GainMapReceived;
        public event Action? PongReceived;

        public PeerManager(AudioDevice audioDevice, GridClient client, IVoiceLogger log)
        {
            _audioDevice = audioDevice;
            _client = client;
            _log = log;
        }

        public List<UUID> GetKnownPeers()
        {
            try { return Peers.Keys.ToList(); } catch { return new List<UUID>(); }
        }

        public bool TryGetSsrc(UUID peerId, out uint ssrc) => PeerToSsrc.TryGetValue(peerId, out ssrc);

        public void ClearAllPeers()
        {
            try
            {
                var peers = Peers.Keys.ToList();
                foreach (var p in peers)
                {
                    try { RemovePeer(p); } catch { }
                }

                var ssrcs = SsrcToPeer.Keys.ToList();
                foreach (var s in ssrcs)
                {
                    try
                    {
                        SsrcToPeer.TryRemove(s, out _);
                        try { _audioDevice?.SetSsrcMute(s, true); } catch { }
                        try { _audioDevice?.ClearSsrc(s); } catch { }
                    }
                    catch { }
                }

                PeerToSsrc.Clear();
                Peers.Clear();
            }
            catch { }
        }

        public void RemovePeer(UUID peerId)
        {
            try
            {
                Peers.TryRemove(peerId, out var _);
                try
                {
                    if (PeerToSsrc.TryRemove(peerId, out var ssrc))
                    {
                        SsrcToPeer.TryRemove(ssrc, out _);
                        try { _audioDevice?.SetSsrcMute(ssrc, true); } catch { }
                        try { _audioDevice?.ClearSsrc(ssrc); } catch { }
                    }
                }
                catch { }
                try { PeerLeft?.Invoke(peerId); } catch { }
            }
            catch { }
        }

        private static int? GetInt(JsonElement el)
        {
            try
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.Number:
                        if (el.TryGetInt32(out var i)) return i;
                        if (el.TryGetInt64(out var l)) return (int)l;
                        return (int)el.GetDouble();
                    case JsonValueKind.String:
                        if (int.TryParse(el.GetString(), out var s)) return s;
                        break;
                }
            }
            catch { }
            return null;
        }

        private static bool? GetBool(JsonElement el)
        {
            try
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.True: return true;
                    case JsonValueKind.False: return false;
                    case JsonValueKind.Number:
                        if (el.TryGetInt32(out var i)) return i != 0;
                        break;
                    case JsonValueKind.String:
                        var s = el.GetString()?.Trim('"');
                        if (bool.TryParse(s, out var b)) return b;
                        if (int.TryParse(s, out var n)) return n != 0;
                        break;
                }
            }
            catch { }
            return null;
        }

        private static string? GetString(JsonElement el)
        {
            try { return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString(); }
            catch { return null; }
        }

        private static string BuildJson(Action<Utf8JsonWriter> build)
        {
            var ms = new MemoryStream();
            using var w = new Utf8JsonWriter(ms, new JsonWriterOptions { SkipValidation = true });
            build(w);
            w.Flush();
            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        // Process System.Text.Json-based data channel messages
        public void ProcessJsonElement(JsonElement root, Func<string, bool> sendString, UUID sessionId)
        {
            if (root.ValueKind != JsonValueKind.Object) return;

            // Detect per-peer map (all keys are UUIDs)
            bool allKeysAreUuid = true;
            foreach (var prop in root.EnumerateObject())
            {
                if (string.IsNullOrEmpty(prop.Name) || !UUID.TryParse(prop.Name, out _))
                {
                    allKeysAreUuid = false;
                    break;
                }
            }

            if (allKeysAreUuid)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (!UUID.TryParse(prop.Name, out var peerId)) continue;
                    var val = prop.Value;
                    if (val.ValueKind != JsonValueKind.Object)
                    {
                        RemovePeer(peerId);
                        continue;
                    }

                    var peerMap = val;
                    var state = new VoiceSession.PeerAudioState();
                    Peers.AddOrUpdate(peerId, new OSDMap(), (k, v) => v);

                    if (peerMap.TryGetProperty("p", out var pProp)) state.Power = GetInt(pProp);
                    if (peerMap.TryGetProperty("V", out var vProp)) state.VoiceActive = GetBool(vProp);
                    else if (peerMap.TryGetProperty("v", out var v2Prop)) state.VoiceActive = GetBool(v2Prop);
                    if (peerMap.TryGetProperty("m", out var mProp)) state.ModeratorMuted = GetBool(mProp);

                    try
                    {
                        int? sInt = null;
                        if (peerMap.TryGetProperty("s", out var sProp)) sInt = GetInt(sProp);
                        else if (peerMap.TryGetProperty("ssrc", out var ssrcProp)) sInt = GetInt(ssrcProp);
                        if (sInt.HasValue)
                        {
                            var ssrc = (uint)sInt.Value;
                            if (PeerToSsrc.TryGetValue(peerId, out var oldSsrc) && oldSsrc != ssrc)
                            {
                                SsrcToPeer.TryRemove(oldSsrc, out _);
                                try { _audioDevice?.SetSsrcMute(oldSsrc, true); } catch { }
                                try { _audioDevice?.ClearSsrc(oldSsrc); } catch { }
                            }

                            if (SsrcToPeer.TryGetValue(ssrc, out var mappedPeer) && mappedPeer != peerId)
                                PeerToSsrc.TryRemove(mappedPeer, out _);

                            SsrcToPeer.AddOrUpdate(ssrc, peerId, (k, v) => peerId);
                            PeerToSsrc.AddOrUpdate(peerId, ssrc, (k, v) => ssrc);
                        }
                    }
                    catch { }

                    if (peerMap.TryGetProperty("j", out var jProp) && jProp.ValueKind == JsonValueKind.Object)
                    {
                        if (jProp.TryGetProperty("p", out var jpProp)) state.JoinedPrimary = GetBool(jpProp);
                        try { PeerJoined?.Invoke(peerId); } catch { }

                        // Mirror SL's OnDataReceivedImpl: when a peer joins, send back mute/gain so server applies them.
                        var muteResponse = new Dictionary<UUID, bool>();
                        var gainResponse = new Dictionary<UUID, int>();
                        var muteKey = $"2 {peerId}";
                        if (_client?.Self?.MuteList?.ContainsKey(muteKey) == true &&
                            (_client.Self.MuteList[muteKey].Flags & MuteFlags.VoiceChat) != 0)
                        {
                            muteResponse[peerId] = true;
                        }
                        if (muteResponse.Count > 0 || gainResponse.Count > 0)
                        {
                            try
                            {
                                sendString?.Invoke(BuildJson(jw => {
                                    jw.WriteStartObject();
                                    if (muteResponse.Count > 0)
                                    {
                                        jw.WriteStartObject("m");
                                        foreach (var kv in muteResponse) jw.WriteBoolean(kv.Key.ToString(), kv.Value);
                                        jw.WriteEndObject();
                                    }
                                    if (gainResponse.Count > 0)
                                    {
                                        jw.WriteStartObject("ug");
                                        foreach (var kv in gainResponse) jw.WriteNumber(kv.Key.ToString(), kv.Value);
                                        jw.WriteEndObject();
                                    }
                                    jw.WriteEndObject();
                                }));
                            }
                            catch { }
                        }
                    }

                    if (peerMap.TryGetProperty("l", out var lProp) && GetBool(lProp) == true)
                    {
                        state.Left = true;
                        RemovePeer(peerId);
                    }

                    try { PeerAudioUpdated?.Invoke(peerId, state); } catch { }
                }

                return;
            }

            // Non per-peer messages: handle generic keys

            // Ping/pong
            try
            {
                if (root.TryGetProperty("ping", out var pingEl) && GetBool(pingEl) == true)
                    try { sendString?.Invoke("{\"pong\":true}"); } catch { }
                if (root.TryGetProperty("pong", out var pongEl) && GetBool(pongEl) == true)
                    try { PongReceived?.Invoke(); } catch { }
            }
            catch { }

            // Join
            if (root.TryGetProperty("j", out var joinEl) && joinEl.ValueKind == JsonValueKind.Object)
            {
                UUID peerId = sessionId;
                var idStr = joinEl.TryGetProperty("id", out var joinIdEl) ? GetString(joinIdEl) : null;
                if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr!, out peerId);
                Peers.TryAdd(peerId, new OSDMap());
                try { PeerJoined?.Invoke(peerId); } catch { }
            }

            // Leave
            if (root.TryGetProperty("l", out var leaveEl) && GetBool(leaveEl) == true)
            {
                UUID peerId = sessionId;
                var idStr = root.TryGetProperty("id", out var leaveIdEl) ? GetString(leaveIdEl) : null;
                if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr!, out peerId);
                RemovePeer(peerId);
            }

            // Positions
            var avatarPos = new VoiceSession.AvatarPosition { AgentId = sessionId };
            bool posChanged = false;

            if (root.TryGetProperty("sp", out var spEl) && spEl.ValueKind == JsonValueKind.Object)
            {
                var x = spEl.TryGetProperty("x", out var spx) ? GetInt(spx) : null;
                var y = spEl.TryGetProperty("y", out var spy) ? GetInt(spy) : null;
                var z = spEl.TryGetProperty("z", out var spz) ? GetInt(spz) : null;
                if (x.HasValue && y.HasValue && z.HasValue)
                {
                    avatarPos.SenderPosition = new VoiceSession.Int3 { X = x.Value, Y = y.Value, Z = z.Value };
                    posChanged = true;
                }
            }

            if (root.TryGetProperty("sh", out var shEl) && shEl.ValueKind == JsonValueKind.Object)
            {
                var x = shEl.TryGetProperty("x", out var shx) ? GetInt(shx) : null;
                var y = shEl.TryGetProperty("y", out var shy) ? GetInt(shy) : null;
                var z = shEl.TryGetProperty("z", out var shz) ? GetInt(shz) : null;
                var w = shEl.TryGetProperty("w", out var shw) ? GetInt(shw) : null;
                if (x.HasValue && y.HasValue && z.HasValue && w.HasValue)
                {
                    avatarPos.SenderHeading = new VoiceSession.Int4 { X = x.Value, Y = y.Value, Z = z.Value, W = w.Value };
                    posChanged = true;
                }
            }

            if (root.TryGetProperty("lp", out var lpEl) && lpEl.ValueKind == JsonValueKind.Object)
            {
                var x = lpEl.TryGetProperty("x", out var lpx) ? GetInt(lpx) : null;
                var y = lpEl.TryGetProperty("y", out var lpy) ? GetInt(lpy) : null;
                var z = lpEl.TryGetProperty("z", out var lpz) ? GetInt(lpz) : null;
                if (x.HasValue && y.HasValue && z.HasValue)
                {
                    avatarPos.ListenerPosition = new VoiceSession.Int3 { X = x.Value, Y = y.Value, Z = z.Value };
                    posChanged = true;
                }
            }

            if (root.TryGetProperty("lh", out var lhEl) && lhEl.ValueKind == JsonValueKind.Object)
            {
                var x = lhEl.TryGetProperty("x", out var lhx) ? GetInt(lhx) : null;
                var y = lhEl.TryGetProperty("y", out var lhy) ? GetInt(lhy) : null;
                var z = lhEl.TryGetProperty("z", out var lhz) ? GetInt(lhz) : null;
                var w = lhEl.TryGetProperty("w", out var lhw) ? GetInt(lhw) : null;
                if (x.HasValue && y.HasValue && z.HasValue && w.HasValue)
                {
                    avatarPos.ListenerHeading = new VoiceSession.Int4 { X = x.Value, Y = y.Value, Z = z.Value, W = w.Value };
                    posChanged = true;
                }
            }

            if (posChanged)
            {
                UUID peerId = sessionId;
                var idStr = root.TryGetProperty("id", out var posIdEl) ? GetString(posIdEl) : null;
                if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr!, out peerId);
                avatarPos.AgentId = peerId;

                var osdMap = new OSDMap();
                if (avatarPos.SenderPosition.HasValue)
                {
                    var spm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.SenderPosition.Value.X), ["y"] = OSD.FromInteger(avatarPos.SenderPosition.Value.Y), ["z"] = OSD.FromInteger(avatarPos.SenderPosition.Value.Z) };
                    osdMap["sp"] = spm;
                }
                if (avatarPos.SenderHeading.HasValue)
                {
                    var shm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.SenderHeading.Value.X), ["y"] = OSD.FromInteger(avatarPos.SenderHeading.Value.Y), ["z"] = OSD.FromInteger(avatarPos.SenderHeading.Value.Z), ["w"] = OSD.FromInteger(avatarPos.SenderHeading.Value.W) };
                    osdMap["sh"] = shm;
                }
                if (avatarPos.ListenerPosition.HasValue)
                {
                    var lpm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.ListenerPosition.Value.X), ["y"] = OSD.FromInteger(avatarPos.ListenerPosition.Value.Y), ["z"] = OSD.FromInteger(avatarPos.ListenerPosition.Value.Z) };
                    osdMap["lp"] = lpm;
                }
                if (avatarPos.ListenerHeading.HasValue)
                {
                    var lhm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.X), ["y"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.Y), ["z"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.Z), ["w"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.W) };
                    osdMap["lh"] = lhm;
                }

                try { PeerPositionUpdated?.Invoke(peerId, osdMap); } catch { }
                Peers.AddOrUpdate(peerId, osdMap, (k, v) => osdMap);
            }

            // Mute map
            if (root.TryGetProperty("m", out var muteMapEl) && muteMapEl.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<UUID, bool>();
                foreach (var kv in muteMapEl.EnumerateObject())
                {
                    if (UUID.TryParse(kv.Name, out var id))
                    {
                        var b = GetBool(kv.Value);
                        if (b.HasValue) dict[id] = b.Value;
                    }
                }

                try
                {
                    foreach (var kv in dict)
                    {
                        if (PeerToSsrc.TryGetValue(kv.Key, out var ssrc))
                            try { _audioDevice?.SetSsrcMute(ssrc, kv.Value); } catch { }
                    }
                }
                catch { }

                if (dict.Count > 0) try { MuteMapReceived?.Invoke(dict); } catch { }
            }

            // Gain map
            if (root.TryGetProperty("ug", out var gainMapEl) && gainMapEl.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<UUID, int>();
                foreach (var kv in gainMapEl.EnumerateObject())
                {
                    if (UUID.TryParse(kv.Name, out var id))
                    {
                        var gi = GetInt(kv.Value);
                        if (gi.HasValue) dict[id] = gi.Value;
                    }
                }

                try
                {
                    foreach (var kv in dict)
                    {
                        if (PeerToSsrc.TryGetValue(kv.Key, out var ssrc))
                            try { _audioDevice?.SetSsrcGainPercent(ssrc, kv.Value); } catch { }
                    }
                }
                catch { }

                if (dict.Count > 0) try { GainMapReceived?.Invoke(dict); } catch { }
            }

            // Avatar list (av array or a object map)
            if (root.TryGetProperty("av", out var avEl) && avEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<UUID>();
                foreach (var item in avEl.EnumerateArray())
                {
                    var s = GetString(item);
                    if (!string.IsNullOrEmpty(s) && UUID.TryParse(s!, out var id)) list.Add(id);
                }

                foreach (var id in list) Peers.AddOrUpdate(id, new OSDMap(), (k, v) => v);
                var toRemove = Peers.Keys.Except(list).ToList();
                foreach (var r in toRemove) RemovePeer(r);
                try { PeerListUpdated?.Invoke(list); } catch { }
            }
            else if (root.TryGetProperty("a", out var aEl) && aEl.ValueKind == JsonValueKind.Object)
            {
                var list = new List<UUID>();
                foreach (var kv in aEl.EnumerateObject())
                {
                    if (!UUID.TryParse(kv.Name, out var id)) continue;
                    var val = kv.Value;
                    if (val.ValueKind == JsonValueKind.Null || val.ValueKind == JsonValueKind.Undefined ||
                        (val.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(val.GetString())) ||
                        (val.ValueKind == JsonValueKind.Object && !val.EnumerateObject().GetEnumerator().MoveNext()))
                    {
                        RemovePeer(id);
                        continue;
                    }
                    list.Add(id);
                }

                foreach (var id in list) Peers.AddOrUpdate(id, new OSDMap(), (k, v) => v);
                var toRemoveA = Peers.Keys.Except(list).ToList();
                foreach (var r in toRemoveA) RemovePeer(r);
                try { PeerListUpdated?.Invoke(list); } catch { }
            }
        }

        // Fallback OSD path processing
        public void ProcessOSDMap(OSDMap map, Func<string, bool> sendString, UUID sessionId)
        {
            if (map == null) return;
            try
            {
                if (map.ContainsKey("av") && map["av"] is OSDArray avArr)
                {
                    var list = new List<UUID>();
                    foreach (var item in avArr)
                    {
                        try { var s = item.AsString(); if (UUID.TryParse(s, out var id)) list.Add(id); } catch { }
                    }
                    foreach (var id in list) Peers.AddOrUpdate(id, new OSDMap(), (k, v) => v);
                    var toRemove = Peers.Keys.Except(list).ToList();
                    foreach (var r in toRemove) RemovePeer(r);
                    try { PeerListUpdated?.Invoke(list); } catch { }
                    return;
                }
                if (map.ContainsKey("a") && map["a"] is OSDMap amap)
                {
                    var list = new List<UUID>();
                    foreach (var k in amap.Keys)
                    {
                        if (UUID.TryParse(k, out var id))
                        {
                            var val = amap[k];
                            if (val == null) { RemovePeer(id); continue; }
                            if (val is OSDMap vm && vm.Count == 0) { RemovePeer(id); continue; }
                            try { var s = val.AsString(); if (string.IsNullOrEmpty(s)) { RemovePeer(id); continue; } } catch { }
                            list.Add(id);
                        }
                    }
                    foreach (var id in list) Peers.AddOrUpdate(id, new OSDMap(), (k, v) => v);
                    var toRemove = Peers.Keys.Except(list).ToList(); foreach (var r in toRemove) RemovePeer(r);
                    try { PeerListUpdated?.Invoke(list); } catch { }
                    return;
                }
                foreach (var key in map.Keys) { if (UUID.TryParse(key, out var peerId)) Peers.AddOrUpdate(peerId, new OSDMap(), (k, v) => v); }
            }
            catch { }

            // Reply to ping / record pong
            try
            {
                if (map.ContainsKey("ping") && map["ping"] is OSD && map["ping"].AsBoolean())
                {
                    try { sendString?.Invoke("{\"pong\":true}"); } catch { }
                }
                if (map.ContainsKey("pong") && map["pong"].AsBoolean())
                {
                    try { PongReceived?.Invoke(); } catch { }
                }
            }
            catch { }
        }

        public bool TryGetPeerForSsrc(uint ssrc, out UUID peerId) => SsrcToPeer.TryGetValue(ssrc, out peerId);

    }
}
