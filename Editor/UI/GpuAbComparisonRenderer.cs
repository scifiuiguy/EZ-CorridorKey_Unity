#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    public sealed class GpuAbComparisonRenderer : IDisposable
    {
        const string SampleAPath = "Editor/PrototypeSamples/greenscreen-test-02/frame_000000/Comp/frame_000000.png";
        const string SampleBPath = "Editor/PrototypeSamples/greenscreen-test-02/frame_000000/Processed/frame_000000.png";
        const string ShaderName = "Hidden/CorridorKey/AbSplitPreview";

        readonly VisualElement _surface;
        readonly VisualElement _overlay;
        readonly VisualElement? _dualViewerHost;
        readonly VisualElement? _viewerColumn;
        readonly Label? _hint;
        readonly Image _image;

        int _rebuildGeneration;

        Texture2D? _sourceA;
        Texture2D? _sourceB;
        RenderTexture? _renderTexture;
        Material? _material;
        bool _enabled;
        bool _disposed;
        bool _deferredPreviewRebuildPending;
        Vector2 _midpointNormalized = new Vector2(0.5f, 0.5f);
        float _angleDeg = 90f;

        public GpuAbComparisonRenderer(VisualElement root)
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
                name = "viewer-ab-comparison-image-gpu",
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore
            };
            _image.style.position = Position.Absolute;
            _surface.Insert(0, _image);

            _surface.RegisterCallback<GeometryChangedEvent>(OnSurfaceGeometryChanged);
            LoadSampleTextures();
            LoadMaterial();
            UpdateHintVisibility();
            _image.style.display = DisplayStyle.None;
        }

        public void Dispose()
        {
            _disposed = true;
            CancelDeferredPreviewRebuild();
            _surface.UnregisterCallback<GeometryChangedEvent>(OnSurfaceGeometryChanged);
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
            }
            if (_material != null)
                UnityEngine.Object.DestroyImmediate(_material);
            if (_sourceA != null)
                UnityEngine.Object.DestroyImmediate(_sourceA);
            if (_sourceB != null)
                UnityEngine.Object.DestroyImmediate(_sourceB);
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _image.style.display = enabled && _sourceA != null && _sourceB != null && _material != null ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateHintVisibility();
            if (!_enabled)
                return;

            _rebuildGeneration++;
            var gen = _rebuildGeneration;
            RebuildPreview();

            // UITK often has not finished layout when we first enable; delayCall runs after the editor frame (after layout).
            EditorApplication.delayCall += DelayedRebuild;
            if (_surface.panel != null)
            {
                _surface.panel.visualTree.schedule.Execute(() =>
                {
                    if (!_enabled || gen != _rebuildGeneration)
                        return;
                    RebuildPreview();
                }).ExecuteLater(0);
            }

            void DelayedRebuild()
            {
                if (!_enabled || gen != _rebuildGeneration)
                    return;
                RebuildPreview();
            }
        }

        public void SetSplit(Vector2 midpointNormalized, float angleDeg)
        {
            // SplitChanged runs on every overlay GeometryChanged with the same midpoint/angle when only layout
            // changed (FILES / PARAMETERS / tray) — defer those. When the user drags, values change every move;
            // deferring those made the GPU path a frame behind the CPU path (which rebuilds synchronously).
            const float midEpsSq = 1e-12f;
            const float angleEps = 0.0001f;
            var splitValuesChanged =
                (midpointNormalized - _midpointNormalized).sqrMagnitude > midEpsSq
                || Mathf.Abs(Mathf.DeltaAngle(angleDeg, _angleDeg)) > angleEps;

            _midpointNormalized = midpointNormalized;
            _angleDeg = angleDeg;
            if (!_enabled)
                return;

            if (splitValuesChanged)
            {
                CancelDeferredPreviewRebuild();
                RebuildPreview();
            }
            else
                ScheduleDeferredPreviewRebuild();
        }

        void OnSurfaceGeometryChanged(GeometryChangedEvent evt)
        {
            if (!_enabled || _disposed)
                return;

            ScheduleDeferredPreviewRebuild();
        }

        void CancelDeferredPreviewRebuild()
        {
            if (!_deferredPreviewRebuildPending)
                return;
            EditorApplication.delayCall -= OnDeferredPreviewRebuild;
            _deferredPreviewRebuildPending = false;
        }

        void ScheduleDeferredPreviewRebuild()
        {
            // Sidebar / tray toggles fire many layout passes while geometry is still moving. Rebuilding synchronously
            // can leave UITK repainting against stale state and look like a "screenshot of the window".
            if (_deferredPreviewRebuildPending)
                return;
            _deferredPreviewRebuildPending = true;
            EditorApplication.delayCall += OnDeferredPreviewRebuild;
        }

        void OnDeferredPreviewRebuild()
        {
            EditorApplication.delayCall -= OnDeferredPreviewRebuild;
            _deferredPreviewRebuildPending = false;
            if (_disposed || !_enabled)
                return;
            RebuildPreview();
        }

        void LoadSampleTextures()
        {
            _sourceA = LoadReadablePngTexture(SampleAPath);
            _sourceB = LoadReadablePngTexture(SampleBPath);
        }

        void LoadMaterial()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                return;
            _material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        void RebuildPreview()
        {
            if (_sourceA == null || _sourceB == null || _material == null)
                return;

            var container = AbComparisonPreviewMath.ResolveAbPreviewContainerSize(_surface, _dualViewerHost, _viewerColumn);
            var fittedRect = AbComparisonPreviewMath.ComputeAspectFitRect(
                container,
                new Vector2(_sourceA.width, _sourceA.height));
            ApplyPresentationRect(fittedRect);

            var width = Mathf.Max(1, Mathf.RoundToInt(fittedRect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(fittedRect.height));
            EnsureRenderTexture(width, height);
            if (_renderTexture == null)
                return;

            var normal = AbComparisonPreviewMath.GetLineNormalUnitVector(_angleDeg);
            // Blit binds the source texture as _MainTex only; side A must be sampled from _MainTex in the shader.
            _material.SetTexture("_TexB", _sourceB);
            _material.SetVector("_SplitCenter", new Vector4(_midpointNormalized.x, _midpointNormalized.y, 0f, 0f));
            _material.SetVector("_SplitNormal", new Vector4(normal.x, normal.y, 0f, 0f));
            _material.SetVector("_SplitViewportPx", new Vector4(width, height, 0f, 0f));

            // Bind A explicitly: some Editor frames / layout passes leave _MainTex stale; Blit also sets it from source.
            _material.SetTexture("_MainTex", _sourceA);

            // Source must be non-null (not the backbuffer). First argument also becomes _MainTex for this pass.
            Graphics.Blit(_sourceA, _renderTexture, _material, 0);

            // Do not set image to null: clearing the Image makes UITK repaint with no texture and can sample the
            // Editor window (looks like a "screenshot of itself") — especially when geometry fires often (QUEUE / FILES / PARAMETERS toggles).
            _image.image = _renderTexture;
            _image.MarkDirtyRepaint();
            _surface.MarkDirtyRepaint();
        }

        void EnsureRenderTexture(int width, int height)
        {
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height)
                return;

            // Create and bind the new RT before destroying the old one. If _image still pointed at a destroyed
            // RenderTexture during a layout repaint (geometry thrash from QUEUE / FILES / PARAMETERS), UITK can
            // fall back to sampling the panel/window and look like a "screenshot of itself".
            var oldRt = _renderTexture;
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = "CorridorKey-AbGpuPreview"
            };
            _renderTexture.Create();

            // Only point the Image at the new RT before destroying the previous one. On first allocation there is
            // no old RT — assigning here (before Blit) repaints with an empty RT and brings back the first-open
            // "window screenshot" artifact; RebuildPreview assigns after Graphics.Blit in that case.
            if (oldRt != null)
                _image.image = _renderTexture;

            if (oldRt != null)
            {
                oldRt.Release();
                UnityEngine.Object.DestroyImmediate(oldRt);
            }
        }

        void UpdateHintVisibility()
        {
            if (_hint == null)
                return;

            _hint.style.display =
                (_sourceA != null && _sourceB != null && _material != null) ? DisplayStyle.None : DisplayStyle.Flex;
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
