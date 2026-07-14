// Services/BatchProcessor.cs
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScanTool.Services
{
    public class BatchProcessor
    {
        private readonly Action<string> _executeAction;
        private readonly Func<int> _getCurrentPageIndex;
        private readonly Func<int> _getTotalPages;
        private readonly Func<bool> _hasNextMaterial;
        private bool _cancelled;

        public BatchProcessor(
            Action<string> executeAction,
            Func<int> getCurrentPageIndex,
            Func<int> getTotalPages,
            Func<bool> hasNextMaterial)
        {
            _executeAction = executeAction;
            _getCurrentPageIndex = getCurrentPageIndex;
            _getTotalPages = getTotalPages;
            _hasNextMaterial = hasNextMaterial;
        }

        public void Cancel() => _cancelled = true;

        public async Task ProcessCategory()
        {
            _cancelled = false;

            while (!_cancelled)
            {
                int total = _getTotalPages();
                int current = _getCurrentPageIndex();

                _executeAction("brightness_up");
                _executeAction("brightness_up");
                _executeAction("contrast_up");
                _executeAction("auto_deskew");
                await Task.Delay(200);  // 停一下让用户看清效果

                if (current >= total - 1)
                {
                    if (_hasNextMaterial())
                        _executeAction("next_page");
                    else
                        break;
                }
                else
                {
                    _executeAction("next_page");
                }
            }

            if (!_cancelled)
                MessageBox.Show("自动处理完成！\n\n符合要求的记得全部保存，\n不符合的请手动修改，按Z恢复原始图像。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}