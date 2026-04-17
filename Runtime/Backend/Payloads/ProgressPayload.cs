#nullable enable

namespace CorridorKey.Backend.Payloads
{
    /// <summary>
    /// Coarse job progress from the bridge (frame counts, percent, or phase label).
    /// </summary>
    public sealed class ProgressPayload
    {
        public ProgressPayload(int current, int total, string? phase = null, string? detail = null)
        {
            Current = current;
            Total = total;
            Phase = phase;
            Detail = detail;
        }

        public int Current { get; }
        public int Total { get; }
        public string? Phase { get; }
        /// <summary>Optional sub-step text from the bridge (load stages, ETA hints).</summary>
        public string? Detail { get; }
    }
}
