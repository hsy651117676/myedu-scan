// Services/UploadManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScanTool.Helpers;
using ScanTool.Models;

namespace ScanTool.Services
{
    public class UploadManager : BaseTransferManager
    {
        private Dictionary<string, List<ScanRecord>> _allScans;
        private bool _forceMode;
        private int _maxPages;

        public UploadManager(ApiClient api, string scanDir, TreeView tv, ToolStripProgressBar progress, ToolStripTextBox lblStatus)
     : base(api, scanDir, tv, progress, lblStatus, 3) { }

        public void SetScanData(Dictionary<string, List<ScanRecord>> scans) => _allScans = scans;
        public void SetForceMode(bool force) => _forceMode = force;
        public void SetMaxPages(int maxPages) => _maxPages = maxPages;

        public async Task UploadItem(string rsid, int fl, int archid, List<FileItem> localFiles)
        {
            var validFiles = FilterValidFiles(localFiles);
            var oversizedFiles = new List<string>();

            var finalFiles = new List<FileItem>();
            foreach (var f in validFiles)
            {
                var fileInfo = new FileInfo(f.LocalPath);
                if (fileInfo.Exists && fileInfo.Length > 5 * 1024 * 1024)
                {
                    oversizedFiles.Add(
    $"人员:{rsid} 分类FL:{fl} 材料:{archid}/{f.Filename} ({new FileInfo(f.LocalPath).Length / 1024 / 1024}MB)");
                    continue;
                }
                finalFiles.Add(f);
            }

            if (oversizedFiles.Count > 0)
            {
                lock (_failedFiles)
                    _failedFiles.AddRange(oversizedFiles.Select(f => $"{f}: 文件超过5MB限制，已跳过"));
            }

            if (finalFiles.Count == 0) { _lblStatus.Text = "无有效文件可上传"; return; }

            await RunParallel(finalFiles, f => UploadOne(rsid, fl, archid, f), "上传");

            if (_forceMode && _maxPages > 0)
                await CleanServerOrphans(rsid, archid.ToString(), finalFiles);
        }


        public async Task UploadMultiple(string rsid, int fl, List<(int archid, List<FileItem> files)> tasks)
        {
            var allFiles = new List<(int archid, FileItem file)>();
            var oversizedFiles = new List<string>();

            foreach (var (archid, files) in tasks)
            {
                foreach (var f in FilterValidFiles(files))
                {
                    // ✅ 提前检查文件大小
                    var fileInfo = new FileInfo(f.LocalPath);
                    if (fileInfo.Exists && fileInfo.Length > 5 * 1024 * 1024)
                    {
                        oversizedFiles.Add($"{archid}/{f.Filename} ({fileInfo.Length / 1024 / 1024}MB)");
                        continue;
                    }
                    allFiles.Add((archid, f));
                }
            }

            if (oversizedFiles.Count > 0)
            {
                lock (_failedFiles)
                    _failedFiles.AddRange(oversizedFiles.Select(f => $"{f}: 文件超过5MB限制，已跳过"));
                _lblStatus.Text = $"已跳过 {oversizedFiles.Count} 个超过5MB的文件";
            }

            if (allFiles.Count == 0) { _lblStatus.Text = "无有效文件可上传"; return; }

            await RunParallel(allFiles, item => UploadOne(rsid, fl, item.archid, item.file), "上传");

            if (_forceMode && _maxPages > 0)
            {
                foreach (var (archid, _) in tasks)
                    await CleanServerOrphans(rsid, archid.ToString(), tasks.SelectMany(x => x.files).ToList());
            }
        }
        private List<FileItem> FilterValidFiles(List<FileItem> files)
        {
            if (_maxPages <= 0) return files;
            return files.Where(f =>
            {
                var match = System.Text.RegularExpressions.Regex.Match(f.Filename, @"^(\d+)");
                return match.Success && int.Parse(match.Groups[1].Value) <= _maxPages;
            }).ToList();
        }

        private async Task<(bool skipped, bool success)> UploadOne(string rsid, int fl, int archid, FileItem f)
        {
            await _semaphore.WaitAsync();
            try
            {
                var raw = File.ReadAllBytes(f.LocalPath);
                string pdfkey = Md5Helper.CalcBytesMd5(raw);

                if (!_forceMode)
                {
                    string serverPdfkey = GetServerPdfkey(archid.ToString(), f.Filename);
                    if (serverPdfkey != null && serverPdfkey == pdfkey)
                        return (true, false);
                }

                var encrypted = Program.Crypto.Encrypt(raw);

                // ✅ 加密后再检查一次（加密后体积会略大于原始）
                if (encrypted.Length > 5 * 1024 * 1024)
                {
                    lock (_failedFiles) _failedFiles.Add(
     $"人员:{rsid} 分类FL:{fl} 材料:{archid}/{f.Filename}: 文件超过5MB限制 ({new FileInfo(f.LocalPath).Length / 1024 / 1024}MB)");
                    return (false, false);
                }

                var (ok, msg) = await _api.UploadScan(rsid, fl, archid.ToString(), f.Filename, encrypted, pdfkey);
                if (ok)
                {
                    UpdateScanCache(archid.ToString(), f.Filename, pdfkey, new FileInfo(f.LocalPath).Length);
                    return (false, true);
                }

                lock (_failedFiles) _failedFiles.Add(
    $"人员:{rsid} 分类FL:{fl} 材料:{archid}/{f.Filename}\n{msg}");
                return (false, false);
            }
            catch (Exception ex)
            {
                lock (_failedFiles) _failedFiles.Add(
    $"人员:{rsid} 分类FL:{fl} 材料:{archid}/{f.Filename}: {ex.Message}");
                return (false, false);
            }
            finally { _semaphore.Release(); }
        }


        private async Task CleanServerOrphans(string rsid, string archid, List<FileItem> localFiles)
        {
            if (!_allScans.TryGetValue(archid, out var serverFiles)) return;
            var localFilenames = new HashSet<string>(localFiles.Select(f => f.Filename.ToUpper()));
            var toDelete = serverFiles.Where(s => !localFilenames.Contains(s.Filename.ToUpper())).ToList();
            foreach (var s in toDelete)
            {
                try { await _api.DeleteScan(rsid, archid, s.Filename); }
                catch { }
            }
            if (toDelete.Count > 0)
            {
                lock (_allScans)
                {
                    if (_allScans.TryGetValue(archid, out var list))
                        list.RemoveAll(x => toDelete.Any(d => d.Filename == x.Filename));
                }
            }
        }

        private string GetServerPdfkey(string archid, string filename)
        {
            if (_allScans.TryGetValue(archid, out var list))
                return list.FirstOrDefault(x => x.Filename.Equals(filename, StringComparison.OrdinalIgnoreCase))?.Pdfkey;
            return null;
        }

        private void UpdateScanCache(string archid, string filename, string pdfkey, long length)
        {
            lock (_allScans)
            {
                if (!_allScans.TryGetValue(archid, out var list))
                { list = new List<ScanRecord>(); _allScans[archid] = list; }
                var existing = list.FirstOrDefault(x => x.Filename.Equals(filename, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { existing.Pdfkey = pdfkey; existing.Length = length; }
                else list.Add(new ScanRecord { Filename = filename, Pdfkey = pdfkey, Length = length });
            }
        }
    }
}