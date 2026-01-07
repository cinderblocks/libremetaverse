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
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class ProtobufTests
    {
        [Test]
        public void SerializeBoolean()
        {
            OSD llsdTrue = OSD.FromBoolean(true);
            byte[] binaryTrue = OSDParser.SerializeLLSDProtobuf(llsdTrue);
            OSD llsdTrueDS = OSDParser.DeserializeLLSDProtobuf(binaryTrue);
            Assert.That(llsdTrueDS.Type, Is.EqualTo(OSDType.Boolean));
            Assert.That(llsdTrueDS.AsBoolean(), Is.True);

            OSD llsdFalse = OSD.FromBoolean(false);
            byte[] binaryFalse = OSDParser.SerializeLLSDProtobuf(llsdFalse);
            OSD llsdFalseDS = OSDParser.DeserializeLLSDProtobuf(binaryFalse);
            Assert.That(llsdFalseDS.Type, Is.EqualTo(OSDType.Boolean));
            Assert.That(llsdFalseDS.AsBoolean(), Is.False);
        }

        [Test]
        public void SerializeInteger()
        {
            OSD llsdZeroInt = OSD.FromInteger(0);
            byte[] binaryZeroInt = OSDParser.SerializeLLSDProtobuf(llsdZeroInt);
            OSD llsdZeroIntDS = OSDParser.DeserializeLLSDProtobuf(binaryZeroInt);
            Assert.That(llsdZeroIntDS.Type, Is.EqualTo(OSDType.Integer));
            Assert.That(llsdZeroIntDS.AsInteger(), Is.Zero);

            OSD llsdAnInt = OSD.FromInteger(1234843);
            byte[] binaryAnInt = OSDParser.SerializeLLSDProtobuf(llsdAnInt);
            OSD llsdAnIntDS = OSDParser.DeserializeLLSDProtobuf(binaryAnInt);
            Assert.That(llsdAnIntDS.Type, Is.EqualTo(OSDType.Integer));
            Assert.That(llsdAnIntDS.AsInteger(), Is.EqualTo(1234843));

            OSD llsdNegInt = OSD.FromInteger(-54321);
            byte[] binaryNegInt = OSDParser.SerializeLLSDProtobuf(llsdNegInt);
            OSD llsdNegIntDS = OSDParser.DeserializeLLSDProtobuf(binaryNegInt);
            Assert.That(llsdNegIntDS.Type, Is.EqualTo(OSDType.Integer));
            Assert.That(llsdNegIntDS.AsInteger(), Is.EqualTo(-54321));
        }

        [Test]
        public void SerializeReal()
        {
            OSD llsdReal = OSD.FromReal(947835.234d);
            byte[] binaryReal = OSDParser.SerializeLLSDProtobuf(llsdReal);
            OSD llsdRealDS = OSDParser.DeserializeLLSDProtobuf(binaryReal);
            Assert.That(llsdRealDS.Type, Is.EqualTo(OSDType.Real));
            Assert.That(llsdRealDS.AsReal(), Is.EqualTo(947835.234d));
        }

        [Test]
        public void SerializeUUID()
        {
            OSD llsdAUUID = OSD.FromUUID(new UUID("97f4aeca-88a1-42a1-b385-b97b18abb255"));
            byte[] binaryAUUID = OSDParser.SerializeLLSDProtobuf(llsdAUUID);
            OSD llsdAUUIDDS = OSDParser.DeserializeLLSDProtobuf(binaryAUUID);
            Assert.That(llsdAUUIDDS.Type, Is.EqualTo(OSDType.UUID));
            Assert.That(llsdAUUIDDS.AsString(), Is.EqualTo("97f4aeca-88a1-42a1-b385-b97b18abb255"));

            OSD llsdZeroUUID = OSD.FromUUID(UUID.Zero);
            byte[] binaryZeroUUID = OSDParser.SerializeLLSDProtobuf(llsdZeroUUID);
            OSD llsdZeroUUIDDS = OSDParser.DeserializeLLSDProtobuf(binaryZeroUUID);
            Assert.That(llsdZeroUUIDDS.Type, Is.EqualTo(OSDType.UUID));
            Assert.That(llsdZeroUUIDDS.AsUUID(), Is.EqualTo(UUID.Zero));
        }

        [Test]
        public void SerializeString()
        {
            OSD llsdString = OSD.FromString("abcdefghijklmnopqrstuvwxyz01234567890");
            byte[] binaryString = OSDParser.SerializeLLSDProtobuf(llsdString);
            OSD llsdStringDS = OSDParser.DeserializeLLSDProtobuf(binaryString);
            Assert.That(llsdStringDS.Type, Is.EqualTo(OSDType.String));
            Assert.That(llsdStringDS.AsString(), Is.EqualTo("abcdefghijklmnopqrstuvwxyz01234567890"));

            OSD llsdEmptyString = OSD.FromString(string.Empty);
            byte[] binaryEmptyString = OSDParser.SerializeLLSDProtobuf(llsdEmptyString);
            OSD llsdEmptyStringDS = OSDParser.DeserializeLLSDProtobuf(binaryEmptyString);
            Assert.That(llsdEmptyStringDS.Type, Is.EqualTo(OSDType.String));
            Assert.That(llsdEmptyStringDS.AsString(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SerializeArray()
        {
            OSDArray llsdArray = new OSDArray
            {
                OSD.FromInteger(1),
                OSD.FromInteger(2),
                OSD.FromString("three")
            };

            byte[] binaryArray = OSDParser.SerializeLLSDProtobuf(llsdArray);
            OSD llsdArrayDS = OSDParser.DeserializeLLSDProtobuf(binaryArray);
            Assert.That(llsdArrayDS.Type, Is.EqualTo(OSDType.Array));

            OSDArray arrayDS = (OSDArray)llsdArrayDS;
            Assert.That(arrayDS, Has.Count.EqualTo(3));
            Assert.That(arrayDS[0].AsInteger(), Is.EqualTo(1));
            Assert.That(arrayDS[1].AsInteger(), Is.EqualTo(2));
            Assert.That(arrayDS[2].AsString(), Is.EqualTo("three"));
        }

        [Test]
        public void SerializeMap()
        {
            OSDMap llsdMap = new OSDMap
            {
                ["name"] = OSD.FromString("Test"),
                ["value"] = OSD.FromInteger(42),
                ["enabled"] = OSD.FromBoolean(true)
            };

            byte[] binaryMap = OSDParser.SerializeLLSDProtobuf(llsdMap);
            OSD llsdMapDS = OSDParser.DeserializeLLSDProtobuf(binaryMap);
            Assert.That(llsdMapDS.Type, Is.EqualTo(OSDType.Map));

            OSDMap mapDS = (OSDMap)llsdMapDS;
            Assert.That(mapDS, Has.Count.EqualTo(3));
            Assert.That(mapDS["name"].AsString(), Is.EqualTo("Test"));
            Assert.That(mapDS["value"].AsInteger(), Is.EqualTo(42));
            Assert.That(mapDS["enabled"].AsBoolean(), Is.True);
        }

        [Test]
        public void SerializeNestedComposite()
        {
            OSDArray nestedArray = new OSDArray();
            OSDMap nestedMap = new OSDMap();
            OSDArray innerArray = new OSDArray();
            innerArray.Add(OSD.FromInteger(1));
            innerArray.Add(OSD.FromInteger(2));
            nestedMap["items"] = innerArray;
            nestedMap["name"] = OSD.FromString("nested");
            nestedArray.Add(nestedMap);
            nestedArray.Add(OSD.FromInteger(124));

            byte[] binaryNested = OSDParser.SerializeLLSDProtobuf(nestedArray);
            OSD llsdNestedDS = OSDParser.DeserializeLLSDProtobuf(binaryNested);
            Assert.That(llsdNestedDS.Type, Is.EqualTo(OSDType.Array));

            OSDArray arrayDS = (OSDArray)llsdNestedDS;
            Assert.That(arrayDS, Has.Count.EqualTo(2));

            OSDMap mapDS = (OSDMap)arrayDS[0];
            Assert.That(mapDS.Type, Is.EqualTo(OSDType.Map));
            Assert.That(mapDS, Has.Count.EqualTo(2));
            Assert.That(mapDS["name"].AsString(), Is.EqualTo("nested"));

            OSDArray innerArrayDS = (OSDArray)mapDS["items"];
            Assert.That(innerArrayDS, Has.Count.EqualTo(2));
            Assert.That(innerArrayDS[0].AsInteger(), Is.EqualTo(1));
            Assert.That(innerArrayDS[1].AsInteger(), Is.EqualTo(2));

            Assert.That(arrayDS[1].AsInteger(), Is.EqualTo(124));
        }

        [Test]
        public void AutoDetectProtobuf()
        {
            OSD original = OSD.FromString("test protobuf detection");
            byte[] protobufData = OSDParser.SerializeLLSDProtobuf(original, true);

            // Test auto-detection via Deserialize
            OSD deserialized = OSDParser.Deserialize(protobufData);
            Assert.That(deserialized.Type, Is.EqualTo(OSDType.String));
            Assert.That(deserialized.AsString(), Is.EqualTo("test protobuf detection"));
        }

        [Test]
        public void SerializeBinary()
        {
            byte[] testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0xFF };
            OSD llsdBinary = OSD.FromBinary(testData);
            byte[] protobufData = OSDParser.SerializeLLSDProtobuf(llsdBinary);
            OSD llsdBinaryDS = OSDParser.DeserializeLLSDProtobuf(protobufData);
            Assert.That(llsdBinaryDS.Type, Is.EqualTo(OSDType.Binary));
            Assert.That(llsdBinaryDS.AsBinary(), Is.EqualTo(testData));
        }

        [Test]
        public void SerializeDate()
        {
            DateTime dt = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
            OSD llsdDate = OSD.FromDate(dt);
            byte[] protobufData = OSDParser.SerializeLLSDProtobuf(llsdDate);
            OSD llsdDateDS = OSDParser.DeserializeLLSDProtobuf(protobufData);
            Assert.That(llsdDateDS.Type, Is.EqualTo(OSDType.Date));
            // Allow for small timestamp conversion differences
            Assert.That(Math.Abs((llsdDateDS.AsDate() - dt).TotalSeconds), Is.LessThan(1.0));
        }

        [Test]
        public void SerializeUri()
        {
            OSD llsdUri = OSD.FromUri(new Uri("http://www.example.com/test"));
            byte[] protobufData = OSDParser.SerializeLLSDProtobuf(llsdUri);
            OSD llsdUriDS = OSDParser.DeserializeLLSDProtobuf(protobufData);
            Assert.That(llsdUriDS.Type, Is.EqualTo(OSDType.URI));
            Assert.That(llsdUriDS.AsUri().ToString(), Is.EqualTo("http://www.example.com/test"));
        }
    }
}
