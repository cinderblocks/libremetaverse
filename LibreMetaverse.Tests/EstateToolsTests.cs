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

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("EstateTools")]
    [Category("RequiresLiveServer")]
    class EstateToolsTests : Assert
    {
        private readonly GridClient Client;
        private const int LoginTimeoutSeconds = 30;

        public EstateToolsTests()
        {
            Client = new GridClient();
            Client.Settings.LOGIN_TIMEOUT = LoginTimeoutSeconds * 1000; // Set login timeout
        }

        [OneTimeSetUp]
        [Timeout(45000)] // 45 second timeout for setup
        public void Init()
        {
            var fullusername = Environment.GetEnvironmentVariable("LMVTestAgentUsername");
            var password = Environment.GetEnvironmentVariable("LMVTestAgentPassword");
            if (string.IsNullOrWhiteSpace(fullusername)) 
            { 
                Assert.Ignore("LMVTestAgentUsername is empty. EstateToolsTests cannot be performed."); 
            }
            if (string.IsNullOrWhiteSpace(password)) 
            { 
                Assert.Ignore("LMVTestAgentPassword is empty. EstateToolsTests cannot be performed."); 
            }

            var username = fullusername.Split(' ');

            Console.Write($"Logging in {fullusername}...");
            
            // Connect to the grid with timeout
            string startLoc = NetworkManager.StartLocation("Hooper", 179, 18, 32);
            
            bool loginSuccess = false;
            try
            {
                loginSuccess = Client.Network.Login(username[0], username[1], password, 
                    "Unit Test Framework", startLoc, "admin@radegast.life");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Login threw exception: {ex.Message}");
            }
            
            Assert.That(loginSuccess, Is.True, 
                $"Client failed to login, reason: {Client.Network.LoginMessage}");
            Console.WriteLine("Done");

            Assert.That(Client.Network.Connected, Is.True, 
                "Client is not connected to the grid");

            // Wait a bit for region data to populate
            System.Threading.Thread.Sleep(1000);

            // Check if we have a current sim
            if (Client.Network.CurrentSim == null)
            {
                Assert.Fail("CurrentSim is null after successful login");
            }

            // More flexible region check - allow test to proceed if we're in any region
            if (string.IsNullOrEmpty(Client.Network.CurrentSim.Name))
            {
                Assert.Warn("CurrentSim.Name is empty, but proceeding with tests");
            }
            else if (!Client.Network.CurrentSim.Name.Equals("hooper", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Warn($"Logged in to region '{Client.Network.CurrentSim.Name}' instead of 'Hooper', but proceeding with tests");
            }
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            Console.Write("Logging out...");
            Client.Network.Logout();
            try { Client.Dispose(); } catch { }
            Console.WriteLine("Done");
        }

        [Test]
        [Timeout(15000)] // 15 second timeout for the test
        public void RequestConvenant()
        {
            var waitForReply = new System.Threading.AutoResetEvent(false);
            bool receivedReply = false;
            Exception callbackException = null;
            
            Client.Estate.EstateCovenantReply += CovenantReceived;
            
            try
            {
                Client.Estate.RequestCovenant();
                
                if (!waitForReply.WaitOne(TimeSpan.FromSeconds(10), false))
                {
                    Assert.Fail("Timeout waiting for estate covenant reply after 10 seconds");
                }

                // Check if callback had an exception
                if (callbackException != null)
                {
                    Assert.Fail($"Callback threw exception: {callbackException.Message}");
                }

                Assert.That(receivedReply, Is.True, "Did not receive covenant reply");
            }
            finally
            {
                Client.Estate.EstateCovenantReply -= CovenantReceived;
            }

            return;

            void CovenantReceived(object sender, EstateCovenantReplyEventArgs e)
            {
                try
                {
                    Assert.That(e.EstateName, Is.EqualTo("mainland"));
                    Assert.That(e.CovenantID, Is.EqualTo(new UUID("6d82fa52-5888-6128-4801-b38e86eaf7e4")));
                    Assert.That(e.EstateOwnerID, Is.EqualTo(UUID.Zero));
                    receivedReply = true;
                }
                catch (Exception ex)
                {
                    callbackException = ex;
                }
                finally
                {
                    waitForReply.Set();
                }
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
