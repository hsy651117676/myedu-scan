namespace ScanTool.Models
{
    public class FileItem
    {
        public string Filename { get; set; }
        public string DisplayText { get; set; }
        public bool IsLocal { get; set; }
        public string LocalPath { get; set; }
        public long Length { get; set; }
    }
}