using System;
using System.IO;
using UnityEngine;

namespace CorridorKey.Editor.Settings
{
    /// <summary>
    /// Default on-disk layout for the EZ-CorridorKey checkout used by the future setup wizard and
    /// <see cref="CorridorKeyBackendSettingsWindow"/> when EditorPrefs are empty.
    /// </summary>
    /// <remarks>
    /// Convention: under the user profile, <c>CorridorKey/EZ-CorridorKey</c> — same folder names on
    /// Windows, macOS, and Linux. The wizard will clone or install EZ here unless the user overrides.
    /// </remarks>
    public static class CorridorKeyBackendLayout
    {
        /// <summary>First segment under the user profile, e.g. <c>%USERPROFILE%\CorridorKey</c>.</summary>
        public const string CorridorKeyParentFolderName = "CorridorKey";

        /// <summary>Checkout folder name (matches the upstream repo name).</summary>
        public const string EzDefaultFolderName = "EZ-CorridorKey";

        /// <summary>
        /// Default EZ repository root: <c>UserProfile/CorridorKey/EZ-CorridorKey</c>.
        /// </summary>
        public static string DefaultEzRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                CorridorKeyParentFolderName,
                EzDefaultFolderName);

        /// <summary>
        /// Default venv interpreter path after <c>1-install</c> (or wizard-equivalent) creates <c>.venv</c>.
        /// </summary>
        public static string DefaultPythonExecutablePath()
        {
            var root = DefaultEzRoot;
            if (Application.platform == RuntimePlatform.WindowsEditor)
                return Path.Combine(root, ".venv", "Scripts", "python.exe");
            return Path.Combine(root, ".venv", "bin", "python3");
        }
    }
}
