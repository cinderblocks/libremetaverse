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
using System.Xml;
using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Region archives are tar files, compressed, with assets, terrain and objects in folders of their own. These
    /// tests build archives from first principles with the tar writer and read them with <see cref="OarFile"/>,
    /// and the other way round, so neither side is only checked against itself.
    /// </summary>
    [TestFixture]
    [Category("OarFile")]
    public class OarFileArchiveTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lm-oar-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static byte[] Terrain(int side, Func<int, int, float> height)
        {
            var data = new byte[side * side * 4];
            int pos = 0;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    BitConverter.GetBytes(height(x, y)).CopyTo(data, pos);
                    pos += 4;
                }
            }
            return data;
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

        private sealed class Loaded
        {
            public readonly List<Asset> Assets = new List<Asset>();
            public readonly List<float[,]> Terrains = new List<float[,]>();

            public void From(string file) => OarFile.UnpackageArchive(file,
                (asset, _, __) => Assets.Add(asset), (terrain, _, __) => Terrains.Add(terrain), (linkset, _, __) => { }, (name, settings) => { });
        }

        [Test]
        public void TarWriter_ThenTarReader_GiveBackEveryFile()
        {
            var memory = new MemoryStream();
            var writer = new TarArchiveWriter(memory);
            writer.WriteFile("a.txt", "hello");
            writer.WriteFile("folder/b.bin", new byte[] { 1, 2, 3, 0, 255 });
            writer.WriteFile("folder/long-name-" + new string('x', 70) + ".txt", "long");
            writer.Close();

            var reader = new TarArchiveReader(new MemoryStream(memory.ToArray()));
            var found = new List<(string Path, byte[] Data)>();
            byte[] data;
            while ((data = reader.ReadEntry(out var path, out var type)) != null)
            {
                if (type == TarArchiveReader.TarEntryType.TYPE_NORMAL_FILE) found.Add((path, data));
            }

            Assert.Multiple(() =>
            {
                Assert.That(found.Select(f => f.Path), Is.EqualTo(new[] { "a.txt", "folder/b.bin", "folder/long-name-" + new string('x', 70) + ".txt" }));
                Assert.That(Encoding.UTF8.GetString(found[0].Data), Is.EqualTo("hello"));
                Assert.That(found[1].Data, Is.EqualTo(new byte[] { 1, 2, 3, 0, 255 }));
                Assert.That(Encoding.UTF8.GetString(found[2].Data), Is.EqualTo("long"));
            });
        }

        [Test]
        public void Unpackage_GivesTheAssetsWithTheirIdsAndTypes()
        {
            var note = UUID.Random();
            var script = UUID.Random();
            var file = WriteArchive("assets.oar",
                ("archive.xml", Encoding.UTF8.GetBytes("<archive major_version=\"0\" minor_version=\"1\" />")),
                (ArchiveConstants.ASSETS_PATH + note + "_notecard.txt", Encoding.UTF8.GetBytes("Linden text version 2\n")),
                (ArchiveConstants.ASSETS_PATH + script + "_script.lsl", Encoding.UTF8.GetBytes("default { }")));

            var loaded = new Loaded();
            loaded.From(file);

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Assets, Has.Count.EqualTo(2));
                Assert.That(loaded.Assets[0], Is.TypeOf<AssetNotecard>());
                Assert.That(loaded.Assets[0].AssetID, Is.EqualTo(note));
                Assert.That(loaded.Assets[1], Is.TypeOf<AssetScriptText>());
                Assert.That(loaded.Assets[1].AssetID, Is.EqualTo(script));
                Assert.That(Encoding.UTF8.GetString(loaded.Assets[1].AssetData), Is.EqualTo("default { }"));
            });
        }

        [Test]
        public void Unpackage_GivesTheTerrainAsAGridOfHeights()
        {
            var file = WriteArchive("terrain.oar",
                (ArchiveConstants.TERRAINS_PATH + "default.r32", Terrain(4, (x, y) => y * 10f + x)));

            var loaded = new Loaded();
            loaded.From(file);

            Assert.That(loaded.Terrains, Has.Count.EqualTo(1));
            var terrain = loaded.Terrains[0];
            Assert.Multiple(() =>
            {
                Assert.That(terrain.GetLength(0), Is.EqualTo(4));
                Assert.That(terrain.GetLength(1), Is.EqualTo(4));
                Assert.That(terrain[0, 0], Is.EqualTo(0f));
                Assert.That(terrain[2, 3], Is.EqualTo(23f)); // row 2 (y), column 3 (x)
                Assert.That(terrain[3, 1], Is.EqualTo(31f));
            });
        }

        [Test]
        public void Unpackage_LeavesOutWhatItDoesNotKnow()
        {
            var known = UUID.Random();
            var file = WriteArchive("odd.oar",
                (ArchiveConstants.ASSETS_PATH + known + "_notecard.txt", new byte[] { 65 }),
                (ArchiveConstants.ASSETS_PATH + UUID.Random() + "_nonsense.xyz", new byte[] { 66 }),
                (ArchiveConstants.ASSETS_PATH + "no-separator.txt", new byte[] { 67 }),
                (ArchiveConstants.TERRAINS_PATH + "wrong-size.r32", new byte[] { 1, 2, 3, 4, 5 }),
                (ArchiveConstants.TERRAINS_PATH + "image.png", new byte[] { 1, 2, 3, 4 }));

            var loaded = new Loaded();
            loaded.From(file);

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Assets.Select(a => a.AssetID), Is.EqualTo(new[] { known }));
                Assert.That(loaded.Terrains, Is.Empty);
            });
        }

        [Test]
        public void Unpackage_AFileThatIsNotThere_GivesNothingAndDoesNotThrow()
        {
            var loaded = new Loaded();

            Assert.DoesNotThrow(() => loaded.From(Path.Combine(_dir, "missing.oar")));
            Assert.That(loaded.Assets, Is.Empty);
        }

        [Test]
        public void Package_ThenUnpackage_GivesBackWhatWasInTheFolders()
        {
            var folder = Path.Combine(_dir, "region");
            foreach (var sub in new[] { "assets", "objects", "terrains", "landdata", "settings" })
                Directory.CreateDirectory(Path.Combine(folder, sub));
            var note = UUID.Random();
            File.WriteAllText(Path.Combine(folder, "assets", note + "_notecard.txt"), "Linden text version 2\n");
            File.WriteAllBytes(Path.Combine(folder, "terrains", "default.r32"), Terrain(2, (x, y) => x + 2 * y));

            var archive = Path.Combine(_dir, "packed.oar");
            OarFile.PackageArchive(folder, archive);

            var loaded = new Loaded();
            loaded.From(archive);

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Assets.Select(a => a.AssetID), Is.EqualTo(new[] { note }));
                Assert.That(loaded.Terrains, Has.Count.EqualTo(1));
                Assert.That(loaded.Terrains[0][1, 1], Is.EqualTo(3f));
            });

            // And a person's tar tool sees an archive.xml and the folders' files, as OpenSim expects
            var names = new List<string>();
            using (var stream = new GZipStream(File.OpenRead(archive), CompressionMode.Decompress))
            {
                var reader = new TarArchiveReader(stream);
                byte[] data;
                while ((data = reader.ReadEntry(out var path, out var type)) != null)
                {
                    if (type == TarArchiveReader.TarEntryType.TYPE_NORMAL_FILE) names.Add(path);
                }
            }
            Assert.That(names, Is.EqualTo(new[] { "archive.xml", "assets/" + note + "_notecard.txt", "terrains/default.r32" }));
        }

        [Test]
        public void SOGToXml2_IsWhatTheCoreWritesALinksetWith()
        {
            var prim = new AssetPrim(
                new PrimObject { ID = UUID.Random(), Name = "Root", Rotation = Quaternion.Identity },
                new List<PrimObject> { new PrimObject { ID = UUID.Random(), Name = "Child", Rotation = Quaternion.Identity } });

            var text = new StringWriter();
            using (var writer = new XmlTextWriter(text))
            {
                OarFile.SOGToXml2(writer, prim);
                writer.Flush();
            }

            Assert.That(text.ToString(), Is.EqualTo(prim.EncodeXml()));
        }

        private static byte[] Group(string name, UUID id) => Encoding.UTF8.GetBytes(
            $"<SceneObjectGroup><SceneObjectPart><UUID><UUID>{id}</UUID></UUID><Name>{name}</Name></SceneObjectPart></SceneObjectGroup>");

        private static List<string> LoadedNames(string file)
        {
            var names = new List<string>();
            OarFile.UnpackageArchive(file, (a, _, __) => { }, (t, _, __) => { }, (linkset, _, __) => names.Add(linkset.Parent.Name), (n, s) => { });
            return names;
        }

        [Test]
        public void Unpackage_AnObjectFileThatCannotBeRead_DoesNotLoseTheOthers()
        {
            var file = WriteArchive("one-bad.oar",
                (ArchiveConstants.OBJECTS_PATH + "a.xml", Group("A", UUID.Random())),
                (ArchiveConstants.OBJECTS_PATH + "b.xml", Encoding.UTF8.GetBytes("<SceneObjectGroup><SceneObjectPart><Name>cut off")),
                (ArchiveConstants.OBJECTS_PATH + "c.xml", Group("C", UUID.Random())));

            Assert.That(LoadedNames(file), Is.EqualTo(new[] { "A", "C" }));
        }

        [Test]
        public void Unpackage_AnObjectFileThatIsNotAGroup_IsLeftOut()
        {
            var file = WriteArchive("not-a-group.oar",
                (ArchiveConstants.OBJECTS_PATH + "a.xml", Encoding.UTF8.GetBytes("<Something />")),
                (ArchiveConstants.OBJECTS_PATH + "b.xml", Group("B", UUID.Random())));

            Assert.That(LoadedNames(file), Is.EqualTo(new[] { "B" }));
        }

        [Test]
        public void Unpackage_ObjectFilesWithADeclaration_AreRead()
        {
            // OpenSim's declarations say utf-16 over UTF-8 bytes; a byte order mark is also met
            var declared = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n" + Encoding.UTF8.GetString(Group("Declared", UUID.Random())));
            var withBom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Encoding.UTF8.GetString(Group("Bom", UUID.Random())))).ToArray();
            var file = WriteArchive("declared.oar",
                (ArchiveConstants.OBJECTS_PATH + "a.xml", declared),
                (ArchiveConstants.OBJECTS_PATH + "b.xml", withBom));

            Assert.That(LoadedNames(file), Is.EqualTo(new[] { "Declared", "Bom" }));
        }

        [Test]
        public void LoadObjects_ASceneOfGroups_GivesEachGroupAndLeavesOutOneThatIsEmpty()
        {
            var xml = "<scene>" + Encoding.UTF8.GetString(Group("One", UUID.Random())) + "<SceneObjectGroup />"
                      + Encoding.UTF8.GetString(Group("Two", UUID.Random())) + "</scene>";
            var names = new List<string>();

            OarFile.LoadObjects(Encoding.UTF8.GetBytes(xml), (linkset, _, __) => names.Add(linkset.Parent.Name), 0, 0);

            Assert.That(names, Is.EqualTo(new[] { "One", "Two" }));
        }

        [Test]
        public void LoadObjects_XmlThatCannotBeParsed_Throws()
        {
            Assert.Throws<XmlException>(() => OarFile.LoadObjects(Encoding.UTF8.GetBytes("<SceneObjectGroup><oops>"), (l, _, __) => { }, 0, 0));
        }

        [Test]
        public void LoadObjects_ACallbackThatThrows_IsNotHidden()
        {
            Assert.Throws<InvalidOperationException>(() => OarFile.LoadObjects(Group("A", UUID.Random()),
                (l, _, __) => throw new InvalidOperationException("from the callback"), 0, 0));
        }
    }
}
