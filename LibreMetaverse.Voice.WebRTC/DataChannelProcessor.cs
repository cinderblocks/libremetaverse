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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.Voice.WebRTC
{
    internal class DataChannelProcessor
    {
        private readonly PeerManager _peerManager;
        private readonly GridClient _client;
        private readonly IVoiceLogger _log;
        private readonly Func<string, bool> _sendString;

        public DataChannelProcessor(PeerManager peerManager, GridClient client, IVoiceLogger log, Func<string, bool> sendString)
        {
            _peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            _client = client;
            _log = log;
            _sendString = sendString;
        }

        // Send helpers delegated from VoiceSession
        public bool TrySend(string str)
        {
            try { return _sendString?.Invoke(str) ?? false; }
            catch (Exception ex) { try { _log.Debug($"TrySend failed: {ex.Message}", _client); } catch { } return false; }
        }

        private static string BuildJson(Action<Utf8JsonWriter> build)
        {
            var ms = new MemoryStream();
            using var w = new Utf8JsonWriter(ms, new JsonWriterOptions { SkipValidation = true });
            build(w);
            w.Flush();
            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public bool SendJoin(bool primary = true)
        {
            try
            {
                return TrySend(BuildJson(jw => {
                    jw.WriteStartObject();
                    jw.WriteStartObject("j");
                    jw.WriteBoolean("p", primary);
                    jw.WriteEndObject();
                    jw.WriteEndObject();
                }));
            }
            catch (Exception ex) { try { _log.Debug($"SendJoin failed: {ex.Message}", _client); } catch { } return false; }
        }

        public bool SendLeave()
        {
            try { return TrySend("{\"l\":true}"); }
            catch (Exception ex) { try { _log.Debug($"SendLeave failed: {ex.Message}", _client); } catch { } return false; }
        }

        public bool SendPing() => TrySend("{\"ping\":true}");
        public bool SendPong() => TrySend("{\"pong\":true}");

        public bool SetPeerMute(UUID peerId, bool mute)
        {
            try { return TrySend("{\"m\": {\"" + peerId + "\": " + (mute ? "true" : "false") + "}}"); }
            catch (Exception ex) { try { _log.Debug($"SetPeerMute failed: {ex.Message}", _client); } catch { } return false; }
        }

        /// <summary>
        /// Set per-peer gain on the voice server.
        /// <paramref name="gain"/> must be in SL's 0–220 range (PEER_GAIN_CONVERSION_FACTOR = 220).
        /// 220 = full volume (1.0), 0 = silence.
        /// </summary>
        public bool SetPeerGain(UUID peerId, int gain)
        {
            if (gain < 0) gain = 0;
            if (gain > 220) gain = 220;
            try { return TrySend("{\"ug\": {\"" + peerId + "\": " + gain + "}}"); }
            catch (Exception ex) { try { _log.Debug($"SetPeerGain failed: {ex.Message}", _client); } catch { } return false; }
        }

        /// <summary>
        /// Send avatar body position/heading (<c>sp</c>/<c>sh</c>) and camera/listener
        /// position/heading (<c>lp</c>/<c>lh</c>) as separate values.
        /// This is the preferred overload — it matches SL C++ which tracks avatar and camera poses
        /// independently. <c>listenerPos</c>/<c>listenerHeading</c> should come from the camera.
        /// </summary>
        public bool SendPosition(Vector3d avatarPos, Quaternion avatarHeading,
                                  Vector3d listenerPos, Quaternion listenerHeading)
        {
            try
            {
                int spX = (int)Math.Round(avatarPos.X * 100);
                int spY = (int)Math.Round(avatarPos.Y * 100);
                int spZ = (int)Math.Round(avatarPos.Z * 100);
                int shX = (int)Math.Round(avatarHeading.X * 100);
                int shY = (int)Math.Round(avatarHeading.Y * 100);
                int shZ = (int)Math.Round(avatarHeading.Z * 100);
                int shW = (int)Math.Round(avatarHeading.W * 100);

                int lpX = (int)Math.Round(listenerPos.X * 100);
                int lpY = (int)Math.Round(listenerPos.Y * 100);
                int lpZ = (int)Math.Round(listenerPos.Z * 100);
                int lhX = (int)Math.Round(listenerHeading.X * 100);
                int lhY = (int)Math.Round(listenerHeading.Y * 100);
                int lhZ = (int)Math.Round(listenerHeading.Z * 100);
                int lhW = (int)Math.Round(listenerHeading.W * 100);

                return TrySend(BuildJson(jw => {
                    jw.WriteStartObject();

                    jw.WriteStartObject("sp");
                    jw.WriteNumber("x", spX); jw.WriteNumber("y", spY); jw.WriteNumber("z", spZ);
                    jw.WriteEndObject();

                    jw.WriteStartObject("sh");
                    jw.WriteNumber("x", shX); jw.WriteNumber("y", shY); jw.WriteNumber("z", shZ); jw.WriteNumber("w", shW);
                    jw.WriteEndObject();

                    jw.WriteStartObject("lp");
                    jw.WriteNumber("x", lpX); jw.WriteNumber("y", lpY); jw.WriteNumber("z", lpZ);
                    jw.WriteEndObject();

                    jw.WriteStartObject("lh");
                    jw.WriteNumber("x", lhX); jw.WriteNumber("y", lhY); jw.WriteNumber("z", lhZ); jw.WriteNumber("w", lhW);
                    jw.WriteEndObject();

                    jw.WriteEndObject();
                }));
            }
            catch (Exception ex) { try { _log.Debug($"SendPosition failed: {ex.Message}", _client); } catch { } return false; }
        }

        /// <summary>
        /// Legacy single-pose overload — sets both avatar and listener to the same position/heading.
        /// Prefer the four-argument overload which separates avatar body from camera pose.
        /// </summary>
        public bool SendPosition(Vector3d globalPos, Quaternion heading)
            => SendPosition(globalPos, heading, globalPos, heading);

        public bool SendAvatarArray(List<UUID> avatars)
        {
            try
            {
                return TrySend(BuildJson(jw => {
                    jw.WriteStartObject();
                    jw.WriteStartArray("av");
                    if (avatars != null) foreach (var id in avatars) jw.WriteStringValue(id.ToString());
                    jw.WriteEndArray();
                    jw.WriteEndObject();
                }));
            }
            catch (Exception ex) { try { _log.Debug($"SendAvatarArray failed: {ex.Message}", _client); } catch { } return false; }
        }

        public bool SendAvatarMap(IEnumerable<UUID> avatars)
        {
            try
            {
                return TrySend(BuildJson(jw => {
                    jw.WriteStartObject();
                    jw.WriteStartObject("a");
                    if (avatars != null) foreach (var id in avatars)
                    {
                        jw.WriteStartObject(id.ToString());
                        jw.WriteEndObject();
                    }
                    jw.WriteEndObject();
                    jw.WriteEndObject();
                }));
            }
            catch (Exception ex) { try { _log.Debug($"SendAvatarMap failed: {ex.Message}", _client); } catch { } return false; }
        }

        public bool SendMuteMap(Dictionary<UUID, bool> muteMap)
        {
            try
            {
                return TrySend(BuildJson(jw => {
                    jw.WriteStartObject();
                    jw.WriteStartObject("m");
                    if (muteMap != null) foreach (var kv in muteMap) jw.WriteBoolean(kv.Key.ToString(), kv.Value);
                    jw.WriteEndObject();
                    jw.WriteEndObject();
                }));
            }
            catch (Exception ex) { try { _log.Debug($"SendMuteMap failed: {ex.Message}", _client); } catch { } return false; }
        }

        /// <summary>
        /// Send a batch gain map. Values must be in SL's 0–220 range (PEER_GAIN_CONVERSION_FACTOR = 220).
        /// </summary>
        public bool SendGainMap(Dictionary<UUID, int> gainMap)
        {
            try
            {
                return TrySend(BuildJson(jw => {
                    jw.WriteStartObject();
                    jw.WriteStartObject("ug");
                    if (gainMap != null) foreach (var kv in gainMap)
                    {
                        int v = kv.Value < 0 ? 0 : kv.Value > 220 ? 220 : kv.Value;
                        jw.WriteNumber(kv.Key.ToString(), v);
                    }
                    jw.WriteEndObject();
                    jw.WriteEndObject();
                }));
            }
            catch (Exception ex) { try { _log.Debug($"SendGainMap failed: {ex.Message}", _client); } catch { } return false; }
        }

        public void ProcessMessage(string msg, UUID sessionId)
        {
            if (string.IsNullOrWhiteSpace(msg)) return;

            try
            {
                JsonDocument? doc = null;
                try { doc = JsonDocument.Parse(msg); }
                catch (Exception ex) { try { _log.Debug($"JSON parsing failed: {ex.Message}", _client); } catch { } }

                if (doc != null)
                {
                    using (doc)
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            try { _peerManager.ProcessJsonElement(doc.RootElement, _sendString, sessionId); }
                            catch (Exception ex) { try { _log.Error($"PeerManager.ProcessJsonElement failed: {ex.Message}", _client); } catch { } }
                            return;
                        }
                    }
                }

                // Fallback to OSD path
                try
                {
                    var osd = OSDParser.DeserializeJson(msg);
                    if (osd is OSDMap map)
                    {
                        try { _peerManager.ProcessOSDMap(map, _sendString, sessionId); } catch (Exception ex) { try { _log.Error($"PeerManager.ProcessOSDMap failed: {ex.Message}", _client); } catch { } }
                        return;
                    }

                    try { _log.Debug($"Data channel payload is not an OSDMap (type={osd?.GetType().Name}). Raw: {msg}", _client); } catch { }
                }
                catch (Exception ex)
                {
                    try { _log.Error($"Failed to deserialize fallback OSD JSON: {ex.Message}. Raw: {msg}", _client); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { _log.Error($"DataChannelProcessor failed to process message: {ex.Message}. Raw: {msg}", _client); } catch { }
            }
        }
    }
}
