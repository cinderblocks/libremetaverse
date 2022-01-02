/*
 * Copyright (c) 2021-2022, Sjofn LLC
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
using OpenMetaverse;
using OpenMetaverse.Http;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse
{
    class UserReport
    {
        public enum ReportType : byte { 
            NullReport = 0, // don't use me.
            UnknownReport = 1,
            BugReport = 2,  // obsolete
            ComplaintReport = 3,
            CsRequestReport = 4
        }

        /// <summary>A reference to the current <seealso cref="GridClient"/> instance</summary>
        private GridClient Client;

        /// <summary>
        /// Construct a new instance of the UserReport class
        /// </summary>
        /// <param name="client">A reference to the current <seealso cref="GridClient"/> instance</param>
        UserReport(GridClient client)
        {
            Client = client;
        }

        /// <summary>
        /// Fetch a list of Abuse Report categories from the simulator
        /// </summary>
        /// <returns>Returns Dictionary<string string> of Abuse Report categories from the server</returns>
        public Dictionary<string, string> FetchAbuseReportCategories()
        {
            return FetchAbuseReportCategories(null);
        }

        /// <summary>
        /// Fetch a list of Abuse Report categories from the simulator
        /// </summary>
        /// <param name="lang">language to return categories in</param>
        /// <returns>Returns Dictionary<string, string> of Abuse Report categories from the server</returns>
        public Dictionary<string, string> FetchAbuseReportCategories(string lang)
        {
            Dictionary<string, string> reportCategories = null;
            Uri abuseCategoriesCap = Client.Network.CurrentSim.Caps.CapabilityURI("AbuseCategories");
            if (abuseCategoriesCap != null)
            {
                if (lang != null)
                {
                    // shite C# nonsense
                    UriBuilder builder = new UriBuilder(abuseCategoriesCap);
                    builder.Query = $"lc={lang}";
                    abuseCategoriesCap = builder.Uri;
                }

                var request = new CapsClient(abuseCategoriesCap);
                request.OnComplete += delegate (CapsClient client, OSD result, Exception error)
                {
                    if (error != null)
                    {
                        Logger.Log($"Could not fetch abuse categories from cap. ({error.Message}", 
                            Helpers.LogLevel.Info);
                        return;
                    }
                    if (result is OSDMap respMap && respMap.ContainsKey("categories"))
                    {
                        var categories = respMap["categories"] as OSDArray;
                        reportCategories = categories.Cast<OSDMap>().ToDictionary(
                            row => row["description_localized"].AsString(), 
                            row => row["category"].AsString());
                    }
                };
                request.GetRequestAsync(Client.Settings.CAPS_TIMEOUT);
            } 
            else
            {
                Logger.Log("AbuseCategories capability does not exist. Could not fetch categories list.",
                    Helpers.LogLevel.Info);
            }
            return reportCategories;
        }

        /// <summary>
        /// Sends user report first by trying SendUserReport sim cap, falling back to legacy
        /// </summary>
        /// <param name="reportType"></param>
        /// <param name="category"></param>
        /// <param name="screenshotId"></param>
        /// <param name="objectId"></param>
        /// <param name="abuserId"></param>
        /// <param name="abuseRegionName"></param>
        /// <param name="abuseRegionId"></param>
        /// <param name="pos"></param>
        /// <param name="summary"></param>
        /// <param name="details"></param>
        public void SendUserReport(ReportType reportType, int category,
            UUID screenshotId, UUID objectId, UUID abuserId,
            string abuseRegionName, UUID abuseRegionId, Vector3 pos,
            string summary, string details)
        {
            OSDMap report = new OSDMap
            {
                ["report-type"] = (byte)reportType,
                ["category"] = (byte)category,
                ["check-flags"] = (byte)0u, // this is not used
                ["screenshot-id"] = screenshotId,
                ["object-id"] = objectId,
                ["abuser-id"] = abuserId,
                ["abuse-region-name"] = "",
                ["abuse-region-id"] = UUID.Zero,
                ["position"] = pos,
                ["summary"] = summary,
                ["version-string"] = "<3 LibreMetaverse",
                ["details"] = details
            };

            Uri userReportCap = (screenshotId != UUID.Zero)
                ? Client.Network.CurrentSim.Caps.CapabilityURI("SendUserReportWithScreenshot")
                : Client.Network.CurrentSim.Caps.CapabilityURI("SendUserReport");
            if (userReportCap != null)
            {
                var request = new CapsClient(userReportCap);
                request.OnComplete += delegate(CapsClient client, OSD result, Exception error)
                {
                    if (error != null)
                    {
                        Logger.Log($"Failed to send abuse report via {userReportCap}. " +
                            $"({error.Message}) Falling back to legacy protocol.",
                            Helpers.LogLevel.Warning);
                        SendUserReportLegacy(reportType, category, screenshotId, objectId, abuserId,
                            abuseRegionName, abuseRegionId, pos, summary, details);
                    }

                };
                request.PostRequestAsync(report, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                SendUserReportLegacy(reportType, category, screenshotId, objectId, abuserId,
                    abuseRegionName, abuseRegionId, pos, summary, details);
            }
        }

        /// <summary>
        /// Sends user report using legacy lludp packet
        /// </summary>
        /// <param name="reportType"></param>
        /// <param name="category"></param>
        /// <param name="screenshotId"></param>
        /// <param name="objectId"></param>
        /// <param name="abuserId"></param>
        /// <param name="abuseRegionName"></param>
        /// <param name="abuseRegionId"></param>
        /// <param name="pos"></param>
        /// <param name="summary"></param>
        /// <param name="details"></param>
        public void SendUserReportLegacy(ReportType reportType, int category, 
            UUID screenshotId, UUID objectId, UUID abuserId,
            string abuseRegionName, UUID abuseRegionId, Vector3 pos,
            string summary, string details)
        {
            UserReportPacket urp = new UserReportPacket()
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ReportData =
                {
                    ReportType = (byte)reportType,
                    Category = (byte)category,
                    CheckFlags = (byte)0u,  // unused
                    ScreenshotID = screenshotId,
                    ObjectID = objectId,
                    AbuserID = abuserId,
                    AbuseRegionName = Utils.StringToBytes(abuseRegionName),
                    AbuseRegionID = abuseRegionId,
                    Position = pos,

                    Summary = Utils.StringToBytes(summary),
                    Details = Utils.StringToBytes(details),
                    VersionString = Utils.StringToBytes("<3 LibreMetaverse")
                }
            };
            Client.Network.SendPacket(urp);
		}
    }
}
