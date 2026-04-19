#nullable enable
using System;

namespace CorridorKey.Editor.Backend
{
    /// <summary>JsonUtility payload for <c>guided.sam2_track</c> on <c>unity_bridge.py</c>.</summary>
    [Serializable]
    public sealed class Sam2TrackStdin
    {
        public string cmd = "guided.sam2_track";
        public string request_id = "";
        public string clip_root = "";
        public string frames_dir = "";
    }
}
