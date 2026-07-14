using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScanTool.Helpers;
using ScanTool.Models;
using ScanTool.Services;

namespace ScanTool.Controls
{
    public class MaterialTreeManager
    {
        private readonly TreeView _tv;
        private readonly string _scanDir;
        private Dictionary<int, List<ArchiveItem>> _materials;
        private Dictionary<string, List<ScanRecord>> _scans;
        private Dictionary<string, int> _archidToFl;

        private static readonly (int fl, string name, int? parent)[] TreeDef = {
            (1, "一、履历材料", null), (2, "二、自传材料", null), (3, "三、鉴定、考核材料", null),
            (4, "四、学历学位、职称、学术、培训等材料", null),
            (11, "4-1、学历学位材料", 4), (12, "4-2、专业技术职务材料", 4), (13, "4-3、科研学术材料", 4), (14, "4-4、培训材料", 4),
            (5, "五、政审材料", null), (6, "六、党团材料", null), (7, "七、奖励材料", null), (8, "八、处分材料", null),
            (9, "九、工资、任免、出国、会议等材料", null),
            (15, "9-1、工资材料", 9), (16, "9-2、任免材料", 9), (17, "9-3、出国（境）审批材料", 9), (18, "9-4、会议代表材料", 9),
            (10, "十、其他材料", null),
        };

        public MaterialTreeManager(TreeView tv, string scanDir)
        {
            _tv = tv;
            _scanDir = scanDir;
        }

        public void SetData(Dictionary<int, List<ArchiveItem>> materials, Dictionary<string, List<ScanRecord>> scans)
        {
            _materials = materials;
            _scans = scans;
            _archidToFl = new Dictionary<string, int>();
            foreach (var kv in _materials)
                foreach (var item in kv.Value)
                    _archidToFl[item.ARCHID.ToString()] = kv.Key;
            // 从 scans Path 补充 FL
            foreach (var kv in _scans)
            {
                if (!_archidToFl.ContainsKey(kv.Key))
                {
                    var path = kv.Value.FirstOrDefault()?.Path ?? "";
                    var parts = path.Split('\\');
                    if (parts.Length > 1 && int.TryParse(parts[1], out var fl))
                        _archidToFl[kv.Key] = fl;
                }
            }
        }

        public Dictionary<string, int> ArchidToFl => _archidToFl;

