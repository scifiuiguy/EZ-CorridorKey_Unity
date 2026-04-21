#nullable enable
using System.Collections.Generic;
using CorridorKey.Backend;

namespace CorridorKey.Editor.ViewModels
{
    /// <summary>
    /// Central session state for the editor window — EZ parity: <c>ui/main_window_mixins/*.py</c> (split).
    /// </summary>
    public sealed class CorridorKeySessionVm
    {
        public CorridorKeySessionVm(IBackendClient backend)
        {
            Backend = backend;
        }

        public IBackendClient Backend { get; }

        public List<ClipRowVm> Clips { get; } = new();

        public ClipRowVm? SelectedClip { get; set; }

        /// <summary>EZ <c>clip_input_is_linear</c> — per-clip color interpretation overrides.</summary>
        public Dictionary<string, bool> ClipInputIsLinear { get; } = new();
    }
}
