#nullable enable

namespace CorridorKey.Backend.Payloads
{
    /// <summary>
    /// Coarse job progress from the bridge (frame counts, percent, or phase label).
    /// </summary>
    public sealed class ProgressPayload
    {
        public ProgressPayload(int current, int total, string? phase = null)
        {
            Current = current;
            Total = total;
            Phase = phase;
        }

        public int Current { get; }
        public int Total { get; }
        public string? Phase { get; }
    }
}
