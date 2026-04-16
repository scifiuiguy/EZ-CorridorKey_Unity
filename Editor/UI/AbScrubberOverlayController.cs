#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    public sealed class AbScrubberOverlayController : IDisposable
    {
        public event Action<Vector2, float>? SplitChanged;

        const float LineLengthMultiplier = 3f;
        const float BadgeSizePx = 16f;
        const float MoveRadiusPx = 20f;
        const float LineHoverTolerancePx = 8f;

        readonly VisualElement _overlay;
        readonly VisualElement _line;
        readonly VisualElement _badge;
        readonly VisualElement _comparisonHost;
        readonly VisualElement _inputPane;
        readonly VisualElement _outputPane;
        readonly VisualElement _splitDivider;

        bool _enabled;
        bool _dragging;
        int _activePointerId;
        DragMode _dragMode;
        float _rotateDragOffsetDeg;
        Vector2 _moveDragStartPointerPx;
        Vector2 _moveDragStartMidpointPx;

        Vector2 _midpointNormalized = new Vector2(0.5f, 0.5f);
        float _angleDeg = 90f;

        enum DragMode
        {
            None,
            MoveMidpoint,
            Rotate
        }

        public AbScrubberOverlayController(VisualElement root)
        {
            _overlay = root.Q<VisualElement>("viewer-ab-overlay")
                ?? throw new InvalidOperationException("Missing viewer-ab-overlay.");
            _line = root.Q<VisualElement>("viewer-ab-line")
                ?? throw new InvalidOperationException("Missing viewer-ab-line.");
            _badge = root.Q<VisualElement>("viewer-ab-badge")
                ?? throw new InvalidOperationException("Missing viewer-ab-badge.");
            _comparisonHost = root.Q<VisualElement>("viewer-ab-comparison-host")
                ?? throw new InvalidOperationException("Missing viewer-ab-comparison-host.");
            _inputPane = root.Q<VisualElement>("viewer-input")
                ?? throw new InvalidOperationException("Missing viewer-input.");
            _outputPane = root.Q<VisualElement>("viewer-output")
                ?? throw new InvalidOperationException("Missing viewer-output.");
            _splitDivider = root.Q<VisualElement>("viewer-split-divider")
                ?? throw new InvalidOperationException("Missing viewer-split-divider.");
            _overlay.RegisterCallback<GeometryChangedEvent>(OnOverlayGeometryChanged);
            _overlay.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _overlay.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _overlay.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _overlay.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            _overlay.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            UpdateOverlayVisuals();
            SetEnabled(false);
        }

        public Rect BadgeWorldBounds => _badge.worldBound;

        public Vector2 MidpointNormalized => _midpointNormalized;

        public float AngleDeg => _angleDeg;

        public void Dispose()
        {
            _overlay.UnregisterCallback<GeometryChangedEvent>(OnOverlayGeometryChanged);
            _overlay.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _overlay.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _overlay.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _overlay.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            _overlay.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _comparisonHost.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            _overlay.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            _inputPane.style.display = enabled ? DisplayStyle.None : DisplayStyle.Flex;
            _outputPane.style.display = enabled ? DisplayStyle.None : DisplayStyle.Flex;
            _splitDivider.style.display = enabled ? DisplayStyle.None : DisplayStyle.Flex;
            if (!enabled)
                EndDrag();
            SplitChanged?.Invoke(_midpointNormalized, _angleDeg);
        }

        void OnOverlayGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateOverlayVisuals();
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (!_enabled || evt.button != 0)
                return;

            _badge.RemoveFromClassList("corridor-key-ab-badge--hover");
            _line.RemoveFromClassList("corridor-key-ab-line--hover");

            _dragging = true;
            _activePointerId = evt.pointerId;
            _overlay.CapturePointer(_activePointerId);

            var local = new Vector2(evt.localPosition.x, evt.localPosition.y);
            var centerPx = MidpointToPixels();
            var distance = Vector2.Distance(local, centerPx);
            _dragMode = distance <= MoveRadiusPx ? DragMode.MoveMidpoint : DragMode.Rotate;
            if (_dragMode == DragMode.MoveMidpoint)
            {
                _moveDragStartPointerPx = local;
                _moveDragStartMidpointPx = centerPx;
                _badge.AddToClassList("corridor-key-ab-badge--dragging");
            }
            if (_dragMode == DragMode.Rotate)
            {
                var pointerAngleDeg = PointerToLineAngle(local, centerPx);
                _rotateDragOffsetDeg = Mathf.DeltaAngle(pointerAngleDeg, _angleDeg);
                _line.AddToClassList("corridor-key-ab-line--rotating");
            }
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_enabled)
                return;

            var pos = new Vector2(evt.localPosition.x, evt.localPosition.y);
            if (!_dragging)
                UpdateHoverState(pos);

            if (!_dragging || evt.pointerId != _activePointerId)
                return;

            if (_dragMode == DragMode.MoveMidpoint)
            {
                var width = Mathf.Max(1f, _overlay.layout.width);
                var height = Mathf.Max(1f, _overlay.layout.height);
                var dragDelta = pos - _moveDragStartPointerPx;
                var moveAxis = GetLineNormalUnitVector(_angleDeg);
                var projectedDistance = Vector2.Dot(dragDelta, moveAxis);
                var midpointPx = _moveDragStartMidpointPx + (moveAxis * projectedDistance);
                _midpointNormalized = new Vector2(
                    Mathf.Clamp01(midpointPx.x / width),
                    Mathf.Clamp01(midpointPx.y / height));
            }
            else if (_dragMode == DragMode.Rotate)
            {
                var center = MidpointToPixels();
                var v = pos - center;
                if (v.sqrMagnitude > 1f)
                {
                    _angleDeg = NormalizeDeg(PointerToLineAngle(pos, center) + _rotateDragOffsetDeg);
                    _angleDeg = NormalizeDeg(_angleDeg);
                    if (evt.shiftKey)
                        _angleDeg = SnapToNearestAxis(_angleDeg);
                }
            }

            UpdateOverlayVisuals();
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
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

        void OnPointerLeave(PointerLeaveEvent evt)
        {
            _badge.RemoveFromClassList("corridor-key-ab-badge--hover");
            _line.RemoveFromClassList("corridor-key-ab-line--hover");
            if (!_dragging)
                _dragMode = DragMode.None;
        }

        void EndDrag()
        {
            _dragging = false;
            _dragMode = DragMode.None;
            _badge.RemoveFromClassList("corridor-key-ab-badge--dragging");
            _line.RemoveFromClassList("corridor-key-ab-line--rotating");
            _badge.RemoveFromClassList("corridor-key-ab-badge--hover");
            _line.RemoveFromClassList("corridor-key-ab-line--hover");
            try
            {
                _overlay.ReleasePointer(_activePointerId);
            }
            catch
            {
                // ignore
            }
        }

        void UpdateOverlayVisuals()
        {
            if (_overlay.layout.width <= 0f || _overlay.layout.height <= 0f)
                return;

            if (!IsFinite(_angleDeg))
                _angleDeg = 90f;

            var center = MidpointToPixels();
            if (!IsFinite(center.x) || !IsFinite(center.y))
                return;

            var maxDim = Mathf.Max(_overlay.layout.width, _overlay.layout.height) * LineLengthMultiplier;
            if (!IsFinite(maxDim) || maxDim <= 0f)
                return;

            var lineThickness = _line.resolvedStyle.height;
            if (!IsFinite(lineThickness) || lineThickness <= 0f)
                lineThickness = 2f;

            _line.style.width = maxDim;
            _line.style.left = center.x - (maxDim * 0.5f);
            _line.style.top = center.y - (lineThickness * 0.5f);
            _line.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0f);
            if (IsFinite(_angleDeg))
                _line.transform.rotation = Quaternion.Euler(0f, 0f, _angleDeg);

            _badge.style.left = center.x - (BadgeSizePx * 0.5f);
            _badge.style.top = center.y - (BadgeSizePx * 0.5f);
            _badge.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0f);
            if (IsFinite(_angleDeg))
                _badge.transform.rotation = Quaternion.Euler(0f, 0f, _angleDeg - 90f);

            SplitChanged?.Invoke(_midpointNormalized, _angleDeg);
        }

        Vector2 MidpointToPixels()
        {
            var width = Mathf.Max(1f, _overlay.layout.width);
            var height = Mathf.Max(1f, _overlay.layout.height);
            return new Vector2(_midpointNormalized.x * width, _midpointNormalized.y * height);
        }

        void UpdateHoverState(Vector2 pointerPx)
        {
            var center = MidpointToPixels();
            var distanceToCenter = Vector2.Distance(pointerPx, center);
            var isBadgeHover = distanceToCenter <= MoveRadiusPx;
            var isLineHover = !isBadgeHover && DistanceToInfiniteLine(pointerPx, center, _angleDeg) <= LineHoverTolerancePx;

            if (isBadgeHover)
                _badge.AddToClassList("corridor-key-ab-badge--hover");
            else
                _badge.RemoveFromClassList("corridor-key-ab-badge--hover");

            if (isLineHover)
                _line.AddToClassList("corridor-key-ab-line--hover");
            else
                _line.RemoveFromClassList("corridor-key-ab-line--hover");
        }

        static float NormalizeDeg(float deg)
        {
            while (deg < 0f)
                deg += 360f;
            while (deg >= 360f)
                deg -= 360f;
            return deg;
        }

        static float SnapToNearestAxis(float deg)
        {
            var candidates = new[] { 0f, 90f, 180f, 270f };
            var best = candidates[0];
            var bestDelta = Mathf.Abs(Mathf.DeltaAngle(deg, best));
            for (var i = 1; i < candidates.Length; i++)
            {
                var d = Mathf.Abs(Mathf.DeltaAngle(deg, candidates[i]));
                if (d < bestDelta)
                {
                    bestDelta = d;
                    best = candidates[i];
                }
            }
            return best;
        }

        static float PointerToLineAngle(Vector2 pointerPos, Vector2 center)
        {
            var v = pointerPos - center;
            // 0 degrees is horizontal; +90 is vertical.
            return NormalizeDeg(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 90f);
        }

        static Vector2 GetLineNormalUnitVector(float lineAngleDeg)
        {
            var radians = (lineAngleDeg - 90f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        static float DistanceToInfiniteLine(Vector2 pointPx, Vector2 lineCenterPx, float lineAngleDeg)
        {
            var lineDirection = GetLineDirectionUnitVector(lineAngleDeg);
            var pointDelta = pointPx - lineCenterPx;
            var projected = Vector2.Dot(pointDelta, lineDirection) * lineDirection;
            return (pointDelta - projected).magnitude;
        }

        static Vector2 GetLineDirectionUnitVector(float lineAngleDeg)
        {
            var radians = (lineAngleDeg - 90f) * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
