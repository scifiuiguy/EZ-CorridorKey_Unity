#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CorridorKey.Editor.Settings
{
    /// <summary>
    /// Paths under <c>Assets/StreamingAssets/</c> (relative, using '/') that are excluded from player builds
    /// by <see cref="Build.CorridorKeyStreamingAssetsBuildPreprocessor"/> (stashed during build, restored after).
    /// </summary>
    public static class CorridorKeyBuildExclusionSettings
    {
        public const string EditorPrefsKey = "CorridorKey.StreamingAssetsExcludePaths";

        /// <summary>JSON array of paths relative to StreamingAssets root, e.g. <c>CorridorKey/MyProject</c>.</summary>
        public static IReadOnlyList<string> GetExcludedRelativePaths()
        {
            var raw = EditorPrefs.GetString(EditorPrefsKey, string.Empty).Trim();
            if (string.IsNullOrEmpty(raw))
                return Array.Empty<string>();
            try
            {
                var wrap = JsonUtility.FromJson<StringListWrapper>(raw);
                if (wrap?.paths == null || wrap.paths.Length == 0)
                    return Array.Empty<string>();
                return wrap.paths
                    .Select(NormalizePath)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static void SetExcludedRelativePaths(IReadOnlyList<string> paths)
        {
            var normalized = paths
                .Select(NormalizePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalized.Length == 0)
            {
                EditorPrefs.DeleteKey(EditorPrefsKey);
                return;
            }

            if (!TryValidateAll(normalized, out var err))
            {
                Debug.LogWarning($"[CorridorKey] Build exclusion: invalid paths ignored ({err}).");
                normalized = normalized.Where(p => ValidateRelativePath(p, out _)).ToArray();
            }

            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(new StringListWrapper { paths = normalized }));
        }

        /// <summary>
        /// Returns false if the path is unsafe (e.g. contains <c>..</c>) or absolute.
        /// </summary>
        public static bool ValidateRelativePath(string relativeToStreamingAssets, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(relativeToStreamingAssets))
            {
                error = "path is empty";
                return false;
            }

            var n = NormalizePath(relativeToStreamingAssets);
            if (Path.IsPathRooted(n))
            {
                error = "path must be relative to StreamingAssets";
                return false;
            }

            var parts = n.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part == "..")
                {
                    error = "path must not contain '..'";
                    return false;
                }

                if (part == "." || string.IsNullOrWhiteSpace(part))
                {
                    error = "path must not contain '.' segments";
                    return false;
                }
            }

            return true;
        }

        static bool TryValidateAll(string[] paths, out string err)
        {
            foreach (var p in paths)
            {
                if (!ValidateRelativePath(p, out var e))
                {
                    err = e ?? "invalid";
                    return false;
                }
            }

            err = string.Empty;
            return true;
        }

        static string NormalizePath(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;
            s = s.Trim().Replace('\\', '/').Trim('/');
            return s;
        }

        [Serializable]
        class StringListWrapper
        {
            public string[] paths = Array.Empty<string>();
        }
    }
}
