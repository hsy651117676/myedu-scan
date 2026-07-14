namespace ScanTool.Helpers
{
    public static class FormatHelper
    {
        public static string FileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes}B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024}KB";
            return $"{bytes / (1024 * 1024)}MB";
        }
    }
}