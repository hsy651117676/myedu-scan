// Controls/ScanListManager.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ScanTool.Models;
using ScanTool.Services;

namespace ScanTool.Controls
{
    public class ScanListManager
    {
        private readonly DataGridView _grid;
        private List<FileStatusInfo> _files = new List<FileStatusInfo>();

        public event Action<int> SelectedIndexChanged;
        public int Count => _files.Count;
        public int SelectedIndex => _grid.CurrentRow?.Index ?? -1;
        public List<int> SelectedIndices
        {
            get
            {
                var list = new List<int>();
                foreach (DataGridViewCell cell in _grid.SelectedCells)
                    if (!list.Contains(cell.RowIndex))
                        list.Add(cell.RowIndex);
                return list;
            }
        }

        public ScanListManager(DataGridView grid)
        {
            _grid = grid;
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

        public void SetFiles(List<FileStatusInfo> files)
        {
            _files = files ?? new List<FileStatusInfo>();
            _grid.Rows.Clear();
            foreach (var f in _files)
            {
                int idx = _grid.Rows.Add(f.Index, f.Filename, f.SizeDisplay, GetStatusText(f.Status));
                var row = _grid.Rows[idx];
                row.DefaultCellStyle.BackColor = GetStatusColor(f.Status);
                if (f.IsDirty)
                    row.DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            }
            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
        }

        public FileStatusInfo GetFile(int index)
        {
            return index >= 0 && index < _files.Count ? _files[index] : null;
        }

        public void SelectPage(int index)
        {
            if (index >= 0 && index < _grid.Rows.Count)
            {
                _grid.ClearSelection();
                _grid.Rows[index].Selected = true;
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
                SelectedIndexChanged?.Invoke(index);
            }
        }

        public void NextPage()
        {
            if (_grid.CurrentRow != null && _grid.CurrentRow.Index < _grid.Rows.Count - 1)
            {
                int idx = _grid.CurrentRow.Index + 1;
                _grid.CurrentCell = _grid.Rows[idx].Cells[0];
                SelectedIndexChanged?.Invoke(idx);
            }
        }

        public void PrevPage()
        {
            if (_grid.CurrentRow != null && _grid.CurrentRow.Index > 0)
            {
                int idx = _grid.CurrentRow.Index - 1;
                _grid.CurrentCell = _grid.Rows[idx].Cells[0];
                SelectedIndexChanged?.Invoke(idx);
            }
        }

        private string GetStatusText(FileStatus status) => status switch
        {
            FileStatus.Synced => "已同步",
            FileStatus.LocalOnly => "仅本地",
            FileStatus.ServerOnly => "仅服务器",
            FileStatus.Modified => "未保存",
            FileStatus.LocalUpdated => "有更新(未上传)",
            FileStatus.NotScanned => "未扫描",
            _ => ""
        };

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
            var row = _grid.Rows[index];
            row.Cells[3].Value = GetStatusText(f.Status);
            row.DefaultCellStyle.BackColor = GetStatusColor(f.Status);
            row.DefaultCellStyle.Font = f.IsDirty ? new Font(_grid.Font, FontStyle.Bold) : new Font(_grid.Font, FontStyle.Regular);
        }
    }
}