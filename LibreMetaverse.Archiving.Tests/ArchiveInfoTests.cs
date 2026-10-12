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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The <c>archive.xml</c> of a region archive says which version of the format it is. The control file in
    /// ShoppingCenter.oar was written by OpenSim, so it is what <see cref="ArchiveInfo"/> is checked against.
    /// </summary>
    [TestFixture]
    [Category("OarFile")]
    public class ArchiveInfoTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lm-oar-info-" + Guid.NewGuid());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static string Fixture => Path.Combine(TestContext.CurrentContext.TestDirectory, "OarTestData", "ShoppingCenter.oar");

        /// <summary>The control file exactly as OpenSim wrote it into the fixture.</summary>
        private static byte[] OpenSimControlFile()
        {
            using (var file = File.OpenRead(Fixture))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                var tar = new TarArchiveReader(gzip);
                byte[] data;
                while ((data = tar.ReadEntry(out var path, out var type)) != null)
                {
                    if (path == "archive.xml") return data;
                }
            }
            throw new InvalidOperationException("no archive.xml");
        }

        private string WriteArchive(string name, params (string Path, byte[] Data)[] entries)
        {
            var file = Path.Combine(_dir, name);
            using (var stream = new FileStream(file, FileMode.Create))
            using (var gzip = new GZipStream(stream, CompressionMode.Compress))
            {
                var tar = new TarArchiveWriter(gzip);
                foreach (var entry in entries) tar.WriteFile(entry.Path, entry.Data);
                tar.Close();
            }
            return file;
        }

        private static byte[] Text(string s) => Encoding.UTF8.GetBytes(s);

        private static byte[] Terrain() => new byte[4 * 4];

        private sealed class Unpacked
        {
            public readonly List<ArchiveInfo> Infos = new List<ArchiveInfo>();
            public int Terrains;

            public Unpacked(string file) => OarFile.UnpackageArchive(file, (a, _, __) => { }, (t, _, __) => Terrains++,
                (l, _, __) => { }, (n, s) => { }, info => Infos.Add(info));
        }

        [Test]
        public void TheControlFileOpenSimWrote_IsRead()
        {
            var info = ArchiveInfo.FromBytes(OpenSimControlFile());

            Assert.That(info, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(info.MajorVersion, Is.EqualTo(0));
                Assert.That(info.MinorVersion, Is.EqualTo(8));
                Assert.That(info.VersionString, Is.EqualTo("0.8"));
                Assert.That(info.CreationTime, Is.EqualTo(Utils.UnixTimeToDateTime(1356051833)));
                Assert.That(info.CreationTime.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(info.Id, Is.EqualTo(UUID.Parse("55b0e564-9048-4f6b-817c-b8cb466ed549")));
                Assert.That(info.IsMegaregion, Is.False);
                Assert.That(info.SizeX, Is.EqualTo(256));
                Assert.That(info.SizeY, Is.EqualTo(256));
                Assert.That(info.AssetsIncluded, Is.True);
                Assert.That(info.IsNewerThanSupported, Is.False);
            });
        }

        [Test]
        public void ReadArchiveInfo_GivesTheSameFromTheArchive()
        {
            var info = OarFile.ReadArchiveInfo(Fixture);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.VersionString, Is.EqualTo("0.8"));
            Assert.That(info.Id, Is.EqualTo(UUID.Parse("55b0e564-9048-4f6b-817c-b8cb466ed549")));
        }

        [Test]
        public void Unpackage_TellsTheCallbackOnceWhatTheArchiveSaysAboutItself()
        {
            var unpacked = new Unpacked(Fixture);

            Assert.That(unpacked.Infos, Has.Count.EqualTo(1));
            Assert.That(unpacked.Infos[0].VersionString, Is.EqualTo("0.8"));
            Assert.That(unpacked.Terrains, Is.EqualTo(1), "and goes on to load the rest");
        }

        [Test]
        public void TheFiveArgumentUnpackage_StillWorks()
        {
            int terrains = 0;
            OarFile.UnpackageArchive(Fixture, (a, _, __) => { }, (t, _, __) => terrains++, (l, _, __) => { }, (n, s) => { });

            Assert.That(terrains, Is.EqualTo(1));
        }

        [Test]
        public void TheOldPlaceholder_IsVersion0Point1WithNothingElse()
        {
            // What PackageArchive used to write
            var info = ArchiveInfo.FromBytes(Text("<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<archive major_version=\"0\" minor_version=\"1\" />"));

            Assert.Multiple(() =>
            {
                Assert.That(info.VersionString, Is.EqualTo("0.1"));
                Assert.That(info.CreationTime, Is.Null);
                Assert.That(info.Id, Is.Null);
                Assert.That(info.SizeX, Is.Null);
                Assert.That(info.AssetsIncluded, Is.Null);
                Assert.That(info.IsNewerThanSupported, Is.False);
            });
        }

        [Test]
        public void AnArchiveWithNoControlFile_LoadsAndHasNoInfo()
        {
            var file = WriteArchive("none.oar", ("terrains/default.r32", Terrain()));

            var unpacked = new Unpacked(file);

            Assert.Multiple(() =>
            {
                Assert.That(OarFile.ReadArchiveInfo(file), Is.Null);
                Assert.That(unpacked.Infos, Is.Empty);
                Assert.That(unpacked.Terrains, Is.EqualTo(1));
            });
        }

        [TestCase("this is not xml")]
        [TestCase("<archive>")]
        [TestCase("<archive />")]
        [TestCase("<archive major_version=\"x\" minor_version=\"1\" />")]
        [TestCase("<archive major_version=\"0\" />")]
        [TestCase("<something major_version=\"0\" minor_version=\"8\" />")]
        public void AControlFileThatCannotBeRead_GivesNoInfoAndTheArchiveStillLoads(string content)
        {
            var file = WriteArchive("bad.oar", ("archive.xml", Text(content)), ("terrains/default.r32", Terrain()));

            var unpacked = new Unpacked(file);

            Assert.Multiple(() =>
            {
                Assert.That(ArchiveInfo.FromBytes(Text(content)), Is.Null);
                Assert.That(OarFile.ReadArchiveInfo(file), Is.Null);
                Assert.That(unpacked.Infos, Is.Empty);
                Assert.That(unpacked.Terrains, Is.EqualTo(1));
            });
        }

        [Test]
        public void ANewerArchive_IsReadAsAKnownOneAndSaysItIsNewer()
        {
            // The multi-region layout, from its description: version 1.0 with a list of regions
            var control = "<archive major_version=\"1\" minor_version=\"0\"><creation_info><datetime>1700000000</datetime></creation_info>"
                          + "<assets_included>True</assets_included><regions><region><dir>Region1</dir><size_in_meters>512,512</size_in_meters></region></regions></archive>";
            var file = WriteArchive("newer.oar", ("archive.xml", Text(control)), ("terrains/default.r32", Terrain()));

            var unpacked = new Unpacked(file);

            Assert.That(unpacked.Infos, Has.Count.EqualTo(1));
            var info = unpacked.Infos[0];
            Assert.Multiple(() =>
            {
                Assert.That(info.VersionString, Is.EqualTo("1.0"));
                Assert.That(info.IsNewerThanSupported, Is.True);
                Assert.That(info.AssetsIncluded, Is.True);
                Assert.That(info.CreationTime, Is.EqualTo(Utils.UnixTimeToDateTime(1700000000)));
                Assert.That(unpacked.Terrains, Is.EqualTo(1), "what is laid out as it was in 0.8 is still found");
            });
        }

        [TestCase(0, 8, false)]
        [TestCase(0, 7, false)]
        [TestCase(0, 9, true)]
        [TestCase(1, 0, true)]
        [TestCase(2, 0, true)]
        public void IsNewerThanSupported_ComparesMajorThenMinor(int major, int minor, bool newer)
        {
            Assert.That(new ArchiveInfo { MajorVersion = major, MinorVersion = minor }.IsNewerThanSupported, Is.EqualTo(newer));
        }

        [Test]
        public void ElementsInAnyOrder_AndUnknownOnes_AreFine()
        {
            var control = "<archive minor_version=\"8\" major_version=\"0\"><assets_included>False</assets_included><extra><deep /></extra>"
                          + "<region_info><size_in_meters>512,256</size_in_meters><is_megaregion>True</is_megaregion><note>x</note></region_info>"
                          + "<creation_info><id>55b0e564-9048-4f6b-817c-b8cb466ed549</id><datetime>1356051833</datetime></creation_info></archive>";

            var info = ArchiveInfo.FromBytes(Text(control));

            Assert.Multiple(() =>
            {
                Assert.That(info.AssetsIncluded, Is.False);
                Assert.That(info.IsMegaregion, Is.True);
                Assert.That(info.SizeX, Is.EqualTo(512));
                Assert.That(info.SizeY, Is.EqualTo(256));
                Assert.That(info.Id, Is.EqualTo(UUID.Parse("55b0e564-9048-4f6b-817c-b8cb466ed549")));
                Assert.That(info.CreationTime, Is.EqualTo(Utils.UnixTimeToDateTime(1356051833)));
            });
        }

        [Test]
        public void ToXml_IsTheControlFileOpenSimWrites()
        {
            var opensim = XDocument.Parse(Encoding.UTF8.GetString(OpenSimControlFile()));
            var info = ArchiveInfo.FromBytes(OpenSimControlFile());

            var ours = XDocument.Parse(info.ToXml());

            // Same elements, in the same order, with the same values and the same declaration
            Assert.Multiple(() =>
            {
                Assert.That(Names(ours), Is.EqualTo(Names(opensim)));
                Assert.That(Values(ours), Is.EqualTo(Values(opensim)));
                Assert.That(ours.Root.Attribute("major_version").Value, Is.EqualTo("0"));
                Assert.That(ours.Root.Attribute("minor_version").Value, Is.EqualTo("8"));
                Assert.That(ours.Declaration.Encoding, Is.EqualTo(opensim.Declaration.Encoding));
            });
        }

        private static List<string> Names(XDocument d) => d.Descendants().Select(e => e.Name.LocalName).ToList();
        private static List<string> Values(XDocument d) => d.Descendants().Where(e => !e.HasElements).Select(e => e.Value).ToList();

        [Test]
        public void ToXml_ThenFromBytes_GivesBackTheInfo()
        {
            var info = new ArchiveInfo
            {
                MajorVersion = 0, MinorVersion = 8, CreationTime = Utils.UnixTimeToDateTime(1700000123), Id = UUID.Random(),
                IsMegaregion = true, SizeX = 1024, SizeY = 512, AssetsIncluded = false
            };

            var read = ArchiveInfo.FromBytes(Text(info.ToXml()));

            Assert.Multiple(() =>
            {
                Assert.That(read.VersionString, Is.EqualTo("0.8"));
                Assert.That(read.CreationTime, Is.EqualTo(info.CreationTime));
                Assert.That(read.Id, Is.EqualTo(info.Id));
                Assert.That(read.IsMegaregion, Is.True);
                Assert.That(read.SizeX, Is.EqualTo(1024));
                Assert.That(read.SizeY, Is.EqualTo(512));
                Assert.That(read.AssetsIncluded, Is.False);
            });
        }

        [Test]
        public void ToXml_LeavesOutWhatIsNotKnown()
        {
            var xml = XDocument.Parse(new ArchiveInfo { MajorVersion = 0, MinorVersion = 8 }.ToXml());

            Assert.That(xml.Root.Elements(), Is.Empty);
        }

        private string Folder(bool withAsset)
        {
            var folder = Path.Combine(_dir, "region");
            foreach (var sub in new[] { "assets", "objects", "terrains", "landdata", "settings" })
                Directory.CreateDirectory(Path.Combine(folder, sub));
            File.WriteAllBytes(Path.Combine(folder, "terrains", "default.r32"), Terrain());
            if (withAsset)
                File.WriteAllText(Path.Combine(folder, "assets", UUID.Random() + "_notecard.txt"), "Linden text version 2\n");
            return folder;
        }

        [Test]
        public void PackageArchive_WritesVersion0Point8_ForARegionOf256()
        {
            var archive = Path.Combine(_dir, "default.oar");
            var before = DateTime.UtcNow.AddSeconds(-2);

            OarFile.PackageArchive(Folder(withAsset: true), archive);

            var info = OarFile.ReadArchiveInfo(archive);
            Assert.That(info, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(info.VersionString, Is.EqualTo("0.8"));
                Assert.That(info.IsNewerThanSupported, Is.False);
                Assert.That(info.AssetsIncluded, Is.True);
                Assert.That(info.IsMegaregion, Is.False);
                Assert.That(info.SizeX, Is.EqualTo(256));
                Assert.That(info.SizeY, Is.EqualTo(256));
                Assert.That(info.Id, Is.Not.Null.And.Not.EqualTo(UUID.Zero));
                Assert.That(info.CreationTime, Is.GreaterThan(before).And.LessThan(DateTime.UtcNow.AddSeconds(2)));
            });
        }

        [Test]
        public void PackageArchive_SaysWhenThereAreNoAssets()
        {
            var archive = Path.Combine(_dir, "noassets.oar");

            OarFile.PackageArchive(Folder(withAsset: false), archive);

            Assert.That(OarFile.ReadArchiveInfo(archive).AssetsIncluded, Is.False);
        }

        [Test]
        public void PackageArchive_WritesTheInfoItIsGiven()
        {
            var archive = Path.Combine(_dir, "given.oar");
            var given = new ArchiveInfo { MajorVersion = 0, MinorVersion = 8, SizeX = 512, SizeY = 512, AssetsIncluded = true, Id = UUID.Parse("11111111-1111-1111-1111-111111111111") };

            OarFile.PackageArchive(Folder(withAsset: false), archive, given);

            var info = OarFile.ReadArchiveInfo(archive);
            Assert.Multiple(() =>
            {
                Assert.That(info.SizeX, Is.EqualTo(512));
                Assert.That(info.AssetsIncluded, Is.True, "as given, not as found");
                Assert.That(info.Id, Is.EqualTo(given.Id));
                Assert.That(info.CreationTime, Is.Null);
            });
        }

        [Test]
        public void ArchiveInfo_DoesNotRefuseAnythingItCannotRead()
        {
            // A reader that insists on the declared encoding would fail on a file OpenSim wrote
            var control = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<archive major_version=\"0\" minor_version=\"8\"></archive>";

            Assert.That(ArchiveInfo.FromBytes(Text(control)).VersionString, Is.EqualTo("0.8"));
        }
    }
}
