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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace InventoryExplorer
{
    /// <summary>
    /// A tool to explore and export inventory contents.
    /// Demonstrates inventory navigation, searching, and data export.
    /// </summary>
    internal class InventoryExplorer
    {
        private static GridClient? client;
        private static bool inventoryComplete = false;

        static async Task<int> Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("InventoryExplorer - Explore and export inventory data");
                Console.WriteLine();
                Console.WriteLine("Usage: InventoryExplorer [firstname] [lastname] [password] [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --search <term>    Search for items by name");
                Console.WriteLine("  --type <type>      Filter by type (texture, object, notecard, etc.)");
                Console.WriteLine("  --export <file>    Export inventory tree to file");
                Console.WriteLine("  --stats            Show inventory statistics");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  InventoryExplorer John Doe password123 --stats");
                Console.WriteLine("  InventoryExplorer John Doe password123 --search \"sword\"");
                Console.WriteLine("  InventoryExplorer John Doe password123 --export inventory.txt");
                return 1;
            }

            var options = ParseOptions(args);
            
            client = new GridClient();
            
            client.Network.LoginProgress += Network_LoginProgress;
            client.Network.Disconnected += Network_Disconnected;

            Console.WriteLine("Logging in...");
            var loginParams = client.Network.DefaultLoginParams(args[0], args[1], args[2], 
                "InventoryExplorer", "1.0.0");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            try
            {
                var success = await client.Network.LoginAsync(loginParams, cts.Token);
                
                if (!success)
                {
                    Console.WriteLine($"Login failed: {client.Network.LoginMessage}");
                    return 1;
                }

                Console.WriteLine("Logged in successfully");
                Console.WriteLine("Downloading inventory...");

                // Wait for inventory to download
                var timeout = DateTime.UtcNow.AddSeconds(30);
                while (!inventoryComplete && DateTime.UtcNow < timeout)
                {
                    await Task.Delay(100);
                    if (client.Inventory.Store.Items.Count > 0)
                        inventoryComplete = true;
                }

                if (!inventoryComplete)
                {
                    Console.WriteLine("Warning: Inventory download may be incomplete");
                }

                Console.WriteLine($"Inventory loaded: {client.Inventory.Store.Items.Count} items");
                Console.WriteLine();

                // Process based on options
                if (options.ShowStats)
                    ShowStatistics();
                
                if (!string.IsNullOrEmpty(options.SearchTerm))
                    SearchInventory(options.SearchTerm, options.FilterType);
                
                if (!string.IsNullOrEmpty(options.ExportFile))
                    ExportInventory(options.ExportFile);

                if (!options.ShowStats && string.IsNullOrEmpty(options.SearchTerm) 
                    && string.IsNullOrEmpty(options.ExportFile))
                {
                    ShowInventoryTree();
                }

                client.Network.Logout();
                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Login timed out");
                return 1;
            }
        }

        private static void ShowStatistics()
        {
            if (client == null) return;

            var items = client.Inventory.Store.Items.Values.ToList();
            var folders = client.Inventory.Store.GetContents(client.Inventory.Store.RootFolder).OfType<InventoryFolder>();

            Console.WriteLine("=== Inventory Statistics ===");
            Console.WriteLine($"Total Items: {items.Count}");
            Console.WriteLine($"Root Folders: {folders.Count()}");
            Console.WriteLine();

            var byType = items.GroupBy(i => i.AssetType)
                .OrderByDescending(g => g.Count())
                .Take(10);

            Console.WriteLine("Top Item Types:");
            foreach (var group in byType)
            {
                Console.WriteLine($"  {group.Key,-20} {group.Count(),6} items");
            }
            Console.WriteLine();
        }

        private static void SearchInventory(string searchTerm, AssetType? filterType)
        {
            if (client == null) return;

            var results = client.Inventory.Store.Items.Values
                .Where(i => i.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Where(i => filterType == null || i.AssetType == filterType)
                .OrderBy(i => i.Name)
                .ToList();

            Console.WriteLine($"=== Search Results for '{searchTerm}' ===");
            Console.WriteLine($"Found {results.Count} matching items");
            Console.WriteLine();

            foreach (var item in results.Take(50))
            {
                var folder = client.Inventory.Store.Items.TryGetValue(item.ParentUUID, out var parent) 
                    ? (parent as InventoryFolder)?.Name ?? "Unknown"
                    : "Unknown";

                Console.WriteLine($"{item.Name}");
                Console.WriteLine($"  Type: {item.AssetType}");
                Console.WriteLine($"  Folder: {folder}");
                Console.WriteLine($"  UUID: {item.UUID}");
                Console.WriteLine();
            }

            if (results.Count > 50)
                Console.WriteLine($"... and {results.Count - 50} more results");
        }

        private static void ShowInventoryTree()
        {
            if (client == null) return;

            Console.WriteLine("=== Inventory Tree (Top Level) ===");
            Console.WriteLine();

            var rootContents = client.Inventory.Store.GetContents(client.Inventory.Store.RootFolder);
            
            foreach (var item in rootContents.OrderBy(i => i.Name))
            {
                if (item is InventoryFolder folder)
                {
                    var contents = client.Inventory.Store.GetContents(folder);
                    Console.WriteLine($"?? {folder.Name} ({contents.Count} items)");
                }
                else
                {
                    Console.WriteLine($"?? {item.Name} ({item.AssetType})");
                }
            }
        }

        private static void ExportInventory(string filename)
        {
            if (client == null) return;

            Console.WriteLine($"Exporting inventory to {filename}...");

            var sb = new StringBuilder();
            sb.AppendLine("LibreMetaverse Inventory Export");
            sb.AppendLine($"Date: {DateTime.Now}");
            sb.AppendLine($"Items: {client.Inventory.Store.Items.Count}");
            sb.AppendLine();

            ExportFolder(sb, client.Inventory.Store.RootFolder, 0);

            File.WriteAllText(filename, sb.ToString());
            Console.WriteLine($"Exported {client.Inventory.Store.Items.Count} items to {filename}");
        }

        private static void ExportFolder(StringBuilder sb, InventoryFolder folder, int depth)
        {
            if (client == null) return;

            var indent = new string(' ', depth * 2);
            var contents = client.Inventory.Store.GetContents(folder);

            foreach (var item in contents.OrderBy(i => i is InventoryFolder ? 0 : 1).ThenBy(i => i.Name))
            {
                if (item is InventoryFolder subfolder)
                {
                    sb.AppendLine($"{indent}[Folder] {subfolder.Name}");
                    if (depth < 10) // Prevent too deep recursion
                        ExportFolder(sb, subfolder, depth + 1);
                }
                else
                {
                    sb.AppendLine($"{indent}{item.Name} ({item.AssetType}) - {item.UUID}");
                }
            }
        }

        private static CommandOptions ParseOptions(string[] args)
        {
            var options = new CommandOptions();
            
            for (int i = 3; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--stats":
                        options.ShowStats = true;
                        break;
                    case "--search":
                        if (i + 1 < args.Length)
                            options.SearchTerm = args[++i];
                        break;
                    case "--type":
                        if (i + 1 < args.Length && Enum.TryParse<AssetType>(args[i + 1], true, out var type))
                        {
                            options.FilterType = type;
                            i++;
                        }
                        break;
                    case "--export":
                        if (i + 1 < args.Length)
                            options.ExportFile = args[++i];
                        break;
                }
            }

            return options;
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
            Console.WriteLine($"Disconnected: {e.Reason}");
        }

        private class CommandOptions
        {
            public bool ShowStats { get; set; }
            public string SearchTerm { get; set; } = string.Empty;
            public AssetType? FilterType { get; set; }
            public string ExportFile { get; set; } = string.Empty;
        }
    }
}
