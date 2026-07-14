// Services/CropPresetManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScanTool.Services
{
    public class CropPreset
    {
        public string Name { get; set; }
        public string ScannerName { get; set; } = "";
        public int Dpi { get; set; } = 300;
        public string Paper { get; set; } = "A4";
        public string Orientation { get; set; } = "纵向";
        public int Rotation { get; set; } = 0;
        public string ColorMode { get; set; } = "彩色";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public override string ToString() => $"{Name} ({Paper} {Dpi}DPI {ColorMode} {Rotation}°)";
    }

    public class CropPresetManager
    {
        private readonly string _configPath;
        private readonly string _activePath;
        private List<CropPreset> _presets = new List<CropPreset>();
        private CropPreset _activePreset;

        public List<CropPreset> Presets => _presets;
        public CropPreset ActivePreset => _activePreset;
        public bool HasActive => _activePreset != null;

        public CropPresetManager(string configDir)
        {
            Directory.CreateDirectory(configDir);
            _configPath = Path.Combine(configDir, "crop_presets.ini");
            _activePath = Path.Combine(configDir, "active_preset.ini");
            Load();
        }

        private void Load()
        {
            if (File.Exists(_configPath))
            {
                _presets.Clear();
                foreach (var line in File.ReadAllLines(_configPath))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 11)
                    {
                        _presets.Add(new CropPreset
                        {
                            Name = parts[0],
                            ScannerName = parts[1],
                            Dpi = int.Parse(parts[2]),
                            Paper = parts[3],
                            Orientation = parts[4],
                            Rotation = int.Parse(parts[5]),
                            ColorMode = parts[6],
                            X = int.Parse(parts[7]),
                            Y = int.Parse(parts[8]),
                            Width = int.Parse(parts[9]),
                            Height = int.Parse(parts[10])
                        });
                    }
                }
            }
            if (File.Exists(_activePath))
            {
                var name = File.ReadAllText(_activePath).Trim();
                _activePreset = _presets.FirstOrDefault(p => p.Name == name);
            }
        }

        private void Save()
        {
            File.WriteAllLines(_configPath, _presets.Select(p =>
                $"{p.Name}|{p.ScannerName}|{p.Dpi}|{p.Paper}|{p.Orientation}|{p.Rotation}|{p.ColorMode}|{p.X}|{p.Y}|{p.Width}|{p.Height}"));
        }

        public void AddOrUpdate(string name, string scannerName, int dpi, string paper, string orientation,
            int rotation, string colorMode, int x, int y, int w, int h)
        {
            var existing = _presets.FirstOrDefault(p => p.Name == name);
            if (existing != null)
            {
                existing.ScannerName = scannerName;
                existing.Dpi = dpi; existing.Paper = paper; existing.Orientation = orientation;
                existing.Rotation = rotation; existing.ColorMode = colorMode;
                existing.X = x; existing.Y = y; existing.Width = w; existing.Height = h;
            }
            else
            {
                _presets.Add(new CropPreset
                {
                    Name = name,
                    ScannerName = scannerName,
                    Dpi = dpi,
                    Paper = paper,
                    Orientation = orientation,
                    Rotation = rotation,
                    ColorMode = colorMode,
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h
                });
            }
            Save();
        }

        public void Delete(string name)
        {
            _presets.RemoveAll(p => p.Name == name);
            if (_activePreset?.Name == name) _activePreset = null;
            Save();
            if (File.Exists(_activePath)) File.Delete(_activePath);
        }

        public void Activate(string name)
        {
            _activePreset = _presets.FirstOrDefault(p => p.Name == name);
            File.WriteAllText(_activePath, _activePreset?.Name ?? "");
        }

        public void Deactivate()
        {
            _activePreset = null;
            if (File.Exists(_activePath)) File.Delete(_activePath);
        }
    }
}