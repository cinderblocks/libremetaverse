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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Materials;
using OpenMetaverse.Packets;
using OpenMetaverse.Http;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;

namespace OpenMetaverse
{
    #region Enums

    /// <summary>
    /// 
    /// </summary>
    public enum ReportType : uint
    {
        /// <summary>No report</summary>
        None = 0,
        /// <summary>Unknown report type</summary>
        Unknown = 1,
        /// <summary>Bug report</summary>
        Bug = 2,
        /// <summary>Complaint report</summary>
        Complaint = 3,
        /// <summary>Customer service report</summary>
        CustomerServiceRequest = 4
    }

    /// <summary>
    /// Bitflag field for ObjectUpdateCompressed data blocks, describing 
    /// which options are present for each object
    /// </summary>
    [Flags]
    public enum CompressedFlags : uint
    {
        None = 0x00,
        /// <summary>Unknown</summary>
        ScratchPad = 0x01,
        /// <summary>Whether the object has a TreeSpecies</summary>
        Tree = 0x02,
        /// <summary>Whether the object has floating text ala llSetText</summary>
        HasText = 0x04,
        /// <summary>Whether the object has an active particle system</summary>
        HasParticles = 0x08,
        /// <summary>Whether the object has sound attached to it</summary>
        HasSound = 0x10,
        /// <summary>Whether the object is attached to a root object or not</summary>
        HasParent = 0x20,
        /// <summary>Whether the object has texture animation settings</summary>
        TextureAnimation = 0x40,
        /// <summary>Whether the object has an angular velocity</summary>
        HasAngularVelocity = 0x80,
        /// <summary>Whether the object has a name value pairs string</summary>
        HasNameValues = 0x100,
        /// <summary>Whether the object has a Media URL set</summary>
        MediaURL = 0x200,
        /// <summary>Whether the object has, you guessed it, new particles</summary>
        HasParticlesNew = 0x400
    }

    /// <summary>
    /// Specific Flags for MultipleObjectUpdate requests
    /// </summary>
    [Flags]
    public enum UpdateType : uint
    {
        /// <summary>None</summary>
        None = 0x00,
        /// <summary>Change position of prims</summary>
        Position = 0x01,
        /// <summary>Change rotation of prims</summary>
        Rotation = 0x02,
        /// <summary>Change size of prims</summary>
        Scale = 0x04,
        /// <summary>Perform operation on link set</summary>
        Linked = 0x08,
        /// <summary>Scale prims uniformly, same as selecting ctrl+shift in the
        /// viewer. Used in conjunction with Scale</summary>
        Uniform = 0x10
    }

    /// <summary>
    /// Special values in PayPriceReply. If the price is not one of these
    /// literal value of the price should be use
    /// </summary>
    public enum PayPriceType : int
    {
        /// <summary>
        /// Indicates that this pay option should be hidden
        /// </summary>
        Hide = -1,

        /// <summary>
        /// Indicates that this pay option should have the default value
        /// </summary>
        Default = -2
    }

    #endregion Enums

    #region Structs

    /// <summary>
    /// Contains the variables sent in an object update packet for objects. 
    /// Used to track position and movement of prims and avatars
    /// </summary>
    public struct ObjectMovementUpdate
    {
        /// <summary></summary>
        public bool Avatar;
        /// <summary></summary>
        public Vector4 CollisionPlane;
        /// <summary></summary>
        public byte State;
        /// <summary></summary>
        public uint LocalID;
        /// <summary></summary>
        public Vector3 Position;
        /// <summary></summary>
        public Vector3 Velocity;
        /// <summary></summary>
        public Vector3 Acceleration;
        /// <summary></summary>
        public Quaternion Rotation;
        /// <summary></summary>
        public Vector3 AngularVelocity;
        /// <summary></summary>
        public Primitive.TextureEntry Textures;
    }

    #endregion Structs

    /// <summary>
    /// Handles all network traffic related to prims and avatar positions and 
    /// movement.
    /// </summary>
    public class ObjectManager
    {
        public const float HAVOK_TIMESTEP = 1.0f / 45.0f;

        #region Delegates

        #region ObjectAnimation event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ObjectAnimationEventArgs> m_ObjectAnimation;

