using CorridorKey.Backend;
using CorridorKey.Editor.Settings;

namespace CorridorKey.Editor.Backend
{
    /// <summary>
    /// Resolves <see cref="BackendOptions"/> from Editor settings / wizard.
    /// </summary>
    public static class BackendLocator
    {
        public static BackendOptions GetOptions()
        {
            return new BackendOptions(
                CorridorKeySettings.PythonExecutable,
                CorridorKeySettings.BackendWorkingDirectory);
        }
    }
}
