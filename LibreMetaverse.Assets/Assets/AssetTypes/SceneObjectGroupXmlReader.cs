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
using System.Globalization;
using System.Xml;

namespace LibreMetaverse.Assets
{
    /// <summary>
    /// Reads the <c>SceneObjectGroup</c> XML that OpenSim writes into region archives (and that
    /// <see cref="SceneObjectGroupXmlWriter"/> writes). Elements are matched by name, in any order,
    /// and the ones that are missing keep their defaults: OpenSim leaves out elements such as
    /// <c>TaskInventory</c> when they are empty, and the order has differed between versions.
    /// Elements this library has no use for are skipped.
    /// </summary>
    internal static class SceneObjectGroupXmlReader
    {
        /// <summary>
        /// Reads every <c>SceneObjectPart</c> in a <c>SceneObjectGroup</c> document, in document order.
        /// The first is the root; the others are its children, whether they sit directly in the
        /// group, in an <c>OtherParts</c> element, or the root is in a <c>RootPart</c> element.
        /// </summary>
        public static List<PrimObject> ReadParts(XmlTextReader reader)
        {
            reader.MoveToContent();
            reader.ReadStartElement("SceneObjectGroup");

            var parts = new List<PrimObject>();
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "SceneObjectPart")
                    parts.Add(ReadPart(reader));
                else
                    reader.Read();
            }

