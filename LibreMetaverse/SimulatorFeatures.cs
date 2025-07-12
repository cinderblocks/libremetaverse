/*
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Net.Http;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse
{
    /// <summary>
    /// Stores the Simulator Features provided by the SimulatorFeatures sim capability
    /// </summary>
    public class SimulatorFeatures
    {
        private OSDMap _featureMap = new OSDMap();
        private Simulator _simulator;
        
        internal SimulatorFeatures(Simulator simulator)
        {
            _simulator = simulator;
        }
        
        /// <summary>
        /// Returns true if Simulator Features has a given feature and corresponding true value
        /// </summary>
        /// <param name="feature"></param>
        /// <returns>true if feature exists and is true</returns>
        public bool Has(string feature)
        {
            if (_featureMap.TryGetValue(feature, out var value))
            {
                return value.AsBoolean();
            }
            return false;
        }

        /// <summary>
        /// Returns the value of a specified Simulator Feature
        /// </summary>
        /// <param name="feature"></param>
        /// <returns>Returns value of the specified Simulator Feature, null of not found.</returns>
        public OSD Get(string feature)
        {
            return _featureMap.TryGetValue(feature, out var value) ? value : null;
        }
        
        /// <summary>
        /// Set SimulatorFeatures from <see cref="OSDMap"/> 
        /// </summary>
        /// <param name="features"><see cref="OSDMap"/> of simulator features</param>
        /// <exception cref="ArgumentException">OSD type is not a map</exception>
        public void SetFeatures(HttpResponseMessage response, byte[] responseData, Exception error)
        {
            if (error != null)
            {
                Logger.Log($"Failed to retrieve simulator features. Error: {error.Message}", 
                    Helpers.LogLevel.Warning, _simulator.Client);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Failed to retrieve simulator features. Status: {response.StatusCode} {response.ReasonPhrase}",
                    Helpers.LogLevel.Warning, _simulator.Client);
                return;
            }
            OSD features = OSDParser.Deserialize(responseData);
            
            if (features.Type != OSDType.Map)
            {
                Logger.Log($"Simulator Features response was not valid LLSD map", 
                    Helpers.LogLevel.Warning, _simulator.Client);
                return;
            }
            _featureMap = (OSDMap)features;
        }
    }
}