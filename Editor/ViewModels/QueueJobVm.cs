#nullable enable

namespace CorridorKey.Editor.ViewModels
{
    public enum QueueJobKind
    {
        BiRefNetAlphaHint,
        GvmAlphaHint,
        MatAnyoneAlphaHint,
        Sam2Setup,
        Sam2Track,
    }

    public enum QueueJobStatus
    {
        Queued,
        Running,
        Succeeded,
        Failed,
        Cancelled,
    }

    /// <summary>
    /// EZ GPU job row parity: <see cref="CurrentFrame"/> / <see cref="TotalFrames"/> drive the bar when total is known.
    /// </summary>
    public sealed class QueueJobVm
    {
        public QueueJobVm(
            string jobId,
            QueueJobKind kind,
            string typeDisplay,
            string clipFileLabel,
            QueueJobStatus status = QueueJobStatus.Running)
        {
            JobId = jobId;
            Kind = kind;
            TypeDisplay = typeDisplay;
            ClipFileLabel = clipFileLabel;
            Status = status;
        }

        public string JobId { get; }

        public QueueJobKind Kind { get; }

        public string TypeDisplay { get; }

        public string ClipFileLabel { get; }

        public QueueJobStatus Status { get; set; }

        public int CurrentFrame { get; set; }

        public int TotalFrames { get; set; }

        /// <summary>Bridge <c>request_id</c> or other correlation key (optional).</summary>
        public string? RequestId { get; set; }

        /// <summary>Secondary line: phase, ETA note, or last detail (optional).</summary>
        public string? Detail { get; set; }
    }
}
