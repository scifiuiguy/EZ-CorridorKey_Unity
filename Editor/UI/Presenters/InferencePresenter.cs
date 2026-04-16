using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI.Presenters
{
    /// <summary>
    /// EZ parity: inference section of <c>ui/widgets/parameter_panel.py</c>.
    /// </summary>
    public sealed class InferencePresenter
    {
        public InferencePresenter(VisualElement root)
        {
            Root = root;
        }

        public VisualElement Root { get; }
    }
}
