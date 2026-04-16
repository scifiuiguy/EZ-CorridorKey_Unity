#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Single Files bar under the I/O tray — click anywhere on the bar to show/hide INPUT and EXPORTS together.
    /// </summary>
    public sealed class IoFilesBarToggleController
    {
        readonly VisualElement _ioInput;
        readonly VisualElement _ioExports;
        readonly HorizontalViewerIoSplitController _split;
        readonly VerticalViewerIoTraySplitController? _verticalSplit;

        bool _expanded = true;

        public IoFilesBarToggleController(
            VisualElement ioTrayInput,
            VisualElement ioTrayExports,
            VisualElement filesBar,
            HorizontalViewerIoSplitController split,
            VerticalViewerIoTraySplitController? verticalSplit)
        {
            _ioInput = ioTrayInput;
            _ioExports = ioTrayExports;
            _split = split;
            _verticalSplit = verticalSplit;

            filesBar.tooltip = "Show or hide INPUT and EXPORTS";
            filesBar.RegisterCallback<ClickEvent>(_ => Toggle());

            _split.RefreshIoTrayLayout();
        }

        public bool Expanded => _expanded;

        public void SetExpanded(bool expanded)
        {
            if (_expanded == expanded)
                return;
            _expanded = expanded;
            Apply();
            _verticalSplit?.SetIoPanelsCollapsed(!_expanded);
            _split.RefreshIoTrayLayout();
        }

        void Toggle()
        {
            SetExpanded(!_expanded);
        }

        void Apply()
        {
            if (_expanded)
            {
                _ioInput.style.display = DisplayStyle.Flex;
                _ioInput.pickingMode = PickingMode.Position;
                _ioExports.style.display = DisplayStyle.Flex;
                _ioExports.pickingMode = PickingMode.Position;
            }
            else
            {
                _ioInput.style.display = DisplayStyle.None;
                _ioInput.pickingMode = PickingMode.Ignore;
                _ioExports.style.display = DisplayStyle.None;
                _ioExports.pickingMode = PickingMode.Ignore;
            }
        }

        /// <summary>Wire after <see cref="HorizontalViewerIoSplitController.TryAttach"/>.</summary>
        public static IoFilesBarToggleController? TryAttach(
            VisualElement mainBodyRoot,
            HorizontalViewerIoSplitController? split,
            VerticalViewerIoTraySplitController? verticalSplit)
        {
            if (split == null)
                return null;

            var ioIn = mainBodyRoot.Q<VisualElement>("io-tray-input");
            var ioEx = mainBodyRoot.Q<VisualElement>("io-tray-exports");
            var filesBar = mainBodyRoot.Q<VisualElement>("io-files-bar");

            if (ioIn == null || ioEx == null || filesBar == null)
                return null;

            return new IoFilesBarToggleController(ioIn, ioEx, filesBar, split, verticalSplit);
        }
    }
}
