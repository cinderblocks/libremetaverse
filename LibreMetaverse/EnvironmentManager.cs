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
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    /// <summary>
    /// EEP sky altitude track indices (from llenvironment.h ETrackType).
    /// Track 0 = water/underwater, tracks 1-4 = sky altitude bands.
    /// </summary>
    public enum EnvironmentTrack : int
    {
        /// <summary>Water / underwater environment track</summary>
        Water = 0,
        /// <summary>Ground-level sky track</summary>
        Ground = 1,
        /// <summary>Low altitude sky track (~1000 m)</summary>
        Low = 2,
        /// <summary>Middle altitude sky track (~2000 m)</summary>
        Middle = 3,
        /// <summary>High altitude sky track (~3000 m)</summary>
        High = 4
    }

    /// <summary>
    /// Manages EEP (Extended Environment Protocol) and legacy WindLight environment settings
    /// for the current region and its parcels.
    /// Corresponds to LLEnvironment in the SL C++ viewer (llenvironment.h / llenvironment.cpp).
    /// </summary>
    public class EnvironmentManager : IDisposable
    {
        private readonly GridClient Client;
        private bool _disposed;

        #region Events

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<RegionEnvironmentEventArgs>? m_RegionEnvironmentUpdated;

        /// <summary>Raises the RegionEnvironmentUpdated event</summary>
        protected virtual void OnRegionEnvironmentUpdated(RegionEnvironmentEventArgs e)
        {
            EventHandler<RegionEnvironmentEventArgs>? handler = m_RegionEnvironmentUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RegionEnvironmentUpdatedLock = new object();

        /// <summary>Raised when the region EEP environment is retrieved or updated via the ExtEnvironment capability</summary>
        public event EventHandler<RegionEnvironmentEventArgs> RegionEnvironmentUpdated
        {
            add { lock (m_RegionEnvironmentUpdatedLock) { m_RegionEnvironmentUpdated += value; } }
            remove { lock (m_RegionEnvironmentUpdatedLock) { m_RegionEnvironmentUpdated -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<ParcelEnvironmentEventArgs>? m_ParcelEnvironmentUpdated;

        /// <summary>Raises the ParcelEnvironmentUpdated event</summary>
        protected virtual void OnParcelEnvironmentUpdated(ParcelEnvironmentEventArgs e)
        {
            EventHandler<ParcelEnvironmentEventArgs>? handler = m_ParcelEnvironmentUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ParcelEnvironmentUpdatedLock = new object();

        /// <summary>Raised when a parcel EEP environment is retrieved or updated via the ExtEnvironment capability</summary>
        public event EventHandler<ParcelEnvironmentEventArgs> ParcelEnvironmentUpdated
        {
            add { lock (m_ParcelEnvironmentUpdatedLock) { m_ParcelEnvironmentUpdated += value; } }
            remove { lock (m_ParcelEnvironmentUpdatedLock) { m_ParcelEnvironmentUpdated -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<LegacyEnvironmentEventArgs>? m_LegacyEnvironmentUpdated;

        /// <summary>Raises the LegacyEnvironmentUpdated event</summary>
        protected virtual void OnLegacyEnvironmentUpdated(LegacyEnvironmentEventArgs e)
        {
            EventHandler<LegacyEnvironmentEventArgs>? handler = m_LegacyEnvironmentUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_LegacyEnvironmentUpdatedLock = new object();

        /// <summary>Raised when legacy WindLight environment settings are retrieved via the EnvironmentSettings capability</summary>
        public event EventHandler<LegacyEnvironmentEventArgs> LegacyEnvironmentUpdated
        {
            add { lock (m_LegacyEnvironmentUpdatedLock) { m_LegacyEnvironmentUpdated += value; } }
            remove { lock (m_LegacyEnvironmentUpdatedLock) { m_LegacyEnvironmentUpdated -= value; } }
        }

        #endregion Events

        #region Properties

        /// <summary>
        /// The region-level EEP environment, last retrieved via <see cref="GetRegionEnvironmentAsync"/>.
        /// Null until the first successful retrieval.
        /// </summary>
        public ExtEnvironmentMessage? RegionEnvironment { get; private set; }

        /// <summary>
        /// The last retrieved legacy WindLight environment (EnvironmentSettings cap).
        /// Null until <see cref="GetLegacyEnvironmentAsync"/> is called.
        /// </summary>
        public LegacyEnvironmentMessage? LegacyEnvironment { get; private set; }

        #endregion Properties

        /// <summary>
        /// Creates a new instance of the EnvironmentManager
        /// </summary>
        /// <param name="client">The GridClient instance</param>
        public EnvironmentManager(GridClient client)
        {
            Client = client;
        }

        #region EEP ExtEnvironment methods

        /// <summary>
        /// Requests the region-level EEP environment from the ExtEnvironment capability (GET, no parcel_id).
        /// Updates <see cref="RegionEnvironment"/> and raises <see cref="RegionEnvironmentUpdated"/>.
        /// Corresponds to LLEnvironment::requestRegion in the SL C++ viewer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized environment message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExtEnvironmentMessage?> GetRegionEnvironmentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExtEnvironment");
                if (cap == null)
                {
                    Logger.Warn("ExtEnvironment capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                ExtEnvironmentMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"ExtEnvironment GET region failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"ExtEnvironment GET region non-success: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null) { return; }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            var msg = new ExtEnvironmentMessage();
                            msg.Deserialize(map);
                            result = msg;
                            RegionEnvironment = msg;
                            OnRegionEnvironmentUpdated(new RegionEnvironmentEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse ExtEnvironment region response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching region environment", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Requests the EEP environment for a specific parcel from the ExtEnvironment capability
        /// (GET, with parcel_id query parameter).
        /// Raises <see cref="ParcelEnvironmentUpdated"/>.
        /// Corresponds to LLEnvironment::requestParcel in the SL C++ viewer.
        /// </summary>
        /// <param name="parcelId">The local integer parcel ID to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized environment message, or null if the capability is unavailable or the request fails</returns>
        public async Task<ExtEnvironmentMessage?> GetParcelEnvironmentAsync(int parcelId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExtEnvironment");
                if (cap == null)
                {
                    Logger.Warn("ExtEnvironment capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                var requestUri = new Uri($"{cap}?parcel_id={parcelId}");

                ExtEnvironmentMessage? result = null;
                await http.GetRequestAsync(requestUri, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"ExtEnvironment GET parcel {parcelId} failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"ExtEnvironment GET parcel {parcelId} non-success: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null) { return; }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            var msg = new ExtEnvironmentMessage();
                            msg.Deserialize(map);
                            result = msg;
                            OnParcelEnvironmentUpdated(new ParcelEnvironmentEventArgs(parcelId, msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to parse ExtEnvironment parcel {parcelId} response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error($"Failed fetching parcel {parcelId} environment", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Sets the region-level EEP environment via the ExtEnvironment capability (POST, parcel_id=-1).
        /// Requires estate manager or region owner permissions.
        /// Corresponds to LLEnvironment::updateRegion in the SL C++ viewer.
        /// </summary>
        /// <param name="environment">The environment data to apply. Pass null to reset to the grid default.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The server response (with updated version), or null on failure</returns>
        public async Task<ExtEnvironmentMessage?> SetRegionEnvironmentAsync(EnvironmentData? environment,
            CancellationToken cancellationToken = default)
        {
            return await SetEnvironmentInternalAsync(-1, environment, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the EEP environment for a specific parcel via the ExtEnvironment capability (POST).
        /// Requires parcel owner or manager permissions.
        /// Corresponds to LLEnvironment::updateParcel in the SL C++ viewer.
        /// </summary>
        /// <param name="parcelId">The local integer parcel ID to update</param>
        /// <param name="environment">The environment data to apply. Pass null to reset to the region default.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The server response (with updated version), or null on failure</returns>
        public async Task<ExtEnvironmentMessage?> SetParcelEnvironmentAsync(int parcelId,
            EnvironmentData? environment, CancellationToken cancellationToken = default)
        {
            return await SetEnvironmentInternalAsync(parcelId, environment, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resets a parcel's EEP environment to the region default by posting an empty environment.
        /// Corresponds to LLEnvironment::resetParcel in the SL C++ viewer.
        /// </summary>
        /// <param name="parcelId">The local integer parcel ID to reset</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the reset succeeded, false otherwise</returns>
        public async Task<bool> ResetParcelEnvironmentAsync(int parcelId,
            CancellationToken cancellationToken = default)
        {
            var result = await SetEnvironmentInternalAsync(parcelId, null, cancellationToken).ConfigureAwait(false);
            return result?.Success ?? false;
        }

        /// <summary>
        /// Resets the region EEP environment to the grid default by posting an empty environment.
        /// Requires estate manager or region owner permissions.
        /// Corresponds to LLEnvironment::resetRegion in the SL C++ viewer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the reset succeeded, false otherwise</returns>
        public async Task<bool> ResetRegionEnvironmentAsync(CancellationToken cancellationToken = default)
        {
            var result = await SetEnvironmentInternalAsync(-1, null, cancellationToken).ConfigureAwait(false);
            return result?.Success ?? false;
        }

        private async Task<ExtEnvironmentMessage?> SetEnvironmentInternalAsync(int parcelId,
            EnvironmentData? environment, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("ExtEnvironment");
                if (cap == null)
                {
                    Logger.Warn("ExtEnvironment capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                var body = new OSDMap(2) { ["parcel_id"] = OSD.FromInteger(parcelId) };
                if (environment != null)
                    body["environment"] = environment.Serialize();

                ExtEnvironmentMessage? result = null;
                await http.PostRequestAsync(cap, OSDFormat.Xml, body, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"ExtEnvironment POST parcel_id={parcelId} failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"ExtEnvironment POST parcel_id={parcelId} non-success: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null) { return; }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            var msg = new ExtEnvironmentMessage();
                            msg.Deserialize(map);
                            result = msg;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse ExtEnvironment POST response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error($"Failed setting environment for parcel_id={parcelId}", ex, Client);
                return null;
            }
        }

        #endregion EEP ExtEnvironment methods

        #region Legacy EnvironmentSettings methods

        /// <summary>
        /// Requests the legacy WindLight environment from the EnvironmentSettings capability (GET).
        /// Updates <see cref="LegacyEnvironment"/> and raises <see cref="LegacyEnvironmentUpdated"/>.
        /// Use this for simulators that do not support the EEP ExtEnvironment capability.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized legacy settings message, or null if the capability is unavailable or the request fails</returns>
        public async Task<LegacyEnvironmentMessage?> GetLegacyEnvironmentAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("EnvironmentSettings");
                if (cap == null)
                {
                    Logger.Warn("EnvironmentSettings capability not available.", Client);
                    return null;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return null; }

                LegacyEnvironmentMessage? result = null;
                await http.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"EnvironmentSettings GET failed: {error.Message}", Client);
                            return;
                        }
                        if (response == null || !response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"EnvironmentSettings GET non-success: {response?.StatusCode}", Client);
                            return;
                        }
                        if (data == null) { return; }
                        try
                        {
                            OSD osd = OSDParser.Deserialize(data);
                            if (!(osd is OSDMap map)) { return; }
                            var msg = new LegacyEnvironmentMessage();
                            msg.Deserialize(map);
                            result = msg;
                            LegacyEnvironment = msg;
                            OnLegacyEnvironmentUpdated(new LegacyEnvironmentEventArgs(msg));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to parse EnvironmentSettings response", ex, Client);
                        }
                    }).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching legacy environment", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Sets the legacy WindLight environment via the EnvironmentSettings capability (POST).
        /// Use this for simulators that do not support the EEP ExtEnvironment capability.
        /// </summary>
        /// <param name="settings">Raw LLSD settings data to post (sky/water/day-cycle map)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the post succeeded, false otherwise</returns>
        public async Task<bool> SetLegacyEnvironmentAsync(OSD settings,
            CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("EnvironmentSettings");
                if (cap == null)
                {
                    Logger.Warn("EnvironmentSettings capability not available.", Client);
                    return false;
                }

                var http = Client?.HttpCapsClient;
                if (http == null) { return false; }

                OSDMap body = settings is OSDMap settingsMap ? settingsMap : new OSDMap();
                bool success = false;
                await http.PostRequestAsync(cap, OSDFormat.Xml, body, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"EnvironmentSettings POST failed: {error.Message}", Client);
                            return;
                        }
                        success = response?.IsSuccessStatusCode ?? false;
                        if (!success)
                        {
                            Logger.Warn($"EnvironmentSettings POST non-success: {response?.StatusCode}", Client);
                        }
                    }).ConfigureAwait(false);
                return success;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed setting legacy environment", ex, Client);
                return false;
            }
        }

        #endregion Legacy EnvironmentSettings methods

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Dispose managed resources</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion IDisposable
    }

    /// <summary>Event args for when the region EEP environment is retrieved or updated via ExtEnvironment</summary>
    public class RegionEnvironmentEventArgs : EventArgs
    {
        /// <summary>The ExtEnvironment message returned by the capability</summary>
        public ExtEnvironmentMessage Environment { get; }
        public RegionEnvironmentEventArgs(ExtEnvironmentMessage environment) { Environment = environment; }
    }

    /// <summary>Event args for when a parcel EEP environment is retrieved or updated via ExtEnvironment</summary>
    public class ParcelEnvironmentEventArgs : EventArgs
    {
        /// <summary>The local integer parcel ID this environment applies to</summary>
        public int ParcelId { get; }
        /// <summary>The ExtEnvironment message returned by the capability</summary>
        public ExtEnvironmentMessage Environment { get; }
        public ParcelEnvironmentEventArgs(int parcelId, ExtEnvironmentMessage environment)
        {
            ParcelId = parcelId;
            Environment = environment;
        }
    }

    /// <summary>Event args for when legacy WindLight environment settings are received via EnvironmentSettings</summary>
    public class LegacyEnvironmentEventArgs : EventArgs
    {
        /// <summary>The legacy environment message returned by the capability</summary>
        public LegacyEnvironmentMessage Environment { get; }
        public LegacyEnvironmentEventArgs(LegacyEnvironmentMessage environment) { Environment = environment; }
    }
}
