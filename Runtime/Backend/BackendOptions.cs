#nullable enable

namespace CorridorKey.Backend
{
    /// <summary>
    /// Paths and options for launching the Python bridge (wizard output).
    /// </summary>
    public sealed class BackendOptions
    {
        public BackendOptions(string? pythonExecutablePath, string? workingDirectory)
        {
            PythonExecutablePath = pythonExecutablePath;
            WorkingDirectory = workingDirectory;
        }

        public string? PythonExecutablePath { get; }
        public string? WorkingDirectory { get; }
    }
}
