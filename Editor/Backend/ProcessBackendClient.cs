#nullable enable
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CorridorKey;
using CorridorKey.Backend;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace CorridorKey.Editor.Backend
{
    /// <summary>
    /// Stdio NDJSON bridge to <c>unity_bridge.py</c> (EZ working directory + venv Python).
    /// </summary>
    public sealed class ProcessBackendClient : IBackendClient
    {
        readonly ConcurrentQueue<string> _lineQueue = new();
        readonly object _processLock = new();

        Process? _process;
        CancellationTokenSource? _readCts;
        Task? _ioTasks;
        bool _editorUpdateRegistered;

        public event Action<HealthPayload>? HealthReceived;
        public event Action<LogPayload>? LogReceived;
        public event Action<ProgressPayload>? ProgressReceived;
        public event Action<ClipStatePayload>? ClipStateReceived;
        public event Action<string>? ErrorReceived;

        public void RequestHealthCheck()
        {
            lock (_processLock)
            {
                var err = TryEnsureProcessUnderLock();
                if (err != null)
                {
                    HealthReceived?.Invoke(new HealthPayload(false, err));
                    return;
                }

                try
                {
                    WriteStdinLineUnlocked("{\"cmd\":\"health\"}");
                }
                catch (Exception ex)
                {
                    ErrorReceived?.Invoke(ex.Message);
                    HealthReceived?.Invoke(new HealthPayload(false, ex.Message));
                }
            }
        }

        public void Cancel()
        {
            lock (_processLock)
            {
                try
                {
                    if (_process is { HasExited: false })
                        WriteStdinLineUnlocked("{\"cmd\":\"shutdown\"}");
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void Dispose()
        {
            UnregisterEditorUpdate();
            StopProcess();
        }

        /// <summary>Single active reader (Editor typically hosts one client).</summary>
        internal static ProcessBackendClient? InstanceReader;

        string? TryEnsureProcessUnderLock()
        {
            if (_process is { HasExited: false })
                return null;

            StopProcessLocked();

            var python = CorridorKeySettings.PythonExecutable;
            var ezRoot = CorridorKeySettings.BackendWorkingDirectory;
            if (string.IsNullOrEmpty(python))
                return "EditorPrefs missing Python path (CorridorKey.PythonExecutable). Set in wizard or preferences.";
            string pythonExe = python!;
            var pythonErr = PythonExecutableValidator.Validate(pythonExe);
            if (pythonErr != null)
                return pythonErr;
            if (string.IsNullOrEmpty(ezRoot))
                return "EditorPrefs missing EZ root (CorridorKey.BackendWorkingDirectory). Point at your EZ-CorridorKey checkout.";

            var bridge = CorridorKeyPackagePaths.GetUnityBridgeScriptPath();
            if (string.IsNullOrEmpty(bridge) || !File.Exists(bridge))
                return "unity_bridge.py not found inside this package (Editor/Backend/Python/).";

                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    // -u: unbuffered stdout/stderr so NDJSON lines flush immediately.
                    Arguments = $"-u \"{bridge}\"",
                WorkingDirectory = ezRoot,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                proc.Start();
            }
            catch (Exception ex)
            {
                return $"Failed to start Python: {ex.Message}";
            }

            _process = proc;
            _readCts = new CancellationTokenSource();
            var token = _readCts.Token;

            InstanceReader = this;
            RegisterEditorUpdate(); // drains _lineQueue on main thread before InstanceReader is cleared

            _ioTasks = Task.Run(
                () =>
                {
                    var stdout = Task.Run(() => ReadStdoutLoop(proc, token), token);
                    var stderr = Task.Run(() => ReadStderrLoop(proc, token), token);
                    Task.WaitAll(stdout, stderr);
                },
                token);

            return null;
        }

        void WriteStdinLineUnlocked(string line)
        {
            if (_process?.StandardInput == null)
                throw new InvalidOperationException("Process stdin not available.");
            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }

        static void ReadStdoutLoop(Process proc, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && !proc.HasExited)
                {
                    var line = proc.StandardOutput.ReadLine();
                    if (line == null)
                        break;
                    InstanceReader?.EnqueueLine(line);
                }
            }
            catch
            {
                // process killed or pipe broken
            }
        }

        static void ReadStderrLoop(Process proc, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && !proc.HasExited)
                {
                    var line = proc.StandardError.ReadLine();
                    if (line == null)
                        break;
                    var escaped = EscapeForJson(line);
                    InstanceReader?.EnqueueLine(
                        $"{{\"type\":\"log\",\"level\":\"ERROR\",\"message\":\"{escaped}\",\"logger\":\"python.stderr\"}}");
                }
            }
            catch
            {
                // ignore
            }
        }

        static string EscapeForJson(string s)
        {
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", string.Empty)
                .Replace("\n", "\\n");
        }

        void EnqueueLine(string line)
        {
            _lineQueue.Enqueue(line);
        }

        void RegisterEditorUpdate()
        {
            if (_editorUpdateRegistered)
                return;
            EditorApplication.update += OnEditorUpdate;
            _editorUpdateRegistered = true;
        }

        void UnregisterEditorUpdate()
        {
            if (!_editorUpdateRegistered)
                return;
            EditorApplication.update -= OnEditorUpdate;
            _editorUpdateRegistered = false;
            if (InstanceReader == this)
                InstanceReader = null;
        }

        void OnEditorUpdate()
        {
            while (_lineQueue.TryDequeue(out var line))
                DispatchLine(line);
        }

        void DispatchLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            BridgeLine msg;
            try
            {
                msg = JsonUtility.FromJson<BridgeLine>(line);
            }
            catch
            {
                UnityEngine.Debug.LogWarning($"[CorridorKey] Non-JSON bridge line: {line}");
                return;
            }

            if (string.IsNullOrEmpty(msg.type))
                return;

            switch (msg.type)
            {
                case "health":
                    HealthReceived?.Invoke(new HealthPayload(msg.ok, msg.summary ?? string.Empty));
                    break;
                case "log":
                    LogReceived?.Invoke(new LogPayload(
                        msg.level ?? "INFO",
                        msg.message ?? string.Empty,
                        string.IsNullOrEmpty(msg.logger) ? null : msg.logger));
                    break;
                case "progress":
                    ProgressReceived?.Invoke(new ProgressPayload(
                        msg.current,
                        msg.total,
                        string.IsNullOrEmpty(msg.phase) ? null : msg.phase));
                    break;
                case "clip_state":
                    if (!string.IsNullOrEmpty(msg.clip)
                        && Enum.TryParse(msg.state, ignoreCase: true, out ClipState st))
                        ClipStateReceived?.Invoke(new ClipStatePayload(msg.clip, st));
                    break;
                case "error":
                    ErrorReceived?.Invoke(msg.message ?? msg.summary ?? "Unknown error");
                    break;
                case "done":
                    break;
                default:
                    UnityEngine.Debug.Log($"[CorridorKey] Unhandled bridge type: {msg.type}");
                    break;
            }
        }

        void StopProcess()
        {
            lock (_processLock)
            {
                StopProcessLocked();
            }
        }

        void StopProcessLocked()
        {
            try
            {
                _readCts?.Cancel();
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_process is { HasExited: false })
                {
                    try
                    {
                        _process.StandardInput.WriteLine("{\"cmd\":\"shutdown\"}");
                        _process.StandardInput.Flush();
                    }
                    catch
                    {
                        // ignore
                    }

                    if (!_process.WaitForExit(4000))
                        _process.Kill();
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                _ioTasks?.Wait(3000);
            }
            catch
            {
                // ignore
            }

            if (InstanceReader == this)
                InstanceReader = null;

            _process?.Dispose();
            _process = null;
            _readCts?.Dispose();
            _readCts = null;
            _ioTasks = null;
        }

        [Serializable]
        class BridgeLine
        {
            public string type = string.Empty;
            public bool ok;
            public string summary = string.Empty;
            public string level = string.Empty;
            public string message = string.Empty;
            public string logger = string.Empty;
            public string cmd = string.Empty;
            public int current;
            public int total;
            public string phase = string.Empty;
            public string clip = string.Empty;
            public string state = string.Empty;
        }
    }
}
