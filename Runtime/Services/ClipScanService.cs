using System.Collections.Generic;
using System.IO;

namespace CorridorKey.Services
{
    /// <summary>
    /// EZ parity: <c>backend/clip_scanner.py</c> — optional C# enumeration before the bridge is wired.
    /// </summary>
    public static class ClipScanService
    {
        public static IReadOnlyList<string> ListChildDirectoryNames(string parentPath)
        {
            if (!Directory.Exists(parentPath))
                return System.Array.Empty<string>();

            var list = new List<string>();
            foreach (var dir in Directory.GetDirectories(parentPath))
                list.Add(Path.GetFileName(dir));
            list.Sort();
            return list;
        }
    }
}
