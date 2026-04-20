#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    public sealed class SampleAbComparisonRenderer : IDisposable
    {
        const int MaxCompositeDimensionPx = 1024;
        const float SplitAntialiasWidthPx = 1.25f;
        const string SampleAPath = "Editor/PrototypeSamples/greenscreen-test-02/frame_000000/Comp/frame_000000.png";
        const string SampleBPath = "Editor/PrototypeSamples/greenscreen-test-02/frame_000000/Processed/frame_000000.png";

        readonly VisualElement _surface;
        readonly VisualElement _overlay;
        readonly VisualElement? _dualViewerHost;
        readonly VisualElement? _viewerColumn;
        readonly Label? _hint;
        readonly Image _image;

        Texture2D? _sourceA;
        Texture2D? _sourceB;
        Texture2D? _composited;
        bool _enabled;
        bool _inputIsLinear;
        Vector2 _midpointNormalized = new Vector2(0.5f, 0.5f);
        float _angleDeg = 90f;

        public SampleAbComparisonRenderer(VisualElement root)
        {
            _surface = root.Q<VisualElement>("viewer-ab-comparison-surface")
                ?? throw new InvalidOperationException("Missing viewer-ab-comparison-surface.");
            _overlay = root.Q<VisualElement>("viewer-ab-overlay")
                ?? throw new InvalidOperationException("Missing viewer-ab-overlay.");
            _dualViewerHost = root.Q<VisualElement>("dual-viewer-host");
            _viewerColumn = root.Q<VisualElement>("viewer-column");
            _hint = root.Q<Label>("viewer-ab-comparison-hint");

            _image = new Image
            {
                name = "viewer-ab-comparison-image",
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore
            };
            _image.style.position = Position.Absolute;
            _surface.Insert(0, _image);

            _surface.RegisterCallback<GeometryChangedEvent>(OnSurfaceGeometryChanged);
            LoadSampleTextures();
            UpdateHintVisibility();
        }

        public void Dispose()
        {
            _surface.UnregisterCallback<GeometryChangedEvent>(OnSurfaceGeometryChanged);
            if (_composited != null)
                UnityEngine.Object.DestroyImmediate(_composited);
            ReleaseSources();
        }

        void ReleaseSources()
        {
            if (_sourceA != null)
            {
                UnityEngine.Object.DestroyImmediate(_sourceA);
                _sourceA = null;
            }

            if (_sourceB != null)
            {
                UnityEngine.Object.DestroyImmediate(_sourceB);
                _sourceB = null;
            }

        }

        /// <summary>Replace A/B wipe sources (e.g. first input frame vs BiRefNet alpha). Pass null to skip a side.</summary>
        public void SetComparisonSourcesFromAbsoluteFiles(string? pathA, string? pathB)
        {
            ReleaseSources();
            if (!string.IsNullOrEmpty(pathA))
                _sourceA = TextureFileLoader.LoadReadableFromFile(pathA);
            if (!string.IsNullOrEmpty(pathB))
                _sourceB = TextureFileLoader.LoadReadableFromFile(pathB);

            UpdateHintVisibility();
            _image.style.display = _enabled && _sourceA != null && _sourceB != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_enabled)
                RebuildComposite();
        }

        /// <summary>Restore embedded prototype Comp/Processed samples (development default).</summary>
        public void RestoreDefaultPrototypeSamples()
        {
            ReleaseSources();
            LoadSampleTextures();
            UpdateHintVisibility();
            _image.style.display = _enabled && _sourceA != null && _sourceB != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_enabled)
                RebuildComposite();
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _image.style.display = enabled && _sourceA != null && _sourceB != null ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateHintVisibility();
            if (!_enabled)
                return;

            RebuildComposite();
        }

        public void SetSplit(Vector2 midpointNormalized, float angleDeg)
        {
            _midpointNormalized = midpointNormalized;
            _angleDeg = angleDeg;
            if (_enabled)
                RebuildComposite();
        }

        public void SetInputIsLinear(bool inputIsLinear)
        {
            if (_inputIsLinear == inputIsLinear)
                return;
            _inputIsLinear = inputIsLinear;
            if (_enabled)
                RebuildComposite();
        }

        void OnSurfaceGeometryChanged(GeometryChangedEvent evt)
        {
            if (_enabled)
                RebuildComposite();
        }

        void LoadSampleTextures()
        {
            _sourceA = LoadReadablePngTexture(SampleAPath);
            _sourceB = LoadReadablePngTexture(SampleBPath);
        }

        void RebuildComposite()
        {
            if (_sourceA == null || _sourceB == null)
                return;

            var container = AbComparisonPreviewMath.ResolveAbPreviewContainerSize(_surface, _dualViewerHost, _viewerColumn);
            var fittedRect = AbComparisonPreviewMath.ComputeAspectFitRect(
                container,
                new Vector2(_sourceA.width, _sourceA.height));
            ApplyPresentationRect(fittedRect);

            var width = Mathf.Clamp(Mathf.RoundToInt(fittedRect.width), 1, MaxCompositeDimensionPx);
            var height = Mathf.Clamp(Mathf.RoundToInt(fittedRect.height), 1, MaxCompositeDimensionPx);
            if (width <= 0 || height <= 0)
                return;

            EnsureCompositeTexture(width, height);

            var pixels = new Color32[width * height];
            var centerPx = new Vector2(_midpointNormalized.x * width, _midpointNormalized.y * height);
            var normal = AbComparisonPreviewMath.GetLineNormalUnitVector(_angleDeg);

            for (var y = 0; y < height; y++)
            {
                var v = height > 1 ? y / (float)(height - 1) : 0f;
                var uiY = (height - 1) - y;
                for (var x = 0; x < width; x++)
                {
                    var u = width > 1 ? x / (float)(width - 1) : 0f;
                    // Evaluate the split in UI space (top-left origin) at pixel centers, not corners.
                    var p = new Vector2(x + 0.5f, uiY + 0.5f);
                    var signedDistance = Vector2.Dot(p - centerPx, normal);
                    var colorA = _sourceA.GetPixelBilinear(u, v);
                    if (_inputIsLinear)
                    {
                        colorA.r = Mathf.LinearToGammaSpace(colorA.r);
                        colorA.g = Mathf.LinearToGammaSpace(colorA.g);
                        colorA.b = Mathf.LinearToGammaSpace(colorA.b);
                    }
                    var colorB = _sourceB.GetPixelBilinear(u, v);
                    var blend = Mathf.Clamp01((signedDistance + SplitAntialiasWidthPx) / (SplitAntialiasWidthPx * 2f));
                    var color = Color.Lerp(colorA, colorB, blend);
                    pixels[(y * width) + x] = color;
                }
            }

            _composited!.SetPixels32(pixels);
            _composited.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            _image.image = _composited;
            _image.MarkDirtyRepaint();
        }

        void EnsureCompositeTexture(int width, int height)
        {
            if (_composited != null && _composited.width == width && _composited.height == height)
                return;

            if (_composited != null)
                UnityEngine.Object.DestroyImmediate(_composited);

            _composited = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CorridorKey-AbSampleComposite"
            };
        }

        void UpdateHintVisibility()
        {
            if (_hint == null)
                return;

            _hint.style.display = _sourceA != null && _sourceB != null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void ApplyPresentationRect(Rect rect)
        {
            _image.style.left = rect.xMin;
            _image.style.top = rect.yMin;
            _image.style.width = rect.width;
            _image.style.height = rect.height;
            _image.style.right = StyleKeyword.Auto;
            _image.style.bottom = StyleKeyword.Auto;

            _overlay.style.left = rect.xMin;
            _overlay.style.top = rect.yMin;
            _overlay.style.width = rect.width;
            _overlay.style.height = rect.height;
            _overlay.style.right = StyleKeyword.Auto;
            _overlay.style.bottom = StyleKeyword.Auto;
        }

        static Texture2D? LoadReadablePngTexture(string relativePath)
        {
            var normalizedRelativePath = relativePath.Replace('\\', '/');
            string? assetPath = null;
            var guids = AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(relativePath)} t:Texture2D");
            for (var i = 0; i < guids.Length; i++)
            {
                var candidate = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (!candidate.EndsWith(normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                assetPath = candidate;
                break;
            }

            if (string.IsNullOrEmpty(assetPath))
                return null;

            var absolutePath = Path.GetFullPath(assetPath);
            if (!File.Exists(absolutePath))
                return null;

            var bytes = File.ReadAllBytes(absolutePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = Path.GetFileNameWithoutExtension(relativePath)
            };

            return texture.LoadImage(bytes, markNonReadable: false) ? texture : null;
        }

    }
}
