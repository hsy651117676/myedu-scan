using System;
using System.IO;
using System.Windows.Forms;
using ScanTool.Services;
namespace ScanTool
{
    static class Program
    {
        public static ApiClient Api { get; private set; }       
        public static CryptoHelper Crypto { get; private set; } = new CryptoHelper("3yj8jbvx");
        [STAThread]
        static void Main()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string serverUrl = "https://192.168.18.100";
            var configPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "ScanTool", "config.ini");
            if (System.IO.File.Exists(configPath))
            {
                foreach (var line in System.IO.File.ReadAllLines(configPath))
                { if (line.StartsWith("ServerUrl=")) serverUrl = line.Substring(10); }
            }

            using (var loginForm = new LoginForm(serverUrl))
            {
                if (loginForm.ShowDialog() != DialogResult.OK || !loginForm.LoginSuccess) return;
                Api = loginForm.Api;
            }
            Application.Run(new MainForm(Api));
        }
    }
}