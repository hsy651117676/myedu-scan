using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScanTool.Models;
using ScanTool.Services;

namespace ScanTool.Controls
{
    public class ScanTransferHandler
    {
        private readonly ScanControl _owner;
        private readonly ApiClient _api;
        private readonly string _scanDir;
        private readonly TreeView _tvMaterials;
        private readonly ToolStripProgressBar _progressBar;
        private readonly ToolStripTextBox _lblStatus;
        private UploadManager _uploadManager;
        private DownloadManager _downloadManager;
        private Dictionary<string, List<ScanRecord>> _allScans;
        private Dictionary<string, int> _archidToFl;
        private List<string> _failedFiles = new List<string>();

        public List<string> FailedFiles => _failedFiles;

        public ScanTransferHandler(ScanControl owner, ApiClient api, string scanDir,
            TreeView tvMaterials, ToolStripProgressBar progressBar, ToolStripTextBox lblStatus)
        {
            _owner = owner;
            _api = api;
            _scanDir = scanDir;
            _tvMaterials = tvMaterials;
            _progressBar = progressBar;
            _lblStatus = lblStatus;
        }

        public void Init(Dictionary<string, List<ScanRecord>> allScans, Dictionary<string, int> archidToFl)
        {
            _allScans = allScans;
            _archidToFl = archidToFl;
            _uploadManager = new UploadManager(_api, _scanDir, _tvMaterials, _progressBar, _lblStatus);
            _downloadManager = new DownloadManager(_api, _scanDir, _tvMaterials, _progressBar, _lblStatus);
            _uploadManager.SetScanData(_allScans);
            _downloadManager.SetData(_allScans, _archidToFl);
        }

        public void SetMaxPages(int maxPages)
        {
            _uploadManager?.SetMaxPages(maxPages);
        }

        // ========== 材料级 ==========

        public Task UploadItem(string rsid, int fl, int archid)
        {
            var files = GetLocalFiles(rsid, fl, archid);
            _uploadManager.SetMaxPages(_owner.MaxPages);
            return _uploadManager.UploadItem(rsid, fl, archid, files);
        }

        public Task ForceUploadItem(string rsid, int fl, int archid)
        {
            _uploadManager.SetForceMode(true);
            _uploadManager.SetMaxPages(_owner.MaxPages);
            var files = GetLocalFiles(rsid, fl, archid);
            return _uploadManager.UploadItem(rsid, fl, archid, files).ContinueWith(_ => _uploadManager.SetForceMode(false));
        }

        public Task DownloadItem(string rsid, int fl, int archid)
        {
            return _downloadManager.DownloadItem(rsid, fl, archid);
        }

        public Task ForceDownloadItem(string rsid, int fl, int archid)
        {
            ForceDeleteLocalItem(rsid, fl, archid);
            return _downloadManager.DownloadItem(rsid, fl, archid);
        }

        // ========== 类级 ==========

        public Task UploadCategory(string rsid, int fl)
        {
            var tasks = CollectAllLocalFiles(rsid, fl);
            return _uploadManager.UploadMultiple(rsid, fl, tasks);
        }

        public Task ForceUploadCategory(string rsid, int fl)
        {
            _uploadManager.SetForceMode(true);
            var tasks = CollectAllLocalFiles(rsid, fl);
            return _uploadManager.UploadMultiple(rsid, fl, tasks).ContinueWith(_ => _uploadManager.SetForceMode(false));
        }

        public Task DownloadCategory(string rsid, int fl)
        {
            return _downloadManager.DownloadCategory(rsid, fl);
        }

        public Task ForceDownloadCategory(string rsid, int fl)
        {
            ForceDeleteLocalCategory(rsid, fl);
            return _downloadManager.DownloadCategory(rsid, fl);
        }

        // ========== 整卷 ==========

        public Task UploadWholeVolume(string rsid)
        {
            var tasks = CollectAllLocalFiles(rsid);
            return _uploadManager.UploadMultiple(rsid, 0, tasks);
        }

        public Task ForceUploadWholeVolume(string rsid)
        {
            _uploadManager.SetForceMode(true);
            var tasks = CollectAllLocalFiles(rsid);
            return _uploadManager.UploadMultiple(rsid, 0, tasks).ContinueWith(_ => _uploadManager.SetForceMode(false));
        }

        public Task DownloadWholeVolume(string rsid)
        {
            return _downloadManager.DownloadWholeVolume(rsid);
        }

        public Task ForceDownloadWholeVolume(string rsid)
        {
            ForceDeleteLocalAll(rsid);
            return _downloadManager.DownloadWholeVolume(rsid);
        }

        // ========== 辅助 ==========

        public void RefreshAfterTransfer()
        {
            _failedFiles = _uploadManager?.FailedFiles ?? _downloadManager?.FailedFiles ?? new List<string>();
            _owner.RefreshLocalTree();
        }

        private List<FileItem> GetLocalFiles(string rsid, int fl, int archid)
        {
            string dir = Path.Combine(_scanDir, rsid.PadLeft(8, '0'), fl.ToString(), archid.ToString());
            var files = new List<FileItem>();
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.JPG"))
                {
                    var fi = new FileInfo(f);
                    files.Add(new FileItem { Filename = fi.Name, LocalPath = f, IsLocal = true, Length = fi.Length });
                }
            return files;
        }

        private List<(int archid, List<FileItem> files)> CollectAllLocalFiles(string rsid, int? targetFl = null)
        {
            var tasks = new List<(int archid, List<FileItem> files)>();
            foreach (TreeNode node in _tvMaterials.Nodes)
                CollectLocalMaterials(node, rsid, tasks, targetFl);
            return tasks;
        }

        private void CollectLocalMaterials(TreeNode parent, string rsid,
            List<(int archid, List<FileItem> files)> result, int? targetFl = null)
        {
            foreach (TreeNode node in parent.Nodes)
            {
                if (node.Tag is NodeTag tag && tag.Archid != null && tag.Fl != "0")
                {
                    if (targetFl.HasValue && int.Parse(tag.Fl) != targetFl.Value) continue;
                    string dir = Path.Combine(_scanDir, rsid.PadLeft(8, '0'), tag.Fl, tag.Archid);
                    var files = new List<FileItem>();
                    if (Directory.Exists(dir))
                        foreach (var f in Directory.GetFiles(dir, "*.JPG"))
                        {
                            var fi = new FileInfo(f);
                            files.Add(new FileItem { Filename = fi.Name, LocalPath = f, IsLocal = true, Length = fi.Length });
                        }
                    if (files.Count > 0) result.Add((int.Parse(tag.Archid), files));
                }
                if (node.Nodes.Count > 0) CollectLocalMaterials(node, rsid, result, targetFl);
            }
        }

        private void ForceDeleteLocalAll(string rsid)
        {
            string dir = Path.Combine(_scanDir, rsid.PadLeft(8, '0'));
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.JPG", SearchOption.AllDirectories))
                    File.Delete(f);
        }

        private void ForceDeleteLocalCategory(string rsid, int fl)
        {
            string dir = Path.Combine(_scanDir, rsid.PadLeft(8, '0'), fl.ToString());
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.JPG", SearchOption.AllDirectories))
                    File.Delete(f);
        }

        private void ForceDeleteLocalItem(string rsid, int fl, int archid)
        {
            string dir = Path.Combine(_scanDir, rsid.PadLeft(8, '0'), fl.ToString(), archid.ToString());
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.JPG"))
                    File.Delete(f);
        }
    }
}