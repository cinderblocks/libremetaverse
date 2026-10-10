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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoreJ2K;
using LibreMetaverse.Assets;
using LibreMetaverse.Assets.Gltf;
using LibreMetaverse.Imaging;
using LibreMetaverse.ImportExport;
using NUnit.Framework;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Physically based materials read from glTF. The reference viewer's material import is the model for what the
    /// textures become: no alpha, occlusion in the red channel of the metallic-roughness map (white if there is none), and
    /// the normal map without loss. The images here are small and flat-colored so what came out can be read back.
    /// </summary>
    [TestFixture]
    public class GltfPbrMaterialTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-pbr-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        #region Builders

        private static ManagedImage Flat(int size, byte r, byte g, byte b, byte? a = null)
        {
            var image = new ManagedImage(size, size, a.HasValue
                ? ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha
                : ManagedImage.ImageChannels.Color);
            for (int i = 0; i < size * size; i++)
            {
                image.Red[i] = r; image.Green[i] = g; image.Blue[i] = b;
                if (a.HasValue) image.Alpha[i] = a.Value;
            }
            return image;
        }

        /// <summary>A glTF with one triangle and one material, and the images it can name</summary>
        private sealed class Scene
        {
            public readonly GltfDocument Doc = new GltfDocument();
            public readonly GltfDocumentMaterial Material = new GltfDocumentMaterial { Name = "Paint" };

            public Scene()
            {
                var primitive = new GltfPrimitive();
                primitive.Attributes[GltfPrimitive.ATTR_POSITION] = Accessor(new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 });
                primitive.Material = 0;
                var mesh = new GltfMesh { Name = "Thing" };
                mesh.Primitives.Add(primitive);
                Doc.Meshes.Add(mesh);
                Doc.Materials.Add(Material);
                Doc.Nodes.Add(new GltfNode { Name = "Node", Mesh = 0 });
                var scene = new GltfScene();
                scene.Nodes.Add(0);
                Doc.Scenes.Add(scene);
                Doc.DefaultScene = 0;
            }

            private int View(byte[] data)
            {
                if (Doc.Buffers.Count == 0) Doc.Buffers.Add(new GltfBuffer { Data = Array.Empty<byte>() });
                var buffer = Doc.Buffers[0];
                var old = buffer.Data ?? Array.Empty<byte>();
                int offset = (old.Length + 3) & ~3;
                var grown = new byte[offset + data.Length];
                Buffer.BlockCopy(old, 0, grown, 0, old.Length);
                Buffer.BlockCopy(data, 0, grown, offset, data.Length);
                buffer.Data = grown;
                buffer.ByteLength = grown.Length;
                Doc.BufferViews.Add(new GltfBufferView { Buffer = 0, ByteOffset = offset, ByteLength = data.Length });
                return Doc.BufferViews.Count - 1;
            }

            private int Accessor(float[] values)
            {
                Doc.Accessors.Add(new GltfAccessor
                {
                    BufferView = View(values.SelectMany(BitConverter.GetBytes).ToArray()),
                    ComponentType = GltfComponentType.Float,
                    Type = GltfAccessorType.Vec3,
                    Count = values.Length / 3
                });
                return Doc.Accessors.Count - 1;
            }

            /// <summary>An image held in the file, as a texture. Returns the texture's index.</summary>
            public int Embed(ManagedImage image)
            {
                Doc.Images.Add(new GltfImage { BufferView = View(Targa.Encode(image)), MimeType = "image/x-tga" });
                Doc.Textures.Add(new GltfTexture { Source = Doc.Images.Count - 1 });
                return Doc.Textures.Count - 1;
            }

            /// <summary>An image in a file beside the model, as a texture</summary>
            public int Link(string uri)
            {
                Doc.Images.Add(new GltfImage { Uri = uri });
                Doc.Textures.Add(new GltfTexture { Source = Doc.Images.Count - 1 });
                return Doc.Textures.Count - 1;
            }
        }

        private string Write(Scene scene, string name = "model.glb")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, scene.Doc.ToGlb());
            return path;
        }

        private static ModelPbrMaterial Pbr(ModelLoadResult result)
        {
            Assert.That(result.Prims, Has.Count.EqualTo(1));
            var pbr = result.Prims[0].Faces[0].Material.Pbr;
            Assert.That(pbr, Is.Not.Null);
            return pbr!;
        }

        private static ManagedImage Read(ModelTexture texture)
        {
            Assert.That(texture.Data, Is.Not.Empty);
            return J2kImage.DecodeToImage<ManagedImage>(texture.Data);
        }

        #endregion Builders

        #region Reading

        [Test]
        public void Load_GivesTheMaterialsFactorsAsTheFileHasThem()
        {
            var scene = new Scene();
            scene.Material.BaseColorFactor = new Color4(0.2f, 0.4f, 0.6f, 0.5f);
            scene.Material.MetallicFactor = 0.25f;
            scene.Material.RoughnessFactor = 0.75f;
            scene.Material.EmissiveFactor = new Vector3(2f, 0.5f, 0f); // not clamped
            scene.Material.AlphaMode = GltfAlphaMode.Mask;
            scene.Material.AlphaCutoff = 0.3f;
            scene.Material.DoubleSided = true;

            var pbr = Pbr(new GltfLoader().LoadWithDiagnostics(Write(scene), false));

            Assert.Multiple(() =>
            {
                Assert.That(pbr.Name, Is.EqualTo("Paint"));
                Assert.That(pbr.BaseColorFactor, Is.EqualTo(new Color4(0.2f, 0.4f, 0.6f, 0.5f)));
                Assert.That(pbr.MetallicFactor, Is.EqualTo(0.25f));
                Assert.That(pbr.RoughnessFactor, Is.EqualTo(0.75f));
                Assert.That(pbr.EmissiveFactor, Is.EqualTo(new Vector3(2f, 0.5f, 0f)));
                Assert.That(pbr.AlphaMode, Is.EqualTo(GltfAlphaMode.Mask));
                Assert.That(pbr.AlphaCutoff, Is.EqualTo(0.3f));
                Assert.That(pbr.DoubleSided, Is.True, "the model says so, whatever the conversion does");
            });
        }

        [Test]
        public void Load_MaterialWithNothingSet_HasTheGltfDefaults()
        {
            var pbr = Pbr(new GltfLoader().LoadWithDiagnostics(Write(new Scene()), false));

            Assert.Multiple(() =>
            {
                Assert.That(pbr.MetallicFactor, Is.EqualTo(1f));
                Assert.That(pbr.RoughnessFactor, Is.EqualTo(1f));
                Assert.That(pbr.AlphaMode, Is.EqualTo(GltfAlphaMode.Opaque));
                Assert.That(pbr.BaseColorTexture, Is.Null);
            });
        }

        [Test]
        public void Load_WithTheOptionOff_NamesTheTexturesButLoadsNone_AndSaysNothing()
        {
            var scene = new Scene();
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = scene.Embed(Flat(64, 128, 128, 255)) };
            scene.Material.EmissiveTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 1, 2, 3)), TexCoord = 1 };
            scene.Material.MetallicRoughnessTexture = new GltfTextureRef { Index = 99 }; // not there

            var result = new GltfLoader().LoadWithDiagnostics(Write(scene), loadImages: true);

            var pbr = Pbr(result);
            Assert.Multiple(() =>
            {
                Assert.That(pbr.NormalTexture!.Name, Is.EqualTo("gltf-image-0"));
                Assert.That(pbr.NormalTexture.Data, Is.Empty);
                Assert.That(pbr.EmissiveTexture!.TexCoord, Is.EqualTo(1));
                Assert.That(pbr.MetallicRoughnessTexture, Is.Null);
                Assert.That(result.Diagnostics, Is.Empty, "as before the option existed");
            });
        }

        [Test]
        public void Load_BaseColorIsTheOneTheMeshUses_NotMadeTwice()
        {
            var scene = new Scene();
            scene.Material.BaseColorTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 200, 100, 50, 255)) };

            var material = new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true).Prims[0].Faces[0].Material;

            Assert.That(material.TextureData, Is.Not.Empty);
            Assert.That(material.Pbr!.BaseColorTexture!.Data, Is.SameAs(material.TextureData));
            Assert.That(material.Pbr.BaseColorTexture.Width, Is.EqualTo(64));
        }

        #endregion Reading

        #region The textures, as the viewer prepares them

        [Test]
        public void Load_NormalMap_IsKeptWithoutLossAndWithoutAlpha()
        {
            var normal = new ManagedImage(16, 16, ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha);
            for (int i = 0; i < 256; i++)
            {
                normal.Red[i] = (byte)(i % 256 * 3); normal.Green[i] = (byte)(255 - i % 256 * 2); normal.Blue[i] = (byte)(i % 256 * 7 + 11); normal.Alpha[i] = 40;
            }
            var scene = new Scene();
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = scene.Embed(normal) };

            var pbr = Pbr(new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true));

            var back = Read(pbr.NormalTexture!);
            Assert.Multiple(() =>
            {
                // This library's J2C encoder always writes an alpha component, and a texture with none gets an opaque one
                Assert.That(back.Alpha, Has.All.EqualTo((byte)255), "the 40 it had is gone");
                Assert.That(back.Red, Is.EqualTo(normal.Red), "lossless");
                Assert.That(back.Green, Is.EqualTo(normal.Green));
                Assert.That(back.Blue, Is.EqualTo(normal.Blue));
            });
        }

        [Test]
        public void Load_EmissiveMap_LosesItsAlpha()
        {
            var scene = new Scene();
            scene.Material.EmissiveTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 10, 200, 30, 90)) };

            var pbr = Pbr(new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true));

            var back = Read(pbr.EmissiveTexture!);
            Assert.That(back.Alpha[0], Is.InRange(250, 255), "the 90 it had is gone");
            Assert.That(back.Green[0], Is.InRange(190, 210));
        }

        [Test]
        public void Load_MetallicRoughnessWithNoOcclusion_GetsAWhiteRedChannel()
        {
            // glTF ignores the red channel of this map, and files often have zero in it. In a Second Life material it is
            // occlusion, and zero would make the surface black.
            var scene = new Scene();
            scene.Material.MetallicRoughnessTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 0, 120, 60)) };

            var back = Read(Pbr(new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true)).MetallicRoughnessTexture!);

            Assert.Multiple(() =>
            {
                Assert.That(back.Red[0], Is.InRange(248, 255), "occlusion: none");
                Assert.That(back.Green[0], Is.InRange(112, 128), "roughness is kept");
                Assert.That(back.Blue[0], Is.InRange(52, 68), "metalness is kept");
            });
        }

        [Test]
        public void Load_MetallicRoughnessAndOcclusionInOneImage_IsLeftAlone()
        {
            var scene = new Scene();
            int orm = scene.Embed(Flat(64, 77, 120, 60));
            scene.Material.MetallicRoughnessTexture = new GltfTextureRef { Index = orm };
            scene.Material.OcclusionTexture = new GltfOcclusionTextureRef { Index = orm };

            var back = Read(Pbr(new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true)).MetallicRoughnessTexture!);

            Assert.That(back.Red[0], Is.InRange(70, 84), "the occlusion that was there");
        }

        [Test]
        public void Load_OcclusionInAnImageOfItsOwn_IsScaledAndPackedIntoTheRedChannel()
        {
            var scene = new Scene();
            scene.Material.MetallicRoughnessTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 0, 120, 60)) };
            scene.Material.OcclusionTexture = new GltfOcclusionTextureRef { Index = scene.Embed(Flat(32, 100, 9, 9)) };

            var back = Read(Pbr(new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true)).MetallicRoughnessTexture!);

            Assert.Multiple(() =>
            {
                Assert.That(back.Width, Is.EqualTo(64), "the size of the metallic-roughness map");
                Assert.That(back.Red[0], Is.InRange(94, 106), "occlusion");
                Assert.That(back.Green[0], Is.InRange(112, 128));
                Assert.That(back.Blue[0], Is.InRange(52, 68));
            });
        }

        [Test]
        public void Load_OcclusionWithNoMetallicRoughness_MakesAWhiteMapWithTheOcclusionInRed()
        {
            var scene = new Scene();
            scene.Material.OcclusionTexture = new GltfOcclusionTextureRef { Index = scene.Embed(Flat(64, 100, 9, 9)) };

            var back = Read(Pbr(new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true)).MetallicRoughnessTexture!);

            Assert.Multiple(() =>
            {
                Assert.That(back.Red[0], Is.InRange(94, 106));
                Assert.That(back.Green[0], Is.InRange(248, 255));
                Assert.That(back.Blue[0], Is.InRange(248, 255));
            });
        }

        private sealed class CountingCodec : ITextureCodec
        {
            public int Decodes;

            public ManagedImage Decode(Stream stream)
            {
                Decodes++;
                return Flat(64, 50, 60, 70);
            }
        }

        [Test]
        public void Load_OneImageUsedByManySlotsAndMaterials_IsDecodedOnce()
        {
            var scene = new Scene();
            scene.Doc.Images.Add(new GltfImage { Uri = "data:image/png;base64,AAAA" });
            scene.Doc.Textures.Add(new GltfTexture { Source = 0 });
            scene.Material.BaseColorTexture = new GltfTextureRef { Index = 0 };
            scene.Material.EmissiveTexture = new GltfTextureRef { Index = 0 };
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = 0 };
            scene.Material.MetallicRoughnessTexture = new GltfTextureRef { Index = 0 };
            var second = new GltfDocumentMaterial { Name = "Second", EmissiveTexture = new GltfTextureRef { Index = 0 } };
            scene.Doc.Materials.Add(second);
            scene.Doc.Meshes[0].Primitives.Add(new GltfPrimitive { Material = 1, Attributes = { [GltfPrimitive.ATTR_POSITION] = scene.Doc.Meshes[0].Primitives[0].Attributes[GltfPrimitive.ATTR_POSITION] } });
            var codec = new CountingCodec();

            var result = new GltfLoader(codec) { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true);

            Assert.That(result.Diagnostics, Is.Empty);
            // once for the base color, which the mesh uses, and once for all the rest
            Assert.That(codec.Decodes, Is.EqualTo(2));
        }

        #endregion The textures, as the viewer prepares them

        #region Untrusted files

        [Test]
        public void Load_PbrTextureOutsideTheModelsDirectory_IsNotLoaded()
        {
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(_tempDir)!, "outside-" + Path.GetFileName(_tempDir) + ".tga"), Targa.Encode(Flat(64, 1, 2, 3)));
            var scene = new Scene();
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = scene.Link("../outside-" + Path.GetFileName(_tempDir) + ".tga") };

            var result = new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true);

            Assert.That(result.Prims, Has.Count.EqualTo(1), "the mesh is not lost");
            Assert.That(Pbr(result).NormalTexture!.Data, Is.Empty);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.TextureOutsideModelDirectory));
            File.Delete(Path.Combine(Path.GetDirectoryName(_tempDir)!, "outside-" + Path.GetFileName(_tempDir) + ".tga"));
        }

        [Test]
        public void Load_PbrTextureThatIsNotThere_CostsOnlyTheTexture()
        {
            var scene = new Scene();
            scene.Material.EmissiveTexture = new GltfTextureRef { Index = scene.Link("nothing.tga") };
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = 99 };

            var result = new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Diagnostics.Select(d => d.Code), Is.All.EqualTo(ModelDiagnosticCode.TextureNotLoaded));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(2), "the missing file and the missing texture");
        }

        #endregion Untrusted files

        #region What a Second Life material cannot hold

        [Test]
        public void Load_TypicalMaterial_SaysNothing()
        {
            var scene = new Scene();
            scene.Material.BaseColorTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 1, 2, 3, 255)) };
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = scene.Embed(Flat(64, 128, 128, 255)) };
            scene.Material.MetallicRoughnessTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 0, 100, 100)) };
            scene.Material.EmissiveTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 9, 9, 9)) };
            scene.Doc.ExtensionsUsed.Add("KHR_texture_transform");

            var result = new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true);

            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void Load_SecondSetOfTextureCoordinates_IsApproximated()
        {
            var scene = new Scene();
            scene.Material.EmissiveTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 9, 9, 9)), TexCoord = 1 };

            var d = new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true).Diagnostics.Single();

            Assert.That(d.Code, Is.EqualTo(ModelDiagnosticCode.PbrApproximated));
            Assert.That(d.Subject, Is.EqualTo("Paint"));
        }

        [Test]
        public void Load_NormalScaleAndOcclusionStrength_AreApproximated()
        {
            var scene = new Scene();
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = scene.Embed(Flat(64, 128, 128, 255)), Scale = 2f };
            scene.Material.OcclusionTexture = new GltfOcclusionTextureRef { Index = scene.Embed(Flat(64, 100, 0, 0)), Strength = 0.5f };

            var result = new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true);

            Assert.That(Pbr(result).NormalScale, Is.EqualTo(2f));
            Assert.That(Pbr(result).OcclusionStrength, Is.EqualTo(0.5f));
            var d = result.Diagnostics.Single();
            Assert.That(d.Code, Is.EqualTo(ModelDiagnosticCode.PbrApproximated));
            Assert.That(d.Count, Is.EqualTo(2));
        }

        [Test]
        public void Load_MaterialExtensionsItCannotRead_AreApproximated()
        {
            var scene = new Scene();
            scene.Doc.ExtensionsUsed.Add("KHR_materials_emissive_strength");
            scene.Doc.ExtensionsUsed.Add("KHR_materials_clearcoat");

            var d = new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true).Diagnostics.Single();

            Assert.That(d.Code, Is.EqualTo(ModelDiagnosticCode.PbrApproximated));
            Assert.That(d.Count, Is.EqualTo(2));
            Assert.That(new GltfLoader().LoadWithDiagnostics(Write(scene), true).Diagnostics, Is.Empty, "with the option off, as before");
        }

        #endregion What a Second Life material cannot hold

        #region The material asset

        [Test]
        public void ToAssetMaterial_RoundTripsThroughTheAssetFormat()
        {
            var scene = new Scene();
            scene.Material.BaseColorFactor = new Color4(0.25f, 0.5f, 0.75f, 0.5f);
            scene.Material.MetallicFactor = 0.25f;
            scene.Material.RoughnessFactor = 0.75f;
            scene.Material.EmissiveFactor = new Vector3(0.5f, 0.25f, 0.125f);
            scene.Material.AlphaMode = GltfAlphaMode.Blend;
            scene.Material.DoubleSided = true;
            scene.Material.BaseColorTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 1, 2, 3)) };
            scene.Material.NormalTexture = new GltfNormalTextureRef { Index = scene.Embed(Flat(64, 128, 128, 255)) };
            scene.Material.MetallicRoughnessTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 0, 9, 9)) };
            var pbr = Pbr(new GltfLoader { LoadPbrTextures = true }.LoadWithDiagnostics(Write(scene), true));
            var ids = new Dictionary<ModelTexture, UUID>();
            UUID IdOf(ModelTexture t) => ids.TryGetValue(t, out var id) ? id : ids[t] = UUID.Random();

            var asset = pbr.ToAssetMaterial(IdOf);
            asset.Encode();
            var back = new AssetMaterial(UUID.Random(), asset.AssetData);

            Assert.Multiple(() =>
            {
                Assert.That(back.BaseColorFactor.R, Is.EqualTo(0.25f).Within(1e-4f));
                Assert.That(back.BaseColorFactor.A, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(back.MetallicFactor, Is.EqualTo(0.25f).Within(1e-4f));
                Assert.That(back.RoughnessFactor, Is.EqualTo(0.75f).Within(1e-4f));
                Assert.That(back.EmissiveFactor.Y, Is.EqualTo(0.25f).Within(1e-4f));
                Assert.That(back.AlphaMode, Is.EqualTo(GltfAlphaMode.Blend));
                Assert.That(back.DoubleSided, Is.False, "off, as the viewer's import does, whatever the model says");
                Assert.That(back.TextureIds[AssetMaterial.TEXTURE_BASE_COLOR], Is.EqualTo(ids[pbr.BaseColorTexture!]));
                Assert.That(back.TextureIds[AssetMaterial.TEXTURE_NORMAL], Is.EqualTo(ids[pbr.NormalTexture!]));
                Assert.That(back.TextureIds[AssetMaterial.TEXTURE_METALLIC_ROUGHNESS], Is.EqualTo(ids[pbr.MetallicRoughnessTexture!]));
                Assert.That(back.TextureIds[AssetMaterial.TEXTURE_EMISSIVE], Is.EqualTo(UUID.Zero), "no emissive map");
                Assert.That(ids, Has.Count.EqualTo(3), "asked once for each texture it has");
            });
        }

        [Test]
        public void ToAssetMaterial_TextureTransforms_AreOnlyCarriedWhenAsked()
        {
            var transform = new GltfTextureTransform { Offset = new Vector2(0.25f, 0.5f), Scale = new Vector2(2f, 3f), Rotation = 0.5f };
            var scene = new Scene();
            scene.Material.BaseColorTexture = new GltfTextureRef { Index = scene.Embed(Flat(64, 1, 2, 3)), Transform = transform };
            var pbr = Pbr(new GltfLoader().LoadWithDiagnostics(Write(scene), false));
            Assert.That(pbr.BaseColorTexture!.Transform.Equals(transform), Is.True, "read as the model has it");

            var viewerWay = pbr.ToAssetMaterial(_ => UUID.Random());
            var carried = pbr.ToAssetMaterial(_ => UUID.Random(), carryTextureTransforms: true);
            carried.Encode();
            var back = new AssetMaterial(UUID.Random(), carried.AssetData);

            Assert.That(viewerWay.TextureTransforms[AssetMaterial.TEXTURE_BASE_COLOR].IsDefault, Is.True);
            Assert.That(back.TextureTransforms[AssetMaterial.TEXTURE_BASE_COLOR].Offset.X, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(back.TextureTransforms[AssetMaterial.TEXTURE_BASE_COLOR].Scale.Y, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(back.TextureTransforms[AssetMaterial.TEXTURE_BASE_COLOR].Rotation, Is.EqualTo(0.5f).Within(1e-4f));
        }

        #endregion The material asset
    }
}
