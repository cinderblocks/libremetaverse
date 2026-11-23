namespace LibreMetaverse
{
    public class AgentSettings
    {
        public bool SendUpdates = true;
        public bool SendUpdatesRegularly = true;
        public bool SendAppearance = true;
        public bool SendThrottle = true;
        public bool DisableUpdateDuplicateCheck = true;
        public bool MultipleSims = false;

        /// <summary>
        /// Hand over to a neighbor region on a border crossing the way the Second Life viewer does, when we
        /// already have a child agent there (<see cref="MultipleSims"/>): no second UseCircuitCode on the
        /// connected circuit, keep its capabilities and event queue when the seed capability is unchanged,
        /// and keep the appearance state instead of starting a new appearance pass. Teleports are unchanged.
        /// </summary>
        public bool FastRegionCrossing = false;
    }
}
