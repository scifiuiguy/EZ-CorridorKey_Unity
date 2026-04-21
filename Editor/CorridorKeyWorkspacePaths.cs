#nullable enable
using System.IO;

namespace CorridorKey.Editor
{
    /// <summary>Resolves EZ v2-style workspace root (folder that contains <c>clips/</c>) under <c>CorridorKeyData</c>.</summary>
    public static class CorridorKeyWorkspacePaths
    {
        /// <summary>
        /// Default workspace: <c>…/CorridorKeyData/{DefaultProjectId}/</c> when <c>clips</c> exists (EZ <c>_clips_dir</c> for a single open project).
        /// </summary>
        public static bool TryGetDefaultWorkspaceRoot(out string workspaceRootAbsolute)
        {
            workspaceRootAbsolute = string.Empty;
            var root = Path.Combine(CorridorKeyDataPaths.ProjectRootAbsolute, "CorridorKeyData", CorridorKeyDataPaths.DefaultProjectId);
            root = Path.GetFullPath(root);
            var clips = Path.Combine(root, "clips");
            if (!Directory.Exists(clips))
                return false;
            workspaceRootAbsolute = root;
            return true;
        }

        public static bool IsWorkspacePathAllowed(string absolutePath)
        {
            return CorridorKeyDataPaths.IsPathUnderProject(absolutePath);
        }
    }
}
