#nullable enable
using System;

namespace CorridorKey.Editor.Backend
{
    /// <summary>JsonUtility payload for <c>model.is_installed</c> (Unity cannot serialize anonymous types).</summary>
    [Serializable]
    public sealed class ModelIsInstalledStdin
    {
        public string cmd = "model.is_installed";
        public string request_id = "";
        public string model_name = "";
    }

    /// <summary>JsonUtility payload for <c>model.download_gvm</c>.</summary>
    [Serializable]
    public sealed class ModelDownloadGvmStdin
    {
        public string cmd = "model.download_gvm";
        public string request_id = "";
    }

    /// <summary>JsonUtility payload for <c>model.download_sam2</c> (weights only; diagnostics).</summary>
    [Serializable]
    public sealed class ModelDownloadSam2Stdin
    {
        public string cmd = "model.download_sam2";
        public string request_id = "";
        public string model_name = "base-plus";
    }

    /// <summary>
    /// JsonUtility payload for <c>model.prepare_sam2_track</c>: pip-install EZ <c>[tracker]</c> extra + HF weights
    /// (same as setup wizard / <c>1-install</c> optional SAM2), so Track Mask needs no manual pip.
    /// </summary>
    [Serializable]
    public sealed class ModelPrepareSam2TrackStdin
    {
        public string cmd = "model.prepare_sam2_track";
        public string request_id = "";
        public string model_name = "base-plus";
    }
}
