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
using System.Xml;

namespace LibreMetaverse.Assets
{
    /// <summary>Writes a linkset in the XML form of an OpenSim <c>SceneObjectGroup</c></summary>
    public static class SceneObjectGroupXmlWriter
    {
        /// <summary>
        /// Writes a linkset as the <c>SceneObjectGroup</c> XML that OpenSim region archives (and
        /// <see cref="AssetPrim.EncodeXml"/>) use.
        /// </summary>
        /// <param name="writer">Where to write it</param>
        /// <param name="prim">The linkset</param>
        public static void Write(XmlTextWriter writer, AssetPrim prim)
        {
            writer.WriteStartElement(string.Empty, "SceneObjectGroup", string.Empty);
            if (prim.Parent != null)
            {
                WritePart(writer, prim.Parent, null);
            }
            writer.WriteStartElement(string.Empty, "OtherParts", string.Empty);

            if (prim.Children != null)
            {
                foreach (PrimObject child in prim.Children)
                    WritePart(writer, child, prim.Parent);
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        static void WritePart(XmlTextWriter writer, PrimObject prim, PrimObject? parent)
        {
            writer.WriteStartElement("SceneObjectPart");
            writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");

            WriteUUID(writer, "CreatorID", prim.CreatorID);
            WriteUUID(writer, "FolderID", prim.FolderID);
            writer.WriteElementString("InventorySerial", (prim.Inventory != null) ? prim.Inventory.Serial.ToString() : "0");
            
            // FIXME: Task inventory
            writer.WriteStartElement("TaskInventory");
            if (prim.Inventory != null)
            {
                foreach (PrimObject.InventoryBlock.ItemBlock item in prim.Inventory.Items)
                {
                    writer.WriteStartElement("", "TaskInventoryItem", "");

                    WriteUUID(writer, "AssetID", item.AssetID);
                    writer.WriteElementString("BasePermissions", item.PermsBase.ToString());
                    writer.WriteElementString("CreationDate", (item.CreationDate.ToUniversalTime() - Utils.Epoch).TotalSeconds.ToString(CultureInfo.InvariantCulture));
                    WriteUUID(writer, "CreatorID", item.CreatorID);
                    writer.WriteElementString("Description", item.Description);
                    writer.WriteElementString("EveryonePermissions", item.PermsEveryone.ToString());
                    writer.WriteElementString("Flags", item.Flags.ToString());
                    WriteUUID(writer, "GroupID", item.GroupID);
                    writer.WriteElementString("GroupPermissions", item.PermsGroup.ToString());
                    writer.WriteElementString("InvType", ((int)item.InvType).ToString());
                    WriteUUID(writer, "ItemID", item.ID);
                    WriteUUID(writer, "OldItemID", UUID.Zero);
                    WriteUUID(writer, "LastOwnerID", item.LastOwnerID);
                    writer.WriteElementString("Name", item.Name);
                    writer.WriteElementString("NextPermissions", item.PermsNextOwner.ToString());
                    WriteUUID(writer, "OwnerID", item.OwnerID);
                    writer.WriteElementString("CurrentPermissions", item.PermsOwner.ToString());
                    WriteUUID(writer, "ParentID", prim.ID);
                    WriteUUID(writer, "ParentPartID", prim.ID);
                    WriteUUID(writer, "PermsGranter", item.PermsGranterID);
                    writer.WriteElementString("PermsMask", "0");
                    writer.WriteElementString("Type", ((int)item.Type).ToString());
                    writer.WriteElementString("OwnerChanged", "false");

                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();

            PrimFlags flags = PrimFlags.None;
            if (prim.UsePhysics) flags |= PrimFlags.Physics;
            if (prim.Phantom) flags |= PrimFlags.Phantom;
            if (prim.DieAtEdge) flags |= PrimFlags.DieAtEdge;
            if (prim.ReturnAtEdge) flags |= PrimFlags.ReturnAtEdge;
            if (prim.Temporary) flags |= PrimFlags.Temporary;
            if (prim.Sandbox) flags |= PrimFlags.Sandbox;
            writer.WriteElementString("ObjectFlags", ((int)flags).ToString());

            WriteUUID(writer, "UUID", prim.ID);
            writer.WriteElementString("LocalId", prim.LocalID.ToString());
            writer.WriteElementString("Name", prim.Name);
            writer.WriteElementString("Material", (prim.Material).ToString());
            writer.WriteElementString("RegionHandle", prim.RegionHandle.ToString());
            writer.WriteElementString("ScriptAccessPin", prim.RemoteScriptAccessPIN.ToString());

            Vector3 groupPosition;
            if (parent == null)
                groupPosition = prim.Position;
            else
                groupPosition = parent.Position;

            WriteVector(writer, "GroupPosition", groupPosition);
            if (prim.ParentID == 0)
                WriteVector(writer, "OffsetPosition", Vector3.Zero);
            else
                WriteVector(writer, "OffsetPosition", prim.Position);
            WriteQuaternion(writer, "RotationOffset", prim.Rotation);
            WriteVector(writer, "Velocity", prim.Velocity);
            WriteVector(writer, "RotationalVelocity", Vector3.Zero);
            WriteVector(writer, "AngularVelocity", prim.AngularVelocity);
            WriteVector(writer, "Acceleration", prim.Acceleration);
            writer.WriteElementString("Description", prim.Description);
            writer.WriteStartElement("Color");
                // OpenSim writes each component as a byte, 0 to 255
                writer.WriteElementString("R", ColorByte(prim.TextColor.R));
                writer.WriteElementString("G", ColorByte(prim.TextColor.G));
                writer.WriteElementString("B", ColorByte(prim.TextColor.B));
                writer.WriteElementString("A", ColorByte(prim.TextColor.A));
            writer.WriteEndElement();
            writer.WriteElementString("Text", prim.Text);
            writer.WriteElementString("SitName", prim.SitName);
            writer.WriteElementString("TouchName", prim.TouchName);

            writer.WriteElementString("LinkNum", prim.LinkNumber.ToString());
            writer.WriteElementString("ClickAction", prim.ClickAction.ToString());
            // The order is the one OpenSim writes. OpenSim applies the elements as it meets them, and
            // ExtraParams, which is left empty here, comes before the elements that say what the prim has.
            writer.WriteStartElement("Shape");

            writer.WriteElementString("ProfileCurve", prim.Shape.ProfileCurve.ToString());

            writer.WriteStartElement("TextureEntry");

            byte[] te;
            if (prim.Textures != null)
                te = prim.Textures.GetBytes();
            else
                te = Utils.EmptyBytes;

            writer.WriteBase64(te, 0, te.Length);
            writer.WriteEndElement();

            writer.WriteStartElement("ExtraParams"); writer.WriteEndElement();

            writer.WriteElementString("PathBegin", Primitive.PackBeginCut(prim.Shape.PathBegin).ToString());
            writer.WriteElementString("PathCurve", prim.Shape.PathCurve.ToString());
            writer.WriteElementString("PathEnd", Primitive.PackEndCut(prim.Shape.PathEnd).ToString());
            writer.WriteElementString("PathRadiusOffset", Primitive.PackPathTwist(prim.Shape.PathRadiusOffset).ToString());
            writer.WriteElementString("PathRevolutions", Primitive.PackPathRevolutions(prim.Shape.PathRevolutions).ToString());
            writer.WriteElementString("PathScaleX", Primitive.PackPathScale(prim.Shape.PathScaleX).ToString());
            writer.WriteElementString("PathScaleY", Primitive.PackPathScale(prim.Shape.PathScaleY).ToString());
            writer.WriteElementString("PathShearX", ((byte)Primitive.PackPathShear(prim.Shape.PathShearX)).ToString());
            writer.WriteElementString("PathShearY", ((byte)Primitive.PackPathShear(prim.Shape.PathShearY)).ToString());
            writer.WriteElementString("PathSkew", Primitive.PackPathTwist(prim.Shape.PathSkew).ToString());
            writer.WriteElementString("PathTaperX", Primitive.PackPathTaper(prim.Shape.PathTaperX).ToString());
            writer.WriteElementString("PathTaperY", Primitive.PackPathTaper(prim.Shape.PathTaperY).ToString());
            writer.WriteElementString("PathTwist", Primitive.PackPathTwist(prim.Shape.PathTwist).ToString());
            writer.WriteElementString("PathTwistBegin", Primitive.PackPathTwist(prim.Shape.PathTwistBegin).ToString());
            writer.WriteElementString("PCode", prim.PCode.ToString());
            writer.WriteElementString("ProfileBegin", Primitive.PackBeginCut(prim.Shape.ProfileBegin).ToString());
            writer.WriteElementString("ProfileEnd", Primitive.PackEndCut(prim.Shape.ProfileEnd).ToString());
            writer.WriteElementString("ProfileHollow", Primitive.PackProfileHollow(prim.Shape.ProfileHollow).ToString());
            WriteVector(writer, "Scale", prim.Scale);
            writer.WriteElementString("State", prim.State.ToString());

            AssetPrim.ProfileShape shape = (AssetPrim.ProfileShape)(prim.Shape.ProfileCurve & 0x0F);
            HoleType hole = (HoleType)(prim.Shape.ProfileCurve & 0xF0);
            writer.WriteElementString("ProfileShape", shape.ToString());
            writer.WriteElementString("HollowShape", hole.ToString());

            bool hasSculpt = prim.Sculpt != null && prim.Sculpt.Texture != UUID.Zero;
            WriteUUID(writer, "SculptTexture", hasSculpt ? prim.Sculpt!.Texture : UUID.Zero);
            writer.WriteElementString("SculptType", (hasSculpt ? prim.Sculpt!.Type : 0).ToString());
            writer.WriteStartElement("SculptData"); writer.WriteEndElement();

            // OpenSim writes the values of a flexible or light block whether the prim has one or not
            bool hasFlexi = HasFlexible(prim.Flexible);
            PrimObject.FlexibleBlock flexi = hasFlexi ? prim.Flexible! : new PrimObject.FlexibleBlock();
            writer.WriteElementString("FlexiSoftness", flexi.Softness.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("FlexiTension", Float(flexi.Tension));
            writer.WriteElementString("FlexiDrag", Float(flexi.Drag));
            writer.WriteElementString("FlexiGravity", Float(flexi.Gravity));
            writer.WriteElementString("FlexiWind", Float(flexi.Wind));
            writer.WriteElementString("FlexiForceX", Float(flexi.Force.X));
            writer.WriteElementString("FlexiForceY", Float(flexi.Force.Y));
            writer.WriteElementString("FlexiForceZ", Float(flexi.Force.Z));

            bool hasLight = HasLight(prim.Light);
            PrimObject.LightBlock light = hasLight ? prim.Light! : new PrimObject.LightBlock { Color = new Color4(0f, 0f, 0f, 1f) };
            writer.WriteElementString("LightColorR", Float(light.Color.R));
            writer.WriteElementString("LightColorG", Float(light.Color.G));
            writer.WriteElementString("LightColorB", Float(light.Color.B));
            writer.WriteElementString("LightColorA", Float(light.Color.A));
            writer.WriteElementString("LightRadius", Float(light.Radius));
            writer.WriteElementString("LightCutoff", Float(light.Cutoff));
            writer.WriteElementString("LightFalloff", Float(light.Falloff));
            writer.WriteElementString("LightIntensity", Float(light.Intensity));

            writer.WriteElementString("FlexiEntry", hasFlexi ? "true" : "false");
            writer.WriteElementString("LightEntry", hasLight ? "true" : "false");
            writer.WriteElementString("SculptEntry", hasSculpt ? "true" : "false");

            writer.WriteEndElement();

            WriteVector(writer, "Scale", prim.Scale);
            writer.WriteElementString("UpdateFlag", "0");
            WriteVector(writer, "SitTargetOrientation", Vector3.UnitZ); // TODO: Is this really a vector and not a quaternion?
            WriteVector(writer, "SitTargetPosition", prim.SitOffset);
            WriteVector(writer, "SitTargetPositionLL", prim.SitOffset);
            WriteQuaternion(writer, "SitTargetOrientationLL", prim.SitRotation);
            writer.WriteElementString("ParentID", prim.ParentID.ToString());
            writer.WriteElementString("CreationDate", ((int)Utils.DateTimeToUnixTime(prim.CreationDate)).ToString());
            writer.WriteElementString("Category", "0");
            writer.WriteElementString("SalePrice", prim.SalePrice.ToString());
            writer.WriteElementString("ObjectSaleType", (prim.SaleType).ToString());
            writer.WriteElementString("OwnershipCost", "0");
            WriteUUID(writer, "GroupID", prim.GroupID);
            WriteUUID(writer, "OwnerID", prim.OwnerID);
            WriteUUID(writer, "LastOwnerID", prim.LastOwnerID);
            writer.WriteElementString("BaseMask", ((uint)PermissionMask.All).ToString());
            writer.WriteElementString("OwnerMask", ((uint)PermissionMask.All).ToString());
            writer.WriteElementString("GroupMask", ((uint)PermissionMask.All).ToString());
            writer.WriteElementString("EveryoneMask", ((uint)PermissionMask.All).ToString());
            writer.WriteElementString("NextOwnerMask", ((uint)PermissionMask.All).ToString());
            writer.WriteElementString("Flags", "None");
            WriteUUID(writer, "CollisionSound", prim.CollisionSound);
            writer.WriteElementString("CollisionSoundVolume", Float(prim.CollisionSoundVolume));
            WriteTextureAnimation(writer, prim.TextureAnimation);
            WriteParticleSystem(writer, prim.Particles);
            WriteUUID(writer, "SitTargetAvatar", UUID.Zero);

            writer.WriteEndElement();
        }

        // A block that is all zeros is what a prim without one has, and PrimObject starts with one of each
        static bool HasFlexible(PrimObject.FlexibleBlock? flexible) =>
            flexible != null && (flexible.Softness != 0 || flexible.Tension != 0f || flexible.Drag != 0f
                                 || flexible.Gravity != 0f || flexible.Wind != 0f || flexible.Force != Vector3.Zero);

        static bool HasLight(PrimObject.LightBlock? light) =>
            light != null && (light.Intensity != 0f || light.Radius != 0f || light.Cutoff != 0f || light.Falloff != 0f
                              || light.Color.R != 0f || light.Color.G != 0f || light.Color.B != 0f);

        static string Float(float value) => value.ToString(CultureInfo.InvariantCulture);

        // The texture animation as the simulator sends it to viewers (16 bytes), in base64; empty if the prim has none
        static void WriteTextureAnimation(XmlTextWriter writer, Primitive.TextureAnimation? animation)
        {
            writer.WriteStartElement("TextureAnimation");
            if (animation.HasValue)
            {
                byte[] data = animation.Value.GetBytes();
                writer.WriteBase64(data, 0, data.Length);
            }
            writer.WriteEndElement();
        }

        // The particle system as the simulator sends it to viewers, in base64; empty if the prim has none, as OpenSim writes it
        static void WriteParticleSystem(XmlTextWriter writer, PrimObject.ParticlesBlock? particles)
        {
            writer.WriteStartElement("ParticleSystem");
            if (particles != null && particles.IsSet)
            {
                byte[] data = particles.ToParticleSystem().GetBytes();
                writer.WriteBase64(data, 0, data.Length);
            }
            writer.WriteEndElement();
        }

        static string ColorByte(float component) =>
            ((int)Math.Round(Utils.Clamp(component, 0f, 1f) * 255f)).ToString(CultureInfo.InvariantCulture);

        static void WriteUUID(XmlTextWriter writer, string name, UUID id)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("UUID", id.ToString());
            writer.WriteEndElement();
        }

        static void WriteVector(XmlTextWriter writer, string name, Vector3 vec)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", vec.X.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Y", vec.Y.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Z", vec.Z.ToString(Utils.EnUsCulture));
            writer.WriteEndElement();
        }

        static void WriteQuaternion(XmlTextWriter writer, string name, Quaternion quat)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", quat.X.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Y", quat.Y.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Z", quat.Z.ToString(Utils.EnUsCulture));
            writer.WriteElementString("W", quat.W.ToString(Utils.EnUsCulture));
            writer.WriteEndElement();
        }
    }
}
