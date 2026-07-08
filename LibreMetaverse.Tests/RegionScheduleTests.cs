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
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the RegionSchedule capability. Verified against the reference viewer
    /// (LLFloaterRegionRestartSchedule::requestRegionShcheduleCoro / onSaveButtonClicked in
    /// llfloaterregionrestartschedule.cpp): GET/POST body is {"restart": {"type": "W"|"D",
    /// "days": "&lt;letters&gt;", "time": &lt;seconds after midnight&gt;}}, where days uses the
    /// letters s/m/t/w/r/f/a for Sun..Sat (uppercase on the wire) and a missing "restart" key or a
    /// non-success GET means no schedule is currently configured.
    /// </summary>
    [TestFixture]
    [Category("Estate")]
    public class RegionScheduleTests
    {
        private const string CapUrl = "http://test.invalid/region-schedule";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("RegionSchedule", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task GetRegionRestartScheduleAsync_WeeklySchedule_ParsesDaysAndTime()
        {
            // example from the reference viewer's own comment: 'restart':{'days':'TR','time':i7200,'type':'W'}
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                "{\"restart\":{\"type\":\"W\",\"days\":\"TR\",\"time\":7200}}", "application/json");

            var schedule = await _client.Estate.GetRegionRestartScheduleAsync();

            Assert.That(schedule, Is.Not.Null);
            Assert.That(schedule!.IsDaily, Is.False);
            Assert.That(schedule.Days, Is.EqualTo(RegionRestartDays.Tuesday | RegionRestartDays.Thursday));
            Assert.That(schedule.Time, Is.EqualTo(TimeSpan.FromHours(2)));
        }

        [Test]
        public async Task GetRegionRestartScheduleAsync_DailySchedule_ReturnsAllDays()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                "{\"restart\":{\"type\":\"D\",\"time\":3600}}", "application/json");

            var schedule = await _client.Estate.GetRegionRestartScheduleAsync();

            Assert.That(schedule, Is.Not.Null);
            Assert.That(schedule!.IsDaily, Is.True);
            Assert.That(schedule.Days, Is.EqualTo(RegionRestartDays.All));
            Assert.That(schedule.Time, Is.EqualTo(TimeSpan.FromHours(1)));
        }

        [Test]
        public async Task GetRegionRestartScheduleAsync_NoRestartConfigured_ReturnsNull()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{}", "application/json");

            var schedule = await _client.Estate.GetRegionRestartScheduleAsync();

            Assert.That(schedule, Is.Null);
        }

        [Test]
        public async Task GetRegionRestartScheduleAsync_NoCapability_ReturnsNullWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                var schedule = await client.Estate.GetRegionRestartScheduleAsync();

                Assert.That(schedule, Is.Null);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task SetRegionRestartScheduleAsync_Weekly_PostsUppercaseDaysAndSeconds()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var schedule = new RegionRestartSchedule
            {
                IsDaily = false,
                Days = RegionRestartDays.Monday | RegionRestartDays.Wednesday | RegionRestartDays.Friday,
                Time = TimeSpan.FromMinutes(90) // 1:30 -> 5400 seconds
            };

            var result = await _client.Estate.SetRegionRestartScheduleAsync(schedule);

            Assert.That(result, Is.True);
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1));
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("<key>type</key>"));
            Assert.That(body, Does.Contain("W"));
            Assert.That(body, Does.Contain("MWF"));
            Assert.That(body, Does.Contain("5400"));
        }

        [Test]
        public async Task SetRegionRestartScheduleAsync_DailyIgnoresDays_DoesNotPostDaysKey()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var schedule = new RegionRestartSchedule { IsDaily = true, Time = TimeSpan.FromHours(3) };

            var result = await _client.Estate.SetRegionRestartScheduleAsync(schedule);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Not.Contain("<key>days</key>"));
        }

        [Test]
        public async Task SetRegionRestartScheduleAsync_EmptyDaysClearsSchedule()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var schedule = new RegionRestartSchedule
            {
                IsDaily = false,
                Days = RegionRestartDays.None,
                Time = TimeSpan.Zero
            };

            var result = await _client.Estate.SetRegionRestartScheduleAsync(schedule);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("<key>days</key>"));
            Assert.That(body, Does.Contain("<string />").Or.Contain("<string></string>"));
        }

        [Test]
        public async Task SetRegionRestartScheduleAsync_NoCapability_ReturnsFalseWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                var result = await client.Estate.SetRegionRestartScheduleAsync(
                    new RegionRestartSchedule { IsDaily = true, Time = TimeSpan.Zero });

                Assert.That(result, Is.False);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task SetRegionRestartScheduleAsync_NonSuccessStatus_ReturnsFalse()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.InternalServerError, string.Empty);

            var result = await _client.Estate.SetRegionRestartScheduleAsync(
                new RegionRestartSchedule { IsDaily = true, Time = TimeSpan.Zero });

            Assert.That(result, Is.False);
        }
    }
}
