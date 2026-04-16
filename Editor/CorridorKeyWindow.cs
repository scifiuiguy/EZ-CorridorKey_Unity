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
    /// <summary>
    /// Main CorridorKey Editor window (EZ parity: <c>ui/main_window.py</c>). Queue, viewers, and parameters
    /// will live here; <see cref="RunBackendHealthCheck"/> is one action that talks to the Python bridge.
    /// </summary>
    public sealed class CorridorKeyWindow : EditorWindow
    {
        public const string WindowTitle = "EZ CorridorKey";

        /// <summary>Minimum window size (dual viewers + 280px parameters rail).</summary>
        public static readonly Vector2 MinWindowSize = new Vector2(880f, 420f);

        ProcessBackendClient? _backend;
        CorridorKeySessionVm? _session;
        QueueSidebarController? _queueSidebar;

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
        }

        void CreateGUI()
        {
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
