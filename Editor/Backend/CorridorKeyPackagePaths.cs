#nullable enable
using System.IO;
using UnityEditor;

namespace CorridorKey.Editor.Backend
{
    /// <summary>
    /// Resolves files shipped inside this UPM package (e.g. <c>unity_bridge.py</c>).
    /// </summary>
    public static class CorridorKeyPackagePaths
    {
        /// <summary>Returns absolute path to <c>Editor/Backend/Python/unity_bridge.py</c>, or null if not found.</summary>
        public static string? GetUnityBridgeScriptPath()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(CorridorKeyPackagePaths).Assembly);
            if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
            {
                var candidate = Path.Combine(
                    info.resolvedPath,
                    "Editor",
                    "Backend",
                    "Python",
                    "unity_bridge.py");
                if (File.Exists(candidate))
                    return candidate;
            }

            var guids = AssetDatabase.FindAssets("unity_bridge");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("unity_bridge.py") && File.Exists(path))
                    return Path.GetFullPath(path);
            }

            return null;
        }
    }
}
