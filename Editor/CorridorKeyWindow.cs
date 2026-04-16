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
        public static readonly Vector2 MinWindowSize = new Vector2(904f, 442f);

        ProcessBackendClient? _backend;
        CorridorKeySessionVm? _session;
        QueueSidebarController? _queueSidebar;
        ParametersSidebarController? _parametersSidebar;
        HorizontalViewerIoSplitController? _viewerIoSplit;
        VerticalViewerIoTraySplitController? _viewerIoTraySplit;
        IoFilesBarToggleController? _ioFilesBar;

        bool _immersiveViewersActive;
        bool _savedQueueExpanded;
        bool _savedParamsExpanded;
        bool _savedFilesExpanded;

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
            _viewerIoSplit?.Dispose();
            _viewerIoSplit = null;
            _viewerIoTraySplit?.Dispose();
            _viewerIoTraySplit = null;
            _ioFilesBar = null;
        }

        void CreateGUI()
        {
            _viewerIoSplit?.Dispose();
            _viewerIoSplit = null;
            _viewerIoTraySplit?.Dispose();
            _viewerIoTraySplit = null;
            _ioFilesBar = null;

            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingBottom = 8f;
            root.style.paddingLeft = 8f;
            root.userData = _session;

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

            _viewerIoSplit = HorizontalViewerIoSplitController.TryAttach(body);
            _viewerIoTraySplit = VerticalViewerIoTraySplitController.TryAttach(body);
            _ioFilesBar = IoFilesBarToggleController.TryAttach(body, _viewerIoSplit, _viewerIoTraySplit);

            _immersiveViewersActive = false;
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
                "Run Backend Health Check",
                _ => RunBackendHealthCheck(),
                _ => DropdownMenuAction.Status.Normal);
            // EZ parity: add Open project, preferences, quit, etc. here later.
            toolbar.Add(fileMenu);
            host.Add(toolbar);
        }
    }
}
