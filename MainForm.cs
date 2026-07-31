// MainForm.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScanTool.Controls;
using ScanTool.Models;
using ScanTool.Services;

namespace ScanTool
{
    public partial class MainForm : Form
    {
        private ApiClient _api;
        private string _scanDir = @"D:\scans";
        private ScanControl _scanControl;
        private ListBox _lstSearchResults;
        private Dictionary<int, List<TreeNodeData>> _orgCache = new Dictionary<int, List<TreeNodeData>>();
        private string _currentRsid;
        private string _currentPersonName;
        private SettingsControl _settingsControl;
        private CropPresetManager _presetManager;
        private string _serverUrl;
        private string _viewerPath = "";
        private ScanService _scanService;
        private string _savedScannerName = "";

        public MainForm(ApiClient api = null)
        {
            InitializeComponent();
            LoadConfig();
            if (api != null)
            {
                _api = api;
                _serverUrl = api.BaseUrl;
            }
            else
            {
                _api = new ApiClient(_serverUrl ?? "http://192.168.18.100");
            }
            this.KeyPreview = true;
        }

        private void menuServer_Click(object sender, EventArgs e) => ShowSettings();

        private void ShowSettings()
        {
            if (_settingsControl == null) _settingsControl = new SettingsControl();
            _settingsControl.Init(_api.BaseUrl, _scanDir, _presetManager, _viewerPath, _scanService);
            panelRight.Controls.Clear();
            _settingsControl.Dock = DockStyle.Fill;
            panelRight.Controls.Add(_settingsControl);
        }

