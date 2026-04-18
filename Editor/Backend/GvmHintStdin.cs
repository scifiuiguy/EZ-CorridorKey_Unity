#nullable enable
using System;

namespace CorridorKey.Editor.Backend
{
    /// <summary>JsonUtility payload for <c>alpha.gvm_hint</c> on <c>unity_bridge.py</c>.</summary>
    [Serializable]
    public sealed class GvmHintStdin
    {
        public string cmd = "alpha.gvm_hint";
        public string request_id = "";
        public string clip_root = "";
        public string frames_dir = "";
        public bool overwrite = true;
    }
}
