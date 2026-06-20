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

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    public partial class DirectoryManager
    {
        /// <summary>
        /// Search the people directory and yield results as they arrive, automatically
        /// fetching additional pages until the server signals no more results.
        /// Pass a <paramref name="ct"/> with a timeout to bound the wait for each page.
        /// </summary>
        public async IAsyncEnumerable<AgentSearchData> SearchPeopleAsync(
            string name,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = Channel.CreateUnbounded<List<AgentSearchData>>(
                new UnboundedChannelOptions { SingleReader = true });
            UUID queryID = UUID.Zero;

            void Handler(object? sender, DirPeopleReplyEventArgs e)
            {
                if (e.QueryID == queryID)
                    channel.Writer.TryWrite(e.MatchedPeople);
            }

            DirPeopleReply += Handler;
            try
            {
                int offset = 0;
                while (true)
                {
                    queryID = StartPeopleSearch(name, offset);
                    var batch = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
                    foreach (var item in batch)
                        yield return item;
                    if (batch.Count < 100) break;
                    offset += batch.Count;
                }
            }
            finally
            {
                DirPeopleReply -= Handler;
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Search the group directory and yield results, automatically paging until done.
        /// </summary>
        public async IAsyncEnumerable<GroupSearchData> SearchGroupsAsync(
            string query,
            DirFindFlags flags = DirFindFlags.Groups,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = Channel.CreateUnbounded<List<GroupSearchData>>(
                new UnboundedChannelOptions { SingleReader = true });
            UUID queryID = UUID.Zero;

            void Handler(object? sender, DirGroupsReplyEventArgs e)
            {
                if (e.QueryID == queryID)
                    channel.Writer.TryWrite(e.MatchedGroups);
            }

            DirGroupsReply += Handler;
            try
            {
                int offset = 0;
                while (true)
                {
                    queryID = StartGroupSearch(query, offset, flags);
                    var batch = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
                    foreach (var item in batch)
                        yield return item;
                    if (batch.Count < 100) break;
                    offset += batch.Count;
                }
            }
            finally
            {
                DirGroupsReply -= Handler;
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Search the places database and yield parcel results, automatically paging until done.
        /// </summary>
        public async IAsyncEnumerable<DirectoryParcel> SearchDirPlacesAsync(
            string query,
            DirFindFlags flags = DirFindFlags.DwellSort | DirFindFlags.IncludePG
                                | DirFindFlags.IncludeMature | DirFindFlags.IncludeAdult,
            ParcelCategory category = ParcelCategory.Any,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = Channel.CreateUnbounded<List<DirectoryParcel>>(
                new UnboundedChannelOptions { SingleReader = true });
            UUID queryID = UUID.Zero;

            void Handler(object? sender, DirPlacesReplyEventArgs e)
            {
                if (e.QueryID == queryID)
                    channel.Writer.TryWrite(e.MatchedParcels);
            }

            DirPlacesReply += Handler;
            try
            {
                int offset = 0;
                while (true)
                {
                    queryID = StartDirPlacesSearch(query, flags, category, offset);
                    var batch = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
                    foreach (var item in batch)
                        yield return item;
                    if (batch.Count < 100) break;
                    offset += batch.Count;
                }
            }
            finally
            {
                DirPlacesReply -= Handler;
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Search classified ads and yield results.
        /// The classifieds API returns a single page and carries no query ID for correlation;
        /// avoid issuing concurrent classified searches on the same <see cref="DirectoryManager"/>.
        /// </summary>
        public async IAsyncEnumerable<Classified> SearchClassifiedsAsync(
            string query,
            ClassifiedCategories category = ClassifiedCategories.Any,
            ClassifiedQueryFlags flags = ClassifiedQueryFlags.All,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = Channel.CreateBounded<List<Classified>>(1);

            void Handler(object? sender, DirClassifiedsReplyEventArgs e)
                => channel.Writer.TryWrite(e.Classifieds);

            DirClassifiedsReply += Handler;
            try
            {
                StartClassifiedSearch(query, category, flags);
                var batch = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
                foreach (var item in batch)
                    yield return item;
            }
            finally
            {
                DirClassifiedsReply -= Handler;
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Search in-world events and yield results, automatically paging until done.
        /// Pass <c>"u"</c> as <paramref name="eventDay"/> for upcoming events.
        /// </summary>
        public async IAsyncEnumerable<EventsSearchData> SearchEventsAsync(
            string query,
            DirFindFlags flags = DirFindFlags.DateEvents | DirFindFlags.IncludePG
                                | DirFindFlags.IncludeMature | DirFindFlags.IncludeAdult,
            string eventDay = "u",
            EventCategories category = EventCategories.All,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = Channel.CreateUnbounded<List<EventsSearchData>>(
                new UnboundedChannelOptions { SingleReader = true });
            UUID queryID = UUID.Zero;

            void Handler(object? sender, DirEventsReplyEventArgs e)
            {
                if (e.QueryID == queryID)
                    channel.Writer.TryWrite(e.MatchedEvents);
            }

            DirEventsReply += Handler;
            try
            {
                uint offset = 0;
                while (true)
                {
                    queryID = StartEventsSearch(query, flags, eventDay, offset, category);
                    var batch = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
                    foreach (var ev in batch)
                        yield return ev;
                    if (batch.Count < 100) break;
                    offset += (uint)batch.Count;
                }
            }
            finally
            {
                DirEventsReply -= Handler;
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Search land for sale and yield parcel results.
        /// The land search reply carries no query ID; avoid concurrent land searches
        /// on the same <see cref="DirectoryManager"/>.
        /// </summary>
        public async IAsyncEnumerable<DirectoryParcel> SearchLandAsync(
            SearchTypeFlags typeFlags,
            DirFindFlags findFlags = DirFindFlags.SortAsc | DirFindFlags.PerMeterSort
                                   | DirFindFlags.IncludePG | DirFindFlags.IncludeMature
                                   | DirFindFlags.IncludeAdult,
            int priceLimit = 0,
            int areaLimit = 0,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = Channel.CreateBounded<List<DirectoryParcel>>(1);

            void Handler(object? sender, DirLandReplyEventArgs e)
                => channel.Writer.TryWrite(e.DirParcels);

            DirLandReply += Handler;
            try
            {
                StartLandSearch(findFlags, typeFlags, priceLimit, areaLimit, 0);
                var batch = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
                foreach (var parcel in batch)
                    yield return parcel;
            }
            finally
            {
                DirLandReply -= Handler;
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Search the places directory (user-owned parcels) and yield results.
        /// </summary>
        public async IAsyncEnumerable<PlacesSearchData> SearchPlacesAsync(
            DirFindFlags findFlags,
            ParcelCategory category,
            string query,
            string simulatorName = "",
            UUID groupID = default,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var channel = Channel.CreateBounded<List<PlacesSearchData>>(1);
            UUID queryID = UUID.Zero;

            void Handler(object? sender, PlacesReplyEventArgs e)
            {
                if (e.QueryID == queryID)
                    channel.Writer.TryWrite(e.MatchedPlaces);
            }

            PlacesReply += Handler;
            try
            {
                queryID = StartPlacesSearch(findFlags, category, query, simulatorName, groupID, UUID.Random());
                var batch = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
                foreach (var place in batch)
                    yield return place;
            }
            finally
            {
                PlacesReply -= Handler;
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Fetch details for a single event by ID.
        /// The reply carries no query ID; avoid concurrent event-info requests
        /// on the same <see cref="DirectoryManager"/>.
        /// </summary>
        public async Task<EventInfo?> GetEventInfoAsync(
            uint eventID,
            CancellationToken ct = default)
        {
            var channel = Channel.CreateBounded<EventInfo>(1);

            void Handler(object? sender, EventInfoReplyEventArgs e)
                => channel.Writer.TryWrite(e.MatchedEvent);

            EventInfoReply += Handler;
            try
            {
                EventInfoRequest(eventID);
                return await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                EventInfoReply -= Handler;
                channel.Writer.TryComplete();
            }
        }
    }
}
