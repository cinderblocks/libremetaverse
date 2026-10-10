/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2026, Sjofn LLC.
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
using System.IO;
using System.Runtime.InteropServices;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using LibreMetaverse.Imaging;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Decodes, resizes and J2C-encodes the textures a model refers to, so every
    /// <see cref="IModelLoader"/> hands <see cref="ModelUploader"/> the same kind of texture data.
    /// </summary>
    internal static class ModelTextureLoader
    {
        static ModelTextureLoader()
        {
            // The J2C encode requires ManagedImage to be registered with CoreJ2K's ImageFactory.
            // AssetTexture registers it too, but nothing guarantees that type has been touched yet
            // in a process that only loads models (e.g. a standalone model-upload tool), so register
            // it here as well -- redundant, not conflicting, if AssetTexture already has.
            ImageFactory.Register(new ManagedImageCreator());
        }

        /// <summary>
        /// True if <paramref name="path"/> is inside the directory of <paramref name="modelFileName"/>
        /// or one of its subdirectories.
        /// </summary>
        internal static bool IsInModelDirectory(string modelFileName, string path)
        {
            try
            {
                string root = Path.GetDirectoryName(Path.GetFullPath(modelFileName)) ?? string.Empty;
                if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    && !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    root += Path.DirectorySeparatorChar;
                }

                // GetFullPath collapses any ".." segments, so they cannot be used to climb out
                string full = Path.GetFullPath(path);
                var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                return full.StartsWith(root, comparison);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads the texture file at <paramref name="fname"/> into <paramref name="material"/>. Failures are
        /// logged and leave the material without texture data.
        /// </summary>
        internal static void LoadFile(string fname, string textureName, ModelMaterial material, ITextureCodec? textureCodec)
        {
            try
            {
                string ext = Path.GetExtension(textureName).ToLowerInvariant();

                if (ext == ".jp2" || ext == ".j2c")
                {
                    material.TextureData = File.ReadAllBytes(fname);
                    return;
                }

                ManagedImage image;
                if (ext == ".tga" || ext == ".targa")
                {
                    image = Targa.DecodeToManagedImage(fname);
                }
                else
                {
                    var codec = RequireCodec(fname, textureCodec);
                    using (var fs = File.OpenRead(fname))
                    {
                        image = codec.Decode(fs);
                    }
                }

                Encode(image, material);
                Logger.Info($"Successfully encoded {fname}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed loading {fname}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads texture data that was embedded in the model file into <paramref name="material"/>.
        /// Failures are logged and leave the material without texture data.
        /// </summary>
        /// <param name="data">The compressed image</param>
        /// <param name="extension">Lower case file extension (with the dot) that names the image format</param>
        /// <param name="description">What to call the image in log messages</param>
        internal static void LoadBytes(byte[] data, string extension, string description,
            ModelMaterial material, ITextureCodec? textureCodec)
        {
            try
            {
                if (extension == ".jp2" || extension == ".j2c")
                {
                    material.TextureData = data;
                    return;
                }

                ManagedImage image;
                using (var ms = new MemoryStream(data, writable: false))
                {
                    if (extension == ".tga" || extension == ".targa")
                    {
                        image = Targa.DecodeToManagedImage(ms);
                    }
                    else
                    {
                        image = RequireCodec(description, textureCodec).Decode(ms);
                    }
                }

                Encode(image, material);
                Logger.Info($"Successfully encoded {description}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed loading {description}: {ex.Message}");
            }
        }

        private static ITextureCodec RequireCodec(string what, ITextureCodec? textureCodec)
        {
            return textureCodec ?? throw new InvalidOperationException(
                $"No ITextureCodec configured to decode '{what}'. Reference " +
                "LibreMetaverse.Imaging.Skia (or provide your own ITextureCodec " +
                "implementation) and pass it to the model loader's constructor.");
        }

        private static void Encode(ManagedImage image, ModelMaterial material)
        {
            int width = image.Width;
            int height = image.Height;

            // Handle resizing to prevent excessively large images and irregular dimensions
            if (!IsPowerOfTwo((uint)width) || !IsPowerOfTwo((uint)height) || width > 1024 || height > 1024)
            {
                var origWidth = width;
                var origHeight = height;

                width = ClosestPowerOfTwo(width);
                height = ClosestPowerOfTwo(height);

                width = width > 1024 ? 1024 : width;
                height = height > 1024 ? 1024 : height;

                Logger.Info($"Image has irregular dimensions {origWidth}x{origHeight}. Resizing to {width}x{height}");

                image.ResizeBilinear(width, height);
            }

            material.Width = width;
            material.Height = height;
            material.TextureData = CompleteConfigurationPresets.Streaming.WithFileFormat(false).Encode(image);
        }

        private static bool IsPowerOfTwo(uint n)
        {
            return (n & (n - 1)) == 0 && n != 0;
        }

        private static int ClosestPowerOfTwo(int n)
        {
            int res = 1;

            while (res < n)
            {
                res <<= 1;
            }

            return res > 1 ? res / 2 : 1;
        }
    }
}
