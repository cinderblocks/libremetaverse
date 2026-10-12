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
using System.Globalization;
using System.IO;
using System.Xml;

namespace LibreMetaverse.Assets
{
    /// <summary>
    /// What a region archive says about itself in its <c>archive.xml</c> control file: the version of the
    /// archive format, and when and how the archive was made. Everything but the version is optional, because
    /// older archives and other tools leave parts out.
    /// </summary>
    public sealed class ArchiveInfo
    {
        /// <summary>The newest archive format this library reads: 0.8, the layout OpenSim wrote from 2012.</summary>
        public const int SupportedMajorVersion = 0;
        /// <summary>The newest archive format this library reads: 0.8, the layout OpenSim wrote from 2012.</summary>
        public const int SupportedMinorVersion = 8;

        /// <summary>The archive format version this library writes: 0.8.</summary>
        public const int WrittenMajorVersion = 0;
        /// <summary>The archive format version this library writes: 0.8.</summary>
        public const int WrittenMinorVersion = 8;

        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }

        /// <summary>When the archive was made, in UTC. A time of unspecified kind is written as UTC.</summary>
        public DateTime? CreationTime { get; set; }

        /// <summary>An ID that OpenSim gives each archive it makes.</summary>
        public UUID? Id { get; set; }

        public bool IsMegaregion { get; set; }

        /// <summary>The size of the region in meters, as <c>size_in_meters</c> says. A varregion is not 256 by 256.</summary>
        public int? SizeX { get; set; }
        public int? SizeY { get; set; }

        /// <summary>Whether the archive holds the assets of the objects, or only the objects.</summary>
        public bool? AssetsIncluded { get; set; }

        /// <summary>
        /// True if the archive is of a format newer than <see cref="SupportedMajorVersion"/>.<see cref="SupportedMinorVersion"/>.
        /// It is still read, as that format, and what is laid out differently is not found.
        /// </summary>
        public bool IsNewerThanSupported =>
            MajorVersion > SupportedMajorVersion || (MajorVersion == SupportedMajorVersion && MinorVersion > SupportedMinorVersion);

        /// <summary>The version as <c>major.minor</c>.</summary>
        public string VersionString => MajorVersion.ToString(CultureInfo.InvariantCulture) + "." + MinorVersion.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The control file for an archive made now, in the format this library writes.
        /// </summary>
        /// <param name="assetsIncluded">Whether the archive holds assets</param>
        /// <param name="sizeX">Region width in meters</param>
        /// <param name="sizeY">Region length in meters</param>
        public static ArchiveInfo CreateNew(bool assetsIncluded, int sizeX = 256, int sizeY = 256) => new ArchiveInfo
        {
            MajorVersion = WrittenMajorVersion,
            MinorVersion = WrittenMinorVersion,
            CreationTime = DateTime.UtcNow,
            Id = UUID.Random(),
            IsMegaregion = false,
            SizeX = sizeX,
            SizeY = sizeY,
            AssetsIncluded = assetsIncluded
        };

