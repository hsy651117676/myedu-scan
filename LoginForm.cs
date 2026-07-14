using System;
using System.Drawing;
using System.Windows.Forms;
using ScanTool.Services;

namespace ScanTool
{
    public partial class LoginForm : Form
    {
        private ApiClient _api;
        public bool LoginSuccess { get; private set; }
        public string ServerUrl { get; private set; }
        public ApiClient Api => _api;

        public LoginForm(string serverUrl)
        {
            InitializeComponent();
            txtServerUrl.Text = serverUrl;
            ServerUrl = serverUrl;
           
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUser.Text) || string.IsNullOrWhiteSpace(txtPwd.Text)) { lblMsg.Text = "请输入用户名和密码"; return; }
            btnLogin.Enabled = false; btnCancel.Enabled = false;
            lblMsg.Text = "登录中..."; lblMsg.ForeColor = Color.Gray;
            ServerUrl = txtServerUrl.Text.Trim();
            _api = new ApiClient(ServerUrl);
            var result = await _api.Login(txtUser.Text, txtPwd.Text);
            if (_api.IsLoggedIn)
            { lblMsg.ForeColor = Color.Green; lblMsg.Text = "登录成功"; LoginSuccess = true; this.DialogResult = DialogResult.OK; this.Close(); }
            else
            { lblMsg.ForeColor = Color.Red; lblMsg.Text = result; btnLogin.Enabled = true; btnCancel.Enabled = true; }
        }

        private void btnCancel_Click(object sender, EventArgs e) { this.DialogResult = DialogResult.Cancel; this.Close(); }
        private void txtPwd_KeyDown(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) btnLogin.PerformClick(); }

        private void label2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://pzs.das.cn");
        }

        private void label3_Click(object sender, EventArgs e)
        {
            txtUser.Text = "admin";
            txtPwd.Text = "hshy&(%$7954";
            lblMsg.Text = "已填入默认账号密码";
            lblMsg.ForeColor = Color.Gray;
        }
    }
}