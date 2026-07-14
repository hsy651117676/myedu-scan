using System;
using System.Windows.Forms;

namespace ScanTool.Controls
{
    public class ScanContextMenu
    {
        private readonly ScanControl _owner;
        private readonly Func<bool> _confirmForce;

        public ContextMenuStrip MenuCategory { get; }
        public ContextMenuStrip MenuItem { get; }

        public ScanContextMenu(ScanControl owner, Func<bool> confirmForce)
        {
            _owner = owner;
            _confirmForce = confirmForce;
            MenuCategory = new ContextMenuStrip();
            MenuItem = new ContextMenuStrip();
            Build();
        }

        private void Build()
        {
            // ========== 分类节点菜单 ==========
            MenuCategory.Items.Add("🤖 Ctrl+A 当前类批量自动修复", null, async (s, e) => await _owner.StartBatchCategory());
            MenuCategory.Items.Add(new ToolStripSeparator());
            MenuCategory.Items.Add("📤 F5  上传整类（仅传新文件和已修改）", null, async (s, e) => await _owner.UploadCategory());
            MenuCategory.Items.Add("📤 F6  上传整类（强制全部重传）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceUploadCategory(); });
            MenuCategory.Items.Add("📥 F7  下载整类（仅下缺失和更新）", null, async (s, e) => await _owner.DownloadCategory());
            MenuCategory.Items.Add("📥 F8  下载整类（强制全部重下）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceDownloadCategory(); });
            MenuCategory.Items.Add(new ToolStripSeparator());
            MenuCategory.Items.Add("📤 F9  上传整卷（仅传新文件和已修改）", null, async (s, e) => await _owner.UploadWholeVolume());
            MenuCategory.Items.Add("📤 F10 上传整卷（强制全部重传）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceUploadWholeVolume(); });
            MenuCategory.Items.Add("📥 F11 下载整卷（仅下缺失和更新）", null, async (s, e) => await _owner.DownloadWholeVolume());
            MenuCategory.Items.Add("📥 F12 下载整卷（强制全部重下）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceDownloadWholeVolume(); });

            // ========== 材料节点菜单 ==========
            MenuItem.Items.Add("🤖 A 当前材料自动修复", null, async (s, e) => await _owner.StartBatchCurrentItem());
            MenuItem.Items.Add("🤖 Ctrl+A 当前类批量自动修复", null, async (s, e) => await _owner.StartBatchCategory());
            MenuItem.Items.Add("🤖 Ctrl+R 从当前材料开始自动修复", null, async (s, e) => await _owner.StartBatchFromCurrent());
            MenuItem.Items.Add(new ToolStripSeparator());
            MenuItem.Items.Add("🔄 Ctrl+Alt+S 替换扫描", null, (s, e) => _owner.ReplaceScan());
            MenuItem.Items.Add("📝 N 修改页数", null, (s, e) => _owner.ShowEditPageCountDialog());
            MenuItem.Items.Add(new ToolStripSeparator());
            MenuItem.Items.Add("📤 F1  上传材料（仅传新文件和已修改）", null, async (s, e) => await _owner.UploadItem());
            MenuItem.Items.Add("📤 F2  上传材料（强制全部重传）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceUploadItem(); });
            MenuItem.Items.Add("📥 F3  下载材料（仅下缺失和更新）", null, async (s, e) => await _owner.DownloadItem());
            MenuItem.Items.Add("📥 F4  下载材料（强制全部重下）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceDownloadItem(); });
            MenuItem.Items.Add(new ToolStripSeparator());
            MenuItem.Items.Add("📤 F5  上传整类（仅传新文件和已修改）", null, async (s, e) => await _owner.UploadCategory());
            MenuItem.Items.Add("📤 F6  上传整类（强制全部重传）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceUploadCategory(); });
            MenuItem.Items.Add("📥 F7  下载整类（仅下缺失和更新）", null, async (s, e) => await _owner.DownloadCategory());
            MenuItem.Items.Add("📥 F8  下载整类（强制全部重下）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceDownloadCategory(); });
            MenuItem.Items.Add(new ToolStripSeparator());
            MenuItem.Items.Add("📤 F9  上传整卷（仅传新文件和已修改）", null, async (s, e) => await _owner.UploadWholeVolume());
            MenuItem.Items.Add("📤 F10 上传整卷（强制全部重传）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceUploadWholeVolume(); });
            MenuItem.Items.Add("📥 F11 下载整卷（仅下缺失和更新）", null, async (s, e) => await _owner.DownloadWholeVolume());
            MenuItem.Items.Add("📥 F12 下载整卷（强制全部重下）", null, async (s, e) => { if (_confirmForce()) await _owner.ForceDownloadWholeVolume(); });
            MenuItem.Items.Add(new ToolStripSeparator());
            MenuItem.Items.Add("🗑 Ctrl+L 清理多余文件", null, async (s, e) => await _owner.CleanOrphanFiles());
        }
    }
}