        public void ApplySettings(string serverUrl, string scanDir, string viewerPath)
        {
            _scanDir = scanDir;
            _viewerPath = viewerPath;

            // 只有服务器地址变更时才重新创建 ApiClient
            bool serverChanged = serverUrl != _serverUrl;
            if (serverChanged)
            {
                _serverUrl = serverUrl;
                _api = new ApiClient(serverUrl);
            }

            // 更新 ScanControl 配置
            if (_scanControl != null)
            {
                _scanControl.SetViewerPath(_viewerPath);
                if (serverChanged)
                {
                    _scanControl.Init(_api, _scanDir, _presetManager, _savedScannerName);
                    _scanControl.SetViewerPath(_viewerPath);
                    // 恢复之前选中的人员
                    if (!string.IsNullOrEmpty(_currentRsid))
                        _scanControl.SetPerson(_currentRsid, _currentPersonName);
                }
            }
            _scanControl?.RefreshPresetList();
            SaveConfig(serverUrl, scanDir, viewerPath);
        }
        public void RefreshScanPresets()
        {
            _scanControl?.RefreshPresetList();
        }
        public void RefreshRepairModes()
        {
            _scanControl?.RefreshRepairModeList();
        }
        private void SaveConfig(string serverUrl, string scanDir, string viewerPath)
        {
            var configDir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "ScanTool");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, "config.ini");
            File.WriteAllLines(configPath, new[]
            {
                $"ServerUrl={serverUrl}",
                $"ScanDir={scanDir}",
                $"ViewerPath={viewerPath}",
                $"ScannerName={_savedScannerName}"
            });
        }

        private void LoadConfig()
        {
            var configDir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "ScanTool");
            _presetManager = new CropPresetManager(configDir);
            var configPath = Path.Combine(configDir, "config.ini");
            if (File.Exists(configPath))
            {
                foreach (var line in File.ReadAllLines(configPath))
                {
                    if (line.StartsWith("ServerUrl=")) _serverUrl = line.Substring(10);
                    else if (line.StartsWith("ScanDir=")) _scanDir = line.Substring(8);
                    else if (line.StartsWith("ViewerPath=")) _viewerPath = line.Substring(11);
                    else if (line.StartsWith("ScannerName=")) _savedScannerName = line.Substring(12);
                }
            }
        }

        public void SetScannerName(string name)
        {
            _savedScannerName = name;
            SaveConfig(_api.BaseUrl, _scanDir, _viewerPath);
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            _scanService = new ScanService();
            await LoadOrgTree();
            ShowScan();
        }

        private void ShowOrgTree()
        {
            if (_lstSearchResults != null && panelLeft.Controls.Contains(_lstSearchResults))
                panelLeft.Controls.Remove(_lstSearchResults);
            tvPersons.Visible = true;
            tvPersons.BringToFront();
        }

        private void ShowSearchResults()
        {
            if (_lstSearchResults == null)
            {
                _lstSearchResults = new ListBox { Font = new System.Drawing.Font("微软雅黑", 12F) };
                _lstSearchResults.SelectedIndexChanged += LstSearchResults_SelectedIndexChanged;
            }
            if (!panelLeft.Controls.Contains(_lstSearchResults))
            {
                _lstSearchResults.Dock = DockStyle.Fill;
                panelLeft.Controls.Add(_lstSearchResults);
            }
            tvPersons.Visible = false;
            _lstSearchResults.Visible = true;
            _lstSearchResults.BringToFront();
        }

        private TreeNode CreateNode(TreeNodeData data)
        {
            var text = data.RSID > 0 ? $"{data.TNAME} ({data.DABH})" : $"📁 {data.TNAME}";
            var node = new TreeNode(text) { Tag = data };
            if (data.RSID == 0) node.Nodes.Add(new TreeNode(""));
            return node;
        }

        private async Task LoadOrgTree()
        {
            tvPersons.Nodes.Clear();
            try
            {
                var nodes = await _api.GetOrgRoot();
                _orgCache[-1] = nodes;
                foreach (var n in nodes) tvPersons.Nodes.Add(CreateNode(n));
            }
            catch (Exception ex) { lblStatus.Text = $"加载失败: {ex.Message}"; }
        }

        private async void tvPersons_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (!(node.Tag is TreeNodeData data) || data.RSID != 0) return;
            bool needLoad = node.Nodes.Count == 1 && string.IsNullOrEmpty(node.Nodes[0].Text);
            if (!needLoad) return;
            if (_orgCache.TryGetValue(data.TID, out var cached))
            {
                node.Nodes.Clear();
                foreach (var c in cached) node.Nodes.Add(CreateNode(c));
                return;
            }
            try
            {
                var children = await _api.GetOrgChildren(data.TID);
                _orgCache[data.TID] = children;
                node.Nodes.Clear();
                foreach (var c in children) node.Nodes.Add(CreateNode(c));
            }
            catch { }
        }

        private void tvPersons_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is TreeNodeData data && data.RSID > 0)
            {
                _currentRsid = data.RSID.ToString();
                _currentPersonName = data.TNAME;
                _scanControl?.SetPerson(_currentRsid, _currentPersonName);
            }
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtSearch.Text)) return;
            try
            {
                var persons = await _api.SearchPersons(txtSearch.Text);
                ShowSearchResults();
                _lstSearchResults.Items.Clear();
                foreach (var p in persons) _lstSearchResults.Items.Add(p);
                lblStatus.Text = $"搜索到 {persons.Count} 人";
            }
            catch (Exception ex) { lblStatus.Text = $"搜索失败: {ex.Message}"; }
        }

        private void btnCloseSearch_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            ShowOrgTree();
        }

        private void LstSearchResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_lstSearchResults?.SelectedItem is PersonInfo p)
            {
                _currentRsid = p.Rsid;
                _currentPersonName = p.Name;
                _scanControl?.SetPerson(_currentRsid, _currentPersonName);
            }
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) btnSearch_Click(sender, e);
        }

        public void ShowScan()
        {
            if (_scanControl == null)
            {
                _scanControl = new ScanControl(_scanService);
                _scanControl.Init(_api, _scanDir, _presetManager, _savedScannerName);
            }
            panelRight.Controls.Clear();
            _scanControl.SetViewerPath(_viewerPath);
            _scanControl.Dock = DockStyle.Fill;
            panelRight.Controls.Add(_scanControl);
            if (!string.IsNullOrEmpty(_currentRsid))
                _scanControl.SetPerson(_currentRsid, _currentPersonName);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (txtSearch.Focused) return base.ProcessCmdKey(ref msg, keyData);

            if ((keyData & Keys.Alt) == Keys.Alt)
            {
                Keys numKey = keyData & ~Keys.Alt;
                if (numKey >= Keys.D0 && numKey <= Keys.D9)
                {
                    int idx = numKey - Keys.D0;
                    if (_scanControl != null && _scanControl.Visible)
                    {
                        _scanControl.SelectPreset(idx);
                        return true;
                    }
                }
            }

            if (_scanControl != null && _scanControl.Visible
                && ShortcutManager.Map.TryGetValue(keyData, out var action))
            {
                _scanControl.OnToolAction(action);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}