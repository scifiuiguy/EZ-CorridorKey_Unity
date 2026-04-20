#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorridorKey.Editor.Settings;
using UnityEngine;

namespace CorridorKey.Editor.Backend
{
    /// <summary>
    /// When EditorPrefs are empty, finds the wizard-installed <c>R</c> app tree and its <c>.venv</c> Python so
    /// TRACK MASK and the bridge work without opening settings (same parent folder as the Unity project, or above).
    /// </summary>
    public static class CorridorKeyBackendAutoLocator
    {
        /// <summary>
        /// Resolves Python exe + runtime root (cwd for bridge). Uses EditorPrefs when set; otherwise scans disk.
        /// </summary>
        /// <param name="errorHint">Human-readable failure when returning false.</param>
        public static bool TryResolveForBridge(out string pythonExe, out string runtimeRoot, out string? errorHint)
        {
            pythonExe = "";
            runtimeRoot = "";
            errorHint = null;

            var prefsPy = CorridorKeySettings.PythonExecutable;
            var prefsRoot = CorridorKeySettings.BackendWorkingDirectory;

            if (!string.IsNullOrEmpty(prefsPy) && !string.IsNullOrEmpty(prefsRoot))
            {
                var err = PythonExecutableValidator.Validate(prefsPy);
                if (err != null)
                {
                    errorHint = err;
                    return false;
                }

                if (!IsCorridorKeyRuntimeRoot(prefsRoot))
                {
                    errorHint =
                        "EditorPrefs Backend Working Directory does not look like a CorridorKey repo "
                        + "(expected scripts/setup_models.py and pyproject.toml).";
                    return false;
                }

                pythonExe = prefsPy;
                runtimeRoot = Path.GetFullPath(prefsRoot);
                return true;
            }

            if (!string.IsNullOrEmpty(prefsPy))
            {
                var err = PythonExecutableValidator.Validate(prefsPy);
                if (err != null)
                {
                    errorHint = err;
                    return false;
                }

                var inferred = InferRuntimeRootFromVenvPython(prefsPy);
                if (string.IsNullOrEmpty(inferred) || !IsCorridorKeyRuntimeRoot(inferred))
                {
                    errorHint =
                        "Python path is set but could not infer R root from it (expected …\\.venv\\Scripts\\python.exe). "
                        + "Set Backend Working Directory, or clear Python path to use auto-detect.";
                    return false;
                }

                pythonExe = prefsPy;
                runtimeRoot = inferred;
                return true;
            }

            if (!string.IsNullOrEmpty(prefsRoot))
            {
                if (!IsCorridorKeyRuntimeRoot(prefsRoot))
                {
                    errorHint = "EditorPrefs Backend Working Directory is not a valid CorridorKey repo root.";
                    return false;
                }

                var py = TryGetDefaultVenvPython(prefsRoot);
                if (string.IsNullOrEmpty(py))
                {
                    errorHint =
                        $"No .venv Python found under {prefsRoot}. Run the installer (1-install) in R, or set Python path.";
                    return false;
                }

                var err = PythonExecutableValidator.Validate(py);
                if (err != null)
                {
                    errorHint = err;
                    return false;
                }

                pythonExe = py;
                runtimeRoot = Path.GetFullPath(prefsRoot);
                return true;
            }

            foreach (var root in BuildOrderedRuntimeCandidates())
            {
                var py = TryGetDefaultVenvPython(root);
                if (string.IsNullOrEmpty(py))
                    continue;
                var err = PythonExecutableValidator.Validate(py);
                if (err != null)
                    continue;
                pythonExe = py;
                runtimeRoot = root;
                return true;
            }

            errorHint =
                "Could not find R (CorridorKey runtime) with .venv beside this Unity project. "
                + "Run the setup wizard so R is installed next to the project and 1-install has been run once, "
                + "or set Python + Backend Working Directory in CorridorKey settings.";
            return false;
        }

        internal static bool IsCorridorKeyRuntimeRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            var sm = Path.Combine(path, "scripts", "setup_models.py");
            var pp = Path.Combine(path, "pyproject.toml");
            if (!File.Exists(sm) || !File.Exists(pp))
                return false;
            try
            {
                var head = File.ReadAllText(pp);
                return head.Contains("name = \"corridorkey\"", StringComparison.Ordinal)
                    || head.Contains("name='corridorkey'", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        internal static string? TryGetDefaultVenvPython(string runtimeRoot)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var p = Path.Combine(runtimeRoot, ".venv", "Scripts", "python.exe");
                return File.Exists(p) ? p : null;
            }

            var p3 = Path.Combine(runtimeRoot, ".venv", "bin", "python3");
            if (File.Exists(p3))
                return p3;
            var p2 = Path.Combine(runtimeRoot, ".venv", "bin", "python");
            return File.Exists(p2) ? p2 : null;
        }

        internal static string? InferRuntimeRootFromVenvPython(string pythonExe)
        {
            var full = Path.GetFullPath(pythonExe.Trim().Replace('/', Path.DirectorySeparatorChar));
            var sep = Path.DirectorySeparatorChar;
            var win = $"{sep}.venv{sep}Scripts{sep}python.exe";
            if (full.EndsWith(win, StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(full)!));
            var unix = $"{sep}.venv{sep}bin{sep}python";
            if (full.EndsWith(unix, StringComparison.OrdinalIgnoreCase) || full.EndsWith(unix + "3", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(full)!));
            return null;
        }

        static List<string> BuildOrderedRuntimeCandidates()
        {
            var ordered = new List<string>();

            void appendR(string? rPath)
            {
                if (string.IsNullOrEmpty(rPath))
                    return;
                var f = Path.GetFullPath(rPath);
                if (!IsCorridorKeyRuntimeRoot(f))
                    return;
                if (ordered.Any(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase)))
                    return;
                ordered.Add(f);
            }

            var assets = Application.dataPath;
            if (!string.IsNullOrEmpty(assets))
            {
                var unityProj = Path.GetDirectoryName(assets);
                for (
                    var walk = unityProj;
                    !string.IsNullOrEmpty(walk);
                    walk = Path.GetDirectoryName(walk!))
                {
                    var par = Path.GetDirectoryName(walk);
                    if (string.IsNullOrEmpty(par))
                        break;
                    appendR(Path.Combine(par, "R"));
                }
            }

            var bridge = CorridorKeyPackagePaths.GetUnityBridgeScriptPath();
            if (!string.IsNullOrEmpty(bridge))
            {
                for (
                    var walk = Path.GetDirectoryName(bridge);
                    !string.IsNullOrEmpty(walk);
                    walk = Path.GetDirectoryName(walk!))
                {
                    var par = Path.GetDirectoryName(walk);
                    if (string.IsNullOrEmpty(par))
                        break;
                    appendR(Path.Combine(par, "R"));
                }
            }

            return ordered;
        }
    }
}
