#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    internal static class AbComparisonPreviewMath
    {
        /// <summary>
        /// While the A/B host is hidden or before the first layout pass, <paramref name="surface"/> can measure 0×0.
        /// Fall back to the comparison host parent, then <paramref name="dualViewerHost"/>,
        /// then <paramref name="viewerColumn"/> (parent of the dual row; often has size when dual is still 0).
        /// </summary>
        public static Vector2 ResolveAbPreviewContainerSize(
            VisualElement surface,
            VisualElement? dualViewerHost,
            VisualElement? viewerColumn = null)
        {
            if (surface == null)
                return Vector2.zero;

            var w = surface.layout.width;
            var h = surface.layout.height;
            if (w > 0.5f && h > 0.5f)
                return new Vector2(w, h);

            var p = surface.parent;
            if (p != null)
            {
                w = p.layout.width;
                h = p.layout.height;
                if (w > 0.5f && h > 0.5f)
                    return new Vector2(w, h);
            }

            if (dualViewerHost != null)
            {
                w = dualViewerHost.layout.width;
                h = dualViewerHost.layout.height;
                if (w > 0.5f && h > 0.5f)
                    return new Vector2(w, h);
            }

            if (viewerColumn != null)
            {
                w = viewerColumn.layout.width;
                h = viewerColumn.layout.height;
                if (w > 0.5f && h > 0.5f)
                    return new Vector2(w, h);
            }

            return new Vector2(surface.layout.width, surface.layout.height);
        }

        public static Vector2 GetLineNormalUnitVector(float lineAngleDeg)
        {
            var radians = (lineAngleDeg - 90f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        public static Rect ComputeAspectFitRect(Vector2 containerSize, Vector2 contentSize)
        {
            if (containerSize.x <= 0f || containerSize.y <= 0f || contentSize.x <= 0f || contentSize.y <= 0f)
                return new Rect(0f, 0f, Mathf.Max(1f, containerSize.x), Mathf.Max(1f, containerSize.y));

            var scale = Mathf.Min(containerSize.x / contentSize.x, containerSize.y / contentSize.y);
            var width = Mathf.Max(1f, contentSize.x * scale);
            var height = Mathf.Max(1f, contentSize.y * scale);
            var x = (containerSize.x - width) * 0.5f;
            var y = (containerSize.y - height) * 0.5f;
            return new Rect(x, y, width, height);
        }
    }
}
