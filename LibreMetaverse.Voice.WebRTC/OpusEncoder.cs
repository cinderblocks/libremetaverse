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

using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using Concentus;
using Concentus.Enums;
using Microsoft.Extensions.Logging;

namespace LibreMetaverse.Voice.WebRTC
{
    internal class OpusAudioEncoder : IAudioEncoder
    {
        // Chrome use in SDP two audio channels, but the audio itself contains only one channel,
        // so we must pass it as 2 channels in SDP but create a decoder/encoder with only one channel
        public static readonly AudioFormat MEDIA_FORMAT_OPUS = new AudioFormat(111,
            "opus", SAMPLE_RATE, SAMPLE_RATE, 2,
            "minptime=10;useinbandfec=1;stereo=1;sprop-stereo=1;maxplaybackrate=48000;sprop-maxplaybackrate=48000;sprop-maxcapturerate=48000");

        private readonly AudioEncoder _audioEncoder;

        private const int FRAME_SIZE_MILLISECONDS = 20;
        private const int MAX_DECODED_FRAME_SIZE_MULT = 6;
        private const int MAX_PACKET_SIZE = 4000;
        private const int MAX_FRAME_SIZE = MAX_DECODED_FRAME_SIZE_MULT * 960;
        private const int SAMPLE_RATE = 48000;

        private int _channels = 1;
        private short[] _shortBuffer;
        private byte[] _byteBuffer;

        private IOpusEncoder _opusEncoder;
        private IOpusDecoder _opusDecoder;

        public List<AudioFormat> SupportedFormats { get; }

        public OpusAudioEncoder()
        {
            _audioEncoder = new AudioEncoder();

            // Add OPUS in the list of AudioFormat
            SupportedFormats = new List<AudioFormat> { MEDIA_FORMAT_OPUS };

            // Add also list available in the AudioEncoder available in SIPSorcery
            SupportedFormats.AddRange(_audioEncoder.SupportedFormats);
        }

        public short[] DecodeAudio(byte[] encodedSample, AudioFormat format)
        {
            if (format.FormatName != "opus") { return _audioEncoder.DecodeAudio(encodedSample, format); }

            if (_opusDecoder == null)
            {
                _opusDecoder = OpusCodecFactory.CreateDecoder(SAMPLE_RATE, _channels);
                _shortBuffer = new short[MAX_FRAME_SIZE * _channels];
            }

            try
            {
                var numSamplesDecoded = _opusDecoder.Decode(
                        encodedSample.AsSpan(), _shortBuffer.AsSpan(), GetFrameSize());

                if (numSamplesDecoded >= 1)
                {
                    var buffer = new short[numSamplesDecoded];
                    Array.Copy(_shortBuffer, 0, 
                        buffer, 0, numSamplesDecoded);

                    return buffer;
                }
            }
            catch
            {

            }
            return Array.Empty<short>();
        }

        public byte[] EncodeAudio(short[] in_pcm, AudioFormat format)
        {
            if (format.FormatName != "opus") { return _audioEncoder.EncodeAudio(in_pcm, format); }

            if (_opusEncoder == null)
            {
                _opusEncoder = OpusCodecFactory.CreateEncoder(SAMPLE_RATE, _channels, OpusApplication.OPUS_APPLICATION_VOIP);
                _opusEncoder.ForceMode = OpusMode.MODE_AUTO;
                _byteBuffer = new byte[MAX_PACKET_SIZE];
            }

            try
            {
                var size = _opusEncoder.Encode(
                    in_pcm.AsSpan(), GetFrameSize(), _byteBuffer.AsSpan(), MAX_PACKET_SIZE);

                if (size > 1)
                {
                    var result = new byte[size];
                    Array.Copy(_byteBuffer, 0, 
                        result, 0, size);

                    return result;
                }
            }
            catch
            {

            }
            return Array.Empty<byte>();
        }

        public int GetFrameSize()
        {
            return 960;
            //return (int)(SAMPLE_RATE * FRAME_SIZE_MILLISECONDS / 1000);
        }
    }
}
