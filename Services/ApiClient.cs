// Services/ApiClient.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ScanTool.Models;

namespace ScanTool.Services
{
    public class ApiClient
    {
        private string _baseUrl;
        private readonly HttpClient _client;
        private CookieContainer _cookies = new CookieContainer();
        public bool IsLoggedIn { get; private set; }
        public string BaseUrl => _baseUrl;

        public ApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                MaxConnectionsPerServer = 20,
                UseCookies = true
            };
            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10),
                BaseAddress = new Uri(_baseUrl + "/")
            };
            _client.DefaultRequestHeaders.ConnectionClose = false;
        }

        public async Task<string> Login(string username, string password)
        {
            try
            {
                var data = JsonConvert.SerializeObject(new { username, password });
                var content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{_baseUrl}/api/login/", content);
                var body = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(body);
                IsLoggedIn = (int)result.code == 0;
                return IsLoggedIn ? "登录成功" : (string)result.msg ?? "登录失败";
            }
            catch (Exception ex) { return $"连接失败: {ex.Message}"; }
        }

        public async Task<List<TreeNodeData>> GetOrgRoot()
        {
            var json = await _client.GetStringAsync($"{_baseUrl}/components/tree/");
            var resp = JsonConvert.DeserializeObject<dynamic>(json);
            var list = new List<TreeNodeData>();
            if ((int)resp.code == 0)
                foreach (var item in resp.data)
                    list.Add(new TreeNodeData { TID = (int)item.TID, TNAME = (string)item.TNAME, PID = (int)item.PID, RSID = (int)item.RSID, DABH = (string)item.DABH });
            return list;
        }

        public async Task<List<TreeNodeData>> GetOrgChildren(int tid)
        {
            var json = await _client.GetStringAsync($"{_baseUrl}/components/tree-children/?tid={tid}");
            var resp = JsonConvert.DeserializeObject<dynamic>(json);
            var list = new List<TreeNodeData>();
            if ((int)resp.code == 0)
                foreach (var item in resp.data)
                    list.Add(new TreeNodeData { TID = (int)item.TID, TNAME = (string)item.TNAME, PID = (int)item.PID, RSID = (int)item.RSID, DABH = (string)item.DABH });
            return list;
        }

        public async Task<List<PersonInfo>> SearchPersons(string keyword, int page = 1, int pageSize = 200)
        {
            var url = $"{_baseUrl}/components/person-search/?keyword={Uri.EscapeDataString(keyword)}&page={page}&pageSize={pageSize}";
            var json = await _client.GetStringAsync(url);
            var resp = JsonConvert.DeserializeObject<dynamic>(json);
            var list = new List<PersonInfo>();
            if ((int)resp.code == 0)
                foreach (var item in resp.data)
                    list.Add(new PersonInfo { Rsid = item.rsid?.ToString(), Name = item.displayName?.ToString(), UnitName = item.unitName?.ToString(), ArchiveNo = item.archiveNo?.ToString() });
            return list;
        }

        public async Task<ArchiveTreeAllResult> GetAllMaterials(string rsid)
        {
            var json = await _client.GetStringAsync($"{_baseUrl}/components/archive-tree-all/?rsid={rsid}");

            if (json.Contains("no_permission") || json.Contains("没有权限"))
                throw new UnauthorizedAccessException("没有权限访问该档案");

            var resp = JsonConvert.DeserializeObject<ArchiveTreeAllResult>(json);
            var fullJson = JsonConvert.DeserializeObject<dynamic>(json);
            resp.Materials = new Dictionary<int, List<ArchiveItem>>();
            if ((int)fullJson.code == 0)
            {
                foreach (var kv in fullJson.materials)
                {
                    int fl = int.Parse(kv.Name);
                    resp.Materials[fl] = JsonConvert.DeserializeObject<List<ArchiveItem>>(kv.Value.ToString());
                }
            }
            return resp;
        }

        public async Task<(bool success, string msg)> UploadScan(string rsid, int fl, string archid, string filename, byte[] encryptedData, string pdfkey)
        {
            var url = $"{_baseUrl}/archives/image/api/upload-scan/";
            try
            {
                using (var form = new MultipartFormDataContent())
                {
                    form.Add(new StringContent("YS"), "image_type");
                    form.Add(new StringContent(rsid), "rsid");
                    form.Add(new StringContent(fl.ToString()), "fl");
                    form.Add(new StringContent(archid), "archid");
                    form.Add(new StringContent(filename), "filename");
                    form.Add(new StringContent(pdfkey), "pdfkey");
                    form.Add(new ByteArrayContent(encryptedData), "file", filename);

                    var resp = await _client.PostAsync(url, form);
                    var body = await resp.Content.ReadAsStringAsync();

                    if (!resp.IsSuccessStatusCode)
                        return (false, $"HTTP {(int)resp.StatusCode}\n{body}");

                    var result = JsonConvert.DeserializeObject<ApiResponse<object>>(body);
                    if (result == null)
                        return (false, $"JSON解析失败\n{body}");

                    return (result.Code == 0, result.Msg ?? "");
                }
            }
            catch (Exception ex)
            {
                return (false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        public async Task CleanOrphanRecords(string rsid)
        {
            var url = $"{_baseUrl}/archives/image/clean-orphans/?rsid={rsid}";
            await _client.GetStringAsync(url);
        }

        public async Task<bool> DeleteScan(string rsid, string archid, string filename)
        {
            var url = $"{_baseUrl}/archives/image/api/delete-scan/";
            var data = JsonConvert.SerializeObject(new { rsid, archid, filename });
            var content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync(url, content);
            return resp.IsSuccessStatusCode;
        }

        public async Task CleanOrphans(string rsid)
        {
            var url = $"{_baseUrl}/archives/image/clean-orphans/?rsid={rsid}";
            await _client.GetStringAsync(url);
        }

        public async Task<(bool success, string msg)> UpdatePageCount(string rsid, string archid, int newPages)
        {
            try
            {
                var url = $"{_baseUrl}/archives/image/api/update-page-count/";
                var data = JsonConvert.SerializeObject(new { rsid, archid, ys = newPages });
                var content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");
                var resp = await _client.PostAsync(url, content);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return (false, $"HTTP {(int)resp.StatusCode}\n{body}");

                var result = JsonConvert.DeserializeObject<ApiResponse<object>>(body);
                return (result?.Code == 0, result?.Msg ?? "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}