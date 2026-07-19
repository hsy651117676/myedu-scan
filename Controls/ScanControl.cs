using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ScanTool.Helpers;
using ScanTool.Models;
using ScanTool.Services;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace ScanTool.Controls
{
    public partial class ScanControl : UserControl
    {
        private ApiClient _api;
        private string _scanDir;
        private string _currentRsid;
        private string _currentPersonName;
        private int _currentArchid;
        private int _currentFl;
        private int _maxPages;
        private string _viewerPath = "";
        private string _savedScannerName = "";
        private ScanService _scanService;
        private ImageProcessor _processor;
        private ImageEditor _editor;
        private MaterialTreeManager _treeManager;
        private ScanListManager _listManager;
        private ScanToolbar _toolbar;
        private ImageCacheManager _imageCache;
        private ViewportController _viewport;
        private EraseController _eraseController;
        private PdfExporter _pdfExporter;
        private CropPresetManager _presetManager;
        private RepairProfileManager _repairManager;
        private FileStatusManager _fileStatusManager;
        private ScanContextMenu _contextMenu;
        private ScanTransferHandler _transferHandler;
        private ScanBatchHandler _batchHandler;

        private Dictionary<int, List<ArchiveItem>> _allMaterials;
        private Dictionary<string, List<ScanRecord>> _allScans;
        private Dictionary<string, int> _archidToFl;
        private List<string> _failedFiles = new List<string>();
        private List<FileStatusInfo> _currentFileList;
        private int _scanCounter;
        private bool _isProcessing;
        private string _currentEditingFile;
        private List<string> _expandedNodePaths = new List<string>();
        public async Task StartBatchVolume() => await _batchHandler.StartBatchVolume(GetCurrentRepairProfile);
        public void SetCurrentArchid(int archid) => _currentArchid = archid;
        public void SetCurrentFl(int fl) => _currentFl = fl;
        public void SetMaxPages(int maxPages) => _maxPages = maxPages;
        public void RefreshListAndStatus()
        {
            LoadExistingFiles();
            UpdateCurrentNodeStatus();
        }
        public int MaxPages => _maxPages;

        public string CurrentRsid => _currentRsid;
        public int CurrentFl => _currentFl;
        public int CurrentArchid => _currentArchid;
        public ScanControl(ScanService scanService)
        {
            _scanService = scanService;
            InitializeComponent();
            _contextMenu = new ScanContextMenu(this, ConfirmForce);
            _processor = new ImageProcessor();
            _editor = new ImageEditor(picPreview);
            _listManager = new ScanListManager(dgvFiles);
            _toolbar = new ScanToolbar(panelTools, OnToolAction);
            _imageCache = new ImageCacheManager();
            _viewport = new ViewportController(picPreview);
            _eraseController = new EraseController(picPreview);
            _pdfExporter = new PdfExporter();
            _fileStatusManager = new FileStatusManager();

            _listManager.SelectedIndexChanged += OnPageSelected;
            this.cmbPreset.SelectedIndexChanged += cmbPreset_SelectedIndexChanged;
            this.cmbScanner.DropDown += (s, e) => LoadScanners(true);
            btnMaxScan.Click += (s, e) => ScanMaxOnly();
            btnAutoDetect.Click += (s, e) => ScanAutoDetect();
            lblLocalPath.Click += (s, e) =>
            {
                if (Directory.Exists(lblLocalPath.Text))
                    System.Diagnostics.Process.Start("explorer.exe", lblLocalPath.Text);
            };

            menuFileSave.Click += (s, e) => OnToolAction("save_selected");
            menuFileRestore.Click += (s, e) => OnToolAction("restore_selected");
            menuFileUpload.Click += (s, e) => OnToolAction("upload_selected");
            menuFileDownload.Click += (s, e) => OnToolAction("download_selected");
            menuFileDelete.Click += (s, e) => OnToolAction("delete_selected");
            menuFileOpen.Click += (s, e) => OnToolAction("open_viewer");
            menuFileReplaceScan.Click += (s, e) => ReplaceScan();
            menuFileContext.Opening += menuFileContext_Opening;
            menuFileReorder.Click += (s, e) => ShowReorderDialog();

            dgvFiles.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var info = _listManager.GetFile(e.RowIndex);
                if (info == null) return;
                if (info.Status == FileStatus.ServerOnly)
                {
                    var dr = MessageBox.Show($"本地文件 {info.Filename} 不存在，是否从服务器下载？", "文件缺失",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dr == DialogResult.Yes) _ = DownloadSingleFile(info);
                    return;
                }
                if (info.LocalPath != null && File.Exists(info.LocalPath))
                    OpenFileWithViewer();
            };

            tvMaterials.HideSelection = false;
            tvMaterials.DrawMode = TreeViewDrawMode.OwnerDrawText;
            tvMaterials.DrawNode += (s, e) =>
            {
                if ((e.State & TreeNodeStates.Selected) != 0)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(51, 153, 255)))
                        e.Graphics.FillRectangle(brush, e.Bounds);
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.NodeFont ?? tvMaterials.Font,
                        e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.NodeFont ?? tvMaterials.Font,
                        e.Bounds, e.Node.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
            };
        }

        public void SetViewerPath(string path) => _viewerPath = path;

        public bool ConfirmForce()
        {
            return MessageBox.Show("⚠ 覆盖操作将删除已有文件并重新传输！\n\n确定要继续吗？", "警告",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }
        private void ShowReorderDialog()
        {
            string localDir = Path.Combine(_scanDir, _currentRsid.PadLeft(8, '0'), _currentFl.ToString(), _currentArchid.ToString());
            var dialog = new FileReorderDialog(localDir, _imageCache, () =>
            {
                _currentEditingFile = null;
                _imageCache.ReleaseAll();
                _editor.SetImage(null);
                _viewport.SetOriginalImage(null);
                _processor.Clear();
                LoadExistingFiles();
                UpdateCurrentNodeStatus();
                lblStatus.Text = "文件顺序已调整";
            });
            dialog.Show();
        }
        public void Init(ApiClient api, string scanDir, CropPresetManager presetManager, string savedScannerName)
        {
            _api = api; _scanDir = scanDir; _presetManager = presetManager;
            _savedScannerName = savedScannerName;
            _treeManager = new MaterialTreeManager(tvMaterials, scanDir);

            var configDir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "ScanTool");
            _repairManager = new RepairProfileManager(configDir);

            _transferHandler = new ScanTransferHandler(this, api, scanDir, tvMaterials, progressBar1, lblStatus);
            _batchHandler = new ScanBatchHandler(this, api, scanDir, tvMaterials, progressBar1, lblStatus,
                _processor, _editor, _viewport, _imageCache, _listManager, _scanService,
                GetColorMode, GetCurrentPreset,
                v => _currentEditingFile = v, () => _currentEditingFile,
                LoadExistingFiles, UpdateCurrentNodeStatus, v => lblLocalPath.Text = v);

            this.tvMaterials.AfterSelect -= tvMaterials_AfterSelect;
            this.tvMaterials.NodeMouseClick -= tvMaterials_NodeMouseClick;
            this.tvMaterials.AfterSelect += tvMaterials_AfterSelect;
            this.tvMaterials.NodeMouseClick += tvMaterials_NodeMouseClick;
            if (cmbScanner.Items.Count == 0) LoadScanners();
            RefreshPresetList();
            RefreshRepairModeList();
        }

        public async void SetPerson(string rsid, string name)
        {
            _currentRsid = rsid; _currentPersonName = name;
            lblPersonInfo.Text = $"正在加载 {name}...";
            _currentFileList = new List<FileStatusInfo>();
            _listManager.SetFiles(_currentFileList);
            _scanCounter = 0; _maxPages = 0;
            _imageCache.ReleaseAll(); _currentEditingFile = null;
            _editor.SetImage(null); _viewport.SetOriginalImage(null); _processor.Clear();

            try
            {
                var result = await _api.GetAllMaterials(_currentRsid);
                _allMaterials = result?.Materials ?? new Dictionary<int, List<ArchiveItem>>();
                _allScans = result?.Scans ?? new Dictionary<string, List<ScanRecord>>();
                _treeManager.SetData(_allMaterials, _allScans);
                _treeManager.BuildTree(_currentRsid, _allScans, _imageCache);
                _archidToFl = _treeManager.ArchidToFl;
                _transferHandler?.Init(_allScans, _archidToFl);

                var orphans = new List<string>();
                foreach (var kv in _allScans)
                    if (!_archidToFl.ContainsKey(kv.Key))
                        orphans.Add(kv.Key);

                if (orphans.Count > 0)
                {
                    var msg = $"发现 {orphans.Count} 个孤立扫描件，是否清理？";
                    if (MessageBox.Show(msg, "发现孤立扫描件", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        await _api.CleanOrphans(_currentRsid);
                        var reload = await _api.GetAllMaterials(_currentRsid);
                        _allMaterials = reload?.Materials ?? new Dictionary<int, List<ArchiveItem>>();
                        _allScans = reload?.Scans ?? new Dictionary<string, List<ScanRecord>>();
                        _treeManager.SetData(_allMaterials, _allScans);
                        _treeManager.BuildTree(_currentRsid, _allScans, _imageCache);
                        _archidToFl = _treeManager.ArchidToFl;
                        _transferHandler?.Init(_allScans, _archidToFl);
                    }
                }

                UpdateStats();
                tvMaterials.ExpandAll();
            }
            catch (UnauthorizedAccessException) { lblStatus.Text = "没有权限访问该人员档案"; }
            catch (Exception ex) { lblStatus.Text = $"加载失败: {ex.Message}"; }
        }

        // ==================== 预设 + 修复模式 ====================

        public void SelectPreset(int index)
        {
            if (cmbPreset.Items.Count == 0) return;
            if (index == 0) index = cmbPreset.Items.Count - 1;
            else if (index > cmbPreset.Items.Count - 1) index = cmbPreset.Items.Count - 1;
            cmbPreset.SelectedIndex = index;
        }

        public void RefreshPresetList()
        {
            cmbPreset.Items.Clear();
            cmbPreset.Items.Add("自动检测");
            if (_presetManager != null)
            {
                int i = 1;
                foreach (var p in _presetManager.Presets)
                {
                    string key = i <= 9 ? $"Alt+{i} " : "";
                    cmbPreset.Items.Add(new ComboBoxItem { Text = $"{key}{p}", Preset = p });
                    i++;
                }
            }
            cmbPreset.SelectedIndex = 0;
        }

        public void RefreshRepairModeList()
        {
            cmbRepairMode.Items.Clear();
            cmbRepairMode.Items.Add("不自动修复");
            cmbRepairMode.Items.Add("适当修复");
            cmbRepairMode.Items.Add("深度修复");
            cmbRepairMode.Items.Add("自动判断修复");
            if (_repairManager != null)
                foreach (var p in _repairManager.CustomProfiles)
                    cmbRepairMode.Items.Add(p.Name);
            cmbRepairMode.SelectedIndex = 0;
        }

        private RepairProfile GetCurrentRepairProfile()
        {
            int idx = cmbRepairMode.SelectedIndex;
            if (idx == 0) return RepairLevels.None;
            if (idx == 1) return RepairLevels.Standard;
            if (idx == 2) return RepairLevels.Deep;
            if (idx == 3) return null;                       
            return _repairManager?.GetCustom(idx - 4);  
        }

        private void ApplyRepairProfile(RepairProfile profile)
        {
            if (profile.AutoDeskew) _processor.AutoDeskew();
            if (profile.Denoise) _processor.Denoise();
            if (profile.RemoveBlackBorder) _processor.RemoveBlackBorder();
            if (profile.Grayscale) _processor.Grayscale();
            if (profile.Brightness) _processor.Brightness(profile.BrightnessValue);
            if (profile.Contrast) _processor.Contrast(profile.ContrastValue);
        }

        private void cmbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbPreset.SelectedItem is ComboBoxItem item && item.Preset != null && !string.IsNullOrEmpty(item.Preset.ScannerName))
            {
                for (int i = 0; i < cmbScanner.Items.Count; i++)
                    if (cmbScanner.Items[i].ToString() == item.Preset.ScannerName)
                    { cmbScanner.SelectedIndex = i; break; }
                if (!string.IsNullOrEmpty(item.Preset.ColorMode))
                    for (int i = 0; i < cmbColorMode.Items.Count; i++)
                        if (cmbColorMode.Items[i].ToString() == item.Preset.ColorMode)
                        { cmbColorMode.SelectedIndex = i; break; }
            }
        }

        private string GetColorMode() => cmbColorMode?.SelectedItem?.ToString() ?? "彩色";

        private CropPreset GetCurrentPreset()
        {
            if (cmbPreset.SelectedItem is ComboBoxItem item && item.Preset != null)
                return item.Preset;
            return new CropPreset { ColorMode = GetColorMode() };
        }

        public class ComboBoxItem
        {
            public string Text { get; set; }
            public CropPreset Preset { get; set; }
            public override string ToString() => Text;
        }
        // ==================== 扫描仪 ====================

        private void LoadScanners(bool keepSelection = false)
        {
            var current = keepSelection ? cmbScanner.SelectedItem?.ToString() : null;
            var scanners = _scanService.GetScanners(); cmbScanner.Items.Clear();
            foreach (var s in scanners) cmbScanner.Items.Add(s);

            if (!string.IsNullOrEmpty(_savedScannerName))
                for (int i = 0; i < cmbScanner.Items.Count; i++)
                    if (cmbScanner.Items[i].ToString() == _savedScannerName)
                    { cmbScanner.SelectedIndex = i; _scanService.ConnectScanner(_savedScannerName); return; }

            if (current != null) { for (int i = 0; i < cmbScanner.Items.Count; i++) if (cmbScanner.Items[i].ToString() == current) { cmbScanner.SelectedIndex = i; return; } }
            if (cmbScanner.Items.Count > 0) { cmbScanner.SelectedIndex = 0; _scanService.ConnectScanner(cmbScanner.SelectedItem.ToString()); }
        }

        private void cmbScanner_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbScanner.SelectedItem != null)
            {
                var name = cmbScanner.SelectedItem.ToString();
                _scanService.ConnectScanner(name);
                _savedScannerName = name;
                var mainForm = this.ParentForm as MainForm;
                mainForm?.SetScannerName(name);
            }
        }

        // ==================== 扫描 ====================

        private void ScanMaxOnly()
        {
            var preset = GetCurrentPreset();
            var b = _scanService.ScanMaxArea(preset);
            if (b != null) SaveScanResult(b);
        }

        private void ScanAutoDetect()
        {
            cmbPreset.SelectedIndex = 0;
            var preset = GetCurrentPreset();
            var b = _scanService.Scan(preset);
            if (b != null) SaveScanResult(b);
        }

        private async Task ScanOnePage()
        {
            if (_currentArchid == 0) { MessageBox.Show("请先选择材料目录"); return; }
            if (_maxPages > 0 && _scanCounter >= _maxPages)
            {
                var nextNode = FindNextMaterialNode(tvMaterials.SelectedNode);
                if (nextNode != null) { tvMaterials.SelectedNode = nextNode; tvMaterials_AfterSelect(null, new TreeViewEventArgs(nextNode)); await Task.Delay(300); }
                else { MessageBox.Show("已是最后一份材料，全部扫描完成！"); return; }
            }
            try
            {
                lblStatus.Text = "正在扫描...";
                var preset = GetCurrentPreset();
                var bmp = _scanService.Scan(preset);
                if (bmp == null)
                {
                    MessageBox.Show("扫描失败，请检查扫描仪是否连接并开启。", "扫描错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                SaveScanResult(bmp);
            }
            catch (Exception ex) { lblStatus.Text = $"扫描失败: {ex.Message}"; }
        }


        private void SaveScanResult(Bitmap bmp)
        {
            if (_currentArchid == 0) { MessageBox.Show("请先选择材料目录"); bmp.Dispose(); return; }

            Bitmap finalBmp = bmp;
            Bitmap repaired = null;

            try
            {
                var profile = GetCurrentRepairProfile();
                if (profile == null)  // 自动判断修复
                {
                    var autoProfile = AutoRepairService.Analyze(bmp);
                    if (autoProfile != null && autoProfile.Name != "不自动修复")
                    {
                        repaired = AutoRepairService.Execute(bmp, autoProfile);
                        if (repaired != null)
                        {
                            bmp.Dispose();
                            finalBmp = repaired;
                        }
                    }
                }
                else if (profile.Name != "不自动修复")
                {
                    _processor.LoadImage(bmp);
                    ApplyRepairProfile(profile);
                    var procResult = _processor.CurrentBitmap;
                    if (procResult != null)
                    {
                        bmp.Dispose();
                        finalBmp = new Bitmap(procResult);
                    }
                }

                string outDir = Path.Combine(_scanDir, _currentRsid.PadLeft(8, '0'), _currentFl.ToString(), _currentArchid.ToString());
                Directory.CreateDirectory(outDir);
                _scanCounter++;
                string filename = $"{_scanCounter:D3}.JPG";
                string path = Path.Combine(outDir, filename);

                if (File.Exists(path))
                {
                    lblStatus.Text = $"文件已存在: {filename}，跳过";
                    return;
                }

                using (var saveBmp = new Bitmap(finalBmp))
                {
                    saveBmp.Save(path, ImageFormat.Jpeg);
                }

                _currentEditingFile = path;
                LoadExistingFiles();
                _listManager.SelectPage(_scanCounter - 1);

                // ✅ 修复：去掉多余的 new Bitmap()
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var loaded = new Bitmap(fs))
                {
                    _processor.LoadImage(loaded);
                    _editor.SetImage(loaded);
                    _viewport.SetOriginalImage(loaded);
                    _viewport.FitToScreen();
                }

                UpdateCurrentNodeStatus();
                lblLocalPath.Text = outDir;
                lblStatus.Text = $"扫描完成: {filename}（共{_scanCounter}/{_maxPages}页）";
            }
            finally
            {
                if (finalBmp != null && finalBmp != bmp && finalBmp != repaired)
                {
                    finalBmp.Dispose();
                }
            }
        }
        // ==================== 文件列表 ====================

        private void LoadExistingFiles()
        {
            string localDir = Path.Combine(_scanDir, _currentRsid.PadLeft(8, '0'), _currentFl.ToString(), _currentArchid.ToString());
            lblLocalPath.Text = localDir;
            _currentFileList = _fileStatusManager.BuildFileList(localDir, _maxPages, _allScans, _currentArchid.ToString(), _imageCache);
            _listManager.SetFiles(_currentFileList);
            _scanCounter = _currentFileList.Count(f => f.LocalPath != null);
        }

        // ==================== 树操作 ====================

        private void tvMaterials_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is NodeTag tag && tag.Archid != null)
            {
                _currentArchid = int.Parse(tag.Archid); _currentFl = int.Parse(tag.Fl);
                var match = System.Text.RegularExpressions.Regex.Match(e.Node.Text, @"\((\d+)");
                _maxPages = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                LoadExistingFiles();
                if (_currentFileList.Count > 0) _listManager.SelectPage(0);
                else { _editor.SetImage(null); _viewport.SetOriginalImage(null); _processor.Clear(); _currentEditingFile = null; }
                UpdateCurrentNodeStatus();
                lblStatus.Text = $"当前材料: {e.Node.Text}, 已扫{_scanCounter}/{_maxPages}页";
            }
        }

        private void tvMaterials_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (e.Node?.Tag is NodeTag tag)
            {
                tvMaterials.SelectedNode = e.Node;
                if (tag.Archid != null) { _currentArchid = int.Parse(tag.Archid); _currentFl = int.Parse(tag.Fl); _contextMenu.MenuItem.Show(tvMaterials, e.Location); }
                else { _currentFl = int.Parse(tag.Fl); _contextMenu.MenuCategory.Show(tvMaterials, e.Location); }
            }
            else { _contextMenu.MenuCategory.Show(tvMaterials, e.Location); }
        }

        public TreeNode FindNextMaterialNode(TreeNode current)
        {
            if (current == null) return null;
            if (current.Parent != null) { int idx = current.Parent.Nodes.IndexOf(current); if (idx < current.Parent.Nodes.Count - 1 && current.Parent.Nodes[idx + 1].Tag is NodeTag t && t.Archid != null) return current.Parent.Nodes[idx + 1]; }
            if (current.Parent?.Parent != null) { int pi = current.Parent.Parent.Nodes.IndexOf(current.Parent); if (pi < current.Parent.Parent.Nodes.Count - 1) foreach (TreeNode c in current.Parent.Parent.Nodes[pi + 1].Nodes) if (c.Tag is NodeTag ct && ct.Archid != null) return c; }
            if (current.Parent?.Parent?.Parent != null) { int gi = current.Parent.Parent.Parent.Nodes.IndexOf(current.Parent.Parent); if (gi < current.Parent.Parent.Parent.Nodes.Count - 1) return FindFirstMaterialNode(current.Parent.Parent.Parent.Nodes[gi + 1]); }
            return null;
        }

        private TreeNode FindFirstMaterialNode(TreeNode parent) { foreach (TreeNode c in parent.Nodes) { if (c.Tag is NodeTag t && t.Archid != null) return c; var f = FindFirstMaterialNode(c); if (f != null) return f; } return null; }
        private TreeNode FindMaterialNode(string archid) { foreach (TreeNode n in tvMaterials.Nodes) { var f = FindInNode(n, archid); if (f != null) return f; } return null; }
        private TreeNode FindInNode(TreeNode p, string archid) { if (p.Tag is NodeTag t && t.Archid == archid) return p; foreach (TreeNode c in p.Nodes) { var f = FindInNode(c, archid); if (f != null) return f; } return null; }

        // ==================== 页面选择 ====================

        private int _lastSelectedIndex = -1;

        private void OnPageSelected(int index)
        {
            if (_isProcessing) return;
            if (_lastSelectedIndex >= 0 && _lastSelectedIndex != index && _lastSelectedIndex < _listManager.Count)
            {
                var lastInfo = _listManager.GetFile(_lastSelectedIndex);
                if (lastInfo != null) _listManager.RefreshFileStatus(_lastSelectedIndex, _imageCache, lastInfo.LocalPath);
            }

            var info = _listManager.GetFile(index);
            if (info == null) { _editor.SetImage(null); _viewport.SetOriginalImage(null); _processor.Clear(); _currentEditingFile = null; _lastSelectedIndex = index; return; }
            if (info.Status == FileStatus.NotScanned) { _editor.SetImage(null); _viewport.SetOriginalImage(null); _processor.Clear(); _currentEditingFile = null; _lastSelectedIndex = index; return; }

            SaveCurrentToCache();
            _currentEditingFile = info.LocalPath;

            var bmp = _imageCache.GetImage(info.LocalPath);
            if (bmp != null)
            {
                _processor.LoadImage(bmp);
                _editor.SetImage(bmp);
                _viewport.SetOriginalImage(bmp);
                _viewport.FitToScreen();
                bmp.Dispose();
            }
            else if (File.Exists(info.LocalPath))
            {
                // ✅ 修复：去掉多余的 new Bitmap()
                using (var fs = new FileStream(info.LocalPath, FileMode.Open, FileAccess.Read))
                {
                    var diskBmp = new Bitmap(fs);
                    _processor.LoadImage(diskBmp);
                    _editor.SetImage(diskBmp);
                    _viewport.SetOriginalImage(diskBmp);
                    _viewport.FitToScreen();
                    diskBmp.Dispose();
                }
            }

            _lastSelectedIndex = index;
        }

        private void SaveCurrentToCache() { if (string.IsNullOrEmpty(_currentEditingFile) || _processor.CurrentBitmap == null || !_imageCache.IsDirty(_currentEditingFile)) return; _imageCache.Store(_currentEditingFile, _processor.CurrentBitmap); }

        private void ExitEraseIfActive()
        {
            if (!_eraseController.IsActive) return;
            var snapshot = _eraseController.Stop();
            _viewport.EnableDrag = true;
            if (_eraseController.HasDrawn && snapshot != null && picPreview.Image != null)
            {
                var result = new Bitmap(picPreview.Image);
                _processor.ReplaceImage(result);
                _imageCache.MarkDirty(_currentEditingFile);
                _viewport.SetOriginalImage(result);
                _viewport.FitToScreen();
                result.Dispose();
            }
            snapshot?.Dispose();
        }

        // ==================== 工具动作路由 ====================

        public void OnToolAction(string action)
        {
            if (_currentArchid == 0 && action == "scan") { _ = ScanOnePage(); return; }
            if (action != "erase" && action != "eraser_size_up" && action != "eraser_size_down") ExitEraseIfActive();
            var page = _listManager.GetFile(_listManager.SelectedIndex);
            if (page == null && action != "scan" && action != "batch_scan" && action != "save_all" && action != "export_pdf" && action != "prev_page" && action != "next_page" && action != "zoom_in" && action != "zoom_out" && action != "fit_screen" && action != "reset_zoom" && action != "pan_down" && action != "pan_up" && action != "pan_left" && action != "pan_right" && action != "batch_category" && action != "batch_current_item" && action != "batch_from_current" && !action.StartsWith("jump_")) return;

            switch (action)
            {
                // 批量处理
                case "batch_volume": _ = _batchHandler.StartBatchVolume(GetCurrentRepairProfile); break;
                case "batch_current_item": _ = _batchHandler.StartBatchCurrentItem(GetCurrentRepairProfile); break;
                case "batch_from_current": _ = _batchHandler.StartBatchFromCurrent(GetCurrentRepairProfile); break;
                case "batch_category": _ = _batchHandler.StartBatchCategory(_currentFl, GetCurrentRepairProfile); break;
                case "edit_page_count":
                    _batchHandler.ShowEditPageCountDialog(_allMaterials, _currentRsid, _currentArchid.ToString(), (archid, np) =>
                    {
                        if (_currentArchid.ToString() == archid) _maxPages = np;
                        LoadMaterialTree(); Task.Delay(200).ContinueWith(_ => this.BeginInvoke(new Action(() =>
                        {
                            var tn = FindMaterialNode(archid);
                            if (tn != null) { tvMaterials.SelectedNode = tn; tvMaterials_AfterSelect(null, new TreeViewEventArgs(tn)); }
                        })));
                    }); break;
                case "replace_scan": ReplaceScan(); break;
                case "clean_orphan_files": _ = _batchHandler.CleanOrphanFiles(_allScans, _maxPages, _currentRsid, _currentArchid.ToString(), RefreshLocalTree); break;

                // 上传下载
                case "upload_item": if (_currentArchid == 0) { MessageBox.Show("请先选择材料目录"); break; } _ = _transferHandler.UploadItem(_currentRsid, _currentFl, _currentArchid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "force_upload_item": if (_currentArchid == 0) { MessageBox.Show("请先选择材料目录"); break; } if (ConfirmForce()) _ = _transferHandler.ForceUploadItem(_currentRsid, _currentFl, _currentArchid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "download_item": if (_currentArchid == 0) { MessageBox.Show("请先选择材料目录"); break; } _ = _transferHandler.DownloadItem(_currentRsid, _currentFl, _currentArchid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "force_download_item": if (_currentArchid == 0) { MessageBox.Show("请先选择材料目录"); break; } if (ConfirmForce()) _ = _transferHandler.ForceDownloadItem(_currentRsid, _currentFl, _currentArchid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "upload_category": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } _ = _transferHandler.UploadCategory(_currentRsid, _currentFl).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "force_upload_category": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } if (ConfirmForce()) _ = _transferHandler.ForceUploadCategory(_currentRsid, _currentFl).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "download_category": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } _ = _transferHandler.DownloadCategory(_currentRsid, _currentFl).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "force_download_category": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } if (ConfirmForce()) _ = _transferHandler.ForceDownloadCategory(_currentRsid, _currentFl).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "upload_volume": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } _ = _transferHandler.UploadWholeVolume(_currentRsid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "force_upload_volume": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } if (ConfirmForce()) _ = _transferHandler.ForceUploadWholeVolume(_currentRsid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "download_volume": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } _ = _transferHandler.DownloadWholeVolume(_currentRsid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;
                case "force_download_volume": if (string.IsNullOrEmpty(_currentRsid)) { MessageBox.Show("请先选择人员"); break; } if (ConfirmForce()) _ = _transferHandler.ForceDownloadWholeVolume(_currentRsid).ContinueWith(_ => this.BeginInvoke(new Action(() => AfterTransfer()))); break;

                // 扫描
                case "scan": _ = ScanOnePage(); break;

                // 视口
                case "zoom_in": _viewport.ZoomIn(); break;
                case "zoom_out": _viewport.ZoomOut(); break;
                case "fit_screen": _viewport.FitToScreen(); break;
                case "reset_zoom": _viewport.ResetZoom(); break;
                case "pan_up": _viewport.PanUp(); break;
                case "pan_down": _viewport.PanDown(); break;
                case "pan_left": _viewport.PanLeft(); break;
                case "pan_right": _viewport.PanRight(); break;

                // 图像处理
                case "rotate_left": Process(_processor.RotateLeft); break;
                case "rotate_right": Process(_processor.RotateRight); break;
                case "flip_h": Process(_processor.FlipHorizontal); break;
                case "flip_v": Process(_processor.FlipVertical); break;
                case "brightness_up": Process(() => _processor.Brightness(20)); break;
                case "brightness_down": Process(() => _processor.Brightness(-20)); break;
                case "contrast_up": Process(() => _processor.Contrast(1.2)); break;
                case "contrast_down": Process(() => _processor.Contrast(0.8)); break;
                case "auto_deskew": Process(_processor.AutoDeskew); break;
                case "manual_deskew":
                    _viewport.EnableDrag = false;
                    _editor.StartDeskew(angle => Process(() => _processor.ManualDeskew(angle)));
                    break;
                case "denoise": Process(_processor.Denoise); break;
                case "grayscale": Process(_processor.Grayscale); break;
                case "crop":
                    _viewport.EnableDrag = false;
                    _editor.StartCrop();
                    _editor.CropCompleted -= OnCropCompleted;
                    _editor.CropCompleted += OnCropCompleted;
                    break;
                case "remove_border": Process(_processor.RemoveBlackBorder); break;
                case "erase":
                    _viewport.EnableDrag = false;
                    _eraseController.Start();
                    lblStatus.Text = "擦除模式: 拖动擦除 | +/-调整大小 | 点击其他按钮退出";
                    break;
                case "eraser_size_up": _eraseController.SizeUp(); break;
                case "eraser_size_down": _eraseController.SizeDown(); break;
                case "undo": if (_processor.HasUndo) { _processor.Undo(); RefreshPreview(); } else lblStatus.Text = "没有可撤销的操作"; break;
                case "redo": if (_processor.HasRedo) { _processor.Redo(); RefreshPreview(); } else lblStatus.Text = "没有可重做的操作"; break;
                case "restore":
                    if (!string.IsNullOrEmpty(_currentEditingFile) && File.Exists(_currentEditingFile))
                    {
                        
                        using (var fs = new FileStream(_currentEditingFile, FileMode.Open, FileAccess.Read))
                        {
                            var db = new Bitmap(fs);
                            _processor.LoadImage(db);
                            _editor.SetImage(db);
                            _viewport.SetOriginalImage(db);
                            _viewport.FitToScreen();
                        }
                        _imageCache.Release(_currentEditingFile);
                        LoadExistingFiles();
                        var restIdx = _currentFileList.FindIndex(f => f.LocalPath == _currentEditingFile);
                        if (restIdx >= 0) _listManager.SelectPage(restIdx);
                        lblStatus.Text = "已恢复原始图像";
                    }
                    else
                    {
                        _processor.RestoreOriginal();
                        RefreshPreview();
                    }
                    break;

                // 保存
                case "save": SaveCurrentToCache(); _imageCache.Save(_currentEditingFile); _imageCache.UpdateMd5(_currentEditingFile); LoadExistingFiles(); var savedIdx = _currentFileList.FindIndex(f => f.Filename == page?.Filename); if (savedIdx >= 0) _listManager.SelectPage(savedIdx); UpdateCurrentNodeStatus(); lblStatus.Text = $"已保存: {page?.Filename}"; break;
                case "save_all": SaveCurrentToCache(); _imageCache.SaveAll(); _processor.Clear(); _editor.SetImage(null); _viewport.SetOriginalImage(null); _currentEditingFile = null; LoadExistingFiles(); UpdateCurrentNodeStatus(); lblStatus.Text = "全部已保存"; break;
                case "save_selected": SaveSelectedFiles(); break;
                case "restore_selected": RestoreSelectedFiles(); break;
                case "upload_selected": UploadSelectedFiles(); break;
                case "download_selected": DownloadSelectedFiles(); break;
                case "delete_selected": DeleteSelectedFiles(); break;
                case "open_viewer": OpenFileWithViewer(); break;

                // 导出
                case "export_pdf":
                    if (_currentArchid == 0) { lblStatus.Text = "请先选择材料目录"; break; }
                    string ed = Path.Combine(_scanDir, _currentRsid.PadLeft(8, '0'), _currentFl.ToString(), _currentArchid.ToString());
                    var nt = tvMaterials.SelectedNode?.Text ?? $"FL{_currentFl}_{_currentArchid}";
                    var cn = System.Text.RegularExpressions.Regex.Replace(nt, @"\s*[✓◐!X⬆☁].*", "");
                    cn = System.Text.RegularExpressions.Regex.Replace(cn, @"[\\/:*?""<>|]", "");
                    _pdfExporter.ExportOrPrint(ed, cn, (pp) => this.BeginInvoke(new Action(() => lblStatus.Text = $"PDF已保存: {Path.GetFileName(pp)}")));
                    break;

                case "prev_page": _listManager.PrevPage(); break;
                case "next_page":
                    if (_listManager.Count == 0) break;
                    int ni = _listManager.SelectedIndex + 1;
                    if (ni < _listManager.Count) { _listManager.SelectPage(ni); OnPageSelected(ni); }
                    else { var nn = FindNextMaterialNode(tvMaterials.SelectedNode); if (nn != null) { tvMaterials.SelectedNode = nn; tvMaterials_AfterSelect(null, new TreeViewEventArgs(nn)); } else MessageBox.Show("已是最后一份材料的最后一页！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    break;
                case "escape":
                    _eraseController?.Stop();
                    _editor?.StopCrop();
                    _editor?.StopDeskew();
                    ExitEraseIfActive();
                    _viewport.EnableDrag = true;
                    break;
                case "jump_1": JumpToPage(1); break;
                case "jump_2": JumpToPage(2); break;
                case "jump_3": JumpToPage(3); break;
                case "jump_4": JumpToPage(4); break;
                case "jump_5": JumpToPage(5); break;
                case "jump_6": JumpToPage(6); break;
                case "jump_7": JumpToPage(7); break;
                case "jump_8": JumpToPage(8); break;
                case "jump_9": JumpToPage(9); break;
            }
        }

        private void Process(Action action)
        {
            if (string.IsNullOrEmpty(_currentEditingFile)) return;
            _isProcessing = true;
            action();
            var b = _processor.CurrentBitmap;
            _editor.SetImage(b); _viewport.SetOriginalImage(b); _viewport.FitToScreen();
            _isProcessing = false;
            if (b != null) _imageCache.MarkDirty(_currentEditingFile);
        }

        private void RefreshPreview() { _isProcessing = true; _editor.SetImage(_processor.CurrentBitmap); _viewport.SetOriginalImage(_processor.CurrentBitmap); _viewport.FitToScreen(); _isProcessing = false; }
        private void JumpToPage(int pn) { if (_listManager.Count == 0) return; int idx = pn - 1; if (idx >= _listManager.Count) idx = _listManager.Count - 1; _listManager.SelectPage(idx); OnPageSelected(idx); }

        // ==================== 节点状态 ====================

        private void UpdateCurrentNodeStatus()
        {
            if (tvMaterials.SelectedNode == null) return;
            var t = tvMaterials.SelectedNode.Text;

            bool hasLocalUpdated = _currentFileList?.Any(f => f.Status == FileStatus.LocalUpdated) ?? false;
            bool hasModified = _currentFileList?.Any(f => f.Status == FileStatus.Modified) ?? false;
            bool hasLocalOnly = _currentFileList?.Any(f => f.Status == FileStatus.LocalOnly) ?? false;
            bool hasSynced = _currentFileList?.Any(f => f.Status == FileStatus.Synced) ?? false;
            bool hasServerOnly = _currentFileList?.Any(f => f.Status == FileStatus.ServerOnly) ?? false;
            bool hasAnyLocal = _currentFileList?.Any(f => f.LocalPath != null) ?? false;

            string icon; Color color;
            if (!hasAnyLocal && !hasServerOnly) { color = Color.Tomato; icon = "!"; }
            else if (hasModified) { color = Color.Orange; icon = "✎"; }
            else if (hasLocalOnly && !hasSynced && !hasServerOnly) { color = Color.DodgerBlue; icon = "◐"; }
            else if (hasLocalUpdated) { color = Color.Brown; icon = "⬆"; }
            else if (hasServerOnly && !hasAnyLocal) { color = Color.BlueViolet; icon = "☁"; }
            else if (hasSynced && _currentFileList.Count == _maxPages) { color = Color.ForestGreen; icon = "✓"; }
            else { color = Color.SlateGray; icon = "?"; }

            tvMaterials.SelectedNode.ForeColor = color;
            tvMaterials.SelectedNode.Text = System.Text.RegularExpressions.Regex.Replace(t, @"\((\d+)页\).*", $"($1页) {icon}");
            tvMaterials.Invalidate();
        }

        // ==================== 裁剪 ====================

        private void OnCropCompleted(Rectangle rect)
        {
            _editor.CropCompleted -= OnCropCompleted;
            _viewport.EnableDrag = true;
            Process(() => ApplyCrop(rect));
        }
        private void ApplyCrop(Rectangle rect)
        {
            if (rect.Width < 10 || rect.Height < 10) { _editor.StopCrop(); return; }
            var b = _processor.CurrentBitmap; if (b == null) { _editor.StopCrop(); return; }
            var m = b.ToMat(); int x = Math.Max(0, rect.X), y = Math.Max(0, rect.Y), w = Math.Min(rect.Width, m.Width - x), h = Math.Min(rect.Height, m.Height - y);
            if (w <= 0 || h <= 0) { m.Dispose(); _editor.StopCrop(); return; }
            var cr = new OpenCvSharp.Rect(x, y, w, h); var cropped = new Mat(m, cr); m.Dispose(); _processor.ReplaceImage(cropped.ToBitmap()); cropped.Dispose(); _editor.StopCrop();
        }

        // ==================== 文件右键菜单 ====================

        private void menuFileContext_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var indices = _listManager.SelectedIndices;
            if (indices.Count == 0) { e.Cancel = true; return; }

            bool hasDirty = false, hasLocal = false, hasServer = false;
            foreach (var idx in indices)
            {
                var f = _listManager.GetFile(idx);
                if (f == null) continue;
                if (f.IsDirty) hasDirty = true;
                if (f.LocalPath != null) hasLocal = true;
                if (f.Status == FileStatus.ServerOnly || f.Status == FileStatus.Synced || f.Status == FileStatus.LocalUpdated) hasServer = true;
            }

            menuFileSave.Visible = hasDirty;
            menuFileRestore.Visible = hasLocal;
            menuFileUpload.Visible = hasLocal;
            menuFileDownload.Visible = hasServer;
            menuFileDelete.Visible = hasLocal;
            menuFileOpen.Visible = hasLocal;
            menuFileReorder.Visible = indices.Count > 0;
        }

        private void SaveSelectedFiles()
        {
            foreach (var idx in _listManager.SelectedIndices) { var f = _listManager.GetFile(idx); if (f != null && f.IsDirty && f.LocalPath != null) _imageCache.Save(f.LocalPath); }
            LoadExistingFiles(); UpdateCurrentNodeStatus(); lblStatus.Text = "选中文件已保存";
        }

        private void RestoreSelectedFiles()
        {
            foreach (var idx in _listManager.SelectedIndices) { var f = _listManager.GetFile(idx); if (f != null && f.LocalPath != null) _imageCache.Release(f.LocalPath); }
            LoadExistingFiles(); lblStatus.Text = "已恢复原始";
        }

        private async void UploadSelectedFiles()
        {
            var files = new List<FileItem>();
            foreach (var idx in _listManager.SelectedIndices) { var f = _listManager.GetFile(idx); if (f != null && f.LocalPath != null) files.Add(new FileItem { Filename = f.Filename, LocalPath = f.LocalPath, IsLocal = true, Length = f.LocalLength }); }
            if (files.Count == 0) return;
            _transferHandler.SetMaxPages(_maxPages);
            await _transferHandler.UploadItem(_currentRsid, _currentFl, _currentArchid);
            AfterTransfer();
        }

        private async void DownloadSelectedFiles()
        {
            foreach (var idx in _listManager.SelectedIndices) { var f = _listManager.GetFile(idx); if (f != null && (f.Status == FileStatus.ServerOnly || f.Status == FileStatus.Synced || f.Status == FileStatus.LocalUpdated)) await DownloadSingleFile(f); }
            LoadExistingFiles();
        }

        private async Task DownloadSingleFile(FileStatusInfo info)
        {
            try
            {
                lblStatus.Text = $"正在下载: {info.Filename}...";
                string rsidPadded = _currentRsid.PadLeft(8, '0');
                string url = $"{_api.BaseUrl}/das_images/YS/{rsidPadded}/{_currentFl}/{_currentArchid}/{info.Filename}";
                var handler = new System.Net.Http.HttpClientHandler { ServerCertificateCustomValidationCallback = (a, b, c, d) => true };
                using (var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) })
                {
                    var encData = await client.GetByteArrayAsync(url);
                    var decData = Program.Crypto.Decrypt(encData);
                    string outDir = Path.Combine(_scanDir, rsidPadded, _currentFl.ToString(), _currentArchid.ToString());
                    Directory.CreateDirectory(outDir);
                    File.WriteAllBytes(Path.Combine(outDir, info.Filename), decData);
                }
                lblStatus.Text = $"下载完成: {info.Filename}";
                LoadExistingFiles();
                var idx = _currentFileList.FindIndex(f => f.Filename == info.Filename);
                if (idx >= 0) { _listManager.SelectPage(idx); OnPageSelected(idx); }
            }
            catch (Exception ex) { lblStatus.Text = $"下载失败: {ex.Message}"; }
        }

        private void DeleteSelectedFiles()
        {
            if (MessageBox.Show("确定删除选中的本地文件？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            foreach (var idx in _listManager.SelectedIndices) { var f = _listManager.GetFile(idx); if (f != null && f.LocalPath != null) _imageCache.Delete(f.LocalPath); }
            LoadExistingFiles();
        }

        private void OpenFileWithViewer()
        {
            var f = _listManager.GetFile(_listManager.SelectedIndex);
            if (f == null || f.LocalPath == null || !File.Exists(f.LocalPath)) return;
            if (string.IsNullOrEmpty(_viewerPath))
            {
                var dr = MessageBox.Show("当前使用系统默认程序打开。\n\n如需指定程序打开，请前往「服务器设置 → 文件查看器」设置。\n\n是否继续用系统默认程序打开？", "打开文件", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dr == DialogResult.Yes) System.Diagnostics.Process.Start(f.LocalPath);
            }
            else System.Diagnostics.Process.Start(_viewerPath, $"\"{f.LocalPath}\"");
        }

        // ==================== 替换扫描 ====================

        public void ReplaceScan()
        {
            _batchHandler.ReplaceScan(_allScans, _scanService, _imageCache, _processor, _editor, _viewport, _listManager,
                _scanDir, _currentRsid, _currentFl, _currentArchid, GetColorMode, GetCurrentPreset,
                v => _currentEditingFile = v, () => _currentEditingFile, LoadExistingFiles, UpdateCurrentNodeStatus,
                v => lblLocalPath.Text = v, GetCurrentRepairProfile);
        }
        

        // ==================== 批量/页数/清理（公开给菜单调用） ====================
        public async Task TestBatchCategory()
        {
            MessageBox.Show("TestBatchCategory 被调用");
            await _batchHandler.StartBatchCategory(_currentFl, GetCurrentRepairProfile);
        }
        public async Task StartBatchCurrentItem() => await _batchHandler.StartBatchCurrentItem(GetCurrentRepairProfile);
        public async Task StartBatchCategory()
        {
            try
            {
                MessageBox.Show("进入 StartBatchCategory 无参方法");
                await _batchHandler.StartBatchCategory(_currentFl, GetCurrentRepairProfile);
                MessageBox.Show("StartBatchCategory 执行完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"StartBatchCategory 异常: {ex.Message}\n{ex.StackTrace}");
            }
        }
        public async Task StartBatchFromCurrent() => await _batchHandler.StartBatchFromCurrent(GetCurrentRepairProfile);
        public void ShowEditPageCountDialog() => _batchHandler.ShowEditPageCountDialog(_allMaterials, _currentRsid, _currentArchid.ToString(), (archid, np) =>
        {
            if (_currentArchid.ToString() == archid) _maxPages = np;
            LoadMaterialTree(); Task.Delay(200).ContinueWith(_ => this.BeginInvoke(new Action(() =>
            {
                var tn = FindMaterialNode(archid);
                if (tn != null) { tvMaterials.SelectedNode = tn; tvMaterials_AfterSelect(null, new TreeViewEventArgs(tn)); }
            })));
        });
        public async Task CleanOrphanFiles() => await _batchHandler.CleanOrphanFiles(_allScans, _maxPages, _currentRsid, _currentArchid.ToString(), RefreshLocalTree);

        // ==================== 上传下载（公开给菜单调用） ====================

        public async Task UploadItem() { _transferHandler.SetMaxPages(_maxPages); await _transferHandler.UploadItem(_currentRsid, _currentFl, _currentArchid); AfterTransfer(); }
        public async Task ForceUploadItem() { _transferHandler.SetMaxPages(_maxPages); await _transferHandler.ForceUploadItem(_currentRsid, _currentFl, _currentArchid); AfterTransfer(); }
        public async Task DownloadItem() { await _transferHandler.DownloadItem(_currentRsid, _currentFl, _currentArchid); AfterTransfer(); }
        public async Task ForceDownloadItem() { await _transferHandler.ForceDownloadItem(_currentRsid, _currentFl, _currentArchid); AfterTransfer(); }
        public async Task UploadCategory() { await _transferHandler.UploadCategory(_currentRsid, _currentFl); AfterTransfer(); }
        public async Task ForceUploadCategory() { await _transferHandler.ForceUploadCategory(_currentRsid, _currentFl); AfterTransfer(); }
        public async Task DownloadCategory() { await _transferHandler.DownloadCategory(_currentRsid, _currentFl); AfterTransfer(); }
        public async Task ForceDownloadCategory() { await _transferHandler.ForceDownloadCategory(_currentRsid, _currentFl); AfterTransfer(); }
        public async Task UploadWholeVolume() { await _transferHandler.UploadWholeVolume(_currentRsid); AfterTransfer(); }
        public async Task ForceUploadWholeVolume() { await _transferHandler.ForceUploadWholeVolume(_currentRsid); AfterTransfer(); }
        public async Task DownloadWholeVolume() { await _transferHandler.DownloadWholeVolume(_currentRsid); AfterTransfer(); }
        public async Task ForceDownloadWholeVolume() { await _transferHandler.ForceDownloadWholeVolume(_currentRsid); AfterTransfer(); }

        // ==================== 刷新 ====================

        private void AfterTransfer()
        {
            _failedFiles = _transferHandler.FailedFiles;
            if (_failedFiles.Count > 0) ShowFailedList();
            RefreshLocalTree();
        }

        public void RefreshAfterBatch()
        {
            LoadExistingFiles();
            UpdateCurrentNodeStatus();
        }

        public void RefreshLocalTree()
        {
            var savedArchid = _currentArchid.ToString();
            var savedFl = _currentFl.ToString();
            var savedIndex = _listManager.SelectedIndex;

            SaveExpandedNodes();
            _treeManager.SetData(_allMaterials, _allScans);
            _treeManager.BuildTree(_currentRsid, _allScans, _imageCache);
            UpdateStats();
            LoadExistingFiles();
            RestoreExpandedNodes();

            if (!string.IsNullOrEmpty(savedArchid))
            {
                var node = FindMaterialNode(savedArchid);
                if (node != null) { tvMaterials.SelectedNode = node; if (savedIndex >= 0 && savedIndex < _listManager.Count) _listManager.SelectPage(savedIndex); }
            }
        }

        private void LoadMaterialTree()
        {
            if (_allMaterials == null) return;
            _treeManager.SetData(_allMaterials, _allScans);
            _treeManager.BuildTree(_currentRsid, _allScans, _imageCache);
        }

        // ==================== 展开状态 ====================

        private void SaveExpandedNodes()
        {
            _expandedNodePaths.Clear();
            foreach (TreeNode node in tvMaterials.Nodes) CollectExpandedNodes(node);
        }
        private void CollectExpandedNodes(TreeNode parent)
        {
            if (parent.IsExpanded) _expandedNodePaths.Add(parent.FullPath);
            foreach (TreeNode child in parent.Nodes) CollectExpandedNodes(child);
        }
        private void RestoreExpandedNodes()
        {
            foreach (TreeNode node in tvMaterials.Nodes) RestoreExpandedNodesRecursive(node);
        }
        private void RestoreExpandedNodesRecursive(TreeNode node)
        {
            if (_expandedNodePaths.Contains(node.FullPath)) node.Expand();
            foreach (TreeNode child in node.Nodes) RestoreExpandedNodesRecursive(child);
        }

        // ==================== 其他 ====================

        private void UpdateStats()
        {
            int total = 0, scanned = 0;
            if (_allMaterials != null)
                foreach (var kv in _allMaterials)
                    foreach (var item in kv.Value)
                    { total++; if (_allScans != null && _allScans.TryGetValue(item.ARCHID.ToString(), out var sl) && sl.Count > 0) scanned++; }
            lblPersonInfo.Text = $"{_currentPersonName} ({_currentRsid})  材料 {total} | 已扫描 {scanned} | 未扫描 {total - scanned}";
        }

        private void ShowFailedList()
        {
            if (_failedFiles.Count == 0) return;
            var form = new Form { Text = $"失败列表（{_failedFiles.Count} 个）", Size = new Size(500, 350), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.SizableToolWindow };
            var txt = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = string.Join(Environment.NewLine, _failedFiles), Font = new Font("微软雅黑", 10F) };
            form.Controls.Add(txt);
            form.ShowDialog();
        }

        private void label1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("⚠ 内存占用说明\n\n程序在处理图像时会将修改后的图片暂存在内存中，\n处理页数越多，内存占用越大，可能导致电脑变卡。\n\n💡 建议：\n1. 每处理完一类材料后，点击【全存】按钮保存到磁盘并释放内存。\n2. 未保存的修改仅存在内存中，切换人员或关闭程序会丢失，请及时存盘。\n3. 如电脑持续卡顿，请先点击【全存】保存当前工作，再重启程序。", "温馨提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}