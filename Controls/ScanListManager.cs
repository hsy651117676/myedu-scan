// Controls/ScanListManager.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ScanTool.Models;
using ScanTool.Services;

namespace ScanTool.Controls
{
    public enum ScanListViewMode
    {
        List,      // 列表视图
        Thumbnail  // 平铺缩略图视图
    }

    public class ScanListManager
    {
        private readonly DataGridView _grid;
        private readonly ListView _thumbnailView; // 新增：缩略图视图
        private readonly Panel _container; // 新增：容器面板
        private List<FileStatusInfo> _files = new List<FileStatusInfo>();
        private ImageCacheManager _imageCache;
        private ScanListViewMode _viewMode = ScanListViewMode.List;
        private int _thumbnailSize = 170; // 缩略图大小

        public event Action<int> SelectedIndexChanged;
        public event Action PrevMaterialRequested;
        public int Count => _files.Count;
        public int SelectedIndex => _viewMode == ScanListViewMode.List ?
            _grid.CurrentRow?.Index ?? -1 :
            (_thumbnailView.SelectedItems.Count > 0 ? _thumbnailView.SelectedItems[0].Index : -1);

        public ScanListViewMode ViewMode => _viewMode;

        public int ThumbnailSize
        {
            get => _thumbnailSize;
            set
            {
                _thumbnailSize = Math.Max(100, Math.Min(180, value));

                if (_viewMode == ScanListViewMode.Thumbnail)
                {
                    int thumbWidth = _thumbnailSize;
                    int thumbHeight = (int)(_thumbnailSize * 1.414);

                    // 确保不超过256限制
                    if (thumbHeight > 256)
                    {
                        thumbHeight = 256;
                        thumbWidth = (int)(256 / 1.414);
                    }

                    _thumbnailView.LargeImageList.ImageSize = new Size(thumbWidth, thumbHeight);
                    _thumbnailView.TileSize = new Size(thumbWidth + 20, thumbHeight + 30);
                    RefreshThumbnailView();
                }
            }
        }

        public List<int> SelectedIndices
        {
            get
            {
                var list = new List<int>();

                if (_viewMode == ScanListViewMode.List)
                {
                    // 列表模式支持多选
                    foreach (DataGridViewCell cell in _grid.SelectedCells)
                        if (!list.Contains(cell.RowIndex))
                            list.Add(cell.RowIndex);
                }
                else
                {
                    // 缩略图模式只返回单个选中项
                    if (_thumbnailView.SelectedItems.Count > 0)
                        list.Add(_thumbnailView.SelectedItems[0].Index);
                }

                return list;
            }
        }
       
        public ScanListManager(DataGridView grid, ListView thumbnailView, Panel container)
        {
            _grid = grid;
            _thumbnailView = thumbnailView;
            _container = container;

            InitializeGridView();
            InitializeThumbnailView();  // 直接初始化

            ShowListView();
        }


        private void InitializeGridView()
        {
            _grid.AllowUserToOrderColumns = false;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.None;
            _grid.RowTemplate.Height = 24;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;

            _grid.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _files.Count)
                    SelectedIndexChanged?.Invoke(e.RowIndex);
            };

