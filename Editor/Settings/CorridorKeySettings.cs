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

        /// <summary>Optional full path to <c>nvml.dll</c> for the header GPU meter (Windows). Empty = auto-resolve.</summary>
        public const string NvmlDllPathKey = "CorridorKey.NvmlDllPath";

        const string PythonKey = PythonExecutableKey;
        const string WorkingDirKey = BackendWorkingDirectoryKey;
        const string NvmlKey = NvmlDllPathKey;

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

        /// <summary>
        /// Optional extra path to try for NVIDIA NVML after <c>nvml.dll</c> by name fails (see <c>NvmlGpuMeter</c>).
        /// Load order: by name → this path if set → <see cref="System.Environment.SystemDirectory"/>/<c>nvml.dll</c>.
        /// </summary>
        public static string? NvmlDllPath
        {
            get
            {
                var v = EditorPrefs.GetString(NvmlKey, string.Empty).Trim();
                return string.IsNullOrEmpty(v) ? null : v;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    EditorPrefs.DeleteKey(NvmlKey);
                else
                    EditorPrefs.SetString(NvmlKey, value);
            }
        }
    }
}
