#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Draggable horizontal divider between the viewer block and the INPUTS/EXPORTS tray; adjusts tray height in pixels.
    /// </summary>
    public sealed class VerticalViewerIoTraySplitController : IDisposable
    {
        public const float DividerHeightPx = 4f;

        public const float MinIoTrayHeightPx = 96f;

        /// <summary>Minimum height for viewer+queue+params block (dual viewers + chrome + playhead strip).</summary>
        public const float MinViewerBlockHeightPx = 252f;

        readonly VisualElement _workspaceColumn;
        readonly VisualElement _divider;
        readonly VisualElement _ioTray;
        readonly VisualElement _ioTrayRow;
        readonly VisualElement _statusBar;

        float _ioTrayHeight = -1f;
        bool _ioPanelsCollapsed;
        bool _dragging;
        int _activePointerId;

        public VerticalViewerIoTraySplitController(
            VisualElement workspaceColumn,
            VisualElement divider,
            VisualElement ioTray,
            VisualElement ioTrayRow,
            VisualElement statusBar)
        {
            _workspaceColumn = workspaceColumn;
            _divider = divider;
            _ioTray = ioTray;
            _ioTrayRow = ioTrayRow;
            _statusBar = statusBar;

            _workspaceColumn.RegisterCallback<GeometryChangedEvent>(OnWorkspaceGeometryChanged);
            _divider.RegisterCallback<PointerDownEvent>(OnDividerPointerDown);
            _divider.RegisterCallback<PointerMoveEvent>(OnDividerPointerMove);
            _divider.RegisterCallback<PointerUpEvent>(OnDividerPointerUp);
            _divider.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            ApplyIoTrayHeight();
        }

        /// <summary>When true, INPUT/EXPORT row is hidden and tray height shrinks so the viewer block can grow.</summary>
        public void SetIoPanelsCollapsed(bool collapsed)
        {
            if (_ioPanelsCollapsed == collapsed)
                return;
            _ioPanelsCollapsed = collapsed;
            ApplyIoTrayHeight();
        }

        public void Dispose()
        {
            _workspaceColumn.UnregisterCallback<GeometryChangedEvent>(OnWorkspaceGeometryChanged);
            _divider.UnregisterCallback<PointerDownEvent>(OnDividerPointerDown);
            _divider.UnregisterCallback<PointerMoveEvent>(OnDividerPointerMove);
            _divider.UnregisterCallback<PointerUpEvent>(OnDividerPointerUp);
            _divider.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        void OnWorkspaceGeometryChanged(GeometryChangedEvent evt)
        {
            if (_dragging)
                return;
            ApplyIoTrayHeight();
        }

        void OnDividerPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;
            _dragging = true;
            _activePointerId = evt.pointerId;
            _divider.CapturePointer(_activePointerId);
            if (_ioTrayHeight < 0f)
                _ioTrayHeight = Mathf.Max(_ioTray.layout.height, MinIoTrayHeightPx);
            evt.StopPropagation();
        }

        void OnDividerPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _activePointerId)
                return;

            // Negate vertical delta — PointerMoveEvent.deltaPosition.y is opposite to the tray height change we want.
            var dy = evt.deltaPosition.y;
            _ioTrayHeight -= dy;
            ApplyIoTrayHeight();
            evt.StopPropagation();
        }

        void OnDividerPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _activePointerId)
                return;
            EndDrag();
            evt.StopPropagation();
        }

        void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _activePointerId)
                EndDrag();
        }

        void EndDrag()
        {
            _dragging = false;
            try
            {
                _divider.ReleasePointer(_activePointerId);
            }
            catch
            {
                // ignore
            }
        }

        void ApplyIoTrayHeight()
        {
            var totalH = _workspaceColumn.layout.height;
            if (totalH <= 0f || float.IsNaN(totalH))
                return;

            var statusH = _statusBar.layout.height;
            if (float.IsNaN(statusH) || statusH < 0f)
                statusH = 0f;

            var available = totalH - statusH - DividerHeightPx;
            if (available <= 0f || float.IsNaN(available))
                return;

            if (_ioPanelsCollapsed)
            {
                _ioTrayRow.style.display = DisplayStyle.None;
                var collapsedH = CorridorKeyWindowLayout.IoTrayCollapsedHeightPx;
                _ioTray.style.height = collapsedH;
                _ioTray.style.flexGrow = 0;
                _ioTray.style.flexShrink = 0;
                _ioTray.style.minHeight = collapsedH;
                return;
            }

            _ioTrayRow.style.display = DisplayStyle.Flex;

            if (_ioTrayHeight < 0f)
                _ioTrayHeight = Mathf.Max(_ioTray.layout.height, MinIoTrayHeightPx);

            // Reserve MinViewerBlockHeightPx for the viewer strip, but shrink the tray if the window is too short.
            var maxIo = Mathf.Min(
                Mathf.Max(MinIoTrayHeightPx, available - MinViewerBlockHeightPx),
                available - MinViewerBlockHeightPx);
            maxIo = Mathf.Max(maxIo, 0f);
            _ioTrayHeight = Mathf.Clamp(_ioTrayHeight, Mathf.Min(MinIoTrayHeightPx, maxIo), maxIo);

            _ioTray.style.height = _ioTrayHeight;
            _ioTray.style.flexGrow = 0;
            _ioTray.style.flexShrink = 0;
            _ioTray.style.minHeight = Mathf.Min(MinIoTrayHeightPx, _ioTrayHeight);
        }

        /// <summary>Wire after <see cref="CorridorKeyWindowLayout.BuildMainBodyColumn"/>.</summary>
        public static VerticalViewerIoTraySplitController? TryAttach(VisualElement mainBodyRoot)
        {
            var workspace = mainBodyRoot.Q<VisualElement>("workspace-column");
            var top = mainBodyRoot.Q<VisualElement>("viewer-params-block-row");
            var div = mainBodyRoot.Q<VisualElement>("io-tray-split-divider");
            var tray = mainBodyRoot.Q<VisualElement>("io-tray");
            var trayRow = mainBodyRoot.Q<VisualElement>("io-tray-row");
            var status = mainBodyRoot.Q<VisualElement>("status-bar");

            if (workspace == null || top == null || div == null || tray == null || trayRow == null || status == null)
                return null;

            return new VerticalViewerIoTraySplitController(workspace, div, tray, trayRow, status);
        }
    }
}
