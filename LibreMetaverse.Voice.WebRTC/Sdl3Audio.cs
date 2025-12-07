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

namespace LibreMetaverse
{
    public class Sdl3Audio : IDisposable
    {
        public SDL3AudioEndPoint EndPoint { get; private set; } // For Playback (Sink)
        public SDL3AudioSource Source { get; private set; }     // For Microphone (Source)
        private readonly OpusAudioEncoder _audioEncoder = new OpusAudioEncoder();
        public bool IsAvailable { get; } = false;
        // Exposed state and events for consumers
        public event Action<bool> OnPlaybackActiveChanged;
        public event Action<bool> OnRecordingActiveChanged;
        public event Action<uint, byte[]> OnAudioSourceEncodedSample;

        public bool PlaybackActive { get; private set; } = false;
        public bool RecordingActive { get; private set; } = false;

        public (uint id, string name) PlaybackDevice { get; private set; }
        public (uint id, string name) RecordingDevice { get; private set; }

        // Convenience properties expected by some consumers
        private float _speakerLevel = 1.0f; // 0.0 - 1.0
        public float SpeakerLevel
        {
            get => _speakerLevel;
            set
            {
                // clamp
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
                    if (value)
                        StopRecording();
                    else
                        StartRecording();
                }
                catch { }
            }
        }

        // Returns playback device names from SDL helper
        public Dictionary<uint, string> GetPlaybackDevices()
        {
            return SDL3Helper.GetAudioPlaybackDevices();
        }

        // Returns recording device names from SDL helper
        public Dictionary<uint, string> GetRecordingDevices()
        {
            return SDL3Helper.GetAudioRecordingDevices();
        }

        private readonly IVoiceLogger _log;

        public Sdl3Audio(IVoiceLogger logger = null)
        {
            _log = logger ?? new OpenMetaverseVoiceLogger();
            // Register adapter provider so SIPSorcery components using ILoggerFactory can be wired to our voice logger
            try
            {
                var factory = LoggerFactory.Create(builder => { builder.AddProvider(new VoiceLoggerProvider(_log)); });
                // If SIPSorcery components accept a global factory, set it here. Otherwise, providers created later will pick this up when supplied.
            }
            catch { }
            try
            {
                SDL3Helper.InitSDL();
                _log.Debug("SDL3 initialized successfully");

                var playbackDeviceIndex = DeviceSelection(false);
                var recordingDeviceIndex = DeviceSelection(true);
                var playbackDevice = GetDevice(playbackDeviceIndex, false);
                var recordingDevice = GetDevice(recordingDeviceIndex, true);

                PlaybackDevice = playbackDevice ?? (id:0, name:string.Empty);
                RecordingDevice = recordingDevice ?? (id: 0, name: string.Empty);

                _log.Debug($"Playback device: '{playbackDevice}'");
                _log.Debug($"Recording device: '{recordingDevice}'");

                EndPoint = new SDL3AudioEndPoint(PlaybackDevice.name, _audioEncoder);
                _log.Debug("SDL3AudioEndPoint created");

                // Use stereo sink to match incoming Opus stereo (opus/48000/2)
                var fmt = new AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                _log.Debug($"Audio sink format set: {fmt.Codec}, {fmt.ClockRate} Hz, {fmt.ChannelCount} ch");

                // Set initial volume
                TrySetEndpointVolume(_speakerLevel);

                // Initialize recording source
                try
                {
                    Source = new SDL3AudioSource(RecordingDevice.name, _audioEncoder);
                    _log.Debug("SDL3AudioSource created");
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to create audio source (recording may not work): {ex.Message}");
                    Source = null;
                }

                // Mark as available AFTER successful initialization
                IsAvailable = true;
                _log.Debug("SDL3 audio system ready");

                // Automatically start playback to hear audio
                _ = StartPlaybackAsync();
            }
            catch (DllNotFoundException dllEx)
            {
                _log.Error($"SDL3 DLL not found: {dllEx.Message}");
                IsAvailable = false;
                EndPoint = null;
            }
            catch (EntryPointNotFoundException epnEx)
            {
                _log.Error($"SDL3 entry point missing: {epnEx.Message}");
                IsAvailable = false;
                EndPoint = null;
            }
            catch (BadImageFormatException bife)
            {
                // Common on architecture mismatch between native SDL3 and running process
                _log.Error($"SDL3 initialization failed (bad image / incorrect native format): {bife.Message}");
                _log.Error("Possible causes: mixing x86/x64 native SDL3 binaries with a process of the opposite bitness.");
                IsAvailable = false;
                EndPoint = null;
            }
            catch (Exception ex)
            {
                _log.Error($"SDL3 initialization failed: {ex.Message}\n{ex.StackTrace}");
                IsAvailable = false;
                EndPoint = null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (PlaybackActive)
                {
                    _ = StopPlaybackAsync();
                }
                if (RecordingActive)
                {
                    StopRecording();
                }

                if (IsAvailable)
                {
                    SDL3Helper.QuitSDL();
                }
            }
            catch { }
        }

