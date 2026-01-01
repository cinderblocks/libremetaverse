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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace PrimInspector
{
    /// <summary>
    /// A simple tool to inspect primitive objects in a virtual world.
    /// Demonstrates object discovery, property inspection, and primitive types.
    /// </summary>
    internal class PrimInspector
    {
        private static GridClient? client;
        private static readonly ManualResetEvent primPropertiesEvent = new ManualResetEvent(false);

        static async Task<int> Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("PrimInspector - Inspect primitives in a virtual world");
                Console.WriteLine();
                Console.WriteLine("Usage: PrimInspector [firstname] [lastname] [password] [search_term]");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  PrimInspector John Doe password123");
                Console.WriteLine("  PrimInspector John Doe password123 \"Chair\"");
                return 1;
            }

            var searchTerm = args.Length > 3 ? args[3] : string.Empty;

            client = new GridClient
            {
                Settings = { MULTIPLE_SIMS = false }
            };

            client.Network.LoginProgress += Network_LoginProgress;
            client.Network.Disconnected += Network_Disconnected;
            client.Objects.ObjectProperties += Objects_ObjectProperties;

            Console.WriteLine("Logging in...");
            var loginParams = client.Network.DefaultLoginParams(args[0], args[1], args[2], 
                "PrimInspector", "1.0.0");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            try
            {
                var success = await client.Network.LoginAsync(loginParams, cts.Token);
                
                if (!success)
                {
                    Console.WriteLine($"Login failed: {client.Network.LoginMessage}");
                    return 1;
                }

                Console.WriteLine($"Logged in to {client.Network.CurrentSim.Name}");
                Console.WriteLine($"Position: {client.Self.SimPosition}");
                Console.WriteLine();

                // Wait a moment for objects to load
                await Task.Delay(2000);

                // Find and inspect objects
                InspectNearbyObjects(searchTerm);

                Console.WriteLine();
                Console.WriteLine("Press Enter to logout...");
                Console.ReadLine();

                client.Network.Logout();
                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Login timed out");
                return 1;
            }
        }

        private static void InspectNearbyObjects(string searchTerm)
        {
            if (client?.Network.CurrentSim == null)
                return;

            var objects = client.Network.CurrentSim.ObjectsPrimitives.Values.ToList();
            
            Console.WriteLine($"Found {objects.Count} objects in current simulator");
            Console.WriteLine();

            // Filter by search term if provided
            var filtered = string.IsNullOrEmpty(searchTerm)
                ? objects
                : objects.Where(o => (o.Properties?.Name?.Contains(searchTerm, 
                    StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            if (filtered.Count == 0 && !string.IsNullOrEmpty(searchTerm))
            {
                Console.WriteLine($"No objects found matching '{searchTerm}'");
                return;
            }

            // Group by distance
            var sorted = filtered
                .Select(o => new { Prim = o, Distance = Vector3.Distance(o.Position, client.Self.SimPosition) })
                .OrderBy(x => x.Distance)
                .Take(10)
                .ToList();

            Console.WriteLine($"Inspecting {sorted.Count} nearest objects:");
            Console.WriteLine(new string('-', 80));

            foreach (var item in sorted)
            {
                InspectPrimitive(item.Prim, item.Distance);
                Console.WriteLine(new string('-', 80));
            }
        }

        private static void InspectPrimitive(Primitive prim, float distance)
        {
            // Request properties if we don't have them
            if (prim.Properties == null && client != null)
            {
                primPropertiesEvent.Reset();
                client.Objects.SelectObject(client.Network.CurrentSim, prim.LocalID);
                primPropertiesEvent.WaitOne(3000);
            }

            Console.WriteLine($"Object: {prim.Properties?.Name ?? "Unknown"} (ID: {prim.ID})");
            Console.WriteLine($"  Local ID: {prim.LocalID}");
            Console.WriteLine($"  Type: {prim.Type}");
            Console.WriteLine($"  Distance: {distance:F2}m");
            Console.WriteLine($"  Position: {prim.Position}");
            Console.WriteLine($"  Rotation: {prim.Rotation}");
            Console.WriteLine($"  Scale: {prim.Scale}");
            
            if (prim.ParentID != 0)
                Console.WriteLine($"  Parent ID: {prim.ParentID}");

            Console.WriteLine($"  Flags: {prim.Flags}");

            if (prim.PrimData != null)
            {
                Console.WriteLine($"  Material: {prim.PrimData.Material}");
                Console.WriteLine($"  PCode: {prim.PrimData.PCode}");
                Console.WriteLine($"  Profile: {prim.PrimData.ProfileCurve}");
                Console.WriteLine($"  Path: {prim.PrimData.PathCurve}");
            }

            if (prim.Sculpt != null)
            {
                Console.WriteLine($"  Sculpt Type: {prim.Sculpt.Type}");
                Console.WriteLine($"  Sculpt Texture: {prim.Sculpt.SculptTexture}");
            }

            if (prim.Light != null)
            {
                Console.WriteLine($"  Light: Color={prim.Light.Color}, Intensity={prim.Light.Intensity}, Radius={prim.Light.Radius}");
            }

            if (prim.Flexible != null)
            {
                Console.WriteLine($"  Flexible: Softness={prim.Flexible.Softness}, Gravity={prim.Flexible.Gravity}");
            }

            if (prim.Properties != null)
            {
                Console.WriteLine($"  Owner: {prim.Properties.OwnerID}");
                Console.WriteLine($"  Creator: {prim.Properties.CreatorID}");
                Console.WriteLine($"  Description: {prim.Properties.Description}");
                
                if (prim.Properties.SaleType != SaleType.Not)
                    Console.WriteLine($"  For Sale: {prim.Properties.SaleType} L${prim.Properties.SalePrice}");
            }

            if (!string.IsNullOrEmpty(prim.Text))
                Console.WriteLine($"  Hover Text: {prim.Text}");
        }

        private static void Network_LoginProgress(object? sender, LoginProgressEventArgs e)
        {
            if (e.Status == LoginStatus.Success)
            {
                Console.WriteLine("Login successful");
            }
            else if (e.Status == LoginStatus.Failed)
            {
                Console.WriteLine($"Login failed: {e.Message}");
            }
        }

        private static void Network_Disconnected(object? sender, DisconnectedEventArgs e)
        {
            Console.WriteLine($"Disconnected: {e.Reason} - {e.Message}");
        }

        private static void Objects_ObjectProperties(object? sender, ObjectPropertiesEventArgs e)
        {
            primPropertiesEvent.Set();
        }
    }
}
