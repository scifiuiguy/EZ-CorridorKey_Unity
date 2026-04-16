using CorridorKey.Editor.Settings;

namespace CorridorKey.Editor.Wizard
{
    /// <summary>
    /// Future: first-run wizard to install or locate EZ Python backend — see README.
    /// Default clone/install root: <see cref="CorridorKeyBackendLayout.DefaultEzRoot"/>.
    /// </summary>
    public static class BackendSetupWizard
    {
        /// <summary>Wizard should install EZ here unless the user picks another folder.</summary>
        public static string PlannedDefaultEzRoot => CorridorKeyBackendLayout.DefaultEzRoot;
    }
}