        private int DeviceSelection(bool recordingDevice)
        {
            var deviceTypeStr = recordingDevice ? "recording" : "playback";
            var sdlDevices = recordingDevice
                ? SDL3Helper.GetAudioRecordingDevices()
                : SDL3Helper.GetAudioPlaybackDevices();

            // Quit if no Audio devices found
            if (sdlDevices.Count < 1)
            {
                _log.Warn($"SDL Audio - Could not find an audio {deviceTypeStr} device.");
                return -1;
            }

            // Prefer using the system default device. SDL typically accepts a null/empty device name to mean the default
            // Return -1 to indicate "use default" and let GetDeviceName translate that to null.
            _log.Debug($"SDL Audio - Found {sdlDevices.Count} {deviceTypeStr} devices, using system default device.");
            return -1;
        }

        private static (uint id, string name)? GetDevice(int index, bool recordingDevice)
        {
            return recordingDevice
                ? SDL3Helper.GetAudioRecordingDevice(index)
                : SDL3Helper.GetAudioPlaybackDevice(index);
        }

        public void AudioSource_OnAudioSourceEncodedSample(uint durationRtpUnits, byte[] sample)
        {
            _samplesReceivedCount++;

            if (_samplesReceivedCount == 1)
            {
             //   Logger.Log($"FIRST audio sample received! IsAvailable={IsAvailable}, EndPoint={(EndPoint != null ? "exists" : "null")}, PlaybackActive={PlaybackActive}", Helpers.LogLevel.Info);
            }

            if (_samplesReceivedCount % 50 == 1 && _samplesReceivedCount > 1) // Log every 50th sample to avoid spam
            {
               // Logger.Log($"Audio samples received: {_samplesReceivedCount}", Helpers.LogLevel.Info);
            }

            if (!IsAvailable)
            {
                _log.Warn("SDL3 audio not available");
                return;
            }

            if (EndPoint == null)
            {
                _log.Warn("Audio sink EndPoint is null — SDL3 not initialized or unavailable.");
                return;
            }

            if (!PlaybackActive)
            {
                if (_samplesReceivedCount <= 5)
                {
                    _log.Warn("Playback not active, attempting to start...");
                    _ = StartPlaybackAsync();
                }
                return;
            }

            if (sample == null || sample.Length == 0)
            {
                _log.Warn("Received empty audio sample.");
                return;
            }

            if (_samplesReceivedCount <= 3) // Log first few samples in detail
            {
                _log.Debug($"Decoding Opus sample #{_samplesReceivedCount}: {sample.Length} bytes, duration: {durationRtpUnits} RTP units");
            }

            try
            {
                var pcmSample = _audioEncoder.DecodeAudio(sample, OpusAudioEncoder.MEDIA_FORMAT_OPUS);

                if (pcmSample == null || pcmSample.Length == 0)
                {
                    if (_samplesReceivedCount <= 5)
                    {
                        _log.Warn($"Decoded PCM sample is null or empty for sample #{_samplesReceivedCount}");
                    }
                    return;
                }

                if (_samplesReceivedCount <= 3)
                {
                    _log.Debug($"Decoded to PCM: {pcmSample.Length} samples");
                }

                var pcmBytes = pcmSample.SelectMany(BitConverter.GetBytes).ToArray();

                if (_samplesReceivedCount <= 3)
                {
                    _log.Debug($"Sending {pcmBytes.Length} bytes to sink (sample #{_samplesReceivedCount})");
                }

                EndPoint.PutAudioSample(pcmBytes);

                if (_samplesReceivedCount <= 3)
                {
                    _log.Info($"Sample #{_samplesReceivedCount} sent to sink successfully");
                }
            }
            catch (Exception ex)
            {
                if (_samplesReceivedCount <= 5)
                {
                    _log.Error($"Error processing audio sample #{_samplesReceivedCount}: {ex.Message}");
                }
            }
        }

        private int _samplesReceivedCount = 0;