        ///<summary>Raises the ObjectAnimation Event</summary>
        /// <param name="e">An ObjectAnimationEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnObjectAnimation(ObjectAnimationEventArgs e)
        {
            EventHandler<ObjectAnimationEventArgs> handler = m_ObjectAnimation;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ObjectAnimationLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// an agents animation playlist</summary>
        public event EventHandler<ObjectAnimationEventArgs> ObjectAnimation
        {
            add { lock (m_ObjectAnimationLock) { m_ObjectAnimation += value; } }
            remove { lock (m_ObjectAnimationLock) { m_ObjectAnimation -= value; } }
        }
        #endregion ObjectAnimation event

        #region ObjectUpdate event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<PrimEventArgs> m_ObjectUpdate;

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ObjectUpdateLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// A <see cref="Primitive"/>, Foliage or Attachment</summary>
        /// <seealso cref="RequestObject"/>
        /// <seealso cref="RequestObjects"/>
        public event EventHandler<PrimEventArgs> ObjectUpdate
        {
            add { lock (m_ObjectUpdateLock) { m_ObjectUpdate += value; } }
            remove { lock (m_ObjectUpdateLock) { m_ObjectUpdate -= value; } }
        }
        #endregion ObjectUpdate event

        #region ObjectProperties event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ObjectPropertiesEventArgs> m_ObjectProperties;

        ///<summary>Raises the ObjectProperties Event</summary>
        /// <param name="e">A ObjectPropertiesEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnObjectProperties(ObjectPropertiesEventArgs e)
        {
            EventHandler<ObjectPropertiesEventArgs> handler = m_ObjectProperties;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ObjectPropertiesLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// additional <seea cref="Primitive"/> information</summary>
        /// <seealso cref="SelectObject"/>
        /// <seealso cref="SelectObjects"/>
        public event EventHandler<ObjectPropertiesEventArgs> ObjectProperties
        {
            add { lock (m_ObjectPropertiesLock) { m_ObjectProperties += value; } }
            remove { lock (m_ObjectPropertiesLock) { m_ObjectProperties -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ObjectPropertiesUpdatedEventArgs> m_ObjectPropertiesUpdated;

        ///<summary>Raises the ObjectPropertiesUpdated Event</summary>
        /// <param name="e">A ObjectPropertiesUpdatedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnObjectPropertiesUpdated(ObjectPropertiesUpdatedEventArgs e)
        {
            EventHandler<ObjectPropertiesUpdatedEventArgs> handler = m_ObjectPropertiesUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ObjectPropertiesUpdatedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// Primitive.ObjectProperties for an object we are currently tracking</summary>
        public event EventHandler<ObjectPropertiesUpdatedEventArgs> ObjectPropertiesUpdated
        {
            add { lock (m_ObjectPropertiesUpdatedLock) { m_ObjectPropertiesUpdated += value; } }
            remove { lock (m_ObjectPropertiesUpdatedLock) { m_ObjectPropertiesUpdated -= value; } }
        }
        #endregion ObjectProperties event

        #region ObjectPropertiesFamily event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ObjectPropertiesFamilyEventArgs> m_ObjectPropertiesFamily;

        ///<summary>Raises the ObjectPropertiesFamily Event</summary>
        /// <param name="e">A ObjectPropertiesFamilyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnObjectPropertiesFamily(ObjectPropertiesFamilyEventArgs e)
        {
            EventHandler<ObjectPropertiesFamilyEventArgs> handler = m_ObjectPropertiesFamily;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ObjectPropertiesFamilyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// additional <seea cref="Primitive"/> and <see cref="Avatar"/> details</summary>
        /// <seealso cref="RequestObjectPropertiesFamily"/>
        public event EventHandler<ObjectPropertiesFamilyEventArgs> ObjectPropertiesFamily
        {
            add { lock (m_ObjectPropertiesFamilyLock) { m_ObjectPropertiesFamily += value; } }
            remove { lock (m_ObjectPropertiesFamilyLock) { m_ObjectPropertiesFamily -= value; } }
        }
        #endregion ObjectPropertiesFamily

        #region AvatarUpdate event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarUpdateEventArgs> m_AvatarUpdate;
        private EventHandler<ParticleUpdateEventArgs> m_ParticleUpdate;

        ///<summary>Raises the AvatarUpdate Event</summary>
        /// <param name="e">A AvatarUpdateEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarUpdate(AvatarUpdateEventArgs e)
        {
            EventHandler<AvatarUpdateEventArgs> handler = m_AvatarUpdate;
            handler?.Invoke(this, e);
        }
        /// <summary>
        /// Raises the ParticleUpdate Event
        /// </summary>
        /// <param name="e">A ParticleUpdateEventArgs object containing 
        /// the data sent from the simulator</param>
        protected virtual void OnParticleUpdate(ParticleUpdateEventArgs e) {
            EventHandler<ParticleUpdateEventArgs> handler = m_ParticleUpdate;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarUpdateLock = new object();

        private readonly object m_ParticleUpdateLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// updated information for an <see cref="Avatar"/></summary>
        public event EventHandler<AvatarUpdateEventArgs> AvatarUpdate
        {
            add { lock (m_AvatarUpdateLock) { m_AvatarUpdate += value; } }
            remove { lock (m_AvatarUpdateLock) { m_AvatarUpdate -= value; } }
        }
        #endregion AvatarUpdate event

        #region TerseObjectUpdate event
        public event EventHandler<ParticleUpdateEventArgs> ParticleUpdate {
            add { lock (m_ParticleUpdateLock) { m_ParticleUpdate += value; } }
            remove { lock (m_ParticleUpdateLock) { m_ParticleUpdate -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<TerseObjectUpdateEventArgs> m_TerseObjectUpdate;

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TerseObjectUpdateLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// <see cref="Primitive"/> and <see cref="Avatar"/> movement changes</summary>
        public event EventHandler<TerseObjectUpdateEventArgs> TerseObjectUpdate
        {
            add { lock (m_TerseObjectUpdateLock) { m_TerseObjectUpdate += value; } }
            remove { lock (m_TerseObjectUpdateLock) { m_TerseObjectUpdate -= value; } }
        }
        #endregion TerseObjectUpdate event

        #region ObjectDataBlockUpdate event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ObjectDataBlockUpdateEventArgs> m_ObjectDataBlockUpdate;

        ///<summary>Raises the ObjectDataBlockUpdate Event</summary>
        /// <param name="e">A ObjectDataBlockUpdateEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnObjectDataBlockUpdate(ObjectDataBlockUpdateEventArgs e)
        {
            EventHandler<ObjectDataBlockUpdateEventArgs> handler = m_ObjectDataBlockUpdate;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ObjectDataBlockUpdateLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// updates to an Objects DataBlock</summary>
        public event EventHandler<ObjectDataBlockUpdateEventArgs> ObjectDataBlockUpdate
        {
            add { lock (m_ObjectDataBlockUpdateLock) { m_ObjectDataBlockUpdate += value; } }
            remove { lock (m_ObjectDataBlockUpdateLock) { m_ObjectDataBlockUpdate -= value; } }
        }
        #endregion ObjectDataBlockUpdate event

        #region KillObject event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<KillObjectEventArgs> m_KillObject;

        ///<summary>Raises the KillObject Event</summary>
        /// <param name="e">A KillObjectEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnKillObject(KillObjectEventArgs e)
        {
            EventHandler<KillObjectEventArgs> handler = m_KillObject;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_KillObjectLock = new object();

        /// <summary>Raised when the simulator informs us an <see cref="Primitive"/>
        /// or <see cref="Avatar"/> is no longer within view</summary>
        public event EventHandler<KillObjectEventArgs> KillObject
        {
            add { lock (m_KillObjectLock) { m_KillObject += value; } }
            remove { lock (m_KillObjectLock) { m_KillObject -= value; } }
        }
        #endregion KillObject event

        #region KillObjects event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<KillObjectsEventArgs> m_KillObjects;

        ///<summary>Raises the KillObjects Event</summary>
        /// <param name="e">A KillObjectsEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnKillObjects(KillObjectsEventArgs e)
        {
            EventHandler<KillObjectsEventArgs> handler = m_KillObjects;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_KillObjectsLock = new object();

        /// <summary>Raised when the simulator informs us when a group of <see cref="Primitive"/>
        /// or <see cref="Avatar"/> is no longer within view</summary>
        public event EventHandler<KillObjectsEventArgs> KillObjects
        {
            add { lock (m_KillObjectsLock) { m_KillObjects += value; } }
            remove { lock (m_KillObjectsLock) { m_KillObjects -= value; } }
        }
        #endregion KillObjects event

        #region AvatarSitChanged event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarSitChangedEventArgs> m_AvatarSitChanged;

        ///<summary>Raises the AvatarSitChanged Event</summary>
        /// <param name="e">A AvatarSitChangedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarSitChanged(AvatarSitChangedEventArgs e)
        {
            EventHandler<AvatarSitChangedEventArgs> handler = m_AvatarSitChanged;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarSitChangedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// updated sit information for our <see cref="Avatar"/></summary>
        public event EventHandler<AvatarSitChangedEventArgs> AvatarSitChanged
        {
            add { lock (m_AvatarSitChangedLock) { m_AvatarSitChanged += value; } }
            remove { lock (m_AvatarSitChangedLock) { m_AvatarSitChanged -= value; } }
        }
        #endregion AvatarSitChanged event

        #region PayPriceReply event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<PayPriceReplyEventArgs> m_PayPriceReply;

        ///<summary>Raises the PayPriceReply Event</summary>
        /// <param name="e">A PayPriceReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnPayPriceReply(PayPriceReplyEventArgs e)
        {
            EventHandler<PayPriceReplyEventArgs> handler = m_PayPriceReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_PayPriceReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// purchase price information for a <see cref="Primitive"/></summary>
        public event EventHandler<PayPriceReplyEventArgs> PayPriceReply
        {
            add { lock (m_PayPriceReplyLock) { m_PayPriceReply += value; } }
            remove { lock (m_PayPriceReplyLock) { m_PayPriceReply -= value; } }
        }
        #endregion PayPriceReply

        #region PhysicsProperties event
        /// <summary>
        /// Callback for getting object media data via CAP
        /// </summary>
        /// <param name="success">Indicates if the operation was successful</param>
        /// <param name="version">Object media version string</param>
        /// <param name="faceMedia">Array indexed on prim face of media entry data</param>
        public delegate void ObjectMediaCallback(bool success, string version, MediaEntry[] faceMedia);

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<PhysicsPropertiesEventArgs> m_PhysicsProperties;

        ///<summary>Raises the PhysicsProperties Event</summary>
        /// <param name="e">A PhysicsPropertiesEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnPhysicsProperties(PhysicsPropertiesEventArgs e)
        {
            EventHandler<PhysicsPropertiesEventArgs> handler = m_PhysicsProperties;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_PhysicsPropertiesLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// additional <seea cref="Primitive"/> information</summary>
        /// <seealso cref="SelectObject"/>
        /// <seealso cref="SelectObjects"/>
        public event EventHandler<PhysicsPropertiesEventArgs> PhysicsProperties
        {
            add { lock (m_PhysicsPropertiesLock) { m_PhysicsProperties += value; } }
            remove { lock (m_PhysicsPropertiesLock) { m_PhysicsProperties -= value; } }
        }
        #endregion PhysicsProperties event

        #endregion Delegates

        /// <summary>Reference to the GridClient object</summary>
        protected GridClient Client;
        /// <summary>Does periodic dead reckoning calculation to convert
        /// velocity and acceleration to new positions for objects</summary>
        private Timer InterpolationTimer;

        /// <summary>
        /// Construct a new instance of the ObjectManager class
        /// </summary>
        /// <param name="client">A reference to the <see cref="GridClient"/> instance</param>
        public ObjectManager(GridClient client)
        {
            Client = client;

            Client.Network.RegisterCallback(PacketType.ObjectUpdate, ObjectUpdateHandler, false);
            Client.Network.RegisterCallback(PacketType.ImprovedTerseObjectUpdate, ImprovedTerseObjectUpdateHandler, false);
            Client.Network.RegisterCallback(PacketType.ObjectUpdateCompressed, ObjectUpdateCompressedHandler);
            Client.Network.RegisterCallback(PacketType.ObjectUpdateCached, ObjectUpdateCachedHandler);
            Client.Network.RegisterCallback(PacketType.KillObject, KillObjectHandler);
            Client.Network.RegisterCallback(PacketType.ObjectPropertiesFamily, ObjectPropertiesFamilyHandler);
            Client.Network.RegisterCallback(PacketType.ObjectProperties, ObjectPropertiesHandler);
            Client.Network.RegisterCallback(PacketType.PayPriceReply, PayPriceReplyHandler);
            Client.Network.RegisterCallback(PacketType.ObjectAnimation, ObjectAnimationHandler);
            Client.Network.RegisterEventCallback("ObjectPhysicsProperties", ObjectPhysicsPropertiesHandler);
        }

        #region Internal event handlers

        private void Network_OnDisconnected(NetworkManager.DisconnectType reason, string message)
        {
            if (InterpolationTimer != null)
            {
                InterpolationTimer.Dispose();
                InterpolationTimer = null;
            }
        }

        private void Network_OnConnected(object sender)
        {
            if (Client.Settings.USE_INTERPOLATION_TIMER)
            {
                InterpolationTimer = new Timer(InterpolationTimer_Elapsed, null, Client.Settings.INTERPOLATION_INTERVAL, Timeout.Infinite);
            }
        }

        #endregion Internal event handlers

        #region Public Methods

        /// <summary>
        /// Request information for a single object from a <see cref="Simulator"/> 
        /// you are currently connected to
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>
        /// <param name="localID">The Local ID of the object</param>
        public void RequestObject(Simulator simulator, uint localID)
        {
            RequestMultipleObjectsPacket request = new RequestMultipleObjectsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new RequestMultipleObjectsPacket.ObjectDataBlock[1]
            };
            request.ObjectData[0] = new RequestMultipleObjectsPacket.ObjectDataBlock
            {
                ID = localID,
                CacheMissType = 0
            };

            Client.Network.SendPacket(request, simulator);
        }

        /// <summary>
        /// Request information for multiple objects contained in
        /// the same simulator
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the objects are located</param>
        /// <param name="localIDs">An array containing the Local IDs of the objects</param>
        public void RequestObjects(Simulator simulator, List<uint> localIDs)
        {
            RequestMultipleObjectsPacket request = new RequestMultipleObjectsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new RequestMultipleObjectsPacket.ObjectDataBlock[localIDs.Count]
            };

            for (var i = 0; i < localIDs.Count; i++)
            {
                request.ObjectData[i] = new RequestMultipleObjectsPacket.ObjectDataBlock
                {
                    ID = localIDs[i],
                    CacheMissType = 0
                };
            }

            Client.Network.SendPacket(request, simulator);
        }

        /// <summary>
        /// Attempt to purchase an original object, a copy, or the contents of
        /// an object
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>
        /// <param name="saleType">Whether the original, a copy, or the object
        /// contents are on sale. This is used for verification, if the
        /// sale type is not valid for the object the purchase will fail</param>
        /// <param name="price">Price of the object. This is used for 
        /// verification, if it does not match the actual price the purchase
        /// will fail</param>
        /// <param name="groupID">Group ID that will be associated with the new
        /// purchase</param>
        /// <param name="categoryID">Inventory folder UUID where the object or objects 
        /// purchased should be placed</param>
        /// <example>
        /// <code>
        ///     BuyObject(Client.Network.CurrentSim, 500, SaleType.Copy, 
        ///         100, UUID.Zero, Client.Self.InventoryRootFolderUUID);
        /// </code> 
        ///</example>
        public void BuyObject(Simulator simulator, uint localID, SaleType saleType, int price, UUID groupID,
            UUID categoryID)
        {
            ObjectBuyPacket buy = new ObjectBuyPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    GroupID = groupID,
                    CategoryID = categoryID
                },
                ObjectData = new ObjectBuyPacket.ObjectDataBlock[1]
            };

            buy.ObjectData[0] = new ObjectBuyPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                SaleType = (byte)saleType,
                SalePrice = price
            };

            Client.Network.SendPacket(buy, simulator);
        }

        /// <summary>
        /// Request prices that should be displayed in pay dialog. This will triggger the simulator
        /// to send us back a PayPriceReply which can be handled by OnPayPriceReply event
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>
        /// <param name="objectID">The ID of the object</param>
        /// <remarks>The result is raised in the <see cref="PayPriceReply"/> event</remarks>
        public void RequestPayPrice(Simulator simulator, UUID objectID)
        {
            RequestPayPricePacket payPriceRequest = new RequestPayPricePacket
            {
                ObjectData = new RequestPayPricePacket.ObjectDataBlock
                {
                    ObjectID = objectID
                }
            };

            Client.Network.SendPacket(payPriceRequest, simulator);
        }

        /// <summary>
        /// Select a single object. This will cause the <see cref="Simulator"/> to send us 
        /// an <see cref="ObjectPropertiesPacket"/> which will raise the <see cref="ObjectProperties"/> event
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>        
        /// <seealso cref="ObjectPropertiesFamilyEventArgs"/>
        public void SelectObject(Simulator simulator, uint localID)
        {
            SelectObject(simulator, localID, true);
        }

        /// <summary>
        /// Select a single object. This will cause the <see cref="Simulator"/> to send us 
        /// an <see cref="ObjectPropertiesPacket"/> which will raise the <see cref="ObjectProperties"/> event
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>
        /// <param name="localID">The Local ID of the object</param>
        /// <param name="automaticDeselect">if true, a call to <see cref="DeselectObject"/> is
        /// made immediately following the request</param>
        /// <seealso cref="ObjectPropertiesFamilyEventArgs"/>
        public void SelectObject(Simulator simulator, uint localID, bool automaticDeselect)
        {
            ObjectSelectPacket select = new ObjectSelectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectSelectPacket.ObjectDataBlock[1]
            };

            select.ObjectData[0] = new ObjectSelectPacket.ObjectDataBlock
            {
                ObjectLocalID = localID
            };

            Client.Network.SendPacket(select, simulator);

            if (automaticDeselect)
            {
                DeselectObject(simulator, localID);
            }
        }

        /// <summary>
        /// Select multiple objects. This will cause the <see cref="Simulator"/> to send us 
        /// an <see cref="ObjectPropertiesPacket"/> which will raise the <see cref="ObjectProperties"/> event
        /// </summary>        
        /// <param name="simulator">The <see cref="Simulator"/> the objects are located</param> 
        /// <param name="localIDs">An array containing the Local IDs of the objects</param>
        /// <param name="automaticDeselect">Should objects be deselected immediately after selection</param>
        /// <seealso cref="ObjectPropertiesFamilyEventArgs"/>
        public void SelectObjects(Simulator simulator, uint[] localIDs, bool automaticDeselect)
        {
            ObjectSelectPacket select = new ObjectSelectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectSelectPacket.ObjectDataBlock[localIDs.Length]
            };

            for (var i = 0; i < localIDs.Length; i++)
            {
                select.ObjectData[i] = new ObjectSelectPacket.ObjectDataBlock
                {
                    ObjectLocalID = localIDs[i]
                };
            }

            Client.Network.SendPacket(select, simulator);

            if (automaticDeselect)
            {
                DeselectObjects(simulator, localIDs);
            }
        }

        /// <summary>
        /// Select multiple objects. This will cause the <see cref="Simulator"/> to send us 
        /// an <see cref="ObjectPropertiesPacket"/> which will raise the <see cref="ObjectProperties"/> event
        /// </summary>        
        /// <param name="simulator">The <see cref="Simulator"/> the objects are located</param> 
        /// <param name="localIDs">An array containing the Local IDs of the objects</param>
        /// <seealso cref="ObjectPropertiesFamilyEventArgs"/>
        public void SelectObjects(Simulator simulator, uint[] localIDs)
        {
            SelectObjects(simulator, localIDs, true);
        }

        /// <summary>
        /// Update the properties of an object
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>        
        /// <param name="physical">true to turn the objects physical property on</param>
        /// <param name="temporary">true to turn the objects temporary property on</param>
        /// <param name="phantom">true to turn the objects phantom property on</param>
        /// <param name="castsShadow">true to turn the objects cast shadows property on</param>
        public void SetFlags(Simulator simulator, uint localID, bool physical, bool temporary, bool phantom, bool castsShadow)
        {
            SetFlags(simulator, localID, physical, temporary, phantom, castsShadow, PhysicsShapeType.Prim, 1000f, 0.6f, 0.5f, 1f);
        }

        /// <summary>
        /// Update the properties of an object
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>        
        /// <param name="physical">true to turn the objects physical property on</param>
        /// <param name="temporary">true to turn the objects temporary property on</param>
        /// <param name="phantom">true to turn the objects phantom property on</param>
        /// <param name="castsShadow">true to turn the objects cast shadows property on</param>
        /// <param name="physicsType">Type of the representation prim will have in the physics engine</param>
        /// <param name="density">Density - normal value 1000</param>
        /// <param name="friction">Friction - normal value 0.6</param>
        /// <param name="restitution">Restitution - standard value 0.5</param>
        /// <param name="gravityMultiplier">Gravity multiplier - standard value 1.0</param>
        public void SetFlags(Simulator simulator, uint localID, bool physical, bool temporary, bool phantom, bool castsShadow,
            PhysicsShapeType physicsType, float density, float friction, float restitution, float gravityMultiplier)
        {
            ObjectFlagUpdatePacket flags = new ObjectFlagUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    ObjectLocalID = localID,
                    UsePhysics = physical,
                    IsTemporary = temporary,
                    IsPhantom = phantom,
                    CastsShadows = castsShadow
                },
                ExtraPhysics = new ObjectFlagUpdatePacket.ExtraPhysicsBlock[1]
            };

            flags.ExtraPhysics[0] = new ObjectFlagUpdatePacket.ExtraPhysicsBlock
            {
                PhysicsShapeType = (byte)physicsType,
                Density = density,
                Friction = friction,
                Restitution = restitution,
                GravityMultiplier = gravityMultiplier
            };

            Client.Network.SendPacket(flags, simulator);
        }

        /// <summary>
        /// Sets the sale properties of a single object
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>        
        /// <param name="saleType">One of the options from the <see cref="SaleType"/> enum</param>
        /// <param name="price">The price of the object</param>
        public void SetSaleInfo(Simulator simulator, uint localID, SaleType saleType, int price)
        {
            ObjectSaleInfoPacket sale = new ObjectSaleInfoPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectSaleInfoPacket.ObjectDataBlock[1]
            };
            sale.ObjectData[0] = new ObjectSaleInfoPacket.ObjectDataBlock
            {
                LocalID = localID,
                SalePrice = price,
                SaleType = (byte)saleType
            };

            Client.Network.SendPacket(sale, simulator);
        }

        /// <summary>
        /// Sets the sale properties of multiple objects
        /// </summary>        
        /// <param name="simulator">The <see cref="Simulator"/> the objects are located</param> 
        /// <param name="localIDs">An array containing the Local IDs of the objects</param>
        /// <param name="saleType">One of the options from the <see cref="SaleType"/> enum</param>
        /// <param name="price">The price of the object</param>
        public void SetSaleInfo(Simulator simulator, List<uint> localIDs, SaleType saleType, int price)
        {
            ObjectSaleInfoPacket sale = new ObjectSaleInfoPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectSaleInfoPacket.ObjectDataBlock[localIDs.Count]
            };

            for (var i = 0; i < localIDs.Count; i++)
            {
                sale.ObjectData[i] = new ObjectSaleInfoPacket.ObjectDataBlock
                {
                    LocalID = localIDs[i],
                    SalePrice = price,
                    SaleType = (byte)saleType
                };
            }

            Client.Network.SendPacket(sale, simulator);
        }

        /// <summary>
        /// Deselect a single object
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>
        public void DeselectObject(Simulator simulator, uint localID)
        {
            ObjectDeselectPacket deselect = new ObjectDeselectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectDeselectPacket.ObjectDataBlock[1]
            };

            deselect.ObjectData[0] = new ObjectDeselectPacket.ObjectDataBlock
            {
                ObjectLocalID = localID
            };

            Client.Network.SendPacket(deselect, simulator);
        }

        /// <summary>
        /// Deselect multiple objects.
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the objects are located</param> 
        /// <param name="localIDs">An array containing the Local IDs of the objects</param>
        public void DeselectObjects(Simulator simulator, uint[] localIDs)
        {
            ObjectDeselectPacket deselect = new ObjectDeselectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectDeselectPacket.ObjectDataBlock[localIDs.Length]
            };

            for (var i = 0; i < localIDs.Length; i++)
            {
                deselect.ObjectData[i] = new ObjectDeselectPacket.ObjectDataBlock
                {
                    ObjectLocalID = localIDs[i]
                };
            }

            Client.Network.SendPacket(deselect, simulator);
        }

        /// <summary>
        /// Perform a click action on an object
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>
        public void ClickObject(Simulator simulator, uint localID)
        {
            ClickObject(simulator, localID, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, Vector3.Zero, Vector3.Zero);
        }

        /// <summary>
        /// Perform a click action (Grab) on a single object
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>        
        /// <param name="localID">The Local ID of the object</param>
        /// <param name="uvCoord">The texture coordinates to touch</param>
        /// <param name="stCoord">The surface coordinates to touch</param>
        /// <param name="faceIndex">The face of the position to touch</param>
        /// <param name="position">The region coordinates of the position to touch</param>
        /// <param name="normal">The surface normal of the position to touch (A normal is a vector perpendicular to the surface)</param>
        /// <param name="binormal">The surface binormal of the position to touch (A binormal is a vector tangent to the surface
        /// pointing along the U direction of the tangent space</param>
        public void ClickObject(Simulator simulator, uint localID, Vector3 uvCoord, Vector3 stCoord, int faceIndex, Vector3 position,
            Vector3 normal, Vector3 binormal)
        {
            ObjectGrabPacket grab = new ObjectGrabPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData =
                {
                    GrabOffset = Vector3.Zero,
                    LocalID = localID
                },
                SurfaceInfo = new ObjectGrabPacket.SurfaceInfoBlock[1]
            };
            grab.SurfaceInfo[0] = new ObjectGrabPacket.SurfaceInfoBlock
            {
                UVCoord = uvCoord,
                STCoord = stCoord,
                FaceIndex = faceIndex,
                Position = position,
                Normal = normal,
                Binormal = binormal
            };

            Client.Network.SendPacket(grab, simulator);

            // TODO: If these hit the server out of order the click will fail 
            // and we'll be grabbing the object
            Thread.Sleep(50);

            ObjectDeGrabPacket degrab = new ObjectDeGrabPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData =
                {
                    LocalID = localID
                },
                SurfaceInfo = new ObjectDeGrabPacket.SurfaceInfoBlock[1]
            };
            degrab.SurfaceInfo[0] = new ObjectDeGrabPacket.SurfaceInfoBlock
            {
                UVCoord = uvCoord,
                STCoord = stCoord,
                FaceIndex = faceIndex,
                Position = position,
                Normal = normal,
                Binormal = binormal
            };

            Client.Network.SendPacket(degrab, simulator);
        }

        /// <summary>
        /// Create (rez) a new prim object in a simulator
        /// </summary>
        /// <param name="simulator">A reference to the <seealso cref="OpenMetaverse.Simulator"/> object to place the object in</param>
        /// <param name="prim">Data describing the prim object to rez</param>
        /// <param name="groupID">Group ID that this prim will be set to, or UUID.Zero if you
        /// do not want the object to be associated with a specific group</param>
        /// <param name="position">An approximation of the position at which to rez the prim</param>
        /// <param name="scale">Scale vector to size this prim</param>
        /// <param name="rotation">Rotation quaternion to rotate this prim</param>
        /// <remarks>Due to the way client prim rezzing is done on the server,
        /// the requested position for an object is only close to where the prim
        /// actually ends up. If you desire exact placement you'll need to 
        /// follow up by moving the object after it has been created. This
        /// function will not set textures, light and flexible data, or other 
        /// extended primitive properties</remarks>
        public void AddPrim(Simulator simulator, Primitive.ConstructionData prim, UUID groupID, Vector3 position,
            Vector3 scale, Quaternion rotation)
        {
            AddPrim(simulator, prim, groupID, position, scale, rotation, PrimFlags.CreateSelected);
        }

        /// <summary>
        /// Create (rez) a new prim object in a simulator
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="Simulator"/> object to place the object in</param>
        /// <param name="prim">Data describing the prim object to rez</param>
        /// <param name="groupID">Group ID that this prim will be set to, or UUID.Zero if you
        /// do not want the object to be associated with a specific group</param>
        /// <param name="position">An approximation of the position at which to rez the prim</param>
        /// <param name="scale">Scale vector to size this prim</param>
        /// <param name="rotation">Rotation quaternion to rotate this prim</param>
        /// <param name="createFlags">Specify the <see cref="PrimFlags"/></param>
        /// <remarks>Due to the way client prim rezzing is done on the server,
        /// the requested position for an object is only close to where the prim
        /// actually ends up. If you desire exact placement you'll need to 
        /// follow up by moving the object after it has been created. This
        /// function will not set textures, light and flexible data, or other 
        /// extended primitive properties</remarks>
        public void AddPrim(Simulator simulator, Primitive.ConstructionData prim, UUID groupID, Vector3 position,
            Vector3 scale, Quaternion rotation, PrimFlags createFlags)
        {
            ObjectAddPacket packet = new ObjectAddPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    GroupID = groupID
                },
                ObjectData =
                {
                    State = prim.State,
                    AddFlags = (uint)createFlags,
                    PCode = (byte)PCode.Prim,
                    Material = (byte)prim.Material,
                    Scale = scale,
                    Rotation = rotation,
                    PathCurve = (byte)prim.PathCurve,
                    PathBegin = Primitive.PackBeginCut(prim.PathBegin),
                    PathEnd = Primitive.PackEndCut(prim.PathEnd),
                    PathRadiusOffset = Primitive.PackPathTwist(prim.PathRadiusOffset),
                    PathRevolutions = Primitive.PackPathRevolutions(prim.PathRevolutions),
                    PathScaleX = Primitive.PackPathScale(prim.PathScaleX),
                    PathScaleY = Primitive.PackPathScale(prim.PathScaleY),
                    PathShearX = (byte)Primitive.PackPathShear(prim.PathShearX),
                    PathShearY = (byte)Primitive.PackPathShear(prim.PathShearY),
                    PathSkew = Primitive.PackPathTwist(prim.PathSkew),
                    PathTaperX = Primitive.PackPathTaper(prim.PathTaperX),
                    PathTaperY = Primitive.PackPathTaper(prim.PathTaperY),
                    PathTwist = Primitive.PackPathTwist(prim.PathTwist),
                    PathTwistBegin = Primitive.PackPathTwist(prim.PathTwistBegin),
                    ProfileCurve = prim.profileCurve,
                    ProfileBegin = Primitive.PackBeginCut(prim.ProfileBegin),
                    ProfileEnd = Primitive.PackEndCut(prim.ProfileEnd),
                    ProfileHollow = Primitive.PackProfileHollow(prim.ProfileHollow),
                    RayStart = position,
                    RayEnd = position,
                    RayEndIsIntersection = 0,
                    RayTargetID = UUID.Zero,
                    BypassRaycast = 1
                }
            };

            Client.Network.SendPacket(packet, simulator);
        }

