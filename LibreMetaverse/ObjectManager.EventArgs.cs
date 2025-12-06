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

using OpenMetaverse.Packets;
using System;
using System.Collections.Generic;

namespace OpenMetaverse
{
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


    public class ParticleUpdateEventArgs : EventArgs
    {
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
        public ParticleUpdateEventArgs(Simulator simulator, Primitive.ParticleSystem particlesystem, Primitive source)
        {
            this.Simulator = simulator;
            this.ParticleSystem = particlesystem;
            this.Source = source;
        }
    }

    /// <summary>Provides additional primitive data for the <see cref="ObjectManager.ObjectProperties"/> event</summary>
    /// <remarks><para>The <see cref="ObjectManager.ObjectProperties"/> event occurs when the simulator sends
    /// <see cref="ObjectPropertiesPacket"/> containing additional details for a Primitive, Foliage or Attachment data</para>
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
    /// <seealso cref="ObjectManager.ObjectUpdate"/>
    /// <seealso cref="ObjectManager.AvatarUpdate"/>
    /// <seealso cref="AvatarUpdateEventArgs"/>
    /// <seealso cref="PrimEventArgs"/>
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
        /// Construct a new instance of the ObjectPropertiesUpdatedEventArgs class
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

    /// <summary>Provides primitive data containing updated location, velocity, rotation, textures for the
    /// <see cref="ObjectManager.TerseObjectUpdate"/> event</summary>
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
}
