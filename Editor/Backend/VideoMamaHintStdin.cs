#nullable enable
using System;

namespace CorridorKey.Editor.Backend
{
    /// <summary>JsonUtility payload for <c>alpha.videomama_hint</c> on <c>unity_bridge.py</c>.</summary>
    [Serializable]
    public sealed class VideoMamaHintStdin
    {
        public string cmd = "alpha.videomama_hint";
        public string request_id = "";
        public string clip_root = "";
        public string frames_dir = "";
        public bool overwrite = true;
    }
}
