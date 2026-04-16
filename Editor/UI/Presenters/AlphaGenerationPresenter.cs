using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI.Presenters
{
    /// <summary>
    /// EZ parity: alpha generation section of <c>ui/widgets/parameter_panel.py</c>.
    /// </summary>
    public sealed class AlphaGenerationPresenter
    {
        public AlphaGenerationPresenter(VisualElement root)
        {
            Root = root;
        }

        public VisualElement Root { get; }
    }
}
