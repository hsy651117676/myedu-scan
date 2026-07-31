// Services/UserSettings.cs
using System.Collections.Generic;

namespace ScanTool.Services
{
    public static class UserSettings
    {
        private static readonly Dictionary<string, object> _cache = new Dictionary<string, object>
        {
            {"EraserSize", 40},
            {"EraserShape", "Circle"}
        };

        public static T Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var val) && val is T tVal)
                return tVal;
            return default;
        }

        public static void Set<T>(string key, T value)
        {
            _cache[key] = value;
        }
    }
}