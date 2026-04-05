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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    /// <summary>
    /// AgentManager partial class - Experience API
    /// </summary>
    public partial class AgentManager
    {
        #region Experience Events

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AgentExperiencesEventArgs>? m_AgentExperiencesUpdated;

        /// <summary>Raises the AgentExperiencesUpdated event</summary>
        /// <param name="e">An AgentExperiencesEventArgs object containing the data returned from the capability</param>
        protected virtual void OnAgentExperiencesUpdated(AgentExperiencesEventArgs e)
        {
            EventHandler<AgentExperiencesEventArgs>? handler = m_AgentExperiencesUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AgentExperiencesUpdatedLock = new object();

        /// <summary>Raised when the agent's owned experience list is refreshed via the AgentExperiences capability</summary>
        public event EventHandler<AgentExperiencesEventArgs> AgentExperiencesUpdated
        {
            add { lock (m_AgentExperiencesUpdatedLock) { m_AgentExperiencesUpdated += value; } }
            remove { lock (m_AgentExperiencesUpdatedLock) { m_AgentExperiencesUpdated -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<ExperiencePreferencesEventArgs>? m_ExperiencePreferencesUpdated;

        /// <summary>Raises the ExperiencePreferencesUpdated event</summary>
        /// <param name="e">An ExperiencePreferencesEventArgs object containing the data returned from the capability</param>
        protected virtual void OnExperiencePreferencesUpdated(ExperiencePreferencesEventArgs e)
        {
            EventHandler<ExperiencePreferencesEventArgs>? handler = m_ExperiencePreferencesUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ExperiencePreferencesUpdatedLock = new object();

        /// <summary>Raised when the agent's experience allow/block preferences are refreshed via the
        /// GetExperiences or ExperiencePreferences capability</summary>
        public event EventHandler<ExperiencePreferencesEventArgs> ExperiencePreferencesUpdated
        {
            add { lock (m_ExperiencePreferencesUpdatedLock) { m_ExperiencePreferencesUpdated += value; } }
            remove { lock (m_ExperiencePreferencesUpdatedLock) { m_ExperiencePreferencesUpdated -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<RegionExperiencesEventArgs>? m_RegionExperiencesUpdated;

        /// <summary>Raises the RegionExperiencesUpdated event</summary>
        /// <param name="e">A RegionExperiencesEventArgs object containing the data returned from the capability</param>
        protected virtual void OnRegionExperiencesUpdated(RegionExperiencesEventArgs e)
        {
            EventHandler<RegionExperiencesEventArgs>? handler = m_RegionExperiencesUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RegionExperiencesUpdatedLock = new object();

        /// <summary>Raised when the region's experience list is refreshed via the RegionExperiences capability</summary>
        public event EventHandler<RegionExperiencesEventArgs> RegionExperiencesUpdated
        {
            add { lock (m_RegionExperiencesUpdatedLock) { m_RegionExperiencesUpdated += value; } }
            remove { lock (m_RegionExperiencesUpdatedLock) { m_RegionExperiencesUpdated -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<ExperienceInfoEventArgs>? m_ExperienceInfoReceived;

        /// <summary>Raises the ExperienceInfoReceived event</summary>
        /// <param name="e">An ExperienceInfoEventArgs object containing the data returned from the capability</param>
        protected virtual void OnExperienceInfoReceived(ExperienceInfoEventArgs e)
        {
            EventHandler<ExperienceInfoEventArgs>? handler = m_ExperienceInfoReceived;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ExperienceInfoReceivedLock = new object();

        /// <summary>Raised when experience details are received via the GetExperienceInfo,
        /// FindExperienceByName, or ExperienceQuery capability</summary>
        public event EventHandler<ExperienceInfoEventArgs> ExperienceInfoReceived
        {
            add { lock (m_ExperienceInfoReceivedLock) { m_ExperienceInfoReceived += value; } }
            remove { lock (m_ExperienceInfoReceivedLock) { m_ExperienceInfoReceived -= value; } }
        }

        #endregion Experience Events

        #region Experience Properties

        /// <summary>The agent's owned experience list, last retrieved via the AgentExperiences capability.
        /// Null until <see cref="GetAgentExperiencesAsync"/> is called.</summary>
        public ExperienceListMessage? AgentExperienceList { get; private set; }

        /// <summary>The agent's experience allow/block preferences, last retrieved via the GetExperiences
        /// or ExperiencePreferences capability.
        /// Null until <see cref="GetAgentExperiencePermissionsAsync"/> or <see cref="GetExperiencePreferencesAsync"/> is called.</summary>
        public ExperiencePreferencesMessage? ExperiencePreferences { get; private set; }

        /// <summary>The region's experience list, last retrieved via the RegionExperiences capability.
        /// Null until <see cref="GetRegionExperiencesAsync"/> is called.</summary>
        public RegionExperiencesMessage? LastRegionExperiences { get; private set; }

        #endregion Experience Properties

        #region Experience API

        /// <summary>
        /// Retrieves the list of experiences associated with the current agent from the AgentExperiences capability.
        /// Updates <see cref="AgentExperienceList"/> and raises <see cref="AgentExperiencesUpdated"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceListMessage?> GetAgentExperiencesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("AgentExperiences");
                if (cap == null)
                {
                    Logger.Warn("AgentExperiences capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                ExperienceListMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"AgentExperiences request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"AgentExperiences non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("AgentExperiences returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                            AgentExperienceList = msg;
                            OnAgentExperiencesUpdated(new AgentExperiencesEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse AgentExperiences response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching AgentExperiences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Retrieves detailed information for one or more experiences from the GetExperienceInfo capability.
        /// Raises <see cref="ExperienceInfoReceived"/> with the results.
        /// </summary>
        /// <param name="experienceIds">One or more public experience UUIDs to look up</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceInfoMessage?> GetExperienceInfoAsync(IEnumerable<UUID> experienceIds,
            CancellationToken cancellationToken = default)
        {
            if (experienceIds == null) throw new ArgumentNullException(nameof(experienceIds));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("GetExperienceInfo");
                if (cap == null)
                {
                    Logger.Warn("GetExperienceInfo capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                var idList = new List<UUID>(experienceIds);
                if (idList.Count == 0) { return new ExperienceInfoMessage(); }

                var qs = new StringBuilder();
                foreach (var id in idList)
                {
                    if (qs.Length > 0) qs.Append('&');
                    qs.Append("public_id=");
                    qs.Append(id);
                }
                var requestUri = new Uri($"{cap}?{qs}");

                ExperienceInfoMessage? result = null;
                await http.GetRequestAsync(requestUri, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"GetExperienceInfo request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"GetExperienceInfo non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("GetExperienceInfo returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceInfoMessage msg = new ExperienceInfoMessage();
                            msg.Deserialize(map);
                            result = msg;
                            OnExperienceInfoReceived(new ExperienceInfoEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse GetExperienceInfo response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching GetExperienceInfo", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Searches for experiences by name using the FindExperienceByName capability.
        /// Raises <see cref="ExperienceInfoReceived"/> with the results.
        /// </summary>
        /// <param name="query">Search string</param>
        /// <param name="pageSize">Maximum number of results to return (default 30)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceInfoMessage?> FindExperienceByNameAsync(string query, int pageSize = 30,
            CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("FindExperienceByName");
                if (cap == null)
                {
                    Logger.Warn("FindExperienceByName capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                var requestUri = new Uri($"{cap}?page_size={pageSize}&query={Uri.EscapeDataString(query)}");

                ExperienceInfoMessage? result = null;
                await http.GetRequestAsync(requestUri, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"FindExperienceByName request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"FindExperienceByName non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("FindExperienceByName returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceInfoMessage msg = new ExperienceInfoMessage();
                            msg.Deserialize(map);
                            result = msg;
                            OnExperienceInfoReceived(new ExperienceInfoEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse FindExperienceByName response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching FindExperienceByName", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Retrieves experiences where the current agent has administrator rights
        /// from the GetAdminExperiences capability.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceListMessage?> GetAdminExperiencesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("GetAdminExperiences");
                if (cap == null)
                {
                    Logger.Warn("GetAdminExperiences capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                ExperienceListMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"GetAdminExperiences request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"GetAdminExperiences non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("GetAdminExperiences returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse GetAdminExperiences response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching GetAdminExperiences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Retrieves experiences created by the current agent from the GetCreatorExperiences capability.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceListMessage?> GetCreatorExperiencesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("GetCreatorExperiences");
                if (cap == null)
                {
                    Logger.Warn("GetCreatorExperiences capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                ExperienceListMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"GetCreatorExperiences request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"GetCreatorExperiences non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("GetCreatorExperiences returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse GetCreatorExperiences response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching GetCreatorExperiences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Retrieves the agent's global experience allow/block list from the GetExperiences capability.
        /// Updates <see cref="ExperiencePreferences"/> and raises <see cref="ExperiencePreferencesUpdated"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperiencePreferencesMessage?> GetAgentExperiencePermissionsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("GetExperiences");
                if (cap == null)
                {
                    Logger.Warn("GetExperiences capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                ExperiencePreferencesMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"GetExperiences request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"GetExperiences non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("GetExperiences returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperiencePreferencesMessage msg = new ExperiencePreferencesMessage();
                            msg.Deserialize(map);
                            result = msg;
                            ExperiencePreferences = msg;
                            OnExperiencePreferencesUpdated(new ExperiencePreferencesEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse GetExperiences response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching GetExperiences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Retrieves the agent's per-experience preferences from the ExperiencePreferences capability.
        /// Updates <see cref="ExperiencePreferences"/> and raises <see cref="ExperiencePreferencesUpdated"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperiencePreferencesMessage?> GetExperiencePreferencesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExperiencePreferences");
                if (cap == null)
                {
                    Logger.Warn("ExperiencePreferences capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                ExperiencePreferencesMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"ExperiencePreferences GET failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"ExperiencePreferences non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("ExperiencePreferences returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperiencePreferencesMessage msg = new ExperiencePreferencesMessage();
                            msg.Deserialize(map);
                            result = msg;
                            ExperiencePreferences = msg;
                            OnExperiencePreferencesUpdated(new ExperiencePreferencesEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse ExperiencePreferences response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching ExperiencePreferences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Updates the agent's per-experience preferences via the ExperiencePreferences capability.
        /// </summary>
        /// <param name="preferences">The preferences to post</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SetExperiencePreferencesAsync(ExperiencePreferencesMessage preferences,
            CancellationToken cancellationToken = default)
        {
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExperiencePreferences");
                if (cap == null)
                {
                    Logger.Warn("ExperiencePreferences capability not available.", Client);
                    return;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return; }

                await http.PostRequestAsync(cap, OSDFormat.Xml, preferences.Serialize(), cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"ExperiencePreferences POST failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"ExperiencePreferences POST non-success status: {response?.StatusCode}", Client);
                        }
                    }).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed setting ExperiencePreferences", ex, Client);
            }
        }

        /// <summary>
        /// Retrieves the experiences associated with a group from the GroupExperiences capability.
        /// </summary>
        /// <param name="groupId">UUID of the group to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceListMessage?> GetGroupExperiencesAsync(UUID groupId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("GroupExperiences");
                if (cap == null)
                {
                    Logger.Warn("GroupExperiences capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                var requestUri = new Uri($"{cap}?group_id={groupId}");

                ExperienceListMessage? result = null;
                await http.GetRequestAsync(requestUri, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"GroupExperiences request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"GroupExperiences non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("GroupExperiences returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse GroupExperiences response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching GroupExperiences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Updates experience metadata via the UpdateExperience capability.
        /// </summary>
        /// <param name="info">The experience info to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated ExperienceInfo returned by the server, or null on failure</returns>
        public async Task<ExperienceInfo?> UpdateExperienceAsync(ExperienceInfo info,
            CancellationToken cancellationToken = default)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("UpdateExperience");
                if (cap == null)
                {
                    Logger.Warn("UpdateExperience capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                ExperienceInfo? result = null;
                await http.PostRequestAsync(cap, OSDFormat.Xml, info.Serialize(), cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"UpdateExperience POST failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"UpdateExperience non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null) { return; }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceInfoMessage msg = new ExperienceInfoMessage();
                            msg.Deserialize(map);
                            if (msg.Experiences.Count > 0)
                                result = msg.Experiences[0];
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse UpdateExperience response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed updating experience", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Checks whether the current agent has administrator rights for the specified experience
        /// via the IsExperienceAdmin capability.
        /// </summary>
        /// <param name="experienceId">UUID of the experience to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the agent is an administrator of the experience, false otherwise</returns>
        public async Task<bool> IsExperienceAdminAsync(UUID experienceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("IsExperienceAdmin");
                if (cap == null)
                {
                    Logger.Warn("IsExperienceAdmin capability not available.", Client);
                    return false;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return false; }

                var requestUri = new Uri($"{cap}?experience_id={experienceId}");

                bool result = false;
                await http.GetRequestAsync(requestUri, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"IsExperienceAdmin request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"IsExperienceAdmin non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null) { return; }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (osd is OSDMap map)
                                result = map["status"].AsBoolean();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse IsExperienceAdmin response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed checking IsExperienceAdmin", ex, Client);
                return false;
            }
        }

        /// <summary>
        /// Checks whether the current agent is a contributor to the specified experience
        /// via the IsExperienceContributor capability.
        /// </summary>
        /// <param name="experienceId">UUID of the experience to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the agent is a contributor to the experience, false otherwise</returns>
        public async Task<bool> IsExperienceContributorAsync(UUID experienceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("IsExperienceContributor");
                if (cap == null)
                {
                    Logger.Warn("IsExperienceContributor capability not available.", Client);
                    return false;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return false; }

                var requestUri = new Uri($"{cap}?experience_id={experienceId}");

                bool result = false;
                await http.GetRequestAsync(requestUri, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"IsExperienceContributor request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"IsExperienceContributor non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null) { return; }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (osd is OSDMap map)
                                result = map["status"].AsBoolean();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse IsExperienceContributor response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed checking IsExperienceContributor", ex, Client);
                return false;
            }
        }

        /// <summary>
        /// Retrieves the experiences trusted, blocked, and running in the current region
        /// from the RegionExperiences capability.
        /// Updates <see cref="LastRegionExperiences"/> and raises <see cref="RegionExperiencesUpdated"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<RegionExperiencesMessage?> GetRegionExperiencesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("RegionExperiences");
                if (cap == null)
                {
                    Logger.Warn("RegionExperiences capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                RegionExperiencesMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"RegionExperiences request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"RegionExperiences non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("RegionExperiences returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            RegionExperiencesMessage msg = new RegionExperiencesMessage();
                            msg.Deserialize(map);
                            result = msg;
                            LastRegionExperiences = msg;
                            OnRegionExperiencesUpdated(new RegionExperiencesEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse RegionExperiences response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching RegionExperiences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Searches for experiences using the ExperienceQuery capability.
        /// Raises <see cref="ExperienceInfoReceived"/> with the results.
        /// </summary>
        /// <param name="query">Search string (may be empty to list all)</param>
        /// <param name="groupId">Filter results to a specific group UUID, or <see cref="UUID.Zero"/> for no filter</param>
        /// <param name="maturity">Maturity filter: "g" (general), "m" (mature), or "a" (adult)</param>
        /// <param name="pageSize">Maximum number of results per page (default 30)</param>
        /// <param name="page">Zero-based page number (default 0)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceInfoMessage?> QueryExperiencesAsync(string query = "",
            UUID groupId = default, string maturity = "g", int pageSize = 30, int page = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExperienceQuery");
                if (cap == null)
                {
                    Logger.Warn("ExperienceQuery capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                var qs = new StringBuilder();
                qs.Append($"page_size={pageSize}&page={page}&maturity={Uri.EscapeDataString(maturity)}");
                if (!string.IsNullOrEmpty(query))
                    qs.Append($"&query={Uri.EscapeDataString(query)}");
                if (groupId != UUID.Zero)
                    qs.Append($"&group_id={groupId}");
                var requestUri = new Uri($"{cap}?{qs}");

                ExperienceInfoMessage? result = null;
                await http.GetRequestAsync(requestUri, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"ExperienceQuery request failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"ExperienceQuery non-success status: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null)
                        {
                            Logger.Warn("ExperienceQuery returned no data.", Client);
                            return;
                        }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            ExperienceInfoMessage msg = new ExperienceInfoMessage();
                            msg.Deserialize(map);
                            result = msg;
                            OnExperienceInfoReceived(new ExperienceInfoEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse ExperienceQuery response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching ExperienceQuery", ex, Client);
                return null;
            }
        }

        #endregion Experience API
    }
}
