// Services/FileStatusManager.cs
using System.Collections.Generic;
using System.IO;
using ScanTool.Helpers;
using ScanTool.Models;

namespace ScanTool.Services
{
    public class FileStatusInfo
    {
        public int Index { get; set; }
        public string Filename { get; set; }
        public string SizeDisplay { get; set; }
        public FileStatus Status { get; set; }
        public string LocalPath { get; set; }
        public long LocalLength { get; set; }
        public string ServerPdfkey { get; set; }
        public bool IsDirty { get; set; }
    }

    public class FileStatusManager
    {
        public List<FileStatusInfo> BuildFileList(
     string localDir,
     int maxPages,
     Dictionary<string, List<ScanRecord>> allScans,
     string archid,
     ImageCacheManager imageCache)
        {
            var result = new List<FileStatusInfo>();
            var localFiles = new Dictionary<string, string>();

            if (Directory.Exists(localDir))
            {
                foreach (var f in Directory.GetFiles(localDir, "*.JPG"))
                    localFiles[Path.GetFileName(f)] = f;
            }

            var serverFiles = new Dictionary<string, ScanRecord>();
            if (allScans != null && allScans.TryGetValue(archid, out var sl))
            {
                foreach (var s in sl)
                    serverFiles[s.Filename] = s;
            }

            int total = maxPages;
            if (localFiles.Count > total) total = localFiles.Count;
            if (serverFiles.Count > total) total = serverFiles.Count;
            if (total == 0) total = maxPages;

            for (int i = 1; i <= total; i++)
            {
                string filename = $"{i:D3}.JPG";
                bool localExists = localFiles.TryGetValue(filename, out string localPath);
                bool serverExists = serverFiles.TryGetValue(filename, out ScanRecord serverRecord);

                var info = new FileStatusInfo
                {
                    Index = i,
                    Filename = filename,
                    LocalPath = localPath
                };

                if (localExists)
                {
                    var fi = new FileInfo(localPath);
                    info.LocalLength = fi.Length;
                    info.SizeDisplay = FormatHelper.FileSize(fi.Length);

                    // 检查是否有 .tmp 文件
                    info.IsDirty = imageCache?.IsDirty(localPath) ?? File.Exists(localPath + ".tmp");
                }
                else
                {
                    info.SizeDisplay = "-";
                    info.IsDirty = false;
                }

                if (localExists && serverExists)
                {
                    if (info.IsDirty)
                    {
                        info.Status = FileStatus.Modified;
                    }
                    else
                    {
                        string localMd5 = imageCache?.GetMd5(localPath) ?? Md5Helper.CalcFileMd5(localPath);
                        info.ServerPdfkey = serverRecord.Pdfkey;
                        info.Status = localMd5 == serverRecord.Pdfkey ? FileStatus.Synced : FileStatus.LocalUpdated;
                    }
                }
                else if (localExists && !serverExists)
                {
                    info.Status = info.IsDirty ? FileStatus.Modified : FileStatus.LocalOnly;
                }
                else if (!localExists && serverExists)
                {
                    info.Status = FileStatus.ServerOnly;
                    info.LocalLength = serverRecord.Length;
                    info.SizeDisplay = FormatHelper.FileSize(serverRecord.Length);
                }
                else
                {
                    info.Status = FileStatus.NotScanned;
                }

                result.Add(info);
            }

            return result;
        }
    }
}