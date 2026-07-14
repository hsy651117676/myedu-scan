// Services/DownloadManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScanTool.Helpers;
using ScanTool.Models;

namespace ScanTool.Services
{
    public class DownloadManager : BaseTransferManager
    {
        private Dictionary<string, List<ScanRecord>> _allScans;
        private Dictionary<string, int> _archidToFl;

        public DownloadManager(ApiClient api, string scanDir, TreeView tv, ToolStripProgressBar progress, ToolStripTextBox lblStatus)
     : base(api, scanDir, tv, progress, lblStatus, 5) { }

        public void SetData(Dictionary<string, List<ScanRecord>> scans, Dictionary<string, int> archidToFl)
        {
            _allScans = scans;
            _archidToFl = archidToFl;
        }

        public Task DownloadWholeVolume(string rsid)
        {
            if (_allScans == null || _allScans.Count == 0) { _lblStatus.Text = "无扫描件"; return Task.CompletedTask; }
            var files = new List<(int fl, int archid, ScanRecord s)>();
            foreach (var kv in _allScans)
            {
                int fl = _archidToFl.TryGetValue(kv.Key, out var f) ? f : 0;
                int archid = int.Parse(kv.Key);
                foreach (var s in kv.Value) files.Add((fl, archid, s));
            }
            return DownloadFiles(rsid, files);
        }

        public Task DownloadCategory(string rsid, int fl)
        {
            var files = new List<(int fl, int archid, ScanRecord s)>();
            foreach (TreeNode node in _tv.Nodes) CollectFiles(node, files, fl);
            return DownloadFiles(rsid, files);
        }

        public Task DownloadItem(string rsid, int fl, int archid)
        {
            if (!_allScans.TryGetValue(archid.ToString(), out var list) || list.Count == 0)
            { _lblStatus.Text = "无扫描件"; return Task.CompletedTask; }
            return DownloadFiles(rsid, list.Select(s => (fl, archid, s)).ToList());
        }

        private async Task DownloadFiles(string rsid, List<(int fl, int archid, ScanRecord s)> files)
        {
            await RunParallel(files, item => DownloadOne(rsid, item.fl, item.archid, item.s), "下载");
        }

        private async Task<(bool skipped, bool success)> DownloadOne(string rsid, int fl, int archid, ScanRecord s)
        {
            await _semaphore.WaitAsync();
            try
            {
                string rsidPadded = rsid.PadLeft(8, '0');
                var outDir = Path.Combine(_scanDir, rsidPadded, fl.ToString(), archid.ToString());
                Directory.CreateDirectory(outDir);
                string localPath = Path.Combine(outDir, s.Filename);

                if (File.Exists(localPath) && Md5Helper.CalcFileMd5(localPath) == s.Pdfkey)
                    return (true, false);

                string url = $"{_api.BaseUrl}/das_images/YS/{rsidPadded}/{fl}/{archid}/{s.Filename}";
                var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (a, b, c, d) => true };
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) })
                {
                    var resp = await client.GetAsync(url);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        lock (_failedFiles) _failedFiles.Add($"{archid}/{s.Filename}\nURL: {url}\nHTTP {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");
                        return (false, false);
                    }
                    var encData = await resp.Content.ReadAsByteArrayAsync();
                    var decData = Program.Crypto.Decrypt(encData);
                    File.WriteAllBytes(localPath, decData);
                    return (false, true);
                }
            }
            catch (Exception ex)
            {
                var err = $"{archid}/{s.Filename}\nURL: {_api.BaseUrl}/das_images/YS/{rsid.PadLeft(8, '0')}/{fl}/{archid}/{s.Filename}\n{ex.GetType().Name}: {ex.Message}";
                if (ex.InnerException != null)
                    err += $"\n内部: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                lock (_failedFiles) _failedFiles.Add(err);
                return (false, false);
            }
            finally { _semaphore.Release(); }
        }

        private void CollectFiles(TreeNode parent, List<(int fl, int archid, ScanRecord s)> result, int targetFl)
        {
            foreach (TreeNode node in parent.Nodes)
            {
                if (node.Tag is NodeTag tag && tag.Archid != null && tag.Fl != "0" && int.Parse(tag.Fl) == targetFl)
                {
                    if (_allScans.TryGetValue(tag.Archid, out var list) && list.Count > 0)
                        result.AddRange(list.Select(s => (int.Parse(tag.Fl), int.Parse(tag.Archid), s)));
                }
                if (node.Nodes.Count > 0) CollectFiles(node, result, targetFl);
            }
        }
    }
}