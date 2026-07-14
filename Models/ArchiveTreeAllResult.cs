using System.Collections.Generic;

namespace ScanTool.Models
{
    public class ArchiveTreeAllResult
    {
        public int Code { get; set; }
        public Dictionary<int, List<ArchiveItem>> Materials { get; set; }
        public Dictionary<string, List<ScanRecord>> Scans { get; set; }
    }
}