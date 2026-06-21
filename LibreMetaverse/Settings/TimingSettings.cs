namespace LibreMetaverse
{
    public class TimingSettings
    {
        public int TransferTimeout = 90_000;
        public int TeleportTimeout = 40_000;
        public int LogoutTimeout = 5_000;
        public int CapsTimeout = 60_000;
        public int LoginTimeout = 60_000;
        public int ResendTimeout = 4_000;
        public int SimulatorTimeout = 30_000;
        public int MapRequestTimeout = 5_000;
        public int AgentUpdateInterval = 500;
        public int InterpolationInterval = 250;
    }
}