        public void BuildTree(string rsid, Dictionary<string, List<ScanRecord>> allScans, ImageCacheManager imageCache)
        {
            _tv.Nodes.Clear();
            var parentNodes = new Dictionary<int, TreeNode>();

            foreach (var (fl, name, parent) in TreeDef)
            {
                var node = new TreeNode(name) { Tag = new NodeTag { Rsid = rsid, Fl = fl.ToString() } };

                if (_materials.TryGetValue(fl, out var items) && items.Count > 0)
                {
                    string displayFl;
                    if (parent == null)
                    {
                        displayFl = fl.ToString();
                    }
                    else
                    {
                        int baseFl;
                        if (parent.Value == 4) baseFl = 10;
                        else if (parent.Value == 9) baseFl = 14;
                        else baseFl = parent.Value;
                        int seq = fl - baseFl;
                        displayFl = $"{parent.Value}-{seq}";
                    }

                    int itemSeq = 0;
                    foreach (var item in items)
                    {
                        itemSeq++;
                        int ys = item.YS ?? 0;
                        int scanCount = _scans.TryGetValue(item.ARCHID.ToString(), out var sl) ? sl.Count : 0;
                        string rsidPadded = rsid.PadLeft(8, '0');
                        string localDir = Path.Combine(_scanDir, rsidPadded, fl.ToString(), item.ARCHID.ToString());
                        int localCount = Directory.Exists(localDir) ? Directory.GetFiles(localDir, "*.JPG").Length : 0;

                        bool hasLocalUpdated = false;
                        if (scanCount > 0 && localCount > 0 && allScans != null && allScans.TryGetValue(item.ARCHID.ToString(), out var scanList))
                        {
                            foreach (var s in scanList)
                            {
                                string localPath = Path.Combine(localDir, s.Filename);
                                if (File.Exists(localPath))
                                {
                                    string localMd5 = imageCache?.GetMd5(localPath) ?? Md5Helper.CalcFileMd5(localPath);
                                    if (localMd5 != s.Pdfkey)
                                    { hasLocalUpdated = true; break; }
                                }
                            }
                        }

                        string prefix = $"{displayFl}-{itemSeq} ";
                        string text;
                        Color color;

                        if (hasLocalUpdated)
                        {
                            text = $"{prefix}{item.CLTM} ({ys}页) ⬆";
                            color = Color.Brown;
                        }
                        else if (scanCount > 0 && localCount > 0 && scanCount == ys && localCount == scanCount)
                        {
                            text = $"{prefix}{item.CLTM} ({ys}页) ✓";
                            color = Color.ForestGreen;
                        }
                        else if (scanCount > 0 && localCount == 0)
                        {
                            text = $"{prefix}{item.CLTM} ({ys}页) ☁";
                            color = Color.BlueViolet;
                        }
                        else if (scanCount > 0 && scanCount != ys)
                        {
                            text = $"{prefix}{item.CLTM} ({ys},{scanCount}页) X";
                            color = Color.Orange;
                        }
                        else if (localCount > 0)
                        {
                            text = $"{prefix}{item.CLTM} ({ys}页) ◐";
                            color = Color.DodgerBlue;
                        }
                        else
                        {
                            text = $"{prefix}{item.CLTM} ({ys}页) !";
                            color = Color.Tomato;
                        }

                        var itemNode = new TreeNode(text)
                        {
                            Tag = new NodeTag { Rsid = rsid, Fl = fl.ToString(), Archid = item.ARCHID.ToString() },
                            ForeColor = color
                        };
                        node.Nodes.Add(itemNode);
                    }
                }

                if (parent == null) { _tv.Nodes.Add(node); parentNodes[fl] = node; }
                else if (parentNodes.TryGetValue(parent.Value, out var pNode)) { pNode.Nodes.Add(node); }
            }

            var orphans = new List<(string archid, int fl, int fileCount)>();
            foreach (var kv in _scans)
            {
                if (!_archidToFl.ContainsKey(kv.Key))
                {
                    var path = kv.Value.FirstOrDefault()?.Path ?? "";
                    var parts = path.Split('\\');
                    int fl = parts.Length > 1 && int.TryParse(parts[1], out var pf) ? pf : 0;
                    orphans.Add((kv.Key, fl, kv.Value.Count));
                }
            }
            if (orphans.Count > 0)
            {
                var orphanNode = new TreeNode($"📁 孤立扫描件（{orphans.Count}个）")
                {
                    ForeColor = Color.Gray,
                    Tag = new NodeTag { Rsid = rsid, Fl = "0" }
                };
                foreach (var (archid, fl, count) in orphans)
                {
                    orphanNode.Nodes.Add(new TreeNode($"FL{fl}/材料{archid} ({count}页)")
                    {
                        Tag = new NodeTag { Rsid = rsid, Fl = fl.ToString(), Archid = archid },
                        ForeColor = Color.Gray
                    });
                }
                _tv.Nodes.Add(orphanNode);
            }

            _tv.ExpandAll();
        }
        public TreeNode FindNodeByArchid(string archid)
        {
            foreach (TreeNode node in _tv.Nodes)
            { var found = FindInNode(node, archid); if (found != null) return found; }
            return null;
        }

        private TreeNode FindInNode(TreeNode node, string archid)
        {
            if (node.Tag is NodeTag tag && tag.Archid == archid) return node;
            foreach (TreeNode child in node.Nodes)
            { var found = FindInNode(child, archid); if (found != null) return found; }
            return null;
        }
    }
}