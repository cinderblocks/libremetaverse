namespace LibreMetaverse
{
    public class AgentSettings
    {
        public bool SendUpdates = true;
        public bool SendUpdatesRegularly = true;
        public bool SendAppearance = true;

        /// <summary>
        /// Send the Current Outfit folder's attachments to the region (RezMultipleAttachmentsFromInv) after a
        /// successful server bake. Turn off when the application manages its attachments itself; it can still
        /// call <see cref="AppearanceManager.SendOutfitToCurrentSimulatorAsync"/> when it wants to.
        /// </summary>
        public bool SendOutfitAfterBake = true;

        /// <summary>
        /// When the outfit is sent, ask the region to detach everything first (FirstDetachAll). When off, only
        /// the objects that aren't attached already are sent, and nothing is sent if all of them are.
        /// </summary>
        public bool OutfitSendDetachAll = true;

        /// <summary>
        /// Objects in the Current Outfit folder (item ID, name) to leave out of the outfit send. null sends all of them.
        /// </summary>
        public System.Func<UUID, string, bool>? OutfitSendExclude = null;

        /// <summary>
        /// Order of the objects in the outfit send by name: lower ranks first, equal ranks in folder order.
        /// null keeps the folder order.
        /// </summary>
        public System.Func<string, int>? OutfitSendRank = null;
        public bool SendThrottle = true;
        public bool DisableUpdateDuplicateCheck = true;
        public bool MultipleSims = false;
    }
}