        /// <summary>
        /// Rez a Linden tree
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="scale">The size of the tree</param>
        /// <param name="rotation">The rotation of the tree</param>
        /// <param name="position">The position of the tree</param>
        /// <param name="treeType">The Type of tree</param>
        /// <param name="groupOwner">The <see cref="UUID"/> of the group to set the tree to, 
        /// or UUID.Zero if no group is to be set</param>
        /// <param name="newTree">true to use the "new" Linden trees, false to use the old</param>
        public void AddTree(Simulator simulator, Vector3 scale, Quaternion rotation, Vector3 position,
            Tree treeType, UUID groupOwner, bool newTree)
        {
            ObjectAddPacket add = new ObjectAddPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    GroupID = groupOwner
                },
                ObjectData =
                {
                    BypassRaycast = 1,
                    Material = 3,
                    PathCurve = 16,
                    PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree,
                    RayEnd = position,
                    RayStart = position,
                    RayTargetID = UUID.Zero,
                    Rotation = rotation,
                    Scale = scale,
                    State = (byte)treeType
                }
            };

            Client.Network.SendPacket(add, simulator);
        }

        /// <summary>
        /// Rez grass and ground cover
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="scale">The size of the grass</param>
        /// <param name="rotation">The rotation of the grass</param>
        /// <param name="position">The position of the grass</param>
        /// <param name="grassType">The type of grass from the <see cref="Grass"/> enum</param>
        /// <param name="groupOwner">The <see cref="UUID"/> of the group to set the tree to, 
        /// or UUID.Zero if no group is to be set</param>
        public void AddGrass(Simulator simulator, Vector3 scale, Quaternion rotation, Vector3 position,
            Grass grassType, UUID groupOwner)
        {
            ObjectAddPacket add = new ObjectAddPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    GroupID = groupOwner
                },
                ObjectData =
                {
                    BypassRaycast = 1,
                    Material = 3,
                    PathCurve = 16,
                    PCode = (byte)PCode.Grass,
                    RayEnd = position,
                    RayStart = position,
                    RayTargetID = UUID.Zero,
                    Rotation = rotation,
                    Scale = scale,
                    State = (byte)grassType
                }
            };

            Client.Network.SendPacket(add, simulator);
        }

        /// <summary>
        /// Set the textures to apply to the faces of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="textures">The texture data to apply</param>
        public void SetTextures(Simulator simulator, uint localID, Primitive.TextureEntry textures)
        {
            SetTextures(simulator, localID, textures, string.Empty);
        }

        /// <summary>
        /// Set the textures to apply to the faces of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="textures">The texture data to apply</param>
        /// <param name="mediaUrl">A media URL (not used)</param>
        public void SetTextures(Simulator simulator, uint localID, Primitive.TextureEntry textures, string mediaUrl)
        {
            ObjectImagePacket image = new ObjectImagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectImagePacket.ObjectDataBlock[1]
            };

            image.ObjectData[0] = new ObjectImagePacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                TextureEntry = textures.GetBytes(),
                MediaURL = Utils.StringToBytes(mediaUrl)
            };

            Client.Network.SendPacket(image, simulator);
        }

        /// <summary>
        /// Set the Light data on an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="light"><see cref="Primitive.LightData"/> object containing the data to set</param>
        public void SetLight(Simulator simulator, uint localID, Primitive.LightData light)
        {
            ObjectExtraParamsPacket extra = new ObjectExtraParamsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectExtraParamsPacket.ObjectDataBlock[1]
            };

            extra.ObjectData[0] = new ObjectExtraParamsPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                ParamType = (byte)ExtraParamType.Light,
                // Disables the light if intensity is 0
                ParamInUse = light.Intensity != 0.0f,
                ParamData = light.GetBytes()
            };
            extra.ObjectData[0].ParamSize = (uint)extra.ObjectData[0].ParamData.Length;

            Client.Network.SendPacket(extra, simulator);
        }

        /// <summary>
        /// Set the flexible data on an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="flexible">A <see cref="Primitive.FlexibleData"/> object containing the data to set</param>
        public void SetFlexible(Simulator simulator, uint localID, Primitive.FlexibleData flexible)
        {
            ObjectExtraParamsPacket extra = new ObjectExtraParamsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectExtraParamsPacket.ObjectDataBlock[1]
            };

            extra.ObjectData[0] = new ObjectExtraParamsPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                ParamType = (byte)ExtraParamType.Flexible,
                ParamInUse = true,
                ParamData = flexible.GetBytes()
            };
            extra.ObjectData[0].ParamSize = (uint)extra.ObjectData[0].ParamData.Length;

            Client.Network.SendPacket(extra, simulator);
        }

        /// <summary>
        /// Set the sculptie texture and data on an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="sculpt">A <see cref="Primitive.SculptData"/> object containing the data to set</param>
        public void SetSculpt(Simulator simulator, uint localID, Primitive.SculptData sculpt)
        {
            ObjectExtraParamsPacket extra = new ObjectExtraParamsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectExtraParamsPacket.ObjectDataBlock[1]
            };

            extra.ObjectData[0] = new ObjectExtraParamsPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                ParamType = (byte)ExtraParamType.Sculpt,
                ParamInUse = true,
                ParamData = sculpt.GetBytes()
            };
            extra.ObjectData[0].ParamSize = (uint)extra.ObjectData[0].ParamData.Length;

            Client.Network.SendPacket(extra, simulator);

            // Not sure why, but if you don't send this the sculpted prim disappears
            ObjectShapePacket shape = new ObjectShapePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new OpenMetaverse.Packets.ObjectShapePacket.ObjectDataBlock[1]
            };

            shape.ObjectData[0] = new OpenMetaverse.Packets.ObjectShapePacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                PathScaleX = 100,
                PathScaleY = 150,
                PathCurve = 32
            };

            Client.Network.SendPacket(shape, simulator);
        }

        /// <summary>
        /// Unset additional primitive parameters on an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="type">The extra parameters to set</param>
        public void SetExtraParamOff(Simulator simulator, uint localID, ExtraParamType type)
        {
            ObjectExtraParamsPacket extra = new ObjectExtraParamsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectExtraParamsPacket.ObjectDataBlock[1]
            };

            extra.ObjectData[0] = new ObjectExtraParamsPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                ParamType = (byte)type,
                ParamInUse = false,
                ParamData = Utils.EmptyBytes,
                ParamSize = 0
            };

            Client.Network.SendPacket(extra, simulator);
        }

        /// <summary>
        /// Link multiple prims into a linkset
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the objects reside</param>
        /// <param name="localIDs">An array which contains the IDs of the objects to link</param>
        /// <remarks>The last object in the array will be the root object of the linkset</remarks>
        public void LinkPrims(Simulator simulator, List<uint> localIDs)
        {
            ObjectLinkPacket packet = new ObjectLinkPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectLinkPacket.ObjectDataBlock[localIDs.Count]
            };

            for (int i = 0; i < localIDs.Count; i++)
            {
                packet.ObjectData[i] = new ObjectLinkPacket.ObjectDataBlock
                {
                    ObjectLocalID = localIDs[i]
                };
            }

            Client.Network.SendPacket(packet, simulator);
        }

        /// <summary>
        /// Delink/Unlink multiple prims from a linkset
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the objects reside</param>
        /// <param name="localIDs">An array which contains the IDs of the objects to delink</param>
        public void DelinkPrims(Simulator simulator, List<uint> localIDs)
        {
            ObjectDelinkPacket packet = new ObjectDelinkPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectDelinkPacket.ObjectDataBlock[localIDs.Count]
            };

            int i = 0;
            foreach (uint localID in localIDs)
            {
                packet.ObjectData[i] = new ObjectDelinkPacket.ObjectDataBlock
                {
                    ObjectLocalID = localID
                };

                i++;
            }

            Client.Network.SendPacket(packet, simulator);
        }

        /// <summary>
        /// Change the rotation of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="rotation">The new rotation of the object</param>
        public void SetRotation(Simulator simulator, uint localID, Quaternion rotation)
        {
            ObjectRotationPacket objRotPacket = new ObjectRotationPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectRotationPacket.ObjectDataBlock[1]
            };

            objRotPacket.ObjectData[0] = new ObjectRotationPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                Rotation = rotation
            };
            Client.Network.SendPacket(objRotPacket, simulator);
        }

        /// <summary>
        /// Set the name of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="name">A string containing the new name of the object</param>
        public void SetName(Simulator simulator, uint localID, string name)
        {
            SetNames(simulator, new uint[] { localID }, new string[] { name });
        }

        /// <summary>
        /// Set the name of multiple objects
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the objects reside</param>
        /// <param name="localIDs">An array which contains the IDs of the objects to change the name of</param>
        /// <param name="names">An array which contains the new names of the objects</param>
        public void SetNames(Simulator simulator, uint[] localIDs, string[] names)
        {
            ObjectNamePacket namePacket = new ObjectNamePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectNamePacket.ObjectDataBlock[localIDs.Length]
            };

            for (int i = 0; i < localIDs.Length; ++i)
            {
                namePacket.ObjectData[i] = new ObjectNamePacket.ObjectDataBlock
                {
                    LocalID = localIDs[i],
                    Name = Utils.StringToBytes(names[i])
                };
            }

            Client.Network.SendPacket(namePacket, simulator);
        }

        /// <summary>
        /// Set the description of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="description">A string containing the new description of the object</param>
        public void SetDescription(Simulator simulator, uint localID, string description)
        {
            SetDescriptions(simulator, new uint[] { localID }, new string[] { description });
        }

        /// <summary>
        /// Set the descriptions of multiple objects
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the objects reside</param>
        /// <param name="localIDs">An array which contains the IDs of the objects to change the description of</param>
        /// <param name="descriptions">An array which contains the new descriptions of the objects</param>
        public void SetDescriptions(Simulator simulator, uint[] localIDs, string[] descriptions)
        {
            ObjectDescriptionPacket descPacket = new ObjectDescriptionPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectDescriptionPacket.ObjectDataBlock[localIDs.Length]
            };

            for (int i = 0; i < localIDs.Length; ++i)
            {
                descPacket.ObjectData[i] = new ObjectDescriptionPacket.ObjectDataBlock
                {
                    LocalID = localIDs[i],
                    Description = Utils.StringToBytes(descriptions[i])
                };
            }

            Client.Network.SendPacket(descPacket, simulator);
        }

        /// <summary>
        /// Attach an object to this avatar
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="attachPoint">The point on the avatar the object will be attached</param>
        /// <param name="rotation">The rotation of the attached object</param>
        public void AttachObject(Simulator simulator, uint localID, AttachmentPoint attachPoint, Quaternion rotation)
        {
            ObjectAttachPacket attach = new ObjectAttachPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    AttachmentPoint = (byte)attachPoint
                },
                ObjectData = new ObjectAttachPacket.ObjectDataBlock[1]
            };

            attach.ObjectData[0] = new ObjectAttachPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                Rotation = rotation
            };

            Client.Network.SendPacket(attach, simulator);
        }

        /// <summary>
        /// Drop an attached object from this avatar
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/>
        /// object where the objects reside. This will always be the simulator the avatar is currently in
        /// </param>
        /// <param name="localID">The object's ID which is local to the simulator the object is in</param>
        public void DropObject(Simulator simulator, uint localID)
        {
            ObjectDropPacket dropit = new ObjectDropPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectDropPacket.ObjectDataBlock[1]
            };
            dropit.ObjectData[0] = new ObjectDropPacket.ObjectDataBlock
            {
                ObjectLocalID = localID
            };

            Client.Network.SendPacket(dropit, simulator);
        }

        /// <summary>
        /// Detach an object from yourself
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> 
        /// object where the objects reside
        /// 
        /// This will always be the simulator the avatar is currently in
        /// </param>
        /// <param name="localIDs">An array which contains the IDs of the objects to detach</param>
        public void DetachObjects(Simulator simulator, List<uint> localIDs)
        {
            ObjectDetachPacket detach = new ObjectDetachPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectDetachPacket.ObjectDataBlock[localIDs.Count]
            };

            for (int i = 0; i < localIDs.Count; i++)
            {
                detach.ObjectData[i] = new ObjectDetachPacket.ObjectDataBlock
                {
                    ObjectLocalID = localIDs[i]
                };
            }

            Client.Network.SendPacket(detach, simulator);
        }

        /// <summary>
        /// Change the position of an object, Will change position of entire linkset
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="position">The new position of the object</param>
        public void SetPosition(Simulator simulator, uint localID, Vector3 position)
        {
            UpdateObject(simulator, localID, position, UpdateType.Position | UpdateType.Linked);
        }

        /// <summary>
        /// Change the position of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="position">The new position of the object</param>
        /// <param name="childOnly">if true, will change position of (this) child prim only, not entire linkset</param>
        public void SetPosition(Simulator simulator, uint localID, Vector3 position, bool childOnly)
        {
            UpdateType type = UpdateType.Position;

            if (!childOnly)
                type |= UpdateType.Linked;

            UpdateObject(simulator, localID, position, type);
        }

        /// <summary>
        /// Change the Scale (size) of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="scale">The new scale of the object</param>
        /// <param name="childOnly">If true, will change scale of this prim only, not entire linkset</param>
        /// <param name="uniform">True to resize prims uniformly</param>
        public void SetScale(Simulator simulator, uint localID, Vector3 scale, bool childOnly, bool uniform)
        {
            UpdateType type = UpdateType.Scale;

            if (!childOnly)
                type |= UpdateType.Linked;

            if (uniform)
                type |= UpdateType.Uniform;

            UpdateObject(simulator, localID, scale, type);
        }

        /// <summary>
        /// Change the Rotation of an object that is either a child or a whole linkset
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="quat">The new scale of the object</param>
        /// <param name="childOnly">If true, will change rotation of this prim only, not entire linkset</param>
        public void SetRotation(Simulator simulator, uint localID, Quaternion quat, bool childOnly)
        {
            UpdateType type = UpdateType.Rotation;

            if (!childOnly)
                type |= UpdateType.Linked;

            MultipleObjectUpdatePacket multiObjectUpdate = new MultipleObjectUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new MultipleObjectUpdatePacket.ObjectDataBlock[1]
            };

            multiObjectUpdate.ObjectData[0] = new MultipleObjectUpdatePacket.ObjectDataBlock
            {
                Type = (byte)type,
                ObjectLocalID = localID,
                Data = quat.GetBytes()
            };

            Client.Network.SendPacket(multiObjectUpdate, simulator);
        }

        /// <summary>
        /// Send a Multiple Object Update packet to change the size, scale or rotation of a primitive
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="data">The new rotation, size, or position of the target object</param>
        /// <param name="type">The flags from the <see cref="UpdateType"/> Enum</param>
        public void UpdateObject(Simulator simulator, uint localID, Vector3 data, UpdateType type)
        {
            MultipleObjectUpdatePacket multiObjectUpdate = new MultipleObjectUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new MultipleObjectUpdatePacket.ObjectDataBlock[1]
            };

            multiObjectUpdate.ObjectData[0] = new MultipleObjectUpdatePacket.ObjectDataBlock
            {
                Type = (byte)type,
                ObjectLocalID = localID,
                Data = data.GetBytes()
            };

            Client.Network.SendPacket(multiObjectUpdate, simulator);
        }

        /// <summary>
        /// Deed an object (prim) to a group, Object must be shared with group which
        /// can be accomplished with SetPermissions()
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="groupOwner">The <see cref="UUID"/> of the group to deed the object to</param>
        public void DeedObject(Simulator simulator, uint localID, UUID groupOwner)
        {
            ObjectOwnerPacket objDeedPacket = new ObjectOwnerPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                HeaderData =
                {
                    // Can only be use in God mode
                    Override = false,
                    OwnerID = UUID.Zero,
                    GroupID = groupOwner
                },
                ObjectData = new ObjectOwnerPacket.ObjectDataBlock[1]
            };

            objDeedPacket.ObjectData[0] = new ObjectOwnerPacket.ObjectDataBlock
            {
                ObjectLocalID = localID
            };

            Client.Network.SendPacket(objDeedPacket, simulator);
        }

        /// <summary>
        /// Deed multiple objects (prims) to a group, Objects must be shared with group which
        /// can be accomplished with SetPermissions()
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localIDs">An array which contains the IDs of the objects to deed</param>
        /// <param name="groupOwner">The <see cref="UUID"/> of the group to deed the object to</param>
        public void DeedObjects(Simulator simulator, List<uint> localIDs, UUID groupOwner)
        {
            ObjectOwnerPacket packet = new ObjectOwnerPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                HeaderData =
                {
                    // Can only be use in God mode
                    Override = false,
                    OwnerID = UUID.Zero,
                    GroupID = groupOwner
                },
                ObjectData = new ObjectOwnerPacket.ObjectDataBlock[localIDs.Count]
            };

            for (int i = 0; i < localIDs.Count; i++)
            {
                packet.ObjectData[i] = new ObjectOwnerPacket.ObjectDataBlock
                {
                    ObjectLocalID = localIDs[i]
                };
            }
            Client.Network.SendPacket(packet, simulator);
        }

        /// <summary>
        /// Set the permissions on multiple objects
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the objects reside</param>
        /// <param name="localIDs">An array which contains the IDs of the objects to set the permissions on</param>
        /// <param name="who">The new Who mask to set</param>
        /// <param name="permissions">Which permission to modify</param>
        /// <param name="set">The new state of permission</param>
        public void SetPermissions(Simulator simulator, List<uint> localIDs, PermissionWho who,
            PermissionMask permissions, bool set)
        {
            ObjectPermissionsPacket packet = new ObjectPermissionsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                HeaderData =
                {
                    // Override can only be used by gods
                    Override = false
                },
                ObjectData = new ObjectPermissionsPacket.ObjectDataBlock[localIDs.Count]
            };

            for (int i = 0; i < localIDs.Count; i++)
            {
                packet.ObjectData[i] = new ObjectPermissionsPacket.ObjectDataBlock
                {
                    ObjectLocalID = localIDs[i],
                    Field = (byte)who,
                    Mask = (uint)permissions,
                    Set = Convert.ToByte(set)
                };
            }

            Client.Network.SendPacket(packet, simulator);
        }

        /// <summary>
        /// Request additional properties for an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="objectID"></param>
        public void RequestObjectPropertiesFamily(Simulator simulator, UUID objectID)
        {
            RequestObjectPropertiesFamily(simulator, objectID, true);
        }

        /// <summary>
        /// Request additional properties for an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="objectID">Absolute UUID of the object</param>
        /// <param name="reliable">Whether to require server acknowledgement of this request</param>
        public void RequestObjectPropertiesFamily(Simulator simulator, UUID objectID, bool reliable)
        {
            RequestObjectPropertiesFamilyPacket properties = new RequestObjectPropertiesFamilyPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData =
                {
                    ObjectID = objectID,
                    // TODO: RequestFlags is typically only for bug report submissions, but we might be able to
                    // use it to pass an arbitrary uint back to the callback
                    RequestFlags = 0
                }
            };

            properties.Header.Reliable = reliable;

            Client.Network.SendPacket(properties, simulator);
        }

        /// <summary>
        /// Set the ownership of a list of objects to the specified group
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the objects reside</param>
        /// <param name="localIds">An array which contains the IDs of the objects to set the group id on</param>
        /// <param name="groupID">The Groups ID</param>
        public void SetObjectsGroup(Simulator simulator, List<uint> localIds, UUID groupID)
        {
            ObjectGroupPacket packet = new ObjectGroupPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    GroupID = groupID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectGroupPacket.ObjectDataBlock[localIds.Count]
            };

            for (int i = 0; i < localIds.Count; i++)
            {
                packet.ObjectData[i] = new ObjectGroupPacket.ObjectDataBlock
                {
                    ObjectLocalID = localIds[i]
                };
            }

            Client.Network.SendPacket(packet, simulator);
        }

        /// <summary>
        /// Update current URL of the previously set prim media
        /// </summary>
        /// <param name="primID">UUID of the prim</param>
        /// <param name="newURL">Set current URL to this</param>
        /// <param name="face">Prim face number</param>
        /// <param name="sim">Simulator in which prim is located</param>
        public void NavigateObjectMedia(UUID primID, int face, string newURL, Simulator sim)
        {
            Uri cap;
            if ((cap = Client.Network.CurrentSim.Caps?.CapabilityURI("ObjectMediaNavigate")) == null)
            {
                Logger.Log("ObjectMediaNavigate capability not available", Helpers.LogLevel.Error, Client);
                return;
            }

            ObjectMediaNavigateMessage payload = new ObjectMediaNavigateMessage
            {
                PrimID = primID, URL = newURL, Face = face
            };

            Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(), 
                CancellationToken.None, (response, data, error) =>
            {
                if (error != null)
                {
                    Logger.Log($"ObjectMediaNavigate: {error.Message}", Helpers.LogLevel.Error, Client, error);
                }
            });
        }

        /// <summary>
        /// Set object media
        /// </summary>
        /// <param name="primID">UUID of the prim</param>
        /// <param name="faceMedia">Array the length of prims number of faces. Null on face indexes where there is
        /// no media, <see cref="MediaEntry"/> on faces which contain the media</param>
        /// <param name="sim">Simulator in which prim is located</param>
        public void UpdateObjectMedia(UUID primID, MediaEntry[] faceMedia, Simulator sim)
        {
            Uri cap;
            if (sim.Caps == null || (cap = Client.Network.CurrentSim.Caps.CapabilityURI("ObjectMedia")) == null)
            {
                Logger.Log("ObjectMedia capability not available", Helpers.LogLevel.Error, Client);
                return;
            }

            ObjectMediaUpdate payload = new ObjectMediaUpdate {PrimID = primID, FaceMedia = faceMedia, Verb = "UPDATE"};

            Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(), 
                CancellationToken.None, (response, data, error) =>
            {
                if (error != null)
                {
                    Logger.Log($"ObjectMediaUpdate: {error.Message}", Helpers.LogLevel.Error, Client, error);
                }
            });

        }

        /// <summary>
        /// Retrieve information about object media
        /// </summary>
        /// <param name="primID">UUID of the primitive</param>
        /// <param name="sim">Simulator where prim is located</param>
        /// <param name="callback">Call this callback when done</param>
        public void RequestObjectMedia(UUID primID, Simulator sim, ObjectMediaCallback callback)
        {
            Uri cap;
            if (sim.Caps != null && (cap = Client.Network.CurrentSim.Caps.CapabilityURI("ObjectMedia")) != null)
            {
                ObjectMediaRequest payload = new ObjectMediaRequest {PrimID = primID, Verb = "GET"};

                Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(),
                    CancellationToken.None, (httpResponse, data, error) =>
                {
                    if (error != null)
                    {
                        Logger.Log("Failed retrieving ObjectMedia data", Helpers.LogLevel.Error, Client, error);
                        try { callback(false, string.Empty, null); }
                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client); }
                        return;
                    }

                    ObjectMediaMessage msg = new ObjectMediaMessage();
                    OSD result = OSDParser.Deserialize(data);
                    msg.Deserialize((OSDMap)result);

                    if (msg.Request is ObjectMediaResponse response)
                    {
                        if (Client.Settings.OBJECT_TRACKING)
                        {
                            Primitive prim = sim.ObjectsPrimitives.Find(p => p.ID == primID);
                            if (prim != null)
                            {
                                prim.MediaVersion = response.Version;
                                prim.FaceMedia = response.FaceMedia;
                            }
                        }

                        try { callback(true, response.Version, response.FaceMedia); }
                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client); }
                    }
                    else
                    {
                        try { callback(false, string.Empty, null); }
                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client); }
                    }
                });
            }
            else
            {
                Logger.Log("ObjectMedia capability not available", Helpers.LogLevel.Error, Client);
                try { callback(false, string.Empty, null); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client); }
            }
        }

        public async Task<IEnumerable<LegacyMaterial>> RequestMaterials(Simulator sim)
        {
            if (sim == null) { return null; }

            if (sim.Caps == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            if (sim.Caps == null)
            {
                Logger.Log("Caps are down, unable to retrieve materials.", Helpers.LogLevel.Info, Client);
                return null;
            }

            var uri = sim.Caps.CapabilityURI("RenderMaterials");

            List<LegacyMaterial> matsToReturn = new List<LegacyMaterial>();

            Logger.Log($"Awaiting materials from {uri}", Helpers.LogLevel.Info, Client);

            await Client.HttpCapsClient.GetRequestAsync(uri, CancellationToken.None,
                   ((response, data, error) =>
                   {
                       if (error != null)
                       {
                           Logger.Log("Failed fetching materials", Helpers.LogLevel.Error, Client, error);
                           return;
                       }

                       if (data == null || data.Length == 0)
                       {
                           Logger.Log("Failed fetching materials; result was empty.", Helpers.LogLevel.Error, Client);

                           return;
                       }

                       try
                       {
                           OSD result = OSDParser.Deserialize(data);
                           RenderMaterialsMessage info = new RenderMaterialsMessage();
                           info.Deserialize(result as OSDMap);

                           if (info.MaterialData is OSDArray mats)
                           {
                               foreach (var entry in mats)
                               {
                                   if (entry is OSDMap map)
                                   {
                                       matsToReturn.Add(new LegacyMaterial(map));
                                   }
                                   else
                                   {
                                       Logger.Log("Unexpected OSD return;\n" + OSDParser.SerializeJson(entry, true).ToJson(), 
                                           Helpers.LogLevel.Info, Client);
                                   }
                               }
                           }
                           else
                           {
                               Logger.Log("Unexpected OSD return;\n" + OSDParser.SerializeJson(result, true).ToJson(), 
                                   Helpers.LogLevel.Info, Client);
                           }

                           Logger.Log($"Fetched (x{matsToReturn.Count}) from {uri}", Helpers.LogLevel.Info, Client);
                       }
                       catch (Exception ex)
                       {
                           Logger.Log("Failed fetching RenderMaterials", Helpers.LogLevel.Error, Client, ex);

                           if (data.Length > 0)
                           {
                               Logger
                                   .Log("Response unparsable; " + System.Text.Encoding.UTF8.GetString(data),
                                        Helpers.LogLevel.Info, Client);
                           }
                       }
                   }));

            return matsToReturn;
        }

        public async Task<IEnumerable<LegacyMaterial>> RequestMaterials(Simulator sim, IEnumerable<UUID> materials)
        {
            if (sim == null) { return null; }

            if (sim.Caps == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            if (sim.Caps == null)
            {
                Logger.Log("Caps are down, unable to retrieve materials.", Helpers.LogLevel.Info, Client);
                return null;
            }

            var array = new OSDArray();

            foreach (var material in materials)
            {
                array.Add(material);
            }

            OSDMap request = new OSDMap(new Dictionary<string, OSD>
            {
                {"Zipped", Helpers.ZCompressOSD(array)}
            });

            var uri = sim.Caps.CapabilityURI("RenderMaterials");

            List<LegacyMaterial> matsToReturn = new List<LegacyMaterial>();

            Logger.Log($"Awaiting materials (x{array.Count}) from {uri}", Helpers.LogLevel.Info, Client);

            await Client.HttpCapsClient.PostRequestAsync(uri, OSDFormat.Xml, request, CancellationToken.None,
                       (response, data, error) =>
                       {
                           if (error != null)
                           {
                               Logger.Log("Failed fetching materials",
                                          Helpers.LogLevel.Error, Client, error);
                               return;
                           }

                           if (data == null || data.Length == 0)
                           {
                               Logger.Log("Failed fetching materials; result was empty.",
                                          Helpers.LogLevel.Error, Client);

                               Logger
                                   .Log($"Sent:\n{uri}\n{Convert.ToBase64String(OSDParser.SerializeLLSDBinary(request), Base64FormattingOptions.InsertLineBreaks)}",
                                        Helpers.LogLevel.Info, Client);

                               return;
                           }

                           try
                           {
                               OSD result = OSDParser.Deserialize(data);
                               RenderMaterialsMessage info = new RenderMaterialsMessage();
                               info.Deserialize(result as OSDMap);

                               if (info.MaterialData is OSDArray mats)
                               {
                                   foreach (var entry in mats)
                                   {
                                       if (entry is OSDMap map)
                                       {
                                           matsToReturn.Add(new LegacyMaterial(map));
                                       }
                                       else
                                       {
                                           Logger.Log("Unexpected OSD return;\n" + OSDParser.SerializeJson(entry, true).ToJson(), 
                                               Helpers.LogLevel.Info, Client);
                                       }
                                   }
                               }
                               else
                               {
                                   Logger.Log("Unexpected OSD return;\n" + OSDParser.SerializeJson(result, true).ToJson(), 
                                       Helpers.LogLevel.Info, Client);
                               }

                               Logger.Log($"Fetched (x{matsToReturn.Count}) from {uri}", Helpers.LogLevel.Info, Client);
                           }
                           catch (Exception ex)
                           {
                               Logger.Log("Failed fetching RenderMaterials",
                                          Helpers.LogLevel.Error, Client, ex);

                               Logger.Log($"Sent:\n{uri}\n{System.Text.Encoding.UTF8.GetString(OSDParser.SerializeLLSDXmlBytes(request))}",
                                        Helpers.LogLevel.Info, Client);

                               Logger.Log("Requests: " + string.Join(",", materials.Select(m => m.ToString())),
                                       Helpers.LogLevel.Info);

                               if (data.Length > 0)
                               {
                                   Logger
                                       .Log("Unable to parse response; " + System.Text.Encoding.UTF8.GetString(data),
                                            Helpers.LogLevel.Info, Client);
                               }
                           }
                       });

            return matsToReturn;
        }
        #endregion

        #region Packet Handlers

        private void ObjectAnimationHandler(object sender, PacketReceivedEventArgs e)
        {
            if (!(e.Packet is ObjectAnimationPacket data)) { return; }

            List<Animation> signaledAnimations = new List<Animation>(data.AnimationList.Length);

            for (var i = 0; i < data.AnimationList.Length; i++)
            {
                Animation animation = new Animation
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
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            ObjectUpdatePacket update = (ObjectUpdatePacket)packet;
            UpdateDilation(e.Simulator, update.RegionData.TimeDilation);

            foreach (var block in update.ObjectData)
            {
                ObjectMovementUpdate objectupdate = new ObjectMovementUpdate();
                //Vector4 collisionPlane = Vector4.Zero;
                //Vector3 position;
                //Vector3 velocity;
                //Vector3 acceleration;
                //Quaternion rotation;
                //Vector3 angularVelocity;
                NameValue[] nameValues;
                bool attachment = false;
                PCode pcode = (PCode)block.PCode;

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

                string nameValue = Utils.BytesToString(block.NameValue);
                if (nameValue.Length > 0)
                {
                    string[] lines = nameValue.Split('\n');
                    nameValues = new NameValue[lines.Length];

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(lines[i]))
                        {
                            NameValue nv = new NameValue(lines[i]);
                            if (nv.Name == "AttachItemID") attachment = true;
                            nameValues[i] = nv;
                        }
                    }
                }
                else
                {
                    nameValues = Array.Empty<NameValue>();
                }

                #endregion NameValue parsing

                #region Decode Object (primitive) parameters
                Primitive.ConstructionData data = new Primitive.ConstructionData
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
                int pos = 0;
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
                        // Accleration
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
                        Logger.Log("Got an ObjectUpdate block with ObjectUpdate field length of " +
                                   block.ObjectData.Length, Helpers.LogLevel.Warning, Client);

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

                        bool isNewObject;
                        lock (simulator.ObjectsPrimitives.Dictionary)
                            isNewObject = !simulator.ObjectsPrimitives.ContainsKey(block.ID);

                        Primitive prim = GetPrimitive(simulator, block.ID, block.FullID);

                        // Textures
                        objectupdate.Textures = new Primitive.TextureEntry(block.TextureEntry, 0,
                            block.TextureEntry.Length);

                        OnObjectDataBlockUpdate(new ObjectDataBlockUpdateEventArgs(simulator, prim, data, block, objectupdate, nameValues));

                        #region Update Prim Info with decoded data
                        prim.Flags = (PrimFlags)block.UpdateFlags;

                        if ((prim.Flags & PrimFlags.ZlibCompressed) != 0)
                        {
                            Logger.Log("Got a ZlibCompressed ObjectUpdate, implement me!",
                                Helpers.LogLevel.Warning, Client);
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
                                    Logger.Log("Got a foliage update with an invalid TreeSpecies field", Helpers.LogLevel.Warning);
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
                        
                        EventHandler<PrimEventArgs> handler = m_ObjectUpdate;
                        if (handler != null)
                        {
                            ThreadPool.QueueUserWorkItem(delegate(object o)
                                { handler(this, new PrimEventArgs(simulator, prim, update.RegionData.TimeDilation, isNewObject, attachment)); });
                        }
                        //OnParticleUpdate handler replacing decode particles, PCode.Particle system appears to be deprecated this is a fix
                        if (prim.ParticleSys.PartMaxAge != 0) {
                            OnParticleUpdate(new ParticleUpdateEventArgs(simulator, prim.ParticleSys, prim));
                        }

                        break;
                    #endregion Prim and Foliage
                    #region Avatar
                    case PCode.Avatar:

                        bool isNewAvatar;
                        lock (simulator.ObjectsAvatars.Dictionary)
                            isNewAvatar = !simulator.ObjectsAvatars.ContainsKey(block.ID);

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

                        Avatar avatar = GetAvatar(simulator, block.ID, block.FullID);

                        objectupdate.Avatar = true;
                        // Textures
                        objectupdate.Textures = new Primitive.TextureEntry(block.TextureEntry, 0,
                            block.TextureEntry.Length);

                        OnObjectDataBlockUpdate(new ObjectDataBlockUpdateEventArgs(simulator, avatar, data, block, objectupdate, nameValues));

                        uint oldSeatID = avatar.ParentID;

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
                            Logger.Log("Unexpected Data field for an avatar update, length " + block.Data.Length, Helpers.LogLevel.Warning);
                        }
                        avatar.ParentID = block.ParentID;
                        avatar.RegionHandle = update.RegionData.RegionHandle;

                        SetAvatarSittingOn(simulator, avatar, block.ParentID, oldSeatID);

                        // Textures
                        avatar.Textures = objectupdate.Textures;

                        #endregion Create an Avatar from the decoded data

                        OnAvatarUpdate(new AvatarUpdateEventArgs(simulator, avatar, update.RegionData.TimeDilation, isNewAvatar));

                        break;
                    #endregion Avatar
                    case PCode.ParticleSystem:
                        DecodeParticleUpdate(block);
                        break;
                    default:
                        Logger.DebugLog("Got an ObjectUpdate block with an unrecognized PCode " + pcode, Client);
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
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            ImprovedTerseObjectUpdatePacket terse = (ImprovedTerseObjectUpdatePacket)packet;
            UpdateDilation(simulator, terse.RegionData.TimeDilation);

            foreach (var block in terse.ObjectData)
            {
                try
                {
                    int pos = 4;
                    uint localid = Utils.BytesToUInt(block.Data, 0);

                    // Check if we are interested in this update
                    if (!Client.Settings.ALWAYS_DECODE_OBJECTS
                        && localid != Client.Self.localID
                        && m_TerseObjectUpdate == null)
                    {
                        continue;
                    }

                    #region Decode update data

                    ObjectMovementUpdate update = new ObjectMovementUpdate
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

                    Primitive obj = !Client.Settings.OBJECT_TRACKING ? null : (update.Avatar) ?
                        GetAvatar(simulator, update.LocalID, UUID.Zero) :
                        GetPrimitive(simulator, update.LocalID, UUID.Zero);

                    // Fire the pre-emptive notice (before we stomp the object)
                    EventHandler<TerseObjectUpdateEventArgs> handler = m_TerseObjectUpdate;
                    if (handler != null)
                    {
                        ThreadPool.QueueUserWorkItem(delegate(object o)
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
                    Logger.Log(ex.Message, Helpers.LogLevel.Warning, Client, ex);
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ObjectUpdateCompressedHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            ObjectUpdateCompressedPacket update = (ObjectUpdateCompressedPacket)packet;

            foreach (var block in update.ObjectData)
            {
                int i = 0;

                try
                {
                    // UUID
                    UUID FullID = new UUID(block.Data, 0);
                    i += 16;
                    // Local ID
                    uint LocalID = (uint)(block.Data[i++] + (block.Data[i++] << 8) +
                                          (block.Data[i++] << 16) + (block.Data[i++] << 24));
                    // PCode
                    PCode pcode = (PCode)block.Data[i++];

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

                    bool isNew;
                    lock (simulator.ObjectsPrimitives.Dictionary)
                        isNew = !simulator.ObjectsPrimitives.ContainsKey(LocalID);

                    Primitive prim = GetPrimitive(simulator, LocalID, FullID);

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
                    CompressedFlags flags = (CompressedFlags)Utils.BytesToUInt(block.Data, i);
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
                        int idx = i;
                        while (block.Data[i] != 0)
                        {
                            i++;
                        }

                        // Floating text
                        prim.Text = Utils.BytesToString(block.Data, idx, i - idx);
                        i++;

                        // Text color
                        prim.TextColor = new Color4(block.Data, i,false,true);
                        i += 4;
                    }
                    else
                    {
                        prim.Text = string.Empty;
                    }

                    // Media URL
                    if ((flags & CompressedFlags.MediaURL) != 0)
                    {
                        int idx = i;
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
                        string text = string.Empty;
                        while (block.Data[i] != 0)
                        {
                            text += (char)block.Data[i];
                            i++;
                        }
                        i++;

                        // Parse the name values
                        if (text.Length > 0)
                        {
                            string[] lines = text.Split('\n');
                            prim.NameValues = new NameValue[lines.Length];

                            for (int j = 0; j < lines.Length; j++)
                            {
                                if (!string.IsNullOrEmpty(lines[j]))
                                {
                                    NameValue nv = new NameValue(lines[j]);
                                    prim.NameValues[j] = nv;
                                }
                            }
                        }
                    }

                    prim.PrimData.PathCurve = (PathCurve)block.Data[i++];
                    ushort pathBegin = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.PathBegin = Primitive.UnpackBeginCut(pathBegin);
                    ushort pathEnd = Utils.BytesToUInt16(block.Data, i); i += 2;
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
                    ushort profileBegin = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.ProfileBegin = Primitive.UnpackBeginCut(profileBegin);
                    ushort profileEnd = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.ProfileEnd = Primitive.UnpackEndCut(profileEnd);
                    ushort profileHollow = Utils.BytesToUInt16(block.Data, i); i += 2;
                    prim.PrimData.ProfileHollow = Primitive.UnpackProfileHollow(profileHollow);

                    // TextureEntry
                    int textureEntryLength = (int)Utils.BytesToUInt(block.Data, i);
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

                    EventHandler<PrimEventArgs> handler = m_ObjectUpdate;
                    handler?.Invoke(this, new PrimEventArgs(simulator, prim, update.RegionData.TimeDilation, isNew, prim.IsAttachment));

                    #endregion
                }
                catch (IndexOutOfRangeException ex)
                {
                    Logger.Log("Error decoding an ObjectUpdateCompressed packet", Helpers.LogLevel.Warning, Client, ex);
                    Logger.Log(block, Helpers.LogLevel.Warning);
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
                bool cachedPrimitives = Client.Settings.CACHE_PRIMITIVES;
                Packet packet = e.Packet;
                Simulator simulator = e.Simulator;

                ObjectUpdateCachedPacket update = (ObjectUpdateCachedPacket)packet;
                List<uint> ids = new List<uint>(update.ObjectData.Length);

                // Object caching is implemented when Client.Settings.PRIMITIVES_FACTORY is True, otherwise request updates for all of these objects
                foreach (var odb in update.ObjectData)
                {
                    uint localID = odb.ID;

                    if (cachedPrimitives)
                    {
                        if (!simulator.DataPool.NeedsRequest(localID, odb.CRC))
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
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            KillObjectPacket kill = (KillObjectPacket)packet;

            // Notify first, so that handler has a chance to get a
            // reference from the ObjectTracker to the object being killed
            uint[] killed = new uint[kill.ObjectData.Length];
            for (int i = 0; i < kill.ObjectData.Length; i++)
            {
                OnKillObject(new KillObjectEventArgs(simulator, kill.ObjectData[i].ID));
                killed[i] = kill.ObjectData[i].ID;
            }
            OnKillObjects(new KillObjectsEventArgs(e.Simulator, killed));


            lock (simulator.ObjectsPrimitives.Dictionary)
            {
                List<uint> removeAvatars = new List<uint>();
                List<uint> removePrims = new List<uint>();

                if (Client.Settings.OBJECT_TRACKING)
                {
                    uint localID;
                    foreach (var odb in kill.ObjectData)
                    {
                        localID = odb.ID;

                        if (simulator.ObjectsPrimitives.Dictionary.ContainsKey(localID))
                            removePrims.Add(localID);

                        foreach (var prim in simulator.ObjectsPrimitives.Dictionary.Where(prim => prim.Value.ParentID == localID))
                        {
                            OnKillObject(new KillObjectEventArgs(simulator, prim.Key));
                            removePrims.Add(prim.Key);
                        }
                    }
                }

                if (Client.Settings.AVATAR_TRACKING)
                {
                    lock (simulator.ObjectsAvatars.Dictionary)
                    {
                        uint localID;
                        foreach (var odb in kill.ObjectData)
                        {
                            localID = odb.ID;

                            if (simulator.ObjectsAvatars.Dictionary.ContainsKey(localID))
                                removeAvatars.Add(localID);

                            List<uint> rootPrims = new List<uint>();

                            foreach (var prim in simulator.ObjectsPrimitives.Dictionary.Where(prim => prim.Value.ParentID == localID))
                            {
                                OnKillObject(new KillObjectEventArgs(simulator, prim.Key));
                                removePrims.Add(prim.Key);
                                rootPrims.Add(prim.Key);
                            }

                            foreach (var prim in simulator.ObjectsPrimitives.Dictionary.Where(prim => rootPrims.Contains(prim.Value.ParentID)))
                            {
                                OnKillObject(new KillObjectEventArgs(simulator, prim.Key));
                                removePrims.Add(prim.Key);
                            }
                        }

                        //Do the actual removing outside the loops but still inside the lock.
                        //This safely prevents the collection from being modified during a loop.
                        foreach (uint removeID in removeAvatars)
                            simulator.ObjectsAvatars.Dictionary.Remove(removeID);
                    }
                }

                if (Client.Settings.CACHE_PRIMITIVES)
                {
                    simulator.DataPool.ReleasePrims(removePrims);
                }
                foreach (uint removeID in removePrims)
                    simulator.ObjectsPrimitives.Dictionary.Remove(removeID);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ObjectPropertiesHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            ObjectPropertiesPacket op = (ObjectPropertiesPacket)packet;
            ObjectPropertiesPacket.ObjectDataBlock[] datablocks = op.ObjectData;

            foreach (var objectData in datablocks)
            {
                Primitive.ObjectProperties props = new Primitive.ObjectProperties
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

                int numTextures = objectData.TextureID.Length / 16;
                props.TextureIDs = new UUID[numTextures];
                for (int j = 0; j < numTextures; ++j)
                    props.TextureIDs[j] = new UUID(objectData.TextureID, j * 16);

                if (Client.Settings.OBJECT_TRACKING)
                {
                    Primitive findPrim = simulator.ObjectsPrimitives.Find(
                        prim => prim.ID == props.ObjectID);

                    if (findPrim != null)
                    {
                        OnObjectPropertiesUpdated(new ObjectPropertiesUpdatedEventArgs(simulator, findPrim, props));

                        lock (simulator.ObjectsPrimitives.Dictionary)
                        {
                            if (simulator.ObjectsPrimitives.Dictionary.ContainsKey(findPrim.LocalID))
                                simulator.ObjectsPrimitives.Dictionary[findPrim.LocalID].Properties = props;
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
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            ObjectPropertiesFamilyPacket op = (ObjectPropertiesFamilyPacket)packet;
            Primitive.ObjectProperties props = new Primitive.ObjectProperties();

            ReportType requestType = (ReportType)op.ObjectData.RequestFlags;

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
                Primitive findPrim = simulator.ObjectsPrimitives.Find(
                    prim => prim.ID == op.ObjectData.ObjectID);

                if (findPrim != null)
                {
                    lock (simulator.ObjectsPrimitives.Dictionary)
                    {
                        if (simulator.ObjectsPrimitives.Dictionary.ContainsKey(findPrim.LocalID))
                        {
                            if (simulator.ObjectsPrimitives.Dictionary[findPrim.LocalID].Properties == null)
                                simulator.ObjectsPrimitives.Dictionary[findPrim.LocalID].Properties = new Primitive.ObjectProperties();
                            simulator.ObjectsPrimitives.Dictionary[findPrim.LocalID].Properties.SetFamilyProperties(props);
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
                Packet packet = e.Packet;
                Simulator simulator = e.Simulator;

                PayPriceReplyPacket p = (PayPriceReplyPacket)packet;
                UUID objectID = p.ObjectData.ObjectID;
                int defaultPrice = p.ObjectData.DefaultPayPrice;
                int[] buttonPrices = new int[p.ButtonData.Length];

                for (int i = 0; i < p.ButtonData.Length; i++)
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
            ObjectPhysicsPropertiesMessage msg = (ObjectPhysicsPropertiesMessage)message;

            if (Client.Settings.OBJECT_TRACKING)
            {
                foreach (var prop in msg.ObjectPhysicsProperties)
                {
                    lock (simulator.ObjectsPrimitives.Dictionary)
                    {
                        if (simulator.ObjectsPrimitives.Dictionary.TryGetValue(prop.LocalID, out var primitive))
                        {
                            primitive.PhysicsProps = prop;
                        }
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

        #endregion Packet Handlers

        #region Utility Functions

        /// <summary>
        /// Setup construction data for a basic primitive shape
        /// </summary>
        /// <param name="type">Primitive shape to construct</param>
        /// <returns>Construction data that can be plugged into a <see cref="Primitive"/></returns>
        public static Primitive.ConstructionData BuildBasicShape(PrimType type)
        {
            Primitive.ConstructionData prim = new Primitive.ConstructionData
            {
                PCode = PCode.Prim,
                Material = Material.Wood
            };

            switch (type)
            {
                case PrimType.Box:
                    prim.ProfileCurve = ProfileCurve.Square;
                    prim.PathCurve = PathCurve.Line;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 1f;
                    prim.PathScaleY = 1f;
                    prim.PathRevolutions = 1f;
                    break;
                case PrimType.Cylinder:
                    prim.ProfileCurve = ProfileCurve.Circle;
                    prim.PathCurve = PathCurve.Line;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 1f;
                    prim.PathScaleY = 1f;
                    prim.PathRevolutions = 1f;
                    break;
                case PrimType.Prism:
                    prim.ProfileCurve = ProfileCurve.EqualTriangle;
                    prim.PathCurve = PathCurve.Line;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 0f;
                    prim.PathScaleY = 0f;
                    prim.PathRevolutions = 1f;
                    break;
                case PrimType.Ring:
                    prim.ProfileCurve = ProfileCurve.EqualTriangle;
                    prim.PathCurve = PathCurve.Circle;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 1f;
                    prim.PathScaleY = 0.25f;
                    prim.PathRevolutions = 1f;
                    break;
                case PrimType.Sphere:
                    prim.ProfileCurve = ProfileCurve.HalfCircle;
                    prim.PathCurve = PathCurve.Circle;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 1f;
                    prim.PathScaleY = 1f;
                    prim.PathRevolutions = 1f;
                    break;
                case PrimType.Torus:
                    prim.ProfileCurve = ProfileCurve.Circle;
                    prim.PathCurve = PathCurve.Circle;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 1f;
                    prim.PathScaleY = 0.25f;
                    prim.PathRevolutions = 1f;
                    break;
                case PrimType.Tube:
                    prim.ProfileCurve = ProfileCurve.Square;
                    prim.PathCurve = PathCurve.Circle;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 1f;
                    prim.PathScaleY = 0.25f;
                    prim.PathRevolutions = 1f;
                    break;
                case PrimType.Sculpt:
                    prim.ProfileCurve = ProfileCurve.Circle;
                    prim.PathCurve = PathCurve.Circle;
                    prim.ProfileEnd = 1f;
                    prim.PathEnd = 1f;
                    prim.PathScaleX = 1f;
                    prim.PathScaleY = 0.5f;
                    prim.PathRevolutions = 1f;
                    break;
                default:
                    throw new NotSupportedException("Unsupported shape: " + type);
            }

            return prim;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sim"></param>
        /// <param name="av"></param>
        /// <param name="localid"></param>
        /// <param name="oldSeatID"></param>
        protected void SetAvatarSittingOn(Simulator sim, Avatar av, uint localid, uint oldSeatID)
        {
            if (Client.Network.CurrentSim == sim && av.LocalID == Client.Self.localID)
            {
                Client.Self.sittingOn = localid;
            }

            av.ParentID = localid;


            if (m_AvatarSitChanged != null && oldSeatID != localid)
            {
                OnAvatarSitChanged(new AvatarSitChangedEventArgs(sim, av, localid, oldSeatID));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="dilation"></param>
        protected void UpdateDilation(Simulator s, uint dilation)
        {
            s.Stats.Dilation = dilation / 65535.0f;
        }


        /// <summary>
        /// Set the Shape data of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="prim">Data describing the prim shape</param>
        public void SetShape(Simulator simulator, uint localID, Primitive.ConstructionData prim)
        {
            ObjectShapePacket shape = new ObjectShapePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new OpenMetaverse.Packets.ObjectShapePacket.ObjectDataBlock[1]
            };

            shape.ObjectData[0] = new OpenMetaverse.Packets.ObjectShapePacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                PathCurve = (byte)prim.PathCurve,
                PathBegin = Primitive.PackBeginCut(prim.PathBegin),
                PathEnd = Primitive.PackEndCut(prim.PathEnd),
                PathScaleX = Primitive.PackPathScale(prim.PathScaleX),
                PathScaleY = Primitive.PackPathScale(prim.PathScaleY),
                PathShearX = (byte)Primitive.PackPathShear(prim.PathShearX),
                PathShearY = (byte)Primitive.PackPathShear(prim.PathShearY),
                PathTwist = Primitive.PackPathTwist(prim.PathTwist),
                PathTwistBegin = Primitive.PackPathTwist(prim.PathTwistBegin),
                PathRadiusOffset = Primitive.PackPathTwist(prim.PathRadiusOffset),
                PathTaperX = Primitive.PackPathTaper(prim.PathTaperX),
                PathTaperY = Primitive.PackPathTaper(prim.PathTaperY),
                PathRevolutions = Primitive.PackPathRevolutions(prim.PathRevolutions),
                PathSkew = Primitive.PackPathTwist(prim.PathSkew),
                ProfileCurve = prim.profileCurve,
                ProfileBegin = Primitive.PackBeginCut(prim.ProfileBegin),
                ProfileEnd = Primitive.PackEndCut(prim.ProfileEnd),
                ProfileHollow = Primitive.PackProfileHollow(prim.ProfileHollow)
            };

            Client.Network.SendPacket(shape, simulator);
        }

        /// <summary>
        /// Set the Material data of an object
        /// </summary>
        /// <param name="simulator">A reference to the <see cref="OpenMetaverse.Simulator"/> object where the object resides</param>
        /// <param name="localID">The objects ID which is local to the simulator the object is in</param>
        /// <param name="material">The new material of the object</param>
        public void SetMaterial(Simulator simulator, uint localID, Material material)
        {
            ObjectMaterialPacket matPacket = new ObjectMaterialPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = new ObjectMaterialPacket.ObjectDataBlock[1]
            };

            matPacket.ObjectData[0] = new ObjectMaterialPacket.ObjectDataBlock
            {
                ObjectLocalID = localID,
                Material = (byte)material
            };

            Client.Network.SendPacket(matPacket, simulator);
        }


        #endregion Utility Functions

        #region Object Tracking Link

                /// <summary>
        /// 
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="localID"></param>
        /// <param name="fullID"></param>
        /// <returns></returns>
        protected Primitive GetPrimitive(Simulator simulator, uint localID, UUID fullID)
        {
            return GetPrimitive(simulator, localID, fullID, true);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="localID"></param>
        /// <param name="fullID"></param>
        /// <param name="createIfMissing"></param>
        /// <returns></returns>
        public Primitive GetPrimitive(Simulator simulator, uint localID, UUID fullID, bool createIfMissing)
        {
            if (Client.Settings.OBJECT_TRACKING)
            {
                lock (simulator.ObjectsPrimitives.Dictionary)
                {
                    if (simulator.ObjectsPrimitives.Dictionary.TryGetValue(localID, out var prim))
                    {
                        return prim;
                    }
                    else
                    {
                        if (!createIfMissing) { return null; }
                        if (Client.Settings.CACHE_PRIMITIVES)
                        {
                            prim = simulator.DataPool.MakePrimitive(localID);
                        }
                        else
                        {
                            prim = new Primitive
                            {
                                LocalID = localID,
                                RegionHandle = simulator.Handle
                            };
                        }
                        prim.ActiveClients++;
                        prim.ID = fullID;

                        simulator.ObjectsPrimitives.Dictionary[localID] = prim;

                        return prim;
                    }
                }
            }
            else
            {
                return new Primitive();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="localID"></param>
        /// <param name="fullID"></param>
        /// <returns></returns>
        protected Avatar GetAvatar(Simulator simulator, uint localID, UUID fullID)
        {
            if (Client.Settings.AVATAR_TRACKING)
            {
                lock (simulator.ObjectsAvatars.Dictionary)
                {
                    if (simulator.ObjectsAvatars.Dictionary.TryGetValue(localID, out var avatar))
                    {
                        return avatar;
                    }

                    avatar = new Avatar
                    {
                        LocalID = localID,
                        ID = fullID,
                        RegionHandle = simulator.Handle
                    };

                    simulator.ObjectsAvatars.Dictionary[localID] = avatar;

                    return avatar;
                }
            }
            else
            {
                return new Avatar();
            }
        }

        #endregion Object Tracking Link

        protected void InterpolationTimer_Elapsed(object obj)
        {
            int elapsed = 0;

            if (Client.Network.Connected)
            {
                int start = Environment.TickCount;

                int interval = Environment.TickCount - Client.Self.lastInterpolation;
                float seconds = interval / 1000f;

                // Iterate through all simulators
                Simulator[] sims = Client.Network.Simulators.ToArray();
                foreach (var sim in sims)
                {
                    float adjSeconds = seconds * sim.Stats.Dilation;

                    // Iterate through all of this region's avatars
                    sim.ObjectsAvatars.ForEach(
                        delegate(Avatar avatar)
                        {
                            #region Linear Motion
                            // Only do movement interpolation (extrapolation) when there is a non-zero velocity but 
                            // no acceleration
                            if (avatar.Acceleration != Vector3.Zero && avatar.Velocity == Vector3.Zero)
                            {
                                avatar.Position += (avatar.Velocity + avatar.Acceleration *
                                    (0.5f * (adjSeconds - HAVOK_TIMESTEP))) * adjSeconds;
                                avatar.Velocity += avatar.Acceleration * adjSeconds;
                            }
                            #endregion Linear Motion
                        }
                    );

                    // Iterate through all of this region's primitives
                    sim.ObjectsPrimitives.ForEach(
                        delegate(Primitive prim)
                        {
                            if (prim.Joint == JointType.Invalid)
                            {
                                #region Angular Velocity
                                Vector3 angVel = prim.AngularVelocity;
                                float omega = angVel.LengthSquared();

                                if (omega > 0.00001f)
                                {
                                    omega = (float)Math.Sqrt(omega);
                                    float angle = omega * adjSeconds;
                                    angVel *= 1.0f / omega;
                                    Quaternion dQ = Quaternion.CreateFromAxisAngle(angVel, angle);

                                    prim.Rotation *= dQ;
                                }
                                #endregion Angular Velocity

                                #region Linear Motion
                                // Only do movement interpolation (extrapolation) when there is a non-zero velocity but 
                                // no acceleration
                                if (prim.Acceleration != Vector3.Zero && prim.Velocity == Vector3.Zero)
                                {
                                    prim.Position += (prim.Velocity + prim.Acceleration *
                                        (0.5f * (adjSeconds - HAVOK_TIMESTEP))) * adjSeconds;
                                    prim.Velocity += prim.Acceleration * adjSeconds;
                                }
                                #endregion Linear Motion
                            }
                            else if (prim.Joint == JointType.Hinge)
                            {
                                //FIXME: Hinge movement extrapolation
                            }
                            else if (prim.Joint == JointType.Point)
                            {
                                //FIXME: Point movement extrapolation
                            }
                            else
                            {
                                Logger.Log("Unhandled joint type " + prim.Joint, Helpers.LogLevel.Warning, Client);
                            }
                        }
                    );
                }

                // Make sure the last interpolated time is always updated
                Client.Self.lastInterpolation = Environment.TickCount;

                elapsed = Client.Self.lastInterpolation - start;
            }

            // Start the timer again. Use a minimum of a 50ms pause in between calculations
            int delay = Math.Max(50, Client.Settings.INTERPOLATION_INTERVAL - elapsed);
            InterpolationTimer?.Change(delay, Timeout.Infinite);

        }
    }
    #region EventArgs classes

    /// <summary>Provides data for the <see cref="ObjectManager.ObjectUpdate"/> event</summary>
    /// <remarks><para>The <see cref="ObjectManager.ObjectUpdate"/> event occurs when the simulator sends
    /// an <see cref="ObjectUpdatePacket"/> containing a Primitive, Foliage or Attachment data</para>
    /// <para>Note 1: The <see cref="ObjectManager.ObjectUpdate"/> event will not be raised when the object is an Avatar</para>
    /// <para>Note 2: It is possible for the <see cref="ObjectManager.ObjectUpdate"/> to be 
    /// raised twice for the same object if for example the primitive moved to a new simulator, then returned to the current simulator or
    /// if an Avatar crosses the border into a new simulator and returns to the current simulator</para>
    /// </remarks>
    /// <example>
    /// The following code example uses the <see cref="PrimEventArgs.Prim"/>, <see cref="PrimEventArgs.Simulator"/>, and <see cref="PrimEventArgs.IsAttachment"/>
    /// properties to display new Primitives and Attachments on the <see cref="Console"/> window.
    /// <code>
    ///     // Subscribe to the event that gives us prim and foliage information
    ///     Client.Objects.ObjectUpdate += Objects_ObjectUpdate;
    ///     
    ///
    ///     private void Objects_ObjectUpdate(object sender, PrimEventArgs e)
    ///     {
    ///         Console.WriteLine("Primitive {0} {1} in {2} is an attachment {3}", e.Prim.ID, e.Prim.LocalID, e.Simulator.Name, e.IsAttachment);
    ///     }
    /// </code>
    /// </example>
    /// <seealso cref="ObjectManager.ObjectUpdate"/>
    /// <seealso cref="ObjectManager.AvatarUpdate"/>
    /// <seealso cref="AvatarUpdateEventArgs"/>
    public class PrimEventArgs : EventArgs
    {
        /// <summary>Get the simulator the <see cref="Primitive"/> originated from</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the <see cref="Primitive"/> details</summary>
        public Primitive Prim { get; }

        /// <summary>true if the <see cref="Primitive"/> did not exist in the dictionary before this update (always true if object tracking has been disabled)</summary>
        public bool IsNew { get; }

        /// <summary>true if the <see cref="Primitive"/> is attached to an <see cref="Avatar"/></summary>
        public bool IsAttachment { get; }

        /// <summary>Get the simulator Time Dilation</summary>
        public ushort TimeDilation { get; }

        /// <summary>
        /// Construct a new instance of the PrimEventArgs class
        /// </summary>
        /// <param name="simulator">The simulator the object originated from</param>
        /// <param name="prim">The Primitive</param>
        /// <param name="timeDilation">The simulator time dilation</param>
        /// <param name="isNew">The prim was not in the dictionary before this update</param>
        /// <param name="isAttachment">true if the primitive represents an attachment to an agent</param>
        public PrimEventArgs(Simulator simulator, Primitive prim, ushort timeDilation, bool isNew, bool isAttachment)
        {
            this.Simulator = simulator;
            this.IsNew = isNew;
            this.IsAttachment = isAttachment;
            this.Prim = prim;
            this.TimeDilation = timeDilation;
        }
    }

    /// <summary>Provides data for the <see cref="ObjectManager.AvatarUpdate"/> event</summary>
    /// <remarks><para>The <see cref="ObjectManager.AvatarUpdate"/> event occurs when the simulator sends
    /// <see cref="ObjectUpdatePacket"/> containing Avatar data</para>    
    /// <para>Note 1: The <see cref="ObjectManager.AvatarUpdate"/> event will not be raised when the object is an Avatar</para>
    /// <para>Note 2: It is possible for the <see cref="ObjectManager.AvatarUpdate"/> to be 
    /// raised twice for the same avatar if for example the avatar moved to a new simulator, then returned to the current simulator</para>
    /// </remarks>
    /// <example>
    /// The following code example uses the <see cref="AvatarUpdateEventArgs.Avatar"/> property to make a request for the top picks
    /// using the <see cref="AvatarManager.RequestAvatarPicks"/> method in the <see cref="AvatarManager"/> class to display the names
    /// of our own agents picks listings on the <see cref="Console"/> window.
    /// <code>
    ///     // subscribe to the AvatarUpdate event to get our information
    ///     Client.Objects.AvatarUpdate += Objects_AvatarUpdate;
    ///     Client.Avatars.AvatarPicksReply += Avatars_AvatarPicksReply;
    ///     
    ///     private void Objects_AvatarUpdate(object sender, AvatarUpdateEventArgs e)
    ///     {
    ///         // we only want our own data
    ///         if (e.Avatar.LocalID == Client.Self.LocalID)
    ///         {    
    ///             // Unsubscribe from the avatar update event to prevent a loop
    ///             // where we continually request the picks every time we get an update for ourselves
    ///             Client.Objects.AvatarUpdate -= Objects_AvatarUpdate;
    ///             // make the top picks request through AvatarManager
    ///             Client.Avatars.RequestAvatarPicks(e.Avatar.ID);
    ///         }
    ///     }
    ///
    ///     private void Avatars_AvatarPicksReply(object sender, AvatarPicksReplyEventArgs e)
    ///     {
    ///         // we'll unsubscribe from the AvatarPicksReply event since we now have the data 
    ///         // we were looking for
    ///         Client.Avatars.AvatarPicksReply -= Avatars_AvatarPicksReply;
    ///         // loop through the dictionary and extract the names of the top picks from our profile
    ///         foreach (var pickName in e.Picks.Values)
    ///         {
    ///             Console.WriteLine(pickName);
    ///         }
    ///     }
    /// </code>
    /// </example>
    /// <seealso cref="ObjectManager.ObjectUpdate"/>
    /// <seealso cref="PrimEventArgs"/>
    public class AvatarUpdateEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object originated from</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the <see cref="Avatar"/> data</summary>
        public Avatar Avatar { get; }

        /// <summary>Get the simulator time dilation</summary>
        public ushort TimeDilation { get; }

        /// <summary>true if the <see cref="Avatar"/> did not exist in the dictionary before this update (always true if avatar tracking has been disabled)</summary>
        public bool IsNew { get; }

        /// <summary>
        /// Construct a new instance of the AvatarUpdateEventArgs class
        /// </summary>
        /// <param name="simulator">The simulator the packet originated from</param>
        /// <param name="avatar">The <see cref="Avatar"/> data</param>
        /// <param name="timeDilation">The simulator time dilation</param>
        /// <param name="isNew">The avatar was not in the dictionary before this update</param>
        public AvatarUpdateEventArgs(Simulator simulator, Avatar avatar, ushort timeDilation, bool isNew)
        {
            this.Simulator = simulator;
            this.Avatar = avatar;
            this.TimeDilation = timeDilation;
            this.IsNew = isNew;
        }
    }

    public class ObjectAnimationEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent</summary>
        public UUID ObjectID { get; }

        /// <summary>Get the list of animations to start</summary>
        public List<Animation> Animations { get; }

        /// <summary>
        /// Construct a new instance of the AvatarAnimationEventArgs class
        /// </summary>
        /// <param name="objectID">The ID of the agent</param>
        /// <param name="anims">The list of animations to start</param>
        public ObjectAnimationEventArgs(UUID objectID, List<Animation> anims)
        {
            this.ObjectID = objectID;
            this.Animations = anims;
        }
    }


    public class ParticleUpdateEventArgs : EventArgs {
        /// <summary>Get the simulator the object originated from</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the <see cref="ParticleSystem"/> data</summary>
        public Primitive.ParticleSystem ParticleSystem { get; }

        /// <summary>Get <see cref="Primitive"/> source</summary>
        public Primitive Source { get; }

        /// <summary>
        /// Construct a new instance of the ParticleUpdateEventArgs class
        /// </summary>
        /// <param name="simulator">The simulator the packet originated from</param>
        /// <param name="particlesystem">The ParticleSystem data</param>
        /// <param name="source">The Primitive source</param>
        public ParticleUpdateEventArgs(Simulator simulator, Primitive.ParticleSystem particlesystem, Primitive source) {
            this.Simulator = simulator;
            this.ParticleSystem = particlesystem;
            this.Source = source;
        }
    }

    /// <summary>Provides additional primitive data for the <see cref="ObjectManager.ObjectProperties"/> event</summary>
    /// <remarks><para>The <see cref="ObjectManager.ObjectProperties"/> event occurs when the simulator sends
    /// <see cref="ObjectPropertiesPacket"/> containing additional details for a Primitive, Foliage data or Attachment data</para>
    /// <para>The <see cref="ObjectManager.ObjectProperties"/> event is also raised when a <see cref="ObjectManager.SelectObject"/> request is
    /// made.</para>
    /// </remarks>
    /// <example>
    /// The following code example uses the <see cref="PrimEventArgs.Prim"/>, <see cref="PrimEventArgs.Simulator"/> and
    /// <see cref="ObjectPropertiesEventArgs.Properties"/>
    /// properties to display new attachments and send a request for additional properties containing the name of the
    /// attachment then display it on the <see cref="Console"/> window.
    /// <code>    
    ///     // Subscribe to the event that provides additional primitive details
    ///     Client.Objects.ObjectProperties += Objects_ObjectProperties;
    ///      
    ///     // handle the properties data that arrives
    ///     private void Objects_ObjectProperties(object sender, ObjectPropertiesEventArgs e)
    ///     {
    ///         Console.WriteLine("Primitive Properties: {0} Name is {1}", e.Properties.ObjectID, e.Properties.Name);
    ///     }   
    /// </code>
    /// </example>
    public class ObjectPropertiesEventArgs : EventArgs
    {
        protected readonly Simulator m_Simulator;
        protected readonly Primitive.ObjectProperties m_Properties;

        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator => m_Simulator;

        /// <summary>Get the primitive properties</summary>
        public Primitive.ObjectProperties Properties => m_Properties;

        /// <summary>
        /// Construct a new instance of the ObjectPropertiesEventArgs class
        /// </summary>
        /// <param name="simulator">The simulator the object is located</param>
        /// <param name="props">The primitive Properties</param>
        public ObjectPropertiesEventArgs(Simulator simulator, Primitive.ObjectProperties props)
        {
            this.m_Simulator = simulator;
            this.m_Properties = props;
        }
    }

    /// <summary>Provides additional primitive data for the <see cref="ObjectManager.ObjectPropertiesUpdated"/> event</summary>
    /// <remarks><para>The <see cref="ObjectManager.ObjectPropertiesUpdated"/> event occurs when the simulator sends
    /// an <see cref="ObjectPropertiesPacket"/> containing additional details for a Primitive or Foliage data that is currently
    /// being tracked in the <see cref="Simulator.ObjectsPrimitives"/> dictionary</para>
    /// <para>The <see cref="ObjectManager.ObjectPropertiesUpdated"/> event is also raised when a <see cref="ObjectManager.SelectObject"/> request is
    /// made and <see cref="Settings.OBJECT_TRACKING"/> is enabled</para>    
    /// </remarks>    
    public class ObjectPropertiesUpdatedEventArgs : ObjectPropertiesEventArgs
    {
        /// <summary>Get the primitive details</summary>
        public Primitive Prim { get; }

        /// <summary>
        /// Construct a new instance of the ObjectPropertiesUpdatedEvenrArgs class
        /// </summary>                
        /// <param name="simulator">The simulator the object is located</param>
        /// <param name="prim">The Primitive</param>
        /// <param name="props">The primitive Properties</param>
        public ObjectPropertiesUpdatedEventArgs(Simulator simulator, Primitive prim, Primitive.ObjectProperties props) : base(simulator, props)
        {
            this.Prim = prim;
        }
    }

    /// <summary>Provides additional primitive data, permissions and sale info for the <see cref="ObjectManager.ObjectPropertiesFamily"/> event</summary>
    /// <remarks><para>The <see cref="ObjectManager.ObjectPropertiesFamily"/> event occurs when the simulator sends
    /// an <see cref="ObjectPropertiesPacket"/> containing additional details for a Primitive, Foliage data or Attachment. This includes
    /// Permissions, Sale info, and other basic details on an object</para>
    /// <para>The <see cref="ObjectManager.ObjectProperties"/> event is also raised when a <see cref="ObjectManager.RequestObjectPropertiesFamily"/> request is
    /// made, the viewer equivalent is hovering the mouse cursor over an object</para>
    /// </remarks>    
    public class ObjectPropertiesFamilyEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator { get; }

        /// <summary></summary>
        public Primitive.ObjectProperties Properties { get; }

        /// <summary></summary>
        public ReportType Type { get; }

        public ObjectPropertiesFamilyEventArgs(Simulator simulator, Primitive.ObjectProperties props, ReportType type)
        {
            this.Simulator = simulator;
            this.Properties = props;
            this.Type = type;
        }
    }

    /// <summary>Provides primitive data containing updated location, velocity, rotation, textures for the <see cref="ObjectManager.TerseObjectUpdate"/> event</summary>
    /// <remarks><para>The <see cref="ObjectManager.TerseObjectUpdate"/> event occurs when the simulator sends updated location, velocity, rotation, etc</para>        
    /// </remarks>
    public class TerseObjectUpdateEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the primitive details</summary>
        public Primitive Prim { get; }

        /// <summary></summary>
        public ObjectMovementUpdate Update { get; }

        /// <summary></summary>
        public ushort TimeDilation { get; }

        public TerseObjectUpdateEventArgs(Simulator simulator, Primitive prim, ObjectMovementUpdate update, ushort timeDilation)
        {
            this.Simulator = simulator;
            this.Prim = prim;
            this.Update = update;
            this.TimeDilation = timeDilation;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ObjectDataBlockUpdateEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the primitive details</summary>
        public Primitive Prim { get; }

        /// <summary></summary>
        public Primitive.ConstructionData ConstructionData { get; }

        /// <summary></summary>
        public ObjectUpdatePacket.ObjectDataBlock Block { get; }

        /// <summary></summary>
        public ObjectMovementUpdate Update { get; }

        /// <summary></summary>
        public NameValue[] NameValues { get; }

        public ObjectDataBlockUpdateEventArgs(Simulator simulator, Primitive prim, Primitive.ConstructionData constructionData,
            ObjectUpdatePacket.ObjectDataBlock block, ObjectMovementUpdate objectupdate, NameValue[] nameValues)
        {
            this.Simulator = simulator;
            this.Prim = prim;
            this.ConstructionData = constructionData;
            this.Block = block;
            this.Update = objectupdate;
            this.NameValues = nameValues;
        }
    }

    /// <summary>Provides notification when an Avatar, Object or Attachment is DeRezzed or moves out of the avatars view for the 
    /// <see cref="ObjectManager.KillObject"/> event</summary>
    public class KillObjectEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator { get; }

        /// <summary>The LocalID of the object</summary>
        public uint ObjectLocalID { get; }

        public KillObjectEventArgs(Simulator simulator, uint objectID)
        {
            this.Simulator = simulator;
            this.ObjectLocalID = objectID;
        }
    }

    /// <summary>Provides notification when an Avatar, Object or Attachment is DeRezzed or moves out of the avatars view for the 
    /// <see cref="ObjectManager.KillObjects"/> event</summary>
    public class KillObjectsEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator { get; }

        /// <summary>The LocalID of the object</summary>
        public uint[] ObjectLocalIDs { get; }

        public KillObjectsEventArgs(Simulator simulator, uint[] objectIDs)
        {
            this.Simulator = simulator;
            this.ObjectLocalIDs = objectIDs;
        }
    }

    /// <summary>
    /// Provides updates sit position data
    /// </summary>
    public class AvatarSitChangedEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator { get; }

        /// <summary></summary>
        public Avatar Avatar { get; }

        /// <summary></summary>
        public uint SittingOn { get; }

        /// <summary></summary>
        public uint OldSeat { get; }

        public AvatarSitChangedEventArgs(Simulator simulator, Avatar avatar, uint sittingOn, uint oldSeat)
        {
            this.Simulator = simulator;
            this.Avatar = avatar;
            this.SittingOn = sittingOn;
            this.OldSeat = oldSeat;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class PayPriceReplyEventArgs : EventArgs
    {
        /// <summary>Get the simulator the object is located</summary>
        public Simulator Simulator { get; }

        /// <summary></summary>
        public UUID ObjectID { get; }

        /// <summary></summary>
        public int DefaultPrice { get; }

        /// <summary></summary>
        public int[] ButtonPrices { get; }

        public PayPriceReplyEventArgs(Simulator simulator, UUID objectID, int defaultPrice, int[] buttonPrices)
        {
            this.Simulator = simulator;
            this.ObjectID = objectID;
            this.DefaultPrice = defaultPrice;
            this.ButtonPrices = buttonPrices;
        }
    }

    public class ObjectMediaEventArgs : EventArgs
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Media version string
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Array of media entries indexed by face number
        /// </summary>
        public MediaEntry[] FaceMedia { get; set; }

        public ObjectMediaEventArgs(bool success, string version, MediaEntry[] faceMedia)
        {
            this.Success = success;
            this.Version = version;
            this.FaceMedia = faceMedia;
        }
    }

    /// <summary>
    /// Set when simulator sends us information on primitive's physical properties
    /// </summary>
    public class PhysicsPropertiesEventArgs : EventArgs
    {
        /// <summary>Simulator where the message originated</summary>
        public Simulator Simulator;
        /// <summary>Updated physical properties</summary>
        public Primitive.PhysicsProperties PhysicsProperties;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sim">Simulator where the message originated</param>
        /// <param name="props">Updated physical properties</param>
        public PhysicsPropertiesEventArgs(Simulator sim, Primitive.PhysicsProperties props)
        {
            Simulator = sim;
            PhysicsProperties = props;
        }
    }

    #endregion
}
