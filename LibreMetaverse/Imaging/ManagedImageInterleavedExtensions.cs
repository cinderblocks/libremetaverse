/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2024-2026, Sjofn LLC.
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

using CoreJ2K.Util;

namespace LibreMetaverse.Imaging
{
    /// <summary>
    /// Bridges CoreJ2K's <see cref="InterleavedImage"/> (the legacy/sample-level-access decode
    /// path) into <see cref="ManagedImage"/>. Kept here rather than on <see cref="ManagedImage"/>
    /// itself since <see cref="ManagedImage"/> lives in LibreMetaverse.Imaging.Abstractions, which
    /// has no CoreJ2K dependency.
    /// </summary>
    public static class ManagedImageInterleavedExtensions
    {
        /// <summary>
        /// Converts an <see cref="InterleavedImage"/> to a <see cref="ManagedImage"/>.
        /// Currently only supporting 8-bit channels.
        /// </summary>
        /// <param name="image">Input <see cref="InterleavedImage"/></param>
        public static ManagedImage ToManagedImage(this InterleavedImage image)
        {
            var result = new ManagedImage(image.Width, image.Height, 0);

            var pixelCount = result.Width * result.Height;
            var numComp = image.NumberOfComponents;
            switch (numComp)
            {
                case 1:
                    result.Channels = ManagedImage.ImageChannels.Gray;
                    result.Red = new byte[pixelCount];
                    image.ToComponentBytes(0, result.Red);
                    break;
                case 2:
                    result.Channels = ManagedImage.ImageChannels.Gray | ManagedImage.ImageChannels.Alpha;
                    result.Red = new byte[pixelCount];
                    result.Alpha = new byte[pixelCount];
                    image.ToComponentBytes(0, result.Red);
                    image.ToComponentBytes(1, result.Alpha);
                    break;
                case 3:
                    result.Channels = ManagedImage.ImageChannels.Color;
                    result.Red = new byte[pixelCount];
                    result.Green = new byte[pixelCount];
                    result.Blue = new byte[pixelCount];
                    image.ToComponentBytes(0, result.Red);
                    image.ToComponentBytes(1, result.Green);
                    image.ToComponentBytes(2, result.Blue);
                    break;
                case 4:
                    result.Channels = ManagedImage.ImageChannels.Alpha | ManagedImage.ImageChannels.Color;
                    result.Red = new byte[pixelCount];
                    result.Green = new byte[pixelCount];
                    result.Blue = new byte[pixelCount];
                    result.Alpha = new byte[pixelCount];
                    image.ToComponentBytes(0, result.Red);
                    image.ToComponentBytes(1, result.Green);
                    image.ToComponentBytes(2, result.Blue);
                    image.ToComponentBytes(3, result.Alpha);
                    break;
                case 5:
                    result.Channels = ManagedImage.ImageChannels.Alpha | ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Bump;
                    result.Red = new byte[pixelCount];
                    result.Green = new byte[pixelCount];
                    result.Blue = new byte[pixelCount];
                    result.Bump = new byte[pixelCount];
                    result.Alpha = new byte[pixelCount];
                    image.ToComponentBytes(0, result.Red);
                    image.ToComponentBytes(1, result.Green);
                    image.ToComponentBytes(2, result.Blue);
                    image.ToComponentBytes(3, result.Bump);
                    image.ToComponentBytes(4, result.Alpha);
                    break;
            }

            return result;
        }
    }
}
