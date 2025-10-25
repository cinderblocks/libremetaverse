/*
 * Copyright (c) 2025, Sjofn LLC
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
using OpenMetaverse.Packets;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("EstateTools")]
    class EstateToolsTests : Assert
    {
        private readonly GridClient Client;

        public EstateToolsTests()
        {
            Client = new GridClient();
        }

        [OneTimeSetUp]
        public void Init()
        {
            var fullusername = Environment.GetEnvironmentVariable("LMVTestAgentUsername");
            var password = Environment.GetEnvironmentVariable("LMVTestAgentPassword");
            if (string.IsNullOrWhiteSpace(fullusername)) { Assert.Ignore("LMVTestAgentUsername is empty. EstateToolsTests cannot be performed."); }
            if (string.IsNullOrWhiteSpace(password)) { Assert.Ignore("LMVTestAgentPassword is empty. EstateToolsTests cannot be performed."); }

            var username = fullusername.Split(' ');

            Console.Write($"Logging in {fullusername}...");
            // Connect to the grid
            string startLoc = NetworkManager.StartLocation("Hooper", 179, 18, 32);
            Assert.That(Client.Network.Login(username[0], username[1], password, "Unit Test Framework", startLoc,
                "admin@radegast.life"), Is.True, "Client failed to login, reason: " + Client.Network.LoginMessage);
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
        public void RequestConvenant()
        {
            var waitForReply = new System.Threading.AutoResetEvent(false);
            Client.Estate.EstateCovenantReply += CovenantReceived;
            Client.Estate.RequestCovenant();
            if (!waitForReply.WaitOne(TimeSpan.FromSeconds(10), false))
            {
                Assert.Fail("Timeout waiting for estate covenant");
            }

            return;

            void CovenantReceived(object sender, EstateCovenantReplyEventArgs e)
            {
                Assert.That(e.EstateName, Is.EqualTo("mainland"));
                Assert.That(e.CovenantID, Is.EqualTo(new UUID("6d82fa52-5888-6128-4801-b38e86eaf7e4")));
                Assert.That(e.EstateOwnerID, Is.EqualTo(UUID.Zero));
                waitForReply.Set();
            }
        }

        /*[Test]
        public void EstateInfo()
        {
            var waitForReply = new System.Threading.AutoResetEvent(false);
            Client.Estate.EstateUpdateInfoReply += EstateInfoReceived;
            Client.Estate.RequestInfo();
            if (!waitForReply.WaitOne(TimeSpan.FromSeconds(10), false))
            {
                Assert.Fail("Timeout waiting for Estate info");
            }
            Client.Estate.EstateUpdateInfoReply -= EstateInfoReceived;
            return;

            void EstateInfoReceived(object sender, EstateUpdateInfoReplyEventArgs e)
            {
                Console.Write($"Estate name {e.EstateName}");
                waitForReply.Set();
            }
        }*/
    }
}
