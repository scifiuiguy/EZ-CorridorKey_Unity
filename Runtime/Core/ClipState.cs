namespace CorridorKey
{
    /// <summary>
    /// EZ parity: <c>backend/clip_state.py</c> (<see cref="ClipState"/>).
    /// </summary>
    public enum ClipState
    {
        Extracting,
        Raw,
        Masked,
        Ready,
        Complete,
        Error
    }
}
