using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScanTool.Helpers
{
    public class NaturalStringComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);
        public int Compare(string x, string y) => StrCmpLogicalW(x, y);
    }
}