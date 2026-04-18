#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor;
using CorridorKey.Editor.Backend;
using CorridorKey.Editor.UI;
using CorridorKey.Editor.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.Integration
{
    /// <summary>
    /// Runs <c>alpha.gvm_hint</c> via <see cref="ProcessBackendClient"/>, then loads the first plate frame and matching
    /// AlphaHint into INPUT/OUTPUT and feeds the A/B wipe (CPU + GPU), mirroring <see cref="BiRefNetViewerIntegration"/>.
    /// </summary>
    public sealed class GvmViewerIntegration : IDisposable
    {
        readonly ProcessBackendClient _backend;
        readonly VisualElement _body;
        readonly SampleAbComparisonRenderer? _sampleAb;
        readonly GpuAbComparisonRenderer? _gpuAb;
        readonly DualViewerChromeController? _dualViewerChrome;

        Image? _inputImage;
        Image? _outputImage;

        string? _pendingRequestId;
        readonly Action<QueueJobVm, string>? _onQueueJobFailed;

        // Model download tracking
        string? _pendingDownloadRequestId;
        QueueJobVm? _queuedJobAfterDownload;

        public GvmViewerIntegration(
            ProcessBackendClient backend,
            VisualElement body,
            SampleAbComparisonRenderer? sampleAb,
            GpuAbComparisonRenderer? gpuAb,
            DualViewerChromeController? dualViewerChrome = null,
            Action<QueueJobVm, string>? onQueueJobFailed = null)
        {
            _backend = backend;
            _body = body;
            _sampleAb = sampleAb;
            _gpuAb = gpuAb;
            _dualViewerChrome = dualViewerChrome;
            _onQueueJobFailed = onQueueJobFailed;
            _backend.BridgeCommandDoneReceived += OnBridgeCommandDone;
        }

        public void Dispose()
        {
            _backend.BridgeCommandDoneReceived -= OnBridgeCommandDone;
        }

        /// <summary>Starts GVM alpha hint for the default <see cref="CorridorKeyDataPaths"/> clip.</summary>
        public void RequestGvmForDefaultClip(QueueJobVm? queueRow = null)
        {
            if (!CorridorKeyDataPaths.TryGetDefaultTestClip(out var clipRoot, out var framesDir))
            {
                Debug.LogError(
                    "[CorridorKey] GVM: default test clip not found. Expected CorridorKeyData with Frames extracted.");
                FailQueueRow(queueRow, "No default clip");
                return;
            }

            if (!CorridorKeyDataPaths.IsPathUnderProject(clipRoot))
            {
                Debug.LogError($"[CorridorKey] GVM: refusing clip_root outside project: {clipRoot}");
                FailQueueRow(queueRow, "Invalid clip path");
                return;
            }

            // Check if GVM model is installed first
            CheckAndDownloadGvmModel(queueRow, clipRoot, framesDir);
        }

        void CheckAndDownloadGvmModel(QueueJobVm? queueRow, string clipRoot, string framesDir)
        {
            var checkRequestId = Guid.NewGuid().ToString("N");
            _pendingDownloadRequestId = checkRequestId;
            _queuedJobAfterDownload = queueRow;

            var checkPayload = new
            {
                cmd = "model.is_installed",
                request_id = checkRequestId,
                model_name = "gvm"
            };

            var err = _backend.TrySendJson(checkPayload);
            if (err != null)
            {
                _pendingDownloadRequestId = null;
                _queuedJobAfterDownload = null;
                FailQueueRow(queueRow, $"Model check failed: {err}");
                Debug.LogError($"[CorridorKey] GVM model check send failed — {err}");
                return;
            }

            var status = _body.Q<Label>("viewer-shared-status-label");
            if (status != null)
                status.text = "Checking GVM model...";

            Debug.Log($"[CorridorKey] Checking GVM model installation (request_id={checkRequestId}).");
        }

        void StartGvmAfterModelCheck(QueueJobVm? queueRow, string clipRoot, string framesDir)
        {
            var requestId = queueRow != null ? queueRow.JobId : Guid.NewGuid().ToString("N");
            if (queueRow != null)
                queueRow.RequestId = requestId;

            _pendingRequestId = requestId;

            var payload = new GvmHintStdin
            {
                request_id = requestId,
                clip_root = clipRoot,
                frames_dir = framesDir,
                overwrite = true,
            };

            var err = _backend.TrySendJson(payload);
            if (err != null)
            {
                _pendingRequestId = null;
                FailQueueRow(queueRow, err);
                Debug.LogError($"[CorridorKey] GVM: bridge send failed — {err}");
                return;
            }

            var status = _body.Q<Label>("viewer-shared-status-label");
            if (status != null)
                status.text = "GVM… (see Console)";

            Debug.Log($"[CorridorKey] GVM started (request_id={requestId}).");
        }

        void FailQueueRow(QueueJobVm? queueRow, string detail)
        {
            if (queueRow == null)
                return;
            _onQueueJobFailed?.Invoke(queueRow, detail);
        }

        void OnBridgeCommandDone(BridgeCommandDonePayload p)
        {
            // Handle model download check
            if (!string.IsNullOrEmpty(_pendingDownloadRequestId) && p.RequestId == _pendingDownloadRequestId)
            {
                _pendingDownloadRequestId = null;
                var status = _body.Q<Label>("viewer-shared-status-label");

                if (p.Cmd == "model.is_installed")
                {
                    if (p.Ok)
                    {
                        // Model is installed, proceed with GVM
                        Debug.Log("[CorridorKey] GVM model is installed, starting GVM alpha generation.");
                        if (CorridorKeyDataPaths.TryGetDefaultTestClip(out var defaultClipRoot, out var defaultFramesDir))
                        {
                            StartGvmAfterModelCheck(_queuedJobAfterDownload, defaultClipRoot, defaultFramesDir);
                        }
                        else
                        {
                            FailQueueRow(_queuedJobAfterDownload, "No default clip after model check");
                        }
                    }
                    else
                    {
                        // Model not installed, start download
                        Debug.Log("[CorridorKey] GVM model not installed, starting download.");
                        if (status != null)
                            status.text = "Downloading GVM model...";
                        
                        var downloadRequestId = Guid.NewGuid().ToString("N");
                        _pendingDownloadRequestId = downloadRequestId;
                        
                        var downloadPayload = new
                        {
                            cmd = "model.download_gvm",
                            request_id = downloadRequestId
                        };
                        
                        var err = _backend.TrySendJson(downloadPayload);
                        if (err != null)
                        {
                            _pendingDownloadRequestId = null;
                            _queuedJobAfterDownload = null;
                            FailQueueRow(_queuedJobAfterDownload, $"Download start failed: {err}");
                            Debug.LogError($"[CorridorKey] GVM download send failed — {err}");
                        }
                    }
                    _queuedJobAfterDownload = null;
                    return;
                }
                else if (p.Cmd == "model.download_gvm")
                {
                    if (p.Ok)
                    {
                        // Download succeeded, now start GVM
                        Debug.Log("[CorridorKey] GVM model download completed, starting GVM alpha generation.");
                        if (CorridorKeyDataPaths.TryGetDefaultTestClip(out var downloadClipRoot, out var downloadFramesDir))
                        {
                            StartGvmAfterModelCheck(_queuedJobAfterDownload, downloadClipRoot, downloadFramesDir);
                        }
                        else
                        {
                            FailQueueRow(_queuedJobAfterDownload, "No default clip after download");
                        }
                    }
                    else
                    {
                        // Download failed
                        FailQueueRow(_queuedJobAfterDownload, $"GVM model download failed: {p.Summary}");
                        Debug.LogError($"[CorridorKey] GVM model download failed: {p.Summary}");
                        if (status != null)
                            status.text = "GVM model download failed";
                    }
                    _queuedJobAfterDownload = null;
                    return;
                }
            }

            // Handle GVM alpha generation
            if (string.IsNullOrEmpty(_pendingRequestId) || p.RequestId != _pendingRequestId)
                return;
            if (p.Cmd != "alpha.gvm_hint")
                return;

            _pendingRequestId = null;

            var statusLabel = _body.Q<Label>("viewer-shared-status-label");
            if (!p.Ok)
            {
                if (statusLabel != null)
                    statusLabel.text = "GVM failed";
                Debug.LogError($"[CorridorKey] GVM finished with error: {p.Summary}");
                return;
            }

            if (!CorridorKeyDataPaths.TryGetDefaultTestClip(out var resultClipRoot, out var resultFramesDir))
            {
                if (statusLabel != null)
                    statusLabel.text = "Ready";
                return;
            }

            if (!TryGetFirstFrameAndAlphaPaths(resultClipRoot, resultFramesDir, out var framePath, out var alphaPath))
            {
                Debug.LogWarning("[CorridorKey] GVM ok but could not resolve first frame / alpha paths.");
                if (statusLabel != null)
                    statusLabel.text = "Ready";
                return;
            }

            ApplyDualViewTextures(framePath, alphaPath);
            _sampleAb?.SetComparisonSourcesFromAbsoluteFiles(framePath, alphaPath);
            _gpuAb?.SetComparisonSourcesFromAbsoluteFiles(framePath, alphaPath);

            _dualViewerChrome?.SelectViewModeById("alpha");

            if (statusLabel != null)
                statusLabel.text = "Ready — GVM alpha ready (toggle A/B to compare)";

            Debug.Log(
                $"[CorridorKey] GVM UI: INPUT={Path.GetFileName(framePath)} OUTPUT(alpha)={Path.GetFileName(alphaPath)}");
        }

        static bool TryGetFirstFrameAndAlphaPaths(string clipRoot, string framesDir, out string framePath, out string alphaPath)
        {
            framePath = "";
            alphaPath = "";

            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp",
            };

            var files = Directory.GetFiles(framesDir)
                .Where(p => exts.Contains(Path.GetExtension(p)))
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
                return false;

            framePath = files[0];
            var stem = Path.GetFileNameWithoutExtension(framePath);
            alphaPath = Path.Combine(clipRoot, "AlphaHint", stem + ".png");
            return File.Exists(alphaPath);
        }

        void ApplyDualViewTextures(string framePath, string alphaPath)
        {
            var inTex = TextureFileLoader.LoadReadableFromFile(framePath);
            var alphaTex = TextureFileLoader.LoadReadableFromFile(alphaPath);
            if (inTex == null || alphaTex == null)
            {
                Debug.LogWarning("[CorridorKey] GVM: failed to load preview textures from disk.");
                return;
            }

            EnsurePaneImage("viewer-input", ref _inputImage);
            EnsurePaneImage("viewer-output", ref _outputImage);
            if (_inputImage == null || _outputImage == null)
                return;

            var inPh = _body.Q<Label>("viewer-input-placeholder-label");
            var outPh = _body.Q<Label>("viewer-output-placeholder-label");
            if (inPh != null)
                inPh.style.display = DisplayStyle.None;
            if (outPh != null)
                outPh.style.display = DisplayStyle.None;

            ReplacePaneTexture(_inputImage, inTex);
            ReplacePaneTexture(_outputImage, alphaTex);
            _inputImage.style.display = DisplayStyle.Flex;
            _outputImage.style.display = DisplayStyle.Flex;
            _inputImage.MarkDirtyRepaint();
            _outputImage.MarkDirtyRepaint();
        }

        static void ReplacePaneTexture(Image img, Texture2D tex)
        {
            if (img.image is Texture2D oldTex && oldTex != tex)
                UnityEngine.Object.DestroyImmediate(oldTex);
            img.image = tex;
        }

        void EnsurePaneImage(string paneName, ref Image? slot)
        {
            if (slot != null)
                return;

            var surface = _body.Q<VisualElement>($"{paneName}-surface");
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
    }
}
