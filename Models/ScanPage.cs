namespace ScanTool.Models
{
    public class ScanPage
    {
        public int Index { get; set; }
        public string Filename { get; set; }
        public string LocalPath { get; set; }
        public bool Processed { get; set; }
    }
}