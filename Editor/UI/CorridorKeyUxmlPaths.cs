#nullable enable
using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Locates <c>CorridorKeyWindow.uxml</c> under the package (works with any Packages folder name).
    /// </summary>
    public static class CorridorKeyUxmlPaths
    {
        public const string WindowUxmlAssetName = "CorridorKeyWindow";

        public static VisualTreeAsset? LoadWindowTree()
        {
            var guids = AssetDatabase.FindAssets($"{WindowUxmlAssetName} t:VisualTreeAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("CorridorKey", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase))
                    continue;
                var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (asset != null)
                    return asset;
            }

            return null;
        }
    }
}
