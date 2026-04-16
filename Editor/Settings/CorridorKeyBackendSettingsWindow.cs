using System.IO;
using UnityEditor;
using UnityEngine;

namespace CorridorKey.Editor.Settings
{
    /// <summary>
    /// Sets <see cref="CorridorKeySettings"/> (EditorPrefs) for the Python bridge.
    /// These values are not stored in Project Settings; they are per-machine Editor preferences.
    /// </summary>
    public sealed class CorridorKeyBackendSettingsWindow : EditorWindow
    {
        string _pythonPath = string.Empty;
        string _ezRootPath = string.Empty;

        [MenuItem("Tools/CorridorKey/Backend Settings…", false, 20)]
        public static void Open()
        {
            var window = GetWindow<CorridorKeyBackendSettingsWindow>(true, "CorridorKey Backend", true);
            window.minSize = new Vector2(520f, 200f);
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

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                    Save();

                if (GUILayout.Button("Clear saved paths", GUILayout.Width(140)))
                {
                    CorridorKeySettings.PythonExecutable = null;
                    CorridorKeySettings.BackendWorkingDirectory = null;
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
            RefreshFromPrefs();
            Debug.Log("[CorridorKey] Backend paths saved to EditorPrefs.");
        }
    }
}
