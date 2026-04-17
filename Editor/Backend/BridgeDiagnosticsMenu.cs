#nullable enable
using System;
using System.IO;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace CorridorKey.Editor.Backend
{
    /// <summary>
    /// Phase 1: run stdio bridge diagnostics (FFmpeg health, Python/imports, ffmpeg -version, file probe).
    /// Results go to the Unity Console. Uses a dedicated <see cref="ProcessBackendClient"/> (separate from an open CorridorKey window).
    /// </summary>
    static class BridgeDiagnosticsMenu
    {
        const int ExpectedDoneCount = 5;
        const string Phase2ProjectFolder = "260415_062207_greenscreen-test-02";
        const string Phase2ClipFolder = "greenscreen-test-02";

        [MenuItem("Tools/CorridorKey/Debug/Run bridge diagnostics (Phase 1)", false, 200)]
        static void RunPhase1Diagnostics()
        {
            if (string.IsNullOrEmpty(CorridorKeySettings.PythonExecutable))
            {
                Debug.LogError("[CorridorKey] Set the Python executable in CorridorKey backend settings first.");
                return;
            }

            if (string.IsNullOrEmpty(CorridorKeySettings.BackendWorkingDirectory))
            {
                Debug.LogError("[CorridorKey] Set the EZ root (backend working directory) first.");
                return;
            }

            var projectVersionPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectVersion.txt"));

            var client = new ProcessBackendClient();
            var remaining = ExpectedDoneCount;
            var aborted = false;

            void Abort(string? reason = null)
            {
                if (aborted)
                    return;
                aborted = true;
                Unsub();
                client.Dispose();
                if (!string.IsNullOrEmpty(reason))
                    Debug.LogError($"[CorridorKey] Diagnostics aborted: {reason}");
            }

            void Unsub()
            {
                client.LogReceived -= OnLog;
                client.HealthReceived -= OnHealth;
                client.DiagnosticResultReceived -= OnDiag;
                client.BridgeCommandDoneReceived -= OnDone;
                client.ErrorReceived -= OnErr;
            }

            void OnLog(LogPayload p) => BackendLogForwarder.Forward(p);

            void OnHealth(HealthPayload p)
            {
                if (p.Ok)
                    Debug.Log($"[CorridorKey] Health: OK — {p.Summary}");
                else
                    Debug.LogWarning($"[CorridorKey] Health: Fail — {p.Summary}");

                if (!p.Ok && IsEditorSetupFailure(p.Summary))
                    Abort("fix backend settings (see messages above).");
            }

            void OnDiag(DiagnosticResultPayload p)
            {
                var tag = p.Ok ? "OK" : "FAIL";
                Debug.Log($"[CorridorKey] diag.{p.Diag} [{tag}] {p.Summary}");
            }

            void OnDone(BridgeCommandDonePayload p)
            {
                if (p.Cmd == "shutdown")
                    return;
                Debug.Log($"[CorridorKey] done cmd={p.Cmd} request_id={p.RequestId} ok={p.Ok}");
                remaining--;
                if (remaining <= 0 && !aborted)
                {
                    Unsub();
                    client.Dispose();
                    Debug.Log("[CorridorKey] Phase 1 diagnostics finished (bridge process shut down).");
                }
            }

            void OnErr(string e) => Debug.LogError($"[CorridorKey] {e}");

            client.LogReceived += OnLog;
            client.HealthReceived += OnHealth;
            client.DiagnosticResultReceived += OnDiag;
            client.BridgeCommandDoneReceived += OnDone;
            client.ErrorReceived += OnErr;

            Debug.Log("[CorridorKey] Starting Phase 1 bridge diagnostics (see log lines below).");

            var r0 = Guid.NewGuid().ToString("N");
            var r1 = Guid.NewGuid().ToString("N");
            var r2 = Guid.NewGuid().ToString("N");
            var r3 = Guid.NewGuid().ToString("N");
            var r4 = Guid.NewGuid().ToString("N");

            client.RequestHealthCheck(r0);
            if (aborted)
                return;

            void TrySendDiag(BridgeDiagStdin payload)
            {
                if (aborted)
                    return;
                var err = client.TrySendJson(payload);
                if (err != null)
                {
                    Debug.LogError($"[CorridorKey] {err}");
                    Abort(err);
                }
            }

            TrySendDiag(new BridgeDiagStdin { cmd = "diag.python", request_id = r1 });
            if (aborted)
                return;
            TrySendDiag(new BridgeDiagStdin { cmd = "diag.imports", request_id = r2 });
            if (aborted)
                return;
            TrySendDiag(new BridgeDiagStdin { cmd = "diag.ffmpeg_version", request_id = r3 });
            if (aborted)
                return;
            TrySendDiag(new BridgeDiagStdin
            {
                cmd = "diag.file_exists",
                request_id = r4,
                path = projectVersionPath
            });
        }

        [MenuItem("Tools/CorridorKey/Debug/Extract frames (Phase 2 copied clip)", false, 210)]
        static void RunPhase2ExtractFrames()
        {
            if (string.IsNullOrEmpty(CorridorKeySettings.PythonExecutable))
            {
                Debug.LogError("[CorridorKey] Set the Python executable in CorridorKey backend settings first.");
                return;
            }

            if (string.IsNullOrEmpty(CorridorKeySettings.BackendWorkingDirectory))
            {
                Debug.LogError("[CorridorKey] Set the EZ root (backend working directory) first.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var phase2Root = Path.Combine(projectRoot, "CorridorKeyData", Phase2ProjectFolder, "clips", Phase2ClipFolder);
            var inputPath = Path.Combine(phase2Root, "Source", "greenscreen-test-02.mp4");
            var outputDir = Path.Combine(phase2Root, "Frames");

            if (!File.Exists(inputPath))
            {
                Debug.LogError($"[CorridorKey] Phase 2 input clip not found: {inputPath}");
                return;
            }

            if (!IsPathUnder(projectRoot, outputDir))
            {
                Debug.LogError($"[CorridorKey] Refusing to write outside current Unity project: {outputDir}");
                return;
            }

            var requestId = Guid.NewGuid().ToString("N");
            var client = new ProcessBackendClient();
            var completed = false;

            void Finish(string finalMessage)
            {
                if (completed)
                    return;
                completed = true;
                Unsub();
                client.Dispose();
                Debug.Log(finalMessage);
            }

            void Unsub()
            {
                client.LogReceived -= OnLog;
                client.ProgressReceived -= OnProgress;
                client.DiagnosticResultReceived -= OnDiag;
                client.BridgeCommandDoneReceived -= OnDone;
                client.ErrorReceived -= OnErr;
            }

            void OnLog(LogPayload p) => BackendLogForwarder.Forward(p);

            void OnProgress(ProgressPayload p)
            {
                var phase = string.IsNullOrEmpty(p.Phase) ? "phase" : p.Phase;
                var tail = string.IsNullOrEmpty(p.Detail) ? "" : $" — {p.Detail}";
                Debug.Log($"[CorridorKey] progress {phase}: {p.Current}/{p.Total}{tail}");
            }

            void OnDiag(DiagnosticResultPayload p)
            {
                if (p.RequestId != requestId)
                    return;
                var tag = p.Ok ? "OK" : "FAIL";
                Debug.Log($"[CorridorKey] diag.{p.Diag} [{tag}] {p.Summary}");
            }

            void OnDone(BridgeCommandDonePayload p)
            {
                if (p.RequestId != requestId || p.Cmd != "media.extract_frames")
                    return;

                if (!p.Ok)
                {
                    Finish($"[CorridorKey] Phase 2 extract failed: {p.Summary}");
                    return;
                }

                var frames = Directory.Exists(outputDir)
                    ? Directory.GetFiles(outputDir, "*.png")
                    : Array.Empty<string>();
                Array.Sort(frames, StringComparer.OrdinalIgnoreCase);
                var first = frames.Length > 0 ? Path.GetFileName(frames[0]) : "(none)";
                var last = frames.Length > 0 ? Path.GetFileName(frames[^1]) : "(none)";
                Finish(
                    $"[CorridorKey] Phase 2 extract complete: {frames.Length} frame(s) in {outputDir}; first={first}; last={last}");
            }

            void OnErr(string e) => Debug.LogError($"[CorridorKey] {e}");

            client.LogReceived += OnLog;
            client.ProgressReceived += OnProgress;
            client.DiagnosticResultReceived += OnDiag;
            client.BridgeCommandDoneReceived += OnDone;
            client.ErrorReceived += OnErr;

            Debug.Log($"[CorridorKey] Starting Phase 2 extract from copied clip: {inputPath}");
            var err = client.TrySendJson(new MediaExtractFramesStdin
            {
                cmd = "media.extract_frames",
                request_id = requestId,
                input_path = inputPath,
                output_dir = outputDir,
                overwrite = true
            });
            if (err != null)
                Finish($"[CorridorKey] Phase 2 extract aborted: {err}");
        }

        [MenuItem("Tools/CorridorKey/Debug/Run GVM hint (Phase 3 alpha slice)", false, 220)]
        static void RunPhase3GvmHint()
        {
            if (string.IsNullOrEmpty(CorridorKeySettings.PythonExecutable))
            {
                Debug.LogError("[CorridorKey] Set the Python executable in CorridorKey backend settings first.");
                return;
            }

            if (string.IsNullOrEmpty(CorridorKeySettings.BackendWorkingDirectory))
            {
                Debug.LogError("[CorridorKey] Set the EZ root (backend working directory) first.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var clipRoot = Path.Combine(projectRoot, "CorridorKeyData", Phase2ProjectFolder, "clips", Phase2ClipFolder);
            var framesDir = Path.Combine(clipRoot, "Frames");
            var alphaDir = Path.Combine(clipRoot, "AlphaHint");
            var clipJson = Path.Combine(clipRoot, "clip.json");

            if (!Directory.Exists(framesDir))
            {
                Debug.LogError($"[CorridorKey] Frames not found. Run extract first: {framesDir}");
                return;
            }
            if (!IsPathUnder(projectRoot, clipRoot))
            {
                Debug.LogError($"[CorridorKey] Refusing to run outside current Unity project: {clipRoot}");
                return;
            }

            var requestId = Guid.NewGuid().ToString("N");
            var client = new ProcessBackendClient();
            var completed = false;

            void Finish(string finalMessage)
            {
                if (completed)
                    return;
                completed = true;
                Unsub();
                client.Dispose();
                Debug.Log(finalMessage);
            }

            void Unsub()
            {
                client.LogReceived -= OnLog;
                client.ProgressReceived -= OnProgress;
                client.DiagnosticResultReceived -= OnDiag;
                client.BridgeCommandDoneReceived -= OnDone;
                client.ErrorReceived -= OnErr;
            }

            void OnLog(LogPayload p) => BackendLogForwarder.Forward(p);

            void OnProgress(ProgressPayload p)
            {
                var phase = string.IsNullOrEmpty(p.Phase) ? "phase" : p.Phase;
                var tail = string.IsNullOrEmpty(p.Detail) ? "" : $" — {p.Detail}";
                Debug.Log($"[CorridorKey] progress {phase}: {p.Current}/{p.Total}{tail}");
            }

            void OnDiag(DiagnosticResultPayload p)
            {
                if (p.RequestId != requestId)
                    return;
                var tag = p.Ok ? "OK" : "FAIL";
                Debug.Log($"[CorridorKey] diag.{p.Diag} [{tag}] {p.Summary}");
            }

            void OnDone(BridgeCommandDonePayload p)
            {
                if (p.RequestId != requestId || p.Cmd != "alpha.gvm_hint")
                    return;

                if (!p.Ok)
                {
                    Finish($"[CorridorKey] GVM hint failed: {p.Summary}");
                    return;
                }

                var alphaFrames = Directory.Exists(alphaDir)
                    ? Directory.GetFiles(alphaDir, "*.png")
                    : Array.Empty<string>();
                Array.Sort(alphaFrames, StringComparer.OrdinalIgnoreCase);
                var first = alphaFrames.Length > 0 ? Path.GetFileName(alphaFrames[0]) : "(none)";
                var last = alphaFrames.Length > 0 ? Path.GetFileName(alphaFrames[^1]) : "(none)";
                var clipJsonExists = File.Exists(clipJson) ? "yes" : "no";
                Finish(
                    $"[CorridorKey] GVM hint complete: {alphaFrames.Length} alpha frame(s) in {alphaDir}; first={first}; last={last}; clip.json updated={clipJsonExists}");
            }

            void OnErr(string e) => Debug.LogError($"[CorridorKey] {e}");

            client.LogReceived += OnLog;
            client.ProgressReceived += OnProgress;
            client.DiagnosticResultReceived += OnDiag;
            client.BridgeCommandDoneReceived += OnDone;
            client.ErrorReceived += OnErr;

            Debug.Log($"[CorridorKey] Starting GVM hint from frames: {framesDir}");
            var err = client.TrySendJson(new GvmHintStdin
            {
                cmd = "alpha.gvm_hint",
                request_id = requestId,
                clip_root = clipRoot,
                frames_dir = framesDir,
                overwrite = true
            });
            if (err != null)
                Finish($"[CorridorKey] GVM hint aborted: {err}");
        }

        [MenuItem("Tools/CorridorKey/Debug/Diagnose BiRefNet checkpoint (paths, files, torch)", false, 224)]
        static void DiagnoseBiRefNetCheckpoint()
        {
            if (string.IsNullOrEmpty(CorridorKeySettings.PythonExecutable))
            {
                Debug.LogError("[CorridorKey] Set the Python executable in CorridorKey backend settings first.");
                return;
            }

            if (string.IsNullOrEmpty(CorridorKeySettings.BackendWorkingDirectory))
            {
                Debug.LogError("[CorridorKey] Set the EZ root (backend working directory) first.");
                return;
            }

            var requestId = Guid.NewGuid().ToString("N");
            var client = new ProcessBackendClient();
            var completed = false;

            void Finish(string finalMessage)
            {
                if (completed)
                    return;
                completed = true;
                Unsub();
                client.Dispose();
                Debug.Log(finalMessage);
            }

            void Unsub()
            {
                client.LogReceived -= OnLog;
                client.DiagnosticResultReceived -= OnDiag;
                client.BridgeCommandDoneReceived -= OnDone;
                client.ErrorReceived -= OnErr;
            }

            void OnLog(LogPayload p) => BackendLogForwarder.Forward(p);

            void OnDiag(DiagnosticResultPayload p)
            {
                if (p.RequestId != requestId)
                    return;
                var tag = p.Ok ? "OK" : "FAIL";
                Debug.Log($"[CorridorKey] diag.{p.Diag} [{tag}] {p.Summary}");
            }

            void OnDone(BridgeCommandDonePayload p)
            {
                if (p.RequestId != requestId || p.Cmd != "diag.birefnet")
                    return;
                if (!p.Ok && !string.IsNullOrEmpty(p.Summary))
                    Debug.LogWarning($"[CorridorKey] diag.birefnet done: {p.Summary}");
                Finish("[CorridorKey] BiRefNet checkpoint diagnostic finished (see diag lines above).");
            }

            void OnErr(string e) => Debug.LogError($"[CorridorKey] {e}");

            client.LogReceived += OnLog;
            client.DiagnosticResultReceived += OnDiag;
            client.BridgeCommandDoneReceived += OnDone;
            client.ErrorReceived += OnErr;

            var bridgeScript = CorridorKeyPackagePaths.GetUnityBridgeScriptPath();
            Debug.Log(
                "[CorridorKey] BiRefNet checkpoint diagnostic: usage=Matting. "
                + "Python runs the bridge script from this Unity project (not your EZ/R folder). "
                + $"bridge_script={bridgeScript}; "
                + $"process_working_directory (imports, modules/…)={CorridorKeySettings.BackendWorkingDirectory}");

            var err = client.TrySendJson(new BridgeDiagStdin
            {
                cmd = "diag.birefnet",
                request_id = requestId,
                usage = "Matting",
            });
            if (err != null)
                Finish($"[CorridorKey] diag.birefnet aborted: {err}");
        }

        [MenuItem("Tools/CorridorKey/Debug/Run BiRefNet hint (Phase 3 alpha slice)", false, 225)]
        static void RunPhase3BiRefNetHint()
        {
            if (string.IsNullOrEmpty(CorridorKeySettings.PythonExecutable))
            {
                Debug.LogError("[CorridorKey] Set the Python executable in CorridorKey backend settings first.");
                return;
            }

            if (string.IsNullOrEmpty(CorridorKeySettings.BackendWorkingDirectory))
            {
                Debug.LogError("[CorridorKey] Set the EZ root (backend working directory) first.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var clipRoot = Path.Combine(projectRoot, "CorridorKeyData", Phase2ProjectFolder, "clips", Phase2ClipFolder);
            var framesDir = Path.Combine(clipRoot, "Frames");
            var alphaDir = Path.Combine(clipRoot, "AlphaHint");
            var clipJson = Path.Combine(clipRoot, "clip.json");

            if (!Directory.Exists(framesDir))
            {
                Debug.LogError($"[CorridorKey] Frames not found. Run extract first: {framesDir}");
                return;
            }
            if (!IsPathUnder(projectRoot, clipRoot))
            {
                Debug.LogError($"[CorridorKey] Refusing to run outside current Unity project: {clipRoot}");
                return;
            }

            var requestId = Guid.NewGuid().ToString("N");
            var client = new ProcessBackendClient();
            var completed = false;

            void Finish(string finalMessage)
            {
                if (completed)
                    return;
                completed = true;
                Unsub();
                client.Dispose();
                Debug.Log(finalMessage);
            }

            void Unsub()
            {
                client.LogReceived -= OnLog;
                client.ProgressReceived -= OnProgress;
                client.DiagnosticResultReceived -= OnDiag;
                client.BridgeCommandDoneReceived -= OnDone;
                client.ErrorReceived -= OnErr;
            }

            void OnLog(LogPayload p) => BackendLogForwarder.Forward(p);

            void OnProgress(ProgressPayload p)
            {
                var phase = string.IsNullOrEmpty(p.Phase) ? "phase" : p.Phase;
                var tail = string.IsNullOrEmpty(p.Detail) ? "" : $" — {p.Detail}";
                Debug.Log($"[CorridorKey] progress {phase}: {p.Current}/{p.Total}{tail}");
            }

            void OnDiag(DiagnosticResultPayload p)
            {
                if (p.RequestId != requestId)
                    return;
                var tag = p.Ok ? "OK" : "FAIL";
                Debug.Log($"[CorridorKey] diag.{p.Diag} [{tag}] {p.Summary}");
            }

            void OnDone(BridgeCommandDonePayload p)
            {
                if (p.RequestId != requestId || p.Cmd != "alpha.birefnet_hint")
                    return;

                if (!p.Ok)
                {
                    Finish($"[CorridorKey] BiRefNet hint failed: {p.Summary}");
                    return;
                }

                var alphaFrames = Directory.Exists(alphaDir)
                    ? Directory.GetFiles(alphaDir, "*.png")
                    : Array.Empty<string>();
                Array.Sort(alphaFrames, StringComparer.OrdinalIgnoreCase);
                var first = alphaFrames.Length > 0 ? Path.GetFileName(alphaFrames[0]) : "(none)";
                var last = alphaFrames.Length > 0 ? Path.GetFileName(alphaFrames[^1]) : "(none)";
                var clipJsonExists = File.Exists(clipJson) ? "yes" : "no";
                Finish(
                    $"[CorridorKey] BiRefNet hint complete: {alphaFrames.Length} alpha frame(s) in {alphaDir}; first={first}; last={last}; clip.json updated={clipJsonExists}");
            }

            void OnErr(string e) => Debug.LogError($"[CorridorKey] {e}");

            client.LogReceived += OnLog;
            client.ProgressReceived += OnProgress;
            client.DiagnosticResultReceived += OnDiag;
            client.BridgeCommandDoneReceived += OnDone;
            client.ErrorReceived += OnErr;

            Debug.Log($"[CorridorKey] Starting BiRefNet hint from frames: {framesDir} (usage=Matting)");
            var err = client.TrySendJson(new BiRefNetHintStdin
            {
                cmd = "alpha.birefnet_hint",
                request_id = requestId,
                clip_root = clipRoot,
                frames_dir = framesDir,
                usage = "Matting",
                overwrite = true
            });
            if (err != null)
                Finish($"[CorridorKey] BiRefNet hint aborted: {err}");
        }

        static bool IsPathUnder(string rootPath, string candidatePath)
        {
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            var cand = Path.GetFullPath(candidatePath);
            return cand.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsEditorSetupFailure(string summary)
        {
            if (string.IsNullOrEmpty(summary))
                return false;
            return summary.Contains("EditorPrefs", StringComparison.Ordinal)
                   || summary.Contains("unity_bridge.py not found", StringComparison.OrdinalIgnoreCase)
                   || summary.Contains("missing EZ root", StringComparison.OrdinalIgnoreCase)
                   || summary.Contains("Python executable not found", StringComparison.OrdinalIgnoreCase)
                   || summary.Contains("Failed to start Python", StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        class BridgeDiagStdin
        {
            public string cmd = string.Empty;
            public string request_id = string.Empty;
            public string path = string.Empty;
            public string usage = string.Empty;
        }

        [Serializable]
        class MediaExtractFramesStdin
        {
            public string cmd = "media.extract_frames";
            public string request_id = string.Empty;
            public string input_path = string.Empty;
            public string output_dir = string.Empty;
            public bool overwrite;
        }

        [Serializable]
        class GvmHintStdin
        {
            public string cmd = "alpha.gvm_hint";
            public string request_id = string.Empty;
            public string clip_root = string.Empty;
            public string frames_dir = string.Empty;
            public bool overwrite;
        }

        [Serializable]
        class BiRefNetHintStdin
        {
            public string cmd = "alpha.birefnet_hint";
            public string request_id = string.Empty;
            public string clip_root = string.Empty;
            public string frames_dir = string.Empty;
            public string usage = "Matting";
            public bool overwrite;
        }
    }
}
