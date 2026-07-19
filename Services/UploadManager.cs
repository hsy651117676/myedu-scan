using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Debug.WriteLine($"[UploadItem] rsid={rsid}, fl={fl}, archid={archid}, forceMode={_forceMode}, maxPages={_maxPages}, fileCount={localFiles.Count}");

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
                    Debug.WriteLine($"[UploadItem] 超大文件跳过: {f.Filename}, size={fileInfo.Length}");
                    continue;
                }
                finalFiles.Add(f);
            }

            if (oversizedFiles.Count > 0)
            {
                lock (_failedFiles)
                    _failedFiles.AddRange(oversizedFiles.Select(f => $"{f}: 文件超过5MB限制，已跳过"));
            }

            Debug.WriteLine($"[UploadItem] 过滤后: valid={finalFiles.Count}, oversized={oversizedFiles.Count}");

            if (finalFiles.Count == 0) { _lblStatus.Text = "无有效文件可上传"; return; }

            await RunParallel(finalFiles, f => UploadOne(rsid, fl, archid, f), "上传");

            if (_forceMode && _maxPages > 0)
            {
                Debug.WriteLine($"[UploadItem] 强制模式，开始清理服务器孤儿文件...");
                await CleanServerOrphans(rsid, archid.ToString(), finalFiles);
            }
        }


        public async Task UploadMultiple(string rsid, List<(int fl, int archid, List<FileItem> files)> tasks)
        {
            Debug.WriteLine($"[UploadMultiple] taskCount={tasks.Count}, forceMode={_forceMode}, maxPages={_maxPages}");

            var allFiles = new List<(int fl, int archid, FileItem file)>();
            var oversizedFiles = new List<string>();

            foreach (var (fl, archid, files) in tasks)
            {
                Debug.WriteLine($"[UploadMultiple] archid={archid}, fl={fl}, fileCount={files.Count}");
                foreach (var f in FilterValidFiles(files))
                {
                    var fileInfo = new FileInfo(f.LocalPath);
                    if (fileInfo.Exists && fileInfo.Length > 5 * 1024 * 1024)
                    {
                        oversizedFiles.Add($"{archid}/{f.Filename} ({fileInfo.Length / 1024 / 1024}MB)");
                        Debug.WriteLine($"[UploadMultiple] 超大文件跳过: archid={archid}, {f.Filename}, size={fileInfo.Length}");
                        continue;
                    }
                    allFiles.Add((fl, archid, f));
                }
            }

            if (oversizedFiles.Count > 0)
            {
                lock (_failedFiles)
                    _failedFiles.AddRange(oversizedFiles.Select(f => $"{f}: 文件超过5MB限制，已跳过"));
                _lblStatus.Text = $"已跳过 {oversizedFiles.Count} 个超过5MB的文件";
            }

            Debug.WriteLine($"[UploadMultiple] 总文件: {allFiles.Count}, 超大: {oversizedFiles.Count}");

            if (allFiles.Count == 0) { _lblStatus.Text = "无有效文件可上传"; return; }

            await RunParallel(allFiles, item => UploadOne(rsid, item.fl, item.archid, item.file), "上传");

            if (_forceMode && _maxPages > 0)
            {
                Debug.WriteLine($"[UploadMultiple] 强制模式，开始清理孤儿文件...");
                foreach (var (fl, archid, files) in tasks)
                    await CleanServerOrphans(rsid, archid.ToString(), files);
            }
        }

        private List<FileItem> FilterValidFiles(List<FileItem> files)
        {
            if (_maxPages <= 0)
            {
                Debug.WriteLine($"[FilterValidFiles] _maxPages<=0, 不过滤, count={files.Count}");
                return files;
            }
            var result = new List<FileItem>();
            foreach (var f in files)
            {
                var match = System.Text.RegularExpressions.Regex.Match(f.Filename, @"^(\d+)");
                if (match.Success && int.Parse(match.Groups[1].Value) <= _maxPages)
                {
                    result.Add(f);
                }
                else
                {
                    Debug.WriteLine($"[FilterValidFiles] 过滤: {f.Filename}, maxPages={_maxPages}");
                }
            }
            Debug.WriteLine($"[FilterValidFiles] 过滤前={files.Count}, 过滤后={result.Count}, maxPages={_maxPages}");
            return result;
        }

        private async Task<(bool skipped, bool success)> UploadOne(string rsid, int fl, int archid, FileItem f)
        {
            Debug.WriteLine($"[UploadOne] 开始: rsid={rsid}, fl={fl}, archid={archid}, file={f.Filename}, localPath={f.LocalPath}");
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(f.LocalPath))
                {
                    Debug.WriteLine($"[UploadOne] 文件不存在: {f.LocalPath}");
                    lock (_failedFiles) _failedFiles.Add($"文件不存在: {f.LocalPath}");
                    return (false, false);
                }

                var raw = File.ReadAllBytes(f.LocalPath);
                string pdfkey = Md5Helper.CalcBytesMd5(raw);
                Debug.WriteLine($"[UploadOne] 本地MD5={pdfkey}, 文件大小={raw.Length}");

                if (!_forceMode)
                {
                    string serverPdfkey = GetServerPdfkey(archid.ToString(), f.Filename);
                    Debug.WriteLine($"[UploadOne] 非强制模式, 服务器MD5={serverPdfkey ?? "null"}");
                    if (serverPdfkey != null && serverPdfkey == pdfkey)
                    {
                        Debug.WriteLine($"[UploadOne] MD5相同，跳过");
                        return (true, false);
                    }
                }
                else
                {
                    Debug.WriteLine($"[UploadOne] 强制模式，跳过MD5比较");
                }

                var encrypted = Program.Crypto.Encrypt(raw);
                Debug.WriteLine($"[UploadOne] 加密后大小={encrypted.Length}");

                if (encrypted.Length > 5 * 1024 * 1024)
                {
                    Debug.WriteLine($"[UploadOne] 加密后超过5MB，放弃");
                    lock (_failedFiles) _failedFiles.Add(
     $"人员:{rsid} 分类FL:{fl} 材料:{archid}/{f.Filename}: 加密后超过5MB限制 ({encrypted.Length} bytes)");
                    return (false, false);
                }

                Debug.WriteLine($"[UploadOne] 调用API上传...");
                var (ok, msg) = await _api.UploadScan(rsid, fl, archid.ToString(), f.Filename, encrypted, pdfkey);
                Debug.WriteLine($"[UploadOne] API返回: ok={ok}, msg={msg}");

                if (ok)
                {
                    UpdateScanCache(archid.ToString(), f.Filename, pdfkey, new FileInfo(f.LocalPath).Length);
                    Debug.WriteLine($"[UploadOne] 上传成功，缓存已更新");
                    return (false, true);
                }

                Debug.WriteLine($"[UploadOne] 上传失败，加入失败列表");
                lock (_failedFiles) _failedFiles.Add(
    $"人员:{rsid} 分类FL:{fl} 材料:{archid}/{f.Filename}\n{msg}");
                return (false, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UploadOne] 异常: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Debug.WriteLine($"[UploadOne] 内部异常: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                lock (_failedFiles) _failedFiles.Add(
    $"人员:{rsid} 分类FL:{fl} 材料:{archid}/{f.Filename}: {ex.Message}");
                return (false, false);
            }
            finally
            {
                _semaphore.Release();
                Debug.WriteLine($"[UploadOne] 结束: {f.Filename}");
            }
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