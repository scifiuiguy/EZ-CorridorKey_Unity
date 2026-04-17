#nullable enable
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor.Backend;
using CorridorKey.Editor.UI;
using CorridorKey.Editor.ViewModels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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
        ParametersSidebarController? _parametersSidebar;
        InferenceSectionController? _inferenceSectionController;
        OutputPerformanceSectionController? _outputPerformanceSectionController;
        HorizontalViewerIoSplitController? _viewerIoSplit;
        VerticalViewerIoTraySplitController? _viewerIoTraySplit;
        IoFilesBarToggleController? _ioFilesBar;
        AbScrubberOverlayController? _abScrubberOverlay;
        SampleAbComparisonRenderer? _sampleAbComparisonRenderer;
        GpuAbComparisonRenderer? _gpuAbComparisonRenderer;

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

            if (_backend != null)
            {
                _backend.LogReceived -= OnBackendLog;
                _backend.HealthReceived -= OnBackendHealth;
                _backend.Dispose();
                _backend = null;
            }

            _session = null;
            _queueSidebar = null;
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

            var sidebar = body.Q<VisualElement>("queue-sidebar");
            var queueContent = body.Q<VisualElement>("queue-content");
            var queueTab = body.Q<VisualElement>("queue-tab");
            if (sidebar != null && queueContent != null && queueTab != null)
                _queueSidebar = new QueueSidebarController(sidebar, queueContent, queueTab);

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

            var dualViewerChrome = new DualViewerChromeController(body);
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
            dualViewerChrome.AbToggled += on =>
            {
                _abPreviewEnabled = on;
                ApplyAbPreviewMode();
            };
            dualViewerChrome.AbRendererModeToggled += gpuOn =>
            {
                _abGpuPreviewEnabled = gpuOn;
                ApplyAbPreviewMode();
            };
            ApplyAbPreviewMode();
            _ = new ViewerPlayheadStripController(body);
            _ = new ParametersRailController(body);
            SeedQueueDummyCards(body);

            body.Q<Button>("status-run-selected")?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[CorridorKey] RUN SELECTED clicked."));

            body.Q<Button>("io-tray-input-reset-io")?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[CorridorKey] RESET IO clicked."));
            body.Q<Button>("io-tray-input-add")?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[CorridorKey] ADD clicked."));
            body.Q<Button>("queue-clear-button")?.RegisterCallback<ClickEvent>(_ =>
            {
                var queueScroll = body.Q<ScrollView>("queue-scroll");
                queueScroll?.Clear();
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

            _immersiveViewersActive = false;
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

        // Immersive toggle: key handling lives in OnGUI — EditorWindow keyboard input goes through IMGUI, not UITK KeyDownEvent.
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
            if (e.type == EventType.KeyDown &&
                e.keyCode == KeyCode.Space &&
                (e.shift || (e.modifiers & EventModifiers.Shift) != 0) &&
                focusedWindow == this)
            {
                e.Use();
                ToggleImmersiveViewers();
            }

            // UITK style.cursor is unreliable for Editor windows; IMGUI cursor rects work consistently.
            var root = rootVisualElement;
            if (root == null)
                return;

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
                _ => DropdownMenuAction.Status.Normal);
            editMenu.menu.AppendAction(
                "Clear Paint Strokes",
                _ => OnMenuClearPaintStrokes(),
                _ => DropdownMenuAction.Status.Normal);
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

        static void SeedQueueDummyCards(VisualElement body)
        {
            var queueScroll = body.Q<ScrollView>("queue-scroll");
            if (queueScroll == null)
                return;

            queueScroll.Clear();
            for (var i = 1; i <= 10; i++)
            {
                var card = QueueJobCardFactory.Create(
                    typeText: i % 3 == 0 ? "INFERENCE" : (i % 2 == 0 ? "ALPHA" : "EXTRACT"),
                    fileText: $"shot_{i:00}_greenscreen.mov",
                    statusText: i % 4 == 0 ? "Queued" : (i % 5 == 0 ? "Ready" : "Waiting for worker"),
                    onRemove: el => el.RemoveFromHierarchy());
                queueScroll.Add(card);
            }

            Debug.Log("[CorridorKey] Seeded 10 dummy queue job cards (debug).");
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

        static void OnMenuClearPaintStrokes()
        {
            Debug.Log("[CorridorKey] Edit > Clear Paint Strokes clicked.");
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
