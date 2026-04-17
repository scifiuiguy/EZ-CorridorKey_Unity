using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CorridorKey.Editor.Settings
{
    /// <summary>
    /// Sets <see cref="CorridorKeySettings"/> (EditorPrefs): Python bridge paths and optional Windows NVML DLL path.
    /// These values are not stored in Project Settings; they are per-machine Editor preferences.
    /// </summary>
    public sealed class CorridorKeyBackendSettingsWindow : EditorWindow
    {
        string _pythonPath = string.Empty;
        string _ezRootPath = string.Empty;
        string _nvmlDllPath = string.Empty;

        [MenuItem("Tools/CorridorKey/Backend Settings…", false, 20)]
        public static void Open()
        {
            var window = GetWindow<CorridorKeyBackendSettingsWindow>(true, "CorridorKey Backend", true);
            window.minSize = new Vector2(520f, 320f);
            window.RefreshFromPrefs();
            window.Show();
        }

        void OnEnable()
        {
            RefreshFromPrefs();
        }

        void RefreshFromPrefs()
        {
            _pythonPath = CorridorKeySettings.PythonExecutable
                ?? CorridorKeyBackendLayout.DefaultPythonExecutablePath();
            _ezRootPath = CorridorKeySettings.BackendWorkingDirectory
                ?? CorridorKeyBackendLayout.DefaultEzRoot;
            // No EditorPrefs value = show the usual driver path so devs see where NVML typically lives.
            // Runtime still resolves without any saved path (see NvmlGpuMeter.TryLoadNvmlModule).
            _nvmlDllPath = CorridorKeySettings.NvmlDllPath ?? NvmlDefaultDisplayPath();
        }

        /// <summary>Most likely <c>nvml.dll</c> path on Windows (driver install). Not persisted unless the user saves an override.</summary>
        static string NvmlDefaultDisplayPath()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                return string.Empty;
            return Path.Combine(Environment.SystemDirectory, "nvml.dll");
        }

        /// <summary>Maps the text field to EditorPrefs: empty, default system path, or plain "nvml.dll" → no override.</summary>
        static string? NormalizeNvmlPathForSave(string fieldValue)
        {
            var t = fieldValue.Trim();
            if (string.IsNullOrEmpty(t))
                return null;
            if (t.Equals("nvml.dll", StringComparison.OrdinalIgnoreCase))
                return null;
            var systemPath = Path.Combine(Environment.SystemDirectory, "nvml.dll");
            if (t.Equals(systemPath, StringComparison.OrdinalIgnoreCase))
                return null;
            return t;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Paths are saved to EditorPrefs on this machine (not in the project folder). " +
                "When nothing is saved yet, the fields show the default install location the wizard will use: " +
                $"`{CorridorKeyBackendLayout.CorridorKeyParentFolderName}/{CorridorKeyBackendLayout.EzDefaultFolderName}` under your user profile, " +
                "with `.venv` for Python. You can save these early or change them before running the bridge.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("EditorPrefs keys", EditorStyles.miniLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.SelectableLabel(CorridorKeySettings.PythonExecutableKey, EditorStyles.wordWrappedLabel);
                EditorGUILayout.SelectableLabel(CorridorKeySettings.BackendWorkingDirectoryKey, EditorStyles.wordWrappedLabel);
                EditorGUILayout.SelectableLabel(CorridorKeySettings.NvmlDllPathKey, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Python executable", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _pythonPath = EditorGUILayout.TextField(_pythonPath);
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    var dir = string.IsNullOrEmpty(_pythonPath)
                        ? string.Empty
                        : Path.GetDirectoryName(_pythonPath);
                    var picked = EditorUtility.OpenFilePanel("Python executable", dir, Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                    if (!string.IsNullOrEmpty(picked))
                        _pythonPath = picked;
                }
            }
            EditorGUILayout.HelpBox(
                "On Windows use the full path to python.exe inside your venv (Scripts\\python.exe). " +
                "Do not use a bare \"python\" command or the Microsoft Store stub (disable Python under App execution aliases if needed).",
                MessageType.None);

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("EZ-CorridorKey root (working directory)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _ezRootPath = EditorGUILayout.TextField(_ezRootPath);
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    var picked = EditorUtility.OpenFolderPanel("EZ repository root", _ezRootPath, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                        _ezRootPath = picked;
                }
            }
            EditorGUILayout.HelpBox(
                "Folder that contains `backend/` (e.g. your EZ clone). The bridge runs with this as the process working directory.",
                MessageType.None);

            EditorGUILayout.Space(8);

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                EditorGUILayout.LabelField("NVML DLL (optional, Windows GPU meter)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _nvmlDllPath = EditorGUILayout.TextField(_nvmlDllPath);
                    if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                    {
                        var dir = string.IsNullOrEmpty(_nvmlDllPath)
                            ? Environment.SystemDirectory
                            : Path.GetDirectoryName(_nvmlDllPath);
                        if (string.IsNullOrEmpty(dir))
                            dir = Environment.SystemDirectory;
                        var picked = EditorUtility.OpenFilePanel("nvml.dll (NVIDIA driver)", dir, "dll");
                        if (!string.IsNullOrEmpty(picked))
                            _nvmlDllPath = picked;
                    }
                }
                EditorGUILayout.HelpBox(
                    "You do not need to set this for the meter to work. Resolution order is always: load `nvml.dll` " +
                    "by name first, then your saved override (if any), then the explicit System32 path. " +
                    "So a mistaken override cannot block a working load-by-name. Only set a custom path when " +
                    "by-name and System32 both fail (e.g. nonstandard driver layout). Saving the default path " +
                    "shown above does not store an override. Reopen the CorridorKey window after changing this.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("NVML / GPU meter", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "The header GPU meter uses NVIDIA NVML on Windows Editor only. No configuration here on this platform.",
                    MessageType.None);
            }

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                    Save();

                if (GUILayout.Button("Clear saved paths", GUILayout.Width(140)))
                {
                    CorridorKeySettings.PythonExecutable = null;
                    CorridorKeySettings.BackendWorkingDirectory = null;
                    CorridorKeySettings.NvmlDllPath = null;
                    RefreshFromPrefs();
                }

                if (GUILayout.Button("Reload from disk", GUILayout.Width(120)))
                    RefreshFromPrefs();
            }
        }

        void Save()
        {
            var py = string.IsNullOrWhiteSpace(_pythonPath) ? null : _pythonPath.Trim();
            if (py != null)
            {
                var err = PythonExecutableValidator.Validate(py, requireExecutableExists: false);
                if (err != null)
                {
                    EditorUtility.DisplayDialog("CorridorKey — Python path", err, "OK");
                    return;
                }
            }

            CorridorKeySettings.PythonExecutable = py;
            CorridorKeySettings.BackendWorkingDirectory = string.IsNullOrWhiteSpace(_ezRootPath) ? null : _ezRootPath.Trim();
            CorridorKeySettings.NvmlDllPath = Application.platform == RuntimePlatform.WindowsEditor
                ? NormalizeNvmlPathForSave(_nvmlDllPath)
                : null;
            RefreshFromPrefs();
            Debug.Log("[CorridorKey] Backend paths saved to EditorPrefs.");
        }
    }
}
