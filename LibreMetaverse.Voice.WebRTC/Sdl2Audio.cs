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

using System;
using System.Linq;
using LibreMetaverse.Voice.WebRTC;
using OpenMetaverse;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.SDL2;

namespace LibreMetaverse
{
    internal class Sdl2Audio : IDisposable
    {
        public SDL2AudioEndPoint EndPoint { get; }
        private readonly OpusAudioEncoder _audioEncoder = new OpusAudioEncoder();

        public Sdl2Audio()
        {
            SDL2Helper.InitSDL();
            var outDeviceIdx = DeviceSelection(false);
            EndPoint = new SDL2AudioEndPoint(GetDeviceName(outDeviceIdx, false), _audioEncoder);
            EndPoint.SetAudioSinkFormat(OpusAudioEncoder.MEDIA_FORMAT_OPUS);
            EndPoint.OnAudioSinkError += (err) =>
            {
                Logger.Log($"SDL Audio sink error: {err}", Helpers.LogLevel.Warning);
            };
            EndPoint.StartAudioSink();
        }

        public void Dispose()
        {
            SDL2Helper.QuitSDL();
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

            Logger.Log($"SDL Audio - Found {sdlDevices.Count}, but using {sdlDevices[0]}", Helpers.LogLevel.Info);
            
            return 0;
        }

        private static string GetDeviceName(int index, bool recordingDevice)
        {
            return recordingDevice 
                ? SDL2Helper.GetAudioRecordingDevice(index) 
                : SDL2Helper.GetAudioPlaybackDevice(index);
        }

        public void AudioSource_OnAudioSourceEncodedSample(uint durationRtpUnits, byte[] sample)
        {
            // Decode sample
            var pcmSample = _audioEncoder.DecodeAudio(sample, OpusAudioEncoder.MEDIA_FORMAT_OPUS);
            var pcmBytes = pcmSample.SelectMany(BitConverter.GetBytes).ToArray();
            EndPoint.GotAudioSample(pcmBytes);
        }

        public void AudioSource_OnAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            byte[] pcmBytes = sample.SelectMany(BitConverter.GetBytes).ToArray();
            EndPoint.GotAudioSample(pcmBytes);
        }
    }
}
