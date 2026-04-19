#nullable enable
using System.Collections.Generic;
using System.IO;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor.Backend;
using CorridorKey.Editor.Integration;
using CorridorKey.Editor.UI;
using CorridorKey.Editor.UI.Presenters;
using CorridorKey.Editor.ViewModels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

namespace CorridorKey.Editor
{
    // Touch: nudge Unity to recompile Editor scripts if the domain didn’t reload.

    /// <summary>
    /// Main CorridorKey Editor window (EZ parity: <c>ui/main_window.py</c>). Queue, viewers, and parameters
    /// will live here; <see cref="RunBackendHealthCheck"/> is one action that talks to the Python bridge.
    /// </summary>
    public sealed class CorridorKeyWindow : EditorWindow
    {
        public const string WindowTitle = "EZ CorridorKey";

        /// <summary>Minimum window size (dual viewers + parameters tab + 280px content).</summary>
        /// <summary>Includes playhead strip (~48px) below dual viewers.</summary>
        public static readonly Vector2 MinWindowSize = new Vector2(904f, 490f);

        ProcessBackendClient? _backend;
        CorridorKeySessionVm? _session;
        QueueSidebarController? _queueSidebar;
        QueuePresenter? _queuePresenter;
        ParametersSidebarController? _parametersSidebar;
        InferenceSectionController? _inferenceSectionController;
        OutputPerformanceSectionController? _outputPerformanceSectionController;
        HorizontalViewerIoSplitController? _viewerIoSplit;
        VerticalViewerIoTraySplitController? _viewerIoTraySplit;
        IoFilesBarToggleController? _ioFilesBar;
        AbScrubberOverlayController? _abScrubberOverlay;
        SampleAbComparisonRenderer? _sampleAbComparisonRenderer;
        GpuAbComparisonRenderer? _gpuAbComparisonRenderer;
        GpuMeterHeaderController? _gpuMeterHeader;

        BiRefNetViewerIntegration? _biRefNetViewerIntegration;
        GvmViewerIntegration? _gvmViewerIntegration;
        TrackMaskIntegration? _trackMaskIntegration;

        DualViewerChromeController? _dualViewerChrome;

        ViewerPlayheadStripController? _playheadStrip;
        VisualElement? _viewerBody;
        List<string>? _plateFramePaths;
        string? _playheadClipRoot;
        Image? _playheadInputImage;
        Image? _playheadOutputImage;
        InputAnnotationRasterController? _inputAnnotations;
        ParametersRailController? _parametersRail;

        /// <summary>Unity may persist <see cref="EditorWindow"/> fields across domain reloads; reset in <see cref="CreateGUI"/>.</summary>
        [System.NonSerialized]
        bool _abPreviewEnabled;

        [System.NonSerialized]
        bool _abGpuPreviewEnabled = true;

        bool _immersiveViewersActive;
        bool _savedQueueExpanded;
        bool _savedParamsExpanded;
        bool _savedFilesExpanded;

        /// <summary>Debug: when true, advances the status bar by 10% every 0.5s. Private + NonSerialized so Unity does not persist it on the window (public fields on EditorWindow do).</summary>
        [System.NonSerialized]
        bool _testLoadAnimation;

        /// <summary>When UITK and IMGUI both see the same KeyDown, avoid double-toggling brush / undo.</summary>
        [System.NonSerialized]
        int _annotationHotkeyConsumedFrame = -1;

        [System.NonSerialized]
        KeyCode _annotationHotkeyConsumedKey = KeyCode.None;

        ProgressBar? _inferenceLoadProgress;
        IVisualElementScheduledItem? _inferenceLoadTestSchedule;
        float _testLoadAnimPercent;

