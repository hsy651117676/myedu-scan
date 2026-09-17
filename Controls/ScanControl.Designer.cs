using System.Drawing;
using System.Windows.Forms;

namespace ScanTool.Controls
{
    partial class ScanControl
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelTop;
        private Panel panelInfo;
        private Label lblPersonInfo;
        private Label lblLocalPath;
        private ComboBox cmbScanner;
        private ComboBox cmbPreset;
        private ComboBox cmbColorMode;
        private ComboBox cmbRepairMode;
        private SplitContainer splitMain;
        private TreeView tvMaterials;
        private SplitContainer splitRight;
        private DataGridView dgvFiles;
        private Panel panelPreview;
        private PictureBox picPreview;
        private Panel panelTools;
        private StatusStrip statusStrip1;
        private ToolStripProgressBar progressBar1;
        private ContextMenuStrip menuFileContext;
        private ToolStripMenuItem menuFileSave;
        private ToolStripMenuItem menuFileRestore;
        private ToolStripMenuItem menuFileUpload;
        private ToolStripMenuItem menuFileDownload;
        private ToolStripMenuItem menuFileDelete;
        private ToolStripMenuItem menuFileReplaceScan;
        private ToolStripMenuItem menuFileOpen;
        private ToolStripTextBox lblStatus;
        private ToolStripMenuItem menuFileReorder;
        private Panel panelFileList;
        private System.Windows.Forms.ListView lvThumbnails;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelTop = new System.Windows.Forms.Panel();
            this.cmbRotation = new System.Windows.Forms.ComboBox();
            this.cmbSplitMode = new System.Windows.Forms.ComboBox();
            this.chkSplitScan = new System.Windows.Forms.CheckBox();
            this.cmbScanner = new System.Windows.Forms.ComboBox();
            this.cmbPreset = new System.Windows.Forms.ComboBox();
            this.cmbColorMode = new System.Windows.Forms.ComboBox();
            this.cmbRepairMode = new System.Windows.Forms.ComboBox();
            this.panelInfo = new System.Windows.Forms.Panel();
            this.lblPersonInfo = new System.Windows.Forms.Label();
            this.lblLocalPath = new System.Windows.Forms.Label();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.tvMaterials = new System.Windows.Forms.TreeView();
            this.splitRight = new System.Windows.Forms.SplitContainer();
            this.panelFileList = new System.Windows.Forms.Panel();
            this.dgvFiles = new System.Windows.Forms.DataGridView();
            this.menuFileContext = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuFileSave = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileRestore = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileUpload = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileDownload = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileReplaceScan = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileReorder = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileOpen = new System.Windows.Forms.ToolStripMenuItem();
            this.lvThumbnails = new System.Windows.Forms.ListView();
            this.panelPreview = new System.Windows.Forms.Panel();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.panelTools = new System.Windows.Forms.Panel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.progressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.lblStatus = new System.Windows.Forms.ToolStripTextBox();
            this.panelTop.SuspendLayout();
            this.panelInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitRight)).BeginInit();
            this.splitRight.Panel1.SuspendLayout();
            this.splitRight.Panel2.SuspendLayout();
            this.splitRight.SuspendLayout();
            this.panelFileList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFiles)).BeginInit();
            this.menuFileContext.SuspendLayout();
            this.panelPreview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.cmbRotation);
            this.panelTop.Controls.Add(this.cmbSplitMode);
            this.panelTop.Controls.Add(this.chkSplitScan);
            this.panelTop.Controls.Add(this.cmbScanner);
            this.panelTop.Controls.Add(this.cmbPreset);
            this.panelTop.Controls.Add(this.cmbColorMode);
            this.panelTop.Controls.Add(this.cmbRepairMode);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1268, 35);
            this.panelTop.TabIndex = 3;
            // 
            // cmbRotation
            // 
            this.cmbRotation.FormattingEnabled = true;
            this.cmbRotation.Location = new System.Drawing.Point(1161, 5);
            this.cmbRotation.Name = "cmbRotation";
            this.cmbRotation.Size = new System.Drawing.Size(128, 25);
            this.cmbRotation.TabIndex = 9;
            // 
            // cmbSplitMode
            // 
            this.cmbSplitMode.FormattingEnabled = true;
            this.cmbSplitMode.Location = new System.Drawing.Point(1028, 6);
            this.cmbSplitMode.Name = "cmbSplitMode";
            this.cmbSplitMode.Size = new System.Drawing.Size(128, 25);
            this.cmbSplitMode.TabIndex = 8;
            // 
            // chkSplitScan
            // 
            this.chkSplitScan.AutoSize = true;
            this.chkSplitScan.Location = new System.Drawing.Point(956, 8);
            this.chkSplitScan.Name = "chkSplitScan";
            this.chkSplitScan.Size = new System.Drawing.Size(75, 21);
            this.chkSplitScan.TabIndex = 7;
            this.chkSplitScan.Text = "拆分扫描";
            this.chkSplitScan.UseVisualStyleBackColor = true;
            // 
            // cmbScanner
            // 
            this.cmbScanner.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbScanner.Font = new System.Drawing.Font("微软雅黑", 11F);
            this.cmbScanner.Location = new System.Drawing.Point(7, 3);
            this.cmbScanner.Name = "cmbScanner";
            this.cmbScanner.Size = new System.Drawing.Size(257, 28);
            this.cmbScanner.TabIndex = 0;
            // 
            // cmbPreset
            // 
            this.cmbPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPreset.Font = new System.Drawing.Font("微软雅黑", 11F);
            this.cmbPreset.Items.AddRange(new object[] {
            "自动检测"});
            this.cmbPreset.Location = new System.Drawing.Point(270, 4);
            this.cmbPreset.Name = "cmbPreset";
            this.cmbPreset.Size = new System.Drawing.Size(302, 28);
            this.cmbPreset.TabIndex = 1;
            // 
            // cmbColorMode
            // 
            this.cmbColorMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbColorMode.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.cmbColorMode.Items.AddRange(new object[] {
            "彩色",
            "灰度",
            "黑白"});
            this.cmbColorMode.Location = new System.Drawing.Point(578, 5);
            this.cmbColorMode.Name = "cmbColorMode";
            this.cmbColorMode.Size = new System.Drawing.Size(70, 27);
            this.cmbColorMode.TabIndex = 5;
            // 
            // cmbRepairMode
            // 
            this.cmbRepairMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRepairMode.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.cmbRepairMode.Items.AddRange(new object[] {
            "不自动修复",
            "适当修复",
            "深度修复"});
            this.cmbRepairMode.Location = new System.Drawing.Point(654, 4);
            this.cmbRepairMode.Name = "cmbRepairMode";
            this.cmbRepairMode.Size = new System.Drawing.Size(281, 27);
            this.cmbRepairMode.TabIndex = 6;
            // 
            // panelInfo
            // 
            this.panelInfo.Controls.Add(this.lblPersonInfo);
            this.panelInfo.Controls.Add(this.lblLocalPath);
            this.panelInfo.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelInfo.Location = new System.Drawing.Point(0, 35);
            this.panelInfo.Name = "panelInfo";
            this.panelInfo.Padding = new System.Windows.Forms.Padding(4, 2, 4, 0);
            this.panelInfo.Size = new System.Drawing.Size(1268, 41);
            this.panelInfo.TabIndex = 2;
            // 
            // lblPersonInfo
            // 
            this.lblPersonInfo.AutoSize = true;
            this.lblPersonInfo.Font = new System.Drawing.Font("微软雅黑", 16F);
            this.lblPersonInfo.Location = new System.Drawing.Point(4, 3);
            this.lblPersonInfo.Name = "lblPersonInfo";
            this.lblPersonInfo.Size = new System.Drawing.Size(0, 30);
            this.lblPersonInfo.TabIndex = 0;
            // 
            // lblLocalPath
            // 
            this.lblLocalPath.AutoSize = true;
            this.lblLocalPath.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblLocalPath.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblLocalPath.Font = new System.Drawing.Font("微软雅黑", 14F);
            this.lblLocalPath.ForeColor = System.Drawing.Color.Blue;
            this.lblLocalPath.Location = new System.Drawing.Point(1264, 2);
            this.lblLocalPath.Name = "lblLocalPath";
            this.lblLocalPath.Size = new System.Drawing.Size(0, 25);
            this.lblLocalPath.TabIndex = 1;
            this.lblLocalPath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 76);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.tvMaterials);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.splitRight);
            this.splitMain.Size = new System.Drawing.Size(1268, 411);
            this.splitMain.SplitterDistance = 330;
            this.splitMain.TabIndex = 1;
            // 
            // tvMaterials
            // 
            this.tvMaterials.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvMaterials.Font = new System.Drawing.Font("微软雅黑", 11F);
            this.tvMaterials.HideSelection = false;
            this.tvMaterials.Location = new System.Drawing.Point(0, 0);
            this.tvMaterials.Name = "tvMaterials";
            this.tvMaterials.Size = new System.Drawing.Size(330, 411);
            this.tvMaterials.TabIndex = 0;
            // 
            // splitRight
            // 
            this.splitRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitRight.Location = new System.Drawing.Point(0, 0);
            this.splitRight.Name = "splitRight";
            // 
            // splitRight.Panel1
            // 
            this.splitRight.Panel1.Controls.Add(this.panelFileList);
            // 
            // splitRight.Panel2
            // 
            this.splitRight.Panel2.Controls.Add(this.panelPreview);
            this.splitRight.Panel2.Controls.Add(this.panelTools);
            this.splitRight.Size = new System.Drawing.Size(934, 411);
            this.splitRight.SplitterDistance = 247;
            this.splitRight.TabIndex = 0;
            // 
            // panelFileList
            // 
            this.panelFileList.Controls.Add(this.dgvFiles);
            this.panelFileList.Controls.Add(this.lvThumbnails);
            this.panelFileList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelFileList.Location = new System.Drawing.Point(0, 0);
            this.panelFileList.Name = "panelFileList";
            this.panelFileList.Size = new System.Drawing.Size(247, 411);
            this.panelFileList.TabIndex = 0;
            // 
            // dgvFiles
            // 
            this.dgvFiles.ContextMenuStrip = this.menuFileContext;
            this.dgvFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvFiles.Location = new System.Drawing.Point(0, 0);
            this.dgvFiles.Name = "dgvFiles";
            this.dgvFiles.Size = new System.Drawing.Size(247, 411);
            this.dgvFiles.TabIndex = 0;
            // 
            // menuFileContext
            // 
            this.menuFileContext.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuFileSave,
            this.menuFileRestore,
            this.menuFileUpload,
            this.menuFileDownload,
            this.menuFileDelete,
            this.menuFileReplaceScan,
            this.menuFileReorder,
            this.menuFileOpen});
            this.menuFileContext.Name = "menuFileContext";
            this.menuFileContext.Size = new System.Drawing.Size(145, 180);
            // 
            // menuFileSave
            // 
            this.menuFileSave.Name = "menuFileSave";
            this.menuFileSave.Size = new System.Drawing.Size(144, 22);
            this.menuFileSave.Text = "💾 保存";
            // 
            // menuFileRestore
            // 
            this.menuFileRestore.Name = "menuFileRestore";
            this.menuFileRestore.Size = new System.Drawing.Size(144, 22);
            this.menuFileRestore.Text = "🔄 恢复原始";
            // 
            // menuFileUpload
            // 
            this.menuFileUpload.Name = "menuFileUpload";
            this.menuFileUpload.Size = new System.Drawing.Size(144, 22);
            this.menuFileUpload.Text = "📤 上传";
            // 
            // menuFileDownload
            // 
            this.menuFileDownload.Name = "menuFileDownload";
            this.menuFileDownload.Size = new System.Drawing.Size(144, 22);
            this.menuFileDownload.Text = "📥 下载";
            // 
            // menuFileDelete
            // 
            this.menuFileDelete.Name = "menuFileDelete";
            this.menuFileDelete.Size = new System.Drawing.Size(144, 22);
            this.menuFileDelete.Text = "🗑 删除本地";
            // 
            // menuFileReplaceScan
            // 
            this.menuFileReplaceScan.Name = "menuFileReplaceScan";
            this.menuFileReplaceScan.Size = new System.Drawing.Size(144, 22);
            this.menuFileReplaceScan.Text = "🔄 替换扫描";
            // 
            // menuFileReorder
            // 
            this.menuFileReorder.Name = "menuFileReorder";
            this.menuFileReorder.Size = new System.Drawing.Size(144, 22);
            this.menuFileReorder.Text = "📋 调整顺序";
            // 
            // menuFileOpen
            // 
            this.menuFileOpen.Name = "menuFileOpen";
            this.menuFileOpen.Size = new System.Drawing.Size(144, 22);
            this.menuFileOpen.Text = "🔍 打开方式";
            // 
            // lvThumbnails
            // 
            this.lvThumbnails.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.lvThumbnails.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lvThumbnails.ContextMenuStrip = this.menuFileContext;
            this.lvThumbnails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvThumbnails.HideSelection = false;
            this.lvThumbnails.Location = new System.Drawing.Point(0, 0);
            this.lvThumbnails.Name = "lvThumbnails";
            this.lvThumbnails.Size = new System.Drawing.Size(247, 411);
            this.lvThumbnails.TabIndex = 1;
            this.lvThumbnails.UseCompatibleStateImageBehavior = false;
            this.lvThumbnails.Visible = false;
            // 
            // panelPreview
            // 
            this.panelPreview.Controls.Add(this.picPreview);
            this.panelPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreview.Location = new System.Drawing.Point(0, 0);
            this.panelPreview.Name = "panelPreview";
            this.panelPreview.Size = new System.Drawing.Size(483, 411);
            this.panelPreview.TabIndex = 0;
            // 
            // picPreview
            // 
            this.picPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.picPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picPreview.Location = new System.Drawing.Point(0, 0);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(483, 411);
            this.picPreview.TabIndex = 0;
            this.picPreview.TabStop = false;
            // 
            // panelTools
            // 
            this.panelTools.AutoScroll = true;
            this.panelTools.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelTools.Location = new System.Drawing.Point(483, 0);
            this.panelTools.Name = "panelTools";
            this.panelTools.Size = new System.Drawing.Size(200, 411);
            this.panelTools.TabIndex = 1;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.progressBar1,
            this.lblStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 487);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1268, 28);
            this.statusStrip1.TabIndex = 4;
            // 
            // progressBar1
            // 
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(120, 22);
            this.progressBar1.Visible = false;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = false;
            this.lblStatus.BackColor = System.Drawing.SystemColors.Control;
            this.lblStatus.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.ReadOnly = true;
            this.lblStatus.Size = new System.Drawing.Size(750, 28);
            this.lblStatus.Text = "就绪";
            // 
            // ScanControl
            // 
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.panelInfo);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.statusStrip1);
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.Name = "ScanControl";
            this.Size = new System.Drawing.Size(1268, 515);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelInfo.ResumeLayout(false);
            this.panelInfo.PerformLayout();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.splitRight.Panel1.ResumeLayout(false);
            this.splitRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitRight)).EndInit();
            this.splitRight.ResumeLayout(false);
            this.panelFileList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvFiles)).EndInit();
            this.menuFileContext.ResumeLayout(false);
            this.panelPreview.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private ComboBox cmbSplitMode;
        private CheckBox chkSplitScan;
        private ComboBox cmbRotation;
    }
}