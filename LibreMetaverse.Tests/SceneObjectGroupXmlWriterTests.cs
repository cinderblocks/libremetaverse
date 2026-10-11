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
using System.IO;
using System.Xml;
using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="SceneObjectGroupXmlWriter"/> is what <see cref="AssetPrim.EncodeXml"/> writes a linkset with, and what
    /// region archives hold their objects as, so a linkset written and read back has to be the linkset that was written.
    /// </summary>
    [TestFixture]
    public class SceneObjectGroupXmlWriterTests
    {
        private static PrimObject Part(string name, uint localId, uint parentId, Vector3 position)
        {
            return new PrimObject
            {
                ID = UUID.Random(),
                CreatorID = UUID.Random(),
                OwnerID = UUID.Random(),
                Name = name,
                Description = name + " description",
                LocalID = localId,
                ParentID = parentId,
                Position = position,
                Rotation = Quaternion.Identity,
                Scale = new Vector3(1f, 2f, 3f)
            };
        }

        private static AssetPrim Linkset()
        {
            var parent = Part("Root", 100, 0, new Vector3(128f, 64f, 25f));
            var children = new System.Collections.Generic.List<PrimObject>
            {
                Part("First child", 101, 100, new Vector3(1f, 0f, 0f)),
                Part("Second child", 102, 100, new Vector3(0f, 1f, 0f))
            };
            return new AssetPrim(parent, children);
        }

        private static string Write(AssetPrim prim)
        {
            var text = new StringWriter();
            using (var writer = new XmlTextWriter(text))
            {
                SceneObjectGroupXmlWriter.Write(writer, prim);
                writer.Flush();
            }
            return text.ToString();
        }

        [Test]
        public void Write_MakesASceneObjectGroupWithARootPartAndTheOtherParts()
        {
            var xml = new XmlDocument();
            xml.LoadXml(Write(Linkset()));

            Assert.Multiple(() =>
            {
                Assert.That(xml.DocumentElement.LocalName, Is.EqualTo("SceneObjectGroup"));
                Assert.That(xml.SelectNodes("/SceneObjectGroup/SceneObjectPart"), Has.Count.EqualTo(1));
                Assert.That(xml.SelectNodes("/SceneObjectGroup/OtherParts/SceneObjectPart"), Has.Count.EqualTo(2));
                Assert.That(xml.SelectSingleNode("/SceneObjectGroup/SceneObjectPart/Name").InnerText, Is.EqualTo("Root"));
            });
        }

        [Test]
        public void EncodeXml_IsWhatTheWriterWrites()
        {
            var prim = Linkset();

            Assert.That(prim.EncodeXml(), Is.EqualTo(Write(prim)));
        }

        [Test]
        public void Write_PutsTheFieldsOfEachPartWhereOpenSimLooksForThem()
        {
            var prim = Linkset();
            var xml = new XmlDocument();
            xml.LoadXml(Write(prim));

            string Root(string path) => xml.SelectSingleNode("/SceneObjectGroup/SceneObjectPart/" + path).InnerText;
            string Other(int index, string path) => xml.SelectNodes("/SceneObjectGroup/OtherParts/SceneObjectPart")[index].SelectSingleNode(path).InnerText;

            Assert.Multiple(() =>
            {
                Assert.That(Root("UUID/UUID"), Is.EqualTo(prim.Parent.ID.ToString()));
                Assert.That(Root("Description"), Is.EqualTo("Root description"));
                Assert.That(Root("CreatorID/UUID"), Is.EqualTo(prim.Parent.CreatorID.ToString()));
                Assert.That(Root("GroupPosition/X"), Is.EqualTo("128").Or.EqualTo("128.0"));
                Assert.That(Other(0, "Name"), Is.EqualTo("First child"));
                Assert.That(Other(1, "Name"), Is.EqualTo("Second child"));
                Assert.That(Other(1, "UUID/UUID"), Is.EqualTo(prim.Children[1].ID.ToString()));
                Assert.That(Other(1, "OffsetPosition/Y"), Is.EqualTo("1"));
            });
        }

        [Test]
        public void Write_AlinksetWithNoChildren_HasEmptyOtherParts()
        {
            var xml = new XmlDocument();
            xml.LoadXml(Write(new AssetPrim(Part("Alone", 1, 0, Vector3.Zero), new System.Collections.Generic.List<PrimObject>())));

            Assert.That(xml.SelectNodes("/SceneObjectGroup/OtherParts/SceneObjectPart"), Is.Empty);
        }
    }
}
