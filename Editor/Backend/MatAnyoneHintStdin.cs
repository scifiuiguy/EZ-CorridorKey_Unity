#nullable enable
using System;

namespace CorridorKey.Editor.Backend
{
    /// <summary>JsonUtility payload for <c>alpha.matanyone2_hint</c> on <c>unity_bridge.py</c>.</summary>
    [Serializable]
    public sealed class MatAnyoneHintStdin
    {
        public string cmd = "alpha.matanyone2_hint";
        public string request_id = "";
        public string clip_root = "";
        public string frames_dir = "";
        public bool overwrite = true;
    }
}
