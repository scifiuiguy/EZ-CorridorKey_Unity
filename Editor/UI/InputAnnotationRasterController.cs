#nullable enable
using System;
using System.Collections.Generic;
using CorridorKey.Editor.UI.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// EZ parity: foreground (1) / background (2) brush on INPUT, raster overlay, <c>annotations.json</c> on clip root.
    /// Image coordinates: x left→right, y top→bottom (same as UI Toolkit &amp; EZ).
    /// </summary>
    public sealed class InputAnnotationRasterController : IDisposable
    {
        /// <summary>EZ <c>split_view.py</c> default <c>_brush_radius</c>.</summary>
        const float DefaultBrushRadius = 15f;

        /// <summary>EZ <c>split_view.py</c> brush resize clamp.</summary>
        const float MinBrushRadius = 2f;

        const float MaxBrushRadius = 200f;

        /// <summary>EZ: 2 image pixels per vertical display pixel while Shift+drag resizing.</summary>
        const float BrushResizeImagePixelsPerDisplayPixel = 2f;

        /// <summary>EZ <c>annotation_overlay.py</c> cursor outline (fg).</summary>
        static readonly Color FgCursorOutline = new Color(44f / 255f, 195f / 255f, 80f / 255f, 200f / 255f);

        /// <summary>EZ <c>_BG_CURSOR</c>.</summary>
        static readonly Color BgCursorOutline = new Color(209f / 255f, 0f, 0f, 200f / 255f);

        readonly VisualElement _root;
        readonly Func<string?> _getClipRoot;
        readonly Func<Image?> _getPlateImage;
        readonly Func<int> _getStemIndex;

        ViewerPlayheadStripController? _playhead;

        Image? _overlay;
        VisualElement? _brushCursor;
        Label? _brushResizeLabel;
        Texture2D? _overlayTex;
        Color32[]? _scratch;

        Dictionary<int, List<AnnotationStrokeData>> _byStem = new();
        string? _loadedClipRoot;

        /// <summary>Fired after saved stroke data in <c>annotations.json</c> may have changed (load, save, undo, clear).</summary>
        public event Action? AnnotationPersistenceChanged;

        string? _brushMode;
        float _brushRadius = DefaultBrushRadius;
        AnnotationStrokeData? _currentStroke;
        bool _drawing;
        int _pointerId = -1;
        int _strokeStemIndex = -1;

        bool _resizingBrush;
        float _resizeStartPlateY;
        float _resizeStartRadius;
        int _resizePointerId = -1;

        public InputAnnotationRasterController(
            VisualElement root,
            Func<string?> getClipRoot,
            Func<int> getStemIndex,
            Func<Image?> getPlateImage)
        {
            _root = root;
            _getClipRoot = getClipRoot;
            _getStemIndex = getStemIndex;
            _getPlateImage = getPlateImage;

            EditorApplication.update += OnEditorUpdate;
        }

        public void SetPlayheadStrip(ViewerPlayheadStripController? strip)
        {
            if (_playhead != null)
                _playhead.FrameChanged -= OnPlayheadFrameChanged;
            _playhead = strip;
            if (_playhead != null)
                _playhead.FrameChanged += OnPlayheadFrameChanged;
        }

        public void SetClipRoot(string? clipRoot)
        {
            if (string.Equals(_loadedClipRoot, clipRoot, StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrEmpty(_loadedClipRoot) && _drawing)
                CommitOrCancelStroke(saveToDisk: true);

            if (_resizingBrush)
                EndBrushResize();

            _loadedClipRoot = clipRoot;
            _byStem.Clear();
            if (!string.IsNullOrEmpty(clipRoot))
                _byStem = AnnotationJsonIo.Load(clipRoot);

            RefreshOverlayRaster();
            NotifyAnnotationPersistenceChanged();
        }

        public void Dispose()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (_playhead != null)
                _playhead.FrameChanged -= OnPlayheadFrameChanged;

            if (_resizingBrush)
                EndBrushResize();
            TeardownOverlay();
        }

        /// <summary>Call after INPUT plate texture is replaced (e.g. playhead frame load).</summary>
        public void NotifyInputPlateUpdated()
        {
            EnsureOverlayUnderPlate();
            RefreshOverlayRaster();
        }

        void OnPlayheadFrameChanged(int stem)
        {
            if (_drawing)
                CommitOrCancelStroke(saveToDisk: true);
            if (_resizingBrush)
                EndBrushResize();
            RefreshOverlayRaster();
        }

        void OnEditorUpdate()
        {
            if (_overlay != null)
                return;
            var plate = _getPlateImage();
            if (plate?.image is Texture2D)
                EnsureOverlayUnderPlate();
        }

        void EnsureOverlayUnderPlate()
        {
            var plate = _getPlateImage();
            if (plate == null || plate.image is not Texture2D tex || tex.width <= 0 || tex.height <= 0)
            {
                TeardownOverlay();
                return;
            }

            if (_overlay != null && _overlay.parent == plate)
            {
                EnsureOverlayTexture(tex.width, tex.height);
                EnsureBrushCursorOnPlate(plate);
                EnsureBrushResizeLabelOnPlate(plate);
                UpdateCursorHint();
                return;
            }

            TeardownOverlay();
            plate.style.position = Position.Relative;

            _overlay = new Image
            {
                name = "viewer-input-annotation-overlay",
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
            };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.RegisterCallback<PointerDownEvent>(OnOverlayPointerDown, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerMoveEvent>(OnOverlayPointerMoveBrush, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerEnterEvent>(OnOverlayPointerEnter, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerLeaveEvent>(OnOverlayPointerLeave, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerUpEvent>(OnOverlayPointerUp, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<PointerCaptureOutEvent>(OnOverlayPointerCaptureOut, TrickleDown.TrickleDown);

            plate.Add(_overlay);
            EnsureBrushCursorOnPlate(plate);
            EnsureBrushResizeLabelOnPlate(plate);
            EnsureOverlayTexture(tex.width, tex.height);
            UpdateCursorHint();
        }

        void TeardownOverlay()
        {
            if (_overlay != null)
            {
                _overlay.UnregisterCallback<PointerDownEvent>(OnOverlayPointerDown, TrickleDown.TrickleDown);
                _overlay.UnregisterCallback<PointerMoveEvent>(OnOverlayPointerMoveBrush, TrickleDown.TrickleDown);
                _overlay.UnregisterCallback<PointerEnterEvent>(OnOverlayPointerEnter, TrickleDown.TrickleDown);
                _overlay.UnregisterCallback<PointerLeaveEvent>(OnOverlayPointerLeave, TrickleDown.TrickleDown);
                _overlay.UnregisterCallback<PointerUpEvent>(OnOverlayPointerUp, TrickleDown.TrickleDown);
                _overlay.UnregisterCallback<PointerCaptureOutEvent>(OnOverlayPointerCaptureOut, TrickleDown.TrickleDown);
                _overlay.RemoveFromHierarchy();
                _overlay = null;
            }

            if (_brushCursor != null)
            {
                _brushCursor.RemoveFromHierarchy();
                _brushCursor = null;
            }

            if (_brushResizeLabel != null)
            {
                _brushResizeLabel.RemoveFromHierarchy();
                _brushResizeLabel = null;
            }

            if (_overlayTex != null)
            {
                UnityEngine.Object.DestroyImmediate(_overlayTex);
                _overlayTex = null;
            }

            _scratch = null;
        }

        void EnsureOverlayTexture(int w, int h)
        {
            if (_overlayTex != null && _overlayTex.width == w && _overlayTex.height == h && _scratch != null)
                return;

            if (_overlayTex != null)
                UnityEngine.Object.DestroyImmediate(_overlayTex);

            _overlayTex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CorridorKey-InputAnnotationOverlay",
            };
            _scratch = new Color32[w * h];
            if (_overlay != null)
                _overlay.image = _overlayTex;
        }

        /// <summary>
        /// True when any stem has saved strokes (not counting an in-progress stroke).</summary>
        public bool HasAnyAnnotations()
        {
            foreach (var kv in _byStem)
            {
                if (kv.Value is { Count: > 0 })
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Remove all strokes for the current clip from memory and disk (<c>annotations.json</c> deleted when empty).
        /// </summary>
        public void ClearAllAnnotations()
        {
            if (_resizingBrush)
                EndBrushResize();

            if (_drawing)
            {
                var pid = _pointerId;
                if (_overlay != null && pid >= 0 && _overlay.HasPointerCapture(pid))
                    _overlay.ReleasePointer(pid);
                CommitOrCancelStroke(saveToDisk: false);
            }

            _byStem.Clear();
            var clip = _getClipRoot();
            if (!string.IsNullOrEmpty(clip))
                AnnotationJsonIo.Save(clip, _byStem);
            RefreshOverlayRaster();
            NotifyAnnotationPersistenceChanged();
        }

        /// <summary>EZ <c>AnnotationMixin._undo_annotation</c>: Ctrl+Z pops last stroke on current frame while brush is active.</summary>
        public bool TryUndoLastStroke()
        {
            if (_resizingBrush)
                return false;
            var clip = _getClipRoot();
            if (string.IsNullOrEmpty(clip))
                return false;
            if (string.IsNullOrEmpty(_brushMode))
                return false;

            if (_drawing)
            {
                var pid = _pointerId;
                if (_overlay != null && pid >= 0 && _overlay.HasPointerCapture(pid))
                    _overlay.ReleasePointer(pid);
                CommitOrCancelStroke(saveToDisk: false);
                return true;
            }

            var stem = _getStemIndex();
            if (!_byStem.TryGetValue(stem, out var list) || list.Count == 0)
                return false;

            list.RemoveAt(list.Count - 1);
            if (list.Count == 0)
                _byStem.Remove(stem);

            AnnotationJsonIo.Save(clip, _byStem);
            RefreshOverlayRaster();
            NotifyAnnotationPersistenceChanged();
            return true;
        }

        void NotifyAnnotationPersistenceChanged()
        {
            AnnotationPersistenceChanged?.Invoke();
        }

        /// <summary>
        /// UITK <see cref="KeyDownEvent"/> on the window root with <see cref="TrickleDown"/> so 1/2/Ctrl+Z work
        /// while focus is on the parameters rail, queue, etc. (IMGUI <see cref="EditorWindow.OnGUI"/> does not receive
        /// those keys when a UITK control has keyboard focus).
        /// </summary>
        public bool TryHandleKeyDownEvent(KeyDownEvent evt)
        {
            var focusTarget = evt.target as Focusable;
            var command = evt.commandKey || (evt.modifiers & EventModifiers.Command) != 0;
            return TryHandleBindingKeys(evt.keyCode, evt.ctrlKey, command, focusTarget);
        }

        /// <summary>
        /// IMGUI fallback when <see cref="EventType.KeyDown"/> is delivered to the window (mouse over or keyboard focus).
        /// </summary>
        public bool TryHandleImGuiEditorKey(Event e)
        {
            if (e.type != EventType.KeyDown)
                return false;
            var focused = _root.panel?.focusController?.focusedElement;
            return TryHandleBindingKeys(e.keyCode, e.control, e.command, focused);
        }

        bool TryHandleBindingKeys(KeyCode keyCode, bool control, bool command, Focusable? focusTarget)
        {
            if (IsUnderTextField(focusTarget))
                return false;
            if (control || command)
            {
                if (keyCode == KeyCode.Z)
                    return TryUndoLastStroke();
            }

            if (keyCode == KeyCode.Alpha1 || keyCode == KeyCode.Keypad1)
            {
                ToggleBrush("fg");
                return true;
            }

            if (keyCode == KeyCode.Alpha2 || keyCode == KeyCode.Keypad2)
            {
                ToggleBrush("bg");
                return true;
            }

            return false;
        }

        void ToggleBrush(string type)
        {
            if (_brushMode == type)
                _brushMode = null;
            else
                _brushMode = type;
            UpdateCursorHint();
        }

        void UpdateCursorHint()
        {
            var on = !string.IsNullOrEmpty(_brushMode);
            if (_overlay != null)
                _overlay.pickingMode = on ? PickingMode.Position : PickingMode.Ignore;
            if (!on)
            {
                if (_resizingBrush)
                    EndBrushResize();
                HideBrushCursor();
                HideResizeLabel();
            }
        }

        void EnsureBrushCursorOnPlate(Image plate)
        {
            if (_brushCursor != null && _brushCursor.parent == plate)
                return;

            if (_brushCursor != null)
            {
                _brushCursor.RemoveFromHierarchy();
                _brushCursor = null;
            }

            _brushCursor = new VisualElement
            {
                name = "viewer-input-brush-cursor",
                pickingMode = PickingMode.Ignore,
            };
            _brushCursor.style.position = Position.Absolute;
            _brushCursor.style.display = DisplayStyle.None;
            _brushCursor.style.backgroundColor = Color.clear;
            const float bw = 1.5f;
            _brushCursor.style.borderTopWidth = bw;
            _brushCursor.style.borderBottomWidth = bw;
            _brushCursor.style.borderLeftWidth = bw;
            _brushCursor.style.borderRightWidth = bw;
            var half = new StyleLength(new Length(50f, LengthUnit.Percent));
            _brushCursor.style.borderTopLeftRadius = half;
            _brushCursor.style.borderTopRightRadius = half;
            _brushCursor.style.borderBottomLeftRadius = half;
            _brushCursor.style.borderBottomRightRadius = half;

            plate.Add(_brushCursor);
        }

        void EnsureBrushResizeLabelOnPlate(Image plate)
        {
            if (_brushResizeLabel != null && _brushResizeLabel.parent == plate)
                return;

            if (_brushResizeLabel != null)
            {
                _brushResizeLabel.RemoveFromHierarchy();
                _brushResizeLabel = null;
            }

            _brushResizeLabel = new Label
            {
                name = "viewer-input-brush-resize-label",
                pickingMode = PickingMode.Ignore,
                text = "",
            };
            _brushResizeLabel.style.position = Position.Absolute;
            _brushResizeLabel.style.display = DisplayStyle.None;
            _brushResizeLabel.style.width = 60f;
            _brushResizeLabel.style.fontSize = 10;
            _brushResizeLabel.style.color = new Color(0.92f, 0.92f, 0.92f);
            _brushResizeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _brushResizeLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.59f);
            _brushResizeLabel.style.paddingLeft = 4f;
            _brushResizeLabel.style.paddingRight = 4f;
            _brushResizeLabel.style.paddingTop = 2f;
            _brushResizeLabel.style.paddingBottom = 2f;
            _brushResizeLabel.style.borderTopLeftRadius = 2f;
            _brushResizeLabel.style.borderTopRightRadius = 2f;
            _brushResizeLabel.style.borderBottomLeftRadius = 2f;
            _brushResizeLabel.style.borderBottomRightRadius = 2f;

            plate.Add(_brushResizeLabel);
        }

        void HideBrushCursor()
        {
            if (_brushCursor == null)
                return;
            _brushCursor.style.display = DisplayStyle.None;
        }

        void HideResizeLabel()
        {
            if (_brushResizeLabel == null)
                return;
            _brushResizeLabel.style.display = DisplayStyle.None;
        }

        void EndBrushResize()
        {
            if (!_resizingBrush)
                return;
            _resizingBrush = false;
            var id = _resizePointerId;
            _resizePointerId = -1;
            if (_overlay != null && id >= 0 && _overlay.HasPointerCapture(id))
                _overlay.ReleasePointer(id);
            HideResizeLabel();
        }

        void OnOverlayPointerEnter(PointerEnterEvent evt)
        {
            if (string.IsNullOrEmpty(_brushMode))
                return;
            UpdateBrushCursorAtPlateLocal(evt.localPosition);
        }

        void OnOverlayPointerLeave(PointerLeaveEvent _)
        {
            HideBrushCursor();
        }

        void OnOverlayPointerMoveBrush(PointerMoveEvent evt)
        {
            if (_resizingBrush && evt.pointerId == _resizePointerId)
            {
                var deltaY = _resizeStartPlateY - evt.localPosition.y;
                _brushRadius = Mathf.Clamp(
                    _resizeStartRadius + deltaY * BrushResizeImagePixelsPerDisplayPixel,
                    MinBrushRadius,
                    MaxBrushRadius);
                if (!string.IsNullOrEmpty(_brushMode))
                    UpdateBrushCursorAtPlateLocal(evt.localPosition);
                evt.StopPropagation();
                return;
            }

            if (!string.IsNullOrEmpty(_brushMode))
                UpdateBrushCursorAtPlateLocal(evt.localPosition);

            if (!_drawing || _currentStroke == null || evt.pointerId != _pointerId)
                return;
            var plate = _getPlateImage();
            if (plate?.image is not Texture2D tex)
                return;

            if (!TryImagePixelFromPlateLocal(plate, tex, evt.localPosition, out var ix, out var iy))
                return;

            _currentStroke.px.Add(ix);
            _currentStroke.py.Add(iy);
            RefreshOverlayRaster();
            evt.StopPropagation();
        }

        void UpdateBrushCursorAtPlateLocal(Vector2 localInPlate)
        {
            if (_brushCursor == null || string.IsNullOrEmpty(_brushMode))
                return;
            var plate = _getPlateImage();
            if (plate?.image is not Texture2D tex)
                return;

            var lw = plate.layout.width;
            var lh = plate.layout.height;
            if (lw < 0.5f || lh < 0.5f)
                return;

            var fitted = AbComparisonPreviewMath.ComputeAspectFitRect(new Vector2(lw, lh), new Vector2(tex.width, tex.height));
            var lx = localInPlate.x - fitted.x;
            var ly = localInPlate.y - fitted.y;
            if (lx < -0.001f || ly < -0.001f || lx > fitted.width + 0.001f || ly > fitted.height + 0.001f)
            {
                HideBrushCursor();
                if (_resizingBrush)
                    HideResizeLabel();
                return;
            }

            var displayRadius = _brushRadius * (fitted.width / tex.width);
            displayRadius = Mathf.Max(1f, displayRadius);
            var d = displayRadius * 2f;

            var c = _brushMode == "fg" ? FgCursorOutline : BgCursorOutline;
            _brushCursor.style.borderTopColor = c;
            _brushCursor.style.borderBottomColor = c;
            _brushCursor.style.borderLeftColor = c;
            _brushCursor.style.borderRightColor = c;

            _brushCursor.style.width = d;
            _brushCursor.style.height = d;
            _brushCursor.style.left = localInPlate.x - displayRadius;
            _brushCursor.style.top = localInPlate.y - displayRadius;

            _brushCursor.style.display = DisplayStyle.Flex;
            _brushCursor.MarkDirtyRepaint();

            if (_resizingBrush && _brushResizeLabel != null)
            {
                _brushResizeLabel.text = $"{Mathf.RoundToInt(_brushRadius)}px";
                _brushResizeLabel.style.left = localInPlate.x - 30f;
                _brushResizeLabel.style.top = localInPlate.y + displayRadius + 4f;
                _brushResizeLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                HideResizeLabel();
            }
        }

        void OnOverlayPointerDown(PointerDownEvent evt)
        {
            if (string.IsNullOrEmpty(_brushMode))
                return;
            var clip = _getClipRoot();
            if (string.IsNullOrEmpty(clip))
                return;
            var plate = _getPlateImage();
            if (plate?.image is not Texture2D tex || _overlay == null)
                return;

            if (!TryImagePixelFromPlateLocal(plate, tex, evt.localPosition, out var ix, out var iy))
                return;

            if (evt.shiftKey && evt.button == 0)
            {
                _resizingBrush = true;
                _resizeStartPlateY = evt.localPosition.y;
                _resizeStartRadius = _brushRadius;
                _resizePointerId = evt.pointerId;
                _overlay.CapturePointer(evt.pointerId);
                UpdateBrushCursorAtPlateLocal(evt.localPosition);
                evt.StopPropagation();
                return;
            }

            _drawing = true;
            _pointerId = evt.pointerId;
            _strokeStemIndex = _getStemIndex();
            _overlay.CapturePointer(evt.pointerId);

            _currentStroke = AnnotationStrokeData.Create(_brushMode == "fg" ? "fg" : "bg", _brushRadius);
            _currentStroke.px.Add(ix);
            _currentStroke.py.Add(iy);
            RefreshOverlayRaster();
            evt.StopPropagation();
        }

        void OnOverlayPointerUp(PointerUpEvent evt)
        {
            if (_resizingBrush && evt.pointerId == _resizePointerId)
            {
                EndBrushResize();
                evt.StopPropagation();
                return;
            }

            if (!_drawing || evt.pointerId != _pointerId)
                return;
            if (_overlay != null && _overlay.HasPointerCapture(evt.pointerId))
                _overlay.ReleasePointer(evt.pointerId);
            _pointerId = -1;
            CommitOrCancelStroke(saveToDisk: true);
            evt.StopPropagation();
        }

        void OnOverlayPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_resizingBrush && evt.pointerId == _resizePointerId)
            {
                _resizingBrush = false;
                _resizePointerId = -1;
                HideResizeLabel();
                return;
            }

            if (_drawing)
                CommitOrCancelStroke(saveToDisk: true);
        }

        void CommitOrCancelStroke(bool saveToDisk)
        {
            if (!_drawing && _currentStroke == null)
                return;

            _drawing = false;
            _pointerId = -1;

            var stem = _strokeStemIndex >= 0 ? _strokeStemIndex : _getStemIndex();
            _strokeStemIndex = -1;
            var clip = _getClipRoot();

            if (saveToDisk && _currentStroke != null && _currentStroke.px.Count > 0
                && !string.IsNullOrEmpty(clip))
            {
                if (!_byStem.TryGetValue(stem, out var list))
                {
                    list = new List<AnnotationStrokeData>();
                    _byStem[stem] = list;
                }

                list.Add(_currentStroke);
                AnnotationJsonIo.Save(clip, _byStem);
                NotifyAnnotationPersistenceChanged();
            }

            _currentStroke = null;
            RefreshOverlayRaster();
        }

        void RefreshOverlayRaster()
        {
            EnsureOverlayUnderPlate();
            var plate = _getPlateImage();
            if (plate?.image is not Texture2D tex || _scratch == null || _overlayTex == null)
                return;

            var stem = _getStemIndex();
            _byStem.TryGetValue(stem, out var saved);

            var merged = new List<AnnotationStrokeData>();
            if (saved != null)
                merged.AddRange(saved);
            if (_currentStroke != null && _currentStroke.px.Count > 0)
                merged.Add(_currentStroke);

            AnnotationRasterUtil.RasterizeStrokes(_scratch, tex.width, tex.height, merged);

            _overlayTex.SetPixels32(_scratch);
            _overlayTex.Apply(false, false);
            if (_overlay != null)
                _overlay.MarkDirtyRepaint();
        }

        static bool IsUnderTextField(Focusable? f)
        {
            for (VisualElement? e = f as VisualElement; e != null; e = e.parent)
            {
                if (e is TextField)
                    return true;
            }

            return false;
        }

        static bool TryImagePixelFromPlateLocal(Image plate, Texture2D tex, Vector2 localInPlate, out float ix, out float iy)
        {
            ix = iy = 0f;
            var lw = plate.layout.width;
            var lh = plate.layout.height;
            if (lw < 0.5f || lh < 0.5f)
                return false;

            var fitted = AbComparisonPreviewMath.ComputeAspectFitRect(new Vector2(lw, lh), new Vector2(tex.width, tex.height));
            var lx = localInPlate.x - fitted.x;
            var ly = localInPlate.y - fitted.y;
            if (lx < -0.001f || ly < -0.001f || lx > fitted.width + 0.001f || ly > fitted.height + 0.001f)
                return false;

            ix = (lx / fitted.width) * tex.width;
            iy = (ly / fitted.height) * tex.height;
            ix = Mathf.Clamp(ix, 0f, tex.width - 0.001f);
            iy = Mathf.Clamp(iy, 0f, tex.height - 0.001f);
            return true;
        }
    }
}
