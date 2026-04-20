#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor.Backend;
using CorridorKey.Editor.UI;
using CorridorKey.Editor.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.Integration
{
    /// <summary>
    /// Runs <c>alpha.matanyone2_hint</c> via <see cref="ProcessBackendClient"/>, then loads first frame + alpha
    /// into INPUT/OUTPUT panes and updates A/B comparison renderers.
    /// </summary>
    public sealed class MatAnyoneViewerIntegration : IDisposable
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

        public MatAnyoneViewerIntegration(
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

        public void RequestMatAnyoneForDefaultClip(QueueJobVm? queueRow = null)
        {
            if (!CorridorKeyDataPaths.TryGetDefaultTestClip(out var clipRoot, out var framesDir))
            {
                Debug.LogError("[CorridorKey] MatAnyone2: default test clip not found.");
                FailQueueRow(queueRow, "No default clip");
                return;
            }

            if (!CorridorKeyDataPaths.IsPathUnderProject(clipRoot))
            {
                Debug.LogError($"[CorridorKey] MatAnyone2: refusing clip_root outside project: {clipRoot}");
                FailQueueRow(queueRow, "Invalid clip path");
                return;
            }

            var requestId = queueRow != null ? queueRow.JobId : Guid.NewGuid().ToString("N");
            if (queueRow != null)
                queueRow.RequestId = requestId;

            _pendingRequestId = requestId;
            var payload = new MatAnyoneHintStdin
            {
                cmd = "alpha.matanyone2_hint",
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
                Debug.LogError($"[CorridorKey] MatAnyone2: bridge send failed — {err}");
                return;
            }

            var status = _body.Q<Label>("viewer-shared-status-label");
            if (status != null)
                status.text = "MatAnyone2... (see Console)";
            Debug.Log($"[CorridorKey] MatAnyone2 started (request_id={requestId}).");
        }

        void FailQueueRow(QueueJobVm? queueRow, string detail)
        {
            if (queueRow == null)
                return;
            _onQueueJobFailed?.Invoke(queueRow, detail);
        }

        void OnBridgeCommandDone(BridgeCommandDonePayload p)
        {
            if (string.IsNullOrEmpty(_pendingRequestId) || p.RequestId != _pendingRequestId)
                return;
            if (p.Cmd != "alpha.matanyone2_hint")
                return;

            _pendingRequestId = null;
            var status = _body.Q<Label>("viewer-shared-status-label");
            if (!p.Ok)
            {
                if (status != null)
                    status.text = "MatAnyone2 failed";
                Debug.LogError($"[CorridorKey] MatAnyone2 finished with error: {p.Summary}");
                return;
            }

            if (!CorridorKeyDataPaths.TryGetDefaultTestClip(out var clipRoot, out var framesDir))
            {
                if (status != null)
                    status.text = "Ready";
                return;
            }

            if (!TryGetFirstFrameAndAlphaPaths(clipRoot, framesDir, out var framePath, out var alphaPath))
            {
                Debug.LogWarning("[CorridorKey] MatAnyone2 ok but could not resolve first frame / alpha paths.");
                if (status != null)
                    status.text = "Ready";
                return;
            }

            ApplyDualViewTextures(framePath, alphaPath);
            _sampleAb?.SetComparisonSourcesFromAbsoluteFiles(framePath, alphaPath);
            _gpuAb?.SetComparisonSourcesFromAbsoluteFiles(framePath, alphaPath);
            _dualViewerChrome?.SelectViewModeById("alpha");
            if (status != null)
                status.text = "Ready — MatAnyone2 alpha ready (toggle A/B to compare)";
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
                Debug.LogWarning("[CorridorKey] Failed to load MatAnyone2 preview textures from disk.");
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
