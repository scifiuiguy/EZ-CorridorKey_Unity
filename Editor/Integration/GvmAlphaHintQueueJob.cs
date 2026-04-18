#nullable enable
using System;
using System.IO;
using CorridorKey.Editor;
using CorridorKey.Editor.ViewModels;

namespace CorridorKey.Editor.Integration
{
    /// <summary>GVM alpha-hint queue row: same clip label pattern as <see cref="BiRefNetAlphaHintQueueJob"/>.</summary>
    public static class GvmAlphaHintQueueJob
    {
        public static QueueJobVm CreateJobVm()
        {
            var clipLabel = ResolveDefaultClipLabel();
            return new QueueJobVm(
                Guid.NewGuid().ToString("N"),
                QueueJobKind.GvmAlphaHint,
                "GVM",
                clipLabel,
                QueueJobStatus.Running)
            {
                CurrentFrame = 0,
                TotalFrames = 0,
            };
        }

        static string ResolveDefaultClipLabel()
        {
            if (CorridorKeyDataPaths.TryGetDefaultTestClip(out var clipRoot, out _))
            {
                return Path.GetFileName(
                    clipRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            return "No default clip";
        }
    }
}
