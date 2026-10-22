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
using System.Linq;
using System.Net;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// What a login server answers with, as <see cref="LoginResponseData"/> reads it. The response here is shaped as the
    /// viewer reads it (indra/newview/llstartup.cpp, process_login_success_response): <c>look_at</c> and <c>home</c> are
    /// strings in LLSD notation, <c>inventory-root</c> is an array of one map, and so on.
    /// </summary>
    [TestFixture]
    [Category("Login")]
    public class LoginResponseDataTests
    {
        internal static readonly UUID AgentId = UUID.Parse("11111111-1111-1111-1111-111111111111");
        internal static readonly UUID SessionId = UUID.Parse("22222222-2222-2222-2222-222222222222");
        internal static readonly UUID SecureSessionId = UUID.Parse("33333333-3333-3333-3333-333333333333");
        internal static readonly UUID InventoryRootId = UUID.Parse("44444444-4444-4444-4444-444444444444");
        internal static readonly UUID LibraryRootId = UUID.Parse("55555555-5555-5555-5555-555555555555");
        internal static readonly UUID LibraryOwnerId = UUID.Parse("66666666-6666-6666-6666-666666666666");
        internal static readonly UUID BuddyId = UUID.Parse("77777777-7777-7777-7777-777777777777");
        internal static readonly UUID OutfitItemId = UUID.Parse("88888888-8888-8888-8888-888888888888");
        internal static readonly UUID GestureItemId = UUID.Parse("99999999-9999-9999-9999-999999999999");
        internal static readonly UUID GestureAssetId = UUID.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private static OSDMap Folder(string name, UUID id, UUID parent, int type, int version) => new OSDMap
        {
            ["folder_id"] = OSD.FromUUID(id),
            ["parent_id"] = OSD.FromUUID(parent),
            ["name"] = OSD.FromString(name),
            ["type_default"] = OSD.FromInteger(type),
            ["version"] = OSD.FromInteger(version)
        };

        /// <summary>A successful response in the shape the viewer reads.</summary>
        internal static OSDMap SuccessResponse()
        {
            var map = new OSDMap
            {
                ["login"] = OSD.FromString("true"),
                ["agent_id"] = OSD.FromUUID(AgentId),
                ["session_id"] = OSD.FromUUID(SessionId),
                ["secure_session_id"] = OSD.FromUUID(SecureSessionId),
                ["first_name"] = OSD.FromString("\"Test\""),
                ["last_name"] = OSD.FromString("Resident"),
                ["start_location"] = OSD.FromString("last"),
                ["agent_access"] = OSD.FromString("M"),
                ["agent_access_max"] = OSD.FromString("A"),
                ["agent_region_access"] = OSD.FromString("M"),
                ["look_at"] = OSD.FromString("[r0.99,r0.1,r0]"),
                ["home"] = OSD.FromString("{'region_handle':[r256000,r256512], 'position':[r128,r64,r25.5], 'look_at':[r1,r0,r0]}"),
                ["message"] = OSD.FromString("Welcome to the grid"),
                ["reason"] = OSD.FromString(""),
                ["circuit_code"] = OSD.FromInteger(123456),
                ["region_x"] = OSD.FromInteger(256000),
                ["region_y"] = OSD.FromInteger(256512),
                ["region_size_x"] = OSD.FromInteger(512),
                ["region_size_y"] = OSD.FromInteger(512),
                ["sim_ip"] = OSD.FromString("10.1.2.3"),
                ["sim_port"] = OSD.FromInteger(13000),
                ["seed_capability"] = OSD.FromString("https://sim.example.org:12043/cap/abcd"),
                ["seconds_since_epoch"] = OSD.FromInteger(1700000000),
                ["account_type"] = OSD.FromString("Premium"),
                ["agent_appearance_service"] = OSD.FromString("https://appearance.example.org/"),
                ["cof_version"] = OSD.FromInteger(7),
                ["map-server-url"] = OSD.FromString("https://map.example.org/"),
                ["openid_url"] = OSD.FromString("https://openid.example.org/"),
                ["snapshot_config_url"] = OSD.FromString("https://snap.example.org/"),
                ["udp_blacklist"] = OSD.FromString("EnableSimulator,TeleportFinish"),
                ["mfa_hash"] = OSD.FromString("hash-value"),
                ["max-agent-groups"] = OSD.FromInteger(42),
                ["inventory-root"] = new OSDArray { new OSDMap { ["folder_id"] = OSD.FromUUID(InventoryRootId) } },
                ["inventory-skeleton"] = new OSDArray
                {
                    Folder("My Inventory", InventoryRootId, UUID.Zero, 8, 12),
                    Folder("Clothing", UUID.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), InventoryRootId, 5, 3)
                },
                ["inventory-lib-owner"] = new OSDArray { new OSDMap { ["agent_id"] = OSD.FromUUID(LibraryOwnerId) } },
                ["inventory-lib-root"] = new OSDArray { new OSDMap { ["folder_id"] = OSD.FromUUID(LibraryRootId) } },
                ["inventory-skel-lib"] = new OSDArray { Folder("Library", LibraryRootId, UUID.Zero, 8, 1) },
                ["buddy-list"] = new OSDArray
                {
                    new OSDMap { ["buddy_id"] = OSD.FromString(BuddyId.ToString()), ["buddy_rights_given"] = OSD.FromInteger(3), ["buddy_rights_has"] = OSD.FromInteger(1) }
                },
                ["initial-outfit"] = new OSDArray { new OSDMap { ["item_id"] = OSD.FromString(OutfitItemId.ToString()) } },
                ["gestures"] = new OSDArray { new OSDMap { ["item_id"] = OSD.FromUUID(GestureItemId), ["asset_id"] = OSD.FromUUID(GestureAssetId) } },
                ["classified_categories"] = new OSDArray { new OSDMap { ["category_id"] = OSD.FromInteger(1), ["category_name"] = OSD.FromString("Shopping") } },
                ["event_categories"] = new OSDArray { new OSDMap { ["category_id"] = OSD.FromInteger(2), ["category_name"] = OSD.FromString("Music") } },
                ["global-textures"] = new OSDArray { new OSDMap { ["cloud_texture_id"] = OSD.FromUUID(UUID.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")) } },
                ["ui-config"] = new OSDArray { new OSDMap { ["allow_first_life"] = OSD.FromString("Y") } },
                ["premium_packages"] = new OSDMap { ["package_name"] = OSD.FromString("Premium") },
                ["account_level_benefits"] = new OSDMap
                {
                    ["mesh_upload_cost"] = OSD.FromInteger(10),
                    ["texture_upload_cost"] = OSD.FromInteger(10),
                    ["create_group_cost"] = OSD.FromInteger(100),
                    ["linden_buy_fee"] = OSD.FromReal(0.5),
                    ["estate_access_token"] = OSD.FromString("token"),
                    ["large_texture_upload_cost"] = new OSDArray { OSD.FromInteger(40), OSD.FromInteger(10), OSD.FromInteger(20) },
                    ["attachment_limit"] = OSD.FromInteger(50)
                }
            };
            return map;
        }

        private static LoginResponseData Parsed(OSDMap map)
        {
            var data = new LoginResponseData();
            data.Parse(map);
            return data;
        }

        #region A successful response

        [Test]
        public void ASuccessfulResponse_IsRead()
        {
            var d = Parsed(SuccessResponse());

            Assert.Multiple(() =>
            {
                Assert.That(d.Login, Is.EqualTo(LoginState.True));
                Assert.That(d.Success, Is.True);
                Assert.That(d.AgentID, Is.EqualTo(AgentId));
                Assert.That(d.SessionID, Is.EqualTo(SessionId));
                Assert.That(d.SecureSessionID, Is.EqualTo(SecureSessionId));
                Assert.That(d.FirstName, Is.EqualTo("Test"), "the quotes SL puts round it are taken off");
                Assert.That(d.LastName, Is.EqualTo("Resident"));
                Assert.That(d.StartLocation, Is.EqualTo("last"));
                Assert.That(d.AgentAccess, Is.EqualTo("M"));
                Assert.That(d.AgentAccessMax, Is.EqualTo("A"));
                Assert.That(d.AgentRegionAccess, Is.EqualTo("M"));
                Assert.That(d.LookAt, Is.EqualTo(new Vector3(0.99f, 0.1f, 0f)));
                Assert.That(d.Message, Is.EqualTo("Welcome to the grid"));
                Assert.That(d.Reason, Is.EqualTo(string.Empty));
                Assert.That(d.CircuitCode, Is.EqualTo(123456));
                Assert.That(d.RegionX, Is.EqualTo(256000u));
                Assert.That(d.RegionY, Is.EqualTo(256512u));
                Assert.That(d.RegionSizeX, Is.EqualTo(512u));
                Assert.That(d.RegionSizeY, Is.EqualTo(512u));
                Assert.That(d.SimIP, Is.EqualTo(IPAddress.Parse("10.1.2.3")));
                Assert.That(d.SimPort, Is.EqualTo((ushort)13000));
                Assert.That(d.SeedCapability, Is.EqualTo("https://sim.example.org:12043/cap/abcd"));
                Assert.That(d.SecondsSinceEpoch, Is.EqualTo(1700000000));
            });
        }

        [Test]
        public void TheHome_IsReadFromItsNotationString()
        {
            var d = Parsed(SuccessResponse());

            Assert.Multiple(() =>
            {
                Assert.That(d.Home.RegionHandle, Is.EqualTo(Utils.UIntsToLong(256000, 256512)));
                Assert.That(d.Home.Position, Is.EqualTo(new Vector3(128f, 64f, 25.5f)));
                Assert.That(d.Home.LookAt, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            });
        }

        [Test]
        public void TheHome_IsAlsoReadFromAHomeInfoMap()
        {
            var map = SuccessResponse();
            map.Remove("home");
            map["home_info"] = new OSDMap
            {
                ["region_handle"] = OSD.FromString("[r256000,r256512]"),
                ["position"] = new OSDArray { OSD.FromReal(1), OSD.FromReal(2), OSD.FromReal(3) },
                ["look_at"] = new OSDArray { OSD.FromReal(0), OSD.FromReal(1), OSD.FromReal(0) }
            };

            var d = Parsed(map);

            Assert.Multiple(() =>
            {
                Assert.That(d.Home.RegionHandle, Is.EqualTo(Utils.UIntsToLong(256000, 256512)));
                Assert.That(d.Home.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(d.Home.LookAt, Is.EqualTo(Vector3.UnitY));
            });
        }

        [Test]
        public void LookAt_CanBeAnArrayAsWellAsAString()
        {
            var map = SuccessResponse();
            map["look_at"] = new OSDArray { OSD.FromReal(0.5), OSD.FromReal(0.25), OSD.FromReal(1) };

            Assert.That(Parsed(map).LookAt, Is.EqualTo(new Vector3(0.5f, 0.25f, 1f)));
        }

        [Test]
        public void TheInventory_IsReadWithTheFoldersOwnedByTheAgentOrTheLibraryOwner()
        {
            var d = Parsed(SuccessResponse());

            Assert.Multiple(() =>
            {
                Assert.That(d.InventoryRoot, Is.EqualTo(InventoryRootId));
                Assert.That(d.LibraryRoot, Is.EqualTo(LibraryRootId));
                Assert.That(d.LibraryOwner, Is.EqualTo(LibraryOwnerId));
                Assert.That(d.InventorySkeleton, Has.Length.EqualTo(2));
                Assert.That(d.InventorySkeleton[0].Name, Is.EqualTo("My Inventory"));
                Assert.That(d.InventorySkeleton[0].UUID, Is.EqualTo(InventoryRootId));
                Assert.That(d.InventorySkeleton[0].ParentUUID, Is.EqualTo(UUID.Zero));
                Assert.That(d.InventorySkeleton[0].PreferredType, Is.EqualTo((FolderType)8));
                Assert.That(d.InventorySkeleton[0].Version, Is.EqualTo(12));
                Assert.That(d.InventorySkeleton[1].Name, Is.EqualTo("Clothing"));
                Assert.That(d.InventorySkeleton[1].ParentUUID, Is.EqualTo(InventoryRootId));
                Assert.That(d.InventorySkeleton.All(f => f.OwnerID == AgentId), Is.True);
                Assert.That(d.LibrarySkeleton, Has.Length.EqualTo(1));
                Assert.That(d.LibrarySkeleton[0].OwnerID, Is.EqualTo(LibraryOwnerId));
            });
        }

        [Test]
        public void TheBuddyListAndTheInitialOutfit_AreRead()
        {
            var d = Parsed(SuccessResponse());

            Assert.Multiple(() =>
            {
                Assert.That(d.BuddyList, Has.Length.EqualTo(1));
                Assert.That(d.BuddyList[0].BuddyId, Is.EqualTo(BuddyId.ToString()));
                Assert.That(d.BuddyList[0].BuddyRightsGiven, Is.EqualTo(3));
                Assert.That(d.BuddyList[0].BuddyRightsHas, Is.EqualTo(1));
                Assert.That(d.InitialOutfit, Is.EqualTo(OutfitItemId.ToString()));
            });
        }

        [Test]
        public void TheActiveGestures_AreRead()
        {
            var d = Parsed(SuccessResponse());

            Assert.That(d.Gestures, Has.Count.EqualTo(1));
            Assert.That(d.Gestures[GestureItemId], Is.EqualTo(GestureAssetId));
        }

        [Test]
        public void AReplyWithoutGestures_HasNone()
        {
            var response = SuccessResponse();
            response.Remove("gestures");

            Assert.That(Parsed(response).Gestures, Is.Empty);
        }

        [Test]
        public void TheOptionalFields_AreRead()
        {
            var d = Parsed(SuccessResponse());

            Assert.Multiple(() =>
            {
                Assert.That(d.AccountType, Is.EqualTo("Premium"));
                Assert.That(d.AgentAppearanceServiceURL, Is.EqualTo("https://appearance.example.org/"));
                Assert.That(d.COFVersion, Is.EqualTo(7u));
                Assert.That(d.MapServerUrl, Is.EqualTo("https://map.example.org/"));
                Assert.That(d.OpenIDUrl, Is.EqualTo("https://openid.example.org/"));
                Assert.That(d.SnapshotConfigUrl, Is.EqualTo("https://snap.example.org/"));
                Assert.That(d.UDPBlacklist, Is.EqualTo("EnableSimulator,TeleportFinish"));
                Assert.That(d.MfaHash, Is.EqualTo("hash-value"));
                Assert.That(d.MaxAgentGroups, Is.EqualTo(42));
                Assert.That(d.ClassifiedCategories, Has.Count.EqualTo(1));
                Assert.That(d.EventCategories, Has.Count.EqualTo(1));
                Assert.That(d.GlobalTextures, Has.Count.EqualTo(1));
                Assert.That(d.UiConfig, Has.Count.EqualTo(1));
                Assert.That(d.PremiumPackages, Contains.Key("package_name"));
            });
        }

        [Test]
        public void TheAccountLevelBenefits_AreRead_WithTheDefaultsForWhatIsMissing()
        {
            var b = Parsed(SuccessResponse()).AccountLevelBenefits;

            Assert.That(b, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(b.MeshUploadCost, Is.EqualTo(10));
                Assert.That(b.TextureUploadCost, Is.EqualTo(10));
                Assert.That(b.CreateGroupCost, Is.EqualTo(100));
                Assert.That(b.LindenBuyFee, Is.EqualTo(0.5));
                Assert.That(b.EstateAccessToken, Is.EqualTo("token"));
                Assert.That(b.AttachmentLimit, Is.EqualTo(50));
                Assert.That(b.LargeTextureUploadCost, Is.EqualTo(new[] { 10, 20, 40 }), "sorted");
                Assert.That(b.Stipend, Is.EqualTo(-1), "not given");
                Assert.That(b.LastnameChangeCost, Is.EqualTo(-1.0), "not given");
            });
        }

        [Test]
        public void AccountLevelBenefitsWithNoAttachmentLimit_AllowThirtyEight_AndTheLargeTextureCostDefaultsToTheTextureCost()
        {
            var b = new AccountLevelBenefits(new OSDMap { ["texture_upload_cost"] = OSD.FromInteger(10) });

            Assert.That(b.AttachmentLimit, Is.EqualTo(38));
            Assert.That(b.LargeTextureUploadCost, Is.EqualTo(new[] { 10 }));
        }

        #endregion A successful response

        #region Other responses

        [Test]
        public void AFailedLogin_GivesTheReasonAndMessage_AndTheRestIsLeftAlone()
        {
            var d = Parsed(new OSDMap
            {
                ["login"] = OSD.FromString("false"),
                ["reason"] = OSD.FromString("key"),
                ["message"] = OSD.FromString("Could not authenticate your avatar.")
            });

            Assert.Multiple(() =>
            {
                Assert.That(d.Login, Is.EqualTo(LoginState.False));
                Assert.That(d.Success, Is.False);
                Assert.That(d.Reason, Is.EqualTo("key"));
                Assert.That(d.Message, Is.EqualTo("Could not authenticate your avatar."));
                Assert.That(d.AgentID, Is.EqualTo(UUID.Zero));
                Assert.That(d.SimIP, Is.Null);
                Assert.That(d.InventorySkeleton, Is.Empty);
            });
        }

        [TestCase("true", LoginState.True)]
        [TestCase("TRUE", LoginState.True)]
        [TestCase("false", LoginState.False)]
        [TestCase("indeterminate", LoginState.Indeterminate)]
        [TestCase("Indeterminate", LoginState.Indeterminate)]
        [TestCase("nonsense", LoginState.False)]
        public void TheLoginStateIsReadFromItsString(string text, LoginState expected)
        {
            Assert.That(Parsed(new OSDMap { ["login"] = OSD.FromString(text) }).Login, Is.EqualTo(expected));
        }

        [Test]
        public void ARegionOfDefaultSize_IsAssumedWhenTheSizeIsNotGiven_AndMaxGroupsIsMinusOne()
        {
            var map = SuccessResponse();
            map.Remove("region_size_x");
            map.Remove("region_size_y");
            map.Remove("max-agent-groups");

            var d = Parsed(map);

            Assert.Multiple(() =>
            {
                Assert.That(d.RegionSizeX, Is.EqualTo(256u));
                Assert.That(d.RegionSizeY, Is.EqualTo(256u));
                Assert.That(d.MaxAgentGroups, Is.EqualTo(-1));
            });
        }

        [Test]
        public void ASimAddressThatIsNotAnAddress_GivesNone()
        {
            var map = SuccessResponse();
            map["sim_ip"] = OSD.FromString("not an address");

            Assert.That(Parsed(map).SimIP, Is.Null);
        }

        [Test]
        public void KeysThatAreNotKnown_AreIgnored()
        {
            var map = SuccessResponse();
            map["something_new"] = new OSDMap { ["a"] = OSD.FromInteger(1) };

            Assert.That(Parsed(map).AgentID, Is.EqualTo(AgentId));
        }

        [Test]
        public void AHomeThatIsNotNotation_GivesNoHome_AndTheRestIsKept()
        {
            var map = SuccessResponse();
            map["home"] = OSD.FromString("this is not notation {{{");

            var d = Parsed(map);

            Assert.Multiple(() =>
            {
                Assert.That(d.Home.RegionHandle, Is.EqualTo(0ul));
                Assert.That(d.Home.Position, Is.EqualTo(Vector3.Zero));
                Assert.That(d.AgentID, Is.EqualTo(AgentId));
                Assert.That(d.SimIP, Is.EqualTo(IPAddress.Parse("10.1.2.3")));
            });
        }

        [Test]
        public void ParseGivesACopyOfWhatItParsed_AsWellAsFillingTheInstance()
        {
            var data = new LoginResponseData();

            var result = data.Parse(SuccessResponse());

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ParsedData, Is.Not.Null.And.Not.SameAs(data));
                Assert.That(result.ParsedData.AgentID, Is.EqualTo(AgentId));
                Assert.That(result.ParsedData.InventorySkeleton, Has.Length.EqualTo(2));
                Assert.That(data.AgentID, Is.EqualTo(AgentId));
            });
        }

        #endregion Other responses

        #region The legacy XML-RPC reply

        private static Hashtable LegacySuccess() => new Hashtable
        {
            ["login"] = "true",
            ["agent_id"] = AgentId.ToString(),
            ["session_id"] = SessionId.ToString(),
            ["secure_session_id"] = SecureSessionId.ToString(),
            ["first_name"] = "\"Test\"",
            ["last_name"] = "Resident",
            ["look_at"] = "[r0.99,r0.1,r0]",
            ["home"] = "{'region_handle':[r256000,r256512], 'position':[r128,r64,r25.5], 'look_at':[r1,r0,r0]}",
            ["circuit_code"] = 123456,
            ["region_x"] = 256000,
            ["region_y"] = 256512,
            ["sim_ip"] = "10.1.2.3",
            ["sim_port"] = 13000,
            ["seed_capability"] = "https://sim.example.org/cap/abcd",
            ["seconds_since_epoch"] = 1700000000,
            ["message"] = "Welcome",
            ["inventory-root"] = new ArrayList { new Hashtable { ["folder_id"] = InventoryRootId.ToString() } },
            ["inventory-skeleton"] = new ArrayList
            {
                new Hashtable { ["folder_id"] = InventoryRootId.ToString(), ["parent_id"] = UUID.Zero.ToString(), ["name"] = "My Inventory", ["type_default"] = 8, ["version"] = 5 }
            },
            ["buddy-list"] = new ArrayList
            {
                new Hashtable { ["buddy_id"] = BuddyId.ToString(), ["buddy_rights_given"] = 3, ["buddy_rights_has"] = 1 }
            },
            ["initial-outfit"] = new ArrayList { new Hashtable { ["item_id"] = OutfitItemId.ToString() } },
            ["gestures"] = new ArrayList { new Hashtable { ["item_id"] = GestureItemId.ToString(), ["asset_id"] = GestureAssetId.ToString() } }
        };

        [Test]
        public void ALegacyReply_IsRead()
        {
            var d = new LoginResponseData();
            d.Parse(LegacySuccess());

            Assert.Multiple(() =>
            {
                Assert.That(d.Success, Is.True);
                Assert.That(d.AgentID, Is.EqualTo(AgentId));
                Assert.That(d.FirstName, Is.EqualTo("Test"));
                Assert.That(d.LookAt, Is.EqualTo(new Vector3(0.99f, 0.1f, 0f)));
                Assert.That(d.Home.RegionHandle, Is.EqualTo(Utils.UIntsToLong(256000, 256512)));
                Assert.That(d.Home.Position, Is.EqualTo(new Vector3(128f, 64f, 25.5f)));
                Assert.That(d.CircuitCode, Is.EqualTo(123456));
                Assert.That(d.SimIP, Is.EqualTo(IPAddress.Parse("10.1.2.3")));
                Assert.That(d.SimPort, Is.EqualTo((ushort)13000));
                Assert.That(d.InventoryRoot, Is.EqualTo(InventoryRootId));
                Assert.That(d.InventorySkeleton.Single().Name, Is.EqualTo("My Inventory"));
                Assert.That(d.BuddyList.Single().BuddyId, Is.EqualTo(BuddyId.ToString()));
                Assert.That(d.InitialOutfit, Is.EqualTo(OutfitItemId.ToString()));
                Assert.That(d.Gestures[GestureItemId], Is.EqualTo(GestureAssetId));
            });
        }

        [Test]
        public void ALegacyReplyThatFailed_IsNotReadBeyondItsReason()
        {
            var table = LegacySuccess();
            table["login"] = "false";
            table["reason"] = "presence";

            var d = new LoginResponseData();
            d.Parse(table);

            Assert.Multiple(() =>
            {
                Assert.That(d.Success, Is.False);
                Assert.That(d.Reason, Is.EqualTo("presence"));
                Assert.That(d.SimIP, Is.Null, "not read after a failure");
                Assert.That(d.InventorySkeleton, Is.Empty);
            });
        }

        [Test]
        public void ALegacyRedirect_GivesWhereToGoNext()
        {
            var table = new Hashtable
            {
                ["login"] = "indeterminate",
                ["next_url"] = "https://login2.example.org/",
                ["next_method"] = "login_to_simulator",
                ["next_duration"] = 5,
                ["next_options"] = new ArrayList { "inventory-root", "buddy-list" },
                ["message"] = "Please wait"
            };

            var d = new LoginResponseData();
            d.Parse(table);

            Assert.Multiple(() =>
            {
                Assert.That(d.Login, Is.EqualTo(LoginState.Indeterminate));
                Assert.That(d.NextUrl, Is.EqualTo("https://login2.example.org/"));
                Assert.That(d.NextMethod, Is.EqualTo("login_to_simulator"));
                Assert.That(d.NextDuration, Is.EqualTo(5));
                Assert.That(d.NextOptions, Is.EqualTo(new[] { "inventory-root", "buddy-list" }));
            });
        }

        #endregion The legacy XML-RPC reply
    }
}
