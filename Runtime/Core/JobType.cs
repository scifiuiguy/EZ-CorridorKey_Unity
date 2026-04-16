namespace CorridorKey
{
    /// <summary>
    /// EZ parity: job kinds from <c>ui/workers/gpu_job_worker.py</c> / <c>JobType</c>.
    /// </summary>
    public enum JobType
    {
        Extract,
        GvmAlpha,
        BirefnetAlpha,
        TrackMask,
        VideomamaAlpha,
        MatAnyone2Alpha,
        Inference,
        Unknown
    }
}
