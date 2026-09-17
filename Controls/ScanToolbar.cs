using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScanTool.Controls
{
    public class ScanToolbar
    {
        private readonly Panel _panel;
        private readonly Action<string> _onAction;
        private readonly List<Control> _controls = new List<Control>();

        private const int Col1X = 0;
        private const int Col2X = 100;
        private const int BtnWidth = 90;
        private const int BtnHeight = 32;

        public ScanToolbar(Panel panel, Action<string> onAction)
        {
            _panel = panel;
            _onAction = onAction;
            _panel.AutoScroll = false;
            Build();
        }

        private void Build()
        {
            _panel.Controls.Clear();
            _controls.Clear();

            int y = 5;

            // ==================== 扫描操作 ====================
            AddSection("扫描操作", ref y);
            AddTwoButtons("📷 扫描", "scan", "🔄 替换扫描", "replace_scan", ref y);

            // ==================== 视图操作 ====================
            AddSection("视图操作", ref y);
            AddTwoButtons("🔍 放大", "zoom_in", "🔎 缩小", "zoom_out", ref y);
            AddTwoButtons("📐 适应", "fit_screen", "🔄 重置", "reset_zoom", ref y);
            AddTwoButtons("⬆ 上移", "pan_up", "⬇ 下移", "pan_down", ref y);
            AddTwoButtons("⬅ 左移", "pan_left", "➡ 右移", "pan_right", ref y);

            // ==================== 图像操作 ====================
            AddSection("图像操作", ref y);
            AddTwoButtons("↺ 左转", "rotate_left", "↻ 右转", "rotate_right", ref y);
            AddTwoButtons("↔ 水平翻转", "flip_h", "↕ 垂直翻转", "flip_v", ref y);
            AddTwoButtons("☀ 亮度+", "brightness_up", "🌙 亮度-", "brightness_down", ref y);
            AddTwoButtons("◐ 对比度+", "contrast_up", "◑ 对比度-", "contrast_down", ref y);
            AddTwoButtons("📏 自动纠偏", "auto_deskew", "📐 手动纠偏", "manual_deskew", ref y);
            AddTwoButtons("🧹 去噪", "denoise", "⬜ 灰度", "grayscale", ref y);
            AddTwoButtons("✂ 裁剪", "crop", "⬛ 去黑边", "remove_border", ref y);

            // ==================== 编辑操作 ====================
            AddSection("编辑操作", ref y);
            AddTwoButtons("🧹 擦除", "erase", "↩ 撤销", "undo", ref y);
            AddTwoButtons("↪ 重做", "redo", "🔄 恢复原始", "restore", ref y);

            // ==================== 文件操作 ====================
            AddSection("文件操作", ref y);
            AddTwoButtons("💾 保存", "save", "💾 全存", "save_all", ref y);
            AddOneButton("📄 导出PDF", "export_pdf", ref y);
            AddOneButton("🖼 切换视图", "toggle_view", ref y);

            // ==================== 批量操作 ====================
            AddSection("批量操作", ref y);
            AddTwoButtons("🤖 当前材料", "batch_current_item", "🤖 当前类", "batch_category", ref y);
            AddOneButton("🤖 从当前开始", "batch_from_current", ref y);
        }

        private void AddSection(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(5, y),
                Size = new Size(180, 20),
                Font = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(64, 64, 64)
            };
            _panel.Controls.Add(lbl);
            y += 22;
        }

        private void AddTwoButtons(string text1, string action1, string text2, string action2, ref int y)
        {
            var btn1 = MakeButton(text1, action1, Col1X, y);
            var btn2 = MakeButton(text2, action2, Col2X, y);
            _panel.Controls.Add(btn1);
            _panel.Controls.Add(btn2);
            y += BtnHeight + 4;
        }

        private void AddOneButton(string text, string action, ref int y)
        {
            var btn = MakeButton(text, action, Col1X, y);
            btn.Width = BtnWidth * 2 + 4;
            _panel.Controls.Add(btn);
            y += BtnHeight + 4;
        }

        private Button MakeButton(string text, string action, int x, int y)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(BtnWidth, BtnHeight),
                Font = new Font("微软雅黑", 9F),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Tag = action
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btn.Click += (s, e) => _onAction?.Invoke((string)((Button)s).Tag);

            var tip = ShortcutManager.GetToolTip(action);
            if (!string.IsNullOrEmpty(tip))
            {
                var t = new ToolTip();
                t.SetToolTip(btn, tip);
            }

            return btn;
        }
    }
}