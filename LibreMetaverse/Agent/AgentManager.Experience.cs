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
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
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

        /// <summary>Raised when experience details are received via the GetExperienceInfo
        /// or FindExperienceByName capability</summary>
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
                var (response, data) = await http.GetAsync(cap, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"AgentExperiences non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("AgentExperiences returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                            AgentExperienceList = msg;
                            OnAgentExperiencesUpdated(new AgentExperiencesEventArgs(msg));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse AgentExperiences response", ex, Client);
                    }
                }
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

                // Reference viewer's LLExperienceCache::requestExperiences appends an "id/" path
                // segment before the query string (e.g. ".../GetExperienceInfo/id/?public_id=...").
                var qs = new StringBuilder("page_size=").Append(idList.Count);
                foreach (var id in idList)
                {
                    qs.Append("&public_id=");
                    qs.Append(id);
                }
                var baseUri = cap.ToString().TrimEnd('/') + "/id/";
                var requestUri = new Uri($"{baseUri}?{qs}");

                ExperienceInfoMessage? result = null;
                var (response, data) = await http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"GetExperienceInfo non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("GetExperienceInfo returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperienceInfoMessage msg = new ExperienceInfoMessage();
                            msg.Deserialize(map);
                            result = msg;
                            OnExperienceInfoReceived(new ExperienceInfoEventArgs(msg));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse GetExperienceInfo response", ex, Client);
                    }
                }
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
        /// <param name="page">Zero-based page number (default 0)</param>
        /// <param name="pageSize">Maximum number of results per page (default 30)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExperienceInfoMessage?> FindExperienceByNameAsync(string query, int page = 0,
            int pageSize = 30, CancellationToken cancellationToken = default)
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

                var requestUri = new Uri($"{cap}?page={page}&page_size={pageSize}&query={Uri.EscapeDataString(query)}");

                ExperienceInfoMessage? result = null;
                var (response, data) = await http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"FindExperienceByName non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("FindExperienceByName returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperienceInfoMessage msg = new ExperienceInfoMessage();
                            msg.Deserialize(map);
                            result = msg;
                            OnExperienceInfoReceived(new ExperienceInfoEventArgs(msg));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse FindExperienceByName response", ex, Client);
                    }
                }
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
                var (response, data) = await http.GetAsync(cap, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"GetAdminExperiences non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("GetAdminExperiences returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse GetAdminExperiences response", ex, Client);
                    }
                }
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
                var (response, data) = await http.GetAsync(cap, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"GetCreatorExperiences non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("GetCreatorExperiences returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse GetCreatorExperiences response", ex, Client);
                    }
                }
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
                var (response, data) = await http.GetAsync(cap, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"GetExperiences non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("GetExperiences returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperiencePreferencesMessage msg = new ExperiencePreferencesMessage();
                            msg.Deserialize(map);
                            result = msg;
                            ExperiencePreferences = msg;
                            OnExperiencePreferencesUpdated(new ExperiencePreferencesEventArgs(msg));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse GetExperiences response", ex, Client);
                    }
                }
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
                var (response, data) = await http.GetAsync(cap, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"ExperiencePreferences non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("ExperiencePreferences returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperiencePreferencesMessage msg = new ExperiencePreferencesMessage();
                            msg.Deserialize(map);
                            result = msg;
                            ExperiencePreferences = msg;
                            OnExperiencePreferencesUpdated(new ExperiencePreferencesEventArgs(msg));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse ExperiencePreferences response", ex, Client);
                    }
                }
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

                var (response, data) = await http.PostAsync(cap, OSDFormat.Xml, preferences.Serialize(), cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"ExperiencePreferences POST non-success status: {response.StatusCode}", Client);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed setting ExperiencePreferences", ex, Client);
            }
        }

        /// <summary>
        /// Retrieves the agent's allow/block permission for a single experience via the
        /// ExperiencePreferences capability. This is what the viewer calls when it needs to know
        /// (e.g. for an experience profile) whether one specific experience is currently allowed or
        /// blocked, without fetching the entire preferences list.
        /// </summary>
        /// <param name="experienceId">UUID of the experience to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>"Allow", "Block", or "Forget" (no explicit preference set), or null if the request failed</returns>
        public async Task<string?> GetExperiencePermissionAsync(UUID experienceId, CancellationToken cancellationToken = default)
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

                var requestUri = new Uri($"{cap}?{experienceId}");
                var (response, data) = await http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"ExperiencePreferences (single) non-success status: {response.StatusCode}", Client);
                    return null;
                }
                if (data == null) { return null; }

                return OSDParser.Deserialize(data) is OSDMap map ? ExtractExperiencePermission(map, experienceId) : null;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching experience permission", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Sets the agent's allow/block permission for a single experience via the
        /// ExperiencePreferences capability, without disturbing the rest of the preferences list.
        /// </summary>
        /// <param name="experienceId">UUID of the experience to update</param>
        /// <param name="permission">"Allow" or "Block"</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the server accepted the change</returns>
        public async Task<bool> SetExperiencePermissionAsync(UUID experienceId, string permission,
            CancellationToken cancellationToken = default)
        {
            if (permission == null) throw new ArgumentNullException(nameof(permission));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExperiencePreferences");
                if (cap == null)
                {
                    Logger.Warn("ExperiencePreferences capability not available.", Client);
                    return false;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return false; }

                var body = new OSDMap
                {
                    [experienceId.ToString()] = new OSDMap { ["permission"] = OSD.FromString(permission) }
                };

                var (response, _) = await http.PutAsync(cap, OSDFormat.Xml, body, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"ExperiencePreferences PUT non-success status: {response.StatusCode}", Client);
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed setting experience permission", ex, Client);
                return false;
            }
        }

        /// <summary>
        /// Clears any explicit allow/block preference for a single experience via the
        /// ExperiencePreferences capability, reverting it to the agent's default handling.
        /// </summary>
        /// <param name="experienceId">UUID of the experience to forget</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the server accepted the change</returns>
        public async Task<bool> ForgetExperiencePermissionAsync(UUID experienceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExperiencePreferences");
                if (cap == null)
                {
                    Logger.Warn("ExperiencePreferences capability not available.", Client);
                    return false;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return false; }

                var requestUri = new Uri($"{cap}?{experienceId}");
                using var response = await http.DeleteAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"ExperiencePreferences DELETE non-success status: {response.StatusCode}", Client);
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed forgetting experience permission", ex, Client);
                return false;
            }
        }

        /// <summary>Mirrors the reference viewer's experiencePermissionResults(): "Allow" if the
        /// experience appears in the "experiences" (allowed) list, "Block" if in "blocked", otherwise
        /// "Forget" (no explicit preference).</summary>
        private static string ExtractExperiencePermission(OSDMap map, UUID experienceId)
        {
            if (map["experiences"] is OSDArray allowed)
                foreach (OSD entry in allowed)
                    if (entry.AsUUID() == experienceId) return "Allow";
            if (map["blocked"] is OSDArray blocked)
                foreach (OSD entry in blocked)
                    if (entry.AsUUID() == experienceId) return "Block";
            return "Forget";
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

                // Reference viewer's LLExperienceCache::getGroupExperiencesCoro appends the bare
                // group UUID after "?" -- no "group_id=" key prefix.
                var requestUri = new Uri($"{cap}?{groupId}");

                ExperienceListMessage? result = null;
                var (response, data) = await http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"GroupExperiences non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("GroupExperiences returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperienceListMessage msg = new ExperienceListMessage();
                            msg.Deserialize(map);
                            result = msg;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse GroupExperiences response", ex, Client);
                    }
                }
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
                var (response, data) = await http.PostAsync(cap, OSDFormat.Xml, info.Serialize(), cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"UpdateExperience non-success status: {response.StatusCode}", Client);
                }
                else if (data != null)
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            ExperienceInfoMessage msg = new ExperienceInfoMessage();
                            msg.Deserialize(map);
                            if (msg.Experiences.Count > 0)
                                result = msg.Experiences[0];
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse UpdateExperience response", ex, Client);
                    }
                }
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
                var (response, data) = await http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"IsExperienceAdmin non-success status: {response.StatusCode}", Client);
                }
                else if (data != null)
                {
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
                }
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
                var (response, data) = await http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"IsExperienceContributor non-success status: {response.StatusCode}", Client);
                }
                else if (data != null)
                {
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
                }
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
                var (response, data) = await http.GetAsync(cap, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"RegionExperiences non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("RegionExperiences returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (!(osd is OSDMap map)) { }
                        else
                        {
                            RegionExperiencesMessage msg = new RegionExperiencesMessage();
                            msg.Deserialize(map);
                            result = msg;
                            LastRegionExperiences = msg;
                            OnRegionExperiencesUpdated(new RegionExperiencesEventArgs(msg));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse RegionExperiences response", ex, Client);
                    }
                }
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching RegionExperiences", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Checks whether one or more experiences are currently permitted to run on a parcel, via
        /// the ExperienceQuery capability. This mirrors the reference viewer's only consumer of the
        /// capability, DayInjection::testExperiencesOnParcelCoro (llenvironment.cpp), which uses it
        /// to verify that environment-altering experiences attached to the day cycle are still
        /// allowed on the parcel the agent is currently standing on: it builds
        /// "?parcelid=&lt;id&gt;&amp;experiences=&lt;uuid&gt;,&lt;uuid&gt;,..." and reads back an
        /// "experiences" map of experience UUID (string) to boolean allowed/blocked. This capability
        /// has nothing to do with searching/listing experiences (that is FindExperienceByName) --
        /// an earlier implementation incorrectly modeled it as a paged query/maturity/group_id
        /// search returning "experience_keys", a shape this capability does not produce.
        /// </summary>
        /// <param name="parcelLocalId">Local ID of the parcel to check</param>
        /// <param name="experienceIds">The experience UUIDs to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A dictionary mapping each queried experience UUID to whether it is currently
        /// permitted to run on the parcel, or null if the capability is unavailable or the request
        /// fails</returns>
        public async Task<Dictionary<UUID, bool>?> QueryExperiencesOnParcelAsync(int parcelLocalId,
            IEnumerable<UUID> experienceIds, CancellationToken cancellationToken = default)
        {
            if (experienceIds == null) throw new ArgumentNullException(nameof(experienceIds));
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

                var idList = new List<UUID>(experienceIds);
                var qs = new StringBuilder("parcelid=").Append(parcelLocalId);
                if (idList.Count > 0)
                {
                    qs.Append("&experiences=");
                    qs.Append(string.Join(",", idList));
                }
                var requestUri = new Uri($"{cap}?{qs}");

                Dictionary<UUID, bool>? result = null;
                var (response, data) = await http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"ExperienceQuery non-success status: {response.StatusCode}", Client);
                }
                else if (data == null)
                {
                    Logger.Warn("ExperienceQuery returned no data.", Client);
                }
                else
                {
                    try
                    {
                        OSD osd = OSDParser.Deserialize(data);
                        if (osd is OSDMap map && map["experiences"] is OSDMap expMap)
                        {
                            result = new Dictionary<UUID, bool>();
                            foreach (string key in expMap.Keys)
                            {
                                result[new UUID(key)] = expMap[key].AsBoolean();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to parse ExperienceQuery response", ex, Client);
                    }
                }
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
