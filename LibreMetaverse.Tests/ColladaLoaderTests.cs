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
using System.IO;
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.Imaging;
using LibreMetaverse.ImportExport;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the .dae -&gt; List&lt;ModelPrim&gt; path that <see cref="InventoryManager.UploadModelAsync"/>
    /// documents as its entry point. A minimal single-triangle Collada document (one node, one
    /// geometry, one Lambert material referencing one image) is written to a temp directory
    /// alongside a real Targa-encoded texture, loaded through the actual XML deserializer, and fed
    /// straight into the mesh upload flow to prove the whole pipeline -- including the
    /// width/height capture added for the fee-quote texture_info fix -- works end to end from a
    /// real file, not just from hand-built ModelPrim objects.
    /// </summary>
    [TestFixture]
    public class ColladaLoaderTests
    {
        private const string DaeXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<COLLADA xmlns=""http://www.collada.org/2005/11/COLLADASchema"" version=""1.4.1"">
  <asset>
    <created>2026-01-01T00:00:00</created>
    <modified>2026-01-01T00:00:00</modified>
    <up_axis>Y_UP</up_axis>
  </asset>
  <library_images>
    <image id=""Image_tex0"">
      <init_from>tex0.tga</init_from>
    </image>
  </library_images>
  <library_effects>
    <effect id=""Effect0"">
      <profile_COMMON>
        <technique sid=""common"">
          <lambert>
            <diffuse>
              <!-- texcoord must equal the image id: ColladaLoader.ExtractMaterial reads
                   tex.texcoord, not tex.texture, as the image reference. This is not standard
                   COLLADA sampler2D indirection; match ColladaLoader's behavior, not the spec. -->
              <texture texture=""Image_tex0"" texcoord=""Image_tex0"" />
            </diffuse>
          </lambert>
        </technique>
      </profile_COMMON>
    </effect>
  </library_effects>
  <library_materials>
    <material id=""Mat0"">
      <instance_effect url=""#Effect0"" />
    </material>
  </library_materials>
  <library_geometries>
    <geometry id=""Mesh0"">
      <mesh>
        <source id=""Mesh0-positions"">
          <float_array id=""Mesh0-positions-array"" count=""9"">0 0 0 1 0 0 0 1 0</float_array>
        </source>
        <source id=""Mesh0-normals"">
          <float_array id=""Mesh0-normals-array"" count=""3"">0 0 1</float_array>
        </source>
        <source id=""Mesh0-uv"">
          <float_array id=""Mesh0-uv-array"" count=""6"">0 0 1 0 0 1</float_array>
        </source>
        <vertices id=""Mesh0-vertices"">
          <input semantic=""POSITION"" source=""#Mesh0-positions"" />
        </vertices>
        <triangles count=""1"" material=""Mat0Symbol"">
          <input semantic=""VERTEX"" offset=""0"" source=""#Mesh0-vertices"" />
          <input semantic=""NORMAL"" offset=""1"" source=""#Mesh0-normals"" />
          <input semantic=""TEXCOORD"" offset=""2"" source=""#Mesh0-uv"" />
          <p>0 0 0 1 0 1 2 0 2</p>
        </triangles>
      </mesh>
    </geometry>
  </library_geometries>
  <library_visual_scenes>
    <visual_scene id=""Scene"" name=""Scene"">
      <node id=""Prim0"" name=""Prim0"">
        <matrix>1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
        <instance_geometry url=""#Mesh0"">
          <bind_material>
            <technique_common>
              <instance_material symbol=""Mat0Symbol"" target=""#Mat0"" />
            </technique_common>
          </bind_material>
        </instance_geometry>
      </node>
    </visual_scene>
  </library_visual_scenes>
</COLLADA>";

        private string _tempDir = string.Empty;
        private string _daePath = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-collada-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
            _daePath = Path.Combine(_tempDir, "model.dae");
            File.WriteAllText(_daePath, DaeXml);

            // A real Targa file, round-tripped through the same encoder LibreMetaverse itself
            // ships (Targa.Encode / DecodeToManagedImage), so ColladaLoader decodes real TGA bytes
            // rather than a hand-crafted stand-in.
            var image = new ManagedImage(64, 32, ManagedImage.ImageChannels.Color);
            for (var i = 0; i < image.Red.Length; i++)
            {
                image.Red[i] = (byte)(i * 7);
                image.Green[i] = (byte)(i * 11);
                image.Blue[i] = (byte)(i * 13);
            }
            File.WriteAllBytes(Path.Combine(_tempDir, "tex0.tga"), Targa.Encode(image));
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Test]
        public void Load_MinimalTriangleWithTgaTexture_ProducesExpectedModelPrim()
        {
            var prims = new ColladaLoader().Load(_daePath, loadImages: true);

            Assert.That(prims, Has.Count.EqualTo(1));
            var prim = prims[0];

            Assert.That(prim.Faces, Has.Count.EqualTo(1));
            var material = prim.Faces[0].Material;

            Assert.Multiple(() =>
            {
                Assert.That(material.Texture, Is.EqualTo("tex0.tga"));
                Assert.That(material.TextureData, Is.Not.Empty, "LoadImage should have decoded and J2C-encoded the referenced texture");
                Assert.That(material.Width, Is.EqualTo(64));
                Assert.That(material.Height, Is.EqualTo(32));
                Assert.That(prim.Faces[0].Vertices, Has.Count.EqualTo(3), "one triangle == three vertices");
            });
        }

        [Test]
        public async Task Load_ThenUploadModelAsync_WiresRealTextureDimensionsIntoFeeQuote()
        {
            const string capUrl = "http://test.invalid/new-file-agent-inventory";
            const string uploaderUrl = "http://test.invalid/uploader/abc123";

            using var client = new FakeGridClient();
            client.AddCapability("NewFileAgentInventory", new Uri(capUrl));
            client.AddHttpResponse(new Uri(capUrl), HttpStatusCode.OK,
                "{\"state\":\"upload\",\"uploader\":\"" + uploaderUrl + "\",\"upload_price\":10}", "application/json");
            client.AddHttpResponse(new Uri(uploaderUrl), HttpStatusCode.OK,
                "{\"state\":\"complete\",\"new_inventory_item\":\"" + UUID.Random() + "\",\"new_asset\":\"" + UUID.Random() + "\"}",
                "application/json");

            var prims = new ColladaLoader().Load(_daePath, loadImages: true);
            Assert.That(prims, Has.Count.EqualTo(1), "fixture should still parse to exactly one prim");
            var realTextureBytes = prims[0].Faces[0].Material.TextureData;

            var result = await client.Inventory.UploadModelAsync(
                prims, "Collada Test Model", "loaded from a real .dae", UUID.Random(), Permissions.NoPermissions);
            Assert.That(result.Success, Is.True);

            var feeRequestBody = OSDParser.Deserialize(client.CapturedRequestBodies[0]) as OSDMap;
            var assetResources = feeRequestBody!["asset_resources"] as OSDMap;
            var textureInfo = assetResources!["texture_info"] as OSDArray;
            var uploadBody = OSDParser.Deserialize(client.CapturedRequestBodies[1]) as OSDMap;
            var uploadTextureList = uploadBody!["texture_list"] as OSDArray;

            Assert.Multiple(() =>
            {
                // Dimensions came from the real decoded TGA via ColladaLoader.LoadImage, not a stub.
                Assert.That(textureInfo, Has.Count.EqualTo(1));
                Assert.That(((OSDMap)textureInfo![0])["width"].AsInteger(), Is.EqualTo(64));
                Assert.That(((OSDMap)textureInfo[0])["height"].AsInteger(), Is.EqualTo(32));
                Assert.That(((OSDArray)assetResources["texture_list"])[0].AsBinary(), Is.EqualTo(Array.Empty<byte>()));

                // The final upload phase carries the real J2C bytes ColladaLoader produced.
                Assert.That(uploadTextureList, Has.Count.EqualTo(1));
                Assert.That(uploadTextureList![0].AsBinary(), Is.EqualTo(realTextureBytes));
            });
        }
    }
}
