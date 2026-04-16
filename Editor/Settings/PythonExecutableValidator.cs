#nullable enable
using System.IO;
using UnityEngine;

namespace CorridorKey.Editor.Settings
{
    /// <summary>
    /// Ensures the bridge uses a real interpreter. On Windows, bare <c>python</c> and the
    /// Microsoft Store shim under <c>WindowsApps</c> fail with a stderr message that is easy to misread.
    /// </summary>
    public static class PythonExecutableValidator
    {
        /// <param name="requireExecutableExists">
        /// When false, allows saving a path that does not exist yet (e.g. wizard default before <c>.venv</c> is created).
        /// The bridge still requires a real file at runtime.
        /// </param>
        public static string? Validate(string pythonPath, bool requireExecutableExists = true)
        {
            if (string.IsNullOrWhiteSpace(pythonPath))
                return null;

            pythonPath = pythonPath.Trim();

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                if (!Path.IsPathRooted(pythonPath))
                {
                    return "On Windows, set Python to the full path to python.exe (for example …\\.venv\\Scripts\\python.exe). " +
                        "A bare \"python\" command resolves to the Microsoft Store stub and will not run the bridge.";
                }

                var full = Path.GetFullPath(pythonPath);
                if (full.IndexOf("WindowsApps", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "That path is the Microsoft Store Python launcher (or alias). Set the full path to your " +
                        "venv Scripts\\python.exe, or disable the Python app execution aliases under " +
                        "Settings → Apps → Advanced app settings → App execution aliases.";
                }

                if (requireExecutableExists && !File.Exists(pythonPath))
                    return $"Python executable not found: {pythonPath}";
            }
            else
            {
                if (requireExecutableExists && Path.IsPathRooted(pythonPath) && !File.Exists(pythonPath))
                    return $"Python executable not found: {pythonPath}";
            }

            return null;
        }
    }
}
