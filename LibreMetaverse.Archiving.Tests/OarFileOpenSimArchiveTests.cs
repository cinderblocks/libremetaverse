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
using System.Xml.Linq;
using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Reads a region archive that OpenSim wrote (ShoppingCenter.oar, see OarTestData/README.md), and checks what
    /// <see cref="OarFile"/> gives against the XML in the archive read another way, with <see cref="XDocument"/>.
    /// </summary>
    [TestFixture]
    [Category("OarFile")]
    public class OarFileOpenSimArchiveTests
    {
        private const int ObjectFiles = 217;

        private static string FixturePath =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "OarTestData", "ShoppingCenter.oar");

        private sealed class Loaded
        {
            public readonly List<AssetPrim> Linksets = new List<AssetPrim>();
            public readonly List<Asset> Assets = new List<Asset>();
            public readonly List<float[,]> Terrains = new List<float[,]>();
            public readonly List<(string Name, RegionSettings Settings)> Settings = new List<(string, RegionSettings)>();
        }

        private static Loaded Load()
        {
            var loaded = new Loaded();
            OarFile.UnpackageArchive(FixturePath,
                (asset, _, __) => loaded.Assets.Add(asset),
                (terrain, _, __) => loaded.Terrains.Add(terrain),
                (linkset, _, __) => loaded.Linksets.Add(linkset),
                (name, settings) => loaded.Settings.Add((name, settings)));
            return loaded;
        }

        /// <summary>Every file in the archive, read with the framework's gzip and the tar reader.</summary>
        private static List<(string Path, byte[] Data)> RawEntries()
        {
            var entries = new List<(string, byte[])>();
            using (var file = File.OpenRead(FixturePath))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                var tar = new TarArchiveReader(gzip);
                byte[] data;
                while ((data = tar.ReadEntry(out var path, out var type)) != null)
                {
                    if (type == TarArchiveReader.TarEntryType.TYPE_NORMAL_FILE) entries.Add((path, data));
                }
            }
            return entries;
        }

        private static Vector3 Vector(XElement e) =>
            new Vector3(Float(e.Element("X")), Float(e.Element("Y")), Float(e.Element("Z")));

        private static Quaternion Quat(XElement e) =>
            new Quaternion(Float(e.Element("X")), Float(e.Element("Y")), Float(e.Element("Z")), Float(e.Element("W")));

        private static float Float(XElement e) => float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture);

        private static UUID Id(XElement e) => UUID.Parse(e.Element("UUID").Value);

        [Test]
        public void EveryObjectFileInTheArchiveGivesALinkset()
        {
            var raw = RawEntries().Where(e => e.Path.StartsWith(ArchiveConstants.OBJECTS_PATH)).ToList();
            var loaded = Load();

            Assert.Multiple(() =>
            {
                Assert.That(raw, Has.Count.EqualTo(ObjectFiles));
                Assert.That(loaded.Linksets, Has.Count.EqualTo(ObjectFiles));
                Assert.That(loaded.Linksets.All(l => l.Parent != null), Is.True);
            });
        }

        [Test]
        public void EveryPartIsWhatItsXmlSays()
        {
            var raw = RawEntries().Where(e => e.Path.StartsWith(ArchiveConstants.OBJECTS_PATH)).ToList();
            var loaded = Load();
            Assert.That(loaded.Linksets, Has.Count.EqualTo(raw.Count));

            int parts = 0;
            for (int i = 0; i < raw.Count; i++)
            {
                var xml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(raw[i].Data));
                var elements = xml.Descendants("SceneObjectPart").ToList();
                var linkset = loaded.Linksets[i];
                var prims = new[] { linkset.Parent }.Concat(linkset.Children).ToList();

                Assert.That(prims, Has.Count.EqualTo(elements.Count), raw[i].Path);

                for (int p = 0; p < prims.Count; p++)
                {
                    var e = elements[p];
                    var prim = prims[p];
                    string where = $"{raw[i].Path}, part {p}";

                    Assert.Multiple(() =>
                    {
                        Assert.That(prim.ID, Is.EqualTo(Id(e.Element("UUID"))), where);
                        Assert.That(prim.Name, Is.EqualTo(e.Element("Name").Value), where);
                        Assert.That(prim.Description, Is.EqualTo(e.Element("Description").Value), where);
                        Assert.That(prim.LocalID, Is.EqualTo(uint.Parse(e.Element("LocalId").Value)), where);
                        Assert.That(prim.ParentID, Is.EqualTo(uint.Parse(e.Element("ParentID").Value)), where);
                        Assert.That(prim.LinkNumber, Is.EqualTo(int.Parse(e.Element("LinkNum").Value)), where);
                        Assert.That(prim.Scale, Is.EqualTo(Vector(e.Element("Scale"))), where);
                        Assert.That(prim.Rotation, Is.EqualTo(Quat(e.Element("RotationOffset"))), where);
                        Assert.That(prim.Position, Is.EqualTo(Vector(e.Element(p == 0 ? "GroupPosition" : "OffsetPosition"))), where);
                        Assert.That(prim.OwnerID, Is.EqualTo(Id(e.Element("OwnerID"))), where);
                        Assert.That(prim.CreatorID, Is.EqualTo(Id(e.Element("CreatorID"))), where);
                        Assert.That(prim.GroupID, Is.EqualTo(Id(e.Element("GroupID"))), where);
                        Assert.That(prim.PermsOwner, Is.EqualTo(uint.Parse(e.Element("OwnerMask").Value)), where);
                        Assert.That(prim.PermsNextOwner, Is.EqualTo(uint.Parse(e.Element("NextOwnerMask").Value)), where);
                        Assert.That(prim.RegionHandle, Is.EqualTo(ulong.Parse(e.Element("RegionHandle").Value)), where);
                        Assert.That(prim.Material, Is.EqualTo(int.Parse(e.Element("Material").Value)), where);

                        var shape = e.Element("Shape");
                        Assert.That(prim.PCode, Is.EqualTo(int.Parse(shape.Element("PCode").Value)), where);
                        Assert.That(prim.Shape.PathCurve, Is.EqualTo(int.Parse(shape.Element("PathCurve").Value)), where);
                        Assert.That(prim.Shape.ProfileCurve, Is.EqualTo(int.Parse(shape.Element("ProfileCurve").Value)), where);
                        Assert.That(prim.Textures, Is.Not.Null, where);
                    });

                    parts++;
                }
            }

            Assert.That(parts, Is.GreaterThan(ObjectFiles));
        }

        [Test]
        public void ARootAndItsChildrenKeepTheirPlaces()
        {
            // The first object file: a single prim in a group, positioned by the group position
            var loaded = Load();
            var linkset = loaded.Linksets.First(l => l.Parent.ID == UUID.Parse("9a9230fb-c978-4f22-99b5-e057eb70fe81"));

            Assert.Multiple(() =>
            {
                Assert.That(linkset.Parent.Name, Is.EqualTo("Primitive"));
                Assert.That(linkset.Parent.Position, Is.EqualTo(new Vector3(90.49012f, 109.9588f, 20.79342f)));
                Assert.That(linkset.Parent.Rotation, Is.EqualTo(new Quaternion(0f, 0f, 1f, 0f)));
                Assert.That(linkset.Parent.TextColor, Is.EqualTo(new Color4(0f, 0f, 0f, 1f)), "hover text color is bytes, 0 0 0 255");
                Assert.That(linkset.Parent.Shape.PathCurve, Is.EqualTo(16));
                Assert.That(linkset.Parent.Textures, Is.Not.Null);
            });

            // A group with children: the children are placed relative to the root
            var group = loaded.Linksets.First(l => l.Children.Count > 0);
            Assert.That(group.Children.All(c => c.ParentID == group.Parent.LocalID), Is.True);
        }

        [Test]
        public void TheTextureAssetsAreAllThere()
        {
            var raw = RawEntries().Where(e => e.Path.StartsWith(ArchiveConstants.ASSETS_PATH)).ToList();
            var loaded = Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Assets, Has.Count.EqualTo(15));
                Assert.That(loaded.Assets.Select(a => a.AssetType).Distinct(), Is.EqualTo(new[] { AssetType.Texture }));
                Assert.That(loaded.Assets.Select(a => a.AssetID), Is.EquivalentTo(
                    raw.Select(e => UUID.Parse(Path.GetFileName(e.Path).Substring(0, 36)))));
                Assert.That(loaded.Assets.All(a => a.AssetData.Length > 0), Is.True);
            });
        }

        [Test]
        public void TheTerrainIs256By256WithTheHeightsTheFileHolds()
        {
            var raw = RawEntries().Single(e => e.Path.StartsWith(ArchiveConstants.TERRAINS_PATH)).Data;
            var loaded = Load();

            Assert.That(loaded.Terrains, Has.Count.EqualTo(1));
            var terrain = loaded.Terrains[0];

            Assert.Multiple(() =>
            {
                Assert.That(terrain.GetLength(0), Is.EqualTo(256));
                Assert.That(terrain.GetLength(1), Is.EqualTo(256));
                foreach (var (x, y) in new[] { (0, 0), (255, 0), (0, 255), (255, 255), (128, 77), (13, 200) })
                {
                    float expected = Math.Min(Math.Max(BitConverter.ToSingle(raw, (y * 256 + x) * 4), 0f), 255f);
                    Assert.That(terrain[y, x], Is.EqualTo(expected), $"({x}, {y})");
                }
            });
        }

        [Test]
        public void TheRegionSettingsAreRead()
        {
            // The file says encoding="utf-16" but holds UTF-8, as OpenSim writes it
            var loaded = Load();

            Assert.That(loaded.Settings, Has.Count.EqualTo(1));
            Assert.That(loaded.Settings[0].Name, Is.EqualTo("ShoppingCenter"));
            var s = loaded.Settings[0].Settings;

            Assert.Multiple(() =>
            {
                Assert.That(s.AllowDamage, Is.False);
                Assert.That(s.AllowLandResell, Is.True);
                Assert.That(s.AllowLandJoinDivide, Is.True);
                Assert.That(s.MaturityRating, Is.EqualTo(1));
                Assert.That(s.AgentLimit, Is.EqualTo(40));
                Assert.That(s.ObjectBonus, Is.EqualTo(1f));
                Assert.That(s.TerrainDetail0, Is.EqualTo(UUID.Parse("b8d3965a-ad78-bf43-699b-bff8eca6c975")));
                Assert.That(s.TerrainDetail3, Is.EqualTo(UUID.Parse("beb169c7-11ea-fff2-efe5-0f24dc881df2")));
                Assert.That(s.TerrainStartHeight00, Is.EqualTo(10f));
                Assert.That(s.TerrainHeightRange11, Is.EqualTo(60f));
                Assert.That(s.WaterHeight, Is.EqualTo(20f));
                Assert.That(s.TerrainRaiseLimit, Is.EqualTo(100f));
                Assert.That(s.TerrainLowerLimit, Is.EqualTo(-100f));
                Assert.That(s.UseEstateSun, Is.True);
                Assert.That(s.FixedSun, Is.False);
            });
        }

        [Test]
        public void SculptFlexibleAndLightAreWhatTheXmlSays()
        {
            var raw = RawEntries().Where(e => e.Path.StartsWith(ArchiveConstants.OBJECTS_PATH)).ToList();
            var loaded = Load();
            int sculpts = 0, flexibles = 0, lights = 0;

            for (int i = 0; i < raw.Count; i++)
            {
                var elements = XDocument.Parse(System.Text.Encoding.UTF8.GetString(raw[i].Data)).Descendants("SceneObjectPart").ToList();
                var prims = new[] { loaded.Linksets[i].Parent }.Concat(loaded.Linksets[i].Children).ToList();

                for (int p = 0; p < prims.Count; p++)
                {
                    var shape = elements[p].Element("Shape");
                    var prim = prims[p];
                    string where = $"{raw[i].Path}, part {p}";

                    if (shape.Element("SculptEntry").Value == "true")
                    {
                        sculpts++;
                        Assert.That(prim.Sculpt.Texture, Is.EqualTo(Id(shape.Element("SculptTexture"))), where);
                        Assert.That(prim.Sculpt.Type, Is.EqualTo(int.Parse(shape.Element("SculptType").Value)), where);
                    }

                    if (shape.Element("FlexiEntry").Value == "true")
                    {
                        flexibles++;
                        Assert.That(prim.Flexible.Softness, Is.EqualTo(int.Parse(shape.Element("FlexiSoftness").Value)), where);
                        Assert.That(prim.Flexible.Tension, Is.EqualTo(Float(shape.Element("FlexiTension"))), where);
                        Assert.That(prim.Flexible.Force.Z, Is.EqualTo(Float(shape.Element("FlexiForceZ"))), where);
                    }

                    if (shape.Element("LightEntry").Value == "true")
                    {
                        lights++;
                        Assert.That(prim.Light.Radius, Is.EqualTo(Float(shape.Element("LightRadius"))), where);
                        Assert.That(prim.Light.Intensity, Is.EqualTo(Float(shape.Element("LightIntensity"))), where);
                        Assert.That(prim.Light.Color.G, Is.EqualTo(Float(shape.Element("LightColorG"))), where);
                    }
                }
            }

            // The archive has flexible prims (the wildflowers) and lights, and no sculpted prims; those are checked
            // by SceneObjectGroupXmlReaderTests
            Assert.That(flexibles, Is.GreaterThan(0));
            Assert.That(lights, Is.GreaterThan(0));
            TestContext.WriteLine($"sculpt {sculpts}, flexible {flexibles}, light {lights}");
        }

        [Test]
        public void TheWriterUsesTheElementOrderOpenSimDoes()
        {
            // OpenSim applies elements as it meets them, so the order of the ones the writer shares with it matters
            var raw = RawEntries().First(e => e.Path.StartsWith(ArchiveConstants.OBJECTS_PATH));
            var openSim = XDocument.Parse(System.Text.Encoding.UTF8.GetString(raw.Data)).Descendants("SceneObjectPart").First();

            var linkset = Load().Linksets.First(l => l.Parent.ID == UUID.Parse("9a9230fb-c978-4f22-99b5-e057eb70fe81"));
            var ours = XDocument.Parse(linkset.EncodeXml()).Descendants("SceneObjectPart").First();

            Assert.Multiple(() =>
            {
                Assert.That(Shared(ours.Elements(), openSim.Elements()), Is.EqualTo(Shared(openSim.Elements(), ours.Elements())), "part");
                Assert.That(Shared(ours.Element("Shape").Elements(), openSim.Element("Shape").Elements()),
                    Is.EqualTo(Shared(openSim.Element("Shape").Elements(), ours.Element("Shape").Elements())), "shape");
            });
        }

        /// <summary>The names in <paramref name="from"/> that <paramref name="other"/> also has, in the order of <paramref name="from"/>.</summary>
        private static List<string> Shared(IEnumerable<XElement> from, IEnumerable<XElement> other)
        {
            var names = new HashSet<string>(other.Select(e => e.Name.LocalName));
            return from.Select(e => e.Name.LocalName).Where(names.Contains).ToList();
        }
    }
}
