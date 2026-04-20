#nullable enable
using System;
using System.IO;
using CorridorKey.Editor;
using CorridorKey.Editor.ViewModels;

namespace CorridorKey.Editor.Integration
{
    /// <summary>MatAnyone2 alpha-hint queue row metadata.</summary>
    public static class MatAnyoneAlphaHintQueueJob
    {
        public static QueueJobVm CreateJobVm()
        {
            var clipLabel = ResolveDefaultClipLabel();
            return new QueueJobVm(
                Guid.NewGuid().ToString("N"),
                QueueJobKind.MatAnyoneAlphaHint,
                "MATANYONE2",
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
