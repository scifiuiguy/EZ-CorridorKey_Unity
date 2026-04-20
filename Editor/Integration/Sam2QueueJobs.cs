#nullable enable
using System;
using System.IO;
using CorridorKey.Editor;
using CorridorKey.Editor.ViewModels;

namespace CorridorKey.Editor.Integration
{
    /// <summary>Queue rows for TRACK flow: setup/install and mask tracking.</summary>
    public static class Sam2QueueJobs
    {
        public static QueueJobVm CreateSetupJobVm()
        {
            return new QueueJobVm(
                Guid.NewGuid().ToString("N"),
                QueueJobKind.Sam2Setup,
                "SAM2 Setup",
                ResolveDefaultClipLabel(),
                QueueJobStatus.Queued)
            {
                CurrentFrame = 0,
                TotalFrames = 100,
                Detail = "Queued",
            };
        }

        public static QueueJobVm CreateTrackJobVm()
        {
            return new QueueJobVm(
                Guid.NewGuid().ToString("N"),
                QueueJobKind.Sam2Track,
                "SAM2 Track",
                ResolveDefaultClipLabel(),
                QueueJobStatus.Queued)
            {
                CurrentFrame = 0,
                TotalFrames = 0,
                Detail = "Queued",
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