        [MenuItem("Tools/CorridorKey/Open", false, 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<CorridorKeyWindow>(WindowTitle);
            window.minSize = MinWindowSize;
            window.Show();
        }

        void OnEnable()
        {
            _backend = new ProcessBackendClient();
            _backend.LogReceived += OnBackendLog;
            _backend.HealthReceived += OnBackendHealth;
            _session = new CorridorKeySessionVm(_backend);
        }

        /// <summary>Invoked from the window menu bar (File) or future toolbar controls.</summary>
        public void RunBackendHealthCheck()
        {
            _backend?.RequestHealthCheck();
        }

        void OnDisable()
        {
            StopInferenceLoadTestAnimation();

            _biRefNetViewerIntegration?.Dispose();
            _biRefNetViewerIntegration = null;
            _gvmViewerIntegration?.Dispose();
            _gvmViewerIntegration = null;
            _trackMaskIntegration?.Dispose();
            _trackMaskIntegration = null;
            TeardownInputAnnotations();
            _parametersRail = null;

            if (_playheadStrip != null)
            {
                _playheadStrip.FrameChanged -= OnPlayheadFrameChanged;
                _playheadStrip = null;
            }

            _playheadInputImage = null;
            _playheadOutputImage = null;
            _plateFramePaths = null;
            _playheadClipRoot = null;
            _viewerBody = null;

            if (_backend != null)
            {
                _backend.LogReceived -= OnBackendLog;
                _backend.HealthReceived -= OnBackendHealth;
                _backend.Dispose();
                _backend = null;
            }

            _session = null;
            _queueSidebar = null;
            if (_queuePresenter != null)
            {
                _queuePresenter.OnNewQueueCardCreated -= OnNewQueueCardCreated;
                _queuePresenter.Dispose();
                _queuePresenter = null;
            }
            _parametersSidebar = null;
            _inferenceSectionController = null;
            _outputPerformanceSectionController = null;
            _viewerIoSplit?.Dispose();
            _viewerIoSplit = null;
            _viewerIoTraySplit?.Dispose();
            _viewerIoTraySplit = null;
            _ioFilesBar = null;
            _abScrubberOverlay?.Dispose();
            _abScrubberOverlay = null;
            _sampleAbComparisonRenderer?.Dispose();
            _sampleAbComparisonRenderer = null;
            _gpuAbComparisonRenderer?.Dispose();
            _gpuAbComparisonRenderer = null;
            _gpuMeterHeader?.Dispose();
            _gpuMeterHeader = null;
        }

        void CreateGUI()
        {
            StopInferenceLoadTestAnimation();

            // Default: dual INPUT/OUTPUT panes. Without this, Unity-restored field values can leave A/B mode on
            // after a recompile while chrome state is rebuilt (mismatch → A/B view hides the two panes until toggled).
            _abPreviewEnabled = false;
            _abGpuPreviewEnabled = true;

            _viewerIoSplit?.Dispose();
            _viewerIoSplit = null;
            _viewerIoTraySplit?.Dispose();
            _viewerIoTraySplit = null;
            _ioFilesBar = null;
            _abScrubberOverlay?.Dispose();
            _abScrubberOverlay = null;
            _sampleAbComparisonRenderer?.Dispose();
            _sampleAbComparisonRenderer = null;
            _gpuAbComparisonRenderer?.Dispose();
            _gpuAbComparisonRenderer = null;
            _gpuMeterHeader?.Dispose();
            _gpuMeterHeader = null;
            _biRefNetViewerIntegration?.Dispose();
            _biRefNetViewerIntegration = null;
            _gvmViewerIntegration?.Dispose();
            _gvmViewerIntegration = null;
            _trackMaskIntegration?.Dispose();
            _trackMaskIntegration = null;
            TeardownInputAnnotations();
            if (_playheadStrip != null)
            {
                _playheadStrip.FrameChanged -= OnPlayheadFrameChanged;
                _playheadStrip = null;
            }

            _playheadInputImage = null;
            _playheadOutputImage = null;
            _plateFramePaths = null;
            _playheadClipRoot = null;
            _viewerBody = null;
            _parametersRail = null;
            _inferenceSectionController = null;
            _outputPerformanceSectionController = null;

            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingBottom = 8f;
            root.style.paddingLeft = 8f;
            root.userData = _session;
            root.AddToClassList("corridor-key-root");
            var corridorStyles = CorridorKeyUxmlPaths.LoadCorridorKeyStyleSheet();
            if (corridorStyles != null)
                root.styleSheets.Add(corridorStyles);

            var menuHost = new VisualElement { name = "menu-bar-host" };
            menuHost.style.flexShrink = 0;
            BuildMenuBar(menuHost);
            root.Add(menuHost);

            var body = CorridorKeyWindowLayout.BuildMainBodyColumn();
            body.style.flexGrow = 1;
            root.Add(body);

            _gpuMeterHeader = new GpuMeterHeaderController(body);

            var sidebar = body.Q<VisualElement>("queue-sidebar");
            var queueContent = body.Q<VisualElement>("queue-content");
            var queueTab = body.Q<VisualElement>("queue-tab");
            if (sidebar != null && queueContent != null && queueTab != null)
                _queueSidebar = new QueueSidebarController(sidebar, queueContent, queueTab);

            if (_queuePresenter != null)
            {
                _queuePresenter.OnNewQueueCardCreated -= OnNewQueueCardCreated;
                _queuePresenter.Dispose();
            }
            _queuePresenter = new QueuePresenter(body, _backend!);
            _queuePresenter.OnNewQueueCardCreated += OnNewQueueCardCreated;

            var parametersShell = body.Q<VisualElement>("parameters-rail-shell");
            var parametersRail = body.Q<VisualElement>("parameters-rail");
            var parametersTab = body.Q<VisualElement>("parameters-tab");
            if (parametersShell != null && parametersRail != null && parametersTab != null)
                _parametersSidebar = new ParametersSidebarController(parametersShell, parametersRail, parametersTab);

            if (parametersRail != null)
            {
                _inferenceSectionController = new InferenceSectionController(parametersRail);
                _outputPerformanceSectionController = new OutputPerformanceSectionController(parametersRail);
            }

            _viewerIoSplit = HorizontalViewerIoSplitController.TryAttach(body);
            _viewerIoTraySplit = VerticalViewerIoTraySplitController.TryAttach(body);
            _ioFilesBar = IoFilesBarToggleController.TryAttach(body, _viewerIoSplit, _viewerIoTraySplit);

            _dualViewerChrome = new DualViewerChromeController(body);
            _abScrubberOverlay = new AbScrubberOverlayController(body);
            _sampleAbComparisonRenderer = new SampleAbComparisonRenderer(body);
            _gpuAbComparisonRenderer = new GpuAbComparisonRenderer(body);
            if (_abScrubberOverlay != null && _sampleAbComparisonRenderer != null)
            {
                _abScrubberOverlay.SplitChanged += _sampleAbComparisonRenderer.SetSplit;
                _sampleAbComparisonRenderer.SetSplit(_abScrubberOverlay.MidpointNormalized, _abScrubberOverlay.AngleDeg);
            }
            if (_abScrubberOverlay != null && _gpuAbComparisonRenderer != null)
            {
                _abScrubberOverlay.SplitChanged += _gpuAbComparisonRenderer.SetSplit;
                _gpuAbComparisonRenderer.SetSplit(_abScrubberOverlay.MidpointNormalized, _abScrubberOverlay.AngleDeg);
            }

            if (_backend != null)
            {
                _trackMaskIntegration?.Dispose();
                _biRefNetViewerIntegration?.Dispose();
                _biRefNetViewerIntegration = new BiRefNetViewerIntegration(
                    _backend,
                    body,
                    _sampleAbComparisonRenderer,
                    _gpuAbComparisonRenderer,
                    _dualViewerChrome,
                    onQueueJobFailed: (vm, detail) => _queuePresenter?.FailJob(vm, detail));
                _gvmViewerIntegration = new GvmViewerIntegration(
                    _backend,
                    body,
                    _sampleAbComparisonRenderer,
                    _gpuAbComparisonRenderer,
                    _dualViewerChrome,
                    onQueueJobFailed: (vm, detail) => _queuePresenter?.FailJob(vm, detail));
                _trackMaskIntegration = new TrackMaskIntegration(
                    _backend,
                    body,
                    onSam2TrackSucceeded: ScheduleRefreshPlayheadForCurrentFrame,
                    onQueueJobFailed: (vm, detail) => _queuePresenter?.FailJob(vm, detail));
            }

            _dualViewerChrome.AbToggled += on =>
            {
                _abPreviewEnabled = on;
                ApplyAbPreviewMode();
            };
            _dualViewerChrome.AbRendererModeToggled += gpuOn =>
            {
                _abGpuPreviewEnabled = gpuOn;
                ApplyAbPreviewMode();
            };
            ApplyAbPreviewMode();

            _viewerBody = body;
            _playheadStrip = new ViewerPlayheadStripController(body);
            _playheadStrip.FrameChanged += OnPlayheadFrameChanged;
            _inputAnnotations = new InputAnnotationRasterController(
                body,
                () => _playheadClipRoot,
                () => _playheadStrip != null ? _playheadStrip.CurrentStemIndex : 0,
                () => _playheadInputImage);
            _inputAnnotations.SetPlayheadStrip(_playheadStrip);
            _parametersRail = new ParametersRailController(
                body,
                _biRefNetViewerIntegration,
                _gvmViewerIntegration,
                _trackMaskIntegration,
                _queuePresenter,
                () => _inputAnnotations?.HasAnyAnnotations() ?? false);
            _inputAnnotations.AnnotationPersistenceChanged += OnAnnotationPersistenceChanged;
            WirePlayheadFromDefaultTestClip();

            body.Q<Button>("status-run-selected")?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[CorridorKey] RUN SELECTED clicked."));

            body.Q<Button>("io-tray-input-reset-io")?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[CorridorKey] RESET IO clicked."));
            body.Q<Button>("io-tray-input-add")?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[CorridorKey] ADD clicked."));
            body.Q<Button>("queue-clear-button")?.RegisterCallback<ClickEvent>(_ =>
            {
                _queuePresenter?.Clear();
                Debug.Log("[CorridorKey] Queue > CLEAR clicked.");
            });

            _inferenceLoadProgress = body.Q<ProgressBar>("status-inference-loading");
            if (_inferenceLoadProgress != null)
            {
                _inferenceLoadProgress.lowValue = 0f;
                _inferenceLoadProgress.highValue = 100f;
                if (_testLoadAnimation)
                {
                    _inferenceLoadProgress.style.display = DisplayStyle.Flex;
                    _testLoadAnimPercent = 0f;
                    _inferenceLoadProgress.value = 0f;
                    _inferenceLoadTestSchedule = _inferenceLoadProgress.schedule.Execute(() =>
                    {
                        if (!_testLoadAnimation || _inferenceLoadProgress == null)
                            return;
                        _testLoadAnimPercent += 10f;
                        if (_testLoadAnimPercent > 100f)
                            _testLoadAnimPercent = 0f;
                        _inferenceLoadProgress.value = _testLoadAnimPercent;
                    }).Every(500);
                }
                else
                {
                    _inferenceLoadProgress.style.display = DisplayStyle.Flex;
                    _testLoadAnimPercent = 0f;
                    _inferenceLoadProgress.value = 0f;
                }
            }

            // Do not reset _immersiveViewersActive here: CreateGUI can run again while the window is open (e.g. after
            // script reload). Clearing the flag made the next SHIFT+SPACE take the "enter immersive" path again,
            // saving all-false expanded state and making "restore" appear to do nothing.
            if (_immersiveViewersActive)
            {
                _queueSidebar?.SetExpanded(false);
                _parametersSidebar?.SetExpanded(false);
                _ioFilesBar?.SetExpanded(false);
                _viewerIoSplit?.RefreshIoTrayLayout();
            }

            // Immersive hotkey: handle via UITK at root + trickle-down. Keyboard focus after the first toggle often
            // lives on UITK elements, so IMGUI OnGUI may not receive KeyDown for the second press — restore looked broken.
            root.RegisterCallback<KeyDownEvent>(OnRootAnnotationHotkeys, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnRootImmersiveHotkey, TrickleDown.TrickleDown);
        }

        void ApplyAbPreviewMode()
        {
            if (!_abPreviewEnabled)
            {
                _abScrubberOverlay?.SetEnabled(false);
                _sampleAbComparisonRenderer?.SetEnabled(false);
                _gpuAbComparisonRenderer?.SetEnabled(false);
                return;
            }

            // Scrubber first: host + overlay visible; then preview renderers. Defer NotifySplitChanged to next editor
            // frame so overlay has measured layout (UpdateOverlayVisuals also fires SplitChanged when geometry lands).
            _abScrubberOverlay?.SetEnabled(true);
            _sampleAbComparisonRenderer?.SetEnabled(!_abGpuPreviewEnabled);
            _gpuAbComparisonRenderer?.SetEnabled(_abGpuPreviewEnabled);
            EditorApplication.delayCall += NotifyAbSplitIfStillEnabled;
        }

        void NotifyAbSplitIfStillEnabled()
        {
            if (!_abPreviewEnabled)
                return;
            _abScrubberOverlay?.NotifySplitChanged();
        }

        void StopInferenceLoadTestAnimation()
        {
            _inferenceLoadTestSchedule?.Pause();
            _inferenceLoadTestSchedule = null;
            _inferenceLoadProgress = null;
        }

        void TeardownInputAnnotations()
        {
            if (_inputAnnotations == null)
                return;
            _inputAnnotations.AnnotationPersistenceChanged -= OnAnnotationPersistenceChanged;
            _inputAnnotations.Dispose();
            _inputAnnotations = null;
        }

        void OnAnnotationPersistenceChanged()
        {
            _parametersRail?.RefreshAnnotationGatedControls();
        }

        void ScheduleRefreshPlayheadForCurrentFrame()
        {
            EditorApplication.delayCall += RefreshPlayheadForCurrentFrame;
        }

        void RefreshPlayheadForCurrentFrame()
        {
            if (_playheadStrip == null)
                return;
            OnPlayheadFrameChanged(_playheadStrip.CurrentStemIndex);
        }

        void WirePlayheadFromDefaultTestClip()
        {
            if (_playheadStrip == null || _viewerBody == null)
                return;

            _plateFramePaths = null;
            _playheadClipRoot = null;

            if (!CorridorKeyDataPaths.TryGetDefaultTestClip(out var clipRoot, out var framesDir))
            {
                _playheadStrip.SetFrameCount(0);
                _inputAnnotations?.SetClipRoot(null);
                return;
            }

            if (!ClipPlateFramePaths.TryCollectSortedPlateFrames(framesDir, out var paths))
            {
                _playheadStrip.SetFrameCount(0);
                _inputAnnotations?.SetClipRoot(null);
                return;
            }

            _playheadClipRoot = clipRoot;
            _inputAnnotations?.SetClipRoot(clipRoot);
            _plateFramePaths = paths;
            _playheadStrip.SetFrameCount(paths.Count);
            _dualViewerChrome?.SelectViewModeById("alpha");
            _playheadStrip.SetStemIndex(0, notify: true);
        }

        /// <summary>
        /// Loads INPUT plate + OUTPUT for the scrubbed frame. OUTPUT currently uses <c>AlphaHint/{stem}.png</c>
        /// (assumes ALPHA / alpha-hint view). Later: resolve OUTPUT path from dual-viewer channel / session (comp, matte, etc.).
        /// Keeps A/B wipe sources in sync with the same paths (CPU + GPU renderers reload from disk).
        /// </summary>
        void OnPlayheadFrameChanged(int stemIndex)
        {
            if (_viewerBody == null || _plateFramePaths == null || _playheadClipRoot == null)
                return;
            if (stemIndex < 0 || stemIndex >= _plateFramePaths.Count)
                return;

            var platePath = _plateFramePaths[stemIndex];
            var alphaPath = ClipPlateFramePaths.GetAlphaHintPathForPlateFrame(_playheadClipRoot, platePath);

            var inTex = TextureFileLoader.LoadReadableFromFile(platePath);
            if (inTex == null)
            {
                Debug.LogWarning($"[CorridorKey] Playhead: could not load plate {platePath}");
                _sampleAbComparisonRenderer?.SetComparisonSourcesFromAbsoluteFiles(null, null);
                _gpuAbComparisonRenderer?.SetComparisonSourcesFromAbsoluteFiles(null, null);
                return;
            }

            EnsurePlayheadPaneImage("viewer-input", ref _playheadInputImage);
            ReplacePlayheadPaneTexture(_playheadInputImage!, inTex);
            ShowPlayheadPaneWithTexture("viewer-input", _playheadInputImage);

            string? abOutputPath = null;
            EnsurePlayheadPaneImage("viewer-output", ref _playheadOutputImage);
            if (File.Exists(alphaPath))
            {
                var outTex = TextureFileLoader.LoadReadableFromFile(alphaPath);
                if (outTex != null)
                {
                    ReplacePlayheadPaneTexture(_playheadOutputImage!, outTex);
                    ShowPlayheadPaneWithTexture("viewer-output", _playheadOutputImage);
                    abOutputPath = alphaPath;
                }
                else
                    ClearPlayheadOutputPaneToPlaceholder();
            }
            else
                ClearPlayheadOutputPaneToPlaceholder();

            _sampleAbComparisonRenderer?.SetComparisonSourcesFromAbsoluteFiles(platePath, abOutputPath);
            _gpuAbComparisonRenderer?.SetComparisonSourcesFromAbsoluteFiles(platePath, abOutputPath);

            _inputAnnotations?.NotifyInputPlateUpdated();
        }

        void ClearPlayheadOutputPaneToPlaceholder()
        {
            if (_viewerBody == null)
                return;
            if (_playheadOutputImage != null && _playheadOutputImage.image is Texture2D oldOut)
            {
                DestroyImmediate(oldOut);
                _playheadOutputImage.image = null;
            }

            var outPh = _viewerBody.Q<Label>("viewer-output-placeholder-label");
            if (outPh != null)
                outPh.style.display = DisplayStyle.Flex;
            if (_playheadOutputImage != null)
                _playheadOutputImage.style.display = DisplayStyle.None;
        }

        void ShowPlayheadPaneWithTexture(string paneName, Image img)
        {
            if (_viewerBody == null)
                return;
            var ph = _viewerBody.Q<Label>($"{paneName}-placeholder-label");
            if (ph != null)
                ph.style.display = DisplayStyle.None;
            img.style.display = DisplayStyle.Flex;
            img.MarkDirtyRepaint();
        }

        void EnsurePlayheadPaneImage(string paneName, ref Image? slot)
        {
            if (slot != null || _viewerBody == null)
                return;
            var surface = _viewerBody.Q<VisualElement>($"{paneName}-surface");
            if (surface == null)
                return;

            var existing = surface.Q<Image>($"{paneName}-preview-image");
            if (existing != null)
            {
                slot = existing;
                return;
            }

            slot = new Image
            {
                name = $"{paneName}-preview-image",
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
            };
            slot.style.flexGrow = 1;
            slot.style.width = Length.Percent(100);
            slot.style.height = Length.Percent(100);
            slot.style.display = DisplayStyle.None;
            surface.Add(slot);
        }

        static void ReplacePlayheadPaneTexture(Image img, Texture2D tex)
        {
            if (img.image is Texture2D oldTex && oldTex != tex)
                DestroyImmediate(oldTex);
            img.image = tex;
        }

        void OnRootAnnotationHotkeys(KeyDownEvent evt)
        {
            if (_inputAnnotations == null || rootVisualElement == null)
                return;
            if (evt.target is not VisualElement ve || ve.panel != rootVisualElement.panel)
                return;
            if (!_inputAnnotations.TryHandleKeyDownEvent(evt))
                return;
            _annotationHotkeyConsumedFrame = Time.frameCount;
            _annotationHotkeyConsumedKey = evt.keyCode;
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void OnRootImmersiveHotkey(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Space)
                return;
            if (!evt.shiftKey && (evt.modifiers & EventModifiers.Shift) == 0)
                return;
            evt.StopPropagation();
            evt.PreventDefault();
            ToggleImmersiveViewers();
        }

        void ToggleImmersiveViewers()
        {
            if (!_immersiveViewersActive)
            {
                _savedQueueExpanded = _queueSidebar?.Expanded ?? false;
                _savedParamsExpanded = _parametersSidebar?.Expanded ?? true;
                _savedFilesExpanded = _ioFilesBar?.Expanded ?? true;

                _queueSidebar?.SetExpanded(false);
                _parametersSidebar?.SetExpanded(false);
                _ioFilesBar?.SetExpanded(false);

                _immersiveViewersActive = true;
            }
            else
            {
                _queueSidebar?.SetExpanded(_savedQueueExpanded);
                _parametersSidebar?.SetExpanded(_savedParamsExpanded);
                _ioFilesBar?.SetExpanded(_savedFilesExpanded);

                _immersiveViewersActive = false;
            }

            _viewerIoSplit?.RefreshIoTrayLayout();
        }

        void OnGUI()
        {
            var e = Event.current;

            // UITK style.cursor is unreliable for Editor windows; IMGUI cursor rects work consistently.
            var root = rootVisualElement;
            if (root == null)
                return;

            if (e.type == EventType.KeyDown && _inputAnnotations != null
                && (EditorWindow.focusedWindow == this || EditorWindow.mouseOverWindow == this))
            {
                if (Time.frameCount == _annotationHotkeyConsumedFrame && e.keyCode == _annotationHotkeyConsumedKey)
                {
                    e.Use();
                    return;
                }

                if (_inputAnnotations.TryHandleImGuiEditorKey(e))
                {
                    _annotationHotkeyConsumedFrame = Time.frameCount;
                    _annotationHotkeyConsumedKey = e.keyCode;
                    e.Use();
                    return;
                }
            }

            var t = e.type;
            if (t != EventType.Layout && t != EventType.Repaint && t != EventType.MouseMove)
                return;

            void AddCursorRectFor(VisualElement? el, MouseCursor mouse)
            {
                if (el == null)
                    return;
                var r = el.worldBound;
                if (r.width <= 0f || r.height <= 0f)
                    return;
                var tl = root.WorldToLocal(r.min);
                var br = root.WorldToLocal(r.max);
                var guiRect = Rect.MinMaxRect(
                    Mathf.Min(tl.x, br.x),
                    Mathf.Min(tl.y, br.y),
                    Mathf.Max(tl.x, br.x),
                    Mathf.Max(tl.y, br.y));
                EditorGUIUtility.AddCursorRect(guiRect, mouse);
            }

            AddCursorRectFor(root.Q<VisualElement>("viewer-split-divider"), MouseCursor.ResizeHorizontal);
            AddCursorRectFor(root.Q<VisualElement>("io-tray-split-divider"), MouseCursor.ResizeVertical);
            if (_abScrubberOverlay != null)
            {
                var badgeRect = _abScrubberOverlay.BadgeWorldBounds;
                if (badgeRect.width > 0f && badgeRect.height > 0f)
                {
                    var tl = root.WorldToLocal(badgeRect.min);
                    var br = root.WorldToLocal(badgeRect.max);
                    var guiRect = Rect.MinMaxRect(
                        Mathf.Min(tl.x, br.x),
                        Mathf.Min(tl.y, br.y),
                        Mathf.Max(tl.x, br.x),
                        Mathf.Max(tl.y, br.y));
                    EditorGUIUtility.AddCursorRect(guiRect, MouseCursor.MoveArrow);
                }

            }
        }

        static void OnBackendLog(LogPayload payload)
        {
            BackendLogForwarder.Forward(payload);
        }

        static void OnBackendHealth(HealthPayload payload)
        {
            var msg = $"{(payload.Ok ? "OK" : "Fail")}: {payload.Summary}";
            if (payload.Ok)
                Debug.Log($"[CorridorKey] Health: {msg}");
            else
                Debug.LogWarning($"[CorridorKey] Health: {msg}");
        }

        void BuildMenuBar(VisualElement host)
        {
            host.Clear();
            host.style.flexShrink = 0;
            host.style.flexDirection = FlexDirection.Row;
            host.style.borderBottomWidth = 1;
            host.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);
            host.style.paddingBottom = 2;
            host.style.marginBottom = 6;

            var toolbar = new Toolbar();
            toolbar.style.flexGrow = 1;

            var fileMenu = new ToolbarMenu { text = "File" };
            fileMenu.menu.AppendAction(
                "Import Clips/Import Folder...",
                _ => OnMenuImportFolder(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendAction(
                "Import Clips/Import Video(s)...",
                _ => OnMenuImportVideos(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendAction(
                "Import Clips/Import Image Sequence...",
                _ => OnMenuImportImageSequence(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendSeparator(string.Empty);
            fileMenu.menu.AppendAction(
                "Save Session",
                _ => OnMenuSaveSession(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendAction(
                "Open Project...",
                _ => OnMenuOpenProject(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendSeparator(string.Empty);
            fileMenu.menu.AppendAction(
                "Export Video...",
                _ => OnMenuExportVideo(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendAction(
                "Export All Videos",
                _ => OnMenuExportAllVideos(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendSeparator(string.Empty);
            fileMenu.menu.AppendAction(
                "Return to Home",
                _ => OnMenuReturnToHome(),
                _ => DropdownMenuAction.Status.Normal);
            fileMenu.menu.AppendAction(
                "Exit",
                _ => OnMenuExit(),
                _ => DropdownMenuAction.Status.Normal);
            toolbar.Add(fileMenu);

            var editMenu = new ToolbarMenu { text = "Edit" };
            editMenu.menu.AppendAction(
                "Preferences...",
                _ => OnMenuPreferences(),
                _ => DropdownMenuAction.Status.Normal);
            editMenu.menu.AppendAction(
                "Hotkeys...",
                _ => OnMenuHotkeys(),
                _ => DropdownMenuAction.Status.Normal);
            editMenu.menu.AppendSeparator(string.Empty);
            editMenu.menu.AppendAction(
                "Track Paint Masks",
                _ => OnMenuTrackPaintMasks(),
                _ => _inputAnnotations != null && _inputAnnotations.HasAnyAnnotations()
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            editMenu.menu.AppendAction(
                "Clear Paint Strokes",
                _ => OnMenuClearPaintStrokes(),
                _ => _inputAnnotations != null && _inputAnnotations.HasAnyAnnotations()
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            toolbar.Add(editMenu);

            var viewMenu = new ToolbarMenu { text = "View" };
            viewMenu.menu.AppendAction(
                "Reset Layout",
                _ => OnMenuResetLayout(),
                _ => DropdownMenuAction.Status.Normal);
            viewMenu.menu.AppendAction(
                "Toggle Queue Panel",
                _ => OnMenuToggleQueuePanel(),
                _ => DropdownMenuAction.Status.Normal);
            viewMenu.menu.AppendAction(
                "Reset Zoom",
                _ => OnMenuResetZoom(),
                _ => DropdownMenuAction.Status.Normal);
            toolbar.Add(viewMenu);

            var helpMenu = new ToolbarMenu { text = "Help" };
            helpMenu.menu.AppendAction(
                "Console",
                _ => OnMenuConsole(),
                _ => DropdownMenuAction.Status.Normal);
            helpMenu.menu.AppendSeparator(string.Empty);
            helpMenu.menu.AppendAction(
                "Report Issue...",
                _ => OnMenuReportIssue(),
                _ => DropdownMenuAction.Status.Normal);
            helpMenu.menu.AppendSeparator(string.Empty);
            helpMenu.menu.AppendAction(
                "About",
                _ => OnMenuAbout(),
                _ => DropdownMenuAction.Status.Normal);
            toolbar.Add(helpMenu);

            host.Add(toolbar);
        }

        void OnNewQueueCardCreated(QueueJobVm _)
        {
            _queueSidebar?.SetExpanded(true);
        }

        static void OnMenuImportFolder()
        {
            Debug.Log("[CorridorKey] File > Import Clips > Import Folder... clicked.");
        }

        static void OnMenuImportVideos()
        {
            Debug.Log("[CorridorKey] File > Import Clips > Import Video(s)... clicked.");
        }

        static void OnMenuImportImageSequence()
        {
            Debug.Log("[CorridorKey] File > Import Clips > Import Image Sequence... clicked.");
        }

        static void OnMenuSaveSession()
        {
            Debug.Log("[CorridorKey] File > Save Session clicked.");
        }

        static void OnMenuOpenProject()
        {
            Debug.Log("[CorridorKey] File > Open Project... clicked.");
        }

        static void OnMenuExportVideo()
        {
            Debug.Log("[CorridorKey] File > Export Video... clicked.");
        }

        static void OnMenuExportAllVideos()
        {
            Debug.Log("[CorridorKey] File > Export All Videos clicked.");
        }

        static void OnMenuReturnToHome()
        {
            Debug.Log("[CorridorKey] File > Return to Home clicked.");
        }

        static void OnMenuPreferences()
        {
            Debug.Log("[CorridorKey] Edit > Preferences... clicked.");
        }

        static void OnMenuHotkeys()
        {
            Debug.Log("[CorridorKey] Edit > Hotkeys... clicked.");
        }

        static void OnMenuTrackPaintMasks()
        {
            Debug.Log("[CorridorKey] Edit > Track Paint Masks clicked.");
        }

        void OnMenuClearPaintStrokes()
        {
            if (_inputAnnotations == null || !_inputAnnotations.HasAnyAnnotations())
                return;
            if (!EditorUtility.DisplayDialog(
                    "Clear Paint Strokes",
                    "Remove all brush strokes for this clip from annotations.json? This cannot be undone.",
                    "Clear",
                    "Cancel"))
                return;
            _inputAnnotations.ClearAllAnnotations();
        }

        static void OnMenuResetLayout()
        {
            Debug.Log("[CorridorKey] View > Reset Layout clicked.");
        }

        static void OnMenuToggleQueuePanel()
        {
            Debug.Log("[CorridorKey] View > Toggle Queue Panel clicked.");
        }

        static void OnMenuResetZoom()
        {
            Debug.Log("[CorridorKey] View > Reset Zoom clicked.");
        }

        static void OnMenuConsole()
        {
            Debug.Log("[CorridorKey] Help > Console clicked.");
        }

        static void OnMenuReportIssue()
        {
            Debug.Log("[CorridorKey] Help > Report Issue... clicked.");
        }

        static void OnMenuAbout()
        {
            Debug.Log("[CorridorKey] Help > About clicked.");
        }

        void OnMenuExit()
        {
            Debug.Log("[CorridorKey] File > Exit clicked.");
            Close();
        }
    }
}
