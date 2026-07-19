using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScanTool.Models;
using ScanTool.Services;

namespace ScanTool.Controls
{
    public class ScanBatchHandler
    {
        private readonly ScanControl _owner;
        private readonly ApiClient _api;
        private readonly string _scanDir;
        private readonly TreeView _tvMaterials;
        private readonly ToolStripProgressBar _progressBar;
        private readonly ToolStripTextBox _lblStatus;
        private readonly ImageProcessor _processor;
        private readonly ImageEditor _editor;
        private readonly ViewportController _viewport;
        private readonly ImageCacheManager _imageCache;
        private readonly ScanListManager _listManager;
        private readonly ScanService _scanService;
        private readonly Func<string> _getColorMode;
        private readonly Func<CropPreset> _getCurrentPreset;
        private readonly Action<string> _setEditingFile;
        private readonly Func<string> _getEditingFile;
        private readonly Action _loadExistingFiles;
        private readonly Action _updateNodeStatus;
        private readonly Action<string> _setLocalPath;
        private bool _cancelled;

        public ScanBatchHandler(ScanControl owner, ApiClient api, string scanDir,
            TreeView tvMaterials, ToolStripProgressBar progressBar, ToolStripTextBox lblStatus,
            ImageProcessor processor, ImageEditor editor, ViewportController viewport,
            ImageCacheManager imageCache, ScanListManager listManager,
            ScanService scanService,
            Func<string> getColorMode, Func<CropPreset> getCurrentPreset,
            Action<string> setEditingFile, Func<string> getEditingFile,
            Action loadExistingFiles, Action updateNodeStatus, Action<string> setLocalPath)
        {
            _owner = owner;
            _api = api;
            _scanDir = scanDir;
            _tvMaterials = tvMaterials;
            _progressBar = progressBar;
            _lblStatus = lblStatus;
            _processor = processor;
            _editor = editor;
            _viewport = viewport;
            _imageCache = imageCache;
            _listManager = listManager;
            _scanService = scanService;
            _getColorMode = getColorMode;
            _getCurrentPreset = getCurrentPreset;
            _setEditingFile = setEditingFile;
            _getEditingFile = getEditingFile;
            _loadExistingFiles = loadExistingFiles;
            _updateNodeStatus = updateNodeStatus;
            _setLocalPath = setLocalPath;
        }

        public void Cancel() => _cancelled = true;

        // ==================== 批量修复 ====================

