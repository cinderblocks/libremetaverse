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
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class ScriptLanguageTests
    {
        private static OSDMap ScriptItem(Action<OSDMap> configure = null)
        {
            var item = new OSDMap
            {
                ["item_id"] = OSD.FromUUID(UUID.Random()),
                ["parent_id"] = OSD.FromUUID(UUID.Random()),
                ["asset_id"] = OSD.FromUUID(UUID.Random()),
                ["created_at"] = OSD.FromInteger(1785363605),
                ["desc"] = OSD.FromString("description"),
                ["flags"] = OSD.FromInteger(0),
                ["inv_type"] = OSD.FromString("script"),
                ["type"] = OSD.FromString("lsltext"),
                ["name"] = OSD.FromString("New Script"),
                ["metadata"] = new OSDMap(),
                ["permissions"] = new OSDMap
                {
                    ["base_mask"] = OSD.FromInteger(2147483647),
                    ["owner_mask"] = OSD.FromInteger(2147483647),
                    ["group_mask"] = OSD.FromInteger(0),
                    ["everyone_mask"] = OSD.FromInteger(0),
                    ["next_owner_mask"] = OSD.FromInteger(532480),
                    ["owner_id"] = OSD.FromUUID(UUID.Random()),
                    ["creator_id"] = OSD.FromUUID(UUID.Random()),
                    ["last_owner_id"] = OSD.FromUUID(UUID.Random()),
                    ["group_id"] = OSD.FromUUID(UUID.Zero),
                    ["is_owner_group"] = OSD.FromBoolean(false)
                },
                ["sale_info"] = new OSDMap
                {
                    ["sale_price"] = OSD.FromInteger(10),
                    ["sale_type"] = OSD.FromInteger(0)
                }
            };
            configure?.Invoke(item);
            return item;
        }

        private static OSDMap Runtime(string runtime) => new OSDMap { ["script"] = new OSDMap { ["runtime"] = OSD.FromString(runtime) } };

        #region Compile targets

        [Test]
        public void TheTargetOfLslSourceIsMonoOrLso2()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ScriptCompileTarget.FromMono(true), Is.EqualTo("mono"));
                Assert.That(ScriptCompileTarget.FromMono(false), Is.EqualTo("lsl2"));
            });
        }

        [TestCase(false, false, false, "lsl2")]
        [TestCase(true, false, false, "mono")]
        [TestCase(false, true, false, "lsl-luau")]
        [TestCase(false, true, true, "luau")]
        [TestCase(true, true, true, "luau")]
        [TestCase(true, false, true, "mono")]
        public void TheTargetAScriptRunsOnIsReadFromTheReply(bool mono, bool luau, bool luauLanguage, string expected)
        {
            Assert.That(ScriptCompileTarget.FromReply(mono, luau, luauLanguage), Is.EqualTo(expected));
        }

        [TestCase("luau", true, true)]
        [TestCase("LUAU", true, true)]
        [TestCase("lsl-luau", false, true)]
        [TestCase("mono", false, false)]
        [TestCase("lsl2", false, false)]
        [TestCase("", false, false)]
        [TestCase(null, false, false)]
        public void LuaSourceAndTheLuauRuntimeAreTold(string target, bool luaSource, bool luauRuntime)
        {
            Assert.Multiple(() =>
            {
                Assert.That(ScriptCompileTarget.IsLuaSource(target), Is.EqualTo(luaSource));
                Assert.That(ScriptCompileTarget.IsLuauRuntime(target), Is.EqualTo(luauRuntime));
            });
        }

        #endregion Compile targets

        #region Messages

        [Test]
        public void ScriptRunningReply_CarriesTheLuauFields()
        {
            var sent = new ScriptRunningReplyMessage
            {
                ItemID = UUID.Random(), ObjectID = UUID.Random(), Running = true, Mono = false, Luau = true, LuauLanguage = true
            };

            var read = new ScriptRunningReplyMessage();
            read.Deserialize(sent.Serialize());

            Assert.Multiple(() =>
            {
                Assert.That(read.Luau, Is.True);
                Assert.That(read.LuauLanguage, Is.True);
                Assert.That(read.Mono, Is.False);
                Assert.That(read.Running, Is.True);
                Assert.That(read.CompileTarget, Is.EqualTo("luau"));
            });
        }

        [Test]
        public void ScriptRunningReply_FromASimulatorWithoutLuauIsMonoOrLso2()
        {
            // As a simulator without Luau sends it: no Luau fields at all
            var map = new OSDMap
            {
                ["Script"] = new OSDArray
                {
                    new OSDMap
                    {
                        ["ItemID"] = OSD.FromUUID(UUID.Random()),
                        ["ObjectID"] = OSD.FromUUID(UUID.Random()),
                        ["Mono"] = OSD.FromBoolean(true),
                        ["Running"] = OSD.FromBoolean(true)
                    }
                }
            };

            var read = new ScriptRunningReplyMessage();
            read.Deserialize(map);

            Assert.Multiple(() =>
            {
                Assert.That(read.Luau, Is.False);
                Assert.That(read.LuauLanguage, Is.False);
                Assert.That(read.CompileTarget, Is.EqualTo("mono"));
            });
        }

        [Test]
        public void ScriptRunningReply_EventArgsTellTheTargetAndKeepTheOldConstructor()
        {
            var id = UUID.Random();
            var old = new ScriptRunningReplyEventArgs(id, id, true, true);
            var lsl = new ScriptRunningReplyEventArgs(id, id, false, true, true, false);
            var lua = new ScriptRunningReplyEventArgs(id, id, false, true, true, true);

            Assert.Multiple(() =>
            {
                Assert.That(old.CompileTarget, Is.EqualTo("mono"));
                Assert.That(old.IsLuau, Is.False);
                Assert.That(lsl.CompileTarget, Is.EqualTo("lsl-luau"));
                Assert.That(lua.CompileTarget, Is.EqualTo("luau"));
                Assert.That(lua.IsLuaLanguage, Is.True);
            });
        }

        [TestCase("luau")]
        [TestCase("lsl-luau")]
        public void TheUploadRequestsCarryTheLuauTargets(string target)
        {
            var agent = new UpdateScriptAgentRequestMessage { ItemID = UUID.Random(), Target = target };
            var task = new UpdateScriptTaskUpdateMessage { ItemID = UUID.Random(), TaskID = UUID.Random(), ScriptRunning = true, Target = target };

            Assert.Multiple(() =>
            {
                Assert.That(agent.Serialize()["target"].AsString(), Is.EqualTo(target));
                Assert.That(task.Serialize()["target"].AsString(), Is.EqualTo(target));
                Assert.That(new UpdateScriptAgentRequestMessage().Target, Is.EqualTo("lsl2"), "the default is as it always was");
            });
        }

        #endregion Messages

        #region Inventory

        [Test]
        public void TheSubtypeIsTheLowerByteOfTheFlagsAndLeavesTheRest()
        {
            var script = new InventoryLSL(UUID.Random()) { Flags = 0x40000000 };

            Assert.That(script.Subtype, Is.EqualTo(ScriptSubtype.Lsl));
            Assert.That(script.IsLua, Is.False);

            script.Subtype = ScriptSubtype.Lua;

            Assert.Multiple(() =>
            {
                Assert.That(script.IsLua, Is.True);
                Assert.That(script.Flags, Is.EqualTo(0x40000001u));
            });

            script.Subtype = ScriptSubtype.Lsl;
            Assert.That(script.Flags, Is.EqualTo(0x40000000u));
        }

        [Test]
        public void ALuaScriptIsReadFromItsFlags()
        {
            var item = InventoryItem.FromOSD(ScriptItem(m => m["flags"] = OSD.FromInteger(1)));

            Assert.That(item, Is.InstanceOf<InventoryLSL>());
            Assert.That(((InventoryLSL)item).IsLua, Is.True);
        }

        [TestCase("lsl2", 0, "lsl2")]
        [TestCase("mono", 0, "mono")]
        [TestCase("luau", 0, "lsl-luau")]
        [TestCase("luau", 1, "luau")]
        [TestCase("", 1, "luau")]
        [TestCase("", 0, "")]
        public void TheTargetAScriptIsCompiledForIsToldFromItsRuntimeAndSubtype(string runtime, int flags, string expected)
        {
            var script = new InventoryLSL(UUID.Random()) { Runtime = runtime, Flags = (uint)flags };

            Assert.That(script.CompileTarget, Is.EqualTo(expected));
        }

        [Test]
        public void TheRuntimeIsReadFromTheMetadataAnInventoryServiceGives()
        {
            var item = (InventoryLSL)InventoryItem.FromOSD(ScriptItem(m => m["metadata"] = Runtime("luau")));

            Assert.That(item.Runtime, Is.EqualTo("luau"));
        }

        [Test]
        public void TheRuntimeIsReadFromTheScriptOfAnItem()
        {
            var item = (InventoryLSL)InventoryItem.FromOSD(ScriptItem(m => m["script"] = new OSDMap { ["runtime"] = OSD.FromString("mono") }));

            Assert.That(item.Runtime, Is.EqualTo("mono"));
        }

        [Test]
        public void AScriptWithoutARuntimeHasNone()
        {
            var item = (InventoryLSL)InventoryItem.FromOSD(ScriptItem());

            Assert.That(item.Runtime, Is.Empty);
        }

        [Test]
        public void UpdatingAScriptChangesItsRuntimeOnlyWhenTheUpdateHasOne()
        {
            var item = (InventoryLSL)InventoryItem.FromOSD(ScriptItem(m => m["metadata"] = Runtime("mono")));

            item.Update(new OSDMap { ["name"] = OSD.FromString("Renamed") });
            Assert.That(item.Runtime, Is.EqualTo("mono"), "an update that says nothing of it leaves it");

            item.Update(new OSDMap { ["metadata"] = Runtime("luau"), ["flags"] = OSD.FromInteger(1) });
            Assert.Multiple(() =>
            {
                Assert.That(item.Runtime, Is.EqualTo("luau"));
                Assert.That(item.IsLua, Is.True);
                Assert.That(item.Name, Is.EqualTo("Renamed"));
            });
        }

        [Test]
        public void TheRuntimeOfAScriptSurvivesTheInventoryCache()
        {
            var client = new GridClient();
            try
            {
                var inventory = new Inventory(client, UUID.Random());
                var folder = new InventoryFolder(UUID.Random()) { Name = "Scripts" };
                inventory.UpdateNodeFor(folder);
                var script = new InventoryLSL(UUID.Random()) { Name = "Lua", ParentUUID = folder.UUID, Runtime = "luau", Subtype = ScriptSubtype.Lua };
                inventory.UpdateNodeFor(script);

                var file = Path.GetTempFileName();
                try
                {
                    inventory.SaveToDisk(file);
                    // The cache only gives back an item to a folder the inventory already has, as the server's skeleton does
                    inventory.RemoveNodeFor(script);
                    Assert.That(inventory.Contains(script.UUID), Is.False);
                    Assert.That(inventory.RestoreFromDisk(file), Is.EqualTo(1));
                }
                finally
                {
                    File.Delete(file);
                }

                Assert.That(inventory.TryGetValue(script.UUID, out InventoryBase restored), Is.True);
                var lsl = (InventoryLSL)restored;
                Assert.Multiple(() =>
                {
                    Assert.That(lsl.Runtime, Is.EqualTo("luau"));
                    Assert.That(lsl.IsLua, Is.True);
                });
            }
            finally
            {
                client.Dispose();
            }
        }

        #endregion Inventory

        #region Simulator features

        private static SimulatorFeatures FeaturesWith(OSDMap features)
        {
            var client = new GridClient();
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);
            var simulatorFeatures = new SimulatorFeatures(sim);
            using var response = new HttpResponseMessage(HttpStatusCode.OK);
            simulatorFeatures.SetFeatures(response, OSDParser.SerializeLLSDXmlBytes(features), null);
            return simulatorFeatures;
        }

        [Test]
        public void LuaScriptsAreEnabledWhenTheSimulatorSaysSo()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FeaturesWith(new OSDMap { ["LuaScriptsEnabled"] = OSD.FromBoolean(true) }).LuaScriptsEnabled, Is.True);
                Assert.That(FeaturesWith(new OSDMap { ["LuaScriptsEnabled"] = OSD.FromBoolean(false) }).LuaScriptsEnabled, Is.False);
                Assert.That(FeaturesWith(new OSDMap()).LuaScriptsEnabled, Is.False, "a simulator without Luau does not say");
            });
        }

        #endregion Simulator features

        [Test]
        public void ReadCompileErrors_GivesTheErrorsAsStrings()
        {
            var response = new OSDMap
            {
                ["compiled"] = OSD.FromBoolean(false),
                ["errors"] = new OSDArray { OSD.FromString("(3, 7) : ERROR : Syntax error"), OSD.FromString("(4, 1) : ERROR : Unexpected end") }
            };

            Assert.That(InventoryManager.ReadCompileErrors(response),
                Is.EqualTo(new[] { "(3, 7) : ERROR : Syntax error", "(4, 1) : ERROR : Unexpected end" }));
        }

        [Test]
        public void ReadCompileErrors_ErrorsWhichAreNotAnArrayAreNoErrors()
        {
            // a script which compiled can have errors as nothing, and that must not fail the upload
            Assert.That(InventoryManager.ReadCompileErrors(new OSDMap { ["compiled"] = OSD.FromBoolean(true) }), Is.Null);
            Assert.That(InventoryManager.ReadCompileErrors(new OSDMap { ["errors"] = new OSD() }), Is.Null);
            Assert.That(InventoryManager.ReadCompileErrors(new OSDMap { ["errors"] = OSD.FromString("oops") }), Is.Null);
        }

        [Test]
        public void ScriptUploadFailure_IsTheMessageOfTheGridOtherwiseTheState()
        {
            var withMessage = new OSDMap { ["state"] = OSD.FromString("error"), ["message"] = OSD.FromString("Lua is not enabled in this region") };

            Assert.That(InventoryManager.ScriptUploadFailure(withMessage, "error"), Is.EqualTo("Lua is not enabled in this region"));
            Assert.That(InventoryManager.ScriptUploadFailure(new OSDMap { ["state"] = OSD.FromString("error") }, "error"), Is.EqualTo("error"));
            Assert.That(InventoryManager.ScriptUploadFailure(new OSDMap(), string.Empty), Is.EqualTo(string.Empty));
        }
    }
}
