// Services/RepairProfileManager.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScanTool.Models;

namespace ScanTool.Services
{
    public class RepairProfileManager
    {
        private readonly string _configPath;
        private List<RepairProfile> _customProfiles = new List<RepairProfile>();
        public List<RepairProfile> CustomProfiles => _customProfiles;

        public RepairProfileManager(string configDir)
        {
            Directory.CreateDirectory(configDir);
            _configPath = Path.Combine(configDir, "repair_profiles.ini");
            Load();
        }

        private void Load()
        {
            if (File.Exists(_configPath))
            {
                _customProfiles.Clear();
                foreach (var line in File.ReadAllLines(_configPath))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 9)
                    {
                        _customProfiles.Add(new RepairProfile
                        {
                            Name = parts[0],
                            AutoDeskew = bool.Parse(parts[1]),
                            Denoise = bool.Parse(parts[2]),
                            RemoveBlackBorder = bool.Parse(parts[3]),
                            Grayscale = bool.Parse(parts[4]),
                            Brightness = bool.Parse(parts[5]),
                            Contrast = bool.Parse(parts[6]),
                            BrightnessValue = int.Parse(parts[7]),
                            ContrastValue = double.Parse(parts[8])
                        });
                    }
                }
            }
        }

        public void Save()
        {
            File.WriteAllLines(_configPath, _customProfiles.Select(p =>
                $"{p.Name}|{p.AutoDeskew}|{p.Denoise}|{p.RemoveBlackBorder}|{p.Grayscale}|{p.Brightness}|{p.Contrast}|{p.BrightnessValue}|{p.ContrastValue}"));
        }

        public void AddOrUpdate(int index, RepairProfile profile)
        {
            if (index >= 0 && index < _customProfiles.Count)
                _customProfiles[index] = profile;
            else if (index >= _customProfiles.Count && _customProfiles.Count < 7)
                _customProfiles.Add(profile);
            Save();
        }

        public RepairProfile GetCustom(int index)
        {
            return index >= 0 && index < _customProfiles.Count ? _customProfiles[index] : null;
        }
    }
}