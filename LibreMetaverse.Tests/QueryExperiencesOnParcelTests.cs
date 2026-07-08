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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the ExperienceQuery capability. Verified against the reference viewer's only
    /// consumer, DayInjection::testExperiencesOnParcelCoro (llenvironment.cpp): the request is
    /// "?parcelid=&lt;id&gt;&amp;experiences=&lt;uuid&gt;,&lt;uuid&gt;,..." and the response is an
    /// "experiences" map of experience UUID (string) to boolean allowed/blocked. This capability has
    /// nothing to do with searching experiences by name/maturity/group -- an earlier implementation
    /// (QueryExperiencesAsync) invented a paged-search request shape and an "experience_keys"
    /// response shape that this capability does not use at all.
    /// </summary>
    [TestFixture]
    public class QueryExperiencesOnParcelTests
    {
        private const string CapUrl = "http://test.invalid/experience-query";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("ExperienceQuery", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task QueryExperiencesOnParcelAsync_HappyPath_ParsesAllowedMapAndBuildsExpectedQuery()
        {
            var exp1 = UUID.Random();
            var exp2 = UUID.Random();

            _client.AddHttpResponseForPath(CapUrl, HttpStatusCode.OK,
                $"{{\"experiences\":{{\"{exp1}\":true,\"{exp2}\":false}}}}", "application/json");

            var result = await _client.Self.QueryExperiencesOnParcelAsync(42, new[] { exp1, exp2 });

            Assert.That(result, Is.Not.Null);
            Assert.That(result![exp1], Is.True);
            Assert.That(result[exp2], Is.False);

            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1));
            var query = _client.CapturedRequests[0].Uri.Query;
            Assert.That(query, Does.Contain("parcelid=42"));
            Assert.That(query, Does.Contain($"experiences={exp1},{exp2}"));
        }

        [Test]
        public async Task QueryExperiencesOnParcelAsync_NoExperienceIds_OmitsExperiencesParam()
        {
            _client.AddHttpResponseForPath(CapUrl, HttpStatusCode.OK,
                "{\"experiences\":{}}", "application/json");

            var result = await _client.Self.QueryExperiencesOnParcelAsync(7, Enumerable.Empty<UUID>());

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);

            var query = _client.CapturedRequests[0].Uri.Query;
            Assert.That(query, Does.Contain("parcelid=7"));
            Assert.That(query, Does.Not.Contain("experiences"));
        }

        [Test]
        public async Task QueryExperiencesOnParcelAsync_NoCapability_ReturnsNullWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                var result = await client.Self.QueryExperiencesOnParcelAsync(1, new[] { UUID.Random() });

                Assert.That(result, Is.Null);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task QueryExperiencesOnParcelAsync_NonSuccessStatus_ReturnsNull()
        {
            _client.AddHttpResponseForPath(CapUrl, HttpStatusCode.InternalServerError, string.Empty);

            var result = await _client.Self.QueryExperiencesOnParcelAsync(1, new[] { UUID.Random() });

            Assert.That(result, Is.Null);
        }
    }
}
