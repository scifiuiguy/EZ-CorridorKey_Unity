#nullable enable

namespace CorridorKey.Editor.Session
{
    /// <summary>EZ <c>QSettings</c> parity for values not stored in <c>.corridorkey_session.json</c> (e.g. parallel frames).</summary>
    public static class EditorPrefsKeys
    {
        public const string ParallelFrames = "CorridorKey.Editor.ParallelFrames";
    }
}