        public async Task StartBatchCurrentItem(Func<RepairProfile> getProfile)
        {
            var profile = getProfile();
            if (profile != null && profile.Name == "不自动修复")
            {
                _lblStatus.Text = "当前为不自动修复模式，跳过批量处理";
                return;
            }

            if (MessageBox.Show($"将使用【{profile?.Name ?? "自动判断修复"}】自动修复当前材料的所有页面。\n\n确定开始？",
                "确认批量处理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _lblStatus.Text = $"当前材料【{profile?.Name ?? "自动判断修复"}】处理中...";
            await RunBatchRepair(() => false, profile);
            _owner.RefreshAfterBatch();
        }

        public async Task StartBatchCategory(int currentFl, Func<RepairProfile> getProfile)
        {
            var profile = getProfile();
            if (profile != null && profile.Name == "不自动修复")
            {
                _lblStatus.Text = "当前为不自动修复模式，跳过批量处理";
                return;
            }

            if (MessageBox.Show($"将使用【{profile?.Name ?? "自动判断修复"}】自动修复当前类所有材料。\n\n确定开始？",
                "确认批量处理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _lblStatus.Text = $"当前类【{profile?.Name ?? "自动判断修复"}】处理中...";

            int loopCount = 0;

            while (true)
            {
                loopCount++;

                string localDir = Path.Combine(_scanDir, _owner.CurrentRsid.PadLeft(8, '0'), _owner.CurrentFl.ToString(), _owner.CurrentArchid.ToString());

                bool hasFiles = false;
                for (int i = 0; i < _listManager.Count; i++)
                {
                    var f = _listManager.GetFile(i);
                    if (f != null && f.LocalPath != null && File.Exists(f.LocalPath))
                    { hasFiles = true; break; }
                }

                if (Directory.Exists(localDir) && hasFiles)
                {
                    await RunBatchRepair(() => false, profile);
                }

                var nextNode = FindNextMaterialInCategory(_tvMaterials.SelectedNode, currentFl);

                if (nextNode != null)
                {
                    _tvMaterials.SelectedNode = nextNode;

                    if (nextNode.Tag is NodeTag tag && tag.Archid != null)
                    {
                        int archid = int.Parse(tag.Archid);
                        int fl = int.Parse(tag.Fl);

                        _owner.SetCurrentArchid(archid);
                        _owner.SetCurrentFl(fl);

                        var match = System.Text.RegularExpressions.Regex.Match(nextNode.Text, @"\((\d+)");
                        int maxPages = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                        _owner.SetMaxPages(maxPages);

                        _owner.RefreshListAndStatus();

                        if (_listManager.Count > 0) _listManager.SelectPage(0);
                        else { _editor.SetImage(null); _viewport.SetOriginalImage(null); _processor.Clear(); _setEditingFile(null); }
                    }

                    await Task.Delay(300);
                }
                else
                {
                    break;
                }
            }

            _owner.RefreshAfterBatch();
            _lblStatus.Text = $"【{profile?.Name ?? "自动判断修复"}】处理完成";
        }

        public async Task StartBatchFromCurrent(Func<RepairProfile> getProfile)
        {
            var profile = getProfile();
            if (profile != null && profile.Name == "不自动修复")
            {
                _lblStatus.Text = "当前为不自动修复模式，跳过批量处理";
                return;
            }

            if (MessageBox.Show($"将使用【{profile?.Name ?? "自动判断修复"}】从当前材料开始自动修复。\n\n确定开始？",
                "确认批量处理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _lblStatus.Text = $"【{profile?.Name ?? "自动判断修复"}】处理中...";

            TreeNode startNode = _tvMaterials.SelectedNode;
            if (startNode == null || !(startNode.Tag is NodeTag tag) || tag.Archid == null)
            {
                _lblStatus.Text = "请先选择一个材料";
                return;
            }

            int currentFl = _owner.CurrentFl;

            TreeNode flNode = startNode.Parent;
            if (flNode == null)
            {
                _lblStatus.Text = "无法获取分类节点";
                return;
            }

            var allMaterialNodes = new List<TreeNode>();
            CollectAllMaterialNodes(flNode, currentFl, allMaterialNodes);

            int startIndex = allMaterialNodes.IndexOf(startNode);
            if (startIndex < 0)
            {
                _lblStatus.Text = "当前材料不在列表中";
                return;
            }

            for (int i = startIndex; i < allMaterialNodes.Count; i++)
            {
                var node = allMaterialNodes[i];
                if (!(node.Tag is NodeTag nodeTag) || nodeTag.Archid == null) continue;

                _tvMaterials.SelectedNode = node;
                int archid = int.Parse(nodeTag.Archid);
                int fl = int.Parse(nodeTag.Fl);
                _owner.SetCurrentArchid(archid);
                _owner.SetCurrentFl(fl);

                var match = System.Text.RegularExpressions.Regex.Match(node.Text, @"\((\d+)");
                int maxPages = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                _owner.SetMaxPages(maxPages);
                _owner.RefreshListAndStatus();

                if (_listManager.Count > 0) _listManager.SelectPage(0);
                else { _editor.SetImage(null); _viewport.SetOriginalImage(null); _processor.Clear(); _setEditingFile(null); }

                await Task.Delay(200);

                string localDir = Path.Combine(_scanDir, _owner.CurrentRsid.PadLeft(8, '0'), _owner.CurrentFl.ToString(), _owner.CurrentArchid.ToString());
                bool hasFiles = false;
                for (int j = 0; j < _listManager.Count; j++)
                {
                    var f = _listManager.GetFile(j);
                    if (f != null && f.LocalPath != null && File.Exists(f.LocalPath))
                    { hasFiles = true; break; }
                }

                if (Directory.Exists(localDir) && hasFiles)
                {
                    await RunBatchRepair(() => false, profile);
                }
                else
                {
                    _lblStatus.Text = $"跳过 {node.Text}（无本地文件）";
                }
            }

            _owner.RefreshAfterBatch();
            _lblStatus.Text = $"【{profile?.Name ?? "自动判断修复"}】处理完成";
        }

        public async Task StartBatchVolume(Func<RepairProfile> getProfile)
        {
            var profile = getProfile();
            if (profile != null)
            {
                _lblStatus.Text = "整卷自动修复仅在「自动判断修复」模式下可用";
                return;
            }

            if (MessageBox.Show($"将使用【自动判断修复】自动修复整卷所有材料。\n\n确定开始？",
                "确认批量处理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _lblStatus.Text = "整卷【自动判断修复】处理中...";

            var flNodes = new List<TreeNode>();
            foreach (TreeNode node in _tvMaterials.Nodes)
                CollectFlNodes(node, flNodes);

            foreach (var flNode in flNodes)
            {
                if (_cancelled) break;
                if (!(flNode.Tag is NodeTag flTag)) continue;
                int fl = int.Parse(flTag.Fl);

                var materialNodes = new List<TreeNode>();
                CollectAllMaterialNodesInFl(flNode, materialNodes);

                foreach (var materialNode in materialNodes)
                {
                    if (_cancelled) break;
                    if (!(materialNode.Tag is NodeTag tag) || tag.Archid == null) continue;

                    _tvMaterials.SelectedNode = materialNode;
                    int archid = int.Parse(tag.Archid);
                    _owner.SetCurrentArchid(archid);
                    _owner.SetCurrentFl(fl);

                    var match = System.Text.RegularExpressions.Regex.Match(materialNode.Text, @"\((\d+)");
                    int maxPages = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                    _owner.SetMaxPages(maxPages);
                    _owner.RefreshListAndStatus();

                    if (_listManager.Count > 0) _listManager.SelectPage(0);
                    else { _editor.SetImage(null); _viewport.SetOriginalImage(null); _processor.Clear(); _setEditingFile(null); }

                    await Task.Delay(200);

                    string localDir = Path.Combine(_scanDir, _owner.CurrentRsid.PadLeft(8, '0'), fl.ToString(), archid.ToString());
                    bool hasFiles = false;
                    for (int i = 0; i < _listManager.Count; i++)
                    {
                        var f = _listManager.GetFile(i);
                        if (f != null && f.LocalPath != null && File.Exists(f.LocalPath))
                        { hasFiles = true; break; }
                    }

                    if (Directory.Exists(localDir) && hasFiles)
                    {
                        await RunBatchRepair(() => false, profile);
                    }
                    else
                    {
                        _lblStatus.Text = $"跳过 {materialNode.Text}（无本地文件）";
                    }
                }
            }

            _owner.RefreshAfterBatch();
            _lblStatus.Text = _cancelled ? "已取消" : "整卷【自动判断修复】处理完成";
        }

        private async Task RunBatchRepair(Func<bool> hasNextMaterial, RepairProfile profile)
        {
            _cancelled = false;

            while (!_cancelled)
            {
                int idx = _listManager.SelectedIndex;
                var info = _listManager.GetFile(idx);
                int total = _listManager.Count;

                if (info != null && info.LocalPath != null && File.Exists(info.LocalPath))
                {
                    Bitmap originalBmp = null;
                    Bitmap result = null;

                    try
                    {
                        originalBmp = new Bitmap(info.LocalPath);
                        _processor.LoadImage(originalBmp);
                        _editor.SetImage(originalBmp);
                        _viewport.SetOriginalImage(originalBmp);
                        _viewport.FitToScreen();

                        await Task.Delay(10);

                        if (profile == null)
                        {
                            Debug.WriteLine($"[BatchRepair] 材料: {_tvMaterials.SelectedNode?.Text}, 文件: {info.Filename}");
                            var autoProfile = AutoRepairService.Analyze(originalBmp);
                            if (autoProfile != null && autoProfile.Name != "不自动修复")
                            {
                                result = AutoRepairService.Execute(originalBmp, autoProfile);
                            }
                            else
                            {
                                result = new Bitmap(originalBmp);
                            }

                            if (result != null)
                            {
                                _processor.LoadImage(result);
                                _editor.SetImage(result);
                                _viewport.SetOriginalImage(result);
                                _viewport.FitToScreen();
                            }

                            await Task.Delay(10);
                            var finalResult = _processor.CurrentBitmap ?? result;
                            if (finalResult != null)
                            {
                                _imageCache.Store(info.LocalPath, finalResult);
                            }
                        }
                        else if (profile.Name != "不自动修复")
                        {
                            _processor.LoadImage(originalBmp);

                            if (profile.AutoDeskew) _processor.AutoDeskew();
                            if (profile.Denoise) _processor.Denoise();
                            if (profile.RemoveBlackBorder) _processor.RemoveBlackBorder();
                            if (profile.Grayscale) _processor.Grayscale();
                            if (profile.Brightness) _processor.Brightness(profile.BrightnessValue);
                            if (profile.Contrast) _processor.Contrast(profile.ContrastValue);

                            var procResult = _processor.CurrentBitmap;
                            if (procResult != null)
                            {
                                _editor.SetImage(procResult);
                                _viewport.SetOriginalImage(procResult);
                                _viewport.FitToScreen();
                                _imageCache.Store(info.LocalPath, procResult);
                            }
                        }
                        else
                        {
                            result = new Bitmap(originalBmp);
                            _imageCache.Store(info.LocalPath, result);
                        }

                        _lblStatus.Text = $"【{profile?.Name ?? "自动判断修复"}】已处理: {info.Filename}";
                    }
                    finally
                    {
                        originalBmp?.Dispose();
                        if (result != null && result != _processor.CurrentBitmap)
                        {
                            result.Dispose();
                        }
                    }
                }
                else
                {
                    _lblStatus.Text = $"跳过: {info?.Filename ?? "空"}";
                }

                if (idx >= total - 1)
                {
                    if (hasNextMaterial())
                        _owner.OnToolAction("next_page");
                    else
                        break;
                }
                else
                {
                    int beforeIdx = _listManager.SelectedIndex;
                    _owner.OnToolAction("next_page");
                    int afterIdx = _listManager.SelectedIndex;
                    if (afterIdx == beforeIdx && _listManager.Count == total)
                        break;
                }
            }

            _loadExistingFiles();
            _updateNodeStatus();
            _lblStatus.Text = _cancelled ? "已取消" : $"【{profile?.Name ?? "自动判断修复"}】处理完成";
        }

        private TreeNode FindNextMaterialInCategory(TreeNode current, int fl)
        {
            if (current == null) return null;

            TreeNode flNode = current.Parent;
            if (flNode == null) return null;

            var allNodes = new List<TreeNode>();
            CollectMaterialNodes(flNode, fl, allNodes);

            int idx = allNodes.IndexOf(current);
            if (idx < 0) return null;

            for (int i = idx + 1; i < allNodes.Count; i++)
            {
                var node = allNodes[i];
                if (node.Tag is NodeTag tag && tag.Archid != null && int.Parse(tag.Fl) == fl)
                    return node;
            }

            return null;
        }

        private void CollectMaterialNodes(TreeNode parent, int fl, List<TreeNode> result)
        {
            foreach (TreeNode child in parent.Nodes)
            {
                if (child.Tag is NodeTag tag && tag.Archid != null && int.Parse(tag.Fl) == fl)
                {
                    result.Add(child);
                }
                CollectMaterialNodes(child, fl, result);
            }
        }

        private void CollectAllMaterialNodes(TreeNode parent, int fl, List<TreeNode> result)
        {
            foreach (TreeNode child in parent.Nodes)
            {
                if (child.Tag is NodeTag tag && tag.Archid != null && int.Parse(tag.Fl) == fl)
                {
                    result.Add(child);
                }
                CollectAllMaterialNodes(child, fl, result);
            }
        }

        private void CollectFlNodes(TreeNode parent, List<TreeNode> result)
        {
            if (parent.Tag is NodeTag t && t.Fl != null && t.Fl != "0" && t.Archid == null)
                result.Add(parent);

            foreach (TreeNode child in parent.Nodes)
                CollectFlNodes(child, result);
        }

        private void CollectAllMaterialNodesInFl(TreeNode flNode, List<TreeNode> result)
        {
            foreach (TreeNode child in flNode.Nodes)
            {
                if (child.Tag is NodeTag tag && tag.Archid != null)
                {
                    result.Add(child);
                }
                else
                {
                    CollectAllMaterialNodesInFl(child, result);
                }
            }
        }

        // ==================== 替换扫描 ====================

        public void ReplaceScan(Dictionary<string, List<ScanRecord>> allScans,
    ScanService scanService, ImageCacheManager imageCache, ImageProcessor processor,
    ImageEditor editor, ViewportController viewport, ScanListManager listManager,
    string scanDir, string currentRsid, int currentFl, int currentArchid,
    Func<string> getColorMode, Func<CropPreset> getCurrentPreset,
    Action<string> setEditingFile, Func<string> getEditingFile,
    Action loadExistingFiles, Action updateNodeStatus, Action<string> setLocalPath,
    Func<RepairProfile> getRepairProfile)
        {
            var info = listManager.GetFile(listManager.SelectedIndex);
            if (info == null)
            {
                MessageBox.Show("请先在文件列表中选择要替换的文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (currentArchid == 0)
            {
                MessageBox.Show("请先选择材料目录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dr = MessageBox.Show($"将替换文件：{info.Filename}\n\n重新扫描一页替换该文件。\n\n确定继续？",
                "替换扫描确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;

            try
            {
                if (!string.IsNullOrEmpty(info.LocalPath) && File.Exists(info.LocalPath))
                {
                    imageCache.Release(info.LocalPath);
                    File.Delete(info.LocalPath);
                }

                imageCache.Release(getEditingFile());
                editor.SetImage(null);
                viewport.SetOriginalImage(null);
                processor.Clear();
                setEditingFile(null);

                _lblStatus.Text = $"正在替换扫描: {info.Filename}...";
                var preset = getCurrentPreset();
                var bmp = scanService.Scan(preset);
                if (bmp == null)
                {
                    MessageBox.Show("扫描失败", "扫描错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    loadExistingFiles();
                    return;
                }

                var profile = getRepairProfile();
                Bitmap finalBmp = bmp;

                try
                {
                    if (profile == null)
                    {
                        var autoProfile = AutoRepairService.Analyze(bmp);
                        if (autoProfile != null && autoProfile.Name != "不自动修复")
                        {
                            var repaired = AutoRepairService.Execute(bmp, autoProfile);
                            if (repaired != null)
                            {
                                bmp.Dispose();
                                finalBmp = repaired;
                            }
                        }
                    }
                    else if (profile.Name != "不自动修复")
                    {
                        processor.LoadImage(bmp);

                        if (profile.AutoDeskew) processor.AutoDeskew();
                        if (profile.Denoise) processor.Denoise();
                        if (profile.RemoveBlackBorder) processor.RemoveBlackBorder();
                        if (profile.Grayscale) processor.Grayscale();
                        if (profile.Brightness) processor.Brightness(profile.BrightnessValue);
                        if (profile.Contrast) processor.Contrast(profile.ContrastValue);

                        var repaired = processor.CurrentBitmap;
                        if (repaired != null)
                        {
                            bmp.Dispose();
                            finalBmp = new Bitmap(repaired);
                        }
                    }

                    string outDir = Path.Combine(scanDir, currentRsid.PadLeft(8, '0'), currentFl.ToString(), currentArchid.ToString());
                    Directory.CreateDirectory(outDir);
                    string path = Path.Combine(outDir, info.Filename);

                    using (var saveBmp = new Bitmap(finalBmp))
                    {
                        saveBmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }

                    setEditingFile(path);
                    loadExistingFiles();
                    int newIdx = -1;
                    for (int i = 0; i < listManager.Count; i++)
                    {
                        var f = listManager.GetFile(i);
                        if (f != null && f.Filename == info.Filename)
                        {
                            newIdx = i;
                            break;
                        }
                    }
                    if (newIdx >= 0) listManager.SelectPage(newIdx);

                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    using (var loaded = new Bitmap(fs))
                    {
                        processor.LoadImage(loaded);
                        editor.SetImage(loaded);
                        viewport.SetOriginalImage(loaded);
                        viewport.FitToScreen();
                    }

                    imageCache.MarkDirty(path);
                    updateNodeStatus();
                    setLocalPath(outDir);
                    _lblStatus.Text = $"替换扫描完成: {info.Filename}";
                }
                finally
                {
                    if (finalBmp != null && finalBmp != bmp)
                    {
                        finalBmp.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"替换扫描失败: {ex.Message}";
                loadExistingFiles();
            }
        }

        // ==================== 修改页数 ====================

        public void ShowEditPageCountDialog(Dictionary<int, List<ArchiveItem>> allMaterials,
            string currentRsid, string currentArchid, Action<string, int> onUpdated)
        {
            var node = _tvMaterials.SelectedNode;
            if (node?.Tag is NodeTag tag && tag.Archid != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(node.Text, @"\((\d+)");
                string cp = match.Success ? match.Groups[1].Value : "0";
                using (var f = new Form
                {
                    Text = "修改页数",
                    Size = new Size(320, 200),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                })
                {
                    f.Controls.Add(new Label { Text = $"材料：{node.Text}\n\n请输入新的页数：", Location = new Point(15, 15), Size = new Size(280, 50), AutoSize = false });
                    var txt = new TextBox { Text = cp, Location = new Point(15, 75), Width = 200, Font = new Font("微软雅黑", 11F) };
                    txt.KeyPress += (s2, e2) => { if (!char.IsDigit(e2.KeyChar) && e2.KeyChar != (char)Keys.Back) e2.Handled = true; };
                    f.Controls.Add(txt);
                    f.Controls.Add(new Button { Text = "保存", Location = new Point(50, 115), Width = 80, Height = 28, DialogResult = DialogResult.OK });
                    f.Controls.Add(new Button { Text = "取消", Location = new Point(160, 115), Width = 80, Height = 28, DialogResult = DialogResult.Cancel });
                    f.AcceptButton = (Button)f.Controls[2];
                    f.CancelButton = (Button)f.Controls[3];
                    if (f.ShowDialog() == DialogResult.OK &&
                        System.Text.RegularExpressions.Regex.IsMatch(txt.Text.Trim(), @"^\d+$") &&
                        int.TryParse(txt.Text.Trim(), out int np) && np > 0)
                        _ = UpdatePageCount(tag.Archid, np, allMaterials, currentRsid, currentArchid, onUpdated);
                    else if (f.DialogResult == DialogResult.OK)
                        MessageBox.Show("请输入有效的正整数。", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private async Task UpdatePageCount(string archid, int newPages,
            Dictionary<int, List<ArchiveItem>> allMaterials,
            string currentRsid, string currentArchid, Action<string, int> onUpdated)
        {
            try
            {
                _lblStatus.Text = "正在更新页数...";
                var (ok, msg) = await _api.UpdatePageCount(currentRsid, archid, newPages);
                if (ok)
                {
                    foreach (var kv in allMaterials)
                        foreach (var item in kv.Value)
                            if (item.ARCHID.ToString() == archid)
                            { item.YS = newPages; break; }
                    onUpdated(archid, newPages);
                    _lblStatus.Text = $"页数已更新为 {newPages}";
                }
                else
                {
                    _lblStatus.Text = $"页数更新失败: {msg}";
                    MessageBox.Show(msg, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex) { _lblStatus.Text = $"页数更新失败: {ex.Message}"; }
        }

        // ==================== 清理多余文件 ====================

        public async Task CleanOrphanFiles(Dictionary<string, List<ScanRecord>> allScans,
            int maxPages, string currentRsid, string currentArchid, Action refreshLocalTree)
        {
            if (maxPages == 0 || allScans == null) { _lblStatus.Text = "没有多余文件"; return; }
            var toDelete = new List<string>();
            if (allScans.TryGetValue(currentArchid, out var scanList))
                foreach (var s in scanList)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(s.Filename, @"^(\d+)");
                    if (m.Success && int.Parse(m.Groups[1].Value) > maxPages) toDelete.Add(s.Filename);
                }
            if (toDelete.Count == 0) { _lblStatus.Text = "没有多余文件"; return; }
            if (MessageBox.Show($"发现 {toDelete.Count} 个多余文件，是否删除？\n\n{string.Join("\n", toDelete.Take(10))}",
                "清理多余文件", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _progressBar.Visible = true; _progressBar.Value = 0;
            int deleted = 0;
            for (int i = 0; i < toDelete.Count; i++)
            {
                try { if (await _api.DeleteScan(currentRsid, currentArchid, toDelete[i])) deleted++; } catch { }
                _progressBar.Value = Math.Min(i + 1, toDelete.Count);
                _lblStatus.Text = $"清理: {i + 1}/{toDelete.Count}";
            }
            _progressBar.Visible = false;
            _lblStatus.Text = $"已删除 {deleted} 个多余文件";
            if (deleted > 0) refreshLocalTree();
        }
    }
}