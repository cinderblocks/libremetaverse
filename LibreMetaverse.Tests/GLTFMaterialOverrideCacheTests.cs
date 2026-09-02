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

using System.Reflection;
using System.Text;
using LibreMetaverse.Assets;
using LibreMetaverse.Packets;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the GLTF material override cache fed by
    /// <see cref="GenericStreamingMethod.GltfMaterialOverride"/> messages. Verified against the
    /// reference viewer (LLGLTFMaterialList::applyOverrideMessage in llgltfmateriallist.cpp): the
    /// LLSD notation payload is {"id": local_id, "te": [face...], "od": [override...]} with "od"
    /// entries parsed via the same compact override shape as ModifyRegion's terrain overrides
    /// (LLGLTFMaterial::applyOverrideLLSD); an empty/absent "te" array means all overrides were
    /// removed. The cache lives on <see cref="Simulator.GLTFMaterialOverrides"/>, keyed by local ID,
    /// and is authoritative even when the object itself isn't tracked yet (mirrors
    /// LLViewerRegion::cacheFullUpdateGLTFOverride/applyCacheMiscExtras).
    /// </summary>
    [TestFixture]
    public class GLTFMaterialOverrideCacheTests
    {
        private FakeGridClient _client;
        private Simulator _sim;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("Placeholder", new System.Uri("http://test.invalid/placeholder"));
            _sim = _client.Network.CurrentSim;
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        private static void RaiseOverrideMessage(GridClient client, Simulator sim, OSDMap payload)
        {
            var text = OSDParser.SerializeLLSDNotation(payload);
            var args = new GenericStreamingMessageEventArgs(sim, GenericStreamingMethod.GltfMaterialOverride,
                Encoding.UTF8.GetBytes(text));

            var method = typeof(NetworkManager).GetMethod("OnGenericStreamingMessage",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(client.Network, new object[] { args });
        }

        private static OSDMap BuildPayload(uint localId, params (int face, OSDMap? over)[] entries)
        {
            var data = new OSDMap { ["id"] = OSD.FromInteger(localId) };
            var te = new OSDArray();
            var od = new OSDArray();
            foreach (var (face, over) in entries)
            {
                te.Add(OSD.FromInteger(face));
                od.Add(over ?? new OSDMap());
            }
            data["te"] = te;
            data["od"] = od;
            return data;
        }

        private static OSDMap SimpleOverride(Color4 baseColor)
        {
            var mat = new AssetMaterial();
            mat.SetBaseColorFactor(baseColor, forOverride: true);
            return mat.ToOverrideOsd();
        }

        [Test]
        public void GenericStreamingMessage_TrackedObject_AppliesOverrideToFace()
        {
            var localId = 1001u;
            var objectId = UUID.Random();
            var prim = _client.Objects.GetPrimitive(_sim, localId, objectId, true);

            var color = new Color4(0.25f, 0.5f, 0.75f, 1f);
            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (2, SimpleOverride(color))));

            var face = prim.Textures!.GetFace(2);
            Assert.That(face.MaterialOverride, Is.Not.Null);
            Assert.That(face.MaterialOverride!.BaseColorFactor, Is.EqualTo(color));
        }

        [Test]
        public void GenericStreamingMessage_TrackedObject_CachesEntryOnSimulator()
        {
            var localId = 1002u;
            var prim = _client.Objects.GetPrimitive(_sim, localId, UUID.Random(), true);

            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (0, SimpleOverride(Color4.White))));

            Assert.That(_sim.GLTFMaterialOverrides.TryGetValue(localId, out var entry), Is.True);
            Assert.That(entry!.ObjectID, Is.EqualTo(prim.ID));
            Assert.That(entry.FaceOverrides.ContainsKey(0), Is.True);
        }

        [Test]
        public void GenericStreamingMessage_ObjectNotYetTracked_GetPrimitiveDoesNotPrematurelyApplyOverride()
        {
            var localId = 1008u;
            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (0, SimpleOverride(Color4.White))));

            var prim = _client.Objects.GetPrimitive(_sim, localId, UUID.Random(), true);

            // GetPrimitive must not manufacture a TextureEntry to hold the cached override --
            // packet handlers assign a freshly decoded TextureEntry to Primitive.Textures right
            // after calling GetPrimitive, which would silently discard an override applied here.
            Assert.That(prim.Textures, Is.Null);
        }

        [Test]
        public void GenericStreamingMessage_ObjectNotYetTracked_CachesAndAppliesOncePacketHandlerAssignsTextures()
        {
            var localId = 1003u;
            var objectId = UUID.Random();
            var color = new Color4(0.1f, 0.2f, 0.3f, 1f);

            // Override arrives before the object is known -- must still be cached.
            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (1, SimpleOverride(color))));

            Assert.That(_sim.ObjectsPrimitives.ContainsKey(localId), Is.False);
            Assert.That(_sim.GLTFMaterialOverrides.TryGetValue(localId, out var cached), Is.True);
            Assert.That(cached!.ObjectID, Is.EqualTo(UUID.Zero), "object ID unknown until the object shows up");

            // Object shows up afterward: GetPrimitive creates the tracked object, then (mirroring
            // ObjectUpdateHandler/ObjectUpdateCompressedHandler) the wire TextureEntry is decoded
            // and assigned to Textures -- only after that point can the cached override be
            // (re)applied without it being discarded by that assignment.
            var prim = _client.Objects.GetPrimitive(_sim, localId, objectId, true);
            prim.Textures = new Primitive.TextureEntry(UUID.Random());

            var applyMethod = typeof(ObjectManager).GetMethod("ApplyCachedGLTFMaterialOverride",
                BindingFlags.NonPublic | BindingFlags.Static);
            applyMethod!.Invoke(null, new object[] { _sim, prim });

            var face = prim.Textures!.GetFace(1);
            Assert.That(face.MaterialOverride, Is.Not.Null);
            Assert.That(face.MaterialOverride!.BaseColorFactor, Is.EqualTo(color));
            Assert.That(_sim.GLTFMaterialOverrides[localId].ObjectID, Is.EqualTo(objectId));
        }

        [Test]
        public void KillObject_RemovesCachedOverride_SoAReusedLocalIDStartsClean()
        {
            var localId = 1009u;
            _client.Objects.GetPrimitive(_sim, localId, UUID.Random(), true);
            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (0, SimpleOverride(Color4.White))));
            Assert.That(_sim.GLTFMaterialOverrides.ContainsKey(localId), Is.True);

            var kill = new KillObjectPacket
            {
                ObjectData = new[] { new KillObjectPacket.ObjectDataBlock { ID = localId } }
            };
            var method = typeof(ObjectManager).GetMethod("KillObjectHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(_client.Objects, new object[]
            {
                null!, new PacketReceivedEventArgs(kill, _sim)
            });

            Assert.That(_sim.GLTFMaterialOverrides.ContainsKey(localId), Is.False,
                "a stale override must not survive to be handed to whatever object reuses this local ID");
        }

        [Test]
        public void KillObject_RemovesCachedOverride_ForAnObjectThatWasNeverTracked()
        {
            var localId = 1010u;

            // The override arrives and the object is killed before any ObjectUpdate for it ever
            // lands -- ObjectsPrimitives never has an entry for this local ID at all.
            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (0, SimpleOverride(Color4.White))));
            Assert.That(_sim.ObjectsPrimitives.ContainsKey(localId), Is.False);
            Assert.That(_sim.GLTFMaterialOverrides.ContainsKey(localId), Is.True);

            var kill = new KillObjectPacket
            {
                ObjectData = new[] { new KillObjectPacket.ObjectDataBlock { ID = localId } }
            };
            var method = typeof(ObjectManager).GetMethod("KillObjectHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(_client.Objects, new object[]
            {
                null!, new PacketReceivedEventArgs(kill, _sim)
            });

            Assert.That(_sim.GLTFMaterialOverrides.ContainsKey(localId), Is.False,
                "eviction must not depend on the object having been tracked in ObjectsPrimitives");
        }

        [Test]
        public void GenericStreamingMessage_EmptyTeArray_ClearsCacheAndFaceOverride()
        {
            var localId = 1004u;
            var prim = _client.Objects.GetPrimitive(_sim, localId, UUID.Random(), true);

            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (0, SimpleOverride(Color4.White))));
            Assert.That(prim.Textures!.GetFace(0).MaterialOverride, Is.Not.Null);

            // Empty "te" array means every override was removed from this object.
            RaiseOverrideMessage(_client, _sim, new OSDMap { ["id"] = OSD.FromInteger(localId), ["te"] = new OSDArray() });

            Assert.That(_sim.GLTFMaterialOverrides.ContainsKey(localId), Is.False);
            Assert.That(prim.Textures!.GetFace(0).MaterialOverride, Is.Null);
        }

        [Test]
        public void GenericStreamingMessage_FaceDroppedFromUpdate_ClearsThatFaceOnly()
        {
            var localId = 1005u;
            var prim = _client.Objects.GetPrimitive(_sim, localId, UUID.Random(), true);

            RaiseOverrideMessage(_client, _sim,
                BuildPayload(localId, (0, SimpleOverride(Color4.White)), (1, SimpleOverride(Color4.Black))));

            Assert.That(prim.Textures!.GetFace(0).MaterialOverride, Is.Not.Null);
            Assert.That(prim.Textures!.GetFace(1).MaterialOverride, Is.Not.Null);

            // Second update only carries face 0 -- face 1's override should be cleared.
            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (0, SimpleOverride(Color4.Black))));

            Assert.That(prim.Textures!.GetFace(0).MaterialOverride, Is.Not.Null);
            Assert.That(prim.Textures!.GetFace(1).MaterialOverride, Is.Null);
        }

        [Test]
        public void GenericStreamingMessage_RaisesGLTFMaterialOverrideReceivedEvent()
        {
            var localId = 1006u;
            _client.Objects.GetPrimitive(_sim, localId, UUID.Random(), true);

            GLTFMaterialOverrideEventArgs? received = null;
            _client.Objects.GLTFMaterialOverrideReceived += (s, e) => received = e;

            RaiseOverrideMessage(_client, _sim, BuildPayload(localId, (0, SimpleOverride(Color4.White))));

            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Override.LocalID, Is.EqualTo(localId));
        }

        [Test]
        public void GenericStreamingMessage_MalformedPayload_DoesNotThrow()
        {
            var args = new GenericStreamingMessageEventArgs(_sim, GenericStreamingMethod.GltfMaterialOverride,
                Encoding.UTF8.GetBytes("not valid llsd notation {{{"));

            var method = typeof(NetworkManager).GetMethod("OnGenericStreamingMessage",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.DoesNotThrow(() => method!.Invoke(_client.Network, new object[] { args }));
        }

        [Test]
        public void GenericStreamingMessage_UnrelatedMethod_Ignored()
        {
            var localId = 1007u;
            _client.Objects.GetPrimitive(_sim, localId, UUID.Random(), true);

            var args = new GenericStreamingMessageEventArgs(_sim, GenericStreamingMethod.Unknown,
                Encoding.UTF8.GetBytes(OSDParser.SerializeLLSDNotation(
                    BuildPayload(localId, (0, SimpleOverride(Color4.White))))));

            var method = typeof(NetworkManager).GetMethod("OnGenericStreamingMessage",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(_client.Network, new object[] { args });

            Assert.That(_sim.GLTFMaterialOverrides.ContainsKey(localId), Is.False);
        }
    }
}
