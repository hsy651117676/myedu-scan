using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TwainDotNet;
using TwainDotNet.TwainNative;
using TwainDotNet.WinFroms;

namespace ScanTool.Services
{
    public class TwainService : IDisposable
    {
        private Twain _twain;
        private Bitmap _lastImage;
        private bool _scanComplete;
        private Form _dummyForm;
        private IWindowsMessageHook _msgHook;
        private bool _initialized;

        public bool Initialized => _initialized;

        public TwainService()
        {
            _dummyForm = new Form { Width = 0, Height = 0 };
            _dummyForm.Show();
            _dummyForm.Hide();
            _msgHook = new WinFormsWindowMessageHook(_dummyForm);
        }

        public List<string> GetScanners()
        {
            var list = new List<string>();
            try
            {
                var twain = new Twain(_msgHook);
                var sources = twain.SourceNames;
                if (sources != null) list.AddRange(sources);
            }
            catch { }
            return list;
        }

        public bool Connect(string name, bool forceDialog = false)
        {
            try
            {
                if (_twain != null && _initialized)
                    return true;

                _twain = new Twain(_msgHook);
                _twain.ScanningComplete += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[Twain] ScanningComplete触发");
                    _scanComplete = true;
                };
                _twain.TransferImage += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Twain] TransferImage触发, Image={e.Image != null}");
                    if (e.Image != null) _lastImage = new Bitmap(e.Image);
                };

                if (!string.IsNullOrEmpty(name))
                {
                    try { _twain.SelectSource(name); }
                    catch { _twain.SelectSource(); }
                }
                else
                {
                    _twain.SelectSource();
                }

                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Twain] 连接失败: {ex.Message}");
                return false;
            }
        }

        public Bitmap Scan(int dpi = 300, string colorMode = "彩色")
        {
            _scanComplete = false;
            _lastImage = null;
            try
            {
                if (_twain == null || !_initialized) return null;

                var settings = new ScanSettings
                {
                    ShowTwainUI = false,
                    ShouldTransferAllPages = false,
                    Resolution = new ResolutionSettings
                    {
                        Dpi = dpi,
                        ColourSetting = colorMode switch
                        {
                            "彩色" => ColourSetting.Colour,
                            "灰度" => ColourSetting.GreyScale,
                            "黑白" => ColourSetting.BlackAndWhite,
                            _ => ColourSetting.Colour
                        }
                    }
                };

                System.Diagnostics.Debug.WriteLine($"[Twain] 开始扫描 DPI={dpi} ColorMode={colorMode}");
                _twain.StartScanning(settings);

                var start = DateTime.Now;
                while (!_scanComplete && (DateTime.Now - start).TotalSeconds < 60)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
                System.Diagnostics.Debug.WriteLine($"[Twain] 扫描完成, _scanComplete={_scanComplete}, Image={_lastImage != null}");
                return _lastImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Twain] 异常: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            try { _twain = null; } catch { }
            try { _dummyForm?.Close(); _dummyForm = null; } catch { }
        }
    }
}