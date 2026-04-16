namespace CorridorKey
{
    /// <summary>
    /// EZ parity: <c>backend/project.py</c> — project root and paths on disk.
    /// </summary>
    public sealed class ProjectContext
    {
        public ProjectContext(string projectRootPath)
        {
            ProjectRootPath = projectRootPath;
        }

        public string ProjectRootPath { get; }
    }
}
