using System;
using System.IO;
using System.Text;
using LibreMetaverse.Assets;
using LibreMetaverse.Imaging;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.Rendering;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Mesh, texture and compressed LLSD decoding must refuse inputs whose headers claim
    /// more than the data supports instead of allocating what the header asks for.
    /// All inputs here are synthetic.
    /// </summary>
    [TestFixture]
    public class UntrustedAssetDecodeTests
    {
        private int savedMaxParts;
        private int savedMaxPart;
        private long savedMaxAsset;
        private int savedMaxElements;
        private int savedMaxFaces;
        private int savedMaxVertices;
        private int savedMaxTriangles;

        [SetUp]
        public void SaveLimits()
        {
            savedMaxParts = AssetMesh.MaxParts;
            savedMaxPart = AssetMesh.MaxInflatedPartBytes;
            savedMaxAsset = AssetMesh.MaxInflatedAssetBytes;
            savedMaxElements = AssetMesh.MaxDecodedElements;
            savedMaxFaces = FacetedMesh.MaxFaces;
            savedMaxVertices = FacetedMesh.MaxVerticesPerFace;
            savedMaxTriangles = FacetedMesh.MaxTrianglesPerFace;
        }

        [TearDown]
        public void RestoreLimits()
        {
            AssetMesh.MaxParts = savedMaxParts;
            AssetMesh.MaxInflatedPartBytes = savedMaxPart;
            AssetMesh.MaxInflatedAssetBytes = savedMaxAsset;
            AssetMesh.MaxDecodedElements = savedMaxElements;
            FacetedMesh.MaxFaces = savedMaxFaces;
            FacetedMesh.MaxVerticesPerFace = savedMaxVertices;
            FacetedMesh.MaxTrianglesPerFace = savedMaxTriangles;
        }

        #region helpers

        private static byte[] Be32(int value) =>
            new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

        /// <summary>Binary LLSD (no header) of a binary blob that is valid, so a refusal can only be the size limit</summary>
        private static OSD ZerosBlob(int size) => new OSDMap { ["blob"] = OSD.FromBinary(new byte[size]) };

        private static byte[] MeshAsset(OSDMap header, params byte[][] parts)
        {
            using (var ms = new MemoryStream())
            {
                byte[] headerBytes = OSDParser.SerializeLLSDBinary(header, true);
                ms.Write(headerBytes, 0, headerBytes.Length);
                foreach (var part in parts)
                    ms.Write(part, 0, part.Length);
                return ms.ToArray();
            }
        }

        /// <summary>Binary LLSD (no header) of an array of n undefined values: one byte each in, a whole object each out</summary>
        private static byte[] UndefArray(int n)
        {
            var raw = new byte[1 + 4 + n + 1];
            raw[0] = (byte)'[';
            Array.Copy(Be32(n), 0, raw, 1, 4);
            for (int i = 0; i < n; i++) raw[5 + i] = (byte)'!';
            raw[raw.Length - 1] = (byte)']';
            return raw;
        }

        private static byte[] ZCompress(byte[] raw)
        {
            using (var ms = new MemoryStream())
            using (var z = new ComponentAce.Compression.Libs.zlib.ZOutputStream(ms, 9))
            {
                z.Write(raw, 0, raw.Length);
                z.finish();
                return ms.ToArray();
            }
        }

        private static OSDMap PartInfo(int offset, int size) =>
            new OSDMap { ["offset"] = offset, ["size"] = size };

        private static bool TryDecodeMesh(byte[] asset, out AssetMesh mesh)
        {
            mesh = new AssetMesh(UUID.Random(), asset);
            return mesh.Decode();
        }

        /// <summary>A bare JPEG 2000 codestream header: SOC, SIZ, COD, SOT</summary>
        private static byte[] J2kHeader(uint xsiz = 64, uint ysiz = 64, uint tile = 64, int components = 3,
            int bitDepth = 8, int levels = 5, uint xOsiz = 0)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                void U16(int v) { w.Write((byte)(v >> 8)); w.Write((byte)v); }
                void U32(uint v) { U16((int)(v >> 16)); U16((int)(v & 0xFFFF)); }

                U16(0xFF4F);
                U16(0xFF51);
                U16(38 + 3 * components);
                U16(0);
                U32(xsiz); U32(ysiz); U32(xOsiz); U32(0); U32(tile); U32(tile); U32(0); U32(0);
                U16(components);
                for (int i = 0; i < components; i++) { w.Write((byte)(bitDepth - 1)); w.Write((byte)1); w.Write((byte)1); }

                U16(0xFF52);
                U16(12);
                w.Write((byte)0);       // Scod
                w.Write((byte)0);       // progression order
                U16(1);                 // layers
                w.Write((byte)0);       // MCT
                w.Write((byte)levels);  // decomposition levels
                w.Write((byte)4); w.Write((byte)4); w.Write((byte)0); w.Write((byte)0);

                U16(0xFF90);
                return ms.ToArray();
            }
        }

        #endregion

        #region binary LLSD

        [Test]
        public void BinaryLLSD_HugeDeclaredBinaryLength_IsRefused()
        {
            // 'b' with a length of Int32.MaxValue followed by almost nothing
            var data = new byte[] { (byte)'b', 0x7F, 0xFF, 0xFF, 0xFF, 1, 2, 3 };
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDBinary(data));
        }

        [Test]
        public void BinaryLLSD_NegativeLength_IsRefused()
        {
            var data = new byte[] { (byte)'s', 0xFF, 0xFF, 0xFF, 0xFF };
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDBinary(data));
        }

        [Test]
        public void BinaryLLSD_DeeplyNestedArrays_AreRefused()
        {
            using (var ms = new MemoryStream())
            {
                for (int i = 0; i < 100000; i++)
                {
                    ms.WriteByte((byte)'[');
                    ms.Write(Be32(1), 0, 4);
                }
                Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDBinary(ms.ToArray()));
            }
        }

        [Test]
        public void BinaryLLSD_ReasonableNesting_StillParses()
        {
            OSD osd = OSD.FromString("leaf");
            for (int i = 0; i < 20; i++)
                osd = new OSDArray { osd };

            var parsed = OSDParser.DeserializeLLSDBinary(OSDParser.SerializeLLSDBinary(osd));
            for (int i = 0; i < 20; i++)
                parsed = ((OSDArray)parsed)[0];
            Assert.That(parsed.AsString(), Is.EqualTo("leaf"));
        }

        [Test]
        public void BinaryLLSD_BinaryBlob_RoundTrips()
        {
            var blob = new byte[100000];
            new Random(1).NextBytes(blob);
            var parsed = OSDParser.DeserializeLLSDBinary(OSDParser.SerializeLLSDBinary(OSD.FromBinary(blob)));
            Assert.That(parsed.AsBinary(), Is.EqualTo(blob));
        }

        #endregion

        #region compressed LLSD

        [Test]
        public void ZDecompressOSD_RoundTrips()
        {
            var osd = new OSDMap { ["a"] = 1, ["b"] = "two" };
            var result = (OSDMap)Helpers.ZDecompressOSD(Helpers.ZCompressOSD(osd));
            Assert.That(result["a"].AsInteger(), Is.EqualTo(1));
            Assert.That(result["b"].AsString(), Is.EqualTo("two"));
        }

        [Test]
        public void DecompressOSD_RoundTrips()
        {
            var osd = new OSDMap { ["a"] = 1, ["b"] = "two" };
            var result = (OSDMap)Helpers.DecompressOSD(Helpers.ZCompressOSD(osd));
            Assert.That(result["a"].AsInteger(), Is.EqualTo(1));
            Assert.That(result["b"].AsString(), Is.EqualTo("two"));
        }

        [Test]
        public void DecompressOSD_InflatingPastTheCap_IsRefused()
        {
            byte[] bomb = Helpers.ZCompressOSD(ZerosBlob(Helpers.MaxInflatedOSDBytes + 1024 * 1024));

            Assert.That(bomb.Length, Is.LessThan(100 * 1024), "test input should be small");
            Assert.Throws<InvalidDataException>(() => Helpers.DecompressOSD(bomb));
        }

        [Test]
        public void DecompressOSD_JustUnderTheCap_Succeeds()
        {
            byte[] data = Helpers.ZCompressOSD(ZerosBlob(1024 * 1024));

            var result = (OSDMap)Helpers.DecompressOSD(data, 2 * 1024 * 1024, out int inflated);

            Assert.That(inflated, Is.GreaterThan(1024 * 1024));
            Assert.That(result["blob"].AsBinary().Length, Is.EqualTo(1024 * 1024));
        }

        [Test]
        public void ZDecompressOSD_InflatingPastTheCap_IsRefused()
        {
            byte[] bomb = Helpers.ZCompressOSD(ZerosBlob(Helpers.MaxInflatedOSDBytes + 1024 * 1024));

            Assert.That(bomb.Length, Is.LessThan(100 * 1024), "test input should be small");
            Assert.Throws<InvalidDataException>(() => Helpers.ZDecompressOSD(bomb));
        }

        [Test]
        public void ZDecompressOSD_JustUnderTheCap_Succeeds()
        {
            byte[] data = Helpers.ZCompressOSD(ZerosBlob(1024 * 1024));

            var result = (OSDMap)Helpers.ZDecompressOSD(data, 2 * 1024 * 1024);

            Assert.That(result["blob"].AsBinary().Length, Is.EqualTo(1024 * 1024));
        }

        [Test]
        public void RenderMaterialsMessage_Bomb_YieldsEmptyMap()
        {
            byte[] bomb = Helpers.ZCompressOSD(ZerosBlob(Helpers.MaxInflatedOSDBytes + 1024 * 1024));
            var message = new RenderMaterialsMessage();

            message.Deserialize(new OSDMap { ["Zipped"] = OSD.FromBinary(bomb) });

            Assert.That(message.MaterialData, Is.InstanceOf<OSDMap>());
            Assert.That(((OSDMap)message.MaterialData).Count, Is.EqualTo(0));
        }

        #endregion

        [Test]
        public void BinaryLLSD_TooManyValues_AreRefused()
        {
            int saved = OSDParser.MaxBinaryElements;
            try
            {
                OSDParser.MaxBinaryElements = 1000;
                Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDBinary(UndefArray(5000)));
                Assert.That(((OSDArray)OSDParser.DeserializeLLSDBinary(UndefArray(500))).Count, Is.EqualTo(500));
            }
            finally { OSDParser.MaxBinaryElements = saved; }
        }

        [Test]
        public void BinaryLLSD_ElementBudget_IsSharedAcrossDocuments()
        {
            int budget = 1000;
            using (var ms = new MemoryStream(UndefArray(600)))
                OSDParser.DeserializeLLSDBinary(ms, ref budget);
            Assert.That(budget, Is.InRange(300, 400));

            using (var ms = new MemoryStream(UndefArray(600)))
            {
                var stream = ms;
                Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDBinary(stream, ref budget));
            }
        }

        #region mesh

        [Test]
        public void Mesh_ManyTinyValuesWithinTheByteCap_AreRefused()
        {
            // Within the byte caps, but every inflated byte would become an object of its own
            byte[] part = ZCompress(UndefArray(AssetMesh.MaxDecodedElements * 2 / 5));
            Assert.That(part.Length, Is.LessThan(100 * 1024));

            var two = new OSDMap
            {
                ["high_lod"] = PartInfo(0, part.Length),
                ["medium_lod"] = PartInfo(part.Length, part.Length)
            };
            Assert.That(TryDecodeMesh(MeshAsset(two, part, part), out _), Is.True, "two parts are within budget");

            var three = new OSDMap
            {
                ["high_lod"] = PartInfo(0, part.Length),
                ["medium_lod"] = PartInfo(part.Length, part.Length),
                ["low_lod"] = PartInfo(part.Length * 2, part.Length)
            };
            Assert.That(TryDecodeMesh(MeshAsset(three, part, part, part), out _), Is.False, "three are not");
        }

        [Test]
        public void Mesh_NormalAsset_Decodes()
        {
            var lod = new OSDArray { new OSDMap { ["NoGeometry"] = true } };
            byte[] part = Helpers.ZCompressOSD(lod);
            var header = new OSDMap
            {
                ["version"] = 1,
                ["high_lod"] = PartInfo(0, part.Length),
                ["skin"] = PartInfo(0, 0)
            };

            Assert.That(TryDecodeMesh(MeshAsset(header, part), out var mesh), Is.True);
            Assert.That(mesh.MeshData["version"].AsInteger(), Is.EqualTo(1));
            var decoded = (OSDArray)mesh.MeshData["high_lod"];
            Assert.That(decoded.Count, Is.EqualTo(1));
            Assert.That(((OSDMap)decoded[0])["NoGeometry"].AsBoolean(), Is.True);
        }

        [Test]
        public void Mesh_DeclaredSizeLargerThanAsset_IsRefusedWithoutAllocating()
        {
            var header = new OSDMap { ["high_lod"] = PartInfo(0, int.MaxValue) };
            Assert.That(TryDecodeMesh(MeshAsset(header, new byte[16]), out _), Is.False);
        }

        [Test]
        public void Mesh_NegativeSize_IsRefused()
        {
            var header = new OSDMap { ["high_lod"] = PartInfo(0, -1) };
            Assert.That(TryDecodeMesh(MeshAsset(header, new byte[16]), out _), Is.False);
        }

        [Test]
        public void Mesh_OffsetPastEndOfAsset_IsRefused()
        {
            var header = new OSDMap { ["high_lod"] = PartInfo(int.MaxValue, 16) };
            Assert.That(TryDecodeMesh(MeshAsset(header, new byte[16]), out _), Is.False);
        }

        [Test]
        public void Mesh_TooManyParts_IsRefused()
        {
            var header = new OSDMap();
            for (int i = 0; i <= AssetMesh.MaxParts; i++)
                header["part" + i] = PartInfo(0, 0);
            Assert.That(TryDecodeMesh(MeshAsset(header), out _), Is.False);

            header.Remove("part0");
            Assert.That(TryDecodeMesh(MeshAsset(header), out _), Is.True);
        }

        [Test]
        public void Mesh_PartInflatingPastTheCap_IsRefused()
        {
            byte[] bomb = Helpers.ZCompressOSD(ZerosBlob(AssetMesh.MaxInflatedPartBytes + 1024 * 1024));
            var header = new OSDMap { ["high_lod"] = PartInfo(0, bomb.Length) };

            Assert.That(bomb.Length, Is.LessThan(100 * 1024));
            Assert.That(TryDecodeMesh(MeshAsset(header, bomb), out _), Is.False);
        }

        [Test]
        public void Mesh_PartsInflatingPastTheAssetCap_AreRefused()
        {
            AssetMesh.MaxInflatedPartBytes = 1024 * 1024;
            AssetMesh.MaxInflatedAssetBytes = 1024 * 1024;

            // each part is within the per-part cap, together they are not
            byte[] part = Helpers.ZCompressOSD(ZerosBlob(700 * 1024));
            var header = new OSDMap { ["high_lod"] = PartInfo(0, part.Length) };
            Assert.That(TryDecodeMesh(MeshAsset(header, part), out _), Is.True);

            header = new OSDMap
            {
                ["high_lod"] = PartInfo(0, part.Length),
                ["medium_lod"] = PartInfo(part.Length, part.Length)
            };
            Assert.That(TryDecodeMesh(MeshAsset(header, part, part), out _), Is.False);
        }

        [Test]
        public void Mesh_HugeLengthInHeader_IsRefused()
        {
            // a 'b' element in the header declaring 2 GB, in an asset a few bytes long
            var asset = new byte[] { (byte)'{', 0, 0, 0, 1, (byte)'k', 0, 0, 0, 1, (byte)'x', (byte)'b', 0x7F, 0xFF, 0xFF, 0xFF };
            Assert.That(TryDecodeMesh(asset, out _), Is.False);
        }

        #endregion

        #region decoded geometry

        private static byte[] U16s(params ushort[] values)
        {
            var bytes = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                bytes[i * 2] = (byte)(values[i] & 0xFF);
                bytes[i * 2 + 1] = (byte)(values[i] >> 8);
            }
            return bytes;
        }

        private static OSDMap SubMesh(int vertices = 3, ushort[] triangle = null, byte[] position = null, OSDMap positionDomain = null)
        {
            var map = new OSDMap
            {
                ["Position"] = OSD.FromBinary(position ?? new byte[vertices * 6]),
                ["TriangleList"] = OSD.FromBinary(U16s(triangle ?? new ushort[] { 0, 1, 2 }))
            };
            if (positionDomain != null) map["PositionDomain"] = positionDomain;
            return map;
        }

        private static OSDMap Domain(float min, float max) => new OSDMap
        {
            ["Min"] = new OSDArray { min, min, min },
            ["Max"] = new OSDArray { max, max, max }
        };

        private static bool TryDecodeGeometry(OSDArray faces, out FacetedMesh mesh, OSDMap skin = null)
        {
            byte[] part = Helpers.ZCompressOSD(faces);
            var header = new OSDMap { ["high_lod"] = PartInfo(0, part.Length) };
            byte[] skinPart = null;
            if (skin != null)
            {
                skinPart = Helpers.ZCompressOSD(skin);
                header["skin"] = PartInfo(part.Length, skinPart.Length);
            }
            byte[] asset = skinPart == null ? MeshAsset(header, part) : MeshAsset(header, part, skinPart);
            return FacetedMesh.TryDecodeFromAsset(new Primitive(), new AssetMesh(UUID.Random(), asset), DetailLevel.Highest, out mesh);
        }

        [Test]
        public void Geometry_ValidSubMesh_Decodes()
        {
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh() }, out var mesh), Is.True);
            Assert.That(mesh.Faces.Count, Is.EqualTo(1));
            Assert.That(mesh.Faces[0].Vertices.Count, Is.EqualTo(3));
            Assert.That(mesh.Faces[0].Indices, Is.EqualTo(new ushort[] { 0, 1, 2 }));
        }

        [Test]
        public void Geometry_EightFaces_Decode_NineAreRefused()
        {
            var eight = new OSDArray();
            for (int i = 0; i < 8; i++) eight.Add(SubMesh());
            Assert.That(TryDecodeGeometry(eight, out _), Is.True);

            var nine = new OSDArray();
            for (int i = 0; i < 9; i++) nine.Add(SubMesh());
            Assert.That(TryDecodeGeometry(nine, out var mesh), Is.False);
            Assert.That(mesh, Is.Null);
        }

        [Test]
        public void Geometry_TooManyVertices_AreRefused()
        {
            FacetedMesh.MaxVerticesPerFace = 4;
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(4, new ushort[] { 0, 1, 2 }) }, out _), Is.True);
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(5, new ushort[] { 0, 1, 2 }) }, out _), Is.False);
        }

        [Test]
        public void Geometry_TooManyTriangles_AreRefused()
        {
            FacetedMesh.MaxTrianglesPerFace = 1;
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(3, new ushort[] { 0, 1, 2 }) }, out _), Is.True);
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(3, new ushort[] { 0, 1, 2, 2, 1, 0 }) }, out _), Is.False);
        }

        [Test]
        public void Geometry_PartialEntries_AreRefused()
        {
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(position: new byte[20]) }, out _), Is.False);
        }

        [Test]
        public void Geometry_TriangleIndexPastTheVertices_IsRefused()
        {
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(3, new ushort[] { 0, 1, 3 }) }, out _), Is.False);
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(3, new ushort[] { 0, 1, 65535 }) }, out _), Is.False);
        }

        [Test]
        public void Geometry_NonFiniteDomain_IsRefused()
        {
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(positionDomain: Domain(-1f, 1f)) }, out _), Is.True);
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(positionDomain: Domain(float.NaN, 1f)) }, out _), Is.False);
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(positionDomain: Domain(-1f, float.PositiveInfinity)) }, out _), Is.False);
        }

        [Test]
        public void Geometry_DomainWhoseExtentOverflows_IsRefused()
        {
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh(positionDomain: Domain(-3e38f, 3e38f)) }, out _), Is.False);
        }

        [Test]
        public void Geometry_TooManyJoints_AreRefused()
        {
            OSDMap Skin(int joints)
            {
                var names = new OSDArray();
                for (int i = 0; i < joints; i++) names.Add("joint" + i);
                return new OSDMap { ["joint_names"] = names };
            }

            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh() }, out _, Skin(110)), Is.True);
            Assert.That(TryDecodeGeometry(new OSDArray { SubMesh() }, out _, Skin(FacetedMesh.MaxSkinJoints + 1)), Is.False);
        }

        #endregion

        #region texture

        [Test]
        public void Texture_EncodedByUs_Decodes()
        {
            var image = new ManagedImage(32, 16, ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha);
            for (int i = 0; i < image.Red.Length; i++)
            {
                image.Red[i] = (byte)i;
                image.Green[i] = (byte)(i * 3);
                image.Blue[i] = (byte)(i * 7);
                image.Alpha[i] = 255;
            }
            var encoder = new AssetTexture(image);
            encoder.Encode();

            var decoder = new AssetTexture(UUID.Random(), encoder.AssetData);

            Assert.That(decoder.Decode(), Is.True);
            Assert.That(decoder.Image, Is.Not.Null);
            Assert.That(decoder.Image.Width, Is.EqualTo(32));
            Assert.That(decoder.Image.Height, Is.EqualTo(16));
            Assert.That(decoder.Components, Is.EqualTo(4));
        }

        [Test]
        public void Texture_EncodedByUs_PassesHeaderValidation()
        {
            var image = new ManagedImage(64, 64, ManagedImage.ImageChannels.Color);
            var encoder = new AssetTexture(image);
            encoder.Encode();

            Assert.That(AssetTexture.TryValidateHeader(encoder.AssetData, out string reason), Is.True, reason);
        }

        [Test]
        public void Texture_Jp2Wrapped_IsAccepted()
        {
            byte[] codestream = J2kHeader();
            var jp2 = new byte[12 + 8 + codestream.Length];
            // signature box
            Array.Copy(Be32(12), jp2, 4);
            Array.Copy(Encoding.ASCII.GetBytes("jP  "), 0, jp2, 4, 4);
            jp2[8] = 0x0D; jp2[9] = 0x0A; jp2[10] = 0x87; jp2[11] = 0x0A;
            // contiguous codestream box
            Array.Copy(Be32(8 + codestream.Length), 0, jp2, 12, 4);
            Array.Copy(Encoding.ASCII.GetBytes("jp2c"), 0, jp2, 16, 4);
            Array.Copy(codestream, 0, jp2, 20, codestream.Length);

            Assert.That(AssetTexture.TryValidateHeader(jp2, out string reason), Is.True, reason);
        }

        [Test]
        public void Texture_ReasonableHeader_PassesValidation()
        {
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(), out string reason), Is.True, reason);
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(2048, 2048, 1024), out reason), Is.True, reason);
        }

        [Test]
        public void Texture_LargeDeclaredDimensions_AreRefused()
        {
            byte[] data = J2kHeader(4096, 4096, 4096);

            Assert.That(AssetTexture.TryValidateHeader(data, out _), Is.False);
            Assert.That(new AssetTexture(UUID.Random(), data).Decode(), Is.False);
        }

        [Test]
        public void Texture_DimensionLimitIsConfigurable()
        {
            int saved = AssetTexture.MaxDimension;
            try
            {
                AssetTexture.MaxDimension = 4096;
                Assert.That(AssetTexture.TryValidateHeader(J2kHeader(4096, 4096, 4096), out string reason), Is.True, reason);
            }
            finally { AssetTexture.MaxDimension = saved; }
        }

        [Test]
        public void Texture_DimensionsAreMeasuredFromTheImageOrigin()
        {
            // Xsiz - XOsiz is what counts, not Xsiz
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(4096 + 64, 64, 4096 + 64, xOsiz: 4096), out string reason), Is.True, reason);
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(uint.MaxValue, 64, 64), out _), Is.False);
        }

        [Test]
        public void Texture_TooManyTiles_AreRefused()
        {
            // 2048x2048 in 8x8 tiles is 65,536 tiles
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(2048, 2048, 8), out _), Is.False);
        }

        [Test]
        public void Texture_ZeroTileSize_IsRefused()
        {
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(64, 64, 0), out _), Is.False);
        }

        [Test]
        public void Texture_TooManyComponents_AreRefused()
        {
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(components: 200), out _), Is.False);
        }

        [Test]
        public void Texture_ExcessiveBitDepth_IsRefused()
        {
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(bitDepth: 32), out _), Is.False);
        }

        [Test]
        public void Texture_ExcessiveDecompositionLevels_AreRefused()
        {
            Assert.That(AssetTexture.TryValidateHeader(J2kHeader(levels: 200), out _), Is.False);
        }

        [Test]
        public void Texture_TruncatedHeader_IsRefused()
        {
            byte[] data = J2kHeader();
            for (int length = 0; length < 44; length++)
            {
                var truncated = new byte[length];
                Array.Copy(data, truncated, length);
                Assert.That(AssetTexture.TryValidateHeader(truncated, out _), Is.False, $"length {length}");
            }
        }

        [Test]
        public void Texture_Garbage_ReturnsFalseWithoutThrowing()
        {
            var random = new Random(42);
            for (int i = 0; i < 200; i++)
            {
                var data = new byte[random.Next(1, 300)];
                random.NextBytes(data);
                var texture = new AssetTexture(UUID.Random(), data);
                Assert.DoesNotThrow(() => texture.Decode());
            }
        }

        [Test]
        public void Texture_MutatedHeaders_NeverThrow()
        {
            byte[] baseline = J2kHeader();
            var random = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                var data = (byte[])baseline.Clone();
                data[random.Next(data.Length)] = (byte)random.Next(256);
                data[random.Next(data.Length)] = (byte)random.Next(256);
                Assert.DoesNotThrow(() => AssetTexture.TryValidateHeader(data, out _));
            }
        }

        [Test]
        public void Texture_ValidHeaderButNoImageData_ReturnsFalse()
        {
            // passes the header checks, then the decoder fails on the missing tile data
            var texture = new AssetTexture(UUID.Random(), J2kHeader());
            Assert.DoesNotThrow(() => Assert.That(texture.Decode(), Is.False));
            Assert.That(texture.Image, Is.Null);
        }

        #endregion
    }
}
