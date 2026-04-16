#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Draggable vertical divider between INPUT and OUTPUT viewers; keeps IO tray split aligned in pixels
    /// (EZ <c>export_mixin._sync_io_divider</c> + <c>IOTrayPanel.sync_divider</c>).
    /// </summary>
    public sealed class HorizontalViewerIoSplitController : IDisposable
    {
        public const float DividerWidthPx = 4f;
        public const float MinPaneWidthPx = 80f;

        readonly VisualElement _dualHost;
        readonly VisualElement _left;
        readonly VisualElement _divider;
        readonly VisualElement _right;
        readonly VisualElement _ioInput;
        readonly VisualElement _ioExports;
        readonly VisualElement _queueSidebar;
        readonly VisualElement _ioQueueSpacer;
        readonly VisualElement _viewerColumn;
        readonly VisualElement? _parametersShell;
        readonly VisualElement _ioSplitRow;
        readonly VisualElement? _ioFilesBarQueueSpacer;

        float _ratio = 0.5f;
        bool _dragging;
        int _activePointerId;

        public HorizontalViewerIoSplitController(
            VisualElement dualViewerHost,
            VisualElement viewerInputColumn,
            VisualElement viewerDivider,
            VisualElement viewerOutputColumn,
            VisualElement ioTrayInput,
            VisualElement ioTrayExports,
            VisualElement queueSidebar,
            VisualElement ioQueueSpacer,
            VisualElement viewerColumn,
            VisualElement ioTraySplitRow,
            VisualElement? ioFilesBarQueueSpacer,
            VisualElement? parametersShell = null)
        {
            _dualHost = dualViewerHost;
            _left = viewerInputColumn;
            _divider = viewerDivider;
            _right = viewerOutputColumn;
            _ioInput = ioTrayInput;
            _ioExports = ioTrayExports;
            _queueSidebar = queueSidebar;
            _ioQueueSpacer = ioQueueSpacer;
            _viewerColumn = viewerColumn;
            _parametersShell = parametersShell;
            _ioSplitRow = ioTraySplitRow;
            _ioFilesBarQueueSpacer = ioFilesBarQueueSpacer;

            _dualHost.RegisterCallback<GeometryChangedEvent>(OnDualHostGeometryChanged);
            _queueSidebar.RegisterCallback<GeometryChangedEvent>(OnQueueSidebarGeometryChanged);
            if (_parametersShell != null)
                _parametersShell.RegisterCallback<GeometryChangedEvent>(OnParametersShellGeometryChanged);
            _divider.RegisterCallback<PointerDownEvent>(OnDividerPointerDown);
            _divider.RegisterCallback<PointerMoveEvent>(OnDividerPointerMove);
            _divider.RegisterCallback<PointerUpEvent>(OnDividerPointerUp);
            _divider.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        /// <summary>Call after I/O tray INPUTS/EXPORTS visibility toggles (Files bar).</summary>
        public void RefreshIoTrayLayout()
        {
            _dualHost.schedule.Execute(_ => ApplyLayoutFromRatio());
        }

        public void Dispose()
        {
            _dualHost.UnregisterCallback<GeometryChangedEvent>(OnDualHostGeometryChanged);
            _queueSidebar.UnregisterCallback<GeometryChangedEvent>(OnQueueSidebarGeometryChanged);
            if (_parametersShell != null)
                _parametersShell.UnregisterCallback<GeometryChangedEvent>(OnParametersShellGeometryChanged);
            _divider.UnregisterCallback<PointerDownEvent>(OnDividerPointerDown);
            _divider.UnregisterCallback<PointerMoveEvent>(OnDividerPointerMove);
            _divider.UnregisterCallback<PointerUpEvent>(OnDividerPointerUp);
            _divider.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        void OnDualHostGeometryChanged(GeometryChangedEvent evt)
        {
            if (_dragging)
                return;
            ApplyLayoutFromRatio();
        }

        void OnQueueSidebarGeometryChanged(GeometryChangedEvent evt)
        {
            if (_dragging)
                return;
            ApplyLayoutFromRatio();
        }

        void OnParametersShellGeometryChanged(GeometryChangedEvent evt)
        {
            if (_dragging)
                return;
            // Parameters rail width toggles don't always move dual-host geometry in the same pass; defer to post-layout.
            _dualHost.schedule.Execute(_ => ApplyLayoutFromRatio());
        }

        void OnDividerPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;
            _dragging = true;
            _activePointerId = evt.pointerId;
            _divider.CapturePointer(_activePointerId);
            evt.StopPropagation();
        }

        void OnDividerPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _activePointerId)
                return;

            var w = _dualHost.layout.width;
            if (w <= DividerWidthPx + MinPaneWidthPx * 2f)
                return;

            var inner = w - DividerWidthPx;
            var dx = evt.deltaPosition.x;
            _ratio = Mathf.Clamp(_ratio + dx / inner, 0.12f, 0.88f);
            ApplyLayoutFromRatio();
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

        void ApplyLayoutFromRatio()
        {
            var qW = _queueSidebar.layout.width;
            if (float.IsNaN(qW) || qW < 0f)
                qW = 0f;
            _ioQueueSpacer.style.width = qW;
            _ioQueueSpacer.style.flexShrink = 0;
            if (_ioFilesBarQueueSpacer != null)
            {
                _ioFilesBarQueueSpacer.style.width = qW;
                _ioFilesBarQueueSpacer.style.flexShrink = 0;
            }

            _viewerColumn.style.paddingLeft = qW;

            var w = _dualHost.layout.width;
            // When INPUT/OUTPUT have stale fixed widths, dual-host can measure wider than the viewer column's inner width
            // (parameters rail expands / window narrows). Clamp so panes shrink and the row doesn't overflow past the window edge.
            var innerMax = _viewerColumn.layout.width - _viewerColumn.resolvedStyle.paddingLeft;
            if (innerMax > 0f && !float.IsNaN(innerMax) && w > innerMax + 0.5f)
                w = innerMax;

            if (w <= 0f || float.IsNaN(w))
                return;

            var inner = w - DividerWidthPx;
            if (inner <= MinPaneWidthPx * 2f)
                return;

            var leftW = Mathf.Clamp(inner * _ratio, MinPaneWidthPx, inner - MinPaneWidthPx);
            var rightW = inner - leftW;

            _left.style.width = leftW;
            _left.style.flexGrow = 0;
            _left.style.flexShrink = 0;

            _right.style.width = rightW;
            _right.style.flexGrow = 0;
            _right.style.flexShrink = 0;

            var inputVisible = _ioInput.resolvedStyle.display != DisplayStyle.None;
            var exportVisible = _ioExports.resolvedStyle.display != DisplayStyle.None;

            // Split row is already beside the queue spacer; layout.width is the INPUT+EXPORT track (not including qW).
            var ioRowW = _ioSplitRow.layout.width;
            if (float.IsNaN(ioRowW) || ioRowW <= 0f)
                ioRowW = 0f;
            var ioInnerW = Mathf.Max(0f, ioRowW);

            if (inputVisible && exportVisible)
            {
                // INPUT column matches viewer INPUT width; EXPORTS fills the rest of the tray row (extends under parameters).
                // Fixed leftW + fixed rightW summed the dual-host inner width only — too narrow for the wider tray row and clipped EXPORTS.
                _ioInput.style.width = leftW;
                _ioInput.style.flexGrow = 0;
                _ioInput.style.flexShrink = 0;

                _ioExports.style.width = StyleKeyword.Auto;
                _ioExports.style.flexGrow = 1;
                _ioExports.style.flexShrink = 0;
                _ioExports.style.minWidth = 0;
            }
            else if (inputVisible)
            {
                _ioInput.style.width = ioInnerW;
                _ioInput.style.flexGrow = 0;
                _ioInput.style.flexShrink = 0;

                _ioExports.style.width = 0f;
                _ioExports.style.flexGrow = 0;
                _ioExports.style.flexShrink = 0;
            }
            else if (exportVisible)
            {
                _ioInput.style.width = 0f;
                _ioInput.style.flexGrow = 0;
                _ioInput.style.flexShrink = 0;

                _ioExports.style.width = ioInnerW;
                _ioExports.style.flexGrow = 0;
                _ioExports.style.flexShrink = 0;
            }
            else
            {
                _ioInput.style.width = 0f;
                _ioInput.style.flexGrow = 0;
                _ioExports.style.width = 0f;
                _ioExports.style.flexGrow = 0;
            }
        }

        /// <summary>Wire split after <see cref="CorridorKeyWindowLayout.BuildMainBodyColumn"/>.</summary>
        public static HorizontalViewerIoSplitController? TryAttach(VisualElement mainBodyRoot)
        {
            var dualHost = mainBodyRoot.Q<VisualElement>("dual-viewer-host");
            var left = mainBodyRoot.Q<VisualElement>("viewer-input");
            var div = mainBodyRoot.Q<VisualElement>("viewer-split-divider");
            var right = mainBodyRoot.Q<VisualElement>("viewer-output");
            var ioIn = mainBodyRoot.Q<VisualElement>("io-tray-input");
            var ioEx = mainBodyRoot.Q<VisualElement>("io-tray-exports");
            var queue = mainBodyRoot.Q<VisualElement>("queue-sidebar");
            var spacer = mainBodyRoot.Q<VisualElement>("io-tray-queue-spacer");
            var viewerCol = mainBodyRoot.Q<VisualElement>("viewer-column");

            if (dualHost == null || left == null || div == null || right == null || ioIn == null || ioEx == null
                || queue == null || spacer == null || viewerCol == null)
                return null;

            var parametersShell = mainBodyRoot.Q<VisualElement>("parameters-rail-shell");
            var ioSplitRow = mainBodyRoot.Q<VisualElement>("io-tray-split-row");
            var ioFilesSpacer = mainBodyRoot.Q<VisualElement>("io-files-bar-queue-spacer");

            if (ioSplitRow == null)
                return null;

            return new HorizontalViewerIoSplitController(dualHost, left, div, right, ioIn, ioEx, queue, spacer, viewerCol,
                ioSplitRow, ioFilesSpacer, parametersShell);
        }
    }
}
