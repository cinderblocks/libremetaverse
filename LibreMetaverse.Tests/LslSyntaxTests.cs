/*
 * Copyright (c) 2021-2026, Sjofn LLC
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

using System.IO;
using System.Linq;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The keywords of LSL and Lua, and the files of definitions a simulator gives them in
    /// </summary>
    [TestFixture]
    public class LslSyntaxTests
    {
        private static OSDMap Entry(string tooltip) => new OSDMap { ["tooltip"] = OSD.FromString(tooltip) };

        /// <summary>A file of definitions, small, with Lua's way of having names in more than one group</summary>
        private static OSDMap LuaDefinitions()
        {
            var say = new OSDMap
            {
                ["tooltip"] = OSD.FromString("Says a message"),
                ["energy"] = OSD.FromReal(10.0),
                ["return"] = OSD.FromString("()"),
                ["arguments"] = new OSDArray
                {
                    new OSDMap { ["channel"] = new OSDMap { ["type"] = OSD.FromString("number"), ["tooltip"] = OSD.FromString("The channel") } },
                    new OSDMap { ["msg"] = new OSDMap { ["type"] = OSD.FromString("string"), ["tooltip"] = OSD.FromString("The message") } }
                }
            };
            return new OSDMap
            {
                ["controls"] = new OSDMap { ["then"] = Entry("Lua then keyword") },
                ["types"] = new OSDMap { ["vector"] = Entry("The vector type") },
                ["constants"] = new OSDMap
                {
                    ["integer.maxsigned"] = new OSDMap
                    {
                        ["tooltip"] = OSD.FromString("The largest signed integer"),
                        ["type"] = OSD.FromString("number"),
                        ["value"] = OSD.FromInteger(2147483647)
                    }
                },
                ["functions"] = new OSDMap { ["ll.Say"] = say, ["vector"] = Entry("Makes a vector") },
                ["llsd-lsl-syntax-version"] = OSD.FromInteger(2)
            };
        }

        [Test]
        public void ParseKeywords_KeepsTheDottedNamesOfLua()
        {
            var keywords = LslSyntax.ParseKeywords(LuaDefinitions());

            Assert.That(keywords["ll.Say"].Category, Is.EqualTo(LslSyntax.LslCategory.Function));
            Assert.That(keywords["integer.maxsigned"].Category, Is.EqualTo(LslSyntax.LslCategory.Constant));
            Assert.That(keywords["then"].Category, Is.EqualTo(LslSyntax.LslCategory.Control));
        }

        [Test]
        public void ParseKeywords_ANameInTwoGroupsDoesNotStopTheParse()
        {
            // vector is a type and a function in Lua; the function comes after the type, and so do the others
            var definitions = LuaDefinitions();
            definitions["events"] = new OSDMap { ["touch_start"] = Entry("Touched") };

            var keywords = LslSyntax.ParseKeywords(definitions);

            Assert.That(keywords["vector"].Category, Is.EqualTo(LslSyntax.LslCategory.Datatype));
            Assert.That(keywords.ContainsKey("touch_start"), Is.True);
        }

        [Test]
        public void ParseKeywords_ArgumentsAreSeparatedAndTyped()
        {
            var keywords = LslSyntax.ParseKeywords(LuaDefinitions());

            Assert.That(keywords["ll.Say"].Tooltip, Does.Contain("ll.Say (number channel, string msg)"));
        }

        [Test]
        public void TryReadDefinitions_TakesTheMapOrTheBytesOfIt()
        {
            var definitions = LuaDefinitions();

            Assert.That(LslSyntax.TryReadDefinitions(definitions, out var fromMap), Is.True);
            Assert.That(fromMap.ContainsKey("functions"), Is.True);

            var bytes = OSDParser.SerializeLLSDXmlBytes(definitions);
            Assert.That(LslSyntax.TryReadDefinitions(OSD.FromBinary(bytes), out var fromBytes), Is.True);
            Assert.That(fromBytes.ContainsKey("functions"), Is.True);

            Assert.That(LslSyntax.TryReadDefinitions(OSD.FromString("not definitions"), out _), Is.False);
        }

        [Test]
        public void ApplyDefinitionFiles_SetsEachLanguageFromItsFile()
        {
            var lslKeywords = LslSyntax.Keywords;
            var luaKeywords = LslSyntax.LuaKeywords;
            try
            {
                var response = new OSDMap
                {
                    ["files"] = new OSDMap
                    {
                        ["lsl_keywords.xml"] = new OSDMap
                        {
                            ["functions"] = new OSDMap { ["llSay"] = Entry("Says") },
                            ["llsd-lsl-syntax-version"] = OSD.FromInteger(2)
                        },
                        ["lua_keywords.xml"] = OSD.FromBinary(OSDParser.SerializeLLSDXmlBytes(LuaDefinitions())),
                        ["something_else.xml"] = new OSDMap()
                    }
                };

                Assert.That(new LslSyntax().ApplyDefinitionFiles(response, null, UUID.Random()), Is.True);

                Assert.That(LslSyntax.GetKeywords(false).ContainsKey("llSay"), Is.True);
                Assert.That(LslSyntax.GetKeywords(false).ContainsKey("ll.Say"), Is.False);
                Assert.That(LslSyntax.GetKeywords(true).ContainsKey("ll.Say"), Is.True);
                Assert.That(LslSyntax.GetKeywords(true).ContainsKey("llSay"), Is.False);
            }
            finally
            {
                RestoreKeywords(lslKeywords, luaKeywords);
            }
        }

        [Test]
        public void ApplyDefinitionFiles_ALanguageTheSimulatorLeavesOutKeepsItsKeywords()
        {
            var lslKeywords = LslSyntax.Keywords;
            var luaKeywords = LslSyntax.LuaKeywords;
            try
            {
                var syntax = new LslSyntax();
                syntax.ApplyDefinitionFiles(new OSDMap { ["files"] = new OSDMap { ["lua_keywords.xml"] = LuaDefinitions() } }, null, UUID.Random());
                var lsl = LslSyntax.Keywords;

                var onlyLsl = new OSDMap
                {
                    ["files"] = new OSDMap
                    {
                        ["lsl_keywords.xml"] = new OSDMap { ["functions"] = new OSDMap { ["llSay"] = Entry("Says") } }
                    }
                };
                Assert.That(syntax.ApplyDefinitionFiles(onlyLsl, null, UUID.Random()), Is.True);

                Assert.That(LslSyntax.LuaKeywords.ContainsKey("ll.Say"), Is.True);
                Assert.That(LslSyntax.Keywords, Is.Not.SameAs(lsl));
            }
            finally
            {
                RestoreKeywords(lslKeywords, luaKeywords);
            }
        }

        [Test]
        public void ApplyDefinitionFiles_AResponseWithoutFilesIsNotApplied()
        {
            Assert.That(new LslSyntax().ApplyDefinitionFiles(new OSDMap(), null, UUID.Random()), Is.False);
        }

        [Test]
        public void TheLuaDefaultFile_HasTheKeywordsOfLua()
        {
            var file = Path.Combine(TestContext.CurrentContext.TestDirectory, "linden", "keywords_lua_default.xml");
            Assert.That(File.Exists(file), Is.True, "the default file of Lua should come with the library");

            OSDMap map;
            using (var stream = File.OpenRead(file))
            {
                map = (OSDMap)OSDParser.DeserializeLLSDXml(stream);
            }
            var keywords = LslSyntax.ParseKeywords(map);

            Assert.That(keywords["ll.Say"].Category, Is.EqualTo(LslSyntax.LslCategory.Function));
            Assert.That(keywords["end"].Category, Is.EqualTo(LslSyntax.LslCategory.Control));
            Assert.That(keywords["vector"].Category, Is.EqualTo(LslSyntax.LslCategory.Datatype));
            Assert.That(keywords.Values.Count(k => k.Category == LslSyntax.LslCategory.Function), Is.GreaterThan(500));
            Assert.That(keywords.Values.Count(k => k.Category == LslSyntax.LslCategory.Constant), Is.GreaterThan(500));
        }

        /// <summary>The keywords are shared by every instance, put them back for the tests which follow</summary>
        private static void RestoreKeywords(System.Collections.Frozen.FrozenDictionary<string, LslSyntax.LslKeyword> lsl,
            System.Collections.Frozen.FrozenDictionary<string, LslSyntax.LslKeyword> lua)
        {
            typeof(LslSyntax).GetField("_frozenKeywords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, lsl);
            typeof(LslSyntax).GetField("_frozenLuaKeywords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, lua);
        }
    }
}
