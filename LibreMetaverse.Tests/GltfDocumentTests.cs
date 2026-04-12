/*
 * Copyright (c) 2024-2026, Sjofn LLC
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
using System.Text;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Assets.Gltf;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class GltfDocumentTests : Assert
    {
        // Minimal glTF JSON with one mesh, two VEC3-FLOAT positions, and two UNSIGNED_SHORT indices
        private const string MinimalGltfJson = @"{
  ""asset"": { ""version"": ""2.0"", ""generator"": ""Test"" },
  ""scene"": 0,
  ""scenes"": [{ ""name"": ""Scene"", ""nodes"": [0] }],
  ""nodes"": [{ ""name"": ""Mesh"", ""mesh"": 0 }],
  ""meshes"": [{
    ""name"": ""Triangle"",
    ""primitives"": [{
      ""attributes"": { ""POSITION"": 0 },
      ""indices"": 1,
      ""material"": 0,
      ""mode"": 4
    }]
  }],
  ""materials"": [{
    ""name"": ""Mat"",
    ""pbrMetallicRoughness"": {
      ""baseColorFactor"": [1.0, 0.5, 0.0, 1.0],
      ""metallicFactor"": 0.0,
      ""roughnessFactor"": 1.0
    },
    ""alphaMode"": ""OPAQUE"",
    ""alphaCutoff"": 0.5,
    ""doubleSided"": false
  }],
  ""accessors"": [
    {
      ""bufferView"": 0,
      ""byteOffset"": 0,
      ""componentType"": 5126,
      ""count"": 3,
      ""type"": ""VEC3"",
      ""max"": [1.0, 1.0, 0.0],
      ""min"": [0.0, 0.0, 0.0]
    },
    {
      ""bufferView"": 1,
      ""byteOffset"": 0,
      ""componentType"": 5123,
      ""count"": 3,
      ""type"": ""SCALAR""
    }
  ],
  ""bufferViews"": [
    { ""buffer"": 0, ""byteOffset"": 0,  ""byteLength"": 36, ""target"": 34962 },
    { ""buffer"": 0, ""byteOffset"": 36, ""byteLength"": 6,  ""target"": 34963 }
  ],
  ""buffers"": [{
    ""byteLength"": 44,
    ""uri"": ""data:application/octet-stream;base64,AAAAAAAAAAAAAD8AAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA""
  }]
}";

        // 3 positions: (0,0,0) (1,0,0) (0,1,0) packed as 9 little-endian floats = 36 bytes
        // 3 indices:   0, 1, 2 packed as 3 unsigned shorts = 6 bytes  → total 42 bytes, padded to 44
        // Re-encode the correct buffer below in SetUp.
        private static byte[] BuildTriangleBuffer()
        {
            using var ms = new MemoryStream(44);
            using var bw = new BinaryWriter(ms);
            // POSITION accessor: 3 × VEC3 float
            bw.Write(0f); bw.Write(0f); bw.Write(0f);
            bw.Write(1f); bw.Write(0f); bw.Write(0f);
            bw.Write(0f); bw.Write(1f); bw.Write(0f);
            // INDEX accessor: 3 × ushort
            bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)2);
            // Pad to 44
            bw.Write((ushort)0);
            return ms.ToArray();
        }

        private static string BuildJsonWithEmbeddedBuffer()
        {
            var buf = BuildTriangleBuffer();
            var b64 = Convert.ToBase64String(buf);
            return MinimalGltfJson.Replace(
                @"""uri"": ""data:application/octet-stream;base64,AAAAAAAAAAAAAD8AAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA""",
                $@"""uri"": ""data:application/octet-stream;base64,{b64}""");
        }

        // ── Parse tests ────────────────────────────────────────────────────────

        [Test]
        public void LoadGltf_ParsesSceneGraph()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());

            Assert.That(doc.Version, Is.EqualTo("2.0"));
            Assert.That(doc.Generator, Is.EqualTo("Test"));
            Assert.That(doc.DefaultScene, Is.EqualTo(0));
            Assert.That(doc.Scenes.Count, Is.EqualTo(1));
            Assert.That(doc.Scenes[0].Nodes.Count, Is.EqualTo(1));
            Assert.That(doc.Nodes.Count, Is.EqualTo(1));
            Assert.That(doc.Nodes[0].Mesh, Is.EqualTo(0));
        }

        [Test]
        public void LoadGltf_ParsesMeshAndPrimitive()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());

            Assert.That(doc.Meshes.Count, Is.EqualTo(1));
            Assert.That(doc.Meshes[0].Name, Is.EqualTo("Triangle"));
            Assert.That(doc.Meshes[0].Primitives.Count, Is.EqualTo(1));

            var prim = doc.Meshes[0].Primitives[0];
            Assert.That(prim.Attributes.ContainsKey("POSITION"), Is.True);
            Assert.That(prim.Indices, Is.EqualTo(1));
            Assert.That(prim.Material, Is.EqualTo(0));
            Assert.That(prim.Mode, Is.EqualTo(GltfPrimitiveMode.Triangles));
        }

        [Test]
        public void LoadGltf_ParsesMaterial()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());

            Assert.That(doc.Materials.Count, Is.EqualTo(1));
            var mat = doc.Materials[0];
            Assert.That(mat.Name, Is.EqualTo("Mat"));
            Assert.That(mat.BaseColorFactor.R, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mat.BaseColorFactor.G, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(mat.MetallicFactor, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(mat.RoughnessFactor, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mat.AlphaMode, Is.EqualTo(GltfAlphaMode.Opaque));
        }

        // ── Geometry decode tests ──────────────────────────────────────────────

        [Test]
        public void GetPositions_ReturnsCorrectTriangle()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            var prim = doc.Meshes[0].Primitives[0];
            var positions = doc.GetPositions(prim);

            Assert.That(positions.Length, Is.EqualTo(3));
            Assert.That(positions[0], Is.EqualTo(Vector3.Zero));
            Assert.That(positions[1].X, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(positions[1].Y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(positions[2].X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(positions[2].Y, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void GetIndices_ReturnsCorrectValues()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            var prim = doc.Meshes[0].Primitives[0];
            var indices = doc.GetIndices(prim);

            Assert.That(indices.Length, Is.EqualTo(3));
            Assert.That(indices[0], Is.EqualTo(0u));
            Assert.That(indices[1], Is.EqualTo(1u));
            Assert.That(indices[2], Is.EqualTo(2u));
        }

        [Test]
        public void GetPositions_EmptyWhenNoAttribute()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            var emptyPrim = new GltfPrimitive();
            Assert.That(doc.GetPositions(emptyPrim).Length, Is.EqualTo(0));
        }

        [Test]
        public void GetNormals_EmptyWhenNoAttribute()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            var prim = doc.Meshes[0].Primitives[0];
            Assert.That(doc.GetNormals(prim).Length, Is.EqualTo(0));
        }

        // ── Round-trip tests ───────────────────────────────────────────────────

        [Test]
        public void ToJson_RoundTrip_PreservesSceneGraph()
        {
            var original = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            var json = original.ToJson();
            var roundTripped = GltfDocument.LoadGltf(json);

            Assert.That(roundTripped.Scenes.Count,   Is.EqualTo(original.Scenes.Count));
            Assert.That(roundTripped.Nodes.Count,    Is.EqualTo(original.Nodes.Count));
            Assert.That(roundTripped.Meshes.Count,   Is.EqualTo(original.Meshes.Count));
            Assert.That(roundTripped.Materials.Count, Is.EqualTo(original.Materials.Count));
            Assert.That(roundTripped.Materials[0].BaseColorFactor.R,
                Is.EqualTo(original.Materials[0].BaseColorFactor.R).Within(1e-4f));
        }

        [Test]
        public void ToGlb_RoundTrip_PreservesGeometry()
        {
            var original = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            byte[] glb = original.ToGlb();

            // Verify GLB magic
            Assert.That(glb[0], Is.EqualTo(0x67)); // 'g'
            Assert.That(glb[1], Is.EqualTo(0x6C)); // 'l'
            Assert.That(glb[2], Is.EqualTo(0x54)); // 'T'
            Assert.That(glb[3], Is.EqualTo(0x46)); // 'F'

            var roundTripped = GltfDocument.Load(glb);
            var prim = roundTripped.Meshes[0].Primitives[0];
            var positions = roundTripped.GetPositions(prim);

            Assert.That(positions.Length, Is.EqualTo(3));
            Assert.That(positions[1].X, Is.EqualTo(1f).Within(1e-5f));
        }

        // ── GLB auto-detect test ───────────────────────────────────────────────

        [Test]
        public void Load_AutoDetectsGlbVsJson()
        {
            var original = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            byte[] glbBytes  = original.ToGlb();
            byte[] jsonBytes = Encoding.UTF8.GetBytes(original.ToJson());

            var fromGlb  = GltfDocument.Load(glbBytes);
            var fromJson = GltfDocument.Load(jsonBytes);

            Assert.That(fromGlb.Meshes.Count,  Is.EqualTo(1));
            Assert.That(fromJson.Meshes.Count, Is.EqualTo(1));
        }

        // ── Node transform tests ───────────────────────────────────────────────

        [Test]
        public void ParseNode_TrsDefaults()
        {
            var doc = GltfDocument.LoadGltf(BuildJsonWithEmbeddedBuffer());
            var node = doc.Nodes[0];

            Assert.That(node.Matrix.HasValue, Is.False);
            Assert.That(node.Translation, Is.EqualTo(Vector3.Zero));
            Assert.That(node.Rotation, Is.EqualTo(Quaternion.Identity));
            Assert.That(node.Scale, Is.EqualTo(Vector3.One));
        }

        [Test]
        public void ParseNode_ExplicitMatrix()
        {
            const string json = @"{
  ""asset"": { ""version"": ""2.0"" },
  ""nodes"": [{
    ""matrix"": [1,0,0,0, 0,1,0,0, 0,0,1,0, 5,6,7,1]
  }]
}";
            var doc = GltfDocument.LoadGltf(json);
            var node = doc.Nodes[0];

            Assert.That(node.Matrix.HasValue, Is.True);
            // M14 is translation X in a column-major affine matrix
            Assert.That(node.Matrix!.Value.M14, Is.EqualTo(5f).Within(1e-5f));
            Assert.That(node.Matrix!.Value.M24, Is.EqualTo(6f).Within(1e-5f));
            Assert.That(node.Matrix!.Value.M34, Is.EqualTo(7f).Within(1e-5f));
        }
    }
}
