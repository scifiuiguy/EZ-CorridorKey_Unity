namespace CorridorKey
{
    /// <summary>
    /// EZ parity: <c>backend/clip_state.py</c> (<c>ClipEntry</c>) — minimal fields for UI gating.
    /// </summary>
    public sealed class ClipEntry
    {
        public ClipEntry(string name, string rootPath)
        {
            Name = name;
            RootPath = rootPath;
        }

        public string Name { get; }
        public string RootPath { get; }
        public ClipState State { get; set; }
        public int FrameCount { get; set; }
    }
}
