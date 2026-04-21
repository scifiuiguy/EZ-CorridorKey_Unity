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

        static readonly HashSet<string> OutputExtensionsSet = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".exr", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp",
        };
        static readonly string[] OutputExtensions = { ".png", ".exr", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp" };

        public static string? TryResolveViewModePath(string clipRoot, string plateFrameAbsolutePath, string modeId)
        {
            if (string.IsNullOrEmpty(clipRoot) || string.IsNullOrEmpty(plateFrameAbsolutePath))
                return null;
            var stem = Path.GetFileNameWithoutExtension(plateFrameAbsolutePath);
            if (string.IsNullOrEmpty(stem))
                return null;

            switch (modeId)
            {
                case "mask":
                    return TryResolveInDir(Path.Combine(clipRoot, "VideoMamaMaskHint"), stem);
                case "alpha":
                    return TryResolveInDir(Path.Combine(clipRoot, "AlphaHint"), stem);
                case "fg":
                    return TryResolveInDir(Path.Combine(clipRoot, "Output", "FG"), stem);
                case "matte":
                    return TryResolveInDir(Path.Combine(clipRoot, "Output", "Matte"), stem);
                case "comp":
                    return TryResolveInDir(Path.Combine(clipRoot, "Output", "Comp"), stem);
                case "proc":
                    return TryResolveInDir(Path.Combine(clipRoot, "Output", "Processed"), stem);
                default:
                    return null;
            }
        }

        static string? TryResolveInDir(string dir, string stem)
        {
            if (!Directory.Exists(dir))
                return null;
            foreach (var ext in OutputExtensions)
            {
                var p = Path.Combine(dir, stem + ext);
                if (File.Exists(p))
                    return p;
            }

            return null;
        }

        public static bool HasAnyFramesForMode(string clipRoot, string modeId)
        {
            if (string.IsNullOrEmpty(clipRoot))
                return false;
            switch (modeId)
            {
                case "input":
                    return Directory.Exists(Path.Combine(clipRoot, "Frames"));
                case "mask":
                    return DirectoryHasAnyFrameLikeFiles(Path.Combine(clipRoot, "VideoMamaMaskHint"));
                case "alpha":
                    return DirectoryHasAnyFrameLikeFiles(Path.Combine(clipRoot, "AlphaHint"));
                case "fg":
                    return DirectoryHasAnyFrameLikeFiles(Path.Combine(clipRoot, "Output", "FG"));
                case "matte":
                    return DirectoryHasAnyFrameLikeFiles(Path.Combine(clipRoot, "Output", "Matte"));
                case "comp":
                    return DirectoryHasAnyFrameLikeFiles(Path.Combine(clipRoot, "Output", "Comp"));
                case "proc":
                    return DirectoryHasAnyFrameLikeFiles(Path.Combine(clipRoot, "Output", "Processed"));
                default:
                    return false;
            }
        }

        static bool DirectoryHasAnyFrameLikeFiles(string dir)
        {
            if (!Directory.Exists(dir))
                return false;
            foreach (var p in Directory.GetFiles(dir))
            {
                if (OutputExtensionsSet.Contains(Path.GetExtension(p)))
                    return true;
            }

            return false;
        }
    }
}
