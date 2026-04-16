#nullable enable
using System;
using CorridorKey.Backend.Payloads;

namespace CorridorKey.Backend
{
    /// <summary>
    /// EZ parity: contract surface for <c>backend/service/core.py</c> (<c>CorridorKeyService</c>) via a Python process.
    /// </summary>
    public interface IBackendClient : IDisposable
    {
        event Action<HealthPayload>? HealthReceived;
        event Action<LogPayload>? LogReceived;
        event Action<ProgressPayload>? ProgressReceived;
        event Action<ClipStatePayload>? ClipStateReceived;
        event Action<string>? ErrorReceived;

        void RequestHealthCheck();
        void Cancel();
    }
}
