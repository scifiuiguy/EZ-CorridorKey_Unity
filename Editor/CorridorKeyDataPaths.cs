#nullable enable
using System.IO;
using UnityEngine;

namespace CorridorKey.Editor
{
    /// <summary>
    /// Resolves <c>CorridorKeyData/…/clips/…</c> under the Unity project (same layout as Phase 2 bridge tests).
    /// </summary>
    public static class CorridorKeyDataPaths
    {
        /// <summary>Default greenscreen test clip used for BiRefNet UI integration (matches debug menu).</summary>
        public const string DefaultProjectId = "260415_062207_greenscreen-test-02";

        public const string DefaultClipFolderName = "greenscreen-test-02";

        public static string ProjectRootAbsolute =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>Clip root: …/CorridorKeyData/{projectId}/clips/{clipFolder}/</summary>
        public static string GetClipRoot(string projectId, string clipFolderName) =>
            Path.GetFullPath(Path.Combine(ProjectRootAbsolute, "CorridorKeyData", projectId, "clips", clipFolderName));

        public static string GetFramesDir(string projectId, string clipFolderName) =>
            Path.Combine(GetClipRoot(projectId, clipFolderName), "Frames");

        public static string GetAlphaHintDir(string projectId, string clipFolderName) =>
            Path.Combine(GetClipRoot(projectId, clipFolderName), "AlphaHint");

        public static bool TryGetDefaultTestClip(out string clipRoot, out string framesDir)
        {
            clipRoot = GetClipRoot(DefaultProjectId, DefaultClipFolderName);
            framesDir = Path.Combine(clipRoot, "Frames");
            return Directory.Exists(framesDir);
        }

        public static bool IsPathUnderProject(string absoluteCandidatePath)
        {
            var root = Path.GetFullPath(ProjectRootAbsolute).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            var cand = Path.GetFullPath(absoluteCandidatePath);
            return cand.StartsWith(root, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
