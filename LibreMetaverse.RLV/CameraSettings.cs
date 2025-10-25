namespace LibreMetaverse.RLV
{
    public class CameraSettings
    {
        public CameraSettings(float avDistMin, float avDistMax, float fovMin, float fovMax, float zoomMin, float currentFov)
        {
            AvDistMin = avDistMin;
            AvDistMax = avDistMax;
            FovMin = fovMin;
            FovMax = fovMax;
            ZoomMin = zoomMin;
            CurrentFov = currentFov;
        }

        public float AvDistMin { get; }
        public float AvDistMax { get; }
        public float FovMin { get; }
        public float FovMax { get; }
        public float ZoomMin { get; }
        public float CurrentFov { get; }
    }
}
