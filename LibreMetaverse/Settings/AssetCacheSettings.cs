using System.IO;

namespace LibreMetaverse
{
    public class AssetCacheSettings
    {
        public bool Enabled = true;
        public string Dir = Path.Combine(Settings.ResourceDir, "cache");
        public long MaxSize = 1024L * 1024 * 1024;
    }
}
