#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace CorridorKey.Editor.UI.Annotations
{
    /// <summary>Raster EZ-style strokes into an RGBA buffer (top row = screen top, matches UITK / EZ image y).</summary>
    public static class AnnotationRasterUtil
    {
        static readonly Color32 FgColor = new Color32(80, 255, 80, 200);
        static readonly Color32 BgColor = new Color32(255, 72, 72, 200);

        public static void Clear(Color32[] dest, int w, int h)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (var i = 0; i < w * h; i++)
                dest[i] = clear;
        }

        public static void RasterizeStrokes(Color32[] dest, int w, int h, IReadOnlyList<AnnotationStrokeData>? strokes)
        {
            Clear(dest, w, h);
            if (strokes == null || strokes.Count == 0)
                return;

            foreach (var stroke in strokes)
                RasterizeOneStroke(dest, w, h, stroke);
        }

        static void RasterizeOneStroke(Color32[] dest, int w, int h, AnnotationStrokeData stroke)
        {
            var isFg = stroke.brush_type != "bg";
            var c = isFg ? FgColor : BgColor;
            var r = Mathf.Max(1f, stroke.radius);
            var n = stroke.px.Count;
            if (n == 0 || n != stroke.py.Count)
                return;

            for (var i = 0; i < n; i++)
            {
                var x = stroke.px[i];
                var y = stroke.py[i];
                FillCircle(dest, w, h, x, y, r, c);
            }

            for (var i = 1; i < n; i++)
                ThickLine(dest, w, h,
                    stroke.px[i - 1], stroke.py[i - 1],
                    stroke.px[i], stroke.py[i], r, c);
        }

        static void ThickLine(Color32[] dest, int w, int h, float x0, float y0, float x1, float y1, float radius, Color32 c)
        {
            var minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(x0, x1) - radius - 2), 0, w - 1);
            var maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(x0, x1) + radius + 2), 0, w - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(y0, y1) - radius - 2), 0, h - 1);
            var maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(y0, y1) + radius + 2), 0, h - 1);

            var r2 = radius * radius;
            for (var iy = minY; iy <= maxY; iy++)
            {
                for (var ix = minX; ix <= maxX; ix++)
                {
                    var px = ix + 0.5f;
                    var py = iy + 0.5f;
                    if (DistancePointToSegmentSq(px, py, x0, y0, x1, y1) <= r2)
                        PlotTopDown(dest, w, h, ix, iy, c);
                }
            }
        }

        static float DistancePointToSegmentSq(float px, float py, float x0, float y0, float x1, float y1)
        {
            var vx = x1 - x0;
            var vy = y1 - y0;
            var wx = px - x0;
            var wy = py - y0;
            var c1 = vx * wx + vy * wy;
            if (c1 <= 0f)
                return (px - x0) * (px - x0) + (py - y0) * (py - y0);
            var c2 = vx * vx + vy * vy;
            if (c2 <= c1)
                return (px - x1) * (px - x1) + (py - y1) * (py - y1);
            var t = c1 / c2;
            var projX = x0 + t * vx;
            var projY = y0 + t * vy;
            var dx = px - projX;
            var dy = py - projY;
            return dx * dx + dy * dy;
        }

        static void FillCircle(Color32[] dest, int w, int h, float cx, float cy, float radius, Color32 c)
        {
            var minX = Mathf.Clamp(Mathf.FloorToInt(cx - radius - 1), 0, w - 1);
            var maxX = Mathf.Clamp(Mathf.CeilToInt(cx + radius + 1), 0, w - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt(cy - radius - 1), 0, h - 1);
            var maxY = Mathf.Clamp(Mathf.CeilToInt(cy + radius + 1), 0, h - 1);
            var r2 = radius * radius;
            for (var iy = minY; iy <= maxY; iy++)
            {
                for (var ix = minX; ix <= maxX; ix++)
                {
                    var dx = ix + 0.5f - cx;
                    var dy = iy + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2)
                        PlotTopDown(dest, w, h, ix, iy, c);
                }
            }
        }

        /// <summary>ix, iy are top-down image coordinates (y=0 top).</summary>
        static void PlotTopDown(Color32[] dest, int w, int h, int ix, int iy, Color32 c)
        {
            var unityY = h - 1 - iy;
            if (unityY < 0 || unityY >= h)
                return;
            var idx = unityY * w + ix;
            dest[idx] = BlendOver(dest[idx], c);
        }

        static Color32 BlendOver(Color32 under, Color32 over)
        {
            if (over.a == 0)
                return under;
            if (under.a == 0)
                return over;
            var outA = over.a + under.a * (255 - over.a) / 255;
            if (outA < 1)
                return new Color32(0, 0, 0, 0);
            var t = over.a / (float)outA;
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(over.r * t + under.r * (1 - t)), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(over.g * t + under.g * (1 - t)), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(over.b * t + under.b * (1 - t)), 0, 255),
                (byte)Mathf.Clamp(outA, 0, 255));
        }
    }
}
