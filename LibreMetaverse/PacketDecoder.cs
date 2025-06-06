/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace OpenMetaverse.Packets
{
    public static class PacketDecoder
    {
        private static readonly Lazy<Dictionary<string, Func<string, object, string>>> Callbacks =
            new Lazy<Dictionary<string, Func<string, object, string>>>(() =>
            new Dictionary<string, Func<string, object, string>>
            {
                {"Color", DecodeColorField},
                {"TextColor", DecodeColorField},
                {"Timestamp", DecodeTimeStamp},
                {"EstateCovenantReply.Data.CovenantTimestamp", DecodeTimeStamp},
                {"CreationDate", DecodeTimeStamp},
                {"BinaryBucket", DecodeBinaryBucket},
                {"ParcelData.Data", DecodeBinaryToHexString},
                {"LayerData.Data", DecodeBinaryToHexString},
                {"ImageData.Data", DecodeImageData},
                {"TransferData.Data", DecodeBinaryToHexString},
                {"ObjectData.TextureEntry", DecodeTextureEntry},
                {"ImprovedInstantMessage.MessageBlock.Dialog", DecodeDialog},

                // Inventory/Permissions
                {"BaseMask", DecodePermissionMask},
                {"OwnerMask", DecodePermissionMask},
                {"EveryoneMask", DecodePermissionMask},
                {"NextOwnerMask", DecodePermissionMask},
                {"GroupMask", DecodePermissionMask},

                // FetchInventoryDescendents
                {"InventoryData.SortOrder", DecodeInventorySort},

                {"WearableType", DecodeWearableType},
                //
                {"InventoryData.Type", DecodeInventoryType},
                {"InvType", DecodeInventoryInvType},
                {"InventoryData.Flags", DecodeInventoryFlags},
                // BulkUpdateInventory
                {"ItemData.Type", DecodeInventoryType},
                {"ItemData.Flags", DecodeInventoryFlags},

                {"SaleType", DecodeObjectSaleType},

                {"ScriptControlChange.Data.Controls", DecodeScriptControls},

                {"RegionFlags", DecodeRegionFlags},
                {"SimAccess", DecodeSimAccess},
                {"ControlFlags", DecodeControlFlags},

                // AgentUpdate
                {"AgentUpdate.AgentData.State", DecodeAgentState},
                {"AgentUpdate.AgentData.Flags", DecodeAgentFlags},

                // ViewerEffect TypeData
                {"ViewerEffect.Effect.TypeData", DecodeViewerEffectTypeData},
                {"ViewerEffect.Effect.Type", DecodeViewerEffectType},

                // Prim/ObjectUpdate decoders
                {"ObjectUpdate.ObjectData.PCode", DecodeObjectPCode},
                {"ObjectUpdate.ObjectData.Material", DecodeObjectMaterial},
                {"ObjectUpdate.ObjectData.ClickAction", DecodeObjectClickAction},
                {"ObjectData.UpdateFlags", DecodeObjectUpdateFlags},

                {"ObjectUpdate.ObjectData.ObjectData", DecodeObjectData},
                {"TextureAnim", DecodeObjectTextureAnim},
                {"ObjectUpdate.ObjectData.NameValue", DecodeNameValue},
                {"ObjectUpdate.ObjectData.Data", DecodeObjectData},

                {"ObjectUpdate.ObjectData.PSBlock", DecodeObjectParticleSystem},
                {"ParticleSys", DecodeObjectParticleSystem},
                {"ObjectUpdate.ObjectData.ExtraParams", DecodeObjectExtraParams},

                {"ImprovedTerseObjectUpdate.ObjectData.Data", DecodeTerseUpdate},
                {"ImprovedTerseObjectUpdate.ObjectData.TextureEntry", DecodeTerseTextureEntry},

                {"ObjectUpdateCompressed.ObjectData.Data", DecodeObjectCompressedData},

                // ImprovedTerseObjectUpdate & ObjectUpdate AttachmentPoint & ObjectUpdateCompressed
                {"ObjectData.State", DecodeObjectState},
                //{ "ObjectUpdateCompressed.ObjectData.State", DecodeObjectState },
                //{ "ImprovedTerseObjectUpdate.ObjectData.State", DecodeObjectState },


                // ChatFromSimulator 
                {"ChatData.SourceType", DecodeChatSourceType},
                {"ChatData.ChatType", DecodeChatChatType},
                {"ChatData.Audible", DecodeChatAudible},
                {"AttachedSound.DataBlock.Flags", DecodeAttachedSoundFlags},

                {"RequestImage.Type", DecodeImageType},

                {"EstateOwnerMessage.ParamList.Parameter", DecodeEstateParameter},

                {"Codec", DecodeImageCodec},
                {"Info.TeleportFlags", DecodeTeleportFlags},

                // map
                {"MapBlockRequest.AgentData.Flags", DecodeMapRequestFlags},
                {"MapItemRequest.AgentData.Flags", DecodeMapRequestFlags},
                {"MapBlockReply.Data.Access", DecodeMapAccess},
                {"FolderData.Type", DecodeFolderType},
                {"RequestData.ItemType", DecodeGridItemType},

                // TransferRequest/TransferInfo
                {"TransferInfo.Params", DecodeTransferParams},
                {"TransferInfo.ChannelType", DecodeTransferChannelType},
                {"TransferInfo.SourceType", DecodeTransferSourceType},
                {"TransferInfo.TargetType", DecodeTransferTargetType},
                {"TransferData.ChannelType", DecodeTransferChannelType},
                // SendXferPacket
                {"DataPacket.Data", DecodeBinaryToHexString},
                // Directory Manager
                {"DirClassifiedQuery.QueryData.QueryFlags", DecodeDirClassifiedQueryFlags},
                {"QueryData.QueryFlags", DecodeDirQueryFlags},
                {"Category", DecodeCategory},
                {"QueryData.SearchType", SearchTypeFlags},

                {"ClassifiedFlags", DecodeDirClassifiedFlags},
                {"EventFlags", DecodeEventFlags},

                {"ParcelAccessListRequest.Data.Flags", DecodeParcelAcl},
                {"ParcelAccessListReply.Data.Flags", DecodeParcelAcl},
                //{ "ParcelAccessListReply.List.Flags", DecodeParcelACLReply },

                // AgentAnimation
                {"AnimID", DecodeAnimToConst},

                {"LayerData.LayerID.Type", DecodeLayerDataType},

                {"GroupPowers", DecodeGroupPowers}
            });

        #region Custom Decoders

        private static string DecodeTerseUpdate(string fieldName, object fieldData)
        {
            byte[] block = (byte[]) fieldData;
            int i = 4;

            StringBuilder result = new StringBuilder();

            // LocalID
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "LocalID",
                Utils.BytesToUInt(block, 0),
                "Uint32");


            // State
            byte point = block[i++];
            result.AppendFormat("{0,30}: {1,-3} {2,-36} [{3}]" + Environment.NewLine,
                "State",
                point,
                "(" + (AttachmentPoint) point + ")",
                "AttachmentPoint");

            // Avatar boolean
            bool isAvatar = (block[i++] != 0);
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "IsAvatar",
                isAvatar,
                "Boolean");

            // Collision normal for avatar
            if (isAvatar)
            {
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "CollisionPlane",
                    new Vector4(block, i),
                    "Vector4");

                i += 16;
            }

            // Position
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "Position",
                new Vector3(block, i),
                "Vector3");
            i += 12;

            // Velocity
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "Velocity",
                new Vector3(
                    Utils.UInt16ToFloat(block, i, -128.0f, 128.0f),
                    Utils.UInt16ToFloat(block, i + 2, -128.0f, 128.0f),
                    Utils.UInt16ToFloat(block, i + 4, -128.0f, 128.0f)),
                "Vector3");
            i += 6;

            // Acceleration
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "Acceleration",
                new Vector3(
                    Utils.UInt16ToFloat(block, i, -64.0f, 64.0f),
                    Utils.UInt16ToFloat(block, i + 2, -64.0f, 64.0f),
                    Utils.UInt16ToFloat(block, i + 4, -64.0f, 64.0f)),
                "Vector3");

            i += 6;
            // Rotation (theta)
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "Rotation",
                new Quaternion(
                    Utils.UInt16ToFloat(block, i, -1.0f, 1.0f),
                    Utils.UInt16ToFloat(block, i + 2, -1.0f, 1.0f),
                    Utils.UInt16ToFloat(block, i + 4, -1.0f, 1.0f),
                    Utils.UInt16ToFloat(block, i + 6, -1.0f, 1.0f)),
                "Quaternion");
            i += 8;
            // Angular velocity (omega)
            result.AppendFormat("{0,30}: {1,-40} [{2}]",
                "AngularVelocity",
                new Vector3(
                    Utils.UInt16ToFloat(block, i, -64.0f, 64.0f),
                    Utils.UInt16ToFloat(block, i + 2, -64.0f, 64.0f),
                    Utils.UInt16ToFloat(block, i + 4, -64.0f, 64.0f)),
                "Vector3");
            //pos += 6;
            // TODO:  What is in these 6 bytes?
            return result.ToString();
        }

        private static string DecodeObjectCompressedData(string fieldName, object fieldData)
        {
            StringBuilder result = new StringBuilder();
            byte[] block = (byte[]) fieldData;
            int i = 0;

            // UUID
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "ID",
                new UUID(block, 0),
                "UUID");
            i += 16;

            // Local ID
            uint LocalID = (uint) (block[i++] + (block[i++] << 8) +
                                   (block[i++] << 16) + (block[i++] << 24));

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "LocalID",
                LocalID,
                "Uint32");
            // PCode
            PCode pcode = (PCode) block[i++];

            result.AppendFormat("{0,30}: {1,-3} {2,-36} [{3}]" + Environment.NewLine,
                "PCode",
                (int) pcode,
                "(" + pcode + ")",
                "PCode");

            // State
            AttachmentPoint point = (AttachmentPoint) block[i++];
            result.AppendFormat("{0,30}: {1,-3} {2,-36} [{3}]" + Environment.NewLine,
                "State",
                (byte) point,
                "(" + point + ")",
                "AttachmentPoint");

            //CRC
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "CRC",
                Utils.BytesToUInt(block, i),
                "UInt");

            i += 4;
            // Material
            result.AppendFormat("{0,30}: {1,-3} {2,-36} [{3}]" + Environment.NewLine,
                "Material",
                block[i],
                "(" + (Material) block[i++] + ")",
                "Material");

            // Click action
            result.AppendFormat("{0,30}: {1,-3} {2,-36} [{3}]" + Environment.NewLine,
                "ClickAction",
                block[i],
                "(" + (ClickAction) block[i++] + ")",
                "ClickAction");

            // Scale
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "Scale",
                new Vector3(block, i),
                "Vector3");
            i += 12;

            // Position
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "Position",
                new Vector3(block, i),
                "Vector3");
            i += 12;

            // Rotation
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "Rotation",
                new Vector3(block, i),
                "Vector3");

            i += 12;
            // Compressed flags
            CompressedFlags flags = (CompressedFlags) Utils.BytesToUInt(block, i);
            result.AppendFormat("{0,30}: {1,-10} {2,-29} [{3}]" + Environment.NewLine,
                "CompressedFlags",
                Utils.BytesToUInt(block, i),
                "(" + (CompressedFlags) Utils.BytesToUInt(block, i) + ")",
                "UInt");
            i += 4;

            // Owners ID
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "OwnerID",
                new UUID(block, i),
                "UUID");
            i += 16;

            // Angular velocity
            if ((flags & CompressedFlags.HasAngularVelocity) != 0)
            {
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "AngularVelocity",
                    new Vector3(block, i),
                    "Vector3");
                i += 12;
            }

            // Parent ID
            if ((flags & CompressedFlags.HasParent) != 0)
            {
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "ParentID",
                    (uint) (block[i++] + (block[i++] << 8) +
                            (block[i++] << 16) + (block[i++] << 24)),
                    "UInt");
            }

            // Tree data
            if ((flags & CompressedFlags.Tree) != 0)
            {
                result.AppendFormat("{0,30}: {1,-2} {2,-37} [{3}]" + Environment.NewLine,
                    "TreeSpecies",
                    block[i++],
                    "(" + (Tree) block[i] + ")",
                    "Tree");
            }

            // Scratch pad
            else if ((flags & CompressedFlags.ScratchPad) != 0)
            {
                int size = block[i++];
                byte[] scratch = new byte[size];
                Buffer.BlockCopy(block, i, scratch, 0, size);
                result.AppendFormat("{0,30}: {1,-40} [ScratchPad[]]" + Environment.NewLine,
                    "ScratchPad",
                    Utils.BytesToHexString(scratch, $"{"Data",30}"));
                i += size;
            }

            // Floating text
            if ((flags & CompressedFlags.HasText) != 0)
            {
                string text = string.Empty;
                while (block[i] != 0)
                {
                    text += (char) block[i];
                    i++;
                }

                i++;

                // Floating text
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Text",
                    text,
                    "string");

                // Text color
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "TextColor",
                    new Color4(block, i, false),
                    "Color4");
                i += 4;
            }

            // Media URL
            if ((flags & CompressedFlags.MediaURL) != 0)
            {
                string text = string.Empty;
                while (block[i] != 0)
                {
                    text += (char) block[i];
                    i++;
                }

                i++;

                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "MediaURL",
                    text,
                    "string");
            }

            // Particle system
            if ((flags & CompressedFlags.HasParticles) != 0)
            {
                Primitive.ParticleSystem p = new Primitive.ParticleSystem(block, i);
                result.AppendLine(DecodeObjectParticleSystem("ParticleSystem", p));
                i += 86;
            }

            // Extra parameters TODO:
            Primitive prim = new Primitive();
            int bytes = prim.SetExtraParamsFromBytes(block, i);
            i += bytes;
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "ExtraParams[]",
                bytes,
                "byte[]");

            //Sound data
            if ((flags & CompressedFlags.HasSound) != 0)
            {
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "SoundID",
                    new UUID(block, i),
                    "UUID");
                i += 16;

                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "SoundGain",
                    Utils.BytesToFloat(block, i),
                    "Float");
                i += 4;

                result.AppendFormat("{0,30}: {1,-2} {2,-37} [{3}]" + Environment.NewLine,
                    "SoundFlags",
                    block[i++],
                    "(" + (SoundFlags) block[i] + ")",
                    "SoundFlags");

                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "SoundRadius",
                    Utils.BytesToFloat(block, i),
                    "Float");
                i += 4;
            }

            // Name values
            if ((flags & CompressedFlags.HasNameValues) != 0)
            {
                string text = string.Empty;
                while (block[i] != 0)
                {
                    text += (char) block[i];
                    i++;
                }

                i++;

                // Parse the name values
                if (text.Length > 0)
                {
                    string[] lines = text.Split('\n');
                    NameValue[] nameValues = new NameValue[lines.Length];

                    for (int j = 0; j < lines.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(lines[j]))
                        {
                            NameValue nv = new NameValue(lines[j]);
                            nameValues[j] = nv;
                        }
                    }

                    result.AppendLine(DecodeNameValue("NameValues", nameValues));
                }
            }

            result.AppendFormat("{0,30}: {1,-2} {2,-37} [{3}]" + Environment.NewLine,
                "PathCurve",
                block[i],
                "(" + (PathCurve) block[i++] + ")",
                "PathCurve");

            ushort pathBegin = Utils.BytesToUInt16(block, i);
            i += 2;
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathBegin",
                Primitive.UnpackBeginCut(pathBegin),
                "float");

            ushort pathEnd = Utils.BytesToUInt16(block, i);
            i += 2;
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathEnd",
                Primitive.UnpackEndCut(pathEnd),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathScaleX",
                Primitive.UnpackPathScale(block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathScaleY",
                Primitive.UnpackPathScale(block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathShearX",
                Primitive.UnpackPathShear((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathShearY",
                Primitive.UnpackPathShear((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathTwist",
                Primitive.UnpackPathTwist((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathTwistBegin",
                Primitive.UnpackPathTwist((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathRadiusOffset",
                Primitive.UnpackPathTwist((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathTaperX",
                Primitive.UnpackPathTaper((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathTaperY",
                Primitive.UnpackPathTaper((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathRevolutions",
                Primitive.UnpackPathRevolutions(block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "PathSkew",
                Primitive.UnpackPathTwist((sbyte) block[i++]),
                "float");

            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "ProfileCurve",
                block[i++],
                "float");

            ushort profileBegin = Utils.BytesToUInt16(block, i);
            i += 2;
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "ProfileBegin",
                Primitive.UnpackBeginCut(profileBegin),
                "float");

            ushort profileEnd = Utils.BytesToUInt16(block, i);
            i += 2;
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "ProfileEnd",
                Primitive.UnpackEndCut(profileEnd),
                "float");

            ushort profileHollow = Utils.BytesToUInt16(block, i);
            i += 2;
            result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                "ProfileHollow",
                Primitive.UnpackProfileHollow(profileHollow),
                "float");

            int textureEntryLength = (int) Utils.BytesToUInt(block, i);
            i += 4;
            //prim.Textures = new Primitive.TextureEntry(block, i, textureEntryLength);
            string s = DecodeTextureEntry("TextureEntry", new Primitive.TextureEntry(block, i, textureEntryLength));
            result.AppendLine(s);
            i += textureEntryLength;

            // Texture animation
            if ((flags & CompressedFlags.TextureAnimation) != 0)
            {
                i += 4;
                string a = DecodeObjectTextureAnim("TextureAnimation", new Primitive.TextureAnimation(block, i));
                result.AppendLine(a);
            }

            if ((flags & CompressedFlags.HasParticlesNew) != 0)
            {
                Primitive.ParticleSystem p = new Primitive.ParticleSystem(block, i);
                result.AppendLine(DecodeObjectParticleSystem("ParticleSystemNEW", p));
                i += 94;
                if ((p.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.DataGlow) != 0)
                    i += 2;
                if ((p.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.DataBlend) != 0)
                    i += 2;
            }

            return result.ToString();
        }

        private static string DecodeObjectData(string fieldName, object fieldData)
        {
            byte[] data = (byte[]) fieldData;
            if (data.Length == 1)
            {
                return
                    $"{fieldName + " (Tree Species)",30}: {fieldData,2} {"(" + (Tree) data[0] + ")",-38} [{fieldData.GetType().Name}]";
            }
            else if (data.Length == 76)
            {
                /* TODO: these are likely useful packed fields,
                 * need to unpack them */
                Vector4 col = Vector4.Zero;
                Vector3 offset = Vector3.Zero;
                Vector3 vel = Vector3.Zero;
                Vector3 acc = Vector3.Zero;
                Quaternion q = Quaternion.Identity;
                Vector3 angvel = Vector3.Zero;

                col.FromBytes(data, 0);
                offset.FromBytes(data, 16);
                vel.FromBytes(data, 28);
                acc.FromBytes(data, 40);
                q.FromBytes(data, 52, true);
                angvel.FromBytes(data, 64);

                StringBuilder result = new StringBuilder();
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "ColisionPlane",
                    col,
                    "Vector4");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Offset",
                    offset,
                    "Vector3");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Velocity",
                    vel,
                    "Vector3");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Acceleration",
                    acc,
                    "Vector3");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "rotation",
                    q,
                    "Quaternion");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Omega",
                    angvel,
                    "Vector3");
                return result.ToString();
            }
            else if (data.Length == 60)
            {
                /* TODO: these are likely useful packed fields, need to unpack them */
                Vector3 offset = Vector3.Zero;
                Vector3 vel = Vector3.Zero;
                Vector3 acc = Vector3.Zero;
                Quaternion q = Quaternion.Identity;
                Vector3 angvel = Vector3.Zero;

                offset.FromBytes(data, 0);
                vel.FromBytes(data, 12);
                acc.FromBytes(data, 24);
                q.FromBytes(data, 36, true);
                angvel.FromBytes(data, 48);

                StringBuilder result = new StringBuilder();
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Offset",
                    offset,
                    "Vector3");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Velocity",
                    vel,
                    "Vector3");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Acceleration",
                    acc,
                    "Vector3");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "rotation",
                    q,
                    "Quaternion");
                result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                    "Omega",
                    angvel,
                    "Vector3");
                return result.ToString();
            }
            else
            {
                return Utils.BytesToHexString((byte[]) fieldData, $"{fieldName,30}");
            }
        }

        private static string DecodeObjectTextureAnim(string fieldName, object fieldData)
        {
            StringBuilder result = new StringBuilder();
            Primitive.TextureAnimation textureAnim;
            if (fieldData is Primitive.TextureAnimation data)
                textureAnim = data;
            else
                textureAnim = new Primitive.TextureAnimation((byte[]) fieldData, 0);

            result.AppendFormat("{0,30}", " <TextureAnimation>" + Environment.NewLine);
            GenericTypeDecoder(textureAnim, ref result);
            result.AppendFormat("{0,30}", "</TextureAnimation>");

            return result.ToString();
        }

        private static string DecodeEstateParameter(string fieldName, object fieldData)
        {
            byte[] bytes = (byte[]) fieldData;

            return bytes.Length == 17 
                ? $"{fieldName,30}: {new UUID((byte[]) fieldData, 0),-40} [UUID]" 
                : $"{fieldName,30}: {Utils.BytesToString((byte[]) fieldData),-40} [Byte[]]";
        }

        private static string DecodeNameValue(string fieldName, object fieldData)
        {
            NameValue[] nameValues = null;
            if (fieldData is NameValue[] data)
                nameValues = data;
            else
            {
                string nameValue = Utils.BytesToString((byte[]) fieldData);
                if (nameValue.Length > 0)
                {
                    string[] lines = nameValue.Split('\n');
                    nameValues = new NameValue[lines.Length];

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(lines[i]))
                        {
                            NameValue nv = new NameValue(lines[i]);
                            nameValues[i] = nv;
                        }
                    }
                }
            }

            StringBuilder result = new StringBuilder();
            result.AppendFormat("{0,30}", " <NameValues>" + Environment.NewLine);
            if (nameValues != null)
            {
                foreach (var nv in nameValues)
                {
                    result.AppendFormat(
                        "{0,30}: Name={1} Value={2} Class={3} Type={4} Sendto={5}" + Environment.NewLine, "NameValue",
                        nv.Name, nv.Value, nv.Class, nv.Type,
                        nv.Sendto);
                }
            }

            result.AppendFormat("{0,30}", "</NameValues>");
            return result.ToString();
        }

        private static string DecodeObjectExtraParams(string fieldName, object fieldData)
        {
            byte[] data = (byte[]) fieldData;

            int i = 0;
            //int totalLength = 1;

            Primitive.FlexibleData flexible = null;
            Primitive.LightData light = null;
            Primitive.SculptData sculpt = null;
            Primitive.SculptData mesh = null;
            uint meshFlags = 0;
            bool hasMeshFlags = false;

            byte extraParamCount = data[i++];

            for (int k = 0; k < extraParamCount; k++)
            {
                ExtraParamType type = (ExtraParamType) Utils.BytesToUInt16(data, i);
                i += 2;

                uint paramLength = Utils.BytesToUInt(data, i);
                i += 4;

                if (type == ExtraParamType.Flexible)
                    flexible = new Primitive.FlexibleData(data, i);
                else if (type == ExtraParamType.Light)
                    light = new Primitive.LightData(data, i);
                else if (type == ExtraParamType.Sculpt)
                    sculpt = new Primitive.SculptData(data, i);
                else if (type == ExtraParamType.Mesh)
                    mesh = new Primitive.SculptData(data, i);
                else if ((byte) type == 0x70)
                {
                    hasMeshFlags = true;
                    meshFlags = Utils.BytesToUInt(data, i);
                }

                i += (int) paramLength;
                //totalLength += (int)paramLength + 6;
            }

            StringBuilder result = new StringBuilder();
            result.AppendFormat("{0,30}", "<ExtraParams>" + Environment.NewLine);
            if (flexible != null)
            {
                result.AppendFormat("{0,30}", "<Flexible>" + Environment.NewLine);
                GenericTypeDecoder(flexible, ref result);
                result.AppendFormat("{0,30}", "</Flexible>" + Environment.NewLine);
            }

            if (sculpt != null)
            {
                result.AppendFormat("{0,30}", "<Sculpt>" + Environment.NewLine);
                GenericTypeDecoder(sculpt, ref result);
                result.AppendFormat("{0,30}", "</Sculpt>" + Environment.NewLine);
            }

            if (mesh != null)
            {
                result.AppendFormat("{0,30}", "<Mesh>" + Environment.NewLine);
                GenericTypeDecoder(mesh, ref result);
                result.AppendFormat("{0,30}", "</Mesh>" + Environment.NewLine);
            }

            if (light != null)
            {
                result.AppendFormat("{0,30}", "<Light>" + Environment.NewLine);
                GenericTypeDecoder(light, ref result);
                result.AppendFormat("{0,30}", "</Light>" + Environment.NewLine);
            }

            if (hasMeshFlags)
            {
                result.AppendFormat("{0,30}", "<MeshFlags>" + Environment.NewLine);
                result.AppendFormat("{0,30}", meshFlags + Environment.NewLine);
                result.AppendFormat("{0,30}", "</MeshFlags>" + Environment.NewLine);
            }

            result.AppendFormat("{0,30}", "</ExtraParams>");
            return result.ToString();
        }

        private static string DecodeObjectParticleSystem(string fieldName, object fieldData)
        {
            var result = new StringBuilder();
            Primitive.ParticleSystem particleSys;
            if (fieldData is Primitive.ParticleSystem data)
                particleSys = data;
            else
                particleSys = new Primitive.ParticleSystem((byte[]) fieldData, 0);

            result.AppendFormat("{0,30}", "<ParticleSystem>" + Environment.NewLine);
            GenericTypeDecoder(particleSys, ref result);
            result.AppendFormat("{0,30}", "</ParticleSystem>");

            return result.ToString();
        }

        private static void GenericTypeDecoder(object obj, ref StringBuilder result)
        {
            FieldInfo[] fields = obj.GetType().GetFields();

            foreach (FieldInfo field in fields)
            {
                if (SpecialDecoder("a" + "." + "b" + "." + field.Name,
                    field.GetValue(obj), out var special))
                {
                    result.AppendLine(special);
                }
                else
                {
                    result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                        field.Name,
                        field.GetValue(obj),
                        field.FieldType.Name);
                }
            }
        }

        private static string DecodeObjectPCode(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-3} {"(" + (PCode) (byte) fieldData + ")",-36} [PCode]";
        }

        private static string DecodeImageType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-3} {"(" + (ImageType) (byte) fieldData + ")",-36} [ImageType]";
        }

        private static string DecodeImageCodec(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-3} {"(" + (ImageCodec) (byte) fieldData + ")",-36} [ImageCodec]";
        }

        private static string DecodeObjectMaterial(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-3} {"(" + (Material) (byte) fieldData + ")",-36} [Material]";
        }

        private static string DecodeObjectClickAction(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-3} {"(" + (ClickAction) (byte) fieldData + ")",-36} [ClickAction]";
        }

        private static string DecodeEventFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-3} {"(" + (DirectoryManager.EventFlags) (uint) fieldData + ")",-36} [EventFlags]";
        }

        private static string DecodeDirQueryFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.DirFindFlags) (uint) fieldData + ")",-29} [DirectoryManager.DirFindFlags]";
        }

        private static string DecodeDirClassifiedQueryFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.ClassifiedQueryFlags) (uint) fieldData + ")",-29} [ClassifiedQueryFlags]";
        }

        private static string DecodeDirClassifiedFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.ClassifiedFlags) (byte) fieldData + ")",-29} [ClassifiedFlags]";
        }

        private static string DecodeGroupPowers(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-20} {"(" + (GroupPowers) (ulong) fieldData + ")",-19} [GroupPowers]";
        }

        private static string DecodeParcelAcl(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-10} {"(" + (AccessList) (uint) fieldData + ")",-29} [AccessList]";
        }

        private static string SearchTypeFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (DirectoryManager.SearchTypeFlags) (uint) fieldData + ")",-29} [DirectoryManager.SearchTypeFlags]";
        }

        private static string DecodeCategory(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-3} {"(" + fieldData + ")",-36} [ParcelCategory]";
        }

        private static string DecodeObjectUpdateFlags(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-10} {"(" + (PrimFlags) (uint) fieldData + ")",-29} [PrimFlags]";
        }

        private static string DecodeTeleportFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (TeleportFlags) (uint) fieldData + ")",-29} [TeleportFlags]";
        }

        private static string DecodeScriptControls(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(uint) fieldData,-10} {"(" + (AgentManager.ControlFlags) (uint) fieldData + ")",-29} [AgentManager.ControlFlags]";
        }

        private static string DecodeColorField(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(fieldData.GetType().Name.Equals("Color4") ? (Color4) fieldData : new Color4((byte[]) fieldData, 0, false)),-40} [Color4]";
        }

        private static string DecodeTimeStamp(string fieldName, object fieldData)
        {
            if (fieldData is int data && data > 0)
                return
                    $"{fieldName,30}: {data,-10} {"(" + Utils.UnixTimeToDateTime(data) + ")",-29} [{data.GetType().Name}]";
            else if (fieldData is uint u && u > 0)
                return
                    $"{fieldName,30}: {u,-10} {"(" + Utils.UnixTimeToDateTime(u) + ")",-29} [{u.GetType().Name}]";
            else
                return $"{fieldName,30}: {fieldData,-40} [{fieldData.GetType().Name}]";
        }

        private static string DecodeBinaryBucket(string fieldName, object fieldData)
        {
            byte[] bytes = (byte[]) fieldData;
            string bucket;
            if (bytes.Length == 1)
            {
                bucket = $"{bytes[0]}";
            }
            else if (bytes.Length == 17)
            {
                bucket = $"{new UUID(bytes, 1),-36} {bytes[0]} ({(AssetType) (sbyte) bytes[0]})";
            }
            else if (bytes.Length == 16
            ) // the folder ID for the asset to be stored into if we accept an inventory offer
            {
                bucket = new UUID(bytes, 0).ToString();
            }
            else
            {
                bucket = Utils.BytesToString(bytes); // we'll try a string lastly
            }

            return $"{fieldName,30}: {bucket,-40} [Byte[{bytes.Length}]]";
        }

        private static string DecodeBinaryToHexString(string fieldName, object fieldData)
        {
            return $"{Utils.BytesToHexString((byte[]) fieldData, $"{fieldName,30}"),30}";
        }

        private static string DecodeWearableType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {(byte) fieldData,-2} {"(" + (WearableType) fieldData + ")",-37} [WearableType]";
        }

        private static string DecodeInventoryType(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(sbyte) fieldData,-2} {"(" + (AssetType) (sbyte) fieldData + ")",-37} [AssetType]";
        }

        private static string DecodeInventorySort(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-2} {"(" + (InventorySortOrder) (int) fieldData + ")",-37} [InventorySortOrder]";
        }

        private static string DecodeInventoryInvType(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(sbyte) fieldData,-2} {"(" + (InventoryType) fieldData + ")",-37} [InventoryType]";
        }

        private static string DecodeFolderType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {(sbyte) fieldData,-2} {"(" + (FolderType) fieldData + ")",-37} [Folderype]";
        }

        private static string DecodeInventoryFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(uint) fieldData,-2} {"(" + (InventoryItemFlags) (uint) fieldData + ")",-37} [InventoryItemFlags]";
        }

        private static string DecodeObjectSaleType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {(byte) fieldData,-2} {"(" + (SaleType) fieldData + ")",-37} [SaleType]";
        }

        private static string DecodeRegionFlags(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (RegionFlags) (uint) fieldData + ")",-37} [RegionFlags]";
        }

        private static string DecodeTransferParams(string fieldName, object fieldData)
        {
            byte[] paramData = (byte[]) fieldData;
            StringBuilder result = new StringBuilder();
            result.AppendLine(" <Params>");
            if (paramData.Length == 20)
            {
                result.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine,
                    "AssetID",
                    new UUID(paramData, 0));

                result.AppendFormat("{0,30}: {1,-2} {2,-37} [AssetType]" + Environment.NewLine,
                    "AssetType",
                    (sbyte) paramData[16],
                    "(" + (AssetType) (sbyte) paramData[16] + ")");
            }
            else if (paramData.Length == 100)
            {
                //UUID agentID = new UUID(info.TransferInfo.Params, 0);
                result.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine,
                    "AgentID",
                    new UUID(paramData, 0));

                //UUID sessionID = new UUID(info.TransferInfo.Params, 16);
                result.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine,
                    "SessionID",
                    new UUID(paramData, 16));
                //UUID ownerID = new UUID(info.TransferInfo.Params, 32);
                result.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine,
                    "OwnerID",
                    new UUID(paramData, 32));
                //UUID taskID = new UUID(info.TransferInfo.Params, 48);
                result.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine,
                    "TaskID",
                    new UUID(paramData, 48));
                //UUID itemID = new UUID(info.TransferInfo.Params, 64);
                result.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine,
                    "ItemID",
                    new UUID(paramData, 64));

                result.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine,
                    "AssetID",
                    new UUID(paramData, 80));

                result.AppendFormat("{0,30}: {1,-2} {2,-37} [AssetType]" + Environment.NewLine,
                    "AssetType",
                    (sbyte) paramData[96],
                    "(" + (AssetType) (sbyte) paramData[96] + ")");
            }
            else
            {
                Console.WriteLine("Oh Poop!");
            }

            result.Append("</Params>");

            return result.ToString();
        }

        private static string DecodeTransferChannelType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (ChannelType) (int) fieldData + ")",-37} [ChannelType]";
        }

        private static string DecodeTransferSourceType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (SourceType) (int) fieldData + ")",-37} [SourceType]";
        }

        private static string DecodeTransferTargetType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (TargetType) (int) fieldData + ")",-37} [TargetType]";
        }

        private static string DecodeMapRequestFlags(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (GridLayerType) (uint) fieldData + ")",-37} [GridLayerType]";
        }

        private static string DecodeGridItemType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (GridItemType) (uint) fieldData + ")",-37} [GridItemType]";
        }

        private static string DecodeLayerDataType(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-2} {"(" + (TerrainPatch.LayerType) (byte) fieldData + ")",-37} [LayerType]";
        }

        private static string DecodeMapAccess(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (SimAccess) (byte) fieldData + ")",-37} [SimAccess]";
        }

        private static string DecodeSimAccess(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {(byte) fieldData,-2} {"(" + (SimAccess) fieldData + ")",-37} [SimAccess]";
        }

        private static string DecodeAttachedSoundFlags(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {(byte) fieldData,-2} {"(" + (SoundFlags) fieldData + ")",-37} [SoundFlags]";
        }


        private static string DecodeChatSourceType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (SourceType) (byte) fieldData + ")",-37} [SourceType]";
        }

        private static string DecodeChatChatType(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {(byte) fieldData,-2} {"(" + (ChatType) fieldData + ")",-37} [ChatType]";
        }

        private static string DecodeChatAudible(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(byte) fieldData,-2} {"(" + (ChatAudibleLevel) (byte) fieldData + ")",-37} [ChatAudibleLevel]";
        }

        private static string DecodeImageData(string fieldName, object fieldData)
        {
            return $"{Utils.BytesToHexString((byte[]) fieldData, $"{fieldName,30}"),10}";
        }

        private static string DecodeTerseTextureEntry(string fieldName, object fieldData)
        {
            byte[] block = (byte[]) fieldData;

            Primitive.TextureEntry te = new Primitive.TextureEntry(block, 4, block.Length - 4);

            StringBuilder result = new StringBuilder();

            result.AppendFormat("{0,30}", " <TextureEntry>" + Environment.NewLine);
            if (te.DefaultTexture != null)
            {
                result.AppendFormat("{0,30}", "    <DefaultTexture>" + Environment.NewLine);
                GenericFieldsDecoder(te.DefaultTexture, ref result);
                GenericPropertiesDecoder(te.DefaultTexture, ref result);
                result.AppendFormat("{0,30}", "   </DefaultTexture>" + Environment.NewLine);
            }

            result.AppendFormat("{0,30}", "    <FaceTextures>" + Environment.NewLine);
            for (int i = 0; i < te.FaceTextures.Length; i++)
            {
                if (te.FaceTextures[i] != null)
                {
                    result.AppendFormat("{0,30}[{1}]" + Environment.NewLine, "FaceTexture", i);
                    GenericFieldsDecoder(te.FaceTextures[i], ref result);
                    GenericPropertiesDecoder(te.FaceTextures[i], ref result);
                }
            }

            result.AppendFormat("{0,30}", "   </FaceTextures>" + Environment.NewLine);
            result.AppendFormat("{0,30}", "</TextureEntry>");

            return result.ToString();
        }

        private static string DecodeTextureEntry(string fieldName, object fieldData)
        {
            Primitive.TextureEntry te;
            if (fieldData is Primitive.TextureEntry data)
                te = data;
            else
            {
                byte[] tebytes = (byte[]) fieldData;
                te = new Primitive.TextureEntry(tebytes, 0, tebytes.Length);
            }

            StringBuilder result = new StringBuilder();

            result.AppendFormat("{0,30}", " <TextureEntry>" + Environment.NewLine);
            if (te.DefaultTexture != null)
            {
                result.AppendFormat("{0,30}", "    <DefaultTexture>" + Environment.NewLine);
                GenericFieldsDecoder(te.DefaultTexture, ref result);
                GenericPropertiesDecoder(te.DefaultTexture, ref result);
                result.AppendFormat("{0,30}", "   </DefaultTexture>" + Environment.NewLine);
            }

            result.AppendFormat("{0,30}", "    <FaceTextures>" + Environment.NewLine);
            for (int i = 0; i < te.FaceTextures.Length; i++)
            {
                if (te.FaceTextures[i] != null)
                {
                    result.AppendFormat("{0,30}[{1}]" + Environment.NewLine, "FaceTexture", i);
                    GenericFieldsDecoder(te.FaceTextures[i], ref result);
                    GenericPropertiesDecoder(te.FaceTextures[i], ref result);
                }
            }

            result.AppendFormat("{0,30}", "   </FaceTextures>" + Environment.NewLine);
            result.AppendFormat("{0,30}", "</TextureEntry>");

            return result.ToString();
        }

        private static void GenericFieldsDecoder(object obj, ref StringBuilder result)
        {
            Type parcelType = obj.GetType();
            FieldInfo[] fields = parcelType.GetFields();
            foreach (FieldInfo field in fields)
            {
                string special;
                if (SpecialDecoder("a" + "." + "b" + "." + field.Name,
                    field.GetValue(obj), out special))
                {
                    result.AppendLine(special);
                }
                else
                {
                    result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                        field.Name,
                        field.GetValue(obj),
                        field.FieldType.Name);
                }
            }
        }

        private static void GenericPropertiesDecoder(object obj, ref StringBuilder result)
        {
            Type parcelType = obj.GetType();
            PropertyInfo[] propertyInfos = parcelType.GetProperties();
            foreach (PropertyInfo property in propertyInfos)
            {
                string special;
                if (SpecialDecoder("a" + "." + "b" + "." + property.Name,
                    property.GetValue(obj, null), out special))
                {
                    result.AppendLine(special);
                }
                else
                {
                    result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                        property.Name,
                        property.GetValue(obj, null),
                        property.PropertyType.Name);
                }
            }
        }

        private static string DecodeDialog(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(byte) fieldData,-2} {"(" + (InstantMessageDialog) fieldData + ")",-37} [{fieldData.GetType().Name}]";
        }

        private static string DecodeControlFlags(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-10} {"(" + (AgentManager.ControlFlags) (uint) fieldData + ")",-29} [{fieldData.GetType().Name}]";
        }

        private static string DecodePermissionMask(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {(uint) fieldData,-10} {"(" + (PermissionMask) fieldData + ")",-29} [{fieldData.GetType().Name}]";
        }

        private static string DecodeViewerEffectTypeData(string fieldName, object fieldData)
        {
            byte[] data = (byte[]) fieldData;
            StringBuilder sb = new StringBuilder();
            if (data.Length == 56 || data.Length == 57)
            {
                UUID sourceAvatar = new UUID(data, 0);
                UUID targetObject = new UUID(data, 16);
                Vector3d targetPos = new Vector3d(data, 32);
                sb.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine, fieldName,
                    "Source AvatarID=" + sourceAvatar);
                sb.AppendFormat("{0,30}: {1,-40} [UUID]" + Environment.NewLine, fieldName,
                    "Target ObjectID=" + targetObject);


                Helpers.GlobalPosToRegionHandle((float) targetPos.X, (float) targetPos.Y, out _, out _);

                sb.AppendFormat("{0,30}: {1,-40} [Vector3d]", fieldName, targetPos);

                if (data.Length == 57)
                {
                    sb.AppendLine();
                    sb.AppendFormat("{0,30}: {1,-17} {2,-22} [Byte]", fieldName, "Point At Type=" + data[56],
                        "(" + (PointAtType) data[56] + ")");
                }

                return sb.ToString();
            }
            else
            {
                return string.Format("{0,30}: (No Decoder) Length={1}" + Environment.NewLine, fieldName, data.Length) +
                       Utils.BytesToHexString(data, $"{"",30}");
            }
        }

        private static string DecodeAgentState(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (AgentState) (byte) fieldData + ")",-37} [AgentState]";
        }

        private static string DecodeAgentFlags(string fieldName, object fieldData)
        {
            return $"{fieldName,30}: {fieldData,-2} {"(" + (AgentFlags) (byte) fieldData + ")",-37} [AgentFlags]";
        }

        private static string DecodeObjectState(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-2} {"(" + (AttachmentPoint) (byte) fieldData + ")",-37} [AttachmentPoint]";
        }

        private static string DecodeViewerEffectType(string fieldName, object fieldData)
        {
            return
                $"{fieldName,30}: {fieldData,-2} {"(" + (EffectType) (byte) fieldData + ")",-37} [{fieldData.GetType().Name}]";
        }

        private static string DecodeAnimToConst(string fieldName, object fieldData)
        {
            string animConst = "UUID";
            ImmutableDictionary<UUID, string> animsDict = Animations.ToDictionary();
            if (animsDict.ContainsKey((UUID) fieldData))
                animConst = animsDict[(UUID) fieldData];
            return $"{fieldName,30}: {fieldData,-40} [{animConst}]";
        }

        #endregion

        /// <summary>
        /// Creates a formatted string containing the values of a Packet
        /// </summary>
        /// <param name="packet">The Packet</param>
        /// <returns>A formatted string of values of the nested items in the Packet object</returns>
        public static string PacketToString(Packet packet)
        {
            StringBuilder result = new StringBuilder();

            result.AppendFormat(
                "Packet Type: {0} http://lib.openmetaverse.co/wiki/{0} http://wiki.secondlife.com/wiki/{0}" +
                Environment.NewLine, packet.Type);
            result.AppendLine("[Packet Header]");
            // payload
            result.AppendFormat("Sequence: {0}" + Environment.NewLine, packet.Header.Sequence);
            result.AppendFormat(" Options: {0}" + Environment.NewLine, InterpretOptions(packet.Header));
            result.AppendLine();

            result.AppendLine("[Packet Payload]");

            FieldInfo[] fields = packet.GetType().GetFields();

            foreach (var t in fields)
            {
                // we're not interested in any of these here
                if (t.Name == "Type" || t.Name == "Header" || t.Name == "HasVariableBlocks")
                    continue;

                if (t.FieldType.IsArray)
                {
                    result.AppendFormat("{0,30} []" + Environment.NewLine, "-- " + t.Name + " --");
                    RecursePacketArray(t, packet, ref result);
                }
                else
                {
                    result.AppendFormat("{0,30}" + Environment.NewLine, "-- " + t.Name + " --");
                    RecursePacketField(t, packet, ref result);
                }
            }

            return result.ToString();
        }

        public static string InterpretOptions(Header header)
        {
            return "["
                   + (header.AppendedAcks ? "Ack" : "   ")
                   + " "
                   + (header.Resent ? "Res" : "   ")
                   + " "
                   + (header.Reliable ? "Rel" : "   ")
                   + " "
                   + (header.Zerocoded ? "Zer" : "   ")
                   + "]"
                ;
        }

        private static void RecursePacketArray(FieldInfo fieldInfo, object packet, ref StringBuilder result)
        {
            var packetDataObject = fieldInfo.GetValue(packet) as Array;

            if (packetDataObject == null) return;

            foreach (object nestedArrayRecord in packetDataObject)
            {
                FieldInfo[] fields = nestedArrayRecord.GetType().GetFields();

                foreach (FieldInfo t in fields)
                {
                    string special;
                    if (SpecialDecoder(packet.GetType().Name + "." + fieldInfo.Name + "." + t.Name,
                        t.GetValue(nestedArrayRecord), out special))
                    {
                        result.AppendLine(special);
                    }
                    else if (t.FieldType.IsArray) // default for an array (probably a byte[])
                    {
                        result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                            t.Name,
                            Utils.BytesToString((byte[]) t.GetValue(nestedArrayRecord)),
                            /*fields[i].GetValue(nestedArrayRecord).GetType().Name*/ "String");
                    }
                    else // default for a field
                    {
                        result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                            t.Name,
                            t.GetValue(nestedArrayRecord),
                            t.GetValue(nestedArrayRecord).GetType().Name);
                    }
                }

                // Handle Properties
                foreach (PropertyInfo propertyInfo in nestedArrayRecord.GetType().GetProperties())
                {
                    if (propertyInfo.Name.Equals("Length"))
                        continue;

                    string special;
                    if (SpecialDecoder(packet.GetType().Name + "." + fieldInfo.Name + "." + propertyInfo.Name,
                        propertyInfo.GetValue(nestedArrayRecord, null),
                        out special))
                    {
                        result.AppendLine(special);
                    }
                    else
                    {
                        /* Leave the c for now at the end, it signifies something useful that still needs to be done i.e. a decoder written */
                        result.AppendFormat("{0, 30}: {1,-40} [{2}]c" + Environment.NewLine,
                            propertyInfo.Name,
                            Utils.BytesToString((byte[]) propertyInfo.GetValue(nestedArrayRecord, null)),
                            propertyInfo.PropertyType.Name);
                    }
                }

                result.AppendFormat("{0,32}" + Environment.NewLine, "***");
            }
        }

        private static void RecursePacketField(FieldInfo fieldInfo, object packet, ref StringBuilder result)
        {
            object packetDataObject = fieldInfo.GetValue(packet);

            // handle Fields
            foreach (FieldInfo packetValueField in fieldInfo.GetValue(packet).GetType().GetFields())
            {
                string special;
                if (SpecialDecoder(packet.GetType().Name + "." + fieldInfo.Name + "." + packetValueField.Name,
                    packetValueField.GetValue(packetDataObject),
                    out special))
                {
                    result.AppendLine(special);
                }
                else if (packetValueField.FieldType.IsArray)
                {
                    result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                        packetValueField.Name,
                        Utils.BytesToString((byte[]) packetValueField.GetValue(packetDataObject)),
                        /*packetValueField.FieldType.Name*/ "String");
                }
                else
                {
                    result.AppendFormat("{0,30}: {1,-40} [{2}]" + Environment.NewLine,
                        packetValueField.Name, packetValueField.GetValue(packetDataObject),
                        packetValueField.FieldType.Name);
                }
            }

            // Handle Properties
            foreach (PropertyInfo propertyInfo in packetDataObject.GetType().GetProperties())
            {
                if (propertyInfo.Name.Equals("Length"))
                    continue;

                string special;
                if (SpecialDecoder(packet.GetType().Name + "." + fieldInfo.Name + "." + propertyInfo.Name,
                    propertyInfo.GetValue(packetDataObject, null),
                    out special))
                {
                    result.AppendLine(special);
                }
                else if (propertyInfo.GetValue(packetDataObject, null).GetType() == typeof(byte[]))
                {
                    result.AppendFormat("{0, 30}: {1,-40} [{2}]" + Environment.NewLine,
                        propertyInfo.Name,
                        Utils.BytesToString((byte[]) propertyInfo.GetValue(packetDataObject, null)),
                        propertyInfo.PropertyType.Name);
                }
                else
                {
                    result.AppendFormat("{0, 30}: {1,-40} [{2}]" + Environment.NewLine,
                        propertyInfo.Name,
                        propertyInfo.GetValue(packetDataObject, null),
                        propertyInfo.PropertyType.Name);
                }
            }
        }

        private static bool SpecialDecoder(string decoderKey, object fieldData, out string result)
        {
            result = string.Empty;
            string[] keys = decoderKey.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
            string[] keyList = {decoderKey, decoderKey.Replace("Packet", ""), keys[1] + "." + keys[2], keys[2]};
            foreach (string key in keyList)
            {
                if (fieldData is byte[] fd)
                {
                    if (!(fd.Length > 0))
                    {
                        // bypass the decoder since we were passed an empty byte array
                        result = $"{keys[2],30}:";
                        return true;
                    }
                }

                if (Callbacks.Value.ContainsKey(key)) // fieldname e.g: Plane
                {
                    result = Callbacks.Value[key](keys[2], fieldData);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Decode an IMessage object into a beautifully formatted string
        /// </summary>
        /// <param name="message">The IMessage object</param>
        /// <param name="recurseLevel">Recursion level (used for indenting)</param>
        /// <returns>A formatted string containing the names and values of the source object</returns>
        public static string MessageToString(object message, int recurseLevel)
        {
            if (message == null)
                return string.Empty;

            StringBuilder result = new StringBuilder();
            // common/custom types
            if (recurseLevel <= 0)
            {
                result.AppendFormat("Message Type: {0} http://lib.openmetaverse.co/wiki/{0}" + Environment.NewLine,
                    message.GetType().Name);
            }
            else
            {
                string pad = "              +--".PadLeft(recurseLevel + 3);
                result.AppendFormat("{0} {1}" + Environment.NewLine, pad, message.GetType().Name);
            }

            recurseLevel++;

            foreach (FieldInfo messageField in message.GetType().GetFields())
            {
                // an abstract message class
                if (messageField.FieldType.IsAbstract)
                {
                    result.AppendLine(MessageToString(messageField.GetValue(message), recurseLevel));
                }
                // a byte array
                else if (messageField.GetValue(message) != null &&
                         messageField.GetValue(message).GetType() == typeof(byte[]))
                {
                    result.AppendFormat("{0, 30}:" + Environment.NewLine, messageField.Name);

                    result.AppendFormat("{0}" + Environment.NewLine,
                        Utils.BytesToHexString((byte[]) messageField.GetValue(message),
                            $"{"",30}"));
                }

                // an array of class objects
                else if (messageField.FieldType.IsArray)
                {
                    var messageObjectData = messageField.GetValue(message) as Array;
                    
                    if (messageObjectData == null) continue;

                    result.AppendFormat("-- {0} --" + Environment.NewLine, messageField.FieldType.Name);
                    foreach (object nestedArrayObject in messageObjectData)
                    {
                        if (nestedArrayObject == null)
                        {
                            result.AppendFormat("{0,30}" + Environment.NewLine, "-- null --");
                            continue;
                        }
                        else
                        {
                            result.AppendFormat("{0,30}" + Environment.NewLine,
                                "-- " + nestedArrayObject.GetType().Name + " --");
                        }

                        foreach (FieldInfo nestedField in nestedArrayObject.GetType().GetFields())
                        {
                            if (nestedField.FieldType.IsEnum)
                            {
                                result.AppendFormat("{0,30}: {1,-10} {2,-29} [{3}]" + Environment.NewLine,
                                    nestedField.Name,
                                    Enum.Format(nestedField.GetValue(nestedArrayObject).GetType(),
                                        nestedField.GetValue(nestedArrayObject), "D"),
                                    "(" + nestedField.GetValue(nestedArrayObject) + ")",
                                    nestedField.GetValue(nestedArrayObject).GetType().Name);
                            }
                            else if (nestedField.FieldType.IsInterface)
                            {
                                result.AppendLine(
                                    MessageToString(nestedField.GetValue(nestedArrayObject), recurseLevel));
                            }
                            else
                            {
                                result.AppendFormat("{0, 30}: {1,-40} [{2}]" + Environment.NewLine,
                                    nestedField.Name,
                                    nestedField.GetValue(nestedArrayObject),
                                    nestedField.FieldType.Name);
                            }
                        }
                    }
                }
                else
                {
                    if (messageField.FieldType.IsEnum)
                    {
                        result.AppendFormat("{0,30}: {1,-2} {2,-37} [{3}]" + Environment.NewLine,
                            messageField.Name,
                            Enum.Format(messageField.GetValue(message).GetType(),
                                messageField.GetValue(message), "D"),
                            "(" + messageField.GetValue(message) + ")",
                            messageField.FieldType.Name);
                    }
                    else if (messageField.FieldType.IsInterface)
                    {
                        result.AppendLine(MessageToString(messageField.GetValue(message), recurseLevel));
                    }
                    else
                    {
                        result.AppendFormat("{0, 30}: {1,-40} [{2}]" + Environment.NewLine,
                            messageField.Name, messageField.GetValue(message), messageField.FieldType.Name);
                    }
                }
            }

            return result.ToString();
        }
    }
}
