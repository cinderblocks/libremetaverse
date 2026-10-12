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
using System.IO;
using System.Linq;
using System.Xml;
using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="AssetPrim.DecodeXml"/> reads the XML that OpenSim writes into region archives. The elements are
    /// matched by name: OpenSim leaves some out when they are empty and has not always written them in the same
    /// order.
    /// </summary>
    [TestFixture]
    public class SceneObjectGroupXmlReaderTests
    {
        private static readonly UUID RootId = UUID.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly UUID ChildId = UUID.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly UUID OwnerId = UUID.Parse("33333333-3333-3333-3333-333333333333");

        private static string Uuid(string name, UUID id) => $"<{name}><UUID>{id}</UUID></{name}>";

        private static string Vec(string name, float x, float y, float z) =>
            $"<{name}><X>{x}</X><Y>{y}</Y><Z>{z}</Z></{name}>";

        private static string Quat(string name, float x, float y, float z, float w) =>
            $"<{name}><X>{x}</X><Y>{y}</Y><Z>{z}</Z><W>{w}</W></{name}>";

        /// <summary>The parts of a SceneObjectPart, as separate strings so tests can leave out or reorder them.</summary>
        private static string[] PartElements(UUID id, string name, uint localId, uint parentId) => new[]
        {
            "<AllowedDrop>false</AllowedDrop>",
            Uuid("CreatorID", OwnerId),
            Uuid("UUID", id),
            $"<LocalId>{localId}</LocalId>",
            $"<Name>{name}</Name>",
            "<Material>3</Material>",
            "<RegionHandle>1099511628032000</RegionHandle>",
            Vec("GroupPosition", 90.5f, 110f, 20.75f),
            Vec("OffsetPosition", 1f, 2f, 3f),
            Quat("RotationOffset", 0f, 0f, 1f, 0f),
            "<Description>about " + name + "</Description>",
            "<Color><R>0</R><G>0</G><B>0</B><A>255</A></Color>",
            "<Text>hover</Text>",
            "<LinkNum>1</LinkNum>",
            "<Shape><ProfileCurve>0</ProfileCurve><PathCurve>16</PathCurve><PCode>9</PCode><State>4</State>"
                + "<TextureEntry>Jul/tmngTLWZzgnV3j+nMAE4buIpI25J9oyD4de4oyPoAAAAAAAAAACAPwJmZn5BAAAAgD8ApAAAAAAAAgAAAAAAAAA=</TextureEntry></Shape>",
            Vec("Scale", 2f, 3f, 4f),
            $"<ParentID>{parentId}</ParentID>",
            "<CreationDate>1356051833</CreationDate>",
            "<SalePrice>10</SalePrice>",
            Uuid("OwnerID", OwnerId),
            "<OwnerMask>2147483647</OwnerMask>",
            "<Flags>None</Flags>"
        };

        private static string Part(params string[] elements) => "<SceneObjectPart>" + string.Concat(elements) + "</SceneObjectPart>";

        private static AssetPrim Decode(string xml)
        {
            var prim = new AssetPrim();
            Assert.That(prim.DecodeXml(xml), Is.True, "DecodeXml");
            return prim;
        }

        [Test]
        public void ASinglePartGroup_WithNoTaskInventory_Loads()
        {
            // The shape of the 2012 OpenSim files: no TaskInventory, no ObjectFlags, Flags a word
            var prim = Decode("<SceneObjectGroup>" + Part(PartElements(RootId, "Primitive", 2694560731, 0)) + "</SceneObjectGroup>");
            var root = prim.Parent;

            Assert.Multiple(() =>
            {
                Assert.That(root.ID, Is.EqualTo(RootId));
                Assert.That(root.Name, Is.EqualTo("Primitive"));
                Assert.That(root.Description, Is.EqualTo("about Primitive"));
                Assert.That(root.LocalID, Is.EqualTo(2694560731u), "more than an int holds");
                Assert.That(root.CreatorID, Is.EqualTo(OwnerId));
                Assert.That(root.OwnerID, Is.EqualTo(OwnerId));
                Assert.That(root.RegionHandle, Is.EqualTo(1099511628032000ul));
                Assert.That(root.Material, Is.EqualTo(3));
                Assert.That(root.Position, Is.EqualTo(new Vector3(90.5f, 110f, 20.75f)), "a root is placed by the group position");
                Assert.That(root.Rotation, Is.EqualTo(new Quaternion(0f, 0f, 1f, 0f)));
                Assert.That(root.Scale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(root.Text, Is.EqualTo("hover"));
                Assert.That(root.PCode, Is.EqualTo(9));
                Assert.That(root.State, Is.EqualTo(4));
                Assert.That(root.Shape.PathCurve, Is.EqualTo(16));
                Assert.That(root.PermsOwner, Is.EqualTo(2147483647u));
                Assert.That(root.SalePrice, Is.EqualTo(10));
                Assert.That(root.CreationDate, Is.EqualTo(Utils.UnixTimeToDateTime(1356051833)));
                Assert.That(root.Textures, Is.Not.Null);
                Assert.That(root.Inventory.Items, Is.Empty);
                Assert.That(root.UsePhysics || root.Phantom, Is.False);
                Assert.That(prim.Children, Is.Empty);
                Assert.That(prim.AssetID, Is.EqualTo(RootId));
            });
        }

        [Test]
        public void TheElementsCanComeInAnyOrder()
        {
            var forward = Decode("<SceneObjectGroup>" + Part(PartElements(RootId, "R", 7, 0)) + "</SceneObjectGroup>").Parent;
            var reversed = Decode("<SceneObjectGroup>" + Part(Enumerable.Reverse(PartElements(RootId, "R", 7, 0)).ToArray()) + "</SceneObjectGroup>").Parent;

            Assert.Multiple(() =>
            {
                Assert.That(reversed.ID, Is.EqualTo(forward.ID));
                Assert.That(reversed.Name, Is.EqualTo(forward.Name));
                Assert.That(reversed.Position, Is.EqualTo(forward.Position));
                Assert.That(reversed.Scale, Is.EqualTo(forward.Scale));
                Assert.That(reversed.Rotation, Is.EqualTo(forward.Rotation));
                Assert.That(reversed.Shape.PathCurve, Is.EqualTo(forward.Shape.PathCurve));
                Assert.That(reversed.PCode, Is.EqualTo(forward.PCode));
                Assert.That(reversed.Textures, Is.Not.Null);
                Assert.That(reversed.CreationDate, Is.EqualTo(forward.CreationDate));
            });
        }

        [Test]
        public void ChildrenInOtherParts_AreChildrenPlacedByTheirOffset()
        {
            var xml = "<SceneObjectGroup>" + Part(PartElements(RootId, "Root", 100, 0))
                      + "<OtherParts>" + Part(PartElements(ChildId, "Child", 101, 100)) + "</OtherParts></SceneObjectGroup>";
            var prim = Decode(xml);

            Assert.Multiple(() =>
            {
                Assert.That(prim.Parent.Name, Is.EqualTo("Root"));
                Assert.That(prim.Children, Has.Count.EqualTo(1));
                Assert.That(prim.Children[0].ID, Is.EqualTo(ChildId));
                Assert.That(prim.Children[0].ParentID, Is.EqualTo(100u));
                Assert.That(prim.Children[0].Position, Is.EqualTo(new Vector3(1f, 2f, 3f)), "a child is placed by the offset position");
            });
        }

        [Test]
        public void ARootPartElement_HoldsTheRootAndTheRestFollowInOtherParts()
        {
            // The layout of later OpenSim versions
            var xml = "<SceneObjectGroup><RootPart>" + Part(PartElements(RootId, "Root", 100, 0)) + "</RootPart>"
                      + "<OtherParts>" + Part(PartElements(ChildId, "Child", 101, 100))
                      + Part(PartElements(UUID.Random(), "Other", 102, 100)) + "</OtherParts>"
                      + "<GroupScriptStates /></SceneObjectGroup>";
            var prim = Decode(xml);

            Assert.Multiple(() =>
            {
                Assert.That(prim.Parent.Name, Is.EqualTo("Root"));
                Assert.That(prim.Children.Select(c => c.Name), Is.EqualTo(new[] { "Child", "Other" }));
            });
        }

        [Test]
        public void ADeclaredDocumentAndWhiteSpaceBetweenElements_AreFine()
        {
            var xml = "<?xml version=\"1.0\"?>\r\n<SceneObjectGroup>\r\n  "
                      + Part(PartElements(RootId, "Spaced", 1, 0).Select(e => "\r\n    " + e).ToArray()) + "\r\n</SceneObjectGroup>";

            Assert.That(Decode(xml).Parent.Name, Is.EqualTo("Spaced"));
        }

        [Test]
        public void TaskInventoryItems_AreRead_AndPermsMaskIsNotTheBasePermissions()
        {
            var item = "<TaskInventoryItem>" + Uuid("AssetID", ChildId) + "<BasePermissions>2147483647</BasePermissions>"
                       + "<CreationDate>1356051833</CreationDate>" + Uuid("CreatorID", OwnerId) + "<Description>a script</Description>"
                       + "<EveryonePermissions>0</EveryonePermissions><Flags>0</Flags>" + Uuid("GroupID", UUID.Zero)
                       + "<GroupPermissions>1</GroupPermissions><InvType>10</InvType>" + Uuid("ItemID", UUID.Parse("44444444-4444-4444-4444-444444444444"))
                       + Uuid("OldItemID", UUID.Zero) + Uuid("LastOwnerID", OwnerId) + "<Name>New Script</Name>"
                       + "<NextPermissions>581632</NextPermissions>" + Uuid("OwnerID", OwnerId)
                       + "<CurrentPermissions>2147483647</CurrentPermissions>" + Uuid("ParentID", RootId) + Uuid("ParentPartID", RootId)
                       + Uuid("PermsGranter", UUID.Zero) + "<PermsMask>77</PermsMask><Type>10</Type><OwnerChanged>false</OwnerChanged></TaskInventoryItem>";
            var elements = PartElements(RootId, "Scripted", 5, 0).ToList();
            elements.Insert(3, "<InventorySerial>6</InventorySerial><TaskInventory>" + item + "</TaskInventory>");

            var root = Decode("<SceneObjectGroup>" + Part(elements.ToArray()) + "</SceneObjectGroup>").Parent;

            Assert.Multiple(() =>
            {
                Assert.That(root.Inventory.Serial, Is.EqualTo(6));
                Assert.That(root.Inventory.Items, Has.Length.EqualTo(1));
                var i = root.Inventory.Items[0];
                Assert.That(i.Name, Is.EqualTo("New Script"));
                Assert.That(i.Description, Is.EqualTo("a script"));
                Assert.That(i.AssetID, Is.EqualTo(ChildId));
                Assert.That(i.ID, Is.EqualTo(UUID.Parse("44444444-4444-4444-4444-444444444444")));
                Assert.That(i.Type, Is.EqualTo(AssetType.LSLText));
                Assert.That(i.InvType, Is.EqualTo(InventoryType.LSL));
                Assert.That(i.PermsBase, Is.EqualTo(2147483647u), "PermsMask is what a script was granted, not the base permissions");
                Assert.That(i.PermsOwner, Is.EqualTo(2147483647u));
                Assert.That(i.PermsNextOwner, Is.EqualTo(581632u));
                Assert.That(i.PermsGroup, Is.EqualTo(1u));
                Assert.That(i.CreationDate, Is.EqualTo(Utils.UnixTimeToDateTime(1356051833)));
            });
        }

        [Test]
        public void TheHoverTextColor_IsBytesInOpenSim_AndFractionsWhenWrittenWithADecimalPoint()
        {
            Color4 Read(string color) => Decode("<SceneObjectGroup>" + Part(Uuid("UUID", RootId), color) + "</SceneObjectGroup>").Parent.TextColor;

            var bytes = Read("<Color><R>255</R><G>128</G><B>0</B><A>255</A></Color>");
            var fractions = Read("<Color><R>1.0</R><G>0.5</G><B>0.0</B><A>1.0</A></Color>");
            var ones = Read("<Color><R>1</R><G>1</G><B>1</B><A>1</A></Color>");

            Assert.Multiple(() =>
            {
                Assert.That(bytes.R, Is.EqualTo(1f));
                Assert.That(bytes.G, Is.EqualTo(128f / 255f).Within(1e-6f));
                Assert.That(bytes.B, Is.EqualTo(0f));
                Assert.That(bytes.A, Is.EqualTo(1f));
                Assert.That(fractions.G, Is.EqualTo(0.5f));
                Assert.That(ones.R, Is.EqualTo(1f / 255f).Within(1e-6f), "a byte of 1 is a byte of 1");
            });
        }

        [Test]
        public void TheFlags_AreReadFromWordsOrFromObjectFlags()
        {
            PrimObject Read(string flags) => Decode("<SceneObjectGroup>" + Part(Uuid("UUID", RootId), flags) + "</SceneObjectGroup>").Parent;

            var named = Read("<Flags>Phantom, Physics</Flags>");
            var numeric = Read($"<ObjectFlags>{(int)(PrimFlags.Temporary | PrimFlags.DieAtEdge)}</ObjectFlags><Flags>None</Flags>");
            var unknown = Read("<Flags>NotAFlag</Flags>");

            Assert.Multiple(() =>
            {
                Assert.That(named.Phantom && named.UsePhysics, Is.True);
                Assert.That(named.Temporary, Is.False);
                Assert.That(numeric.Temporary && numeric.DieAtEdge, Is.True, "ObjectFlags is what this library writes, and wins over Flags");
                Assert.That(numeric.Phantom, Is.False);
                Assert.That(unknown.Phantom || unknown.UsePhysics || unknown.Temporary, Is.False);
            });
        }

        [Test]
        public void ShapeSculptFlexibleAndLight_AreRead()
        {
            var shape = "<Shape><ProfileCurve>1</ProfileCurve><ProfileShape>Square</ProfileShape><HollowShape>Circle</HollowShape>"
                        + Uuid("SculptTexture", ChildId) + "<SculptType>5</SculptType>"
                        + "<FlexiSoftness>2</FlexiSoftness><FlexiTension>0.5</FlexiTension><FlexiDrag>1.5</FlexiDrag><FlexiGravity>0.25</FlexiGravity><FlexiWind>0.75</FlexiWind>"
                        + "<FlexiForceX>1</FlexiForceX><FlexiForceY>2</FlexiForceY><FlexiForceZ>3</FlexiForceZ>"
                        + "<LightColorR>1</LightColorR><LightColorG>0.5</LightColorG><LightColorB>0.25</LightColorB><LightColorA>1</LightColorA>"
                        + "<LightRadius>10</LightRadius><LightCutoff>0.5</LightCutoff><LightFalloff>0.75</LightFalloff><LightIntensity>0.9</LightIntensity>"
                        + "<FlexiEntry>true</FlexiEntry><LightEntry>true</LightEntry><SculptEntry>true</SculptEntry></Shape>";
            var root = Decode("<SceneObjectGroup>" + Part(Uuid("UUID", RootId), shape) + "</SceneObjectGroup>").Parent;

            Assert.Multiple(() =>
            {
                Assert.That(root.Shape.ProfileCurve, Is.EqualTo((int)AssetPrim.ProfileShape.Square | (int)HoleType.Circle), "from the shape and the hole");
                Assert.That(root.Sculpt, Is.Not.Null);
                Assert.That(root.Sculpt.Texture, Is.EqualTo(ChildId));
                Assert.That(root.Sculpt.Type, Is.EqualTo(5));
                Assert.That(root.Flexible, Is.Not.Null);
                Assert.That(root.Flexible.Softness, Is.EqualTo(2));
                Assert.That(root.Flexible.Tension, Is.EqualTo(0.5f));
                Assert.That(root.Flexible.Force, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(root.Light, Is.Not.Null);
                Assert.That(root.Light.Color, Is.EqualTo(new Color4(1f, 0.5f, 0.25f, 1f)));
                Assert.That(root.Light.Radius, Is.EqualTo(10f));
                Assert.That(root.Light.Intensity, Is.EqualTo(0.9f));
            });
        }

        [Test]
        public void FlexibleAndLightStayAsTheyWere_WhenTheEntryIsFalse()
        {
            // OpenSim writes the values of a flexible or light block whether the prim has one or not
            var shape = "<Shape><FlexiEntry>false</FlexiEntry><LightEntry>false</LightEntry><FlexiSoftness>2</FlexiSoftness><LightRadius>9</LightRadius></Shape>";
            var root = Decode("<SceneObjectGroup>" + Part(Uuid("UUID", RootId), shape) + "</SceneObjectGroup>").Parent;

            Assert.Multiple(() =>
            {
                Assert.That(root.Flexible.Softness, Is.EqualTo(0));
                Assert.That(root.Light.Radius, Is.EqualTo(0f));
                Assert.That(root.Sculpt.Texture, Is.EqualTo(UUID.Zero));
            });
        }

        [Test]
        public void EmptyAndUnknownElements_AreSkipped()
        {
            var xml = "<SceneObjectGroup>" + Part(Uuid("UUID", RootId), "<Name />", "<Description />",
                          "<SomethingNew><Nested><Deeper /></Nested>text</SomethingNew>", "<PayPrice0>-2</PayPrice0>",
                          "<TextureAnimation />", "<Text>after</Text>") + "<GroupScriptStates><ScriptState Engine=\"XEngine\" /></GroupScriptStates></SceneObjectGroup>";
            var root = Decode(xml).Parent;

            Assert.Multiple(() =>
            {
                Assert.That(root.ID, Is.EqualTo(RootId));
                Assert.That(root.Name, Is.EqualTo(string.Empty));
                Assert.That(root.Description, Is.EqualTo(string.Empty));
                Assert.That(root.Text, Is.EqualTo("after"));
            });
        }

        [Test]
        public void AMissingRotation_IsTheIdentity_AndAMissingTextureEntryIsNull()
        {
            var root = Decode("<SceneObjectGroup>" + Part(Uuid("UUID", RootId)) + "</SceneObjectGroup>").Parent;

            Assert.Multiple(() =>
            {
                Assert.That(root.Rotation, Is.EqualTo(Quaternion.Identity));
                Assert.That(root.SitRotation, Is.EqualTo(Quaternion.Identity));
                Assert.That(root.Textures, Is.Null);
            });
        }

        [Test]
        public void ANumberThatIsNotValid_IsLeftAtItsDefault()
        {
            var root = Decode("<SceneObjectGroup>" + Part(Uuid("UUID", RootId), "<Material>x</Material><SalePrice>12</SalePrice><LocalId>1.9</LocalId>") + "</SceneObjectGroup>").Parent;

            Assert.Multiple(() =>
            {
                Assert.That(root.Material, Is.EqualTo(0));
                Assert.That(root.SalePrice, Is.EqualTo(12));
                Assert.That(root.LocalID, Is.EqualTo(1u));
            });
        }

        [Test]
        public void ADocumentWithNoParts_IsNotDecoded()
        {
            Assert.That(new AssetPrim().DecodeXml("<SceneObjectGroup><OtherParts /></SceneObjectGroup>"), Is.False);
        }

        [Test]
        public void ADocumentThatIsNotAGroup_Throws()
        {
            Assert.Throws<XmlException>(() => new AssetPrim().DecodeXml("<Something />"));
        }

        [Test]
        public void LoadPrim_ReadsOnePartAndLeavesTheReaderAfterIt()
        {
            using (var reader = new XmlTextReader(new StringReader("<a>" + Part(Uuid("UUID", RootId), "<Name>one</Name>")
                                                                  + Part(Uuid("UUID", ChildId), "<Name>two</Name>") + "</a>")))
            {
                reader.Read();
                reader.Read();
                var one = AssetPrim.LoadPrim(reader);
                var two = AssetPrim.LoadPrim(reader);

                Assert.Multiple(() =>
                {
                    Assert.That(one.Name, Is.EqualTo("one"));
                    Assert.That(two.Name, Is.EqualTo("two"));
                    Assert.That(reader.NodeType, Is.EqualTo(XmlNodeType.EndElement));
                    Assert.That(reader.Name, Is.EqualTo("a"));
                });
            }
        }

        [Test]
        public void LoadPrim_OnTheWrongElement_Throws()
        {
            using (var reader = new XmlTextReader(new StringReader("<Other />")))
            {
                reader.Read();
                Assert.Throws<XmlException>(() => AssetPrim.LoadPrim(reader));
            }
        }

        [Test]
        public void AWrittenLinkset_IsReadBackAsWritten()
        {
            var parent = new PrimObject
            {
                ID = RootId, Name = "Root", Description = "the root", CreatorID = OwnerId, OwnerID = OwnerId, GroupID = ChildId,
                LocalID = 4000000000u, ParentID = 0, Position = new Vector3(128.5f, 64.25f, 25f), Rotation = Quaternion.Normalize(new Quaternion(0f, 0.5f, 0f, 0.5f)),
                Scale = new Vector3(1.5f, 2.5f, 3.5f), Material = 4, PCode = 9, State = 2, RegionHandle = 1099511628032000ul,
                RemoteScriptAccessPIN = 1234, LinkNumber = 1, ClickAction = 2, SalePrice = 99, SaleType = 1,
                Text = "hover", SitName = "sit", TouchName = "touch", TextColor = new Color4(1f, 0f, 128f / 255f, 64f / 255f),
                UsePhysics = true, Temporary = true, CreationDate = Utils.UnixTimeToDateTime(1356051833),
                Velocity = new Vector3(1f, 2f, 3f), AngularVelocity = new Vector3(4f, 5f, 6f), Acceleration = new Vector3(7f, 8f, 9f),
                SitOffset = new Vector3(0.5f, 0.25f, 0.125f), SitRotation = Quaternion.Identity,
                Textures = new Primitive.TextureEntry(UUID.Parse("5748decc-f629-461c-9a36-a35a221fe21f"))
            };
            parent.Shape.PathCurve = 32;
            parent.Shape.ProfileCurve = 0x01 | 0x20;
            parent.Shape.PathBegin = 0.25f;
            parent.Shape.PathEnd = 0.75f;
            parent.Shape.ProfileHollow = 0.5f;
            parent.Inventory.Serial = 3;
            parent.Inventory.Items = new[]
            {
                new PrimObject.InventoryBlock.ItemBlock
                {
                    ID = ChildId, AssetID = RootId, Name = "Item", Description = "an item", OwnerID = OwnerId, CreatorID = OwnerId,
                    Type = AssetType.Notecard, InvType = InventoryType.Notecard, PermsBase = 2147483647u, PermsOwner = 2147483647u,
                    PermsNextOwner = 581632u, PermsGroup = 0, PermsEveryone = 0, Flags = 0, CreationDate = Utils.UnixTimeToDateTime(1356051833)
                }
            };

            var child = new PrimObject { ID = ChildId, Name = "Child", LocalID = 4000000001u, ParentID = 4000000000u, Position = new Vector3(1f, 2f, 3f), Rotation = Quaternion.Identity, Scale = Vector3.One };
            var written = new AssetPrim(parent, new System.Collections.Generic.List<PrimObject> { child });

            var read = Decode(written.EncodeXml());
            var p = read.Parent;

            Assert.Multiple(() =>
            {
                Assert.That(p.ID, Is.EqualTo(parent.ID));
                Assert.That(p.Name, Is.EqualTo("Root"));
                Assert.That(p.Description, Is.EqualTo("the root"));
                Assert.That(p.LocalID, Is.EqualTo(4000000000u));
                Assert.That(p.Position, Is.EqualTo(parent.Position));
                Assert.That(p.Rotation.X, Is.EqualTo(parent.Rotation.X).Within(1e-6f));
                Assert.That(p.Rotation.W, Is.EqualTo(parent.Rotation.W).Within(1e-6f));
                Assert.That(p.Scale, Is.EqualTo(parent.Scale));
                Assert.That(p.Material, Is.EqualTo(4));
                Assert.That(p.PCode, Is.EqualTo(9));
                Assert.That(p.State, Is.EqualTo(2));
                Assert.That(p.RegionHandle, Is.EqualTo(parent.RegionHandle));
                Assert.That(p.RemoteScriptAccessPIN, Is.EqualTo(1234));
                Assert.That(p.SalePrice, Is.EqualTo(99));
                Assert.That(p.SaleType, Is.EqualTo(1));
                Assert.That(p.Text, Is.EqualTo("hover"));
                Assert.That(p.SitName, Is.EqualTo("sit"));
                Assert.That(p.TouchName, Is.EqualTo("touch"));
                Assert.That(p.UsePhysics, Is.True);
                Assert.That(p.Temporary, Is.True);
                Assert.That(p.Phantom, Is.False);
                Assert.That(p.CreationDate, Is.EqualTo(parent.CreationDate));
                Assert.That(p.Velocity, Is.EqualTo(parent.Velocity));
                Assert.That(p.AngularVelocity, Is.EqualTo(parent.AngularVelocity));
                Assert.That(p.Acceleration, Is.EqualTo(parent.Acceleration));
                Assert.That(p.SitOffset, Is.EqualTo(parent.SitOffset));
                Assert.That(p.OwnerID, Is.EqualTo(OwnerId));
                Assert.That(p.GroupID, Is.EqualTo(ChildId));
                Assert.That(p.Shape.PathCurve, Is.EqualTo(32));
                Assert.That(p.Shape.ProfileCurve, Is.EqualTo(0x01 | 0x20));
                Assert.That(p.Shape.PathBegin, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(p.Shape.PathEnd, Is.EqualTo(0.75f).Within(0.01f));
                Assert.That(p.Shape.ProfileHollow, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(p.Textures, Is.Not.Null);
                Assert.That(p.Textures.DefaultTexture.TextureID, Is.EqualTo(UUID.Parse("5748decc-f629-461c-9a36-a35a221fe21f")));
                Assert.That(p.TextColor.R, Is.EqualTo(1f));
                Assert.That(p.TextColor.G, Is.EqualTo(0f));
                Assert.That(p.TextColor.B, Is.EqualTo(128f / 255f).Within(1e-6f));
                Assert.That(p.TextColor.A, Is.EqualTo(64f / 255f).Within(1e-6f), "alpha was written from the green component");
                Assert.That(p.Inventory.Serial, Is.EqualTo(3));
                Assert.That(p.Inventory.Items, Has.Length.EqualTo(1));
                Assert.That(p.Inventory.Items[0].Name, Is.EqualTo("Item"));
                Assert.That(p.Inventory.Items[0].PermsBase, Is.EqualTo(2147483647u));
                Assert.That(p.Inventory.Items[0].Type, Is.EqualTo(AssetType.Notecard));

                Assert.That(read.Children, Has.Count.EqualTo(1));
                Assert.That(read.Children[0].Name, Is.EqualTo("Child"));
                Assert.That(read.Children[0].ParentID, Is.EqualTo(4000000000u));
                Assert.That(read.Children[0].Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            });
        }

        private static PrimObject Written(PrimObject prim)
        {
            prim.ID = RootId;
            prim.Rotation = Quaternion.Identity;
            return Decode(new AssetPrim(prim, new System.Collections.Generic.List<PrimObject>()).EncodeXml()).Parent;
        }

        [Test]
        public void ASculptPrim_KeepsItsSculptTextureAndType()
        {
            var read = Written(new PrimObject { Sculpt = new PrimObject.SculptBlock { Texture = ChildId, Type = 5 } });

            Assert.Multiple(() =>
            {
                Assert.That(read.Sculpt.Texture, Is.EqualTo(ChildId));
                Assert.That(read.Sculpt.Type, Is.EqualTo(5));
            });
        }

        [Test]
        public void AFlexiblePrim_KeepsItsFlexibleSettings()
        {
            var read = Written(new PrimObject
            {
                Flexible = new PrimObject.FlexibleBlock { Softness = 2, Tension = 1.5f, Drag = 2.5f, Gravity = 0.25f, Wind = 3.75f, Force = new Vector3(1f, -2f, 0.5f) }
            });

            Assert.Multiple(() =>
            {
                Assert.That(read.Flexible.Softness, Is.EqualTo(2));
                Assert.That(read.Flexible.Tension, Is.EqualTo(1.5f));
                Assert.That(read.Flexible.Drag, Is.EqualTo(2.5f));
                Assert.That(read.Flexible.Gravity, Is.EqualTo(0.25f));
                Assert.That(read.Flexible.Wind, Is.EqualTo(3.75f));
                Assert.That(read.Flexible.Force, Is.EqualTo(new Vector3(1f, -2f, 0.5f)));
            });
        }

        [Test]
        public void APrimThatIsALight_KeepsItsLight()
        {
            var read = Written(new PrimObject
            {
                Light = new PrimObject.LightBlock { Color = new Color4(1f, 0.5f, 0.25f, 1f), Intensity = 0.75f, Radius = 10f, Falloff = 0.5f, Cutoff = 0.125f }
            });

            Assert.Multiple(() =>
            {
                Assert.That(read.Light.Color, Is.EqualTo(new Color4(1f, 0.5f, 0.25f, 1f)));
                Assert.That(read.Light.Intensity, Is.EqualTo(0.75f));
                Assert.That(read.Light.Radius, Is.EqualTo(10f));
                Assert.That(read.Light.Falloff, Is.EqualTo(0.5f));
                Assert.That(read.Light.Cutoff, Is.EqualTo(0.125f));
            });
        }

        [Test]
        public void APrimWithNoSculptFlexibleOrLight_SaysSo()
        {
            // The defaults of PrimObject are blocks of zeros, and OpenSim reads the entry flags to know what a prim has
            var xml = new AssetPrim(new PrimObject { ID = RootId }, new System.Collections.Generic.List<PrimObject>()).EncodeXml();
            var shape = System.Xml.Linq.XDocument.Parse(xml).Descendants("Shape").Single();

            Assert.Multiple(() =>
            {
                Assert.That(shape.Element("FlexiEntry").Value, Is.EqualTo("false"));
                Assert.That(shape.Element("LightEntry").Value, Is.EqualTo("false"));
                Assert.That(shape.Element("SculptEntry").Value, Is.EqualTo("false"));
                Assert.That(shape.Element("SculptTexture").Element("UUID").Value, Is.EqualTo(UUID.Zero.ToString()));
                Assert.That(shape.Element("LightColorA").Value, Is.EqualTo("1"), "as OpenSim writes an absent light");
            });
        }

        [Test]
        public void ASculptTextureOfZero_IsNotASculpt()
        {
            var read = Written(new PrimObject { Sculpt = new PrimObject.SculptBlock { Texture = UUID.Zero, Type = 5 } });

            Assert.That(read.Sculpt.Type, Is.EqualTo(0));
        }

        [Test]
        public void ACollisionSoundAndItsVolume_AreKept()
        {
            var sound = UUID.Parse("44444444-4444-4444-4444-444444444444");

            var read = Written(new PrimObject { CollisionSound = sound, CollisionSoundVolume = 0.75f });

            Assert.Multiple(() =>
            {
                Assert.That(read.CollisionSound, Is.EqualTo(sound));
                Assert.That(read.CollisionSoundVolume, Is.EqualTo(0.75f));
            });
        }

        [Test]
        public void ThePrimWithNoCollisionSound_IsWrittenAsOpenSimWritesIt_AfterFlagsAndBeforeTheAnimation()
        {
            var xml = new AssetPrim(new PrimObject { ID = RootId }, new System.Collections.Generic.List<PrimObject>()).EncodeXml();
            var part = System.Xml.Linq.XDocument.Parse(xml).Descendants("SceneObjectPart").Single();
            var names = part.Elements().Select(e => e.Name.LocalName).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(part.Element("CollisionSound").Element("UUID").Value, Is.EqualTo(UUID.Zero.ToString()));
                Assert.That(part.Element("CollisionSoundVolume").Value, Is.EqualTo("0"));
                Assert.That(names.IndexOf("CollisionSound"), Is.GreaterThan(names.IndexOf("Flags")));
                Assert.That(names.IndexOf("CollisionSoundVolume"), Is.EqualTo(names.IndexOf("CollisionSound") + 1));
                Assert.That(names.IndexOf("TextureAnimation"), Is.EqualTo(names.IndexOf("CollisionSoundVolume") + 1));
            });
        }
    }
}
