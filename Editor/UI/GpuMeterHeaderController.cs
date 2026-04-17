#nullable enable
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Drives the header GPU name + VRAM meter via NVML on Windows Editor. Hides the cluster if NVML is unavailable.
    /// </summary>
    public sealed class GpuMeterHeaderController : System.IDisposable
    {
        const double PollIntervalSeconds = 2.0;

        readonly VisualElement _meterRoot;
        readonly Label _gpuName;
        readonly Label _vramLabel;
        readonly ProgressBar _bar;
        readonly Label _vramText;
        VisualElement? _progressFill;
        VisualElement? _gradientInner;
        NvmlGpuMeter? _nvml;
        double _lastPollTime = double.NegativeInfinity;
        bool _subscribedEditorUpdate;
        bool _vramGradientGeometryHooked;

        // Brand colors — full-bar horizontal ramp (green→yellow→red) clipped by fill width; not a single usage tint.
        static readonly Color VramGreen = new Color(0x22 / 255f, 0xC5 / 255f, 0x5E / 255f);
        static readonly Color VramYellow = new Color(0xFF / 255f, 0xF2 / 255f, 0x03 / 255f);
        static readonly Color VramRed = new Color(0xD1 / 255f, 0x00 / 255f, 0x00 / 255f);

        static Texture2D? s_vramGradientTexture;

        static Texture2D VramGradientTexture
        {
            get
            {
                if (s_vramGradientTexture != null)
                    return s_vramGradientTexture;
                const int w = 256;
                var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                for (var x = 0; x < w; x++)
                {
                    var t = x / (w - 1f);
                    Color c;
                    if (t <= 0.5f)
                        c = Color.Lerp(VramGreen, VramYellow, t * 2f);
                    else
                        c = Color.Lerp(VramYellow, VramRed, (t - 0.5f) * 2f);
                    tex.SetPixel(x, 0, c);
                }

                tex.Apply(false, false);
                s_vramGradientTexture = tex;
                return s_vramGradientTexture;
            }
        }

        public GpuMeterHeaderController(VisualElement mainBodyColumn)
        {
            _meterRoot = mainBodyColumn.Q<VisualElement>("header-gpu-meter")
                ?? throw new System.InvalidOperationException("Missing header-gpu-meter.");
            _gpuName = mainBodyColumn.Q<Label>("header-gpu-name")
                ?? throw new System.InvalidOperationException("Missing header-gpu-name.");
            _vramLabel = mainBodyColumn.Q<Label>("header-vram-label")
                ?? throw new System.InvalidOperationException("Missing header-vram-label.");
            _bar = mainBodyColumn.Q<ProgressBar>("header-vram-bar")
                ?? throw new System.InvalidOperationException("Missing header-vram-bar.");
            _vramText = mainBodyColumn.Q<Label>("header-vram-text")
                ?? throw new System.InvalidOperationException("Missing header-vram-text.");

            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                HideMeter();
                return;
            }

            _nvml = NvmlGpuMeter.TryCreate(out _);
            if (_nvml == null)
            {
                HideMeter();
                return;
            }

            _meterRoot.style.display = DisplayStyle.Flex;
            _bar.lowValue = 0f;
            _bar.highValue = 100f;
            _bar.value = 0f;
            ResolveProgressFill();
            EnsureVramGradientLayer();

            EditorApplication.update += OnEditorUpdate;
            _subscribedEditorUpdate = true;

            PollNow();
        }

        void ResolveProgressFill()
        {
            if (_progressFill != null)
                return;
            _progressFill = _bar.Q<VisualElement>(className: "unity-progress-bar__progress");
        }

        void EnsureVramGradientLayer()
        {
            ResolveProgressFill();
            if (_progressFill == null)
                return;
            if (_gradientInner != null)
            {
                SyncVramGradientWidth();
                return;
            }

            _progressFill.style.backgroundColor = Color.clear;
            _progressFill.style.overflow = Overflow.Hidden;

            _gradientInner = new VisualElement();
            _gradientInner.AddToClassList("corridor-key-vram-gradient-inner");
            _gradientInner.style.position = Position.Absolute;
            _gradientInner.style.left = 0;
            _gradientInner.style.top = 0;
            _gradientInner.style.bottom = 0;
            _gradientInner.style.backgroundImage = new StyleBackground(VramGradientTexture);
            _gradientInner.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            _gradientInner.pickingMode = PickingMode.Ignore;
            _progressFill.Add(_gradientInner);

            if (!_vramGradientGeometryHooked)
            {
                _vramGradientGeometryHooked = true;
                _bar.RegisterCallback<GeometryChangedEvent>(OnBarGeometryChanged);
            }

            SyncVramGradientWidth();
            EditorApplication.delayCall += DelayedSyncVramGradientWidth;
        }

        void OnBarGeometryChanged(GeometryChangedEvent evt) => SyncVramGradientWidth();

        void DelayedSyncVramGradientWidth() => SyncVramGradientWidth();

        void SyncVramGradientWidth()
        {
            ResolveProgressFill();
            if (_progressFill == null || _gradientInner == null)
                return;
            var track = _progressFill.parent;
            if (track == null)
                return;
            var w = track.layout.width;
            if (w <= 0.5f)
                return;
            _gradientInner.style.width = w;
        }

        void HideMeter()
        {
            _meterRoot.style.display = DisplayStyle.None;
        }

        void OnEditorUpdate()
        {
            if (_nvml == null)
            {
                HideMeter();
                return;
            }
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastPollTime < PollIntervalSeconds)
                return;
            _lastPollTime = now;
            PollNow();
        }

        void PollNow()
        {
            if (_nvml == null)
                return;
            if (!_nvml.TrySample(out var mem, out var name))
                return;

            _gpuName.text = name;
            var totalGb = mem.Total / (1024.0 * 1024.0 * 1024.0);
            var usedGb = mem.Used / (1024.0 * 1024.0 * 1024.0);
            var pct = totalGb > 0.0 ? (float)(usedGb / totalGb * 100.0) : 0f;
            _bar.value = pct;
            SyncVramGradientWidth();
            _vramText.text = $"{usedGb:F1}/{totalGb:F1}GB";
        }

        public void Dispose()
        {
            if (_subscribedEditorUpdate)
                EditorApplication.update -= OnEditorUpdate;
            _subscribedEditorUpdate = false;
            if (_vramGradientGeometryHooked)
            {
                _bar.UnregisterCallback<GeometryChangedEvent>(OnBarGeometryChanged);
                _vramGradientGeometryHooked = false;
            }

            _nvml?.Dispose();
            _nvml = null;
        }
    }
}
