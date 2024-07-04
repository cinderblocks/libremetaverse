/*
 * Copyright (c) 2006-2016, openmetaverse.co
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

using OggVorbisEncoder;
using System;
using System.IO;

namespace OpenMetaverse.Assets
{
    /// <summary>
    /// Represents a Sound Asset
    /// </summary>
    public class AssetSound : Asset
    {
        /// <summary>Override the base classes AssetType</summary>
        public override AssetType AssetType { get { return AssetType.Sound; } }

        /// <summary>Initializes a new instance of an AssetSound object</summary>
        public AssetSound() { }

        /// <summary>Initializes a new instance of an AssetSound object with parameters</summary>
        /// <param name="assetID">A unique <see cref="UUID"/> specific to this asset</param>
        /// <param name="assetData">A byte array containing the raw asset data</param>
        public AssetSound(UUID assetID, byte[] assetData)
            : base(assetID, assetData)
        {
            if ((assetID != UUID.Zero) && (assetData.Length > 0))
            {
                encodedAudio = true;
            }
        }

        protected bool encodedAudio = false;

        /// <summary>
        /// Converts a byte data for a wave PCM file @ 44100 to a OGG encoding
        /// </summary>
        public override void Encode()
        {
            if (encodedAudio == false)
            {
                encodedAudio = true;
                AssetData = ConvertRawPCMFile(44100, 1, AssetData, PcmSample.SixteenBit, 44100, 2);
            }
        }

        /// <summary>
        /// its already ogg just play it or convert it yourself
        /// </summary>
        /// <returns>true</returns>
        public override bool Decode() { return true; }


        private static byte[] ConvertRawPCMFile(int outputSampleRate, int outputChannels, byte[] pcmSamples, PcmSample pcmSampleSize, int pcmSampleRate, int pcmChannels)
        {
            int numPcmSamples = (pcmSamples.Length / (int)pcmSampleSize / pcmChannels);
            float pcmDuraton = numPcmSamples / (float)pcmSampleRate;

            int numOutputSamples = (int)(pcmDuraton * outputSampleRate);
            //Ensure that samble buffer is aligned to write chunk size
            numOutputSamples = (numOutputSamples / WRITE_BUFFER_SIZE) * WRITE_BUFFER_SIZE;

            float[][] outSamples = new float[outputChannels][];

            for (int ch = 0; ch < outputChannels; ch++)
            {
                outSamples[ch] = new float[numOutputSamples];
            }

            for (int sampleNumber = 0; sampleNumber < numOutputSamples; sampleNumber++)
            {
                float rawSample = 0.0f;

                for (int ch = 0; ch < outputChannels; ch++)
                {
                    int sampleIndex = (sampleNumber * pcmChannels) * (int)pcmSampleSize;

                    if (ch < pcmChannels) sampleIndex += (ch * (int)pcmSampleSize);

                    switch (pcmSampleSize)
                    {
                        case PcmSample.EightBit:
                            rawSample = ByteToSample(pcmSamples[sampleIndex]);
                            break;
                        case PcmSample.SixteenBit:
                            rawSample = ShortToSample((short)(pcmSamples[sampleIndex + 1] << 8 | pcmSamples[sampleIndex]));
                            break;
                    }

                    outSamples[ch][sampleNumber] = rawSample;
                }
            }

            return GenerateFile(outSamples, outputSampleRate, outputChannels);
        }

        private static float ByteToSample(short pcmValue)
        {
            return pcmValue / 128f;
        }

        private static float ShortToSample(short pcmValue)
        {
            return pcmValue / 32768f;
        }

        private const int WRITE_BUFFER_SIZE = 512;

        private static byte[] GenerateFile(float[][] floatSamples, int sampleRate, int channels)
        {
            MemoryStream outputData = new MemoryStream();

            // Stores all the static vorbis bitstream settings
            var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, 0.5f);

            // set up our packet->stream encoder
            var serial = new Random().Next();
            var oggStream = new OggStream(serial);

            // =========================================================
            // HEADER
            // =========================================================
            // Vorbis streams begin with three headers; the initial header (with
            // most of the codec setup parameters) which is mandated by the Ogg
            // bitstream spec.  The second header holds any comment fields.  The
            // third header holds the bitstream codebook.

            var comments = new Comments();
            comments.AddTag("ARTIST", "TEST");

            var infoPacket = HeaderPacketBuilder.BuildInfoPacket(info);
            var commentsPacket = HeaderPacketBuilder.BuildCommentsPacket(comments);
            var booksPacket = HeaderPacketBuilder.BuildBooksPacket(info);

            oggStream.PacketIn(infoPacket);
            oggStream.PacketIn(commentsPacket);
            oggStream.PacketIn(booksPacket);

            // Flush to force audio data onto its own page per the spec
            FlushPages(oggStream, outputData, true);

            // =========================================================
            // BODY (Audio Data)
            // =========================================================
            var processingState = ProcessingState.Create(info);

            for (int readIndex = 0; readIndex <= floatSamples[0].Length; readIndex += WRITE_BUFFER_SIZE)
            {
                if (readIndex == floatSamples[0].Length)
                {
                    processingState.WriteEndOfStream();
                }
                else
                {
                    processingState.WriteData(floatSamples, WRITE_BUFFER_SIZE, readIndex);
                }

                while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
                {
                    oggStream.PacketIn(packet);

                    FlushPages(oggStream, outputData, false);
                }
            }

            FlushPages(oggStream, outputData, true);

            return outputData.ToArray();
        }

        private static void FlushPages(OggStream oggStream, Stream output, bool force)
        {
            while (oggStream.PageOut(out OggPage page, force))
            {
                output.Write(page.Header, 0, page.Header.Length);
                output.Write(page.Body, 0, page.Body.Length);
            }
        }

        enum PcmSample : int
        {
            EightBit = 1,
            SixteenBit = 2
        }
    }
}
