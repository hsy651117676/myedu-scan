// Services/BaseTransferManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScanTool.Services
{
    public abstract class BaseTransferManager
    {
        protected readonly ApiClient _api;
        protected readonly string _scanDir;
        protected readonly TreeView _tv;
        protected readonly ToolStripProgressBar _progress;
        protected readonly ToolStripTextBox _lblStatus;
        protected readonly List<string> _failedFiles = new List<string>();
        protected readonly SemaphoreSlim _semaphore;

        public List<string> FailedFiles => _failedFiles;

        protected BaseTransferManager(ApiClient api, string scanDir, TreeView tv, ToolStripProgressBar progress, ToolStripTextBox lblStatus, int maxConcurrency)
        {
            _api = api;
            _scanDir = scanDir;
            _tv = tv;
            _progress = progress;
            _lblStatus = lblStatus;
            _semaphore = new SemaphoreSlim(maxConcurrency);
        }

        protected async Task RunParallel<T>(List<T> items, Func<T, Task<(bool skipped, bool success)>> worker, string actionName)
        {
            _failedFiles.Clear();
            var sw = Stopwatch.StartNew();
            int total = items.Count;
            if (total == 0) { UpdateStatus("无文件需要处理"); return; }

            if (_progress is ToolStripProgressBar tspb)
            {
                tspb.Visible = true;
                tspb.Maximum = total;
                tspb.Value = 0;
            }

            int success = 0, skipped = 0, current = 0;
            var tasks = new List<Task>();

            UpdateStatus($"开始{actionName} {total} 个文件...");

            foreach (var item in items)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await worker(item);
                    if (result.skipped) Interlocked.Increment(ref skipped);
                    if (result.success) Interlocked.Increment(ref success);
                    var done = Interlocked.Increment(ref current);
                    UpdateProgress(done, total, skipped, actionName);
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            if (_progress is ToolStripProgressBar tspb2) tspb2.Visible = false;
            UpdateStatus($"{actionName}完成: 新{success}，跳过{skipped}，失败{_failedFiles.Count}，耗时{sw.Elapsed.TotalSeconds:F1}s");
        }

        private void UpdateProgress(int done, int total, int skipped, string action)
        {
            if (_progress != null)
            {
                if (_progress.Owner.InvokeRequired)
                    _progress.Owner.Invoke(new Action(() =>
                    {
                        _progress.Maximum = Math.Max(1, total);
                        _progress.Value = Math.Min(done, _progress.Maximum);
                    }));
            }
        }

        private void UpdateStatus(string text)
        {
            if (_lblStatus != null)
            {
                if (_lblStatus.Owner.InvokeRequired)
                    _lblStatus.Owner.Invoke(new Action(() => _lblStatus.Text = text));
            }
        }
    }
}