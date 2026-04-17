#nullable enable
using System;

namespace CorridorKey.Editor.Backend
{
    /// <summary>JsonUtility payload for <c>alpha.birefnet_hint</c> on <c>unity_bridge.py</c>.</summary>
    [Serializable]
    public sealed class BiRefNetHintStdin
    {
        public string cmd = "alpha.birefnet_hint";
        public string request_id = "";
        public string clip_root = "";
        public string frames_dir = "";
        public string usage = "Matting";
        public bool overwrite = true;
    }
}
