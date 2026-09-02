/*
 * Copyright (c) 2026, Sjofn LLC
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
using LibreMetaverse.Assets;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    /// <summary>
    /// One region's most recently received GLTF material override state for a single object,
    /// keyed by the object's simulator-local ID. Mirrors LLGLTFOverrideCacheEntry in the reference
    /// viewer (llvocache.h). Stored on <see cref="Simulator.GLTFMaterialOverrides"/> regardless of
    /// whether the object itself is currently tracked, since an override message can arrive before
    /// the corresponding object update.
    /// </summary>
    public class GLTFMaterialOverrideCacheEntry
    {
        /// <summary>Simulator-local ID of the object this override applies to.</summary>
        public uint LocalID { get; }

        /// <summary>
        /// Full UUID of the object, if known at the time the override was received. Backfilled
        /// later if the object becomes known while this entry is still cached (mirrors the
        /// reference viewer backfilling a null object ID in LLViewerRegion::applyCacheMiscExtras).
        /// </summary>
        public UUID ObjectID { get; set; }

        /// <summary>Handle of the region this override was received from.</summary>
        public ulong RegionHandle { get; }

        /// <summary>
        /// The active override material for each overridden TextureEntry face, keyed by face index.
        /// A face with no entry here has no active override.
        /// </summary>
        public IReadOnlyDictionary<int, AssetMaterial> FaceOverrides { get; }

        public GLTFMaterialOverrideCacheEntry(uint localID, UUID objectID, ulong regionHandle,
            IReadOnlyDictionary<int, AssetMaterial> faceOverrides)
        {
            LocalID = localID;
            ObjectID = objectID;
            RegionHandle = regionHandle;
            FaceOverrides = faceOverrides ?? throw new ArgumentNullException(nameof(faceOverrides));
        }
    }

    /// <summary>Provides data for the <see cref="ObjectManager.GLTFMaterialOverrideReceived"/> event</summary>
    public class GLTFMaterialOverrideEventArgs : EventArgs
    {
        /// <summary>Get the simulator the override was received from</summary>
        public Simulator Simulator { get; }

        /// <summary>The cache entry describing the object's current override state</summary>
        public GLTFMaterialOverrideCacheEntry Override { get; }

        public GLTFMaterialOverrideEventArgs(Simulator simulator, GLTFMaterialOverrideCacheEntry over)
        {
            Simulator = simulator;
            Override = over;
        }
    }

    public partial class ObjectManager
    {
        #region GLTFMaterialOverrideReceived event
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<GLTFMaterialOverrideEventArgs>? m_GLTFMaterialOverrideReceived;

        ///<summary>Raises the GLTFMaterialOverrideReceived Event</summary>
        /// <param name="e">A GLTFMaterialOverrideEventArgs object containing the data sent from the simulator</param>
        protected virtual void OnGLTFMaterialOverrideReceived(GLTFMaterialOverrideEventArgs e)
        {
            EventHandler<GLTFMaterialOverrideEventArgs>? handler = m_GLTFMaterialOverrideReceived;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GLTFMaterialOverrideReceivedLock = new object();

        /// <summary>
        /// Raised when the simulator sends updated GLTF material override state for an object's
        /// faces (a <see cref="GenericStreamingMethod.GltfMaterialOverride"/> message). The same
        /// state is also applied directly to the affected <see cref="Primitive"/>'s
        /// <see cref="Primitive.TextureEntryFace.MaterialOverride"/> when the object is tracked, and
        /// cached on <see cref="Simulator.GLTFMaterialOverrides"/> regardless.
        /// </summary>
        public event EventHandler<GLTFMaterialOverrideEventArgs> GLTFMaterialOverrideReceived
        {
            add { lock (m_GLTFMaterialOverrideReceivedLock) { m_GLTFMaterialOverrideReceived += value; } }
            remove { lock (m_GLTFMaterialOverrideReceivedLock) { m_GLTFMaterialOverrideReceived -= value; } }
        }
        #endregion GLTFMaterialOverrideReceived event

        /// <summary>
        /// Handles a <see cref="NetworkManager.GenericStreamingMessage"/> event, applying
        /// <see cref="GenericStreamingMethod.GltfMaterialOverride"/> payloads to the per-region
        /// override cache and, when the object is already tracked, directly to its faces. Mirrors
        /// LLGLTFMaterialList::applyOverrideMessage in the reference viewer.
        /// </summary>
        private void GenericStreamingMessageHandler(object? sender, GenericStreamingMessageEventArgs e)
        {
            if (e.Method != GenericStreamingMethod.GltfMaterialOverride) { return; }

            OSD parsed;
            try
            {
                parsed = OSDParser.DeserializeLLSDNotation(Utils.BytesToString(e.Data));
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to parse GltfMaterialOverride payload", ex, Client);
                return;
            }

            if (!(parsed is OSDMap data)) { return; }

            var simulator = e.Simulator;
            var localId = (uint)data["id"].AsInteger();
            simulator.ObjectsPrimitives.TryGetValue(localId, out Primitive? prim);
            simulator.GLTFMaterialOverrides.TryGetValue(localId, out var previous);

            // An empty/absent "te" array means all overrides were removed from this object.
            if (!(data["te"] is OSDArray tes) || tes.Count == 0)
            {
                simulator.GLTFMaterialOverrides.TryRemove(localId, out _);
                if (previous != null && prim?.Textures != null)
                {
                    ClearFaceOverrides(prim.Textures, previous.FaceOverrides.Keys);
                }
                return;
            }

            var od = data["od"] as OSDArray;
            var faceOverrides = new Dictionary<int, AssetMaterial>();
            var count = Math.Min(tes.Count, Primitive.TextureEntry.MAX_FACES);
            for (var i = 0; i < count; i++)
            {
                var face = tes[i].AsInteger();
                if (face < 0 || face >= Primitive.TextureEntry.MAX_FACES) { continue; }

                var mat = (od != null && i < od.Count && od[i] is OSDMap overrideMap)
                    ? AssetMaterial.FromOverrideOsd(overrideMap)
                    : new AssetMaterial();
                faceOverrides[face] = mat;
            }

            var entry = new GLTFMaterialOverrideCacheEntry(localId, prim?.ID ?? UUID.Zero, simulator.Handle, faceOverrides);
            simulator.GLTFMaterialOverrides[localId] = entry;

            if (prim != null)
            {
                ApplyFaceOverrides(prim, entry);
                if (previous != null) { ClearFaceOverrides(prim.Textures!, previous.FaceOverrides.Keys, entry.FaceOverrides); }
            }

            OnGLTFMaterialOverrideReceived(new GLTFMaterialOverrideEventArgs(simulator, entry));
        }

        /// <summary>
        /// Re-applies any cached GLTF material override for <paramref name="prim"/> onto its
        /// current <see cref="Primitive.Textures"/>. Must be called after a packet handler
        /// (re)assigns <see cref="Primitive.Textures"/> to a freshly decoded <see cref="Primitive.TextureEntry"/>
        /// -- calling it any earlier (e.g. right after <see cref="GetPrimitive(Simulator, uint, UUID)"/>
        /// creates the object, before its TextureEntry is decoded from the wire) would apply the
        /// override onto a TextureEntry instance the handler is about to discard. Mirrors
        /// LLViewerRegion::applyCacheMiscExtras being invoked once an object's own state is in place.
        /// </summary>
        private static void ApplyCachedGLTFMaterialOverride(Simulator simulator, Primitive prim)
        {
            if (simulator.GLTFMaterialOverrides.TryGetValue(prim.LocalID, out var cachedOverride))
            {
                if (cachedOverride.ObjectID == UUID.Zero) { cachedOverride.ObjectID = prim.ID; }
                ApplyFaceOverrides(prim, cachedOverride);
            }
        }

        /// <summary>Applies a cache entry's per-face overrides onto a tracked primitive's textures.</summary>
        private static void ApplyFaceOverrides(Primitive prim, GLTFMaterialOverrideCacheEntry entry)
        {
            prim.Textures ??= new Primitive.TextureEntry(UUID.Zero);
            foreach (var kv in entry.FaceOverrides)
            {
                prim.Textures.CreateFace((uint)kv.Key).MaterialOverride = kv.Value;
            }
        }

        /// <summary>
        /// Clears <see cref="Primitive.TextureEntryFace.MaterialOverride"/> on faces present in
        /// <paramref name="previousFaces"/> but not in <paramref name="currentOverrides"/> (or on
        /// all of <paramref name="previousFaces"/>, when <paramref name="currentOverrides"/> is
        /// omitted). Only touches faces that already have a materialized
        /// <see cref="Primitive.TextureEntryFace"/> instance, to avoid instantiating one via the
        /// DefaultTexture fallback in <see cref="Primitive.TextureEntry.GetFace"/>.
        /// </summary>
        private static void ClearFaceOverrides(Primitive.TextureEntry textures, IEnumerable<int> previousFaces,
            IReadOnlyDictionary<int, AssetMaterial>? currentOverrides = null)
        {
            foreach (var face in previousFaces)
            {
                if (currentOverrides != null && currentOverrides.ContainsKey(face)) { continue; }
                if (face < 0 || face >= Primitive.TextureEntry.MAX_FACES) { continue; }

                var faceTex = textures.FaceTextures[face];
                if (faceTex != null) { faceTex.MaterialOverride = null; }
            }
        }
    }
}
