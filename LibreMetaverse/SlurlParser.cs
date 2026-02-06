/*
 * Copyright (c) 2022-2026, Sjofn, LLC
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
using System.Text;

namespace OpenMetaverse
{
    /// <summary>
    /// Viewer URI Name Space types (SLURL/SLAPP)
    /// </summary>
    public enum ViewerUriType
    {
        /// <summary>Unknown or invalid URI type</summary>
        Unknown,
        /// <summary>Location URI (region with coordinates)</summary>
        Location,
        /// <summary>Application URI (secondlife:///app/*)</summary>
        Application
    }

    /// <summary>
    /// Application command types for SLAPP URIs
    /// </summary>
    public enum SlappCommand
    {
        Unknown,
        Agent,
        Appearance,
        Balance,
        Chat,
        Classified,
        Event,
        Experience,
        Group,
        Help,
        Inventory,
        Keybinding,
        Login,
        MapTrackAvatar,
        ObjectIm,
        OpenFloater,
        Parcel,
        Region,
        Search,
        ShareWithAvatar,
        Teleport,
        VoiceCallAvatar,
        WearFolder,
        WorldMap
    }

    /// <summary>
    /// Parser and generator for Second Life Viewer URI Name Space (SLURL/SLAPP)
    /// Supports secondlife:// URIs as documented at:
    /// https://wiki.secondlife.com/wiki/Viewer_URI_Name_Space
    /// </summary>
    public class SlurlParser
    {
        public string Sim { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public ViewerUriType UriType { get; private set; }
        public SlappCommand Command { get; private set; }
        public string CommandPath { get; private set; }
        public Dictionary<string, string> QueryParameters { get; private set; }

        /// <summary>
        /// Parse a Second Life location string or SLURL/SLAPP URI
        /// </summary>
        /// <param name="location">Location string to parse</param>
        public SlurlParser(string location)
        {
            if (location == null) { throw new ArgumentNullException(nameof(location), "Location cannot be null."); }
            if (location.Length == 0) { throw new ArgumentException("Location cannot be empty."); }

            QueryParameters = new Dictionary<string, string>();
            
            string toParse;
            if (location.StartsWith("secondlife://"))
            {
                toParse = location.Substring(13);
            }
            else if (location.StartsWith("uri:"))
            {
                toParse = location.Substring(4);
                UriType = ViewerUriType.Location;
                ParseLegacyUri(toParse);
                return;
            }
            else
            {
                toParse = location;
            }

            // Check for application URI (starts with /app/)
            if (toParse.StartsWith("/app/"))
            {
                UriType = ViewerUriType.Application;
                ParseApplicationUri(toParse);
            }
            else
            {
                UriType = ViewerUriType.Location;
                ParseLocationUri(toParse);
            }
        }

        private void ParseLegacyUri(string uri)
        {
            // Format: uri:Region&X&Y&Z
            string[] elements = uri.Split('&');
            Sim = elements[0];
            int parsed;
            X = (elements.Length > 1 && int.TryParse(elements[1], out parsed)) ? parsed : 128;
            Y = (elements.Length > 2 && int.TryParse(elements[2], out parsed)) ? parsed : 128;
            Z = (elements.Length > 3 && int.TryParse(elements[3], out parsed)) ? parsed : 0;
        }

        private void ParseLocationUri(string uri)
        {
            // Remove trailing slash if present
            uri = uri.TrimEnd('/');
            
            // Split by '?' to separate path and query string
            string[] parts = uri.Split(new[] { '?' }, 2);
            string path = parts[0];
            
            if (parts.Length > 1)
            {
                ParseQueryString(parts[1]);
            }

            string[] elements = path.Split('/');
            Sim = elements[0];
            int parsed;
            X = (elements.Length > 1 && int.TryParse(elements[1], out parsed)) ? parsed : 128;
            Y = (elements.Length > 2 && int.TryParse(elements[2], out parsed)) ? parsed : 128;
            Z = (elements.Length > 3 && int.TryParse(elements[3], out parsed)) ? parsed : 0;
        }

        private void ParseApplicationUri(string uri)
        {
            // Remove /app/ prefix
            string appUri = uri.Substring(5);
            
            // Remove trailing slash if present
            appUri = appUri.TrimEnd('/');
            
            // Split by '?' to separate path and query string
            string[] parts = appUri.Split(new[] { '?' }, 2);
            CommandPath = parts[0];
            
            if (parts.Length > 1)
            {
                ParseQueryString(parts[1]);
            }

            // Parse the command
            string[] pathElements = CommandPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathElements.Length > 0)
            {
                Command = ParseCommandType(pathElements[0]);
                
                // For certain commands that might have location data
                if (Command == SlappCommand.Teleport || Command == SlappCommand.WorldMap)
                {
                    if (pathElements.Length > 1)
                    {
                        Sim = pathElements[1];
                        int parsed;
                        X = (pathElements.Length > 2 && int.TryParse(pathElements[2], out parsed)) ? parsed : 128;
                        Y = (pathElements.Length > 3 && int.TryParse(pathElements[3], out parsed)) ? parsed : 128;
                        Z = (pathElements.Length > 4 && int.TryParse(pathElements[4], out parsed)) ? parsed : 0;
                    }
                }
            }
        }

        private SlappCommand ParseCommandType(string command)
        {
            switch (command.ToLowerInvariant())
            {
                case "agent": return SlappCommand.Agent;
                case "appearance": return SlappCommand.Appearance;
                case "balance": return SlappCommand.Balance;
                case "chat": return SlappCommand.Chat;
                case "classified": return SlappCommand.Classified;
                case "event": return SlappCommand.Event;
                case "experience": return SlappCommand.Experience;
                case "group": return SlappCommand.Group;
                case "help": return SlappCommand.Help;
                case "inventory": return SlappCommand.Inventory;
                case "keybinding": return SlappCommand.Keybinding;
                case "login": return SlappCommand.Login;
                case "maptrackavatar": return SlappCommand.MapTrackAvatar;
                case "objectim": return SlappCommand.ObjectIm;
                case "openfloater": return SlappCommand.OpenFloater;
                case "parcel": return SlappCommand.Parcel;
                case "region": return SlappCommand.Region;
                case "search": return SlappCommand.Search;
                case "sharewithavatar": return SlappCommand.ShareWithAvatar;
                case "teleport": return SlappCommand.Teleport;
                case "voicecallavatar": return SlappCommand.VoiceCallAvatar;
                case "wear_folder": return SlappCommand.WearFolder;
                case "worldmap": return SlappCommand.WorldMap;
                default: return SlappCommand.Unknown;
            }
        }

        private void ParseQueryString(string queryString)
        {
            if (string.IsNullOrEmpty(queryString)) return;

            string[] parameters = queryString.Split('&');
            foreach (string param in parameters)
            {
                string[] keyValue = param.Split(new[] { '=' }, 2);
                if (keyValue.Length == 2)
                {
                    QueryParameters[Uri.UnescapeDataString(keyValue[0])] = Uri.UnescapeDataString(keyValue[1]);
                }
                else if (keyValue.Length == 1)
                {
                    QueryParameters[Uri.UnescapeDataString(keyValue[0])] = string.Empty;
                }
            }
        }

        /// <summary>
        /// Get the raw location string (Region/X/Y/Z)
        /// </summary>
        public string GetRawLocation()
        {
            return $"{Sim}/{X}/{Y}/{Z}";
        }

        /// <summary>
        /// Get the SLURL (secondlife://Region/X/Y/Z/)
        /// </summary>
        public string GetSlurl()
        {
            return $"secondlife://{Sim}/{X}/{Y}/{Z}/";
        }

        /// <summary>
        /// Get the legacy start location URI (uri:Region&X&Y&Z)
        /// </summary>
        public string GetStartLocationUri()
        {
            return $"uri:{Sim}&{X}&{Y}&{Z}";
        }

        /// <summary>
        /// Get a SLAPP URL for the specified command and path
        /// </summary>
        public static string GetSlappUrl(SlappCommand command, string path = null, Dictionary<string, string> queryParams = null)
        {
            StringBuilder sb = new StringBuilder("secondlife:///app/");
            
            string commandName = command.ToString().ToLowerInvariant();
            // Handle special cases with underscores
            if (command == SlappCommand.WearFolder)
                commandName = "wear_folder";
            else if (command == SlappCommand.MapTrackAvatar)
                commandName = "maptrackavatar";
            else if (command == SlappCommand.ObjectIm)
                commandName = "objectim";
            else if (command == SlappCommand.OpenFloater)
                commandName = "openfloater";
            else if (command == SlappCommand.ShareWithAvatar)
                commandName = "sharewithavatar";
            else if (command == SlappCommand.VoiceCallAvatar)
                commandName = "voicecallavatar";
            else if (command == SlappCommand.WorldMap)
                commandName = "worldmap";

            sb.Append(commandName);
            
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.StartsWith("/"))
                    sb.Append('/');
                sb.Append(path);
            }

            if (queryParams != null && queryParams.Count > 0)
            {
                sb.Append('?');
                sb.Append(string.Join("&", queryParams.Select(kvp => 
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}")));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate agent profile SLAPP URL
        /// </summary>
        public static string GetAgentUrl(UUID agentId, string action = "about")
        {
            return $"secondlife:///app/agent/{agentId}/{action}";
        }

        /// <summary>
        /// Generate group SLAPP URL
        /// </summary>
        public static string GetGroupUrl(UUID groupId, string action = "about")
        {
            return $"secondlife:///app/group/{groupId}/{action}";
        }

        /// <summary>
        /// Generate teleport SLAPP URL
        /// </summary>
        public static string GetTeleportUrl(string region, int x = 128, int y = 128, int z = 0)
        {
            return $"secondlife:///app/teleport/{region}/{x}/{y}/{z}";
        }

        /// <summary>
        /// Generate world map SLAPP URL
        /// </summary>
        public static string GetWorldMapUrl(string region, int x = 128, int y = 128, int z = 0)
        {
            return $"secondlife:///app/worldmap/{region}/{x}/{y}/{z}";
        }

        /// <summary>
        /// Generate object IM SLAPP URL
        /// </summary>
        public static string GetObjectImUrl(UUID objectId, string objectName, UUID ownerId, bool groupOwned, string slurl)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "name", objectName },
                { "owner", ownerId.ToString() },
                { "slurl", slurl }
            };
            
            if (groupOwned)
            {
                queryParams["groupowned"] = "true";
            }

            return GetSlappUrl(SlappCommand.ObjectIm, objectId.ToString(), queryParams);
        }

        /// <summary>
        /// Generate search SLAPP URL
        /// </summary>
        public static string GetSearchUrl(string category, string searchTerm)
        {
            return $"secondlife:///app/search/{category}/{Uri.EscapeDataString(searchTerm)}";
        }

        /// <summary>
        /// Generate login SLAPP URL
        /// </summary>
        public static string GetLoginUrl(string lastName = null, string sessionId = null, string location = null)
        {
            var queryParams = new Dictionary<string, string>();
            
            if (!string.IsNullOrEmpty(lastName))
                queryParams["last"] = lastName;
            if (!string.IsNullOrEmpty(sessionId))
                queryParams["session"] = sessionId;
            if (!string.IsNullOrEmpty(location))
                queryParams["location"] = location;

            return GetSlappUrl(SlappCommand.Login, null, queryParams.Count > 0 ? queryParams : null);
        }

        /// <summary>
        /// Check if this is a location URI
        /// </summary>
        public bool IsLocation => UriType == ViewerUriType.Location;

        /// <summary>
        /// Check if this is an application URI
        /// </summary>
        public bool IsApplication => UriType == ViewerUriType.Application;
    }

    /// <summary>
    /// Legacy name for SlurlParser. Use SlurlParser instead.
    /// Parser and generator for Second Life Viewer URI Name Space (SLURL/SLAPP)
    /// </summary>
    [Obsolete("Use SlurlParser instead. LocationParser is kept for backward compatibility.", false)]
    public class LocationParser : SlurlParser
    {
        /// <summary>
        /// Parse a Second Life location string or SLURL/SLAPP URI
        /// </summary>
        /// <param name="location">Location string to parse</param>
        public LocationParser(string location) : base(location)
        {
        }
    }
}
