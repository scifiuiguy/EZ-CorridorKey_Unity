using CorridorKey;

namespace CorridorKey.Backend.Payloads
{
    /// <summary>
    /// Clip state update from the bridge (mirrors EZ clip badge transitions).
    /// </summary>
    public sealed class ClipStatePayload
    {
        public ClipStatePayload(string clipName, ClipState state)
        {
            ClipName = clipName;
            State = state;
        }

        public string ClipName { get; }
        public ClipState State { get; }
    }
}
