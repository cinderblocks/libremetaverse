/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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

using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenMetaverse
{
    public partial class ObjectManager
    {
        private void ObjectAnimationHandler(object sender, PacketReceivedEventArgs e)
        {
            if (!(e.Packet is ObjectAnimationPacket data)) { return; }

            var signaledAnimations = new List<Animation>(data.AnimationList.Length);

            for (var i = 0; i < data.AnimationList.Length; i++)
            {
                var animation = new Animation
                {
                    AnimationID = data.AnimationList[i].AnimID,
                    AnimationSequence = data.AnimationList[i].AnimSequenceID
                };
                if (i < data.AnimationList.Length)
                {
                    animation.AnimationSourceObjectID = data.Sender.ID;
                }

                signaledAnimations.Add(animation);
            }

            OnObjectAnimation(new ObjectAnimationEventArgs(data.Sender.ID, signaledAnimations));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ObjectUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var simulator = e.Simulator;

            var update = (ObjectUpdatePacket)packet;
            UpdateDilation(e.Simulator, update.RegionData.TimeDilation);

            foreach (var block in update.ObjectData)
            {
                var objectupdate = new ObjectMovementUpdate();
                //Vector4 collisionPlane = Vector4.Zero;
                //Vector3 position;
                //Vector3 velocity;
                //Vector3 acceleration;
                //Quaternion rotation;
                //Vector3 angularVelocity;
                NameValue[] nameValues;
                var attachment = false;
                var pcode = (PCode)block.PCode;

                #region Relevance check

                // Check if we are interested in this object
                if (!Client.Settings.ALWAYS_DECODE_OBJECTS)
                {
                    switch (pcode)
                    {
                        case PCode.Grass:
                        case PCode.Tree:
                        case PCode.NewTree:
                        case PCode.Prim:
                            if (m_ObjectUpdate == null) continue;
                            break;
                        case PCode.Avatar:
                            // Make an exception for updates about our own agent
                            if (block.FullID != Client.Self.AgentID && m_AvatarUpdate == null) continue;
                            break;
                        case PCode.ParticleSystem:
                            continue; // TODO: Do something with these
                    }
                }

                #endregion Relevance check

                #region NameValue parsing

                var nameValue = Utils.BytesToString(block.NameValue);
                if (nameValue.Length > 0)
                {
                    var lines = nameValue.Split('\n');
                    nameValues = new NameValue[lines.Length];

                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (string.IsNullOrEmpty(lines[i]))
                            continue;

                        var nv = new NameValue(lines[i]);
                        if (nv.Name == "AttachItemID") { attachment = true; }
                        nameValues[i] = nv;
                    }
                }
                else
                {
                    nameValues = Array.Empty<NameValue>();
                }

                #endregion NameValue parsing

                #region Decode Object (primitive) parameters
                var data = new Primitive.ConstructionData
                {
                    State = block.State,
                    Material = (Material)block.Material,
                    PathCurve = (PathCurve)block.PathCurve,
                    profileCurve = block.ProfileCurve,
                    PathBegin = Primitive.UnpackBeginCut(block.PathBegin),
                    PathEnd = Primitive.UnpackEndCut(block.PathEnd),
                    PathScaleX = Primitive.UnpackPathScale(block.PathScaleX),
                    PathScaleY = Primitive.UnpackPathScale(block.PathScaleY),
                    PathShearX = Primitive.UnpackPathShear((sbyte)block.PathShearX),
                    PathShearY = Primitive.UnpackPathShear((sbyte)block.PathShearY),
                    PathTwist = Primitive.UnpackPathTwist(block.PathTwist),
                    PathTwistBegin = Primitive.UnpackPathTwist(block.PathTwistBegin),
                    PathRadiusOffset = Primitive.UnpackPathTwist(block.PathRadiusOffset),
                    PathTaperX = Primitive.UnpackPathTaper(block.PathTaperX),
                    PathTaperY = Primitive.UnpackPathTaper(block.PathTaperY),
                    PathRevolutions = Primitive.UnpackPathRevolutions(block.PathRevolutions),
                    PathSkew = Primitive.UnpackPathTwist(block.PathSkew),
                    ProfileBegin = Primitive.UnpackBeginCut(block.ProfileBegin),
                    ProfileEnd = Primitive.UnpackEndCut(block.ProfileEnd),
                    ProfileHollow = Primitive.UnpackProfileHollow(block.ProfileHollow),
                    PCode = pcode
                };

                #endregion

                #region Decode Additional packed parameters in ObjectData
                var pos = 0;
                switch (block.ObjectData.Length)
                {
                    case 76:
                        // Collision normal for avatar
                        objectupdate.CollisionPlane = new Vector4(block.ObjectData, pos);
                        pos += 16;

                        goto case 60;
                    case 60:
                        // Position
                        objectupdate.Position = new Vector3(block.ObjectData, pos);
                        pos += 12;
                        // Velocity
                        objectupdate.Velocity = new Vector3(block.ObjectData, pos);
                        pos += 12;
                        // Acceleration
                        objectupdate.Acceleration = new Vector3(block.ObjectData, pos);
                        pos += 12;
                        // Rotation (theta)
                        objectupdate.Rotation = new Quaternion(block.ObjectData, pos, true);
                        pos += 12;
                        // Angular velocity (omega)
                        objectupdate.AngularVelocity = new Vector3(block.ObjectData, pos);
                        pos += 12;

                        break;
                    case 48:
                        // Collision normal for avatar
                        objectupdate.CollisionPlane = new Vector4(block.ObjectData, pos);
                        pos += 16;

                        goto case 32;
                    case 32:
                        // The data is an array of unsigned shorts

                        // Position
                        objectupdate.Position = new Vector3(
                            Utils.UInt16ToFloat(block.ObjectData, pos, -0.5f * 256.0f, 1.5f * 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 2, -0.5f * 256.0f, 1.5f * 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 4, -256.0f, 3.0f * 256.0f));
                        pos += 6;
                        // Velocity
                        objectupdate.Velocity = new Vector3(
                            Utils.UInt16ToFloat(block.ObjectData, pos, -256.0f, 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 2, -256.0f, 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 4, -256.0f, 256.0f));
                        pos += 6;
                        // Acceleration
                        objectupdate.Acceleration = new Vector3(
                            Utils.UInt16ToFloat(block.ObjectData, pos, -256.0f, 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 2, -256.0f, 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 4, -256.0f, 256.0f));
                        pos += 6;
                        // Rotation (theta)
                        objectupdate.Rotation = new Quaternion(
                            Utils.UInt16ToFloat(block.ObjectData, pos, -1.0f, 1.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 2, -1.0f, 1.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 4, -1.0f, 1.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 6, -1.0f, 1.0f));
                        pos += 8;
                        // Angular velocity (omega)
                        objectupdate.AngularVelocity = new Vector3(
                            Utils.UInt16ToFloat(block.ObjectData, pos, -256.0f, 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 2, -256.0f, 256.0f),
                            Utils.UInt16ToFloat(block.ObjectData, pos + 4, -256.0f, 256.0f));
                        pos += 6;

                        break;
                    case 16:
                        // The data is an array of single bytes (8-bit numbers)

                        // Position
                        objectupdate.Position = new Vector3(
                            Utils.ByteToFloat(block.ObjectData, pos, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 1, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 2, -256.0f, 256.0f));
                        pos += 3;
                        // Velocity
                        objectupdate.Velocity = new Vector3(
                            Utils.ByteToFloat(block.ObjectData, pos, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 1, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 2, -256.0f, 256.0f));
                        pos += 3;
                        // Acceleration
                        objectupdate.Acceleration = new Vector3(
                            Utils.ByteToFloat(block.ObjectData, pos, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 1, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 2, -256.0f, 256.0f));
                        pos += 3;
                        // Rotation
                        objectupdate.Rotation = new Quaternion(
                            Utils.ByteToFloat(block.ObjectData, pos, -1.0f, 1.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 1, -1.0f, 1.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 2, -1.0f, 1.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 3, -1.0f, 1.0f));
                        pos += 4;
                        // Angular Velocity
                        objectupdate.AngularVelocity = new Vector3(
                            Utils.ByteToFloat(block.ObjectData, pos, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 1, -256.0f, 256.0f),
                            Utils.ByteToFloat(block.ObjectData, pos + 2, -256.0f, 256.0f));
                        pos += 3;

                        break;
                    default:
                        Logger.Warn("Got an ObjectUpdate block with ObjectUpdate field length of " +
                                   block.ObjectData.Length, Client);

                        continue;
                }
                #endregion

                // Determine the object type and create the appropriate class
                switch (pcode)
                {
                    #region Prim and Foliage
                    case PCode.Grass:
                    case PCode.Tree:
                    case PCode.NewTree:
                    case PCode.Prim:

                        var isNewObject = !simulator.ObjectsPrimitives.ContainsKey(block.ID);

                        var prim = GetPrimitive(simulator, block.ID, block.FullID);

                        // Textures
                        objectupdate.Textures = new Primitive.TextureEntry(block.TextureEntry, 0,
                            block.TextureEntry.Length);

                        OnObjectDataBlockUpdate(new ObjectDataBlockUpdateEventArgs(simulator, prim, data, block, objectupdate, nameValues));

                        #region Update Prim Info with decoded data
                        prim.Flags = (PrimFlags)block.UpdateFlags;

                        if ((prim.Flags & PrimFlags.ZlibCompressed) != 0)
                        {
                            Logger.Warn("Got a ZlibCompressed ObjectUpdate, implement me!", Client);
                            continue;
                        }

                        // Automatically request ObjectProperties for prim if it was rezzed selected.
                        if ((prim.Flags & PrimFlags.CreateSelected) != 0)
                        {
                            SelectObject(simulator, prim.LocalID);
                        }

                        prim.NameValues = nameValues;
                        prim.LocalID = block.ID;
                        prim.ID = block.FullID;
                        prim.ParentID = block.ParentID;
                        prim.RegionHandle = update.RegionData.RegionHandle;
                        prim.Scale = block.Scale;
                        prim.ClickAction = (ClickAction)block.ClickAction;
                        prim.OwnerID = block.OwnerID;
                        prim.MediaURL = Utils.BytesToString(block.MediaURL);
                        prim.Text = Utils.BytesToString(block.Text);
                        prim.TextColor = new Color4(block.TextColor, 0, false, true);
                        prim.IsAttachment = attachment;

                        // Sound information
                        prim.Sound = block.Sound;
                        prim.SoundFlags = (SoundFlags)block.Flags;
                        prim.SoundGain = block.Gain;
                        prim.SoundRadius = block.Radius;

                        // Joint information
                        prim.Joint = (JointType)block.JointType;
                        prim.JointPivot = block.JointPivot;
                        prim.JointAxisOrAnchor = block.JointAxisOrAnchor;

                        // Object parameters
                        prim.PrimData = data;

                        // Textures, texture animations, particle system, and extra params
                        prim.Textures = objectupdate.Textures;

                        prim.TextureAnim = new Primitive.TextureAnimation(block.TextureAnim, 0);
                        prim.ParticleSys = new Primitive.ParticleSystem(block.PSBlock, 0);
                        prim.SetExtraParamsFromBytes(block.ExtraParams, 0);

                        // PCode-specific data
                        switch (pcode)
                        {
                            case PCode.Tree:
                            case PCode.NewTree:
                                if (block.Data.Length == 1)
                                    prim.TreeSpecies = (Tree)block.Data[0];
                                else
                                    Logger.Warn("Got a foliage update with an invalid TreeSpecies field");
                                //    prim.ScratchPad = Utils.EmptyBytes;
                                //    break;
                                //default:
                                //    prim.ScratchPad = new byte[block.Data.Length];
                                //    if (block.Data.Length > 0)
                                //        Buffer.BlockCopy(block.Data, 0, prim.ScratchPad, 0, prim.ScratchPad.Length);
                                break;
                        }
                        prim.ScratchPad = Utils.EmptyBytes;

                        // Packed parameters
                        prim.CollisionPlane = objectupdate.CollisionPlane;
                        prim.Position = objectupdate.Position;
                        prim.Velocity = objectupdate.Velocity;
                        prim.Acceleration = objectupdate.Acceleration;
                        prim.Rotation = objectupdate.Rotation;
                        prim.AngularVelocity = objectupdate.AngularVelocity;
                        #endregion

                        var handler = m_ObjectUpdate;
                        if (handler != null)
                        {
                            // Ensure event handlers get the computed world position when necessary
                            ThreadPool.QueueUserWorkItem(delegate (object o)
                            { handler(this, new PrimEventArgs(simulator, prim, update.RegionData.TimeDilation, isNewObject, attachment)); });
                        }
                        //OnParticleUpdate handler replacing decode particles, PCode.Particle system appears to be deprecated this is a fix
                        if (prim.ParticleSys.PartMaxAge != 0)
                        {
                            OnParticleUpdate(new ParticleUpdateEventArgs(simulator, prim.ParticleSys, prim));
                        }

                        break;
                    #endregion Prim and Foliage
                    #region Avatar
                    case PCode.Avatar:

                        var isNewAvatar = !simulator.ObjectsAvatars.ContainsKey(block.ID);

                        // Update some internals if this is our avatar
                        if (block.FullID == Client.Self.AgentID && simulator == Client.Network.CurrentSim)
                        {
                            #region Update Client.Self

                            // We need the local ID to recognize terse updates for our agent
                            Client.Self.localID = block.ID;

                            // Packed parameters
                            Client.Self.collisionPlane = objectupdate.CollisionPlane;
                            Client.Self.relativePosition = objectupdate.Position;
                            Client.Self.velocity = objectupdate.Velocity;
                            Client.Self.acceleration = objectupdate.Acceleration;
                            Client.Self.relativeRotation = objectupdate.Rotation;
                            Client.Self.angularVelocity = objectupdate.AngularVelocity;

                            #endregion
                        }

                        #region Create an Avatar from the decoded data

                        var avatar = GetAvatar(simulator, block.ID, block.FullID);

                        objectupdate.Avatar = true;
                        // Textures
                        objectupdate.Textures = new Primitive.TextureEntry(block.TextureEntry, 0,
                            block.TextureEntry.Length);

                        OnObjectDataBlockUpdate(new ObjectDataBlockUpdateEventArgs(simulator, avatar, data, block, objectupdate, nameValues));

                        var oldSeatID = avatar.ParentID;

                        avatar.ID = block.FullID;
                        avatar.LocalID = block.ID;
                        avatar.Scale = block.Scale;
                        avatar.CollisionPlane = objectupdate.CollisionPlane;
                        avatar.Position = objectupdate.Position;
                        avatar.Velocity = objectupdate.Velocity;
                        avatar.Acceleration = objectupdate.Acceleration;
                        avatar.Rotation = objectupdate.Rotation;
                        avatar.AngularVelocity = objectupdate.AngularVelocity;
                        avatar.NameValues = nameValues;
                        if (nameValues.Length > 0)
                        {
                            // Not great modularity, but considering how often this method runs, better to not, e.g., have Avatar define an ObjectDataBlockUpdate handler.
                            avatar._cachedName = avatar._cachedGroupName = null;
                        }
                        avatar.PrimData = data;
                        if (block.Data.Length > 0)
                        {
                            Logger.Warn("Unexpected Data field for an avatar update, length " + block.Data.Length);
                        }
                        avatar.ParentID = block.ParentID;
                        avatar.RegionHandle = update.RegionData.RegionHandle;

                        SetAvatarSittingOn(simulator, avatar, block.ParentID, oldSeatID);

                        // Textures
                        avatar.Textures = objectupdate.Textures;

                        #endregion Create an Avatar from the decoded data

                        // Provide avatar position accounting for parent prim when invoking handlers
                        OnAvatarUpdate(new AvatarUpdateEventArgs(simulator, avatar, update.RegionData.TimeDilation, isNewAvatar));

                        break;
                    #endregion Avatar
                    case PCode.ParticleSystem:
                        DecodeParticleUpdate(block);
                        break;
                    default:
                        Logger.Debug("Got an ObjectUpdate block with an unrecognized PCode " + pcode, Client);
                        break;
                }
            }
        }

        protected void DecodeParticleUpdate(ObjectUpdatePacket.ObjectDataBlock block)
        {
            // TODO: Handle ParticleSystem ObjectUpdate blocks
            // float bounce_b
            // Vector4 scale_range
            // Vector4 alpha_range
            // Vector3 vel_offset
            // float dist_begin_fadeout
            // float dist_end_fadeout
            // UUID image_uuid
            // long flags
            // byte createme
            // Vector3 diff_eq_alpha
            // Vector3 diff_eq_scale
            // byte max_particles
            // byte initial_particles
            // float kill_plane_z
            // Vector3 kill_plane_normal
            // float bounce_plane_z
            // Vector3 bounce_plane_normal
            // float spawn_range
            // float spawn_frequency
            // float spawn_frequency_range
            // Vector3 spawn_direction
            // float spawn_direction_range
            // float spawn_velocity
            // float spawn_velocity_range
            // float speed_limit
            // float wind_weight
            // Vector3 current_gravity
            // float gravity_weight
            // float global_lifetime
            // float individual_lifetime
            // float individual_lifetime_range
            // float alpha_decay
            // float scale_decay
            // float distance_death
            // float damp_motion_factor
            // Vector3 wind_diffusion_factor
        }

        /// <summary>
        /// A terse object update, used when a transformation matrix or
        /// velocity/acceleration for an object changes but nothing else
        /// (scale/position/rotation/acceleration/velocity)
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ImprovedTerseObjectUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var simulator = e.Simulator;

            var terse = (ImprovedTerseObjectUpdatePacket)packet;
            UpdateDilation(simulator, terse.RegionData.TimeDilation);

            foreach (var block in terse.ObjectData)
            {
                try
                {
                    var pos = 4;
                    var localid = Utils.BytesToUInt(block.Data, 0);

                    // Check if we are interested in this update
                    if (!Client.Settings.ALWAYS_DECODE_OBJECTS
                        && localid != Client.Self.localID
                        && m_TerseObjectUpdate == null)
                    {
                        continue;
                    }

                    #region Decode update data

                    var update = new ObjectMovementUpdate
                    {
                        // LocalID
                        LocalID = localid,
                        // State
                        State = block.Data[pos++],
                        // Avatar boolean
                        Avatar = (block.Data[pos++] != 0)
                    };

                    // Collision normal for avatar
                    if (update.Avatar)
                    {
                        update.CollisionPlane = new Vector4(block.Data, pos);
                        pos += 16;
                    }
                    // Position
                    update.Position = new Vector3(block.Data, pos);
                    pos += 12;
                    // Velocity
                    update.Velocity = new Vector3(
                        Utils.UInt16ToFloat(block.Data, pos, -128.0f, 128.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 2, -128.0f, 128.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 4, -128.0f, 128.0f));
                    pos += 6;
                    // Acceleration
                    update.Acceleration = new Vector3(
                        Utils.UInt16ToFloat(block.Data, pos, -64.0f, 64.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 2, -64.0f, 64.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 4, -64.0f, 64.0f));
                    pos += 6;
                    // Rotation (theta)
                    update.Rotation = new Quaternion(
                        Utils.UInt16ToFloat(block.Data, pos, -1.0f, 1.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 2, -1.0f, 1.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 4, -1.0f, 1.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 6, -1.0f, 1.0f));
                    pos += 8;
                    // Angular velocity (omega)
                    update.AngularVelocity = new Vector3(
                        Utils.UInt16ToFloat(block.Data, pos, -64.0f, 64.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 2, -64.0f, 64.0f),
                        Utils.UInt16ToFloat(block.Data, pos + 4, -64.0f, 64.0f));
                    pos += 6;

                    // Textures
                    // FIXME: Why are we ignoring the first four bytes here?
                    if (block.TextureEntry.Length != 0)
                        update.Textures = new Primitive.TextureEntry(block.TextureEntry, 4, block.TextureEntry.Length - 4);

                    #endregion Decode update data

                    var obj = !Client.Settings.OBJECT_TRACKING ? null : (update.Avatar) ?
                        GetAvatar(simulator, update.LocalID, UUID.Zero) :
                        GetPrimitive(simulator, update.LocalID, UUID.Zero);

                    // Fire the pre-emptive notice (before we stomp the object)
                    var handler = m_TerseObjectUpdate;
                    if (handler != null)
                    {
                        ThreadPool.QueueUserWorkItem(delegate (object o)
                        { handler(this, new TerseObjectUpdateEventArgs(simulator, obj, update, terse.RegionData.TimeDilation)); });
                    }

                    #region Update Client.Self
                    if (update.LocalID == Client.Self.localID)
                    {
                        Client.Self.collisionPlane = update.CollisionPlane;
                        Client.Self.relativePosition = update.Position;
                        Client.Self.velocity = update.Velocity;
                        Client.Self.acceleration = update.Acceleration;
                        Client.Self.relativeRotation = update.Rotation;
                        Client.Self.angularVelocity = update.AngularVelocity;
                    }
                    #endregion Update Client.Self
                    if (Client.Settings.OBJECT_TRACKING && obj != null)
                    {
                        obj.Position = update.Position;
                        obj.Rotation = update.Rotation;
                        obj.Velocity = update.Velocity;
                        obj.CollisionPlane = update.CollisionPlane;
                        obj.Acceleration = update.Acceleration;
                        obj.AngularVelocity = update.AngularVelocity;
                        obj.PrimData.State = update.State;
                        if (update.Textures != null)
                            obj.Textures = update.Textures;
                    }

                }
                catch (Exception ex)
                {
                    Logger.Warn(ex.Message, ex, Client);
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ObjectUpdateCompressedHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var simulator = e.Simulator;

            var update = (ObjectUpdateCompressedPacket)packet;

            foreach (var block in update.ObjectData)
            {
                var i = 0;

                try
                {
                    // UUID
                    var FullID = new UUID(block.Data, 0);
                    i += 16;
                    // Local ID
                    var LocalID = (uint)(block.Data[i++] + (block.Data[i++] << 8) +
                                         (block.Data[i++] << 16) + (block.Data[i++] << 24));
                    // PCode
                    var pcode = (PCode)block.Data[i++];

                    #region Relevance check

                    if (!Client.Settings.ALWAYS_DECODE_OBJECTS)
                    {
                        switch (pcode)
                        {
                            case PCode.Grass:
                            case PCode.Tree:
                            case PCode.NewTree:
                            case PCode.Prim:
                                if (m_ObjectUpdate == null) continue;
                                break;
                        }
                    }

                    #endregion Relevance check

                    var isNew = !simulator.ObjectsPrimitives.ContainsKey(LocalID);

                    var prim = GetPrimitive(simulator, LocalID, FullID);

                    prim.LocalID = LocalID;
                    prim.ID = FullID;
                    prim.Flags = (PrimFlags)block.UpdateFlags;
                    prim.PrimData.PCode = pcode;

                    #region Decode block and update Prim

                    // State
                    prim.PrimData.State = block.Data[i++];
                    // CRC
                    i += 4;
                    // Material
                    prim.PrimData.Material = (Material)block.Data[i++];
                    // Click action
                    prim.ClickAction = (ClickAction)block.Data[i++];
                    // Scale
                    prim.Scale = new Vector3(block.Data, i);
                    i += 12;
                    // Position
                    prim.Position = new Vector3(block.Data, i);
                    i += 12;
                    // Rotation
                    prim.Rotation = new Quaternion(block.Data, i, true);
                    i += 12;
                    // Compressed flags
                    var flags = (CompressedFlags)Utils.BytesToUInt(block.Data, i);
                    i += 4;

                    prim.OwnerID = new UUID(block.Data, i);
                    i += 16;

                    // Angular velocity
                    if ((flags & CompressedFlags.HasAngularVelocity) != 0)
                    {
                        prim.AngularVelocity = new Vector3(block.Data, i);
                        i += 12;
                    }

                    // Parent ID
                    if ((flags & CompressedFlags.HasParent) != 0)
                    {
                        prim.ParentID = (uint)(block.Data[i++] + (block.Data[i++] << 8) +
                                               (block.Data[i++] << 16) + (block.Data[i++] << 24));
                    }
                    else
                    {
                        prim.ParentID = 0;
                    }

                    // Tree data
                    if ((flags & CompressedFlags.Tree) != 0)
                    {
                        prim.TreeSpecies = (Tree)block.Data[i++];
                        //prim.ScratchPad = Utils.EmptyBytes;
                    }
                    // Scratch pad
                    else if ((flags & CompressedFlags.ScratchPad) != 0)
                    {
                        prim.TreeSpecies = 0;

                        int size = block.Data[i++];
                        //prim.ScratchPad = new byte[size];
                        //Buffer.BlockCopy(block.Data, i, prim.ScratchPad, 0, size);
                        i += size;
                    }
                    prim.ScratchPad = Utils.EmptyBytes;

                    // Floating text
                    if ((flags & CompressedFlags.HasText) != 0)
                    {
                        var idx = i;
                        while (block.Data[i] != 0)
                        {
                            i++;
                        }

                        // Floating text
                        prim.Text = Utils.BytesToString(block.Data, idx, i - idx);
                        i++;

                        // Text color
                        prim.TextColor = new Color4(block.Data, i, false, true);
                        i += 4;
                    }
                    else
                    {
                        prim.Text = string.Empty;
                    }

                    // Media URL
                    if ((flags & CompressedFlags.MediaURL) != 0)
                    {
                        var idx = i;
                        while (block.Data[i] != 0)
                        {
                            i++;
                        }

                        prim.MediaURL = Utils.BytesToString(block.Data, idx, i - idx);
                        i++;
                    }

                    // Particle system
                    if ((flags & CompressedFlags.HasParticles) != 0)
                    {
                        prim.ParticleSys = new Primitive.ParticleSystem(block.Data, i);
                        i += 86;
                    }

                    // Extra parameters
                    i += prim.SetExtraParamsFromBytes(block.Data, i);

                    //Sound data
                    if ((flags & CompressedFlags.HasSound) != 0)
                    {
                        prim.Sound = new UUID(block.Data, i);
                        i += 16;

                        prim.SoundGain = Utils.BytesToFloat(block.Data, i);
                        i += 4;
                        prim.SoundFlags = (SoundFlags)block.Data[i++];
                        prim.SoundRadius = Utils.BytesToFloat(block.Data, i);
                        i += 4;
                    }

                    // Name values
                    if ((flags & CompressedFlags.HasNameValues) != 0)
                    {
                        var text = string.Empty;
                        while (block.Data[i] != 0)
                        {
                            text += (char)block.Data[i];
                            i++;
                        }
                        i++;

                        // Parse the name values
                        if (text.Length > 0)
                        {
                            var lines = text.Split('\n');
                            prim.NameValues = new NameValue[lines.Length];

                            for (var j = 0; j < lines.Length; j++)
                            {
                                if (string.IsNullOrEmpty(lines[j]))
                                    continue;

                                var nv = new NameValue(lines[j]);
                                prim.NameValues[j] = nv;
                            }
                        }
                    }

                    prim.PrimData.PathCurve = (PathCurve)block.Data[i++];
                    var pathBegin = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.PathBegin = Primitive.UnpackBeginCut(pathBegin);
                    var pathEnd = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.PathEnd = Primitive.UnpackEndCut(pathEnd);
                    prim.PrimData.PathScaleX = Primitive.UnpackPathScale(block.Data[i++]);
                    prim.PrimData.PathScaleY = Primitive.UnpackPathScale(block.Data[i++]);
                    prim.PrimData.PathShearX = Primitive.UnpackPathShear((sbyte)block.Data[i++]);
                    prim.PrimData.PathShearY = Primitive.UnpackPathShear((sbyte)block.Data[i++]);
                    prim.PrimData.PathTwist = Primitive.UnpackPathTwist((sbyte)block.Data[i++]);
                    prim.PrimData.PathTwistBegin = Primitive.UnpackPathTwist((sbyte)block.Data[i++]);
                    prim.PrimData.PathRadiusOffset = Primitive.UnpackPathTwist((sbyte)block.Data[i++]);
                    prim.PrimData.PathTaperX = Primitive.UnpackPathTaper((sbyte)block.Data[i++]);
                    prim.PrimData.PathTaperY = Primitive.UnpackPathTaper((sbyte)block.Data[i++]);
                    prim.PrimData.PathRevolutions = Primitive.UnpackPathRevolutions(block.Data[i++]);
                    prim.PrimData.PathSkew = Primitive.UnpackPathTwist((sbyte)block.Data[i++]);

                    prim.PrimData.profileCurve = block.Data[i++];
                    var profileBegin = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.ProfileBegin = Primitive.UnpackBeginCut(profileBegin);
                    var profileEnd = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.ProfileEnd = Primitive.UnpackEndCut(profileEnd);
                    var profileHollow = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.ProfileHollow = Primitive.UnpackProfileHollow(profileHollow);

                    // TextureEntry
                    var textureEntryLength = (int)Utils.BytesToUInt(block.Data, i);
                    i += 4;
                    prim.Textures = new Primitive.TextureEntry(block.Data, i, textureEntryLength);
                    i += textureEntryLength;

                    // Texture animation
                    if ((flags & CompressedFlags.TextureAnimation) != 0)
                    {
                        //int textureAnimLength = (int)Utils.BytesToUIntBig(block.Data, i);
                        i += 4;
                        prim.TextureAnim = new Primitive.TextureAnimation(block.Data, i);
                    }

                    #endregion

                    prim.IsAttachment = (flags & CompressedFlags.HasNameValues) != 0 && prim.ParentID != 0;

                    #region Raise Events

                    var handler = m_ObjectUpdate;
                    handler?.Invoke(this, new PrimEventArgs(simulator, prim, update.RegionData.TimeDilation, isNew, prim.IsAttachment));

                    #endregion
                }
                catch (IndexOutOfRangeException ex)
                {
                    Logger.Warn("Error decoding an ObjectUpdateCompressed packet", ex, Client);
                    Logger.Warn(block);
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ObjectUpdateCachedHandler(object sender, PacketReceivedEventArgs e)
        {
            if (Client.Settings.ALWAYS_REQUEST_OBJECTS)
            {
                var cachedPrimitives = Client.Settings.CACHE_PRIMITIVES;
                var packet = e.Packet;
                var simulator = e.Simulator;

                var update = (ObjectUpdateCachedPacket)packet;
                var ids = new List<uint>(update.ObjectData.Length);

                // Object caching is implemented when Client.Settings.PRIMITIVES_FACTORY is True, otherwise request updates for all of these objects
                foreach (var odb in update.ObjectData)
                {
                    var localID = odb.ID;
                    var crc = odb.CRC;

                    if (cachedPrimitives)
                    {
                        if (!simulator.DataPool.NeedsRequest(localID, crc))
                        {
                            continue;
                        }
                    }
                    ids.Add(localID);
                }
                RequestObjects(simulator, ids);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void KillObjectHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var simulator = e.Simulator;

            var kill = (KillObjectPacket)packet;

            // Notify first, so that handler has a chance to get a
            // reference from the ObjectTracker to the object being killed

            var localIdsToKill = new List<uint>(kill.ObjectData.Length);
            foreach (var objectToKill in kill.ObjectData)
            {
                if (objectToKill.ID == Client.Self.localID)
                {
                    continue;
                }

                localIdsToKill.Add(objectToKill.ID);
                OnKillObject(new KillObjectEventArgs(simulator, objectToKill.ID));
            }

            OnKillObjects(new KillObjectsEventArgs(e.Simulator, localIdsToKill.ToArray()));

            var parentIndex = new Dictionary<uint, List<uint>>();
            foreach (var kv in simulator.ObjectsPrimitives)
            {
                var primId = kv.Key;
                var parentId = kv.Value.ParentID;
                if (parentId == 0) { continue; }

                if (!parentIndex.TryGetValue(parentId, out var list))
                {
                    list = new List<uint>(4);
                    parentIndex[parentId] = list;
                }
                list.Add(primId);
            }

            // For each localID to kill, find direct children quickly and traverse descendants
            var removePrims = new List<uint>();
            var visited = new HashSet<uint>(); // reused per batch
            foreach (var localID in localIdsToKill)
            {
                // If the object itself exists as a prim, remove it
                if (simulator.ObjectsPrimitives.ContainsKey(localID))
                {
                    removePrims.Add(localID);
                    visited.Add(localID);
                }

                // If there are direct children, traverse the tree of descendents
                if (parentIndex.TryGetValue(localID, out var directChildren))
                {
                    var stack = new Stack<uint>(directChildren.Count);
                    foreach (var child in directChildren)
                    {
                        if (visited.Add(child))
                        {
                            stack.Push(child);
                            removePrims.Add(child);
                        }
                    }

                    while (stack.Count > 0)
                    {
                        var cur = stack.Pop();
                        if (parentIndex.TryGetValue(cur, out var children))
                        {
                            foreach (var c in children)
                            {
                                if (visited.Add(c))
                                {
                                    stack.Push(c);
                                    removePrims.Add(c);
                                }
                            }
                        }
                    }
                }

                _ = simulator.ObjectsAvatars.TryRemove(localID, out _);
            }

            if (Client.Settings.CACHE_PRIMITIVES)
            {
                simulator.DataPool.ReleasePrims(removePrims);
            }
            foreach (var removeID in removePrims)
            {
                simulator.ObjectsPrimitives.TryRemove(removeID, out _);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ObjectPropertiesHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var simulator = e.Simulator;

            var op = (ObjectPropertiesPacket)packet;
            var datablocks = op.ObjectData;

            foreach (var objectData in datablocks)
            {
                var props = new Primitive.ObjectProperties
                {
                    ObjectID = objectData.ObjectID,
                    AggregatePerms = objectData.AggregatePerms,
                    AggregatePermTextures = objectData.AggregatePermTextures,
                    AggregatePermTexturesOwner = objectData.AggregatePermTexturesOwner,
                    Permissions = new Permissions(objectData.BaseMask, objectData.EveryoneMask, objectData.GroupMask,
                        objectData.NextOwnerMask, objectData.OwnerMask),
                    Category = (ObjectCategory)objectData.Category,
                    CreationDate = Utils.UnixTimeToDateTime((uint)objectData.CreationDate),
                    CreatorID = objectData.CreatorID,
                    Description = Utils.BytesToString(objectData.Description),
                    FolderID = objectData.FolderID,
                    FromTaskID = objectData.FromTaskID,
                    GroupID = objectData.GroupID,
                    InventorySerial = objectData.InventorySerial,
                    ItemID = objectData.ItemID,
                    LastOwnerID = objectData.LastOwnerID,
                    Name = Utils.BytesToString(objectData.Name),
                    OwnerID = objectData.OwnerID,
                    OwnershipCost = objectData.OwnershipCost,
                    SalePrice = objectData.SalePrice,
                    SaleType = (SaleType)objectData.SaleType,
                    SitName = Utils.BytesToString(objectData.SitName),
                    TouchName = Utils.BytesToString(objectData.TouchName)
                };

                var numTextures = objectData.TextureID.Length / 16;
                props.TextureIDs = new UUID[numTextures];
                for (var j = 0; j < numTextures; ++j)
                    props.TextureIDs[j] = new UUID(objectData.TextureID, j * 16);

                if (Client.Settings.OBJECT_TRACKING)
                {
                    if (simulator.GlobalToLocalID.TryGetValue(props.ObjectID, out var localID))
                    {
                        if (simulator.ObjectsPrimitives.TryGetValue(localID, out var findPrim))
                        {
                            if (findPrim != null)
                            {
                                OnObjectPropertiesUpdated(new ObjectPropertiesUpdatedEventArgs(simulator, findPrim, props));

                                if (simulator.ObjectsPrimitives.TryGetValue(findPrim.LocalID, out var primitive))
                                {
                                    primitive.Properties = props;
                                }
                            }
                        }
                    }
                }

                OnObjectProperties(new ObjectPropertiesEventArgs(simulator, props));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ObjectPropertiesFamilyHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var simulator = e.Simulator;

            var op = (ObjectPropertiesFamilyPacket)packet;
            var props = new Primitive.ObjectProperties();

            var requestType = (ReportType)op.ObjectData.RequestFlags;

            props.ObjectID = op.ObjectData.ObjectID;
            props.Category = (ObjectCategory)op.ObjectData.Category;
            props.Description = Utils.BytesToString(op.ObjectData.Description);
            props.GroupID = op.ObjectData.GroupID;
            props.LastOwnerID = op.ObjectData.LastOwnerID;
            props.Name = Utils.BytesToString(op.ObjectData.Name);
            props.OwnerID = op.ObjectData.OwnerID;
            props.OwnershipCost = op.ObjectData.OwnershipCost;
            props.SalePrice = op.ObjectData.SalePrice;
            props.SaleType = (SaleType)op.ObjectData.SaleType;
            props.Permissions.BaseMask = (PermissionMask)op.ObjectData.BaseMask;
            props.Permissions.EveryoneMask = (PermissionMask)op.ObjectData.EveryoneMask;
            props.Permissions.GroupMask = (PermissionMask)op.ObjectData.GroupMask;
            props.Permissions.NextOwnerMask = (PermissionMask)op.ObjectData.NextOwnerMask;
            props.Permissions.OwnerMask = (PermissionMask)op.ObjectData.OwnerMask;

            if (Client.Settings.OBJECT_TRACKING)
            {
                if (simulator.GlobalToLocalID.TryGetValue(props.ObjectID, out var localID))
                {
                    if (simulator.ObjectsPrimitives.TryGetValue(localID, out var findPrim))
                    {
                        if (findPrim != null)
                        {
                            if (simulator.ObjectsPrimitives.TryGetValue(findPrim.LocalID, out var prim))
                            {
                                if (prim.Properties == null)
                                {
                                    prim.Properties = new Primitive.ObjectProperties();
                                }

                                prim.Properties.SetFamilyProperties(props);
                            }
                        }
                    }
                }
            }

            OnObjectPropertiesFamily(new ObjectPropertiesFamilyEventArgs(simulator, props, requestType));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void PayPriceReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_PayPriceReply != null)
            {
                var packet = e.Packet;
                var simulator = e.Simulator;

                var p = (PayPriceReplyPacket)packet;
                var objectID = p.ObjectData.ObjectID;
                var defaultPrice = p.ObjectData.DefaultPayPrice;
                var buttonPrices = new int[p.ButtonData.Length];

                for (var i = 0; i < p.ButtonData.Length; i++)
                {
                    buttonPrices[i] = p.ButtonData[i].PayButton;
                }

                OnPayPriceReply(new PayPriceReplyEventArgs(simulator, objectID, defaultPrice, buttonPrices));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="capsKey"></param>
        /// <param name="message"></param>
        /// <param name="simulator"></param>
        protected void ObjectPhysicsPropertiesHandler(string capsKey, IMessage message, Simulator simulator)
        {
            var msg = (ObjectPhysicsPropertiesMessage)message;

            if (Client.Settings.OBJECT_TRACKING)
            {
                foreach (var prop in msg.ObjectPhysicsProperties)
                {
                    if (simulator.ObjectsPrimitives.TryGetValue(prop.LocalID, out var primitive))
                    {
                        primitive.PhysicsProps = prop;
                    }
                }
            }

            if (m_PhysicsProperties != null)
            {
                foreach (var prop in msg.ObjectPhysicsProperties)
                {
                    OnPhysicsProperties(new PhysicsPropertiesEventArgs(simulator, prop));
                }
            }
        }
    }
}
