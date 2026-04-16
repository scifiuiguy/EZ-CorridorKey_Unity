using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Unity-only: vertical tab strip + parameters column; mirrors <see cref="QueueSidebarController"/> (show/hide content width).
    /// </summary>
    public sealed class ParametersSidebarController
    {
        readonly VisualElement _shell;
        readonly VisualElement _content;
        bool _expanded = true;

        public ParametersSidebarController(VisualElement shell, VisualElement content, VisualElement tab)
        {
            _shell = shell;
            _content = content;
            tab.tooltip = "Toggle parameters panel";
            tab.RegisterCallback<ClickEvent>(_ => Toggle());
            Apply();
        }

        public bool Expanded => _expanded;

        public void Toggle()
        {
            _expanded = !_expanded;
            Apply();
        }

        public void SetExpanded(bool expanded)
        {
            if (_expanded == expanded)
                return;
            _expanded = expanded;
            Apply();
        }

        void Apply()
        {
            if (_expanded)
            {
                _shell.style.width = CorridorKeyWindowLayout.ParametersExpandedWidthPx;
                _content.style.display = DisplayStyle.Flex;
                _content.pickingMode = PickingMode.Position;
            }
            else
            {
                _shell.style.width = CorridorKeyWindowLayout.ParametersTabWidthPx;
                _content.style.display = DisplayStyle.None;
                // Later siblings paint above the tab; hidden rail must not intercept clicks on the tab strip.
                _content.pickingMode = PickingMode.Ignore;
            }
        }
    }
}
