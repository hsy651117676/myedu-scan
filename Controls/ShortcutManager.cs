using System.Collections.Generic;
using System.Windows.Forms;

namespace ScanTool.Controls
{
    /// <summary>
    /// 快捷键统一管理：定义 → 分发映射 → ToolTip
    /// </summary>
    public static class ShortcutManager
    {
        public static readonly Dictionary<Keys, string> Map = new Dictionary<Keys, string>
        {
            // 批量自动
            { Keys.A, "batch_current_item" },
            { Keys.Control | Keys.A, "batch_category" },
            { Keys.Control | Keys.R, "batch_from_current" },
            { Keys.N, "edit_page_count" },
            { Keys.Control | Keys.L, "clean_orphan_files" },
            { Keys.Control | Keys.Shift | Keys.A, "batch_volume" },

            //上传下载
            { Keys.F1, "upload_item" },
            { Keys.F2, "force_upload_item" },
            { Keys.F3, "download_item" },
            { Keys.F4, "force_download_item" },
            { Keys.F5, "upload_category" },
            { Keys.F6, "force_upload_category" },
            { Keys.F7, "download_category" },
            { Keys.F8, "force_download_category" },
            { Keys.F9, "upload_volume" },
            { Keys.F10, "force_upload_volume" },
            { Keys.F11, "download_volume" },
            { Keys.F12, "force_download_volume" },
            // 扫描           
            { Keys.Enter, "scan" },
            { Keys.S | Keys.Control | Keys.Shift, "replace_scan" },
            // 视图
            { Keys.OemCloseBrackets, "zoom_in" },
            { Keys.OemOpenBrackets, "zoom_out" },
            { Keys.Control | Keys.D, "fit_screen" },
            { Keys.D0, "reset_zoom" },
            // 翻页
            { Keys.Up, "prev_page" },
            { Keys.Down, "next_page" },
            // 旋转翻转
            { Keys.Q, "rotate_left" },
            { Keys.W, "rotate_right" },
            { Keys.Control | Keys.V, "flip_h" },
            { Keys.Control | Keys.H, "flip_v" },
            // 亮度对比度
            { Keys.R, "brightness_up" },
            { Keys.T, "brightness_down" },
            { Keys.F, "contrast_up" },
            { Keys.G, "contrast_down" },
            // 纠偏
            { Keys.V, "auto_deskew" },
            { Keys.B, "manual_deskew" },
            // 去噪灰度
            { Keys.J, "denoise" },
            { Keys.Control | Keys.E, "grayscale" },
            // 裁剪去边擦除
            { Keys.X, "crop" },
            { Keys.D, "remove_border" },
            { Keys.E, "erase" },
            { Keys.Oemplus, "eraser_size_up" },
            { Keys.OemMinus, "eraser_size_down" },
            // 编辑
            { Keys.Control | Keys.Z, "undo" },
            { Keys.Alt | Keys.Z, "redo" },
            { Keys.Z, "restore" },
            // 保存
            { Keys.Control | Keys.S, "save" },
            { Keys.Delete, "delete_selected" },
            { Keys.S, "save_all" },
            { Keys.P, "export_pdf" },
            { Keys.M, "apply_all" },
            // 翻页
            { Keys.Space, "next_page" },
            { Keys.D1, "jump_1" }, { Keys.D2, "jump_2" }, { Keys.D3, "jump_3" },
            { Keys.D4, "jump_4" }, { Keys.D5, "jump_5" }, { Keys.D6, "jump_6" },
            { Keys.D7, "jump_7" }, { Keys.D8, "jump_8" }, { Keys.D9, "jump_9" },
            // 退出
            { Keys.Escape, "escape" },
        };

        private static readonly Dictionary<string, string> ToolTips = new Dictionary<string, string>
        {
            {"scan", "扫描 (Enter)"},
            {"replace_scan", "替换扫描 (Ctrl+Shift+S)"},
            {"zoom_in", "放大 (])"},
            {"zoom_out", "缩小 ([)"},
            {"fit_screen", "适应窗口 (Ctrl+D)"},
            {"reset_zoom", "原始大小 (0)"},
            {"prev_page", "上一页 (↑)"},
            {"next_page", "下一页 (↓ / Space)"},
            {"rotate_left", "左旋90° (Q)"},
            {"rotate_right", "右旋90° (W)"},
            {"flip_h", "水平翻转 (Ctrl+V)"},
            {"flip_v", "垂直翻转 (Ctrl+H)"},
            {"brightness_up", "亮度+ (R)"},
            {"brightness_down", "亮度- (T)"},
            {"contrast_up", "对比度+ (F)"},
            {"contrast_down", "对比度- (G)"},
            {"auto_deskew", "自动纠偏 (V)"},
            {"manual_deskew", "手动纠偏 (B)"},
            {"denoise", "去噪 (J)"},
            {"grayscale", "灰度化 (Ctrl+E)"},
            {"crop", "裁剪 (X)"},
            {"remove_border", "去黑边 (D)"},
            {"erase", "擦除 (E) | +/- 调整大小"},
            {"undo", "撤销 (Ctrl+Z)"},
            {"redo", "重做 (Alt+Z)"},
            {"restore", "恢复原始 (Z)"},
            {"save", "保存当前 (Ctrl+S)"},
            {"save_all", "全部保存 (S)"},
            {"export_pdf", "导出PDF (P)"},
            {"batch_current_item", "当前材料自动修复 (A)"},
            {"batch_category", "当前类批量自动修复 (Ctrl+A)"},
            {"batch_from_current", "从当前开始自动修复 (Ctrl+R)"},
            {"batch_volume", "整卷批量自动修复 (Ctrl+Shift+A)"},
        };

        public static string GetToolTip(string action)
        {
            return ToolTips.TryGetValue(action, out var tip) ? tip : action;
        }
    }
}