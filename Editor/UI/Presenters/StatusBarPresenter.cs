using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI.Presenters
{
    /// <summary>
    /// EZ parity: <c>ui/widgets/status_bar.py</c> — progress + run buttons.
    /// </summary>
    public sealed class StatusBarPresenter
    {
        public StatusBarPresenter(VisualElement root)
        {
            Root = root;
        }

        public VisualElement Root { get; }
    }
}
