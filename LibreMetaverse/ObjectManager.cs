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
using OpenMetaverse.StructuredData;
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
    public partial class ObjectManager : IDisposable
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

        private InterpolationService _interpolationService;

        #region Multi-Simulator Object Tracking

        /// <summary>
        /// Tracks which simulators can see a particular object (for border objects)
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<UUID, List<Simulator>> 
            _objectSimulators = new System.Collections.Concurrent.ConcurrentDictionary<UUID, List<Simulator>>();

        /// <summary>
        /// Register that an object is visible in a particular simulator
        /// </summary>
        /// <param name="objectID">UUID of the object</param>
        /// <param name="sim">Simulator where the object is visible</param>
        public void TrackObjectInSimulator(UUID objectID, Simulator sim)
        {
            if (objectID == UUID.Zero || sim == null) return;

            _objectSimulators.AddOrUpdate(objectID,
                new List<Simulator> { sim },
                (key, list) =>
                {
                    lock (list)
                    {
                        if (!list.Contains(sim))
                        {
                            list.Add(sim);
                            Logger.Debug($"Object {objectID} now tracked in {sim.Name}", Client);
                        }
                    }
                    return list;
                });
        }

        /// <summary>
        /// Remove an object from tracking in a specific simulator
        /// </summary>
        /// <param name="objectID">UUID of the object</param>
        /// <param name="sim">Simulator to remove from</param>
        public void UntrackObjectInSimulator(UUID objectID, Simulator sim)
        {
            if (_objectSimulators.TryGetValue(objectID, out var simList))
            {
                lock (simList)
                {
                    simList.Remove(sim);
                    if (simList.Count == 0)
                    {
                        _objectSimulators.TryRemove(objectID, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Get all simulators where an object is visible
        /// </summary>
        /// <param name="objectID">UUID of the object</param>
        /// <returns>List of simulators, or empty list if not tracked</returns>
        public List<Simulator> GetSimulatorsForObject(UUID objectID)
        {
            if (_objectSimulators.TryGetValue(objectID, out var simList))
            {
                lock (simList)
                {
                    return new List<Simulator>(simList);
                }
            }
            return new List<Simulator>();
        }

        /// <summary>
        /// Check if an object is near a region border (within threshold distance)
        /// </summary>
        /// <param name="position">Position of the object in region coordinates</param>
        /// <param name="regionSizeX">Region size X</param>
        /// <param name="regionSizeY">Region size Y</param>
        /// <param name="threshold">Distance threshold to consider "near" border (default 32m)</param>
        /// <returns>True if object is near any border</returns>
        public bool IsNearBorder(Vector3 position, uint regionSizeX, uint regionSizeY, float threshold = 32f)
        {
            return position.X < threshold || 
                   position.X > (regionSizeX - threshold) ||
                   position.Y < threshold || 
                   position.Y > (regionSizeY - threshold);
        }

        /// <summary>
        /// Clean up object tracking for objects that are no longer relevant
        /// </summary>
        public void CleanupObjectTracking()
        {
            if (!Client.Settings.MULTIPLE_SIMS) { return; }

            var connectedSims = new HashSet<Simulator>();
            foreach (var sim in Client.Network.Simulators)
            {
                if (sim.Connected)
                    connectedSims.Add(sim);
            }

            // Remove tracking for objects in disconnected simulators
            foreach (var kvp in _objectSimulators.ToArray())
            {
                var objectID = kvp.Key;
                var simList = kvp.Value;

                lock (simList)
                {
                    simList.RemoveAll(s => !connectedSims.Contains(s));
                    
                    if (simList.Count == 0)
                    {
                        _objectSimulators.TryRemove(objectID, out _);
                        Logger.Debug($"Removed tracking for object {objectID} (no connected sims)", Client);
                    }
                }
            }
        }

        #endregion Multi-Simulator Object Tracking

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

        // IDisposable support
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                // Dispose managed resources and unregister callbacks
                try
                {
                    // Stop interpolation service if active
                    try { _interpolationService?.Stop(); } catch { }
                    _interpolationService = null;

                    if (Client?.Network != null)
                    {
                        try { Client.Network.UnregisterCallback(PacketType.ObjectUpdate, ObjectUpdateHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.ImprovedTerseObjectUpdate, ImprovedTerseObjectUpdateHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.ObjectUpdateCompressed, ObjectUpdateCompressedHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.ObjectUpdateCached, ObjectUpdateCachedHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.KillObject, KillObjectHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.ObjectPropertiesFamily, ObjectPropertiesFamilyHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.ObjectProperties, ObjectPropertiesHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.PayPriceReply, PayPriceReplyHandler); } catch { }
                        try { Client.Network.UnregisterCallback(PacketType.ObjectAnimation, ObjectAnimationHandler); } catch { }
                        try { Client.Network.UnregisterEventCallback("ObjectPhysicsProperties", ObjectPhysicsPropertiesHandler); } catch { }
                    }

                    // Clean up multi-sim object tracking
                    try { _objectSimulators.Clear(); } catch { }
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception while disposing ObjectManager: " + ex.Message, ex, Client);
                }
            }

            disposed = true;
        }

        /// <summary>
        /// Public dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ObjectManager()
        {
            Dispose(false);
        }

        #region Internal event handlers

        private void Network_OnDisconnected(NetworkManager.DisconnectType reason, string message)
        {
            if (_interpolationService != null)
            {
                try { _interpolationService.Stop(); } catch { }
                _interpolationService = null;
                return;
            }
        }

        private void Network_OnConnected(object sender)
        {
            if (Client.Settings.USE_INTERPOLATION_TIMER)
            {
                // Use the extracted service to manage interpolation scheduling and lifecycle
                _interpolationService = new InterpolationService(Client);
                _interpolationService.Start();
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
        /// <param name="simulator">A reference to the <see cref="Simulator"/> object where the object resides</param>        
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
        [Obsolete("Use ClickObjectAsync(simulator, localID, CancellationToken) instead.")]
        public void ClickObject(Simulator simulator, uint localID)
         {
             // Preserve synchronous API by blocking on the async implementation
             ClickObjectAsync(simulator, localID).GetAwaiter().GetResult();
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
        [Obsolete("Use ClickObjectAsync(simulator, localID, uvCoord, stCoord, faceIndex, position, normal, binormal, CancellationToken) instead.")]
        public void ClickObject(Simulator simulator, uint localID, Vector3 uvCoord, Vector3 stCoord, int faceIndex, Vector3 position,
            Vector3 normal, Vector3 binormal)
         {
             // Preserve synchronous API by blocking on the async implementation
             ClickObjectAsync(simulator, localID, uvCoord, stCoord, faceIndex, position, normal, binormal).GetAwaiter().GetResult();
         }

        /// <summary>
        /// Async variant of ClickObject. Sends a grab packet, waits a short delay
        /// and then sends the de-grab packet. Uses Task.Delay instead of Thread.Sleep.
        /// </summary>
        public async Task ClickObjectAsync(Simulator simulator, uint localID, CancellationToken cancellationToken = default)
        {
            await ClickObjectAsync(simulator, localID, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, Vector3.Zero, Vector3.Zero, cancellationToken).ConfigureAwait(false);
        }
 
         /// <summary>
         /// Async variant of ClickObject. Sends a grab packet, waits a short delay
         /// and then sends the de-grab packet. Uses Task.Delay instead of Thread.Sleep.
         /// </summary>
        public async Task ClickObjectAsync(Simulator simulator, uint localID, Vector3 uvCoord, Vector3 stCoord, int faceIndex, Vector3 position,
            Vector3 normal, Vector3 binormal, CancellationToken cancellationToken = default)
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


            // Use async delay instead of blocking the thread; respect cancellation but always send degrab
            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested; continue to send degrab to avoid leaving the object grabbed
            }

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
                ObjectData = new ObjectShapePacket.ObjectDataBlock[1]
            };

            shape.ObjectData[0] = new ObjectShapePacket.ObjectDataBlock
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

            for (var i = 0; i < localIDs.Length; ++i)
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

            for (var i = 0; i < localIDs.Count; i++)
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
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>
        /// <param name="localID">The Local ID of the object</param>
        /// <param name="position">The new position of the object</param>
        /// <param name="childOnly">if true, a call to <see cref="DeselectObject"/> is
        /// made immediately following the request</param>
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
        /// /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>
        /// <param name="localID">The Local ID of the object</param>
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
        /// <param name="simulator">The <see cref="Simulator"/> the object is located</param>
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

            for (var i = 0; i < localIDs.Count; i++)
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

            for (var i = 0; i < localIDs.Count; i++)
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

            for (var i = 0; i < localIds.Count; i++)
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
        public void NavigateObjectMedia(UUID primID, int face, string newURL, Simulator sim, CancellationToken cancellationToken = default)
        {
            Uri cap;
            if ((cap = Client.Network.CurrentSim.Caps?.CapabilityURI("ObjectMediaNavigate")) == null)
            {
                Logger.Error("ObjectMediaNavigate capability not available", Client);
                return;
            }

            ObjectMediaNavigateMessage payload = new ObjectMediaNavigateMessage
            {
                PrimID = primID, URL = newURL, Face = face
            };

            Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(),
                cancellationToken, (response, data, error) =>
            {
                // If the operation was cancelled, ignore the response
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (error != null)
                {
                    Logger.Error($"ObjectMediaNavigate: {error.Message}", error, Client);
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
        public void UpdateObjectMedia(UUID primID, MediaEntry[] faceMedia, Simulator sim, CancellationToken cancellationToken = default)
        {
            Uri cap;
            if (sim.Caps == null || (cap = Client.Network.CurrentSim.Caps.CapabilityURI("ObjectMedia")) == null)
            {
                Logger.Error("ObjectMedia capability not available", Client);
                return;
            }

            ObjectMediaUpdate payload = new ObjectMediaUpdate {PrimID = primID, FaceMedia = faceMedia, Verb = "UPDATE"};

            Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(),
                cancellationToken, (response, data, error) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (error != null)
                {
                    Logger.Error($"ObjectMediaUpdate: {error.Message}", error, Client);
                }
            });

        }

        /// <summary>
        /// Retrieve information about object media
        /// </summary>
        /// <param name="primID">UUID of the primitive</param>
        /// <param name="sim">Simulator where prim is located</param>
        /// <param name="callback">Call this callback when done</param>
        public void RequestObjectMedia(UUID primID, Simulator sim, ObjectMediaCallback callback, CancellationToken cancellationToken = default)
        {
            Uri cap;
            if (sim.Caps != null && (cap = Client.Network.CurrentSim.Caps.CapabilityURI("ObjectMedia")) != null)
            {
                ObjectMediaRequest payload = new ObjectMediaRequest {PrimID = primID, Verb = "GET"};

                Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(),
                    cancellationToken, (httpResponse, data, error) =>
                    {
                        // If cancelled, invoke callback with failure and ignore response
                        if (cancellationToken.IsCancellationRequested)
                        {
                            try { callback(false, string.Empty, null); } catch (Exception ex) { Logger.Error(ex.Message, Client); }
                            return;
                        }
                         if (error != null)
                         {
                             Logger.Error("Failed retrieving ObjectMedia data", error, Client);
                             try { callback(false, string.Empty, null); }
                             catch (Exception ex) { Logger.Error(ex.Message, Client); }
                             return;
                         }

                         ObjectMediaMessage msg = new ObjectMediaMessage();
                         OSD result = OSDParser.Deserialize(data);
                         msg.Deserialize((OSDMap)result);

                         if (msg.Request is ObjectMediaResponse response)
                         {
                             if (Client.Settings.OBJECT_TRACKING)
                             {
                                 var kvp = sim.ObjectsPrimitives.FirstOrDefault(
                                     p => p.Value.ID == primID);
                                 if (kvp.Value != null)
                                 {
                                     Primitive prim = kvp.Value;
                                     if (prim != null)
                                     {
                                         prim.MediaVersion = response.Version;
                                         prim.FaceMedia = response.FaceMedia;
                                     }

                                     sim.ObjectsPrimitives.TryUpdate(kvp.Key, prim, kvp.Value);
                                 }
                             }

                             try { callback(true, response.Version, response.FaceMedia); }
                             catch (Exception ex) { Logger.Error(ex.Message, Client); }
                         }
                         else
                         {
                             try { callback(false, string.Empty, null); }
                             catch (Exception ex) { Logger.Error(ex.Message, Client); }
                         }
                     });
            }
            else
            {
                Logger.Error("ObjectMedia capability not available", Client);
                try { callback(false, string.Empty, null); }
                catch (Exception ex) { Logger.Error("RequestObjectMedia callback failed", ex, Client); }
            }
        }

        public async Task<IEnumerable<LegacyMaterial>> RequestMaterials(Simulator sim, CancellationToken cancellationToken = default)
        {
            if (sim == null) { return null; }

            if (sim.Caps == null)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            if (sim.Caps == null)
            {
                Logger.Info("Caps are down, unable to retrieve materials.", Client);
                return null;
            }

            var uri = sim.Caps.CapabilityURI("RenderMaterials");

            List<LegacyMaterial> matsToReturn = new List<LegacyMaterial>();

            Logger.Info($"Awaiting materials from {uri}", Client);

            await Client.HttpCapsClient.GetRequestAsync(uri, cancellationToken,
                   ((response, data, error) =>
                   {
                       if (error != null)
                       {
                           Logger.Error($"Failed fetching materials: {error}", Client);
                           return;
                       }

                       if (data == null || data.Length == 0)
                       {
                           Logger.Error("Failed fetching materials; result was empty.", Client);

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
                                       Logger.Info("Unexpected OSD return;\n" + OSDParser.SerializeJsonString(entry, true), Client);
                                   }
                               }
                           }
                           else
                           {
                               Logger.Info("Unexpected OSD return;\n" + OSDParser.SerializeJsonString(result, true), Client);
                           }

                           Logger.Info($"Fetched (x{matsToReturn.Count}) from {uri}", Client);
                       }
                       catch (Exception ex)
                       {
                           Logger.Error("Failed fetching RenderMaterials", ex, Client);

                           if (data.Length > 0)
                           {
                               Logger.Info("Response unparsable; " + System.Text.Encoding.UTF8.GetString(data), Client);
                           }
                       }
                   })).ConfigureAwait(false);

            return matsToReturn;
        }

        public async Task<IEnumerable<LegacyMaterial>> RequestMaterials(Simulator sim, IEnumerable<UUID> materials, CancellationToken cancellationToken = default)
        {
            if (sim == null) { return null; }

            if (sim.Caps == null)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            if (sim.Caps == null)
            {
                Logger.Info("Caps are down, unable to retrieve materials.", Client);
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

            Logger.Info($"Awaiting materials (x{array.Count}) from {uri}", Client);

            await Client.HttpCapsClient.PostRequestAsync(uri, OSDFormat.Xml, request, cancellationToken,
                       (response, data, error) =>
                       {
                           if (error != null)
                           {
                               Logger.Error("Failed fetching materials {error}", Client);
                               return;
                           }

                           if (data == null || data.Length == 0)
                           {
                               Logger.Error("Failed fetching materials; result was empty.", Client);

                               Logger.Info($"Sent:\n{uri}\n" +
                                           $"{Convert.ToBase64String(OSDParser.SerializeLLSDBinary(request), Base64FormattingOptions.InsertLineBreaks)}", Client);

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
                                           Logger.Info("Unexpected OSD return;\n" + OSDParser.SerializeJsonString(entry, true), Client);
                                       }
                                   }
                               }
                               else
                               {
                                   Logger.Info("Unexpected OSD return;\n" + OSDParser.SerializeJsonString(result, true), Client);
                               }

                               Logger.Info($"Fetched (x{matsToReturn.Count}) from {uri}", Client);
                           }
                           catch (Exception ex)
                           {
                               Logger.Error("Failed fetching RenderMaterials", ex, Client);

                               Logger.Info($"Sent:\n{uri}\n{System.Text.Encoding.UTF8.GetString(OSDParser.SerializeLLSDXmlBytes(request))}", Client);

                               Logger.Info("Requests: " + string.Join(",", materials.Select(m => m.ToString())));

                               if (data.Length > 0)
                               {
                                   Logger.Info("Unable to parse response; " + System.Text.Encoding.UTF8.GetString(data), Client);
                               }
                           }
                       }).ConfigureAwait(false);

            return matsToReturn;
        }
        #endregion



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
                ObjectData = new ObjectShapePacket.ObjectDataBlock[1]
            };

            shape.ObjectData[0] = new ObjectShapePacket.ObjectDataBlock
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
                if (simulator.ObjectsPrimitives.TryGetValue(localID, out var prim))
                {
                    return prim;
                }

                if (!createIfMissing) {return null;}
                
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

                prim = simulator.ObjectsPrimitives.GetOrAdd(localID, prim);

                simulator.GlobalToLocalID.AddOrUpdate(prim.ID, prim.LocalID, (uuid, u) => prim.LocalID);

                return prim;
            }

            return new Primitive();
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
            if (!Client.Settings.AVATAR_TRACKING)
            {
                return new Avatar();
            }

            return simulator.ObjectsAvatars.GetOrAdd(localID,
                new Avatar { LocalID = localID, ID = fullID, RegionHandle = simulator.Handle });
        }

        #endregion Object Tracking Link

        /// <summary>
        /// Perform a single interpolation pass. Separated so both Timer and PeriodicTimer
        /// paths can reuse the same logic without handling scheduling.
        /// </summary>
        private void PerformInterpolationPass()
        {
            int elapsed = 0;

            if (Client.Network.Connected)
            {
                int start = Environment.TickCount;

                int interval = unchecked(Environment.TickCount - Client.Self.lastInterpolation);
                float seconds = interval / 1000f;

                // Iterate through all simulators
                var sims = Client.Network.Simulators.ToArray();
                foreach (var sim in sims)
                {
                    float adjSeconds = seconds * sim.Stats.Dilation;

                    // Iterate through all of this region's avatars
                    foreach (var avatar in sim.ObjectsAvatars)
                    {
                        var av = avatar.Value;

                        Vector3 velocity, acceleration, position;
                        lock (av)
                        {
                            velocity = av.Velocity;
                            acceleration = av.Acceleration;
                            position = av.Position;
                        }

                        if (acceleration != Vector3.Zero)
                        {
                            velocity += acceleration * adjSeconds;
                        }

                        if (velocity != Vector3.Zero)
                        {
                            position += velocity * adjSeconds;
                        }

                        lock (av)
                        {
                            av.Velocity = velocity;
                            av.Position = position;
                        }
                    }

                    // Iterate through all the simulator's primitives
                    foreach (var prim in sim.ObjectsPrimitives)
                    {
                        var pv = prim.Value;

                        JointType joint;
                        Vector3 angVel, velocity, acceleration, position;
                        Quaternion rotation;

                        lock (pv)
                        {
                            joint = pv.Joint;
                            angVel = pv.AngularVelocity;
                            velocity = pv.Velocity;
                            acceleration = pv.Acceleration;
                            position = pv.Position;
                            rotation = pv.Rotation;
                        }

                        switch (joint)
                        {
                            case JointType.Invalid:
                            {
                                const float omegaThresholdSquared = 0.00001f;
                                float omegaSquared = angVel.LengthSquared();

                                if (omegaSquared > omegaThresholdSquared)
                                {
                                    float omega = (float)Math.Sqrt(omegaSquared);
                                    float angle = omega * adjSeconds;
                                    Vector3 normalizedAngVel = angVel * (1.0f / omega);
                                    Quaternion dQ = Quaternion.CreateFromAxisAngle(normalizedAngVel, angle);

                                    rotation *= dQ;
                                }

                                // Only do movement interpolation (extrapolation) when there is non-zero velocity
                                // but no acceleration
                                if (velocity != Vector3.Zero && acceleration == Vector3.Zero)
                                {
                                    position += (velocity + acceleration *
                                        (0.5f * (adjSeconds - HAVOK_TIMESTEP))) * adjSeconds;
                                    velocity += acceleration * adjSeconds;
                                }

                                lock (pv)
                                {
                                    pv.Position = position;
                                    pv.Velocity = velocity;
                                    pv.Rotation = rotation;
                                }

                                break;
                            }
                            case JointType.Hinge:
                                //FIXME: Hinge movement extrapolation
                                break;
                            case JointType.Point:
                                //FIXME: Point movement extrapolation
                                break;
                            default:
                                Logger.Warn($"Unhandled joint type {joint}", Client);
                                break;
                        }
                    }
                }

                // Make sure the last interpolated time is always updated
                Client.Self.lastInterpolation = Environment.TickCount;

                elapsed = Client.Self.lastInterpolation - start;
            }
        }
    }
}