        /// <summary>
        /// Reads a control file. Elements this does not know are ignored and the ones that are there may come in any order.
        /// </summary>
        /// <returns>The information, or null if this is not a control file or does not say what version it is</returns>
        public static ArchiveInfo? FromStream(Stream stream)
        {
            try
            {
                using (var reader = new XmlTextReader(ArchiveXml.OpenText(stream)))
                {
                    reader.MoveToContent();
                    if (reader.NodeType != XmlNodeType.Element || reader.Name != "archive")
                        return null;

                    if (!int.TryParse(reader.GetAttribute("major_version"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int major)
                        || !int.TryParse(reader.GetAttribute("minor_version"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minor))
                        return null;

                    var info = new ArchiveInfo { MajorVersion = major, MinorVersion = minor };

                    if (reader.IsEmptyElement)
                        return info;

                    reader.Read();
                    while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
                    {
                        if (reader.NodeType != XmlNodeType.Element)
                        {
                            reader.Read();
                            continue;
                        }

                        switch (reader.Name)
                        {
                            case "creation_info":
                                ReadChildren(reader, (name, text) =>
                                {
                                    if (name == "datetime" && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds)
                                        && seconds >= 0 && seconds <= uint.MaxValue)
                                        info.CreationTime = DateTime.SpecifyKind(Utils.UnixTimeToDateTime((uint)seconds), DateTimeKind.Utc);
                                    else if (name == "id" && UUID.TryParse(text, out UUID id))
                                        info.Id = id;
                                });
                                break;
                            case "region_info":
                                ReadChildren(reader, (name, text) =>
                                {
                                    if (name == "is_megaregion" && bool.TryParse(text, out bool mega))
                                        info.IsMegaregion = mega;
                                    else if (name == "size_in_meters")
                                    {
                                        var parts = text.Split(',');
                                        if (parts.Length == 2
                                            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                                            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                                        {
                                            info.SizeX = x;
                                            info.SizeY = y;
                                        }
                                    }
                                });
                                break;
                            case "assets_included":
                                if (bool.TryParse(reader.ReadElementContentAsString().Trim(), out bool included))
                                    info.AssetsIncluded = included;
                                break;
                            default:
                                reader.Skip();
                                break;
                        }
                    }

                    return info;
                }
            }
            catch (XmlException)
            {
                return null;
            }
        }

        /// <summary>Seconds since 1970 (UTC). A time of unspecified kind is taken to be UTC, as the Unix time helpers of this library return it.</summary>
        private static long ToUnixSeconds(DateTime time)
        {
            DateTime utc = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static ArchiveInfo? FromBytes(byte[] data) => FromStream(new MemoryStream(data));

        /// <summary>The leaf elements of the current element, as name and text; anything nested is skipped.</summary>
        private static void ReadChildren(XmlTextReader reader, Action<string, string> handle)
        {
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return;
            }

            reader.Read();
            while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                string name = reader.Name;
                if (reader.IsEmptyElement)
                    reader.Read();
                else
                    handle(name, reader.ReadElementContentAsString().Trim());
            }

            if (reader.NodeType == XmlNodeType.EndElement)
                reader.Read();
        }

        /// <summary>
        /// Writes the control file the way OpenSim does, which a reader that follows the declaration cannot
        /// read: it says utf-16 and is saved as UTF-8. <see cref="FromStream"/> reads it.
        /// </summary>
        public string ToXml()
        {
            var text = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new XmlTextWriter(text) { Formatting = Formatting.Indented, Indentation = 2 })
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("archive");
                writer.WriteAttributeString("major_version", MajorVersion.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("minor_version", MinorVersion.ToString(CultureInfo.InvariantCulture));

                if (CreationTime.HasValue || Id.HasValue)
                {
                    writer.WriteStartElement("creation_info");
                    if (CreationTime.HasValue)
                        writer.WriteElementString("datetime", ToUnixSeconds(CreationTime.Value).ToString(CultureInfo.InvariantCulture));
                    if (Id.HasValue)
                        writer.WriteElementString("id", Id.Value.ToString());
                    writer.WriteEndElement();
                }

                if (IsMegaregion || (SizeX.HasValue && SizeY.HasValue))
                {
                    writer.WriteStartElement("region_info");
                    writer.WriteElementString("is_megaregion", IsMegaregion ? "True" : "False");
                    if (SizeX.HasValue && SizeY.HasValue)
                        writer.WriteElementString("size_in_meters",
                            SizeX.Value.ToString(CultureInfo.InvariantCulture) + "," + SizeY.Value.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }

                if (AssetsIncluded.HasValue)
                    writer.WriteElementString("assets_included", AssetsIncluded.Value ? "True" : "False");

                writer.WriteEndElement();
                writer.Flush();
            }

            return text.ToString();
        }
    }
}
