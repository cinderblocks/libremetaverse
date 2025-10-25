using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace TestClient.Commands.Stats
{
    public class NetstatsCommand : Command
    {
        public NetstatsCommand(TestClient testClient)
        {
            Name = "netstats";
            Description = "Provide packet and capabilities utilization statistics";
            Category = CommandCategory.Simulator;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (!Client.Settings.TRACK_UTILIZATION)
            {
                return "TRACK_UTILIZATION is not enabled in Settings, statistics not available";
            }


            StringBuilder packetOutput = new StringBuilder();
            StringBuilder capsOutput = new StringBuilder();

            packetOutput.AppendFormat("{0,-30}|{1,4}|{2,4}|{3,-10}|{4,-10}|" + global::System.Environment.NewLine, "Packet Name", "Sent", "Recv",
                        " TX Bytes ", " RX Bytes ");

            capsOutput.AppendFormat("{0,-30}|{1,4}|{2,4}|{3,-10}|{4,-10}|" + global::System.Environment.NewLine, "Message Name", "Sent", "Recv",
            " TX Bytes ", " RX Bytes ");
            //                "    RX    "

            long packetsSentCount = 0;
            long packetsRecvCount = 0;
            long packetBytesSent = 0;
            long packetBytesRecv = 0;

            long capsSentCount = 0;
            long capsRecvCount = 0;
            long capsBytesSent = 0;
            long capsBytesRecv = 0;

            foreach (KeyValuePair<string, OpenMetaverse.Stats.UtilizationStatistics.Stat> kvp in Client.Stats.GetStatistics())
            {
                if (kvp.Value.Type == OpenMetaverse.Stats.Type.Message)
                {                              
                    capsOutput.AppendFormat("{0,-30}|{1,4}|{2,4}|{3,-10}|{4,-10}|" + global::System.Environment.NewLine, kvp.Key, kvp.Value.TxCount, kvp.Value.RxCount,
                        FormatBytes(kvp.Value.TxBytes), FormatBytes(kvp.Value.RxBytes));

                    capsSentCount += kvp.Value.TxCount;
                    capsRecvCount += kvp.Value.RxCount;
                    capsBytesSent += kvp.Value.TxBytes;
                    capsBytesRecv += kvp.Value.RxBytes;
                }
                else if (kvp.Value.Type == OpenMetaverse.Stats.Type.Packet)
                {
                    packetOutput.AppendFormat("{0,-30}|{1,4}|{2,4}|{3,-10}|{4,-10}|" + global::System.Environment.NewLine, kvp.Key, kvp.Value.TxCount, kvp.Value.RxCount, 
                        FormatBytes(kvp.Value.TxBytes), FormatBytes(kvp.Value.RxBytes));

                    packetsSentCount += kvp.Value.TxCount;
                    packetsRecvCount += kvp.Value.RxCount;
                    packetBytesSent += kvp.Value.TxBytes;
                    packetBytesRecv += kvp.Value.RxBytes;
                }
            }

            capsOutput.AppendFormat("{0,30}|{1,4}|{2,4}|{3,-10}|{4,-10}|" + global::System.Environment.NewLine, "Capabilities Totals", capsSentCount, capsRecvCount,
                        FormatBytes(capsBytesSent), FormatBytes(capsBytesRecv));

            packetOutput.AppendFormat("{0,30}|{1,4}|{2,4}|{3,-10}|{4,-10}|" + global::System.Environment.NewLine, "Packet Totals", packetsSentCount, packetsRecvCount,
                        FormatBytes(packetBytesSent), FormatBytes(packetBytesRecv));

            return global::System.Environment.NewLine + capsOutput + global::System.Environment.NewLine + global::System.Environment.NewLine + packetOutput;
        }

        public string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new[] { "GB", "MB", "KB", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if ( bytes > max )
                    return $"{decimal.Divide(bytes, max):##.##} {order}";

                max /= scale;
            }
            return "0";
        }
    }
}
