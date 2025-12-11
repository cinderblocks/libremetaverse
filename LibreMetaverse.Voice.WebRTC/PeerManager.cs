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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LitJson;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse.Voice.WebRTC
{
    public class PeerManager
    {
        private readonly ConcurrentDictionary<UUID, OSDMap> Peers = new ConcurrentDictionary<UUID, OSDMap>();
        private readonly ConcurrentDictionary<uint, UUID> SsrcToPeer = new ConcurrentDictionary<uint, UUID>();
        private readonly ConcurrentDictionary<UUID, uint> PeerToSsrc = new ConcurrentDictionary<UUID, uint>();
        private readonly Sdl3Audio _audioDevice;
        private readonly GridClient _client;
        private readonly IVoiceLogger _log;

        public event Action<UUID> PeerJoined;
        public event Action<UUID> PeerLeft;
        public event Action<UUID, OSDMap> PeerPositionUpdated;
        public event Action<List<UUID>> PeerListUpdated;
        public event Action<UUID, VoiceSession.PeerAudioState> PeerAudioUpdated;
        public event Action<Dictionary<UUID, bool>> MuteMapReceived;
        public event Action<Dictionary<UUID, int>> GainMapReceived;

        public PeerManager(Sdl3Audio audioDevice, GridClient client, IVoiceLogger log)
        {
            _audioDevice = audioDevice;
            _client = client;
            _log = log;
        }

        public List<UUID> GetKnownPeers()
        {
            try { return Peers.Keys.ToList(); } catch { return new List<UUID>(); }
        }

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

        private int? ToInt(JsonData d)
        {
            try
            {
                if (d == null) return null;
                if (d.IsInt) return (int)d;
                if (d.IsLong) return (int)(long)d;
                if (d.IsDouble) return (int)(double)d;
                var s = d.ToString();
                if (int.TryParse(s, out var v)) return v;
            }
            catch { }
            return null;
        }
        private bool? ToBool(JsonData d)
        {
            try
            {
                if (d == null) return null;
                if (d.IsBoolean) return (bool)d;
                var s = d.ToString().Trim('"');
                if (bool.TryParse(s, out var b)) return b;
                if (int.TryParse(s, out var i)) return i != 0;
            }
            catch { }
            return null;
        }

        // Process LitJson-formatted data channel messages (preferred path)
        public void ProcessLitJson(JsonData root, Func<string, bool> sendString, UUID sessionId)
        {
            if (root == null || !root.IsObject) return;
            var jd = root;
            IDictionary jdDict = jd;

            // Detect per-peer map (keys are UUIDs)
            bool allKeysAreUuid = (from object kObj in jdDict.Keys select kObj as string)
                .All(k => !string.IsNullOrEmpty(k) && UUID.TryParse(k, out _));

            if (allKeysAreUuid)
            {
                foreach (var kObj in jdDict.Keys)
                {
                    var key = kObj as string;
                    if (!UUID.TryParse(key, out var peerId)) continue;
                    var val = jd[key];
                    if (val == null || !val.IsObject)
                    {
                        RemovePeer(peerId);
                        continue;
                    }
                    var peerMap = val;
                    IDictionary peerDict = peerMap;
                    var state = new VoiceSession.PeerAudioState();
                    Peers.AddOrUpdate(peerId, new OSDMap(), (k, v) => v);
                    if (peerDict != null && peerDict.Contains("p")) state.Power = ToInt(peerMap["p"]);
                    if (peerDict != null && peerDict.Contains("V")) state.VoiceActive = ToBool(peerMap["V"]);
                    else if (peerDict != null && peerDict.Contains("v")) state.VoiceActive = ToBool(peerMap["v"]);

                    try
                    {
                        int? sInt = null;
                        if (peerDict != null && peerDict.Contains("s")) sInt = ToInt(peerMap["s"]);
                        else if (peerDict != null && peerDict.Contains("ssrc")) sInt = ToInt(peerMap["ssrc"]);
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
                            {
                                PeerToSsrc.TryRemove(mappedPeer, out _);
                            }

                            SsrcToPeer.AddOrUpdate(ssrc, peerId, (k, v) => peerId);
                            PeerToSsrc.AddOrUpdate(peerId, ssrc, (k, v) => ssrc);
                        }
                    }
                    catch { }

                    if (peerDict != null && peerDict.Contains("j") && peerMap["j"].IsObject)
                    {
                        var jmap = peerMap["j"];
                        if (jmap is IDictionary jdict && jdict.Contains("p")) state.JoinedPrimary = ToBool(jmap["p"]);
                        try { PeerJoined?.Invoke(peerId); } catch { }
                    }

                    if (peerDict != null && peerDict.Contains("l") && ToBool(peerMap["l"]) == true)
                    {
                        state.Left = true;
                        RemovePeer(peerId);
                    }

                    try { PeerAudioUpdated?.Invoke(peerId, state); } catch { }
                }

                return;
            }

            // Non per-peer messages: handle generic keys
            int? JInt(JsonData d) => ToInt(d);
            bool? JBool(JsonData d) => ToBool(d);
            string JStr(JsonData d) { try { return d?.ToString().Trim('"'); } catch { return null; } }
            var contains = new Func<IDictionary, string, bool>((dict, key) => dict != null && dict.Contains(key));
            var jdContains = jdDict;

            // Ping reply
            try
            {
                if (jdContains != null && contains(jdContains, "ping") && JBool(jd["ping"]) == true)
                {
                    try { sendString?.Invoke("{\"pong\":true}"); } catch { }
                }
            }
            catch { }

            // Join
            if (contains(jdContains, "j") && jd["j"].IsObject)
            {
                UUID peerId = sessionId;
                var joinMap = jd["j"];
                IDictionary joinDict = joinMap;
                var idStr = JStr(joinDict != null && joinDict.Contains("id") ? joinMap["id"] : null);
                if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr, out peerId);
                Peers.TryAdd(peerId, new OSDMap());
                try { PeerJoined?.Invoke(peerId); } catch { }
            }

            // Leave
            if (contains(jdContains, "l") && JBool(jd["l"]) == true)
            {
                UUID peerId = sessionId;
                var idStr = JStr(jdContains.Contains("id") ? jd["id"] : null);
                if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr, out peerId);
                RemovePeer(peerId);
            }

            // Positions
            var avatarPos = new VoiceSession.AvatarPosition { AgentId = sessionId };
            bool posChanged = false;

            if (contains(jdContains, "sp") && jd["sp"].IsObject)
            {
                var sp = jd["sp"];
                IDictionary spDict = sp;
                var x = JInt(spDict != null && spDict.Contains("x") ? sp["x"] : null);
                var y = JInt(spDict != null && spDict.Contains("y") ? sp["y"] : null);
                var z = JInt(spDict != null && spDict.Contains("z") ? sp["z"] : null);
                if (x.HasValue && y.HasValue && z.HasValue) { avatarPos.SenderPosition = new VoiceSession.Int3 { X = x.Value, Y = y.Value, Z = z.Value }; posChanged = true; }
            }

            if (jdContains != null && contains(jdContains, "sh") && jd["sh"].IsObject)
            {
                var sh = jd["sh"];
                IDictionary shDict = sh;
                var x = JInt(shDict != null && shDict.Contains("x") ? sh["x"] : null);
                var y = JInt(shDict != null && shDict.Contains("y") ? sh["y"] : null);
                var z = JInt(shDict != null && shDict.Contains("z") ? sh["z"] : null);
                var w = JInt(shDict != null && shDict.Contains("w") ? sh["w"] : null);
                if (x.HasValue && y.HasValue && z.HasValue && w.HasValue) { avatarPos.SenderHeading = new VoiceSession.Int4 { X = x.Value, Y = y.Value, Z = z.Value, W = w.Value }; posChanged = true; }
            }

            if (jdContains != null && contains(jdContains, "lp") && jd["lp"].IsObject)
            {
                var lp = jd["lp"];
                IDictionary lpDict = lp;
                var x = JInt(lpDict != null && lpDict.Contains("x") ? lp["x"] : null);
                var y = JInt(lpDict != null && lpDict.Contains("y") ? lp["y"] : null);
                var z = JInt(lpDict != null && lpDict.Contains("z") ? lp["z"] : null);
                if (x.HasValue && y.HasValue && z.HasValue) { avatarPos.ListenerPosition = new VoiceSession.Int3 { X = x.Value, Y = y.Value, Z = z.Value }; posChanged = true; }
            }

            if (jdContains != null && contains(jdContains, "lh") && jd["lh"].IsObject)
            {
                var lh = jd["lh"];
                IDictionary lhDict = lh;
                var x = JInt(lhDict != null && lhDict.Contains("x") ? lh["x"] : null);
                var y = JInt(lhDict != null && lhDict.Contains("y") ? lh["y"] : null);
                var z = JInt(lhDict != null && lhDict.Contains("z") ? lh["z"] : null);
                var w = JInt(lhDict != null && lhDict.Contains("w") ? lh["w"] : null);
                if (x.HasValue && y.HasValue && z.HasValue && w.HasValue) { avatarPos.ListenerHeading = new VoiceSession.Int4 { X = x.Value, Y = y.Value, Z = z.Value, W = w.Value }; posChanged = true; }
            }

            if (posChanged)
            {
                UUID peerId = sessionId;
                var idStr = JStr(jdContains != null && jdContains.Contains("id") ? jd["id"] : null);
                if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr, out peerId);
                avatarPos.AgentId = peerId;

                // Convert to OSDMap for legacy handlers
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
            if (jdContains != null && contains(jdContains, "m") && jd["m"].IsObject)
            {
                var muteMap = jd["m"];
                IDictionary muteDict = muteMap;
                var dict = new Dictionary<UUID, bool>();
                if (muteDict != null)
                {
                    foreach (var keyObj in muteDict.Keys)
                    {
                        var key = keyObj as string;
                        if (UUID.TryParse(key, out var id))
                        {
                            var b = JBool(muteMap[key]);
                            if (b.HasValue) dict[id] = b.Value;
                        }
                    }
                }

                try
                {
                    foreach (var kv in dict)
                    {
                        if (PeerToSsrc.TryGetValue(kv.Key, out var ssrc))
                        {
                            try { _audioDevice?.SetSsrcMute(ssrc, kv.Value); } catch { }
                        }
                    }
                }
                catch { }

                if (dict.Count > 0) try { MuteMapReceived?.Invoke(dict); } catch { }
            }

            // Gain map
            if (jdContains != null && contains(jdContains, "ug") && jd["ug"].IsObject)
            {
                var gainMap = jd["ug"];
                IDictionary gainDict = gainMap;
                var dict = new Dictionary<UUID, int>();
                if (gainDict != null)
                {
                    foreach (var keyObj in gainDict.Keys)
                    {
                        var key = keyObj as string;
                        if (UUID.TryParse(key, out var id))
                        {
                            var gi = JInt(gainMap[key]);
                            if (gi.HasValue) dict[id] = gi.Value;
                        }
                    }
                }

                try
                {
                    foreach (var kv in dict)
                    {
                        if (PeerToSsrc.TryGetValue(kv.Key, out var ssrc))
                        {
                            try { _audioDevice?.SetSsrcGainPercent(ssrc, kv.Value); } catch { }
                        }
                    }
                }
                catch { }

                if (dict.Count > 0) try { GainMapReceived?.Invoke(dict); } catch { }
            }

            // Avatar list handling (av or a)
            if (jdContains != null && contains(jdContains, "av") && jd["av"].IsArray)
            {
                var arr = jd["av"];
                var list = new List<UUID>();
                for (int i = 0; i < arr.Count; i++)
                {
                    var item = arr[i];
                    var s = JStr(item);
                    if (!string.IsNullOrEmpty(s) && UUID.TryParse(s, out var id)) list.Add(id);
                }

                foreach (var id in list) Peers.AddOrUpdate(id, new OSDMap(), (k, v) => v);
                var toRemove = Peers.Keys.Except(list).ToList();
                foreach (var r in toRemove) RemovePeer(r);
                try { PeerListUpdated?.Invoke(list); } catch { }
            }
            else if (jdContains != null && contains(jdContains, "a") && jd["a"].IsObject)
            {
                var amap = jd["a"];
                IDictionary amapDict = amap;
                var list = new List<UUID>();
                if (amapDict != null)
                {
                    foreach (var keyObj in amapDict.Keys)
                    {
                        var key = keyObj as string;
                        if (UUID.TryParse(key, out var id))
                        {
                            try
                            {
                                var val = amap[key];
                                if (val == null) { RemovePeer(id); continue; }
                                if (val is JsonData jv)
                                {
                                    if ((jv.IsObject && ((IDictionary)jv).Count == 0) || (jv.IsString && string.IsNullOrEmpty(JStr(jv))))
                                    {
                                        RemovePeer(id);
                                        continue;
                                    }
                                }
                            }
                            catch { }

                            list.Add(id);
                        }
                    }
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

            // Reply to ping if present
            try
            {
                if (map.ContainsKey("ping") && map["ping"] is OSD && map["ping"].AsBoolean())
                {
                    try { sendString?.Invoke("{\"pong\":true}"); } catch { }
                }
            }
            catch { }
        }

        public bool TryGetSsrcForPeer(UUID peerId, out uint ssrc) => PeerToSsrc.TryGetValue(peerId, out ssrc);
        public bool TryGetPeerForSsrc(uint ssrc, out UUID peerId) => SsrcToPeer.TryGetValue(ssrc, out peerId);

    }
}