            return parts;
        }

        /// <summary>
        /// Reads one <c>SceneObjectPart</c> element. The reader ends up after its end tag.
        /// </summary>
        public static PrimObject ReadPart(XmlTextReader reader)
        {
            reader.MoveToContent();
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "SceneObjectPart")
                throw new XmlException($"Expected element 'SceneObjectPart' but found '{reader.Name}'.");

            var obj = new PrimObject
            {
                Shape = new PrimObject.ShapeBlock(),
                Inventory = new PrimObject.InventoryBlock(),
                Rotation = Quaternion.Identity,
                SitRotation = Quaternion.Identity
            };

            Vector3 groupPosition = Vector3.Zero, offsetPosition = Vector3.Zero;
            Vector3? partScale = null, shapeScale = null;
            PrimFlags? objectFlags = null, namedFlags = null;
            UUID sculptTexture = UUID.Zero;
            int sculptType = 0;
            bool hasFlexi = false, hasLight = false;
            var flexible = new PrimObject.FlexibleBlock();
            var light = new PrimObject.LightBlock();
            float lightR = 0, lightG = 0, lightB = 0, lightA = 0;
            float flexForceX = 0, flexForceY = 0, flexForceZ = 0;
            int profileCurve = 0;
            AssetPrim.ProfileShape? profileShape = null;
            HoleType? hollowShape = null;

            ReadChildren(reader, name =>
            {
                switch (name)
                {
                    case "AllowedDrop": obj.AllowedDrop = ParseBool(Text(reader)); break;
                    case "CreatorID": obj.CreatorID = ReadUuid(reader); break;
                    case "FolderID": obj.FolderID = ReadUuid(reader); break;
                    case "InventorySerial": obj.Inventory.Serial = (int)ParseLong(Text(reader)); break;
                    case "TaskInventory": obj.Inventory.Items = ReadTaskInventory(reader); break;
                    case "ObjectFlags": objectFlags = (PrimFlags)(uint)ParseLong(Text(reader)); break;
                    case "Flags":
                        if (Enum.TryParse(Text(reader).Trim(), true, out PrimFlags named))
                            namedFlags = named;
                        break;
                    case "UUID": obj.ID = ReadUuid(reader); break;
                    case "LocalId": obj.LocalID = (uint)ParseLong(Text(reader)); break;
                    case "Name": obj.Name = Text(reader); break;
                    case "Material": obj.Material = (int)ParseLong(Text(reader)); break;
                    case "PassTouches": obj.PassTouches = ParseBool(Text(reader)); break;
                    case "RegionHandle": obj.RegionHandle = (ulong)ParseLong(Text(reader)); break;
                    case "ScriptAccessPin": obj.RemoteScriptAccessPIN = (int)ParseLong(Text(reader)); break;
                    case "GroupPosition": groupPosition = ReadVector(reader); break;
                    case "OffsetPosition": offsetPosition = ReadVector(reader); break;
                    case "RotationOffset": obj.Rotation = ReadQuaternion(reader); break;
                    case "Velocity": obj.Velocity = ReadVector(reader); break;
                    case "AngularVelocity": obj.AngularVelocity = ReadVector(reader); break;
                    case "Acceleration": obj.Acceleration = ReadVector(reader); break;
                    case "Description": obj.Description = Text(reader); break;
                    case "Color": obj.TextColor = ReadTextColor(reader); break;
                    case "Text": obj.Text = Text(reader); break;
                    case "SitName": obj.SitName = Text(reader); break;
                    case "TouchName": obj.TouchName = Text(reader); break;
                    case "LinkNum": obj.LinkNumber = (int)ParseLong(Text(reader)); break;
                    case "ClickAction": obj.ClickAction = (int)ParseLong(Text(reader)); break;
                    case "Scale": partScale = ReadVector(reader); break;
                    case "SitTargetPositionLL": obj.SitOffset = ReadVector(reader); break;
                    case "SitTargetOrientationLL": obj.SitRotation = ReadQuaternion(reader); break;
                    case "ParentID": obj.ParentID = (uint)ParseLong(Text(reader)); break;
                    case "CreationDate": obj.CreationDate = Utils.UnixTimeToDateTime(ParseUnixTime(Text(reader))); break;
                    case "SalePrice": obj.SalePrice = (int)ParseLong(Text(reader)); break;
                    case "ObjectSaleType": obj.SaleType = (int)ParseLong(Text(reader)); break;
                    case "GroupID": obj.GroupID = ReadUuid(reader); break;
                    case "OwnerID": obj.OwnerID = ReadUuid(reader); break;
                    case "LastOwnerID": obj.LastOwnerID = ReadUuid(reader); break;
                    case "BaseMask": obj.PermsBase = (uint)ParseLong(Text(reader)); break;
                    case "OwnerMask": obj.PermsOwner = (uint)ParseLong(Text(reader)); break;
                    case "GroupMask": obj.PermsGroup = (uint)ParseLong(Text(reader)); break;
                    case "EveryoneMask": obj.PermsEveryone = (uint)ParseLong(Text(reader)); break;
                    case "NextOwnerMask": obj.PermsNextOwner = (uint)ParseLong(Text(reader)); break;
                    case "CollisionSound": obj.CollisionSound = ReadUuid(reader); break;
                    case "CollisionSoundVolume": obj.CollisionSoundVolume = ParseFloat(Text(reader)); break;
                    case "TextureAnimation": obj.TextureAnimation = ReadTextureAnimation(Text(reader)) ?? obj.TextureAnimation; break;
                    case "ParticleSystem": obj.Particles = ReadParticleSystem(Text(reader)) ?? obj.Particles; break;

                    case "Shape":
                        ReadChildren(reader, shapeName =>
                        {
                            switch (shapeName)
                            {
                                case "ProfileCurve": profileCurve = (int)ParseLong(Text(reader)); break;
                                case "TextureEntry": obj.Textures = ReadTextureEntry(Text(reader)); break;
                                case "PathBegin": obj.Shape.PathBegin = Primitive.UnpackBeginCut((ushort)ParseLong(Text(reader))); break;
                                case "PathCurve": obj.Shape.PathCurve = (int)ParseLong(Text(reader)); break;
                                case "PathEnd": obj.Shape.PathEnd = Primitive.UnpackEndCut((ushort)ParseLong(Text(reader))); break;
                                case "PathRadiusOffset": obj.Shape.PathRadiusOffset = Primitive.UnpackPathTwist((sbyte)ParseLong(Text(reader))); break;
                                case "PathRevolutions": obj.Shape.PathRevolutions = Primitive.UnpackPathRevolutions((byte)ParseLong(Text(reader))); break;
                                case "PathScaleX": obj.Shape.PathScaleX = Primitive.UnpackPathScale((byte)ParseLong(Text(reader))); break;
                                case "PathScaleY": obj.Shape.PathScaleY = Primitive.UnpackPathScale((byte)ParseLong(Text(reader))); break;
                                case "PathShearX": obj.Shape.PathShearX = Primitive.UnpackPathShear((sbyte)ParseLong(Text(reader))); break;
                                case "PathShearY": obj.Shape.PathShearY = Primitive.UnpackPathShear((sbyte)ParseLong(Text(reader))); break;
                                case "PathSkew": obj.Shape.PathSkew = Primitive.UnpackPathTwist((sbyte)ParseLong(Text(reader))); break;
                                case "PathTaperX": obj.Shape.PathTaperX = Primitive.UnpackPathTaper((sbyte)ParseLong(Text(reader))); break;
                                case "PathTaperY": obj.Shape.PathTaperY = Primitive.UnpackPathTaper((sbyte)ParseLong(Text(reader))); break;
                                case "PathTwist": obj.Shape.PathTwist = Primitive.UnpackPathTwist((sbyte)ParseLong(Text(reader))); break;
                                case "PathTwistBegin": obj.Shape.PathTwistBegin = Primitive.UnpackPathTwist((sbyte)ParseLong(Text(reader))); break;
                                case "PCode": obj.PCode = (int)ParseLong(Text(reader)); break;
                                case "ProfileBegin": obj.Shape.ProfileBegin = Primitive.UnpackBeginCut((ushort)ParseLong(Text(reader))); break;
                                case "ProfileEnd": obj.Shape.ProfileEnd = Primitive.UnpackEndCut((ushort)ParseLong(Text(reader))); break;
                                case "ProfileHollow": obj.Shape.ProfileHollow = Primitive.UnpackProfileHollow((ushort)ParseLong(Text(reader))); break;
                                case "Scale": shapeScale = ReadVector(reader); break;
                                case "State": obj.State = (byte)ParseLong(Text(reader)); break;
                                case "ProfileShape":
                                    if (Enum.TryParse(Text(reader).Trim(), true, out AssetPrim.ProfileShape ps))
                                        profileShape = ps;
                                    break;
                                case "HollowShape":
                                    if (Enum.TryParse(Text(reader).Trim(), true, out HoleType ht))
                                        hollowShape = ht;
                                    break;
                                case "SculptTexture": sculptTexture = ReadUuid(reader); break;
                                case "SculptType": sculptType = (int)ParseLong(Text(reader)); break;
                                case "FlexiSoftness": flexible.Softness = (int)ParseLong(Text(reader)); break;
                                case "FlexiTension": flexible.Tension = ParseFloat(Text(reader)); break;
                                case "FlexiDrag": flexible.Drag = ParseFloat(Text(reader)); break;
                                case "FlexiGravity": flexible.Gravity = ParseFloat(Text(reader)); break;
                                case "FlexiWind": flexible.Wind = ParseFloat(Text(reader)); break;
                                case "FlexiForceX": flexForceX = ParseFloat(Text(reader)); break;
                                case "FlexiForceY": flexForceY = ParseFloat(Text(reader)); break;
                                case "FlexiForceZ": flexForceZ = ParseFloat(Text(reader)); break;
                                case "LightColorR": lightR = ParseFloat(Text(reader)); break;
                                case "LightColorG": lightG = ParseFloat(Text(reader)); break;
                                case "LightColorB": lightB = ParseFloat(Text(reader)); break;
                                case "LightColorA": lightA = ParseFloat(Text(reader)); break;
                                case "LightRadius": light.Radius = ParseFloat(Text(reader)); break;
                                case "LightCutoff": light.Cutoff = ParseFloat(Text(reader)); break;
                                case "LightFalloff": light.Falloff = ParseFloat(Text(reader)); break;
                                case "LightIntensity": light.Intensity = ParseFloat(Text(reader)); break;
                                case "FlexiEntry": hasFlexi = ParseBool(Text(reader)); break;
                                case "LightEntry": hasLight = ParseBool(Text(reader)); break;
                                default: reader.Skip(); break;
                            }
                        });
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            });

            obj.Shape.ProfileCurve = profileShape.HasValue && hollowShape.HasValue
                ? (int)profileShape.Value | (int)hollowShape.Value
                : profileCurve;

            if (sculptTexture != UUID.Zero)
                obj.Sculpt = new PrimObject.SculptBlock { Texture = sculptTexture, Type = sculptType };

            if (hasFlexi)
            {
                flexible.Force = new Vector3(flexForceX, flexForceY, flexForceZ);
                obj.Flexible = flexible;
            }

            if (hasLight)
            {
                light.Color = new Color4(Utils.Clamp(lightR, 0f, 1f), Utils.Clamp(lightG, 0f, 1f),
                    Utils.Clamp(lightB, 0f, 1f), Utils.Clamp(lightA, 0f, 1f));
                obj.Light = light;
            }

            PrimFlags flags = objectFlags ?? namedFlags ?? PrimFlags.None;
            obj.UsePhysics = (flags & PrimFlags.Physics) != 0;
            obj.Phantom = (flags & PrimFlags.Phantom) != 0;
            obj.DieAtEdge = (flags & PrimFlags.DieAtEdge) != 0;
            obj.ReturnAtEdge = (flags & PrimFlags.ReturnAtEdge) != 0;
            obj.Temporary = (flags & PrimFlags.Temporary) != 0;
            obj.Sandbox = (flags & PrimFlags.Sandbox) != 0;

            obj.Scale = partScale ?? shapeScale ?? Vector3.Zero;
            obj.Position = obj.ParentID == 0 ? groupPosition : offsetPosition;

            return obj;
        }

        private static PrimObject.InventoryBlock.ItemBlock[] ReadTaskInventory(XmlTextReader reader)
        {
            var items = new List<PrimObject.InventoryBlock.ItemBlock>();

            ReadChildren(reader, name =>
            {
                if (name != "TaskInventoryItem")
                {
                    reader.Skip();
                    return;
                }

                var item = new PrimObject.InventoryBlock.ItemBlock();
                ReadChildren(reader, itemName =>
                {
                    switch (itemName)
                    {
                        case "AssetID": item.AssetID = ReadUuid(reader); break;
                        case "BasePermissions": item.PermsBase = (uint)ParseLong(Text(reader)); break;
                        case "CreationDate": item.CreationDate = Utils.UnixTimeToDateTime(ParseUnixTime(Text(reader))); break;
                        case "CreatorID": item.CreatorID = ReadUuid(reader); break;
                        case "Description": item.Description = Text(reader); break;
                        case "EveryonePermissions": item.PermsEveryone = (uint)ParseLong(Text(reader)); break;
                        case "Flags": item.Flags = (int)ParseLong(Text(reader)); break;
                        case "GroupID": item.GroupID = ReadUuid(reader); break;
                        case "GroupPermissions": item.PermsGroup = (uint)ParseLong(Text(reader)); break;
                        case "InvType": item.InvType = (InventoryType)(int)ParseLong(Text(reader)); break;
                        case "ItemID": item.ID = ReadUuid(reader); break;
                        case "LastOwnerID": item.LastOwnerID = ReadUuid(reader); break;
                        case "Name": item.Name = Text(reader); break;
                        case "NextPermissions": item.PermsNextOwner = (uint)ParseLong(Text(reader)); break;
                        case "OwnerID": item.OwnerID = ReadUuid(reader); break;
                        case "CurrentPermissions": item.PermsOwner = (uint)ParseLong(Text(reader)); break;
                        case "PermsGranter": item.PermsGranterID = ReadUuid(reader); break;
                        case "Type": item.Type = (AssetType)(int)ParseLong(Text(reader)); break;
                        default: reader.Skip(); break; // OldItemID, ParentID, ParentPartID, PermsMask, OwnerChanged
                    }
                });
                items.Add(item);
            });

            return items.ToArray();
        }

        /// <summary>
        /// Calls <paramref name="handle"/> for each child element of the current element, leaving the
        /// reader after the element's end tag. The handler must consume the element it is given.
        /// </summary>
        private static void ReadChildren(XmlTextReader reader, Action<string> handle)
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

                int line = reader.LineNumber, position = reader.LinePosition;
                handle(reader.Name);

                // A handler that left the reader on the same element would loop forever.
                if (reader.NodeType == XmlNodeType.Element && reader.LineNumber == line && reader.LinePosition == position)
                    reader.Skip();
            }

            if (reader.NodeType == XmlNodeType.EndElement)
                reader.Read();
        }

        private static string Text(XmlTextReader reader) => reader.ReadElementContentAsString();

        private static UUID ReadUuid(XmlTextReader reader)
        {
            UUID id = UUID.Zero;
            ReadChildren(reader, name =>
            {
                if (name == "UUID" || name == "Guid")
                    UUID.TryParse(Text(reader), out id);
                else
                    reader.Skip();
            });
            return id;
        }

        private static Vector3 ReadVector(XmlTextReader reader)
        {
            float x = 0, y = 0, z = 0;
            ReadChildren(reader, name =>
            {
                switch (name)
                {
                    case "X": x = ParseFloat(Text(reader)); break;
                    case "Y": y = ParseFloat(Text(reader)); break;
                    case "Z": z = ParseFloat(Text(reader)); break;
                    default: reader.Skip(); break;
                }
            });
            return new Vector3(x, y, z);
        }

        private static Quaternion ReadQuaternion(XmlTextReader reader)
        {
            float x = 0, y = 0, z = 0, w = 0;
            ReadChildren(reader, name =>
            {
                switch (name)
                {
                    case "X": x = ParseFloat(Text(reader)); break;
                    case "Y": y = ParseFloat(Text(reader)); break;
                    case "Z": z = ParseFloat(Text(reader)); break;
                    case "W": w = ParseFloat(Text(reader)); break;
                    default: reader.Skip(); break;
                }
            });
            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// The hover text color. OpenSim writes each component as a byte (0 to 255); a value with a
        /// decimal point is taken to be a fraction (0 to 1).
        /// </summary>
        private static Color4 ReadTextColor(XmlTextReader reader)
        {
            string r = "0", g = "0", b = "0", a = "0";
            ReadChildren(reader, name =>
            {
                switch (name)
                {
                    case "R": r = Text(reader); break;
                    case "G": g = Text(reader); break;
                    case "B": b = Text(reader); break;
                    case "A": a = Text(reader); break;
                    default: reader.Skip(); break;
                }
            });

            bool fractions = IsFraction(r) || IsFraction(g) || IsFraction(b) || IsFraction(a);
            float Channel(string s) => Utils.Clamp(fractions ? ParseFloat(s) : ParseFloat(s) / 255f, 0f, 1f);
            return new Color4(Channel(r), Channel(g), Channel(b), Channel(a));
        }

        private static bool IsFraction(string s) => s.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0;

        /// <summary>
        /// A texture animation, as the simulator sends it to viewers (16 bytes), in base64. An empty element is a prim without one.
        /// </summary>
        private static Primitive.TextureAnimation? ReadTextureAnimation(string base64)
        {
            byte[] data;
            try { data = Convert.FromBase64String(base64.Trim()); }
            catch (FormatException) { return null; }

            return data.Length >= 16 ? new Primitive.TextureAnimation(data, 0) : (Primitive.TextureAnimation?)null;
        }

        /// <summary>
        /// A particle system, as the simulator sends it to viewers, in base64. An empty element is a prim without one.
        /// </summary>
        private static PrimObject.ParticlesBlock? ReadParticleSystem(string base64)
        {
            byte[] data;
            try { data = Convert.FromBase64String(base64.Trim()); }
            catch (FormatException) { return null; }

            // The sizes of the legacy form and of the one with glow or blend functions after it; any other is not read
            if (data.Length != Primitive.ParticleSystem.LegacyDataBlockSize
                && !(data.Length > Primitive.ParticleSystem.LegacyDataBlockSize && data.Length <= Primitive.ParticleSystem.MaxDataBlockSize))
                return null;

            return PrimObject.ParticlesBlock.FromParticleSystem(new Primitive.ParticleSystem(data, 0));
        }

        private static Primitive.TextureEntry? ReadTextureEntry(string base64)
        {
            try
            {
                byte[] data = Convert.FromBase64String(base64.Trim());
                return data.Length == 0 ? null : new Primitive.TextureEntry(data, 0, data.Length);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static long ParseLong(string s)
        {
            s = s.Trim();
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                return l;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                && d > long.MinValue && d < long.MaxValue)
                return (long)d;
            return 0;
        }

        private static uint ParseUnixTime(string s)
        {
            long seconds = ParseLong(s);
            return seconds < 0 ? 0 : (uint)Math.Min(seconds, uint.MaxValue);
        }

        private static float ParseFloat(string s) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;

        private static bool ParseBool(string s)
        {
            s = s.Trim();
            return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
        }
    }
}
