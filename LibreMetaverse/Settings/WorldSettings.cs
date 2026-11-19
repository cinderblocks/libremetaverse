namespace LibreMetaverse
{
    public class WorldSettings
    {
        public bool AlwaysDecodeObjects = true;
        public bool AlwaysRequestObjects = true;
        public bool TrackObjects = true;
        public bool TrackAvatars = true;
        public bool CachePrimitives = false;
        public bool UseInterpolationTimer = true;
        public bool StoreLandPatches = false;

        /// <summary>
        /// Directory for the on-disk object cache (<see cref="ObjectDiskCache"/>). null or empty, the default,
        /// keeps no cache and tells every region the cache is empty. Set it before connecting.
        /// </summary>
        public string? ObjectCacheDirectory = null;

        /// <summary>Most objects the on-disk object cache keeps per region</summary>
        public int ObjectCacheMaxEntriesPerRegion = 40000;
    }
}
