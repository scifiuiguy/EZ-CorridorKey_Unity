#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor.Backend;
using CorridorKey.Editor.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.Integration
{
    /// <summary>
    /// Runs <c>alpha.birefnet_hint</c> via <see cref="ProcessBackendClient"/>, then loads the first frame and matching
    /// alpha into INPUT/OUTPUT panes and feeds the same files into the A/B wipe (CPU + GPU) renderers.
    /// </summary>
    public sealed class BiRefNetViewerIntegration : IDisposable
    {
        readonly ProcessBackendClient _backend;
        readonly VisualElement _body;
        readonly SampleAbComparisonRenderer? _sampleAb;
        readonly GpuAbComparisonRenderer? _gpuAb;
        readonly DualViewerChromeController? _dualViewerChrome;

        Image? _inputImage;
        Image? _outputImage;

        string? _pendingRequestId;

        public BiRefNetViewerIntegration(
            ProcessBackendClient backend,
            VisualElement body,
            SampleAbComparisonRenderer? sampleAb,
            GpuAbComparisonRenderer? gpuAb,
            DualViewerChromeController? dualViewerChrome = null)
        {
            _backend = backend;
            _body = body;
            _sampleAb = sampleAb;
            _gpuAb = gpuAb;
            _dualViewerChrome = dualViewerChrome;
            _backend.BridgeCommandDoneReceived += OnBridgeCommandDone;
        }

        public void Dispose()
        {
            _backend.BridgeCommandDoneReceived -= OnBridgeCommandDone;
        }

        /// <summary>Starts BiRefNet for the default <see cref="CorridorKeyDataPaths"/> clip (CorridorKeyData test tree).</summary>
        public void RequestBiRefNetForDefaultClip(string usageDisplayName)
        {
            if (!CorridorKeyDataPaths.TryGetDefaultTestClip(out var clipRoot, out var framesDir))
            {
                Debug.LogError(
                    "[CorridorKey] BiRefNet: default test clip not found. Expected CorridorKeyData with Frames extracted.");
                return;
            }

            if (!CorridorKeyDataPaths.IsPathUnderProject(clipRoot))
            {
                Debug.LogError($"[CorridorKey] BiRefNet: refusing clip_root outside project: {clipRoot}");
                return;
            }

            var usageKey = string.IsNullOrWhiteSpace(usageDisplayName)
                ? BiRefNetModelOptions.DefaultDisplayName
                : usageDisplayName.Trim();
            var requestId = Guid.NewGuid().ToString("N");
            _pendingRequestId = requestId;

            var payload = new BiRefNetHintStdin
            {
                cmd = "alpha.birefnet_hint",
                request_id = requestId,
                clip_root = clipRoot,
                frames_dir = framesDir,
                usage = usageKey,
                overwrite = true,
            };

            var err = _backend.TrySendJson(payload);
            if (err != null)
            {
                _pendingRequestId = null;
                Debug.LogError($"[CorridorKey] BiRefNet: bridge send failed — {err}");
                return;
            }

            var status = _body.Q<Label>("viewer-shared-status-label");
            if (status != null)
                status.text = "BiRefNet… (see Console)";

            Debug.Log($"[CorridorKey] BiRefNet started (usage={usageKey}, request_id={requestId}).");
        }

        void OnBridgeCommandDone(BridgeCommandDonePayload p)
        {
            if (string.IsNullOrEmpty(_pendingRequestId) || p.RequestId != _pendingRequestId)
                return;
            if (p.Cmd != "alpha.birefnet_hint")
                return;

            _pendingRequestId = null;

            var status = _body.Q<Label>("viewer-shared-status-label");
            if (!p.Ok)
            {
                if (status != null)
                    status.text = "BiRefNet failed";
                Debug.LogError($"[CorridorKey] BiRefNet finished with error: {p.Summary}");
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
                Debug.LogWarning("[CorridorKey] BiRefNet ok but could not resolve first frame / alpha paths.");
                if (status != null)
                    status.text = "Ready";
                return;
            }

            ApplyDualViewTextures(framePath, alphaPath);
            _sampleAb?.SetComparisonSourcesFromAbsoluteFiles(framePath, alphaPath);
            _gpuAb?.SetComparisonSourcesFromAbsoluteFiles(framePath, alphaPath);

            _dualViewerChrome?.SelectViewModeById("alpha");

            if (status != null)
                status.text = "Ready — BiRefNet alpha ready (toggle A/B to compare)";

            Debug.Log(
                $"[CorridorKey] BiRefNet UI: INPUT={Path.GetFileName(framePath)} OUTPUT(alpha)={Path.GetFileName(alphaPath)}");
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
                Debug.LogWarning("[CorridorKey] Failed to load preview textures from disk.");
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
