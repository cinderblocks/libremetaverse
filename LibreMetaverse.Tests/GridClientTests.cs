/*
 * Copyright (c) 2021-2025, Sjofn LLC
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

using NUnit.Framework;
using System;
using OpenMetaverse;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("GridClient")]
    class GridClientTests : Assert
    {
        readonly GridClient Client;

        public GridClientTests()
        {
            Client = new GridClient();
            Client.Self.Movement.Fly = true;
            // Register callbacks
            //Client.Network.RegisterCallback(PacketType.ObjectUpdate, ObjectUpdateHandler);
        }

        [OneTimeSetUp]
        public void Init()
        {
            var fullusername = Environment.GetEnvironmentVariable("LMVTestAgentUsername");
            var password = Environment.GetEnvironmentVariable("LMVTestAgentPassword");
            if (string.IsNullOrWhiteSpace(fullusername)) { Assert.Ignore("LMVTestAgentUsername is empty. Live GridManagerTests cannot be performed."); }
            if (string.IsNullOrWhiteSpace(password)) { Assert.Ignore("LMVTestAgentPassword is empty. Live GridManagerTests cannot be performed."); }

            var username = fullusername.Split(' ');

            Console.Write($"Logging in {fullusername}...");
            // Connect to the grid
            string startLoc = NetworkManager.StartLocation("Hooper", 179, 18, 32);
            Assert.That(Client.Network.Login(username[0], username[1], password, "Unit Test Framework", startLoc,
                "contact@radegast.life"), Is.True, "Client failed to login, reason: " + Client.Network.LoginMessage);
            Console.WriteLine("Done");

            Assert.That(Client.Network.Connected, Is.True, "Client is not connected to the grid");

            //int start = Environment.TickCount;

            Assert.That(Client.Network.CurrentSim.Name, Is.EqualTo("hooper").IgnoreCase, 
                $"Logged in to region {Client.Network.CurrentSim.Name} instead of Hooper");
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            Console.Write("Logging out...");
            Client.Network.Logout();
            Console.WriteLine("Done");
        }

        [Test]
        public void GetGridRegion()
        {
            Assert.That(Client.Grid.GetGridRegion("Hippo Hollow", GridLayerType.Terrain, out var region), Is.True);
            Assert.That(region.Name, Is.EqualTo("hippo hollow").IgnoreCase);
        }
    }
}
