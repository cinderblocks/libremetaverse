namespace LibreMetaverse
{
    public class TexturePipelineSettings
    {
        public bool Enabled = true;
        public bool UseHttpTextures = true;
        public int MaxConcurrentDownloads = 4;
        public int RequestTimeout = 45_000;
    }
}
