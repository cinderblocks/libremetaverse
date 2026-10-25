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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// A login server's side of the login: <see cref="LoginResponseData.ToOSD"/> writes a response that
    /// <see cref="LoginResponseData.Parse(OSDMap)"/> reads back, in the shape the viewer reads
    /// (indra/newview/llstartup.cpp, process_login_success_response), and <see cref="LoginRequest"/> is what the client posts.
    /// </summary>
    [TestFixture]
    [Category("Login")]
    public class LoginServerSideTests
    {
        #region Harness

        /// <summary>Every public property of an object, as text, so that two can be compared whole.</summary>
        private static string Snapshot(object? value, int depth = 0)
        {
            if (value == null) return "null";
            if (depth > 6) return "...";
            switch (value)
            {
                case string s: return "\"" + s + "\"";
                case OSD osd: return OSDParser.SerializeLLSDNotation(osd);
                case IPAddress ip: return ip.ToString();
                case UUID id: return id.ToString();
                case Vector3 v: return v.ToString();
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";

            if (value is IDictionary dictionary)
            {
                var entries = new List<string>();
                var enumerator = dictionary.GetEnumerator();
                while (enumerator.MoveNext())
                    entries.Add($"{enumerator.Key}={Snapshot(enumerator.Value, depth + 1)}");
                entries.Sort(StringComparer.Ordinal);
                return "{" + string.Join(";", entries) + "}";
            }

            if (value is IEnumerable items)
                return "[" + string.Join(";", items.Cast<object?>().Select(i => Snapshot(i, depth + 1))) + "]";

            var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod != null)
                .Select(p => (Name: p.Name, Value: p.GetValue(value)))
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => (Name: f.Name, Value: f.GetValue(value))))
                .OrderBy(m => m.Name, StringComparer.Ordinal);
            return type.Name + "(" + string.Join(", ", members.Select(m => $"{m.Name}: {Snapshot(m.Value, depth + 1)}")) + ")";
        }

        /// <summary>The first map of an array, as the viewer reads <c>response["inventory-root"][0]</c>.</summary>
        private static OSDMap First(OSD array) => (OSDMap)((OSDArray)array)[0];

        private static LoginResponseData ParsedSuccess()
        {
            var data = new LoginResponseData();
            data.Parse(LoginResponseDataTests.SuccessResponse());
            return data;
        }

        /// <summary>Over the wire and back: serialized as LLSD XML, as a login server sends it.</summary>
        private static OSDMap OverTheWire(OSDMap map) => (OSDMap)OSDParser.Deserialize(OSDParser.SerializeLLSDXmlBytes(map));

        #endregion Harness

        #region The response

        [Test]
        public void ASuccessfulResponse_WrittenAndReadBack_IsTheSameResponse()
        {
            var original = ParsedSuccess();

            var reread = new LoginResponseData();
            reread.Parse(original.ToOSD());

            Assert.That(Snapshot(reread), Is.EqualTo(Snapshot(original)));
        }

        [Test]
        public void ASuccessfulResponse_SentAsLlsdXml_ComesBackTheSame()
        {
            var original = ParsedSuccess();

            var reread = new LoginResponseData();
            reread.Parse(OverTheWire(original.ToOSD()));

            Assert.That(Snapshot(reread), Is.EqualTo(Snapshot(original)));
        }

        [Test]
        public void AResponseBuiltByHand_IsReadBackAsBuilt()
        {
            var built = new LoginResponseData
            {
                Login = LoginState.True,
                AgentID = LoginResponseDataTests.AgentId,
                SessionID = LoginResponseDataTests.SessionId,
                SecureSessionID = LoginResponseDataTests.SecureSessionId,
                FirstName = "Test",
                LastName = "Resident",
                StartLocation = "home",
                AgentAccess = "M",
                AgentAccessMax = "A",
                AgentRegionAccess = "M",
                LookAt = new Vector3(0f, 1f, 0f),
                Message = "Hello",
                CircuitCode = 99,
                RegionX = 1000 * 256,
                RegionY = 1001 * 256,
                RegionSizeX = 256,
                RegionSizeY = 256,
                SimIP = IPAddress.Parse("192.168.1.9"),
                SimPort = 9000,
                SeedCapability = "http://localhost:9000/CAPS/seed",
                SecondsSinceEpoch = 1700000000,
                InventoryRoot = LoginResponseDataTests.InventoryRootId,
                LibraryRoot = LoginResponseDataTests.LibraryRootId,
                LibraryOwner = LoginResponseDataTests.LibraryOwnerId,
                MaxAgentGroups = 60,
                FirstLogin = true
            };
            built.Home.RegionHandle = Utils.UIntsToLong(1000 * 256, 1001 * 256);
            built.Home.Position = new Vector3(100f, 120f, 30f);
            built.Home.LookAt = new Vector3(0f, 1f, 0f);
            built.InventorySkeleton = new[]
            {
                new InventoryFolder(LoginResponseDataTests.InventoryRootId) { Name = "My Inventory", ParentUUID = UUID.Zero, PreferredType = FolderType.Root, Version = 1, OwnerID = built.AgentID }
            };
            built.BuddyList = new[] { new BuddyListEntry { BuddyId = LoginResponseDataTests.BuddyId.ToString(), BuddyRightsGiven = 7, BuddyRightsHas = 3 } };

            var read = new LoginResponseData();
            read.Parse(OverTheWire(built.ToOSD()));

            Assert.That(Snapshot(read), Is.EqualTo(Snapshot(built)));
        }

        [Test]
        public void TheResponse_IsInTheShapeTheViewerReads()
        {
            // Each line is what llstartup.cpp does with the response
            var response = ParsedSuccess().ToOSD();

            Assert.Multiple(() =>
            {
                Assert.That(response["login"].AsString(), Is.EqualTo("true"));
                Assert.That(UUID.Parse(response["agent_id"].AsString()), Is.EqualTo(LoginResponseDataTests.AgentId));
                Assert.That(UUID.Parse(response["session_id"].AsString()), Is.EqualTo(LoginResponseDataTests.SessionId));
                Assert.That(uint.Parse(response["circuit_code"].AsString()), Is.EqualTo(123456u));
                Assert.That(response["sim_ip"].AsString(), Is.EqualTo("10.1.2.3"));
                Assert.That(uint.Parse(response["sim_port"].AsString()), Is.EqualTo(13000u));
                Assert.That(uint.Parse(response["region_x"].AsString()), Is.EqualTo(256000u));
                Assert.That(uint.Parse(response["region_y"].AsString()), Is.EqualTo(256512u));
                Assert.That(int.Parse(response["region_size_x"].AsString()), Is.EqualTo(512));
                Assert.That(response["seed_capability"].AsString(), Is.EqualTo("https://sim.example.org:12043/cap/abcd"));
                Assert.That(uint.Parse(response["seconds_since_epoch"].AsString()), Is.EqualTo(1700000000u));
                Assert.That(response["message"].AsString(), Is.EqualTo("Welcome to the grid"));
                Assert.That(response["start_location"].AsString(), Is.EqualTo("last"));
                Assert.That(response["agent_region_access"].AsString(), Is.EqualTo("M"));
                Assert.That(response["first_name"].AsString(), Does.Not.Contain("\""));

                // The viewer takes these as strings and reads them as LLSD notation
                Assert.That(response["look_at"].Type, Is.EqualTo(OSDType.String));
                var lookAt = (OSDArray)OSDParser.DeserializeLLSDNotation(response["look_at"].AsString());
                Assert.That(lookAt.Select(v => v.AsReal()), Is.EqualTo(new[] { 0.99, 0.1, 0.0 }).Within(1e-6));

                Assert.That(response["home"].Type, Is.EqualTo(OSDType.String));
                var home = (OSDMap)OSDParser.DeserializeLLSDNotation(response["home"].AsString());
                Assert.That(((OSDArray)home["region_handle"]).Select(v => v.AsInteger()), Is.EqualTo(new[] { 256000, 256512 }));
                Assert.That(((OSDArray)home["position"]).Select(v => v.AsReal()), Is.EqualTo(new[] { 128.0, 64.0, 25.5 }));

                // And these are arrays whose first element is the map
                Assert.That(UUID.Parse(First(response["inventory-root"])["folder_id"].AsString()), Is.EqualTo(LoginResponseDataTests.InventoryRootId));
                Assert.That(First(response["login-flags"])["ever_logged_in"].AsString(), Is.EqualTo("Y"));
                Assert.That(UUID.Parse(First(response["inventory-lib-owner"])["agent_id"].AsString()), Is.EqualTo(LoginResponseDataTests.LibraryOwnerId));
                Assert.That(response["inventory-skeleton"], Is.InstanceOf<OSDArray>());
                Assert.That(First(response["buddy-list"])["buddy_rights_given"].AsInteger(), Is.EqualTo(3));
            });
        }

        [Test]
        public void TheFirstLogin_IsSaidInTheLoginFlags_AndReadFromThem()
        {
            var first = ParsedSuccess();
            first.FirstLogin = true;
            var response = first.ToOSD();

            var reread = new LoginResponseData();
            reread.Parse(response);

            Assert.Multiple(() =>
            {
                Assert.That(First(response["login-flags"])["ever_logged_in"].AsString(), Is.EqualTo("N"), "N is a first login");
                Assert.That(reread.FirstLogin, Is.True);
                Assert.That(ParsedSuccess().FirstLogin, Is.False);
            });
        }

        [Test]
        public void AFailedLogin_IsWrittenWithJustItsReasonAndMessage()
        {
            var failed = new LoginResponseData { Login = LoginState.False, Reason = "key", Message = "Could not authenticate your avatar." };

            var response = failed.ToOSD();
            var read = new LoginResponseData();
            read.Parse(OverTheWire(response));

            Assert.Multiple(() =>
            {
                Assert.That(response.Keys, Is.EquivalentTo(new[] { "login", "reason", "message" }));
                Assert.That(response["login"].AsString(), Is.EqualTo("false"));
                Assert.That(read.Login, Is.EqualTo(LoginState.False));
                Assert.That(read.Reason, Is.EqualTo("key"));
                Assert.That(read.Message, Is.EqualTo("Could not authenticate your avatar."));
            });
        }

        [Test]
        public void ARedirect_SaysWhereToGoNext_AndIsReadOnTheLlsdPathToo()
        {
            var redirect = new LoginResponseData
            {
                Login = LoginState.Indeterminate,
                Message = "Please wait",
                NextUrl = "https://login2.example.org/",
                NextMethod = "login_to_simulator",
                NextDuration = 3,
                NextOptions = new[] { "inventory-root", "buddy-list" }
            };

            var response = redirect.ToOSD();
            var read = new LoginResponseData();
            read.Parse(OverTheWire(response));

            Assert.Multiple(() =>
            {
                Assert.That(response["login"].AsString(), Is.EqualTo("indeterminate"));
                Assert.That(response["next_url"].AsString(), Is.EqualTo("https://login2.example.org/"));
                Assert.That(read.Login, Is.EqualTo(LoginState.Indeterminate));
                Assert.That(read.NextUrl, Is.EqualTo("https://login2.example.org/"));
                Assert.That(read.NextMethod, Is.EqualTo("login_to_simulator"));
                Assert.That(read.NextDuration, Is.EqualTo(3));
                Assert.That(read.NextOptions, Is.EqualTo(new[] { "inventory-root", "buddy-list" }));
            });
        }

        [Test]
        public void ADefaultResponse_CanBeWrittenWithoutAnythingSet()
        {
            var empty = new LoginResponseData { Login = LoginState.True };

            OSDMap response = null;
            Assert.DoesNotThrow(() => response = empty.ToOSD());

            var read = new LoginResponseData();
            read.Parse(OverTheWire(response));
            Assert.That(read.Success, Is.True);
        }

        [Test]
        public void TheEntriesOfTheListsReadFromALegacyReply_AreWrittenToo()
        {
            var table = new Hashtable
            {
                ["login"] = "true",
                ["classified_categories"] = new ArrayList { new Hashtable { ["category_id"] = 1, ["category_name"] = "Shopping" } },
                ["global-textures"] = new ArrayList { new Hashtable { ["cloud_texture_id"] = LoginResponseDataTests.AgentId.ToString() } }
            };
            var legacy = new LoginResponseData();
            legacy.Parse(table);

            var read = new LoginResponseData();
            read.Parse(legacy.ToOSD());

            var category = (OSDMap)read.ClassifiedCategories.Single();
            Assert.Multiple(() =>
            {
                Assert.That(category["category_name"].AsString(), Is.EqualTo("Shopping"));
                Assert.That(category["category_id"].AsInteger(), Is.EqualTo(1));
                Assert.That(((OSDMap)read.GlobalTextures.Single())["cloud_texture_id"].AsString(), Is.EqualTo(LoginResponseDataTests.AgentId.ToString()));
            });
        }

        [Test]
        public void ARegionHandleAt_TheEdgeOfTheGrid_SurvivesAsTwoCoordinates()
        {
            var data = ParsedSuccess();
            data.Home.RegionHandle = Utils.UIntsToLong(1_000_000 * 256, 1_000_000 * 256 + 256);

            var read = new LoginResponseData();
            read.Parse(data.ToOSD());

            Assert.That(read.Home.RegionHandle, Is.EqualTo(data.Home.RegionHandle));
        }

        #endregion The response

        #region The benefits

        [Test]
        public void TheAccountLevelBenefits_WrittenAndReadBack_AreTheSame()
        {
            var original = ParsedSuccess().AccountLevelBenefits;

            var reread = new AccountLevelBenefits(original.ToOSD());

            Assert.That(Snapshot(reread), Is.EqualTo(Snapshot(original)));
        }

        [Test]
        public void TheBenefitsAGridGrants_CanBeSetWithoutParsingAnything()
        {
            var benefits = new AccountLevelBenefits { MeshUploadCost = 0, CreateGroupCost = 100, AttachmentLimit = 100, EstateAccessToken = "t", LargeTextureUploadCost = new List<int> { 10, 20 } };

            var map = benefits.ToOSD();
            var read = new AccountLevelBenefits(OverTheWire(map));

            Assert.Multiple(() =>
            {
                Assert.That(map["mesh_upload_cost"].AsInteger(), Is.EqualTo(0));
                Assert.That(read.CreateGroupCost, Is.EqualTo(100));
                Assert.That(read.AttachmentLimit, Is.EqualTo(100));
                Assert.That(read.EstateAccessToken, Is.EqualTo("t"));
                Assert.That(read.LargeTextureUploadCost, Is.EqualTo(new[] { 10, 20 }));
                Assert.That(read.Stipend, Is.EqualTo(-1), "not set");
            });
        }

        [Test]
        public void TheDefaultBenefits_SayNothing()
        {
            var benefits = new AccountLevelBenefits();

            Assert.Multiple(() =>
            {
                Assert.That(benefits.MeshUploadCost, Is.EqualTo(-1));
                Assert.That(benefits.LindenBuyFee, Is.EqualTo(-1.0));
                Assert.That(benefits.EstateAccessToken, Is.EqualTo(string.Empty));
                Assert.That(benefits.AttachmentLimit, Is.EqualTo(38));
                Assert.That(benefits.LargeTextureUploadCost, Is.Empty);
            });
        }

        #endregion The benefits

        #region The request

        private static LoginRequest SampleRequest(bool mfa) => new LoginRequest
        {
            FirstName = "Test",
            LastName = "Resident",
            Password = LoginRequest.HashPassword("secret"),
            Start = "last",
            Channel = "My Viewer",
            Version = "1.2.3",
            Platform = "Mac",
            PlatformVersion = "14.0",
            Mac = "abcdef",
            ViewerDigest = "digest",
            Id0 = "disk",
            AgreeToTos = true,
            ReadCritical = true,
            LastExecEvent = LastExecStatus.Froze,
            HasMfa = mfa,
            Token = mfa ? "123456" : string.Empty,
            MfaHash = mfa ? "saved" : string.Empty,
            Options = new List<string> { "inventory-root", "buddy-list" }
        };

        [Test]
        public void TheRequest_IsTheMapTheClientHasAlwaysPosted()
        {
            var map = SampleRequest(mfa: false).ToOSD();

            Assert.Multiple(() =>
            {
                Assert.That(map.Keys, Is.EqualTo(new[]
                {
                    "first", "last", "passwd", "start", "channel", "version", "platform", "platform_version", "mac",
                    "agree_to_tos", "read_critical", "viewer_digest", "id0", "last_exec_event", "options"
                }));
                Assert.That(map["first"].AsString(), Is.EqualTo("Test"));
                Assert.That(map["passwd"].AsString(), Does.StartWith("$1$").And.Length.EqualTo(35));
                Assert.That(map["agree_to_tos"].Type, Is.EqualTo(OSDType.Boolean));
                Assert.That(map["last_exec_event"].Type, Is.EqualTo(OSDType.Integer));
                Assert.That(map["last_exec_event"].AsInteger(), Is.EqualTo((int)LastExecStatus.Froze));
                Assert.That(((OSDArray)map["options"]).Select(o => o.AsString()), Is.EqualTo(new[] { "inventory-root", "buddy-list" }));
            });
        }

        [Test]
        public void TheMultiFactorFields_AreSentOnlyByAClientThatDoesMultiFactor()
        {
            var without = SampleRequest(mfa: false).ToOSD();
            var with = SampleRequest(mfa: true).ToOSD();

            Assert.Multiple(() =>
            {
                Assert.That(without.ContainsKey("token"), Is.False);
                Assert.That(without.ContainsKey("mfa_hash"), Is.False);
                Assert.That(with["token"].AsString(), Is.EqualTo("123456"));
                Assert.That(with["mfa_hash"].AsString(), Is.EqualTo("saved"));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ARequest_WrittenAndReadBack_IsTheSameRequest(bool mfa)
        {
            var original = SampleRequest(mfa);

            var read = LoginRequest.FromOSD(OverTheWire(original.ToOSD()));

            Assert.That(Snapshot(read), Is.EqualTo(Snapshot(original)));
        }

        [Test]
        public void ARequestWithLessInIt_IsStillRead()
        {
            var read = LoginRequest.FromOSD(new OSDMap { ["first"] = OSD.FromString("A"), ["last"] = OSD.FromString("B"), ["passwd"] = OSD.FromString("$1$x") });

            Assert.Multiple(() =>
            {
                Assert.That(read.FirstName, Is.EqualTo("A"));
                Assert.That(read.Channel, Is.EqualTo(string.Empty));
                Assert.That(read.AgreeToTos, Is.False);
                Assert.That(read.LastExecEvent, Is.EqualTo(LastExecStatus.Normal));
                Assert.That(read.HasMfa, Is.False);
                Assert.That(read.Options, Is.Empty);
            });
        }

        [Test]
        public void ThePasswordIsHashedUnlessItAlreadyIs()
        {
            var hashed = LoginRequest.HashPassword("secret");

            Assert.Multiple(() =>
            {
                Assert.That(hashed, Does.StartWith("$1$").And.Length.EqualTo(35));
                Assert.That(hashed, Is.EqualTo(Utils.MD5("secret")));
                Assert.That(LoginRequest.HashPassword(hashed), Is.EqualTo(hashed), "not hashed twice");
                Assert.That(LoginRequest.HashPassword("$1$already"), Is.EqualTo("$1$already"));
                Assert.That(LoginRequest.HashPassword(new string('x', 35)), Is.EqualTo(new string('x', 35)), "35 characters is taken for a hash, as a client always has");
                Assert.That(LoginRequest.HashPassword(new string('x', 34)), Is.EqualTo(Utils.MD5(new string('x', LoginRequest.MaxPasswordLength))));
                Assert.That(LoginRequest.HashPassword(string.Empty), Is.EqualTo(Utils.MD5(string.Empty)));
            });
        }

        [TestCase("Parting3-Regally-Unending")]
        [TestCase("Python-Nearest-Snooper8")]
        public void OnlyTheFirstSixteenCharactersOfThePasswordAreHashed(string password)
        {
            Assert.Multiple(() =>
            {
                Assert.That(LoginRequest.HashPassword(password), Is.EqualTo(Utils.MD5(password.Substring(0, 16))));
                Assert.That(LoginRequest.HashPassword("My-Password-here4"), Is.EqualTo(Utils.MD5("My-Password-here")));
                Assert.That(LoginRequest.HashPassword(new string('x', 16)), Is.EqualTo(Utils.MD5(new string('x', 16))));
            });
        }

        [TestCase("Parting3-Regally-Unending")]
        [TestCase("Python-Nearest-Snooper8")]
        public void TheWholePasswordIsHashedWhenNotTruncating(string password)
        {
            Assert.Multiple(() =>
            {
                Assert.That(LoginRequest.HashPassword(password, false), Is.EqualTo(Utils.MD5(password)));
                Assert.That(LoginRequest.HashPassword(password, false), Is.Not.EqualTo(LoginRequest.HashPassword(password, true)));
                Assert.That(LoginRequest.HashPassword(password, true), Is.EqualTo(LoginRequest.HashPassword(password)));
            });
        }

        [Test]
        public void AHashedPasswordIsNeverHashedAgainWhetherOrNotTruncating()
        {
            var hashed = Utils.MD5("Parting3-Regally-Unending");

            Assert.Multiple(() =>
            {
                Assert.That(LoginRequest.HashPassword(hashed, false), Is.EqualTo(hashed));
                Assert.That(LoginRequest.HashPassword(hashed, true), Is.EqualTo(hashed));
                Assert.That(LoginRequest.HashPassword(null, false), Is.EqualTo(string.Empty));
            });
        }

        [TestCase("https://login.agni.lindenlab.com/cgi-bin/login.cgi", true)]
        [TestCase("https://login.aditi.lindenlab.com/cgi-bin/login.cgi", true)]
        [TestCase("https://LOGIN.AGNI.LINDENLAB.COM/cgi-bin/login.cgi", true)]
        [TestCase("https://login.secondlife.com/cgi-bin/login.cgi", true)]
        [TestCase("http://127.0.0.1:9000/", false)]
        [TestCase("http://localhost:9000/", false)]
        [TestCase("https://login.osgrid.org/", false)]
        [TestCase("https://notlindenlab.com/", false)]
        [TestCase("https://lindenlab.com.example.org/", false)]
        [TestCase("login.agni.lindenlab.com", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void SecondLifeLoginServersAreRecognized(string loginUri, bool expected)
        {
            Assert.That(LoginRequest.IsSecondLifeLoginServer(loginUri), Is.EqualTo(expected));
        }

        #endregion The request
    }
}
