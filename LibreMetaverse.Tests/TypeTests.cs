/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class TypeTests : Assert
    {
        [Test]
        public void UUIDs()
        {
            // Creation
            UUID a = new UUID();
            byte[] bytes = a.GetBytes();
            for (int i = 0; i < 16; i++)
                Assert.That(bytes[i], Is.Zero);

            // Comparison
            a = new UUID(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
                0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0xFF, 0xFF }, 0);
            UUID b = new UUID(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
                0x0B, 0x0C, 0x0D, 0x0E, 0x0F }, 0);

            Assert.That(a, Is.EqualTo(b), 
                "UUID comparison operator failed, " + a + " should equal " + b);

            // From string
            a = new UUID(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
                0x0B, 0x0C, 0x0D, 0x0E, 0x0F }, 0);
            string zeroonetwo = "00010203-0405-0607-0809-0a0b0c0d0e0f";
            b = new UUID(zeroonetwo);

            Assert.That(a, Is.EqualTo(b), 
                "UUID hyphenated string constructor failed, should have " + a + " but we got " + b);

            // ToString()            
            Assert.That(a, Is.EqualTo(b));                        
            Assert.That(a, Is.EqualTo((UUID)zeroonetwo));

            // TODO: CRC test
        }

        [Test]
        public void Vector3ApproxEquals()
        {
            Vector3 a = new Vector3(1f, 0f, 0f);
            Vector3 b = new Vector3(0f, 0f, 0f);

            Assert.That(a.ApproxEquals(b, 0.9f), Is.False, "ApproxEquals failed (1)");
            Assert.That(a.ApproxEquals(b, 1.0f), Is.True, "ApproxEquals failed (2)");

            a = new Vector3(-1f, 0f, 0f);
            b = new Vector3(1f, 0f, 0f);

            Assert.That(a.ApproxEquals(b, 1.9f), Is.False, "ApproxEquals failed (3)");
            Assert.That(a.ApproxEquals(b, 2.0f), Is.True, "ApproxEquals failed (4)");

            a = new Vector3(0f, -1f, 0f);
            b = new Vector3(0f, -1.1f, 0f);

            Assert.That(a.ApproxEquals(b, 0.09f), Is.False, "ApproxEquals failed (5)");
            Assert.That(a.ApproxEquals(b, 0.11f), Is.True, "ApproxEquals failed (6)");

            a = new Vector3(0f, 0f, 0.00001f);
            b = new Vector3(0f, 0f, 0f);

            Assert.That(b.ApproxEquals(a, float.Epsilon), Is.False, "ApproxEquals failed (6)");
            Assert.That(b.ApproxEquals(a, 0.0001f), Is.True, "ApproxEquals failed (7)");
        }

        [Test]
        public void VectorCasting()
        {
            var testNumbers = new Dictionary<string, double>
            {
                ["1.0"] = 1.0,
                ["1.1"] = 1.1,
                ["1.01"] = 1.01,
                ["1.001"] = 1.001,
                ["1.0001"] = 1.0001,
                ["1.00001"] = 1.00001,
                ["1.000001"] = 1.000001,
                ["1.0000001"] = 1.0000001,
                ["1.00000001"] = 1.00000001
            };

            foreach (var kvp in testNumbers)
            {
                double testNumber = kvp.Value;
                // ReSharper disable once RedundantCast
                double testNumber2 = (double)(float)testNumber;
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                bool noPrecisionLoss = testNumber == testNumber2;

                Vector3 a = new Vector3(
                        (float)testNumber,
                        (float)testNumber, (float)testNumber);
                Vector3d b = new Vector3d(testNumber, testNumber, testNumber);

                Vector3 c = (Vector3)b;
                Vector3d d = a;

                if (noPrecisionLoss)
                {
                    Console.Error.WriteLine("Unsuitable test value used-" +
                            " test number should have precision loss when" +
                            " cast to float ({0}).", kvp.Key);
                }
                else
                {
                    Assert.That(b, Is.Not.EqualTo(d),
                        "Vector casting failed, explicit cast of double to float should result in precision loss" +
                        " which should not magically disappear when Vector3 is implicitly cast to Vector3d." +
                        $" {kvp.Key}: {b.X}, {d.X}");
                }
                Assert.That(a, Is.EqualTo(c),
                    "Vector casting failed, Vector3 compared to" + " explicit cast of Vector3d to Vector3 should" +
                    " result in identical precision loss." + $" {kvp.Key}: {a.X}, {c.X}");
                Assert.That(a == d, Is.True,
                    "Vector casting failed, implicit cast of Vector3" +
                    " to Vector3d should not result in precision loss." + $" {kvp.Key}: {a.X}, {d.X}");
            }
        }

        [Test]
        public void Quaternions()
        {
            Quaternion a = new Quaternion(1, 0, 0, 0);
            Quaternion b = new Quaternion(1, 0, 0, 0);

            Assert.That(a, Is.EqualTo(b), "Quaternion comparison operator failed");

            Quaternion expected = new Quaternion(0, 0, 0, -1);
            Quaternion result = a * b;

            Assert.That(result, Is.EqualTo(expected), 
                a + " * " + b + " produced " + result + " instead of " + expected);

            a = new Quaternion(1, 0, 0, 0);
            b = new Quaternion(0, 1, 0, 0);
            expected = new Quaternion(0, 0, 1, 0);
            result = a * b;

            Assert.That(result, Is.EqualTo(expected), 
                a + " * " + b + " produced " + result + " instead of " + expected);

            a = new Quaternion(0, 0, 1, 0);
            b = new Quaternion(0, 1, 0, 0);
            expected = new Quaternion(-1, 0, 0, 0);
            result = a * b;

            Assert.That(result, Is.EqualTo(expected),
                a + " * " + b + " produced " + result + " instead of " + expected);
        }
        
        [Test]
        public void TestMatrix()
        {
    	    Matrix4 matrix = new Matrix4(0, 0, 74, 1,
				                         0, 435, 0, 1,
				                         345, 0, 34, 1,
				                         0, 0, 0, 0);
    	
    	    /* determinant of singular matrix returns zero */
            Assert.That((double)matrix.Determinant(), Is.Zero.Within(0.001d));
    	
    	    /* inverse of identity matrix is the identity matrix */
       	    Assert.That(Matrix4.Identity, Is.EqualTo(Matrix4.Inverse(Matrix4.Identity)));
  	 
    	    /* inverse of non-singular matrix returns True And InverseMatrix */
            matrix = new Matrix4(1, 1, 0, 0,
    			    		     1, 1, 1, 0,
    					         0, 1, 1, 0,
    					         0, 0, 0, 1);
    	    Matrix4 expectedInverse = new Matrix4(0, 1,-1, 0,
    						                      1,-1, 1, 0,
    						                     -1, 1, 0, 0,
    						                      0, 0, 0, 1);
            Assert.That(Matrix4.Inverse(matrix), Is.EqualTo(expectedInverse));
        }

        //[Test]
        //public void VectorQuaternionMath()
        //{
        //    // Convert a vector to a quaternion and back
        //    Vector3 a = new Vector3(1f, 0.5f, 0.75f);
        //    Quaternion b = a.ToQuaternion();
        //    Vector3 c;
        //    b.GetEulerAngles(out c.X, out c.Y, out c.Z);

        //    Assert.IsTrue(a == c, c.ToString() + " does not equal " + a.ToString());
        //}

        [Test]
        public void FloatsToTerseStrings()
        {
            float f = 1.20f;
            string fstr = "1.2";
            string str;
            
            str = Helpers.FloatToTerseString(f);
            Assert.That(str, Is.EqualTo(fstr), f + " converted to " + str + ", expecting " + fstr);

            f = 24.00f;
            fstr = "24";

            str = Helpers.FloatToTerseString(f);
            Assert.That(str, Is.EqualTo(fstr), f + " converted to " + str + ", expecting " + fstr);

            f = -0.59f;
            fstr = "-.59";

            str = Helpers.FloatToTerseString(f);
            Assert.That(str, Is.EqualTo(fstr), f + " converted to " + str + ", expecting " + fstr);

            f = 0.59f;
            fstr = ".59";

            str = Helpers.FloatToTerseString(f);
            Assert.That(str, Is.EqualTo(fstr), f + " converted to " + str + ", expecting " + fstr);
        }

        [Test]
        public void BitUnpacking()
        {
            byte[] data = new byte[] { 0x80, 0x00, 0x0F, 0x50, 0x83, 0x7D };
            BitPack bitpacker = new BitPack(data, 0);

            int b = bitpacker.UnpackBits(1);
            Assert.That(b, Is.EqualTo(1), "Unpacked " + b + " instead of 1");

            b = bitpacker.UnpackBits(1);
            Assert.That(b, Is.Zero, "Unpacked " + b + " instead of 0");

            bitpacker = new BitPack(data, 2);

            b = bitpacker.UnpackBits(4);
            Assert.That(b, Is.Zero, "Unpacked " + b + " instead of 0");

            b = bitpacker.UnpackBits(8);
            Assert.That(b, Is.EqualTo(0xF5), "Unpacked " + b + " instead of 0xF5");

            b = bitpacker.UnpackBits(4);
            Assert.That(b, Is.Zero, "Unpacked " + b + " instead of 0");

            b = bitpacker.UnpackBits(10);
            Assert.That(b, Is.EqualTo(0x0183), "Unpacked " + b + " instead of 0x0183");
        }

        [Test]
        public void BitPacking()
        {
            byte[] packedBytes = new byte[12];
            BitPack bitpacker = new BitPack(packedBytes, 0);

            bitpacker.PackBits(0x0ABBCCDD, 32);
            bitpacker.PackBits(25, 5);
            bitpacker.PackFloat(123.321f);
            bitpacker.PackBits(1000, 16);

            bitpacker = new BitPack(packedBytes, 0);

            int b = bitpacker.UnpackBits(32);
            Assert.That(b, Is.EqualTo(0x0ABBCCDD), "Unpacked " + b + " instead of 2864434397");

            b = bitpacker.UnpackBits(5);
            Assert.That(b, Is.EqualTo(25), "Unpacked " + b + " instead of 25");

            float f = bitpacker.UnpackFloat();
            Assert.That(f, Is.EqualTo(123.321f), "Unpacked " + f + " instead of 123.321");

            b = bitpacker.UnpackBits(16);
            Assert.That(b, Is.EqualTo(1000), "Unpacked " + b + " instead of 1000");

            packedBytes = new byte[1];
            bitpacker = new BitPack(packedBytes, 0);
            bitpacker.PackBit(true);

            bitpacker = new BitPack(packedBytes, 0);
            b = bitpacker.UnpackBits(1);
            Assert.That(b, Is.EqualTo(1), "Unpacked " + b + " instead of 1");

            // ReSharper disable once RedundantExplicitArraySize
            packedBytes = new byte[1] { byte.MaxValue };
            bitpacker = new BitPack(packedBytes, 0);
            bitpacker.PackBit(false);

            bitpacker = new BitPack(packedBytes, 0);
            b = bitpacker.UnpackBits(1);
            Assert.That(b, Is.Zero, "Unpacked " + b + " instead of 0");
        }

        [Test]
        public void LLSDTerseParsing()
        {
            string testOne = "[r0.99967899999999998428,r-0.025334599999999998787,r0]";
            string testTwo = "[[r1,r1,r1],r0]";
            string testThree = "{'region_handle':[r255232, r256512], 'position':[r33.6, r33.71, r43.13], 'look_at':[r34.6, r33.71, r43.13]}";

            OSD obj = OSDParser.DeserializeLLSDNotation(testOne);
            Assert.That(obj, Is.InstanceOf(typeof(OSDArray)), "Expected SDArray, got " + obj.GetType());
            OSDArray array = (OSDArray)obj;
            Assert.That(array, Has.Exactly(3).Items, "Expected three contained objects, got " + array.Count);
            Assert.That(array[0].AsReal(), Is.EqualTo(0.999d).Within(0.1d), 
                "Unexpected value for first real " + array[0].AsReal());
            Assert.That(array[1].AsReal(), Is.EqualTo(-0.02d).Within(0.1d), 
                "Unexpected value for second real " + array[1].AsReal());
            Assert.That(array[2].AsReal(), Is.Zero, "Unexpected value for third real " + array[2].AsReal());

            obj = OSDParser.DeserializeLLSDNotation(testTwo);
            Assert.That(obj, Is.InstanceOf(typeof(OSDArray)), "Expected SDArray, got " + obj.GetType());
            array = (OSDArray)obj;
            Assert.That(array, Has.Exactly(2).Items, "Expected two contained objects, got " + array.Count);
            Assert.That(array[1].AsReal(), Is.Zero, "Unexpected value for real " + array[1].AsReal());
            obj = array[0];
            Assert.That(obj, Is.InstanceOf(typeof(OSDArray)), "Expected ArrayList, got " + obj.GetType());
            array = (OSDArray)obj;
            Assert.That(array[0].AsReal(), Is.EqualTo(1.0d), "Unexpected value(s) for nested array: "+array[0].AsReal());
            Assert.That(array[1].AsReal(), Is.EqualTo(1.0d), "Unexpected value(s) for nested array: "+array[1].AsReal());
            Assert.That(array[2].AsReal(), Is.EqualTo(1.0d), "Unexpected value(s) for nested array: "+array[2].AsReal());

            obj = OSDParser.DeserializeLLSDNotation(testThree);
            Assert.That(obj, Is.InstanceOf(typeof(OSDMap)), "Expected LLSDMap, got " + obj.GetType());
            OSDMap hashtable = (OSDMap)obj;
            Assert.That(hashtable, Has.Exactly(3).Items, "Expected three contained objects, got " + hashtable.Count);
            Assert.That(hashtable["region_handle"], Is.InstanceOf(typeof(OSDArray)));
            Assert.That(hashtable["region_handle"], Has.Exactly(2).Items);
            Assert.That(hashtable["position"], Is.InstanceOf(typeof(OSDArray)));
            Assert.That(hashtable["position"], Has.Exactly(3).Items);
            Assert.That(hashtable["look_at"], Is.InstanceOf(typeof(OSDArray)));
            Assert.That(hashtable["look_at"], Has.Exactly(3).Items);
        }
    }
}
