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
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using Microsoft.Extensions.Logging;
using LibreMetaverse.Voice.WebRTC;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.SDL3;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace LibreMetaverse
{
    public class Sdl3Audio : IDisposable
    {
        public SDL3AudioEndPoint EndPoint { get; private set; }
        public SDL3AudioSource Source { get; private set; }
        private readonly OpusAudioEncoder _audioEncoder = new OpusAudioEncoder();
        public bool IsAvailable { get; } = false;

        public event Action<bool> OnPlaybackActiveChanged;
        public event Action<bool> OnRecordingActiveChanged;
        public event Action<uint, byte[]> OnAudioSourceEncodedSample;

        public bool PlaybackActive { get; private set; } = false;
        public bool RecordingActive { get; private set; } = false;

        public (uint id, string name) PlaybackDevice { get; private set; }
        public (uint id, string name) RecordingDevice { get; private set; }

        private float _speakerLevel = 1.0f;
        public float SpeakerLevel
        {
            get => _speakerLevel;
            set
            {
                var v = Math.Max(0.0f, Math.Min(1.0f, value));
                _speakerLevel = v;
                TrySetEndpointVolume(v);
            }
        }

        public bool MicMute
        {
            get => !RecordingActive;
            set
            {
                try
                {
                    if (value) StopRecording(); else StartRecording();
                }
                catch { }
            }
        }

        private readonly IVoiceLogger _log;

        public Sdl3Audio(IVoiceLogger logger = null)
        {
            _log = logger ?? new OpenMetaverseVoiceLogger();
            try { var factory = LoggerFactory.Create(builder => { builder.AddProvider(new VoiceLoggerProvider(_log)); }); } catch { }

            try
            {
                SDL3Helper.InitSDL();
                _log.Debug("SDL3 initialized successfully");

                var playbackDeviceIndex = DeviceSelection(false);
                var recordingDeviceIndex = DeviceSelection(true);
                var playbackDevice = GetDevice(playbackDeviceIndex, false);
                var recordingDevice = GetDevice(recordingDeviceIndex, true);

                PlaybackDevice = playbackDevice ?? (id: 0, name: string.Empty);
                RecordingDevice = recordingDevice ?? (id: 0, name: string.Empty);

                EndPoint = new SDL3AudioEndPoint(PlaybackDevice.name, _audioEncoder);
                var fmt = new AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                TrySetEndpointVolume(_speakerLevel);

                try
                {
                    Source = new SDL3AudioSource(RecordingDevice.name, _audioEncoder);
                    AttachSourceHandlers();
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to create audio source (recording may not work): {ex.Message}");
                    Source = null;
                }

                IsAvailable = true;
                // Do not auto-start playback here. Playback should be controlled by connection state
                // to avoid starting too early and to allow proper restart after disconnects.
            }
            catch (Exception ex)
            {
                _log.Error($"SDL3 initialization failed: {ex.Message}");
                IsAvailable = false;
                EndPoint = null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (PlaybackActive) { _ = StopPlaybackAsync(); }
                if (RecordingActive) StopRecording();
                if (IsAvailable) SDL3Helper.QuitSDL();
            }
            catch { }
        }

        private int DeviceSelection(bool recordingDevice)
        {
            var sdlDevices = recordingDevice ? SDL3Helper.GetAudioRecordingDevices() : SDL3Helper.GetAudioPlaybackDevices();
            if (sdlDevices.Count < 1) { _log.Warn($"SDL Audio - Could not find an audio {(recordingDevice ? "recording" : "playback")} device."); return -1; }
            return -1;
        }

        private (uint id, string name)? GetDevice(int index, bool recordingDevice)
        {
            if (index < 0) return recordingDevice ? SDL3Helper.GetAudioRecordingDevice(null) : SDL3Helper.GetAudioPlaybackDevice(null);
            return recordingDevice ? SDL3Helper.GetAudioRecordingDevice(index) : SDL3Helper.GetAudioPlaybackDevice(index);
        }

        private void TrySetEndpointVolume(float normalizedVolume)
        {
            if (EndPoint == null) return;
            try
            {
                var epType = EndPoint.GetType();
                var methodNames = new[] { "SetVolume", "SetPlaybackVolume", "SetSinkVolume", "SetAudioVolume" };
                foreach (var name in methodNames)
                {
                    var mi = epType.GetMethod(name, new[] { typeof(float) }) ?? epType.GetMethod(name, new[] { typeof(double) }) ?? epType.GetMethod(name, new[] { typeof(int) });
                    if (mi != null)
                    {
                        var p = mi.GetParameters()[0].ParameterType;
                        if (p == typeof(float)) mi.Invoke(EndPoint, new object[] { normalizedVolume });
                        else if (p == typeof(double)) mi.Invoke(EndPoint, new object[] { (double)normalizedVolume });
                        else if (p == typeof(int)) mi.Invoke(EndPoint, new object[] { (int)Math.Round(normalizedVolume * 100) });
                        return;
                    }
                }
                var prop = epType.GetProperty("Volume") ?? epType.GetProperty("PlaybackVolume") ?? epType.GetProperty("Level");
                if (prop != null && prop.CanWrite)
                {
                    var pt = prop.PropertyType;
                    if (pt == typeof(float)) prop.SetValue(EndPoint, normalizedVolume);
                    else if (pt == typeof(double)) prop.SetValue(EndPoint, (double)normalizedVolume);
                    else if (pt == typeof(int)) prop.SetValue(EndPoint, (int)Math.Round(normalizedVolume * 100));
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to set endpoint volume: {ex.Message}");
            }
        }

        private void AttachSourceHandlers()
        {
            if (Source == null) return;
            try { Source.OnAudioSourceEncodedSample += AudioSource_OnAudioSourceEncodedSample; } catch { }
            try { Source.OnAudioSourceRawSample += AudioSource_OnAudioSourceRawSample; } catch { }
        }

        private void DetachSourceHandlers()
        {
            if (Source == null) return;
            try { Source.OnAudioSourceEncodedSample -= AudioSource_OnAudioSourceEncodedSample; } catch { }
            try { Source.OnAudioSourceRawSample -= AudioSource_OnAudioSourceRawSample; } catch { }
        }

        // Playback/recording control
        public async Task StartPlaybackAsync()
        {
            if (!IsAvailable || EndPoint == null) return;
            try { await EndPoint.StartAudioSink(); PlaybackActive = true; OnPlaybackActiveChanged?.Invoke(true); _log.Debug("Playback started"); } catch (Exception ex) { _log.Error($"Failed to start playback: {ex.Message}"); }
        }

        public async Task StopPlaybackAsync()
        {
            if (!IsAvailable || EndPoint == null) return;
            try { await EndPoint.CloseAudioSink(); PlaybackActive = false; OnPlaybackActiveChanged?.Invoke(false); _log.Debug("Playback stopped"); } catch (Exception ex) { _log.Error($"Failed to stop playback: {ex.Message}"); }
        }

        public void StartRecording()
        {
            if (!IsAvailable || Source == null) return;
            try { Source.StartAudio(); RecordingActive = true; OnRecordingActiveChanged?.Invoke(true); _log.Debug("Recording started"); } catch (Exception ex) { _log.Error($"Failed to start recording: {ex.Message}"); }
        }

        public void StopRecording()
        {
            if (!IsAvailable || Source == null) return;
            try { DetachSourceHandlers(); StopSourceSafely(Source); RecordingActive = false; OnRecordingActiveChanged?.Invoke(false); _log.Debug("Recording stopped"); } catch (Exception ex) { _log.Error($"Failed to stop recording: {ex.Message}"); }
        }

        private void StopSourceSafely(object source)
        {
            if (source == null) return;
            var t = source.GetType();
            var mi = t.GetMethod("StopAudio", Type.EmptyTypes) ?? t.GetMethod("Stop", Type.EmptyTypes) ?? t.GetMethod("Close", Type.EmptyTypes);
            if (mi != null) { mi.Invoke(source, null); return; }
            if (source is IDisposable d) d.Dispose();
        }

        // File playback and raw stream support
        private Task _filePlaybackTask;
        private CancellationTokenSource _filePlaybackCts;
        private bool _filePlaybackLoop = false;
        private bool _filePlaybackActive = false;
        private bool _filePlaybackWasRecording = false;
        private string _filePlaybackPath;

        private Task _rawStreamTask;
        private CancellationTokenSource _rawStreamCts;
        private bool _rawStreamActive = false;

        public void StartFilePlayback(string path, bool loop = false)
        {
            if (!IsAvailable) throw new InvalidOperationException("SDL3 audio not available");
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            if (!File.Exists(path)) throw new FileNotFoundException(path);

            _filePlaybackWasRecording = RecordingActive;
            if (RecordingActive) { try { StopRecording(); } catch { } }

            _filePlaybackPath = path;
            _filePlaybackLoop = loop;
            _filePlaybackCts = new CancellationTokenSource();
            var token = _filePlaybackCts.Token;
            _filePlaybackActive = true;

            _filePlaybackTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                        using (var br = new BinaryReader(fs))
                        {
                            var riff = new string(br.ReadChars(4));
                            if (riff != "RIFF") throw new InvalidDataException("Not a WAV file (missing RIFF)");
                            br.ReadInt32();
                            var wave = new string(br.ReadChars(4));
                            if (wave != "WAVE") throw new InvalidDataException("Not a WAV file (missing WAVE)");

                            int channels = 1; int sampleRate = 48000; short bitsPerSample = 16;
                            long dataChunkPos = -1;

                            while (fs.Position < fs.Length)
                            {
                                var chunkId = new string(br.ReadChars(4));
                                int chunkSize = br.ReadInt32();
                                if (chunkId == "fmt ")
                                {
                                    br.ReadInt16(); channels = br.ReadInt16(); sampleRate = br.ReadInt32(); br.ReadInt32(); br.ReadInt16(); bitsPerSample = br.ReadInt16();
                                    var remaining = chunkSize - 16; if (remaining > 0) br.ReadBytes(remaining);
                                }
                                else if (chunkId == "data") { dataChunkPos = fs.Position; break; }
                                else { br.ReadBytes(chunkSize); }
                            }

                            if (dataChunkPos < 0) throw new InvalidDataException("WAV data chunk not found");
                            if (bitsPerSample != 16) { _log.Warn($"WAV playback only supports 16-bit PCM files (got {bitsPerSample})"); break; }

                            fs.Position = dataChunkPos;
                            int frameSize = _audioEncoder.GetFrameSize();
                            int bytesPerFrame = frameSize * (bitsPerSample / 8) * channels;

                            while (!token.IsCancellationRequested)
                            {
                                var bytes = br.ReadBytes(bytesPerFrame);
                                if (bytes == null || bytes.Length == 0) break;
                                if (bytes.Length < bytesPerFrame) { var padded = new byte[bytesPerFrame]; Array.Copy(bytes, 0, padded, 0, bytes.Length); bytes = padded; }

                                short[] pcm = new short[bytes.Length / 2];
                                for (int i = 0, si = 0; i < bytes.Length; i += 2) pcm[si++] = BitConverter.ToInt16(bytes, i);

                                short[] mono = DownmixToMono(pcm, channels);
                                if (sampleRate != 48000) mono = ResampleTo48k(mono, sampleRate);

                                int idx = 0;
                                while (idx < mono.Length)
                                {
                                    int remaining = mono.Length - idx;
                                    short[] frame = new short[frameSize];
                                    if (remaining >= frameSize) Array.Copy(mono, idx, frame, 0, frameSize); else Array.Copy(mono, idx, frame, 0, remaining);
                                    byte[] encoded = null;
                                    try { encoded = _audioEncoder.EncodeAudio(frame, OpusAudioEncoder.MEDIA_FORMAT_OPUS); } catch (Exception ex) { _log.Warn($"WAV encode failed: {ex.Message}"); encoded = null; }
                                    if (encoded != null && encoded.Length > 0) { try { OnAudioSourceEncodedSample?.Invoke((uint)frameSize, encoded); } catch { } }
                                    idx += frameSize; if (token.IsCancellationRequested) break;
                                }

                                try { int frames = Math.Max(1, mono.Length / frameSize); await Task.Delay(20 * frames, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                            }
                        }

                        if (!_filePlaybackLoop) break;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _log.Warn($"File playback failed: {ex.Message}"); }
                finally { _filePlaybackActive = false; }

                if (_filePlaybackWasRecording) { try { StartRecording(); } catch { } }
            }, token);
        }

        public void StopFilePlayback()
        {
            if (!_filePlaybackActive || _filePlaybackCts == null) return;
            try { _filePlaybackCts.Cancel(); _filePlaybackTask?.Wait(500); } catch { } finally { _filePlaybackCts.Dispose(); _filePlaybackCts = null; _filePlaybackActive = false; }
        }

        // Convenience helpers
        public void PlayFileOnce(string path) { StartFilePlayback(path, false); }
        public void PlayFileLoop(string path) { StartFilePlayback(path, true); }
        public void StopFile() { StopFilePlayback(); }
        public void SetPlaybackVolume(float normalized) { SpeakerLevel = normalized; }

        public void SetCaptureGainPercent(int percent)
        {
            if (percent < 0) percent = 0; if (percent > 100) percent = 100;
            if (Source == null) { _log.Warn("SetCaptureGainPercent called but Source is null"); return; }
            try
            {
                var t = Source.GetType();
                var methodNames = new[] { "SetMicLevel", "SetCaptureGain", "SetGain", "SetVolume", "SetLevel", "SetCaptureLevel" };
                foreach (var name in methodNames)
                {
                    var mi = t.GetMethod(name, new[] { typeof(int) }) ?? t.GetMethod(name, new[] { typeof(float) }) ?? t.GetMethod(name, new[] { typeof(double) });
                    if (mi != null)
                    {
                        var p = mi.GetParameters()[0].ParameterType;
                        if (p == typeof(int)) mi.Invoke(Source, new object[] { percent });
                        else if (p == typeof(float)) mi.Invoke(Source, new object[] { (float)percent / 100f });
                        else if (p == typeof(double)) mi.Invoke(Source, new object[] { (double)percent / 100.0 });
                        return;
                    }
                }
                var prop = t.GetProperty("Level") ?? t.GetProperty("Gain") ?? t.GetProperty("MicLevel") ?? t.GetProperty("CaptureLevel");
                if (prop != null && prop.CanWrite)
                {
                    var pt = prop.PropertyType;
                    if (pt == typeof(int)) prop.SetValue(Source, percent);
                    else if (pt == typeof(float)) prop.SetValue(Source, (float)percent / 100f);
                    else if (pt == typeof(double)) prop.SetValue(Source, (double)percent / 100.0);
                    return;
                }
                _log.Warn("SetCaptureGainPercent: no known setter found on SDL3AudioSource (operation ignored)");
            }
            catch (Exception ex) { _log.Warn($"SetCaptureGainPercent failed: {ex.Message}"); }
        }

        // Raw PCM streaming
        public void StartRawPcmStream(Stream pcmStream, int channels = 1, int sampleRate = 48000)
        {
            if (pcmStream == null) throw new ArgumentNullException(nameof(pcmStream));
            if (!pcmStream.CanRead) throw new ArgumentException("Stream is not readable", nameof(pcmStream));
            if (_rawStreamActive) StopRawPcmStream();

            _rawStreamCts = new CancellationTokenSource();
            _rawStreamActive = true;
            _rawStreamTask = PlayRawPcmStreamAsync(pcmStream, channels, sampleRate, _rawStreamCts.Token).ContinueWith(t => { _rawStreamActive = false; if (t.IsFaulted) _log.Warn($"Raw PCM stream task faulted: {t.Exception?.GetBaseException().Message}"); }, TaskScheduler.Default);
        }

        public void StartRawPcmFileLoop(string path, int channels = 1, bool loop = true)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            if (_rawStreamActive) StopRawPcmStream();

            _rawStreamCts = new CancellationTokenSource();
            var token = _rawStreamCts.Token;
            _rawStreamActive = true;

            _rawStreamTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var br = new BinaryReader(fs, System.Text.Encoding.UTF8, true))
                        {
                            var riff = new string(br.ReadChars(4)); fs.Position = 0;
                            if (riff == "RIFF")
                            {
                                int channelsFound = 1; int sampleRateFound = 48000; long dataChunkPos = -1;
                                br.ReadChars(12);
                                while (fs.Position < fs.Length)
                                {
                                    var chunkId = new string(br.ReadChars(4)); int chunkSize = br.ReadInt32();
                                    if (chunkId == "fmt ") { br.ReadInt16(); channelsFound = br.ReadInt16(); sampleRateFound = br.ReadInt32(); br.ReadInt32(); br.ReadInt16(); var remaining = chunkSize - 16; if (remaining > 0) br.ReadBytes(remaining); }
                                    else if (chunkId == "data") { dataChunkPos = fs.Position; break; }
                                    else { br.ReadBytes(chunkSize); }
                                }

                                if (dataChunkPos >= 0) { fs.Position = dataChunkPos; await PlayRawPcmStreamAsync(fs, channelsFound, sampleRateFound, token).ConfigureAwait(false); }
                                else { fs.Position = 0; await PlayRawPcmStreamAsync(fs, channels, 48000, token).ConfigureAwait(false); }
                            }
                            else { fs.Position = 0; await PlayRawPcmStreamAsync(fs, channels, 48000, token).ConfigureAwait(false); }
                        }
                        if (!loop) break;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _log.Warn($"Raw PCM file loop failed: {ex.Message}"); }
                finally { _rawStreamActive = false; }
            }, token);
        }

        public void StopRawPcmStream()
        {
            if (!_rawStreamActive || _rawStreamCts == null) return;
            try { _rawStreamCts.Cancel(); _rawStreamTask?.Wait(500); } catch { } finally { _rawStreamCts.Dispose(); _rawStreamCts = null; _rawStreamActive = false; }
        }

        public void FeedPcmSamples(short[] pcmInterleaved, int channels = 1, int sampleRate = 48000)
        {
            if (pcmInterleaved == null) throw new ArgumentNullException(nameof(pcmInterleaved));
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
            var frameSize = _audioEncoder.GetFrameSize();
            short[] monoSamples = DownmixToMono(pcmInterleaved, channels);
            if (sampleRate != 48000) monoSamples = ResampleTo48k(monoSamples, sampleRate);
            int idx = 0;
            while (idx < monoSamples.Length)
            {
                int remaining = monoSamples.Length - idx; short[] frame = new short[frameSize];
                if (remaining >= frameSize) Array.Copy(monoSamples, idx, frame, 0, frameSize); else Array.Copy(monoSamples, idx, frame, 0, remaining);
                byte[] encoded = null; try { encoded = _audioEncoder.EncodeAudio(frame, OpusAudioEncoder.MEDIA_FORMAT_OPUS); } catch (Exception ex) { _log.Warn($"FeedPcmSamples: encode failed: {ex.Message}"); }
                if (encoded != null && encoded.Length > 0) { try { OnAudioSourceEncodedSample?.Invoke((uint)frameSize, encoded); } catch { } }
                idx += frameSize;
            }
        }

        public void FeedPcmBytes(byte[] pcmBytes, int channels = 1, int sampleRate = 48000)
        {
            if (pcmBytes == null) throw new ArgumentNullException(nameof(pcmBytes));
            if (pcmBytes.Length % 2 != 0) throw new ArgumentException("PCM byte array length must be even (16-bit samples)", nameof(pcmBytes));
            int sampleCount = pcmBytes.Length / 2; short[] samples = new short[sampleCount];
            for (int i = 0, si = 0; i < pcmBytes.Length; i += 2, si++) samples[si] = BitConverter.ToInt16(pcmBytes, i);
            FeedPcmSamples(samples, channels, sampleRate);
        }

        public async Task PlayRawPcmStreamAsync(Stream pcmStream, int channels = 1, int sampleRate = 48000, CancellationToken ct = default)
        {
            if (pcmStream == null) throw new ArgumentNullException(nameof(pcmStream));
            if (!pcmStream.CanRead) throw new ArgumentException("Stream is not readable", nameof(pcmStream));
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
            var frameSize = _audioEncoder.GetFrameSize(); int bytesPerSample = 2; int bytesPerFrame = frameSize * bytesPerSample * channels; var buffer = new byte[bytesPerFrame];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = 0; while (read < bytesPerFrame) { int r = await pcmStream.ReadAsync(buffer, read, bytesPerFrame - read, ct).ConfigureAwait(false); if (r == 0) break; read += r; }
                    if (read == 0) break;
                    if (read < bytesPerFrame) for (int i = read; i < bytesPerFrame; i++) buffer[i] = 0;
                    FeedPcmBytes(buffer, channels, sampleRate);
                    try { await Task.Delay(20, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _log.Warn($"PlayRawPcmStreamAsync failed: {ex.Message}"); }
        }

        // Helpers
        private short[] ResampleTo48k(short[] src, int srcRate)
        {
            if (src == null) return Array.Empty<short>(); if (srcRate == 48000) return src; if (src.Length == 0) return Array.Empty<short>();
            double ratio = 48000.0 / srcRate; int dstLen = (int)Math.Round(src.Length * ratio); if (dstLen < 1) return Array.Empty<short>();
            var dst = new short[dstLen]; for (int i = 0; i < dstLen; i++) { double srcPos = i / ratio; int i0 = (int)Math.Floor(srcPos); int i1 = i0 + 1; if (i0 >= src.Length) i0 = src.Length - 1; if (i1 >= src.Length) i1 = src.Length - 1; double frac = srcPos - Math.Floor(srcPos); double s0 = src[i0]; double s1 = src[i1]; var v = (short)Math.Round(s0 * (1.0 - frac) + s1 * frac); dst[i] = v; } return dst;
        }

        private short[] DownmixToMono(short[] interleaved, int channels)
        {
            if (channels <= 1) return interleaved;
            int monoLen = interleaved.Length / channels; var mono = new short[monoLen]; for (int i = 0; i < monoLen; i++) { int acc = 0; for (int c = 0; c < channels; c++) acc += interleaved[i * channels + c]; mono[i] = (short)(acc / channels); } return mono;
        }

        // Incoming source callbacks
        private int _samplesReceivedCount = 0;
        public void AudioSource_OnAudioSourceEncodedSample(uint durationRtpUnits, byte[] sample)
        {
            _samplesReceivedCount++;
            try { OnAudioSourceEncodedSample?.Invoke(durationRtpUnits, sample); } catch { }

            if (!IsAvailable || EndPoint == null) return;
            // Auto-start playback when the first encoded samples arrive (up to a few samples)
            // to match previous behavior and ensure audio plays even if StartPlaybackAsync
            // wasn't called explicitly by the host.
            if (!PlaybackActive) { if (_samplesReceivedCount <= 5) { _ = StartPlaybackAsync(); } return; }

            if (sample == null || sample.Length == 0) return;
            try
            {
                var pcmSample = _audioEncoder.DecodeAudio(sample, OpusAudioEncoder.MEDIA_FORMAT_OPUS);
                if (pcmSample == null || pcmSample.Length == 0) return;
                var pcmBytes = pcmSample.SelectMany(BitConverter.GetBytes).ToArray();
                EndPoint.PutAudioSample(pcmBytes);
            }
            catch { }
        }

        public void AudioSource_OnAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            try { byte[] pcmBytes = sample.SelectMany(BitConverter.GetBytes).ToArray(); EndPoint.PutAudioSample(pcmBytes); } catch { }
        }

        // Device lists
        public IReadOnlyDictionary<uint, string> GetPlaybackDevices()
        {
            return SDL3Helper.GetAudioPlaybackDevices();
        }

        public IReadOnlyDictionary<uint, string> GetRecordingDevices()
        {
            return SDL3Helper.GetAudioRecordingDevices();
        }

        // Replace playback device at runtime (recreates endpoint)
        public void SetPlaybackDevice(string deviceName)
        {
            if (!IsAvailable) return;
            try
            {
                if (deviceName == "Default Speakers" || deviceName == "Default Microphone") deviceName = null;
                try { EndPoint?.CloseAudioSink().Wait(2000); } catch { }
                EndPoint = new SDL3AudioEndPoint(deviceName, _audioEncoder);
                var fmt = new AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                TrySetEndpointVolume(_speakerLevel);
                if (PlaybackActive) { _ = StartPlaybackAsync(); }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to set playback device: {ex.Message}");
            }
        }

        public void SetRecordingDevice(string deviceName)
        {
            if (!IsAvailable) return;
            try
            {
                try { DetachSourceHandlers(); StopSourceSafely(Source); } catch { }

                Exception lastEx = null;
                try { Source = new SDL3AudioSource(deviceName, _audioEncoder); }
                catch (Exception ex1) { lastEx = ex1; Source = null; }

                if (Source == null && deviceName != null)
                {
                    try { Source = new SDL3AudioSource(null, _audioEncoder); }
                    catch (Exception ex2) { lastEx = ex2; Source = null; }
                }

                if (Source == null)
                {
                    try { Source = new SDL3AudioSource(string.Empty, _audioEncoder); }
                    catch (Exception ex3) { lastEx = ex3; Source = null; }
                }

                if (Source == null)
                {
                    _log.Warn($"Failed to create recording source (device='{deviceName}'): {lastEx?.Message}");
                    throw new InvalidOperationException("Failed to create SDL3 audio source.", lastEx);
                }

                AttachSourceHandlers();
                if (RecordingActive) StartRecording();
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to set recording device: {ex.Message}");
                throw;
            }
        }

        public bool EnsureEndpoint()
        {
            if (!IsAvailable) { _log.Warn("Cannot ensure endpoint: SDL3 not available"); return false; }
            if (EndPoint != null) return true;
            try
            {
                EndPoint = new SDL3AudioEndPoint(PlaybackDevice.name, _audioEncoder);
                var fmt = new AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                TrySetEndpointVolume(_speakerLevel);
                _log.Debug("SDL3AudioEndPoint created");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to create SDL3AudioEndPoint: {ex.Message}");
                EndPoint = null;
                return false;
            }
        }
    }
}
