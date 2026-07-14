namespace ScanTool.Models
{
    public class PersonInfo
    {
        public string Rsid { get; set; }
        public string Name { get; set; }
        public string UnitName { get; set; }
        public string ArchiveNo { get; set; }
        public override string ToString() => $"{Name} ({UnitName})";
    }
}