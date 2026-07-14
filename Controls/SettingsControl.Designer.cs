using System.Drawing;
using System.Windows.Forms;

namespace ScanTool.Controls
{
    partial class SettingsControl
    {
        private System.ComponentModel.IContainer components = null;
        private TabControl tabControl1;
        private TabPage tabServer, tabScan, tabAbout;
        private Label lblServer; private TextBox txtServerUrl;
        private Label lblScanDir; private TextBox txtScanDir; private Button btnBrowse;
        private Label lblDefaultScanner; private ComboBox cmbDefaultScanner;
        private GroupBox grpCropMode; private RadioButton rbAutoDetect; private RadioButton rbUsePreset;
        private Label lblPresetList; private ListBox lstPresets;
        private Button btnActivatePreset; private Button btnDeletePreset; private Button btnAddPreset;
        private Button btnRepairPresets;
        private Label lblAbout;
        private Panel bottomPanel; private Button btnSave; private Button btnCancel;
        private TabPage tabViewer;
        private Label lblViewerPath;
        private TextBox txtViewerPath;
        private Button btnBrowseViewer;
        private CheckBox chkUseDefault;
        protected override void Dispose(bool disposing)
        { if (disposing && (components != null)) components.Dispose(); base.Dispose(disposing); }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsControl));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabServer = new System.Windows.Forms.TabPage();
            this.lblServer = new System.Windows.Forms.Label();
            this.txtServerUrl = new System.Windows.Forms.TextBox();
            this.lblScanDir = new System.Windows.Forms.Label();
            this.txtScanDir = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.tabScan = new System.Windows.Forms.TabPage();
            this.lblDefaultScanner = new System.Windows.Forms.Label();
            this.cmbDefaultScanner = new System.Windows.Forms.ComboBox();
            this.grpCropMode = new System.Windows.Forms.GroupBox();
            this.rbAutoDetect = new System.Windows.Forms.RadioButton();
            this.rbUsePreset = new System.Windows.Forms.RadioButton();
            this.lblPresetList = new System.Windows.Forms.Label();
            this.lstPresets = new System.Windows.Forms.ListBox();
            this.btnActivatePreset = new System.Windows.Forms.Button();
            this.btnDeletePreset = new System.Windows.Forms.Button();
            this.btnAddPreset = new System.Windows.Forms.Button();
            this.btnRepairPresets = new System.Windows.Forms.Button();
            this.tabViewer = new System.Windows.Forms.TabPage();
            this.txtViewerPath = new System.Windows.Forms.TextBox();
            this.lblViewerPath = new System.Windows.Forms.Label();
            this.btnBrowseViewer = new System.Windows.Forms.Button();
            this.chkUseDefault = new System.Windows.Forms.CheckBox();
            this.tabAbout = new System.Windows.Forms.TabPage();
            this.lblAbout = new System.Windows.Forms.Label();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.tabServer.SuspendLayout();
            this.tabScan.SuspendLayout();
            this.grpCropMode.SuspendLayout();
            this.tabViewer.SuspendLayout();
            this.tabAbout.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabServer);
            this.tabControl1.Controls.Add(this.tabScan);
            this.tabControl1.Controls.Add(this.tabViewer);
            this.tabControl1.Controls.Add(this.tabAbout);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(872, 515);
            this.tabControl1.TabIndex = 0;
            // 
            // tabServer
            // 
            this.tabServer.Controls.Add(this.lblServer);
            this.tabServer.Controls.Add(this.txtServerUrl);
            this.tabServer.Controls.Add(this.lblScanDir);
            this.tabServer.Controls.Add(this.txtScanDir);
            this.tabServer.Controls.Add(this.btnBrowse);
            this.tabServer.Location = new System.Drawing.Point(4, 28);
            this.tabServer.Name = "tabServer";
            this.tabServer.Size = new System.Drawing.Size(864, 483);
            this.tabServer.TabIndex = 0;
            this.tabServer.Text = "服务器";
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(25, 35);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(93, 20);
            this.lblServer.TabIndex = 0;
            this.lblServer.Text = "服务器地址：";
            // 
            // txtServerUrl
            // 
            this.txtServerUrl.Location = new System.Drawing.Point(125, 32);
            this.txtServerUrl.Name = "txtServerUrl";
            this.txtServerUrl.Size = new System.Drawing.Size(608, 25);
            this.txtServerUrl.TabIndex = 1;
            // 
            // lblScanDir
            // 
            this.lblScanDir.AutoSize = true;
            this.lblScanDir.Location = new System.Drawing.Point(25, 75);
            this.lblScanDir.Name = "lblScanDir";
            this.lblScanDir.Size = new System.Drawing.Size(79, 20);
            this.lblScanDir.TabIndex = 2;
            this.lblScanDir.Text = "扫描目录：";
            // 
            // txtScanDir
            // 
            this.txtScanDir.Location = new System.Drawing.Point(125, 72);
            this.txtScanDir.Name = "txtScanDir";
            this.txtScanDir.Size = new System.Drawing.Size(608, 25);
            this.txtScanDir.TabIndex = 3;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(758, 72);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(55, 26);
            this.btnBrowse.TabIndex = 4;
            this.btnBrowse.Text = "浏览...";
            // 
            // tabScan
            // 
            this.tabScan.Controls.Add(this.lblDefaultScanner);
            this.tabScan.Controls.Add(this.cmbDefaultScanner);
            this.tabScan.Controls.Add(this.grpCropMode);
            this.tabScan.Controls.Add(this.lblPresetList);
            this.tabScan.Controls.Add(this.lstPresets);
            this.tabScan.Controls.Add(this.btnActivatePreset);
            this.tabScan.Controls.Add(this.btnDeletePreset);
            this.tabScan.Controls.Add(this.btnAddPreset);
            this.tabScan.Controls.Add(this.btnRepairPresets);
            this.tabScan.Location = new System.Drawing.Point(4, 28);
            this.tabScan.Name = "tabScan";
            this.tabScan.Size = new System.Drawing.Size(864, 483);
            this.tabScan.TabIndex = 1;
            this.tabScan.Text = "扫描";
            // 
            // lblDefaultScanner
            // 
            this.lblDefaultScanner.AutoSize = true;
            this.lblDefaultScanner.Location = new System.Drawing.Point(25, 15);
            this.lblDefaultScanner.Name = "lblDefaultScanner";
            this.lblDefaultScanner.Size = new System.Drawing.Size(93, 20);
            this.lblDefaultScanner.TabIndex = 0;
            this.lblDefaultScanner.Text = "默认扫描仪：";
            // 
            // cmbDefaultScanner
            // 
            this.cmbDefaultScanner.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDefaultScanner.Location = new System.Drawing.Point(125, 12);
            this.cmbDefaultScanner.Name = "cmbDefaultScanner";
            this.cmbDefaultScanner.Size = new System.Drawing.Size(658, 27);
            this.cmbDefaultScanner.TabIndex = 1;
            // 
            // grpCropMode
            // 
            this.grpCropMode.Controls.Add(this.rbAutoDetect);
            this.grpCropMode.Controls.Add(this.rbUsePreset);
            this.grpCropMode.Location = new System.Drawing.Point(25, 50);
            this.grpCropMode.Name = "grpCropMode";
            this.grpCropMode.Size = new System.Drawing.Size(200, 55);
            this.grpCropMode.TabIndex = 2;
            this.grpCropMode.TabStop = false;
            this.grpCropMode.Text = "裁切模式";
            // 
            // rbAutoDetect
            // 
            this.rbAutoDetect.Checked = true;
            this.rbAutoDetect.Location = new System.Drawing.Point(10, 22);
            this.rbAutoDetect.Name = "rbAutoDetect";
            this.rbAutoDetect.Size = new System.Drawing.Size(80, 24);
            this.rbAutoDetect.TabIndex = 0;
            this.rbAutoDetect.TabStop = true;
            this.rbAutoDetect.Text = "自动检测";
            // 
            // rbUsePreset
            // 
            this.rbUsePreset.Location = new System.Drawing.Point(100, 22);
            this.rbUsePreset.Name = "rbUsePreset";
            this.rbUsePreset.Size = new System.Drawing.Size(90, 24);
            this.rbUsePreset.TabIndex = 1;
            this.rbUsePreset.Text = "使用预设";
            // 
            // lblPresetList
            // 
            this.lblPresetList.AutoSize = true;
            this.lblPresetList.Location = new System.Drawing.Point(25, 115);
            this.lblPresetList.Name = "lblPresetList";
            this.lblPresetList.Size = new System.Drawing.Size(107, 20);
            this.lblPresetList.TabIndex = 3;
            this.lblPresetList.Text = "裁剪预设列表：";
            // 
            // lstPresets
            // 
            this.lstPresets.ItemHeight = 19;
            this.lstPresets.Location = new System.Drawing.Point(25, 140);
            this.lstPresets.Name = "lstPresets";
            this.lstPresets.Size = new System.Drawing.Size(250, 137);
            this.lstPresets.TabIndex = 4;
            // 
            // btnActivatePreset
            // 
            this.btnActivatePreset.Location = new System.Drawing.Point(285, 140);
            this.btnActivatePreset.Name = "btnActivatePreset";
            this.btnActivatePreset.Size = new System.Drawing.Size(60, 26);
            this.btnActivatePreset.TabIndex = 5;
            this.btnActivatePreset.Text = "激活";
            // 
            // btnDeletePreset
            // 
            this.btnDeletePreset.Location = new System.Drawing.Point(285, 175);
            this.btnDeletePreset.Name = "btnDeletePreset";
            this.btnDeletePreset.Size = new System.Drawing.Size(60, 26);
            this.btnDeletePreset.TabIndex = 6;
            this.btnDeletePreset.Text = "删除";
            // 
            // btnAddPreset
            // 
            this.btnAddPreset.Location = new System.Drawing.Point(285, 210);
            this.btnAddPreset.Name = "btnAddPreset";
            this.btnAddPreset.Size = new System.Drawing.Size(60, 26);
            this.btnAddPreset.TabIndex = 7;
            this.btnAddPreset.Text = "+新增";
            // 
            // btnRepairPresets
            // 
            this.btnRepairPresets.Location = new System.Drawing.Point(285, 245);
            this.btnRepairPresets.Name = "btnRepairPresets";
            this.btnRepairPresets.Size = new System.Drawing.Size(60, 26);
            this.btnRepairPresets.TabIndex = 8;
            this.btnRepairPresets.Text = "修复预设";
            // 
            // tabViewer
            // 
            this.tabViewer.Controls.Add(this.txtViewerPath);
            this.tabViewer.Controls.Add(this.lblViewerPath);
            this.tabViewer.Controls.Add(this.btnBrowseViewer);
            this.tabViewer.Controls.Add(this.chkUseDefault);
            this.tabViewer.Location = new System.Drawing.Point(4, 28);
            this.tabViewer.Name = "tabViewer";
            this.tabViewer.Size = new System.Drawing.Size(864, 483);
            this.tabViewer.TabIndex = 3;
            this.tabViewer.Text = "文件查看器";
            // 
            // txtViewerPath
            // 
            this.txtViewerPath.Location = new System.Drawing.Point(102, 18);
            this.txtViewerPath.Name = "txtViewerPath";
            this.txtViewerPath.Size = new System.Drawing.Size(677, 25);
            this.txtViewerPath.TabIndex = 1;
            // 
            // lblViewerPath
            // 
            this.lblViewerPath.AutoSize = true;
            this.lblViewerPath.Location = new System.Drawing.Point(3, 18);
            this.lblViewerPath.Name = "lblViewerPath";
            this.lblViewerPath.Size = new System.Drawing.Size(93, 20);
            this.lblViewerPath.TabIndex = 0;
            this.lblViewerPath.Text = "查看器路径：";
            // 
            // btnBrowseViewer
            // 
            this.btnBrowseViewer.Location = new System.Drawing.Point(785, 18);
            this.btnBrowseViewer.Name = "btnBrowseViewer";
            this.btnBrowseViewer.Size = new System.Drawing.Size(55, 26);
            this.btnBrowseViewer.TabIndex = 2;
            this.btnBrowseViewer.Text = "浏览...";
            // 
            // chkUseDefault
            // 
            this.chkUseDefault.AutoSize = true;
            this.chkUseDefault.Location = new System.Drawing.Point(125, 70);
            this.chkUseDefault.Name = "chkUseDefault";
            this.chkUseDefault.Size = new System.Drawing.Size(168, 24);
            this.chkUseDefault.TabIndex = 3;
            this.chkUseDefault.Text = "使用系统默认程序打开";
            // 
            // tabAbout
            // 
            this.tabAbout.Controls.Add(this.lblAbout);
            this.tabAbout.Location = new System.Drawing.Point(4, 28);
            this.tabAbout.Name = "tabAbout";
            this.tabAbout.Size = new System.Drawing.Size(864, 483);
            this.tabAbout.TabIndex = 2;
            this.tabAbout.Text = "关于";
            // 
            // lblAbout
            // 
            this.lblAbout.AutoSize = true;
            this.lblAbout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAbout.Location = new System.Drawing.Point(0, 0);
            this.lblAbout.Name = "lblAbout";
            this.lblAbout.Size = new System.Drawing.Size(481, 480);
            this.lblAbout.TabIndex = 0;
            this.lblAbout.Text = resources.GetString("lblAbout.Text");
            // 
            // bottomPanel
            // 
            this.bottomPanel.Controls.Add(this.btnSave);
            this.bottomPanel.Controls.Add(this.btnCancel);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 515);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(872, 45);
            this.bottomPanel.TabIndex = 1;
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(158)))), ((int)(((byte)(255)))));
            this.btnSave.ForeColor = System.Drawing.Color.White;
            this.btnSave.Location = new System.Drawing.Point(280, 10);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 28);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "保存";
            this.btnSave.UseVisualStyleBackColor = false;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(365, 10);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(109, 28);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "关闭（返回）";
            // 
            // SettingsControl
            // 
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.bottomPanel);
            this.Name = "SettingsControl";
            this.Size = new System.Drawing.Size(872, 560);
            this.tabControl1.ResumeLayout(false);
            this.tabServer.ResumeLayout(false);
            this.tabServer.PerformLayout();
            this.tabScan.ResumeLayout(false);
            this.tabScan.PerformLayout();
            this.grpCropMode.ResumeLayout(false);
            this.tabViewer.ResumeLayout(false);
            this.tabViewer.PerformLayout();
            this.tabAbout.ResumeLayout(false);
            this.tabAbout.PerformLayout();
            this.bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}