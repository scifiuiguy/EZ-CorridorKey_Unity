using CorridorKey;

namespace CorridorKey.Editor.ViewModels
{
    /// <summary>
    /// EZ parity: <c>ui/models/clip_model.py</c> — one row in the clip queue.
    /// </summary>
    public sealed class ClipRowVm
    {
        public ClipRowVm(string name, ClipState state)
        {
            Name = name;
            State = state;
        }

        public string Name { get; }
        public ClipState State { get; set; }
    }
}
