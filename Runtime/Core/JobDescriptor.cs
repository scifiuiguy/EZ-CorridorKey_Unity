#nullable enable
using System;

namespace CorridorKey
{
    /// <summary>
    /// EZ parity: job snapshot identity from GPU worker / queue.
    /// </summary>
    public sealed class JobDescriptor
    {
        public JobDescriptor(Guid id, JobType type, string? clipName = null)
        {
            Id = id;
            Type = type;
            ClipName = clipName;
        }

        public Guid Id { get; }
        public JobType Type { get; }
        public string? ClipName { get; }
    }
}
