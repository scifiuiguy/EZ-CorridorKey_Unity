using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI.Presenters
{
    /// <summary>
    /// EZ parity: I/O tray / clip list — bind to <c>CorridorKeySessionVm</c> in a later pass.
    /// </summary>
    public sealed class QueuePresenter
    {
        public QueuePresenter(VisualElement root)
        {
            Root = root;
        }

        public VisualElement Root { get; }
    }
}
