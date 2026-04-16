using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI.Presenters
{
    /// <summary>
    /// EZ parity: dual viewer + scrubber host.
    /// </summary>
    public sealed class ViewerPresenter
    {
        public ViewerPresenter(VisualElement root)
        {
            Root = root;
        }

        public VisualElement Root { get; }
    }
}
