#nullable enable
using UnityEditor;

namespace CorridorKey.Editor.Settings
{
    /// <summary>
    /// EditorPrefs keys for backend paths (wizard will populate).
    /// These are stored in the Unity Editor user profile, not in Project Settings.
    /// </summary>
    public static class CorridorKeySettings
    {
        /// <summary>EditorPrefs key string (for documentation / support).</summary>
        public const string PythonExecutableKey = "CorridorKey.PythonExecutable";

        /// <summary>EditorPrefs key string (for documentation / support).</summary>
        public const string BackendWorkingDirectoryKey = "CorridorKey.BackendWorkingDirectory";

        const string PythonKey = PythonExecutableKey;
        const string WorkingDirKey = BackendWorkingDirectoryKey;

        public static string? PythonExecutable
        {
            get
            {
                var v = EditorPrefs.GetString(PythonKey, string.Empty).Trim();
                return string.IsNullOrEmpty(v) ? null : v;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    EditorPrefs.DeleteKey(PythonKey);
                else
                    EditorPrefs.SetString(PythonKey, value);
            }
        }

        public static string? BackendWorkingDirectory
        {
            get
            {
                var v = EditorPrefs.GetString(WorkingDirKey, string.Empty).Trim();
                return string.IsNullOrEmpty(v) ? null : v;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    EditorPrefs.DeleteKey(WorkingDirKey);
                else
                    EditorPrefs.SetString(WorkingDirKey, value);
            }
        }
    }
}
