using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// EZ <c>queue_panel.py</c> parity: 24px vertical QUEUE tab + optional 216px content; click tab toggles width.
    /// </summary>
    public sealed class QueueSidebarController
    {
        readonly VisualElement _sidebar;
        readonly VisualElement _content;
        bool _expanded;

        public QueueSidebarController(VisualElement sidebar, VisualElement content, VisualElement tab)
        {
            _sidebar = sidebar;
            _content = content;
            tab.tooltip = "Toggle queue panel (Q)";
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
                _sidebar.style.width = CorridorKeyWindowLayout.QueueExpandedWidthPx;
                _content.style.display = DisplayStyle.Flex;
            }
            else
            {
                _sidebar.style.width = CorridorKeyWindowLayout.QueueTabWidthPx;
                _content.style.display = DisplayStyle.None;
            }
        }
    }
}
