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

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("LocationParser")]
    public class SlurlParserTests
    {
        [Test]
        public void ParseSimpleLocation_ParsesCorrectly()
        {
            var parser = new SlurlParser("Hooper/128/128/32");
            
            Assert.That(parser.Sim, Is.EqualTo("Hooper"));
            Assert.That(parser.X, Is.EqualTo(128));
            Assert.That(parser.Y, Is.EqualTo(128));
            Assert.That(parser.Z, Is.EqualTo(32));
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Location));
            Assert.That(parser.IsLocation, Is.True);
            Assert.That(parser.IsApplication, Is.False);
        }

        [Test]
        public void ParseSlurl_ParsesCorrectly()
        {
            var parser = new SlurlParser("secondlife://Hooper/179/18/32");
            
            Assert.That(parser.Sim, Is.EqualTo("Hooper"));
            Assert.That(parser.X, Is.EqualTo(179));
            Assert.That(parser.Y, Is.EqualTo(18));
            Assert.That(parser.Z, Is.EqualTo(32));
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Location));
        }

        [Test]
        public void ParseSlurlWithTrailingSlash_ParsesCorrectly()
        {
            var parser = new SlurlParser("secondlife://Hooper/179/18/32/");
            
            Assert.That(parser.Sim, Is.EqualTo("Hooper"));
            Assert.That(parser.X, Is.EqualTo(179));
            Assert.That(parser.Y, Is.EqualTo(18));
            Assert.That(parser.Z, Is.EqualTo(32));
        }

        [Test]
        public void ParseLegacyUri_ParsesCorrectly()
        {
            var parser = new SlurlParser("uri:Hooper&179&18&32");
            
            Assert.That(parser.Sim, Is.EqualTo("Hooper"));
            Assert.That(parser.X, Is.EqualTo(179));
            Assert.That(parser.Y, Is.EqualTo(18));
            Assert.That(parser.Z, Is.EqualTo(32));
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Location));
        }

        [Test]
        public void ParseLocationWithDefaults_UsesDefaultCoordinates()
        {
            var parser = new SlurlParser("secondlife://Hooper");
            
            Assert.That(parser.Sim, Is.EqualTo("Hooper"));
            Assert.That(parser.X, Is.EqualTo(128));
            Assert.That(parser.Y, Is.EqualTo(128));
            Assert.That(parser.Z, Is.EqualTo(0));
        }

        [Test]
        public void ParseLocationPartialCoordinates_UsesDefaults()
        {
            var parser = new SlurlParser("secondlife://Hooper/100");
            
            Assert.That(parser.Sim, Is.EqualTo("Hooper"));
            Assert.That(parser.X, Is.EqualTo(100));
            Assert.That(parser.Y, Is.EqualTo(128));
            Assert.That(parser.Z, Is.EqualTo(0));
        }

        [Test]
        public void ParseAgentSlapp_ParsesCorrectly()
        {
            var agentId = UUID.Random();
            var parser = new SlurlParser($"secondlife:///app/agent/{agentId}/about");
            
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Application));
            Assert.That(parser.Command, Is.EqualTo(SlappCommand.Agent));
            Assert.That(parser.CommandPath, Is.EqualTo($"agent/{agentId}/about"));
            Assert.That(parser.IsApplication, Is.True);
            Assert.That(parser.IsLocation, Is.False);
        }

        [Test]
        public void ParseGroupSlapp_ParsesCorrectly()
        {
            var groupId = UUID.Random();
            var parser = new SlurlParser($"secondlife:///app/group/{groupId}/about");
            
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Application));
            Assert.That(parser.Command, Is.EqualTo(SlappCommand.Group));
            Assert.That(parser.CommandPath, Is.EqualTo($"group/{groupId}/about"));
        }

        [Test]
        public void ParseTeleportSlapp_ParsesLocationData()
        {
            var parser = new SlurlParser("secondlife:///app/teleport/Hooper/100/200/50");
            
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Application));
            Assert.That(parser.Command, Is.EqualTo(SlappCommand.Teleport));
            Assert.That(parser.Sim, Is.EqualTo("Hooper"));
            Assert.That(parser.X, Is.EqualTo(100));
            Assert.That(parser.Y, Is.EqualTo(200));
            Assert.That(parser.Z, Is.EqualTo(50));
        }

        [Test]
        public void ParseWorldMapSlapp_ParsesLocationData()
        {
            var parser = new SlurlParser("secondlife:///app/worldmap/Sandbox/128/128/0");
            
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Application));
            Assert.That(parser.Command, Is.EqualTo(SlappCommand.WorldMap));
            Assert.That(parser.Sim, Is.EqualTo("Sandbox"));
            Assert.That(parser.X, Is.EqualTo(128));
            Assert.That(parser.Y, Is.EqualTo(128));
            Assert.That(parser.Z, Is.EqualTo(0));
        }

        [Test]
        public void ParseLoginSlapp_ParsesQueryParameters()
        {
            var parser = new SlurlParser("secondlife:///app/login?last=Resident&session=abc123&location=home");
            
            Assert.That(parser.UriType, Is.EqualTo(ViewerUriType.Application));
            Assert.That(parser.Command, Is.EqualTo(SlappCommand.Login));
            Assert.That(parser.QueryParameters.ContainsKey("last"), Is.True);
            Assert.That(parser.QueryParameters["last"], Is.EqualTo("Resident"));
            Assert.That(parser.QueryParameters["session"], Is.EqualTo("abc123"));
            Assert.That(parser.QueryParameters["location"], Is.EqualTo("home"));
        }

        [Test]
        public void ParseObjectImSlapp_ParsesQueryParameters()
        {
            var objId = UUID.Random();
            var ownerId = UUID.Random();
            var url = $"secondlife:///app/objectim/{objId}?name=TestObject&owner={ownerId}&groupowned=true&slurl=Hooper/128/128/25";
            var parser = new SlurlParser(url);
            
            Assert.That(parser.Command, Is.EqualTo(SlappCommand.ObjectIm));
            Assert.That(parser.QueryParameters["name"], Is.EqualTo("TestObject"));
            Assert.That(parser.QueryParameters["owner"], Is.EqualTo(ownerId.ToString()));
            Assert.That(parser.QueryParameters["groupowned"], Is.EqualTo("true"));
            Assert.That(parser.QueryParameters["slurl"], Is.EqualTo("Hooper/128/128/25"));
        }

        [Test]
        public void ParseSearchSlapp_ParsesCorrectly()
        {
            var parser = new SlurlParser("secondlife:///app/search/places/Sandbox");
            
            Assert.That(parser.Command, Is.EqualTo(SlappCommand.Search));
            Assert.That(parser.CommandPath, Does.Contain("places"));
            Assert.That(parser.CommandPath, Does.Contain("Sandbox"));
        }

        [Test]
        public void GetRawLocation_ReturnsCorrectFormat()
        {
            var parser = new SlurlParser("Hooper/100/200/50");
            var raw = parser.GetRawLocation();
            
            Assert.That(raw, Is.EqualTo("Hooper/100/200/50"));
        }

        [Test]
        public void GetSlurl_ReturnsCorrectFormat()
        {
            var parser = new SlurlParser("Hooper/100/200/50");
            var slurl = parser.GetSlurl();
            
            Assert.That(slurl, Is.EqualTo("secondlife://Hooper/100/200/50/"));
        }

        [Test]
        public void GetStartLocationUri_ReturnsCorrectFormat()
        {
            var parser = new SlurlParser("Hooper/100/200/50");
            var uri = parser.GetStartLocationUri();
            
            Assert.That(uri, Is.EqualTo("uri:Hooper&100&200&50"));
        }

        [Test]
        public void GetAgentUrl_GeneratesCorrectUrl()
        {
            var agentId = UUID.Random();
            var url = SlurlParser.GetAgentUrl(agentId, "inspect");
            
            Assert.That(url, Is.EqualTo($"secondlife:///app/agent/{agentId}/inspect"));
        }

        [Test]
        public void GetAgentUrl_DefaultAction_UsesAbout()
        {
            var agentId = UUID.Random();
            var url = SlurlParser.GetAgentUrl(agentId);
            
            Assert.That(url, Does.Contain("/about"));
        }

        [Test]
        public void GetGroupUrl_GeneratesCorrectUrl()
        {
            var groupId = UUID.Random();
            var url = SlurlParser.GetGroupUrl(groupId, "inspect");
            
            Assert.That(url, Is.EqualTo($"secondlife:///app/group/{groupId}/inspect"));
        }

        [Test]
        public void GetTeleportUrl_GeneratesCorrectUrl()
        {
            var url = SlurlParser.GetTeleportUrl("Hooper", 100, 200, 50);
            
            Assert.That(url, Is.EqualTo("secondlife:///app/teleport/Hooper/100/200/50"));
        }

        [Test]
        public void GetTeleportUrl_WithDefaults_UsesDefaultCoordinates()
        {
            var url = SlurlParser.GetTeleportUrl("Hooper");
            
            Assert.That(url, Is.EqualTo("secondlife:///app/teleport/Hooper/128/128/0"));
        }

        [Test]
        public void GetWorldMapUrl_GeneratesCorrectUrl()
        {
            var url = SlurlParser.GetWorldMapUrl("Sandbox", 64, 64, 100);
            
            Assert.That(url, Is.EqualTo("secondlife:///app/worldmap/Sandbox/64/64/100"));
        }

        [Test]
        public void GetObjectImUrl_GeneratesCorrectUrl()
        {
            var objectId = UUID.Random();
            var ownerId = UUID.Random();
            var url = SlurlParser.GetObjectImUrl(objectId, "Test Object", ownerId, true, "Hooper/128/128/25");
            
            Assert.That(url, Does.Contain($"objectim/{objectId}"));
            Assert.That(url, Does.Contain("name=Test%20Object"));
            Assert.That(url, Does.Contain($"owner={ownerId}"));
            Assert.That(url, Does.Contain("groupowned=true"));
            Assert.That(url, Does.Contain("slurl=Hooper"));
        }

        [Test]
        public void GetObjectImUrl_NotGroupOwned_OmitsGroupOwnedParameter()
        {
            var objectId = UUID.Random();
            var ownerId = UUID.Random();
            var url = SlurlParser.GetObjectImUrl(objectId, "Test Object", ownerId, false, "Hooper/128/128/25");
            
            Assert.That(url, Does.Not.Contain("groupowned"));
        }

        [Test]
        public void GetSearchUrl_GeneratesCorrectUrl()
        {
            var url = SlurlParser.GetSearchUrl("places", "sandbox island");
            
            Assert.That(url, Is.EqualTo("secondlife:///app/search/places/sandbox%20island"));
        }

        [Test]
        public void GetLoginUrl_WithAllParameters_GeneratesCorrectUrl()
        {
            var url = SlurlParser.GetLoginUrl("Resident", "session123", "home");
            
            Assert.That(url, Does.Contain("login?"));
            Assert.That(url, Does.Contain("last=Resident"));
            Assert.That(url, Does.Contain("session=session123"));
            Assert.That(url, Does.Contain("location=home"));
        }

        [Test]
        public void GetLoginUrl_WithNoParameters_GeneratesBaseUrl()
        {
            var url = SlurlParser.GetLoginUrl();
            
            Assert.That(url, Is.EqualTo("secondlife:///app/login"));
        }

        [Test]
        public void GetSlappUrl_WithQueryParams_GeneratesCorrectUrl()
        {
            var queryParams = new System.Collections.Generic.Dictionary<string, string>
            {
                { "test", "value" },
                { "foo", "bar" }
            };
            
            var url = SlurlParser.GetSlappUrl(SlappCommand.Help, "topic", queryParams);
            
            Assert.That(url, Does.Contain("help/topic"));
            Assert.That(url, Does.Contain("test=value"));
            Assert.That(url, Does.Contain("foo=bar"));
        }

        [Test]
        public void ParseNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SlurlParser(null));
        }

        [Test]
        public void ParseEmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new SlurlParser(""));
        }

        [Test]
        public void ParseUrlEncodedQueryParams_DecodesCorrectly()
        {
            var parser = new SlurlParser("secondlife:///app/search/places/sandbox%20island");
            
            // The path should still be URL encoded in CommandPath
            Assert.That(parser.CommandPath, Does.Contain("sandbox%20island"));
        }

        [Test]
        public void RoundTripLocation_PreservesData()
        {
            var original = new SlurlParser("Hooper/100/200/50");
            var slurl = original.GetSlurl();
            var parsed = new SlurlParser(slurl);
            
            Assert.That(parsed.Sim, Is.EqualTo(original.Sim));
            Assert.That(parsed.X, Is.EqualTo(original.X));
            Assert.That(parsed.Y, Is.EqualTo(original.Y));
            Assert.That(parsed.Z, Is.EqualTo(original.Z));
        }

        [Test]
        public void ParseAllSlappCommands_RecognizesCorrectly()
        {
            var commands = new[]
            {
                ("secondlife:///app/agent/uuid/about", SlappCommand.Agent),
                ("secondlife:///app/appearance/show", SlappCommand.Appearance),
                ("secondlife:///app/balance/request", SlappCommand.Balance),
                ("secondlife:///app/chat/1/hello", SlappCommand.Chat),
                ("secondlife:///app/classified/uuid/about", SlappCommand.Classified),
                ("secondlife:///app/event/uuid/about", SlappCommand.Event),
                ("secondlife:///app/experience/uuid/profile", SlappCommand.Experience),
                ("secondlife:///app/group/uuid/about", SlappCommand.Group),
                ("secondlife:///app/help/search", SlappCommand.Help),
                ("secondlife:///app/inventory/uuid/select", SlappCommand.Inventory),
                ("secondlife:///app/keybinding/jump", SlappCommand.Keybinding),
                ("secondlife:///app/login", SlappCommand.Login),
                ("secondlife:///app/maptrackavatar/uuid", SlappCommand.MapTrackAvatar),
                ("secondlife:///app/objectim/uuid", SlappCommand.ObjectIm),
                ("secondlife:///app/openfloater/preferences", SlappCommand.OpenFloater),
                ("secondlife:///app/parcel/uuid/about", SlappCommand.Parcel),
                ("secondlife:///app/region/uuid/about", SlappCommand.Region),
                ("secondlife:///app/search/all/test", SlappCommand.Search),
                ("secondlife:///app/sharewithavatar/uuid", SlappCommand.ShareWithAvatar),
                ("secondlife:///app/teleport/Hooper/128/128/0", SlappCommand.Teleport),
                ("secondlife:///app/voicecallavatar/uuid", SlappCommand.VoiceCallAvatar),
                ("secondlife:///app/wear_folder/?folder_id=uuid", SlappCommand.WearFolder),
                ("secondlife:///app/worldmap/Hooper/128/128/0", SlappCommand.WorldMap)
            };

            foreach (var (url, expected) in commands)
            {
                var parser = new SlurlParser(url);
                Assert.That(parser.Command, Is.EqualTo(expected), 
                    $"Failed to parse command for: {url}");
            }
        }
    }
}
