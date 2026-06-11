namespace DesktopVideoWallpaper
{
    public class WallpaperPreset
    {
        public string Name { get; set; } = string.Empty;
        public string BackgroundPath { get; set; } = string.Empty;
        public double X { get; set; } = 0.0;
        public double Y { get; set; } = 0.0;
        public double Width { get; set; } = 1.0;
        public double Height { get; set; } = 1.0;
        public string VideoId { get; set; } = "jfKfPfyJRdk";
        public bool IsMuted { get; set; } = false;

        // 4 góc 3D (tỉ lệ từ 0.0 đến 1.0 so với màn hình)
        public double X0 { get; set; } = 0.0;
        public double Y0 { get; set; } = 0.0;
        public double X1 { get; set; } = 1.0;
        public double Y1 { get; set; } = 0.0;
        public double X2 { get; set; } = 1.0;
        public double Y2 { get; set; } = 1.0;
        public double X3 { get; set; } = 0.0;
        public double Y3 { get; set; } = 1.0;

        public bool Is3D { get; set; } = false;

        public double ZoomFactor { get; set; } = 1.0;

        public bool IsLastActive { get; set; } = false;

        public double LastTimestamp { get; set; } = 0.0;
    }
}
