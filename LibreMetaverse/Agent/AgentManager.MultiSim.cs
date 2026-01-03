/**
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2026, Sjofn LLC
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

namespace OpenMetaverse
{
    /// <summary>
    /// AgentManager partial class - Multi-Simulator Support
    /// </summary>
    public partial class AgentManager
    {
        #region Multi-Simulator Support

        /// <summary>Tracks agent state per simulator for multi-sim support</summary>
        private class SimulatorAgentState
        {
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public uint LocalID { get; set; }
            public bool IsPresent { get; set; }
            public DateTime LastUpdate { get; set; }
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<Simulator, SimulatorAgentState> _simulatorStates 
            = new System.Collections.Concurrent.ConcurrentDictionary<Simulator, SimulatorAgentState>();

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<RegionCrossingPredictionEventArgs> m_RegionCrossingPredicted;

        /// <summary>Raises the RegionCrossingPredicted event</summary>
        /// <param name="e">A RegionCrossingPredictionEventArgs object containing prediction data</param>
        protected virtual void OnRegionCrossingPredicted(RegionCrossingPredictionEventArgs e)
        {
            EventHandler<RegionCrossingPredictionEventArgs> handler = m_RegionCrossingPredicted;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RegionCrossingPredictedLock = new object();

        /// <summary>Raised when a region crossing is predicted based on agent movement</summary>
        public event EventHandler<RegionCrossingPredictionEventArgs> RegionCrossingPredicted
        {
            add { lock (m_RegionCrossingPredictedLock) { m_RegionCrossingPredicted += value; } }
            remove { lock (m_RegionCrossingPredictedLock) { m_RegionCrossingPredicted -= value; } }
        }

        /// <summary>
        /// Predicts if agent will cross a region border soon based on velocity and position
        /// </summary>
        private void PredictCrossing()
        {
            if (!Client.Settings.MULTIPLE_SIMS) { return; }

            var currentSim = Client.Network.CurrentSim;
            if (currentSim == null || velocity == Vector3.Zero) { return; }

            var pos = relativePosition;
            var vel = velocity;

            // Calculate time to each border
            float timeToWest = vel.X < -0.1f ? -pos.X / vel.X : float.MaxValue;
            float timeToEast = vel.X > 0.1f ? (currentSim.SizeX - pos.X) / vel.X : float.MaxValue;
            float timeToSouth = vel.Y < -0.1f ? -pos.Y / vel.Y : float.MaxValue;
            float timeToNorth = vel.Y > 0.1f ? (currentSim.SizeY - pos.Y) / vel.Y : float.MaxValue;

            float minTime = Math.Min(Math.Min(timeToWest, timeToEast),
                                     Math.Min(timeToSouth, timeToNorth));

            // If crossing in next 3 seconds, notify
            if (minTime < 3.0f && minTime > 0)
            {
                BorderCrossingDirection direction = BorderCrossingDirection.Unknown;
                if (minTime == timeToWest) direction = BorderCrossingDirection.West;
                else if (minTime == timeToEast) direction = BorderCrossingDirection.East;
                else if (minTime == timeToSouth) direction = BorderCrossingDirection.South;
                else if (minTime == timeToNorth) direction = BorderCrossingDirection.North;

                Logger.Debug($"Crossing predicted in {minTime:F2}s towards {direction}", Client);
                OnRegionCrossingPredicted(new RegionCrossingPredictionEventArgs(currentSim, direction, minTime));
            }
        }

        /// <summary>
        /// Proactively check and connect to neighbor simulators when near borders
        /// </summary>
        private void CheckAndConnectNeighbors()
        {
            if (!Client.Settings.MULTIPLE_SIMS) { return; }

            var currentSim = Client.Network.CurrentSim;
            if (currentSim == null) { return; }

            var pos = relativePosition;
            const float connectThreshold = 64f; // Within 64m of border

            Utils.LongToUInts(currentSim.Handle, out uint x, out uint y);

            // Check each border and attempt to connect to neighbors
            if (pos.X < connectThreshold) // West border
                TryConnectToNeighbor(x - 256, y);
            if (pos.X > (currentSim.SizeX - connectThreshold)) // East
                TryConnectToNeighbor(x + 256, y);
            if (pos.Y < connectThreshold) // South
                TryConnectToNeighbor(x, y - 256);
            if (pos.Y > (currentSim.SizeY - connectThreshold)) // North
                TryConnectToNeighbor(x, y + 256);
        }

        private void TryConnectToNeighbor(uint globalX, uint globalY)
        {
            ulong handle = Utils.UIntsToLong(globalX, globalY);

            // Check if already connected
            var existing = Client.Network.FindSimulator(handle);
            if (existing != null)
            {
                return;
            }

            // The server will send EnableSimulator when it's ready
            Logger.Debug($"Neighbor region at {globalX},{globalY} not yet connected, will connect when server sends EnableSimulator", Client);
        }

        /// <summary>
        /// Updates per-simulator agent state
        /// </summary>
        private void UpdateSimulatorState(Simulator sim)
        {
            if (sim == null) return;

            _simulatorStates.AddOrUpdate(sim,
                new SimulatorAgentState
                {
                    Position = relativePosition,
                    Rotation = relativeRotation,
                    LocalID = localID,
                    IsPresent = sim.AgentMovementComplete,
                    LastUpdate = DateTime.UtcNow
                },
                (key, old) =>
                {
                    old.Position = relativePosition;
                    old.Rotation = relativeRotation;
                    old.LocalID = localID;
                    old.IsPresent = sim.AgentMovementComplete;
                    old.LastUpdate = DateTime.UtcNow;
                    return old;
                });
        }

        #region Proactive Child Agent Establishment

        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, ChildAgentStatus> _childAgentStatus
            = new System.Collections.Concurrent.ConcurrentDictionary<ulong, ChildAgentStatus>();

        private class ChildAgentStatus
        {
            public ulong RegionHandle { get; set; }
            public DateTime RequestTime { get; set; }
            public bool Established { get; set; }
            public BorderCrossingDirection Direction { get; set; }
        }

        /// <summary>
        /// Proactively establish child agents in neighboring regions before crossing
        /// </summary>
        private void ProactiveChildAgentSetup()
        {
            if (!Client.Settings.MULTIPLE_SIMS) return;

            var currentSim = Client.Network.CurrentSim;
            if (currentSim == null || !currentSim.AgentMovementComplete) return;

            var pos = relativePosition;
            var vel = velocity;
            const float setupThreshold = 128f; // Start setup at 128m from border

            Utils.LongToUInts(currentSim.Handle, out uint x, out uint y);

            // Prioritize based on movement direction and proximity
            if (vel.X < -0.5f && pos.X < setupThreshold) // Moving west
            {
                EstablishChildAgent(x - 256, y, BorderCrossingDirection.West);
            }
            else if (vel.X > 0.5f && pos.X > (currentSim.SizeX - setupThreshold)) // Moving east
            {
                EstablishChildAgent(x + 256, y, BorderCrossingDirection.East);
            }

            if (vel.Y < -0.5f && pos.Y < setupThreshold) // Moving south
            {
                EstablishChildAgent(x, y - 256, BorderCrossingDirection.South);
            }
            else if (vel.Y > 0.5f && pos.Y > (currentSim.SizeY - setupThreshold)) // Moving north
            {
                EstablishChildAgent(x, y + 256, BorderCrossingDirection.North);
            }

            // Also establish child agents in all directions if very close to corner
            if (IsNearCorner(pos, currentSim.SizeX, currentSim.SizeY, 64f))
            {
                EstablishChildAgentsInAllDirections(x, y);
            }
        }

        private bool IsNearCorner(Vector3 position, uint sizeX, uint sizeY, float threshold)
        {
            bool nearWestOrEast = position.X < threshold || position.X > (sizeX - threshold);
            bool nearSouthOrNorth = position.Y < threshold || position.Y > (sizeY - threshold);
            return nearWestOrEast && nearSouthOrNorth;
        }

        private void EstablishChildAgentsInAllDirections(uint x, uint y)
        {
            // Establish in all 4 cardinal directions
            EstablishChildAgent(x - 256, y, BorderCrossingDirection.West);
            EstablishChildAgent(x + 256, y, BorderCrossingDirection.East);
            EstablishChildAgent(x, y - 256, BorderCrossingDirection.South);
            EstablishChildAgent(x, y + 256, BorderCrossingDirection.North);
        }

        private void EstablishChildAgent(uint globalX, uint globalY, BorderCrossingDirection direction)
        {
            ulong handle = Utils.UIntsToLong(globalX, globalY);

            // Check if child agent already being established or exists
            if (_childAgentStatus.TryGetValue(handle, out var status))
            {
                // Don't re-establish if recently requested (within 30 seconds)
                if ((DateTime.UtcNow - status.RequestTime).TotalSeconds < 30)
                {
                    return;
                }
            }

            // Check if already connected
            var existing = Client.Network.FindSimulator(handle);
            if (existing != null)
            {
                Logger.Debug($"Already connected to neighbor at {globalX},{globalY}", Client);
                _childAgentStatus[handle] = new ChildAgentStatus
                {
                    RegionHandle = handle,
                    RequestTime = DateTime.UtcNow,
                    Established = true,
                    Direction = direction
                };
                return;
            }

            Logger.Debug($"Requesting child agent establishment at {globalX},{globalY} (direction: {direction})", Client);

            // Track this request
            _childAgentStatus[handle] = new ChildAgentStatus
            {
                RegionHandle = handle,
                RequestTime = DateTime.UtcNow,
                Established = false,
                Direction = direction
            };
        }

        /// <summary>
        /// Clean up child agent status tracking for regions we're no longer near
        /// </summary>
        private void CleanupChildAgentTracking()
        {
            if (!Client.Settings.MULTIPLE_SIMS) return;

            var currentSim = Client.Network.CurrentSim;
            if (currentSim == null) return;

            var now = DateTime.UtcNow;
            var staleThreshold = TimeSpan.FromMinutes(2);

            // Remove stale tracking entries
            foreach (var kvp in _childAgentStatus.ToArray())
            {
                if ((now - kvp.Value.RequestTime) > staleThreshold)
                {
                    _childAgentStatus.TryRemove(kvp.Key, out _);
                }
            }
        }

        #endregion Proactive Child Agent Establishment

        #region Object Tracking Across Simulators

        /// <summary>
        /// Tracks which simulators can see a particular object (for border objects)
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<UUID, List<Simulator>> 
            _objectSimulators = new System.Collections.Concurrent.ConcurrentDictionary<UUID, List<Simulator>>();

        /// <summary>
        /// Register that an object is visible in a particular simulator
        /// </summary>
        /// <param name="objectID">UUID of the object</param>
        /// <param name="sim">Simulator where the object is visible</param>
        public void TrackObjectInSimulator(UUID objectID, Simulator sim)
        {
            if (objectID == UUID.Zero || sim == null) return;

            _objectSimulators.AddOrUpdate(objectID,
                new List<Simulator> { sim },
                (key, list) =>
                {
                    lock (list)
                    {
                        if (!list.Contains(sim))
                        {
                            list.Add(sim);
                            Logger.Debug($"Object {objectID} now tracked in {sim.Name}", Client);
                        }
                    }
                    return list;
                });
        }

        /// <summary>
        /// Remove an object from tracking in a specific simulator
        /// </summary>
        /// <param name="objectID">UUID of the object</param>
        /// <param name="sim">Simulator to remove from</param>
        public void UntrackObjectInSimulator(UUID objectID, Simulator sim)
        {
            if (_objectSimulators.TryGetValue(objectID, out var simList))
            {
                lock (simList)
                {
                    simList.Remove(sim);
                    if (simList.Count == 0)
                    {
                        _objectSimulators.TryRemove(objectID, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Get all simulators where an object is visible
        /// </summary>
        /// <param name="objectID">UUID of the object</param>
        /// <returns>List of simulators, or empty list if not tracked</returns>
        public List<Simulator> GetSimulatorsForObject(UUID objectID)
        {
            if (_objectSimulators.TryGetValue(objectID, out var simList))
            {
                lock (simList)
                {
                    return new List<Simulator>(simList);
                }
            }
            return new List<Simulator>();
        }

        /// <summary>
        /// Check if an object is near a region border (within threshold distance)
        /// </summary>
        /// <param name="position">Position of the object in region coordinates</param>
        /// <param name="regionSizeX">Region size X</param>
        /// <param name="regionSizeY">Region size Y</param>
        /// <param name="threshold">Distance threshold to consider "near" border (default 32m)</param>
        /// <returns>True if object is near any border</returns>
        public bool IsNearBorder(Vector3 position, uint regionSizeX, uint regionSizeY, float threshold = 32f)
        {
            return position.X < threshold || 
                   position.X > (regionSizeX - threshold) ||
                   position.Y < threshold || 
                   position.Y > (regionSizeY - threshold);
        }

        /// <summary>
        /// Clean up object tracking for objects that are no longer relevant
        /// </summary>
        private void CleanupObjectTracking()
        {
            if (!Client.Settings.MULTIPLE_SIMS) return;

            var connectedSims = new HashSet<Simulator>();
            foreach (var sim in Client.Network.Simulators)
            {
                if (sim.Connected)
                    connectedSims.Add(sim);
            }

            // Remove tracking for objects in disconnected simulators
            foreach (var kvp in _objectSimulators.ToArray())
            {
                var objectID = kvp.Key;
                var simList = kvp.Value;

                lock (simList)
                {
                    simList.RemoveAll(s => !connectedSims.Contains(s));
                    
                    if (simList.Count == 0)
                    {
                        _objectSimulators.TryRemove(objectID, out _);
                        Logger.Debug($"Removed tracking for object {objectID} (no connected sims)", Client);
                    }
                }
            }
        }

        #endregion Object Tracking Across Simulators

        #endregion Multi-Simulator Support
    }
}
