#nullable enable
using System;

namespace CorridorKey.Backend
{
    /// <summary>
    /// Optional envelope if you prefer a single stream instead of multiple events on <see cref="IBackendClient"/>.
    /// </summary>
    public readonly struct BackendEvent
    {
        public BackendEvent(BackendEventKind kind, object? payload = null)
        {
            Kind = kind;
            Payload = payload;
        }

        public BackendEventKind Kind { get; }
        public object? Payload { get; }
    }

    public enum BackendEventKind
    {
        Health,
        Log,
        Progress,
        ClipState,
        JobDone,
        Error
    }
}
