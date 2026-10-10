/*
 * Copyright (c) 2026, Sjofn LLC.
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
using System.Collections.Generic;
using LibreMetaverse.Rendering;

namespace LibreMetaverse.ImportExport
{
    /// <summary>A material of an export: a face's color and the image file it is textured with, if any</summary>
    internal sealed class ExportMaterial
    {
        public string Name = string.Empty;
        public Color4 Color;

        /// <summary>The name of the image file written beside the model, or null</summary>
        public string? TextureFile;
    }

    /// <summary>
    /// Works out the materials of an export for the formats that refer to their images by file name: faces
    /// with the same color and texture share a material, and each texture is asked for once and gets a file
    /// named after its asset ID, never after anything a resident chose.
    /// </summary>
    internal sealed class ExportMaterials
    {
        private struct Key : IEquatable<Key>
        {
            public Color4 Color;
            public string? Texture;

            public bool Equals(Key other) => Color.Equals(other.Color) && string.Equals(Texture, other.Texture, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => Color.GetHashCode() * 31 + (Texture?.GetHashCode() ?? 0);
        }

        private readonly Func<UUID, ExportImage?>? _provider;
        private readonly Dictionary<UUID, string?> _files = new Dictionary<UUID, string?>();
        private readonly Dictionary<Key, ExportMaterial> _byKey = new Dictionary<Key, ExportMaterial>();

        public List<ExportMaterial> Materials { get; } = new List<ExportMaterial>();

        /// <summary>The image files to write beside the model</summary>
        public List<ExportedFile> Images { get; } = new List<ExportedFile>();

        public ExportMaterials(Func<UUID, ExportImage?>? imageProvider)
        {
            _provider = imageProvider;
        }

        public ExportMaterial For(Face face)
        {
            var entry = face.TextureFace;
            var color = entry != null ? entry.RGBA : Color4.White;
            var file = entry != null ? FileFor(entry.TextureID) : null;

            var key = new Key { Color = color, Texture = file };
            if (_byKey.TryGetValue(key, out var existing)) return existing;

            var material = new ExportMaterial { Name = "material" + Materials.Count, Color = color, TextureFile = file };
            Materials.Add(material);
            _byKey[key] = material;
            return material;
        }

        private string? FileFor(UUID id)
        {
            if (id == UUID.Zero || id == Primitive.TextureEntry.WHITE_TEXTURE || _provider == null) return null;
            if (_files.TryGetValue(id, out var existing)) return existing;

            string? result = null;
            try
            {
                var image = _provider(id);
                if (image != null && image.Data.Length > 0)
                {
                    string? extension = ExtensionFor(image.MimeType);
                    if (extension == null)
                    {
                        Logger.Warn($"Not exporting texture {id}: {image.MimeType} is not PNG or JPEG");
                    }
                    else
                    {
                        result = id + extension;
                        Images.Add(new ExportedFile(result, image.Data));
                    }
                }
            }
            catch (Exception ex)
            {
                // A texture that cannot be had costs the face its texture, not the export
                Logger.Warn($"Not exporting texture {id}: {ex.Message}");
            }

            _files[id] = result;
            return result;
        }

        private static string? ExtensionFor(string mimeType)
        {
            switch (mimeType.Trim().ToLowerInvariant())
            {
                case "image/png": return ".png";
                case "image/jpeg":
                case "image/jpg": return ".jpg";
                default: return null;
            }
        }
    }
}
