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

using LibreMetaverse.Voice.WebRTC;
using OpenMetaverse;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.SDL2;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    public class Sdl2Audio : IDisposable
    {
        public SDL2AudioEndPoint EndPoint { get; private set; } // For Playback (Sink)
        public SDL2AudioSource Source { get; private set; }     // For Microphone (Source)
        private readonly OpusAudioEncoder _audioEncoder = new OpusAudioEncoder();
        public bool IsAvailable { get; private set; } = false;
        // Exposed state and events for consumers
        public event Action<bool> OnPlaybackActiveChanged;
        public event Action<bool> OnRecordingActiveChanged;
        public event Action<uint, byte[]> OnAudioSourceEncodedSample;

        public bool PlaybackActive { get; private set; } = false;
        public bool RecordingActive { get; private set; } = false;

        public string PlaybackDeviceName { get; private set; } = string.Empty;
        public string RecordingDeviceName { get; private set; } = string.Empty;

        // Convenience properties expected by some consumers
        private float _spkrLevel = 1.0f; // 0.0 - 1.0
        public float SpkrLevel
        {
            get => _spkrLevel;
            set
            {
                // clamp
                var v = Math.Max(0.0f, Math.Min(1.0f, value));
                _spkrLevel = v;
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
        public IEnumerable<string> GetPlaybackDeviceNames()
        {
            try { return SDL2Helper.GetAudioPlaybackDevices(); }
            catch { return Enumerable.Empty<string>(); }
        }

        // Returns recording device names from SDL helper
        public IEnumerable<string> GetRecordingDeviceNames()
        {
            try { return SDL2Helper.GetAudioRecordingDevices(); }
            catch { return Enumerable.Empty<string>(); }
        }

        public Sdl2Audio()
        {
#if NET8_0_OR_GREATER
            // Try to map DllImport requests for "SDL2" to the SDL3 native library if available.
            try
            {
                string[] candidates = new[] { "SDL3", "SDL3.dll", "libSDL3.so", "libSDL3.dylib" };
                IntPtr sdl3Handle = IntPtr.Zero;
                foreach (var name in candidates)
                {
                    if (NativeLibrary.TryLoad(name, out sdl3Handle))
                    {
                        // Resolver that maps requests for "SDL2" to the found native handle
                        DllImportResolver resolver = (libraryName, assembly, searchPath) =>
                        {
                            if (string.Equals(libraryName, "SDL2", StringComparison.OrdinalIgnoreCase))
                            {
                                return sdl3Handle;
                            }
                            return IntPtr.Zero;
                        };

                        // Apply resolver to this assembly and all currently loaded assemblies
                        void ApplyResolverToAssembly(Assembly asm)
                        {
                            try
                            {
                                NativeLibrary.SetDllImportResolver(asm, resolver);
                            }
                            catch { }
                        }

                        var thisAssembly = Assembly.GetExecutingAssembly();
                        ApplyResolverToAssembly(thisAssembly);

                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            ApplyResolverToAssembly(asm);
                        }

                        // Also apply resolver to any assemblies that load after this point
                        AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
                        {
                            try { ApplyResolverToAssembly(args.LoadedAssembly); } catch { }
                        };

                        Logger.Log($"Mapped SDL2 DllImport to native library '{name}'", Helpers.LogLevel.Info);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore mapping failures; we'll fall back to trying SDL2 normally.
                Logger.Log($"SDL3 mapping attempt failed: {ex.Message}", Helpers.LogLevel.Info);
            }
#endif

            try
            {
                SDL2Helper.InitSDL();
                Logger.Log("✅ SDL2 initialized successfully", Helpers.LogLevel.Info);

                var playbackDeviceIndex = DeviceSelection(false);
                var recordingDeviceIndex = DeviceSelection(true);
                var playbackDeviceName = GetDeviceName(playbackDeviceIndex, false);
                var recordingDeviceName = GetDeviceName(recordingDeviceIndex, true);

                PlaybackDeviceName = playbackDeviceName ?? string.Empty;
                RecordingDeviceName = recordingDeviceName ?? string.Empty;

                Logger.Log($"🔊 Playback device: '{playbackDeviceName}'", Helpers.LogLevel.Info);
                Logger.Log($"🎤 Recording device: '{recordingDeviceName}'", Helpers.LogLevel.Info);

                EndPoint = new SDL2AudioEndPoint(playbackDeviceName, _audioEncoder);
                Logger.Log("✅ SDL2AudioEndPoint created", Helpers.LogLevel.Info);

                // Use stereo sink to match incoming Opus stereo (opus/48000/2)
                var fmt = new SIPSorceryMedia.Abstractions.AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                Logger.Log($"✅ Audio sink format set: {fmt.Codec}, {fmt.ClockRate} Hz, {fmt.ChannelCount} ch",
                    Helpers.LogLevel.Info);

                // Set initial volume
                TrySetEndpointVolume(_spkrLevel);

                // Initialize recording source
                try
                {
                    Source = new SDL2AudioSource(recordingDeviceName, _audioEncoder);
                    Logger.Log("✅ SDL2AudioSource created", Helpers.LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Log($"⚠️ Failed to create audio source (recording may not work): {ex.Message}", Helpers.LogLevel.Warning);
                    Source = null;
                }

                // Mark as available AFTER successful initialization
                IsAvailable = true;
                Logger.Log("✅ SDL2 audio system ready", Helpers.LogLevel.Info);

                // Automatically start playback to hear audio
                _ = StartPlaybackAsync();
            }
            catch (DllNotFoundException dllEx)
            {
                Logger.Log($"❌ SDL2 DLL not found: {dllEx.Message}", Helpers.LogLevel.Error);
                IsAvailable = false;
                EndPoint = null;
            }
            catch (EntryPointNotFoundException epnEx)
            {
                Logger.Log($"❌ SDL2 entry point missing: {epnEx.Message}", Helpers.LogLevel.Error);
                IsAvailable = false;
                EndPoint = null;
            }
            catch (BadImageFormatException bife)
            {
                // Common on architecture mismatch between native SDL2 and running process
                Logger.Log($"❌ SDL2 initialization failed (bad image / incorrect native format): {bife.Message}", Helpers.LogLevel.Error);
                Logger.Log("Possible causes: mixing x86/x64 native SDL2 binaries with a process of the opposite bitness.", Helpers.LogLevel.Error);
                Logger.Log("Fixes:", Helpers.LogLevel.Error);
                Logger.Log(" - Ensure your application process matches the SDL2 native DLL architecture (x64 vs x86).", Helpers.LogLevel.Error);
                Logger.Log(" - Place the correct SDL2 native binary in runtimes/<rid>/native or next to the executable.", Helpers.LogLevel.Error);
                Logger.Log(" - Or change your project PlatformTarget to match the native DLL (e.g. x64).", Helpers.LogLevel.Error);
                IsAvailable = false;
                EndPoint = null;
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ SDL2 initialization failed: {ex.Message}\n{ex.StackTrace}", Helpers.LogLevel.Error);
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

                try { (Source as IDisposable)?.Dispose(); } catch { }
                try { (EndPoint as IDisposable)?.Dispose(); } catch { }

                if (IsAvailable)
                {
                    SDL2Helper.QuitSDL();
                }
            }
            catch { }
        }

        private static int DeviceSelection(bool recordingDevice)
        {
            var sdlDevices = recordingDevice
                ? SDL2Helper.GetAudioRecordingDevices()
                : SDL2Helper.GetAudioPlaybackDevices();

            // Quit if no Audio devices found
            if (sdlDevices.Count < 1)
            {
                Logger.Log("SDL Audio - Could not find an audio device.", Helpers.LogLevel.Warning);
                return -1;
            }

            // Prefer using the system default device. SDL typically accepts a null/empty device name to mean the default
            // Return -1 to indicate "use default" and let GetDeviceName translate that to null.
            Logger.Log($"SDL Audio - Found {sdlDevices.Count} devices, using system default device.", Helpers.LogLevel.Info);
            return -1;
        }

        private static string GetDeviceName(int index, bool recordingDevice)
        {
            if (index < 0) return null; // signal to SDL2AudioEndPoint to use the system default device

            return recordingDevice
                ? SDL2Helper.GetAudioRecordingDevice(index)
                : SDL2Helper.GetAudioPlaybackDevice(index);
        }

        public void AudioSource_OnAudioSourceEncodedSample(uint durationRtpUnits, byte[] sample)
        {
            _samplesReceivedCount++;

            if (_samplesReceivedCount == 1)
            {
             //   Logger.Log($"🎉 FIRST audio sample received! IsAvailable={IsAvailable}, EndPoint={(EndPoint != null ? "exists" : "null")}, PlaybackActive={PlaybackActive}", Helpers.LogLevel.Info);
            }

            if (_samplesReceivedCount % 50 == 1 && _samplesReceivedCount > 1) // Log every 50th sample to avoid spam
            {
               // Logger.Log($"📊 Audio samples received: {_samplesReceivedCount}", Helpers.LogLevel.Info);
            }

            if (!IsAvailable)
            {
                Logger.Log("❌ SDL2 audio not available", Helpers.LogLevel.Warning);
                return;
            }

            if (EndPoint == null)
            {
                Logger.Log("❌ Audio sink EndPoint is null — SDL2 not initialized or unavailable.",
                    Helpers.LogLevel.Warning);
                return;
            }

            if (!PlaybackActive)
            {
                if (_samplesReceivedCount <= 5)
                {
                    Logger.Log("⚠️ Playback not active, attempting to start...", Helpers.LogLevel.Warning);
                    _ = StartPlaybackAsync();
                }
                return;
            }

            if (sample == null || sample.Length == 0)
            {
                Logger.Log("⚠️ Received empty audio sample.", Helpers.LogLevel.Warning);
                return;
            }

            if (_samplesReceivedCount <= 3) // Log first few samples in detail
            {
                Logger.Log($"🎵 Decoding Opus sample #{_samplesReceivedCount}: {sample.Length} bytes, duration: {durationRtpUnits} RTP units", Helpers.LogLevel.Info);
            }

            try
            {
                var pcmSample = _audioEncoder.DecodeAudio(sample, OpusAudioEncoder.MEDIA_FORMAT_OPUS);

                if (pcmSample == null || pcmSample.Length == 0)
                {
                    if (_samplesReceivedCount <= 5)
                    {
                        Logger.Log($"❌ Decoded PCM sample is null or empty for sample #{_samplesReceivedCount}", Helpers.LogLevel.Warning);
                    }
                    return;
                }

                if (_samplesReceivedCount <= 3)
                {
                    Logger.Log($"✅ Decoded to PCM: {pcmSample.Length} samples", Helpers.LogLevel.Info);
                }

                var pcmBytes = pcmSample.SelectMany(BitConverter.GetBytes).ToArray();

                if (_samplesReceivedCount <= 3)
                {
                    Logger.Log($"🔊 Sending {pcmBytes.Length} bytes to sink (sample #{_samplesReceivedCount})", Helpers.LogLevel.Info);
                }

                EndPoint.GotAudioSample(pcmBytes);

                if (_samplesReceivedCount <= 3)
                {
                    Logger.Log($"✅ Sample #{_samplesReceivedCount} sent to sink successfully", Helpers.LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                if (_samplesReceivedCount <= 5)
                {
                    Logger.Log($"❌ Error processing audio sample #{_samplesReceivedCount}: {ex.Message}", Helpers.LogLevel.Error);
                }
            }
        }

        private int _samplesReceivedCount = 0;


        public void AudioSource_OnAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            byte[] pcmBytes = sample.SelectMany(BitConverter.GetBytes).ToArray();
            EndPoint.GotAudioSample(pcmBytes);
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
                Logger.Log("✅ Playback started", Helpers.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start playback: {ex.Message}", Helpers.LogLevel.Warning);
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
                Logger.Log("✅ Playback stopped", Helpers.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to stop playback: {ex.Message}", Helpers.LogLevel.Warning);
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
                Logger.Log("✅ Recording started", Helpers.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start recording: {ex.Message}", Helpers.LogLevel.Warning);
            }
        }

        public void StopRecording()
        {
            if (!IsAvailable || Source == null) return;
            try
            {
                // Attempt to stop the source using known method names via reflection
                StopSourceSafely(Source);
                RecordingActive = false;
                _recordingStarted = false;
                OnRecordingActiveChanged?.Invoke(false);
                Logger.Log("✅ Recording stopped", Helpers.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to stop recording: {ex.Message}", Helpers.LogLevel.Warning);
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
                EndPoint = new SDL2AudioEndPoint(deviceName, _audioEncoder);
                PlaybackDeviceName = deviceName ?? string.Empty;
                // Set the audio sink format as done in the constructor
                var fmt = new SIPSorceryMedia.Abstractions.AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                TrySetEndpointVolume(_spkrLevel);
                // Restart sink if it was active
                if (_sinkStarted) { _ = StartPlaybackAsync(); }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to set playback device: {ex.Message}", Helpers.LogLevel.Warning);
            }
        }

        public void SetRecordingDevice(string deviceName)
        {
            if (!IsAvailable) return;
            try
            {
                // Handle default device names
                if (deviceName == "Default Speakers" || deviceName == "Default Microphone")
                {
                    deviceName = null;
                }
                try { StopSourceSafely(Source); } catch { }
                // Dispose old source if it implements IDisposable
                try { (Source as IDisposable)?.Dispose(); } catch { }
                Source = new SDL2AudioSource(deviceName, _audioEncoder);
                RecordingDeviceName = deviceName ?? string.Empty;
                // Restart recording if it was active
                if (_recordingStarted) StartRecording();
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to set recording device: {ex.Message}", Helpers.LogLevel.Warning);
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
                Logger.Log("Cannot ensure endpoint: SDL not available", Helpers.LogLevel.Warning);
                return false;
            }

            if (EndPoint != null) return true;

            try
            {
                var deviceName = string.IsNullOrEmpty(PlaybackDeviceName) ? null : PlaybackDeviceName;
                EndPoint = new SDL2AudioEndPoint(deviceName, _audioEncoder);
                // Use stereo sink so decoded opus stereo audio is rendered correctly
                var fmt = new SIPSorceryMedia.Abstractions.AudioFormat(AudioCodecsEnum.L16, 96, 48000, 2);
                EndPoint.SetAudioSinkFormat(fmt);
                // Set volume
                TrySetEndpointVolume(_spkrLevel);
                Logger.Log("✅ SDL2AudioEndPoint created by EnsureEndpoint", Helpers.LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create SDL2AudioEndPoint in EnsureEndpoint: {ex.Message}", Helpers.LogLevel.Warning);
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
                Logger.Log($"Failed to set endpoint volume: {ex.Message}", Helpers.LogLevel.Debug);
            }
        }

        private bool _sinkStarted = false;
        private bool _recordingStarted = false;
    }
}