            _grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up && _grid.CurrentRow != null)
                {
                    int idx = Math.Max(0, _grid.CurrentRow.Index - 1);
                    _grid.CurrentCell = _grid.Rows[idx].Cells[0];
                    SelectedIndexChanged?.Invoke(idx);
                }
                else if (e.KeyCode == Keys.Down && _grid.CurrentRow != null)
                {
                    int idx = Math.Min(_files.Count - 1, _grid.CurrentRow.Index + 1);
                    _grid.CurrentCell = _grid.Rows[idx].Cells[0];
                    SelectedIndexChanged?.Invoke(idx);
                }
            };
        }

        private void InitializeThumbnailView()
        {
            _thumbnailView.View = View.LargeIcon;
            _thumbnailView.LargeImageList = new ImageList();

            // 图片大小（保持A4比例）
            int thumbWidth = _thumbnailSize;
            int thumbHeight = (int)(_thumbnailSize * 1.414);

            if (thumbHeight > 256)
            {
                thumbHeight = 256;
                thumbWidth = (int)(256 / 1.414);
            }

            _thumbnailView.LargeImageList.ImageSize = new Size(thumbWidth, thumbHeight);
            _thumbnailView.TileSize = new Size(thumbWidth + 20, thumbHeight + 30);

            // 关键修改：只允许单选
            _thumbnailView.MultiSelect = false;
            _thumbnailView.AllowDrop = false;
            _thumbnailView.BackColor = Color.FromArgb(240, 240, 240);
            _thumbnailView.BorderStyle = BorderStyle.None;
            _thumbnailView.HideSelection = false;

            // 只需要处理选择变化事件
            _thumbnailView.ItemSelectionChanged += (s, e) =>
            {
                if (e.IsSelected && e.ItemIndex >= 0 && e.ItemIndex < _files.Count)
                    SelectedIndexChanged?.Invoke(e.ItemIndex);
            };
        }
        private void ShowListView()
        {
            _grid.Visible = true;
            _grid.Dock = DockStyle.Fill;
            _thumbnailView.Visible = false;
            _thumbnailView.Dock = DockStyle.None;
        }

        private void ShowThumbnailView()
        {
            _grid.Visible = false;
            _grid.Dock = DockStyle.None;
            _thumbnailView.Visible = true;
            _thumbnailView.Dock = DockStyle.Fill;
        }

        public void SetImageCache(ImageCacheManager cache)
        {
            _imageCache = cache;
        }

        public void SetViewMode(ScanListViewMode mode)
        {
            if (_viewMode != mode)
            {
                _viewMode = mode;
                int savedIndex = SelectedIndex;

                if (_viewMode == ScanListViewMode.List)
                {
                    ShowListView();
                    RefreshListView();
                }
                else
                {
                    ShowThumbnailView();
                    RefreshThumbnailView();
                }

                if (savedIndex >= 0 && savedIndex < _files.Count)
                {
                    SelectPage(savedIndex);
                }
            }
        }
        public void ToggleViewMode()
        {
            SetViewMode(_viewMode == ScanListViewMode.List ?
                ScanListViewMode.Thumbnail : ScanListViewMode.List);
        }

        public void IncreaseThumbnailSize()
        {
            ThumbnailSize += 20;
        }

        public void DecreaseThumbnailSize()
        {
            ThumbnailSize -= 20;
        }

        public void SetFiles(List<FileStatusInfo> files)
        {
            _files = files ?? new List<FileStatusInfo>();

            RefreshListView();

            // 只在缩略图模式下才加载缩略图
            if (_viewMode == ScanListViewMode.Thumbnail)
            {
                RefreshThumbnailView();
            }

            if (_files.Count > 0)
            {
                SelectPage(0);
            }
        }

        private void RefreshListView()
        {
            _grid.Rows.Clear();
            _grid.Columns.Clear();

            var colIndex = new DataGridViewTextBoxColumn { Name = "colIndex", HeaderText = "序号", Width = 40 };
            colIndex.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colIndex.FillWeight = 10;
            _grid.Columns.Add(colIndex);

            var colFilename = new DataGridViewTextBoxColumn { Name = "colFilename", HeaderText = "文件名", Width = 80 };
            colFilename.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colFilename.FillWeight = 30;
            _grid.Columns.Add(colFilename);

            var colSize = new DataGridViewTextBoxColumn { Name = "colSize", HeaderText = "大小", Width = 55 };
            colSize.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colSize.FillWeight = 15;
            _grid.Columns.Add(colSize);

            var colStatus = new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "状态", Width = 120 };
            colStatus.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colStatus.FillWeight = 45;
            _grid.Columns.Add(colStatus);

            foreach (DataGridViewColumn col in _grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            foreach (var f in _files)
            {
                int idx = _grid.Rows.Add(f.Index, f.Filename, f.SizeDisplay, GetStatusText(f.Status));
                var row = _grid.Rows[idx];
                row.DefaultCellStyle.BackColor = GetStatusColor(f.Status);
                if (f.IsDirty)
                    row.DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            }
        }

        private void RefreshThumbnailView()
        {
            _thumbnailView.Items.Clear();
            _thumbnailView.LargeImageList.Images.Clear();
            _thumbnailView.LargeImageList.ImageSize = new Size(_thumbnailSize, _thumbnailSize);

            foreach (var f in _files)
            {
                Image thumbnail = GetThumbnail(f);
                _thumbnailView.LargeImageList.Images.Add(thumbnail);

                var item = new ListViewItem
                {
                    Text = f.Filename,
                    ImageIndex = _thumbnailView.LargeImageList.Images.Count - 1,
                    ToolTipText = $"{f.Filename}\n状态: {GetStatusText(f.Status)}\n大小: {f.SizeDisplay}"
                };

                // 设置背景色
                item.BackColor = GetStatusColor(f.Status);

                _thumbnailView.Items.Add(item);
            }
        }

        private Image GetThumbnail(FileStatusInfo info)
        {
            if (info.LocalPath == null || !File.Exists(info.LocalPath))
            {
                return CreatePlaceholder();
            }

            try
            {
                // 从缓存获取
                if (_imageCache != null)
                {
                    var cached = _imageCache.GetImage(info.LocalPath);
                    if (cached != null)
                    {
                        var thumb = CreateThumbnail(cached);
                        cached.Dispose();  // ← 这里 Dispose 了，但 GetImage 内部用 using 已经关闭了流
                        return thumb;
                    }
                }

                // 从磁盘加载
                using (var fs = new FileStream(info.LocalPath, FileMode.Open, FileAccess.Read))
                using (var img = Image.FromStream(fs))
                {
                    return CreateThumbnail(img);
                }
            }
            catch
            {
                return CreatePlaceholder();
            }
        }

        private Image CreateThumbnail(Image source)
        {
            // 计算目标尺寸（保持A4比例）
            int targetWidth = _thumbnailSize;
            int targetHeight = (int)(_thumbnailSize * 1.414);

            // 确保不超过256限制
            if (targetHeight > 256)
            {
                targetHeight = 256;
                targetWidth = (int)(256 / 1.414);
            }

            // 计算缩放比例，保持原图比例
            float scaleX = (float)targetWidth / source.Width;
            float scaleY = (float)targetHeight / source.Height;
            float scale = Math.Min(scaleX, scaleY);

            int width = (int)(source.Width * scale);
            int height = (int)(source.Height * scale);

            // 创建A4比例的画布
            var thumb = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(thumb))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;

                // 居中绘制
                int x = (targetWidth - width) / 2;
                int y = (targetHeight - height) / 2;
                g.DrawImage(source, x, y, width, height);
            }
            return thumb;
        }
        private Image CreatePlaceholder()
        {
            int targetWidth = _thumbnailSize;
            int targetHeight = (int)(_thumbnailSize * 1.414);

            // 确保不超过256限制
            if (targetHeight > 256)
            {
                targetHeight = 256;
                targetWidth = (int)(256 / 1.414);
            }

            var bmp = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.LightGray);
                using (var pen = new Pen(Color.Gray, 1))
                {
                    g.DrawRectangle(pen, 10, 10, targetWidth - 20, targetHeight - 20);
                }
                using (var font = new Font("微软雅黑", 8))
                {
                    var text = "无图片";
                    var size = g.MeasureString(text, font);
                    g.DrawString(text, font, Brushes.Gray,
                        (targetWidth - size.Width) / 2,
                        (targetHeight - size.Height) / 2);
                }
            }
            return bmp;
        }

        public FileStatusInfo GetFile(int index)
        {
            return index >= 0 && index < _files.Count ? _files[index] : null;
        }

        public void SelectPage(int index)
        {
            if (index < 0 || index >= _files.Count) return;

            if (_viewMode == ScanListViewMode.List)
            {
                if (index < _grid.Rows.Count)
                {
                    _grid.ClearSelection();
                    _grid.Rows[index].Selected = true;
                    _grid.CurrentCell = _grid.Rows[index].Cells[0];
                }
            }
            else
            {
                if (index < _thumbnailView.Items.Count)
                {
                    _thumbnailView.Items[index].Selected = true;
                    _thumbnailView.Items[index].Focused = true;
                    _thumbnailView.EnsureVisible(index);
                }
            }

            SelectedIndexChanged?.Invoke(index);
        }

        public void NextPage()
        {
            int idx = SelectedIndex;
            if (idx < _files.Count - 1)
            {
                SelectPage(idx + 1);
            }
        }

        public void PrevPage()
        {
            int idx = SelectedIndex;
            if (idx > 0)
            {
                SelectPage(idx - 1);
            }
            else
            {
                PrevMaterialRequested?.Invoke();
            }
        }

        private string GetStatusText(FileStatus status)
        {
            switch (status)
            {
                case FileStatus.Synced: return "已同步";
                case FileStatus.LocalOnly: return "仅本地";
                case FileStatus.ServerOnly: return "仅服务器";
                case FileStatus.Modified: return "未保存";
                case FileStatus.LocalUpdated: return "有更新(未上传)";
                case FileStatus.NotScanned: return "未扫描";
                default: return "";
            }
        }

        private Color GetStatusColor(FileStatus status) => status switch
        {
            FileStatus.LocalUpdated => Color.Brown,
            FileStatus.Modified => Color.Orange,
            FileStatus.LocalOnly => Color.DodgerBlue,
            FileStatus.ServerOnly => Color.BlueViolet,
            FileStatus.NotScanned => Color.Tomato,
            FileStatus.Synced => Color.ForestGreen,
            _ => Color.SlateGray
        };

        public void RefreshFileStatus(int index, ImageCacheManager imageCache, string localPath)
        {
            if (index < 0 || index >= _files.Count) return;
            var f = _files[index];
            if (f == null) return;

            f.IsDirty = imageCache?.IsDirty(localPath) ?? false;
            if (f.IsDirty)
            {
                f.Status = FileStatus.Modified;
            }

            // 更新列表视图
            if (_viewMode == ScanListViewMode.List && _grid.Rows.Count > index)
            {
                var row = _grid.Rows[index];
                row.Cells[3].Value = GetStatusText(f.Status);
                row.DefaultCellStyle.BackColor = GetStatusColor(f.Status);
                row.DefaultCellStyle.Font = f.IsDirty ? new Font(_grid.Font, FontStyle.Bold) : new Font(_grid.Font, FontStyle.Regular);
            }
            // 更新缩略图视图
            else if (_viewMode == ScanListViewMode.Thumbnail && _thumbnailView.Items.Count > index)
            {
                var item = _thumbnailView.Items[index];
                item.BackColor = GetStatusColor(f.Status);
                item.ToolTipText = $"{f.Filename}\n状态: {GetStatusText(f.Status)}\n大小: {f.SizeDisplay}";
            }
        }
    }
}