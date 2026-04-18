#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Sorted plate frame paths under a clip <c>Frames/</c> directory (EZ / bridge layout).
    /// </summary>
    public static class ClipPlateFramePaths
    {
        static readonly HashSet<string> PlateExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp",
        };

        /// <summary>Returns sorted absolute file paths for plate images in <paramref name="framesDir"/>.</summary>
        public static bool TryCollectSortedPlateFrames(string framesDir, out List<string> sortedPaths)
        {
            sortedPaths = new List<string>();
            if (string.IsNullOrEmpty(framesDir) || !Directory.Exists(framesDir))
                return false;

            foreach (var p in Directory.GetFiles(framesDir))
            {
                if (PlateExtensions.Contains(Path.GetExtension(p)))
                    sortedPaths.Add(p);
            }

            if (sortedPaths.Count == 0)
                return false;

            sortedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        /// <summary>
        /// Alpha-hint PNG for a plate frame: <c>{clipRoot}/AlphaHint/{stem}.png</c> (same stem as plate file).
        /// </summary>
        public static string GetAlphaHintPathForPlateFrame(string clipRoot, string plateFrameAbsolutePath)
        {
            var stem = Path.GetFileNameWithoutExtension(plateFrameAbsolutePath);
            return Path.Combine(clipRoot, "AlphaHint", stem + ".png");
        }
    }
}
