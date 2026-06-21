namespace LibreMetaverse
{
    public class PacketSettings
    {
        public int MaxPendingAcks = 10;
        public int StatsQueueSize = 5;
        public int MaxResendCount = 3;
        public bool ThrottleOutgoing = true;
        public bool EnableSimStats = true;
        public bool SendPings = true;
        public bool TrackUtilization = false;
    }
}
