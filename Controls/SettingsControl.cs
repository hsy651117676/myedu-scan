using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ScanTool.Models;
using ScanTool.Services;

namespace ScanTool.Controls
{
    public partial class SettingsControl : UserControl
    {
        private CropPresetManager _presetManager;
        private RepairProfileManager _repairManager;
        private string _viewerPath;
        private PictureBox _picOriginal;
        private PictureBox _picPreview;
        private Bitmap _scannedImage;
        private Rectangle _cropRect;
        private bool _isCropping;
        private Bitmap _cachedPreview;
        private ScanService _scanService;

        public SettingsControl()
        {
            InitializeComponent();
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                { if (dlg.ShowDialog() == DialogResult.OK) txtScanDir.Text = dlg.SelectedPath; }
            };
            btnBrowseViewer.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "选择文件查看器";
                    dlg.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK) txtViewerPath.Text = dlg.FileName;
                }
            };
            btnSave.Click += (s, e) =>
            {
                var mf = this.ParentForm as MainForm;
                mf?.RefreshScanPresets();
                if (mf != null)
                {
                    string viewer = chkUseDefault.Checked ? "" : txtViewerPath.Text.Trim();
                    mf.ApplySettings(txtServerUrl.Text.Trim(), txtScanDir.Text.Trim(), viewer);
                }
            };
            btnCancel.Click += (s, e) =>
            {
                var mf = this.ParentForm as MainForm;
                this.BeginInvoke(new Action(() => mf?.ShowScan()));
            };
            btnAddPreset.Click += (s, e) => StartAddPreset();
            btnDeletePreset.Click += (s, e) =>
            {
                if (lstPresets.SelectedItem is CropPreset p) { _presetManager.Delete(p.Name); RefreshPresetList(); }
            };
            btnActivatePreset.Click += (s, e) =>
            {
                if (lstPresets.SelectedItem is CropPreset p) { _presetManager.Activate(p.Name); RefreshPresetList(); }
            };
            btnRepairPresets.Click += (s, e) => ShowRepairPresetEditor();
        }

        public void Init(string serverUrl, string scanDir, CropPresetManager presetManager, string viewerPath, ScanService scanService)
        {
            _presetManager = presetManager;
            _viewerPath = viewerPath;
            _scanService = scanService;
            txtServerUrl.Text = serverUrl;
            txtScanDir.Text = scanDir;
            chkUseDefault.Checked = string.IsNullOrEmpty(viewerPath);
            txtViewerPath.Text = viewerPath ?? "";
            txtViewerPath.Enabled = !string.IsNullOrEmpty(viewerPath);
            btnBrowseViewer.Enabled = true;

            var configDir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "ScanTool");
            _repairManager = new RepairProfileManager(configDir);

            RefreshPresetList();
            RefreshScannerList();
        }

        private void chkUseDefault_CheckedChanged(object sender, EventArgs e)
        {
            txtViewerPath.Enabled = !chkUseDefault.Checked;
            btnBrowseViewer.Enabled = !chkUseDefault.Checked;
        }

        private void RefreshPresetList()
        {
            lstPresets.Items.Clear();
            if (_presetManager != null)
                foreach (var p in _presetManager.Presets) lstPresets.Items.Add(p);
            if (_presetManager?.HasActive == true) lstPresets.SelectedItem = _presetManager.ActivePreset;
        }

        private void RefreshScannerList()
        {
            cmbDefaultScanner.Items.Clear();
            foreach (var s in _scanService.GetScanners()) cmbDefaultScanner.Items.Add(s);
            if (cmbDefaultScanner.Items.Count > 0) cmbDefaultScanner.SelectedIndex = 0;
        }

        // ==================== 修复预设编辑 ====================

        private void ShowRepairPresetEditor()
        {
            var form = new Form
            {
                Text = "修复预设编辑",
                Size = new Size(400, 380),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblSelect = new Label { Text = "选择预设:", Location = new Point(15, 15), AutoSize = true };
            var cmbSelect = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(85, 12), Width = 200 };
            for (int i = 0; i < 7; i++)
            {
                var p = _repairManager.GetCustom(i);
                cmbSelect.Items.Add(p?.Name ?? $"自定义{i + 1} (空)");
            }

            var lblName = new Label { Text = "名称:", Location = new Point(15, 50), AutoSize = true };
            var txtName = new TextBox { Location = new Point(60, 47), Width = 200 };

            var chkDeskew = new CheckBox { Text = "自动纠偏 (V)", Location = new Point(15, 85), AutoSize = true };
            var chkDenoise = new CheckBox { Text = "去噪 (J)", Location = new Point(15, 110), AutoSize = true };
            var chkRemoveBorder = new CheckBox { Text = "去黑边 (D)", Location = new Point(15, 135), AutoSize = true };
            var chkGrayscale = new CheckBox { Text = "灰度 (Ctrl+E)", Location = new Point(15, 160), AutoSize = true };

            var chkBrightness = new CheckBox { Text = "亮度 (R)", Location = new Point(15, 190), AutoSize = true };
            var numBrightness = new NumericUpDown { Location = new Point(160, 188), Width = 60, Minimum = -50, Maximum = 50, Value = 10 };

            var chkContrast = new CheckBox { Text = "对比度 (F)", Location = new Point(15, 220), AutoSize = true };
            var numContrast = new NumericUpDown { Location = new Point(160, 218), Width = 60, Minimum = 0.1M, Maximum = 3.0M, Increment = 0.1M, DecimalPlaces = 1, Value = 1.1M };

            cmbSelect.SelectedIndexChanged += (s, e) =>
            {
                int idx = cmbSelect.SelectedIndex;
                var p = _repairManager.GetCustom(idx);
                txtName.Text = p?.Name ?? $"自定义{idx + 1}";
                chkDeskew.Checked = p?.AutoDeskew ?? false;
                chkDenoise.Checked = p?.Denoise ?? false;
                chkRemoveBorder.Checked = p?.RemoveBlackBorder ?? false;
                chkGrayscale.Checked = p?.Grayscale ?? false;
                chkBrightness.Checked = p?.Brightness ?? false;
                numBrightness.Value = p?.BrightnessValue ?? 10;
                chkContrast.Checked = p?.Contrast ?? false;
                numContrast.Value = (decimal)(p?.ContrastValue ?? 1.1);
            };
            cmbSelect.SelectedIndex = 0;

            var btnSaveP = new Button { Text = "保存", Location = new Point(100, 270), Width = 80, BackColor = Color.FromArgb(64, 158, 255), ForeColor = Color.White };
            var btnCancel2 = new Button { Text = "取消", Location = new Point(200, 270), Width = 80 };

            btnSaveP.Click += (s, e) =>
            {
                int idx = cmbSelect.SelectedIndex;
                var profile = new RepairProfile
                {
                    Name = string.IsNullOrWhiteSpace(txtName.Text) ? $"自定义{idx + 1}" : txtName.Text,
                    AutoDeskew = chkDeskew.Checked,
                    Denoise = chkDenoise.Checked,
                    RemoveBlackBorder = chkRemoveBorder.Checked,
                    Grayscale = chkGrayscale.Checked,
                    Brightness = chkBrightness.Checked,
                    BrightnessValue = (int)numBrightness.Value,
                    Contrast = chkContrast.Checked,
                    ContrastValue = (double)numContrast.Value
                };
                _repairManager.AddOrUpdate(idx, profile);

                var mf = this.ParentForm as MainForm;
                mf?.RefreshRepairModes();

                form.Close();
            };

            btnCancel2.Click += (s, e) => form.Close();

            form.Controls.AddRange(new Control[] { lblSelect, cmbSelect, lblName, txtName,
                chkDeskew, chkDenoise, chkRemoveBorder, chkGrayscale,
                chkBrightness, numBrightness, chkContrast, numContrast,
                btnSaveP, btnCancel2 });

            form.ShowDialog();
        }

        // ==================== 裁剪预设 ====================

        private Rectangle ScreenToImageRect(Rectangle screenRect)
        {
            if (_scannedImage == null) return screenRect;
            int imgW = _scannedImage.Width, imgH = _scannedImage.Height;
            int boxW = _picOriginal.ClientSize.Width, boxH = _picOriginal.ClientSize.Height;
            float scale = Math.Min((float)boxW / imgW, (float)boxH / imgH);
            int drawW = (int)(imgW * scale), drawH = (int)(imgH * scale);
            int offsetX = (boxW - drawW) / 2, offsetY = (boxH - drawH) / 2;
            float rx = (float)imgW / drawW, ry = (float)imgH / drawH;
            int x = (int)((screenRect.X - offsetX) * rx);
            int y = (int)((screenRect.Y - offsetY) * ry);
            int w = (int)(screenRect.Width * rx);
            int h = (int)(screenRect.Height * ry);
            x = Math.Max(0, Math.Min(x, imgW - 1));
            y = Math.Max(0, Math.Min(y, imgH - 1));
            w = Math.Min(w, imgW - x);
            h = Math.Min(h, imgH - y);
            return new Rectangle(x, y, w, h);
        }

        private void StartAddPreset()
        {
            _scannedImage = null;
            _cachedPreview = null;

            using (var form = new Form
            {
                Text = "新增裁剪预设",
                StartPosition = FormStartPosition.CenterParent,
                WindowState = FormWindowState.Maximized,
                FormBorderStyle = FormBorderStyle.Sizable
            })
            {
                var pnlParams = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };

                var lblName = new Label { Text = "名称:", Location = new Point(10, 15), AutoSize = true };
                var txtName = new TextBox { Location = new Point(50, 12), Width = 100 };

                var lblDpi = new Label { Text = "DPI:", Location = new Point(160, 15), AutoSize = true };
                var cmbDpi = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(195, 12), Width = 55 };
                cmbDpi.Items.AddRange(new object[] { "150", "200", "300", "400", "600" });
                cmbDpi.SelectedIndex = 2;

                var lblPaper = new Label { Text = "纸张:", Location = new Point(260, 15), AutoSize = true };
                var cmbPaper = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(300, 12), Width = 55 };
                cmbPaper.Items.AddRange(new object[] { "A3", "A4", "A5", "B5" });
                cmbPaper.SelectedIndex = 1;

                var lblOri = new Label { Text = "方向:", Location = new Point(365, 15), AutoSize = true };
                var cmbOri = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(405, 12), Width = 55 };
                cmbOri.Items.AddRange(new object[] { "纵向", "横向" });
                cmbOri.SelectedIndex = 0;

                var lblRot = new Label { Text = "旋转:", Location = new Point(470, 15), AutoSize = true };
                var cmbRot = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(510, 12), Width = 55 };
                cmbRot.Items.AddRange(new object[] { "0°", "90°", "180°", "270°" });
                cmbRot.SelectedIndex = 0;

                var lblColor = new Label { Text = "色彩:", Location = new Point(575, 15), AutoSize = true };
                var cmbColor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(615, 12), Width = 60 };
                cmbColor.Items.AddRange(new object[] { "彩色", "灰度", "黑白" });
                cmbColor.SelectedIndex = 0;

                var lblScanner = new Label { Text = "扫描仪:", Location = new Point(685, 15), AutoSize = true };
                var cmbScanner = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(745, 12), Width = 280 };
                foreach (var s in _scanService.GetScanners()) cmbScanner.Items.Add(s);
                if (cmbScanner.Items.Count > 0) cmbScanner.SelectedIndex = 0;

                var btnScan = new Button { Text = "扫描", Location = new Point(1035, 10), Width = 60 };
                var btnPreview = new Button { Text = "预览", Location = new Point(1100, 10), Width = 60 };

                pnlParams.Controls.AddRange(new Control[] {
                    lblName, txtName, lblDpi, cmbDpi, lblPaper, cmbPaper,
                    lblOri, cmbOri, lblRot, cmbRot, lblColor, cmbColor,
                    lblScanner, cmbScanner, btnScan, btnPreview
                });

                var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
                _picOriginal = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 40, 40), SizeMode = PictureBoxSizeMode.Zoom };
                _picPreview = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 40, 40), SizeMode = PictureBoxSizeMode.Zoom };
                var lblOrig = new Label { Text = "原图（框选纸张区域）", Dock = DockStyle.Top, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Height = 20, TextAlign = ContentAlignment.MiddleCenter };
                var lblPrev = new Label { Text = "预览效果", Dock = DockStyle.Top, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Height = 20, TextAlign = ContentAlignment.MiddleCenter };
                var pnlOrig = new Panel { Dock = DockStyle.Fill };
                pnlOrig.Controls.Add(_picOriginal);
                pnlOrig.Controls.Add(lblOrig);
                var pnlPrev = new Panel { Dock = DockStyle.Fill };
                pnlPrev.Controls.Add(_picPreview);
                pnlPrev.Controls.Add(lblPrev);
                split.Panel1.Controls.Add(pnlOrig);
                split.Panel2.Controls.Add(pnlPrev);

                var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 45 };
                var btnSaveP = new Button { Text = "保存预设", Location = new Point(300, 10), Width = 80, BackColor = Color.FromArgb(64, 158, 255), ForeColor = Color.White };
                var btnCancel2 = new Button { Text = "取消", Location = new Point(400, 10), Width = 80 };
                pnlBottom.Controls.Add(btnSaveP);
                pnlBottom.Controls.Add(btnCancel2);

                form.Controls.Add(split);
                form.Controls.Add(pnlParams);
                form.Controls.Add(pnlBottom);

                form.Shown += (s, e) => { split.SplitterDistance = split.ClientSize.Width / 2; };

                btnScan.Click += (s, e) =>
                {
                    if (cmbScanner.SelectedItem != null)
                        _scanService.ConnectScanner(cmbScanner.SelectedItem.ToString());
                    _scannedImage?.Dispose();
                    _scannedImage = _scanService.ScanMaxArea();
                    _cachedPreview?.Dispose();
                    _cachedPreview = null;
                    _picOriginal.Image = _scannedImage;
                    _picPreview.Image = null;
                    _cropRect = Rectangle.Empty;
                };

                btnPreview.Click += (s, e) =>
                {
                    if (_scannedImage != null && _cropRect.Width > 20 && _cropRect.Height > 20)
                    {
                        var ar = ScreenToImageRect(_cropRect);
                        var preset = new CropPreset
                        {
                            Dpi = int.Parse(cmbDpi.SelectedItem.ToString()),
                            Paper = cmbPaper.SelectedItem.ToString(),
                            Orientation = cmbOri.SelectedItem.ToString(),
                            Rotation = cmbRot.SelectedIndex * 90,
                            ColorMode = cmbColor.SelectedItem.ToString(),
                            X = ar.X,
                            Y = ar.Y,
                            Width = ar.Width,
                            Height = ar.Height
                        };
                        _cachedPreview?.Dispose();
                        _cachedPreview = _scanService.PreviewPreset(_scannedImage, preset);
                        _picPreview.Image = _cachedPreview;
                    }
                };

                _picOriginal.MouseDown += (s, e) =>
                {
                    _isCropping = true;
                    _cropRect = new Rectangle(e.X, e.Y, 0, 0);
                };
                _picOriginal.MouseMove += (s, e) =>
                {
                    if (!_isCropping || _scannedImage == null) return;
                    _cropRect = new Rectangle(
                        Math.Min(_cropRect.X, e.X), Math.Min(_cropRect.Y, e.Y),
                        Math.Abs(e.X - _cropRect.X), Math.Abs(e.Y - _cropRect.Y));
                    _picOriginal.Invalidate();
                };
                _picOriginal.MouseUp += (s, e) => { _isCropping = false; };
                _picOriginal.Paint += (s, e) =>
                {
                    if (_cropRect.Width > 0 && _cropRect.Height > 0)
                        using (var pen = new Pen(Color.Red, 2) { DashStyle = DashStyle.Dash })
                            e.Graphics.DrawRectangle(pen, _cropRect);
                };

                btnSaveP.Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(txtName.Text) || _scannedImage == null || _cropRect.Width < 10)
                    {
                        MessageBox.Show("请填写名称、扫描并框选纸张区域。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var ar = ScreenToImageRect(_cropRect);
                    int dpi = int.Parse(cmbDpi.SelectedItem.ToString());
                    string paper = cmbPaper.SelectedItem.ToString();
                    string ori = cmbOri.SelectedItem.ToString();
                    int rot = cmbRot.SelectedIndex * 90;
                    string scannerName = cmbScanner.SelectedItem?.ToString() ?? "";
                    string colorMode = cmbColor.SelectedItem.ToString();
                    _presetManager.AddOrUpdate(txtName.Text, scannerName, dpi, paper, ori, rot, colorMode, ar.X, ar.Y, ar.Width, ar.Height);
                    _presetManager.Activate(txtName.Text);
                    RefreshPresetList();

                    var mf = this.ParentForm as MainForm;
                    mf?.RefreshScanPresets();

                    _scannedImage?.Dispose();
                    _cachedPreview?.Dispose();
                    form.Close();
                };

                btnCancel2.Click += (s, e) =>
                {
                    _scannedImage?.Dispose();
                    _cachedPreview?.Dispose();
                    form.Close();
                };

                form.FormClosed += (s, e) =>
                {
                    _scannedImage?.Dispose();
                    _cachedPreview?.Dispose();
                };

                form.ShowDialog();
            }
        }
    }
}