        public void AudioSource_OnAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            byte[] pcmBytes = sample.SelectMany(BitConverter.GetBytes).ToArray();
            EndPoint.PutAudioSample(pcmBytes);
        }

        // Playback control helpers for DLL consumers
        public async Task StartPlaybackAsync()
        {
            if (!IsAvailable || EndPoint == null) return;
            try
            {
                await EndPoint.StartAudioSink();
                PlaybackActive = true;
                _sinkStarted = true;
                OnPlaybackActiveChanged?.Invoke(true);
                _log.Debug("Playback started");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to start playback: {ex.Message}");
            }
        }

        public async Task StopPlaybackAsync()
        {
            if (!IsAvailable || EndPoint == null) return;
            try
            {
                await EndPoint.CloseAudioSink();
                PlaybackActive = false;
                _sinkStarted = false;
                OnPlaybackActiveChanged?.Invoke(false);
                _log.Debug("Playback stopped");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to stop playback: {ex.Message}");
            }
        }

        // Recording control helpers
        public void StartRecording()
        {
            if (!IsAvailable || Source == null) return;
            try
            {
                Source.StartAudio();
                RecordingActive = true;
                _recordingStarted = true;
                OnRecordingActiveChanged?.Invoke(true);
                _log.Debug("Recording started");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to start recording: {ex.Message}");
            }
        }

        public void StopRecording()
        {
            if (!IsAvailable || Source == null) { return; }
            try
            {
                // Attempt to stop the source using known method names via reflection
                StopSourceSafely(Source);
                RecordingActive = false;
                _recordingStarted = false;
                OnRecordingActiveChanged?.Invoke(false);
                _log.Debug("Recording stopped");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to stop recording: {ex.Message}");
            }
        }

        // Replace playback device at runtime (recreates endpoint)
        public void SetPlaybackDevice(string deviceName)
        {
            if (!IsAvailable) return;
            try
            {
                // Handle default device names
                if (deviceName == "Default Speakers" || deviceName == "Default Microphone")
                {
                    deviceName = null;
                }
                // Dispose existing endpoint if present
                try { EndPoint?.CloseAudioSink().Wait(2000); } catch { }
                EndPoint = new SDL3AudioEndPoint(deviceName, _audioEncoder);
                // Set the audio sink format as done in the constructor
                var fmt = new AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                TrySetEndpointVolume(_speakerLevel);
                // Restart sink if it was active
                if (_sinkStarted) { _ = StartPlaybackAsync(); }
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
                try { StopSourceSafely(Source); } catch { }
                Source = new SDL3AudioSource(deviceName, _audioEncoder);
                // Restart recording if it was active
                if (_recordingStarted) StartRecording();
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to set recording device: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensure the playback endpoint exists. If SDL initialized successfully but the endpoint
        /// was not created (null), attempt to create it using the current PlaybackDeviceName.
        /// Returns true if an endpoint is available after the call.
        /// </summary>
        public bool EnsureEndpoint()
        {
            if (!IsAvailable)
            {
                _log.Warn("Cannot ensure endpoint: SDL3 not available");
                return false;
            }

            if (EndPoint != null) return true;

            try
            {
                EndPoint = new SDL3AudioEndPoint(PlaybackDevice.name, _audioEncoder);
                // Use stereo sink so decoded opus stereo audio is rendered correctly
                var fmt = new AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                // Set volume
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

        private void StopSourceSafely(object source)
        {
            if (source == null) return;
            var t = source.GetType();
            // Try StopAudio
            var mi = t.GetMethod("StopAudio", Type.EmptyTypes);
            if (mi != null)
            {
                mi.Invoke(source, null);
                return;
            }

            // Try Stop
            mi = t.GetMethod("Stop", Type.EmptyTypes);
            if (mi != null)
            {
                mi.Invoke(source, null);
                return;
            }

            // Try Close
            mi = t.GetMethod("Close", Type.EmptyTypes);
            if (mi != null)
            {
                mi.Invoke(source, null);
                return;
            }

            // Fallback to Dispose if available
            if (source is IDisposable d)
            {
                d.Dispose();
            }
        }

        // Try to set playback volume on the SDL endpoint via reflection if method/property exists
        private void TrySetEndpointVolume(float normalizedVolume)
        {
            if (EndPoint == null) return;

            try
            {
                var epType = EndPoint.GetType();

                // Attempt to set via method first, matching common naming patterns
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

                // If method invocation failed, try setting via property if available
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

        private bool _sinkStarted = false;
        private bool _recordingStarted = false;
    }
}
