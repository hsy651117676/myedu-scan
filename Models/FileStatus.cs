// Models/FileStatus.cs
namespace ScanTool.Models
{
    public enum FileStatus
    {
        NotScanned,
        LocalOnly,
        ServerOnly,
        Synced,
        Modified,
        LocalUpdated
    }
}