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
        /// <summary>Phase 1 diagnostics: <c>diag.*</c> results from <c>unity_bridge.py</c>.</summary>
        event Action<DiagnosticResultPayload>? DiagnosticResultReceived;
        /// <summary>One command finished (correlate with <see cref="BridgeCommandDonePayload.RequestId"/>).</summary>
        event Action<BridgeCommandDonePayload>? BridgeCommandDoneReceived;

        void RequestHealthCheck();
        /// <summary>Optional correlation id for <see cref="BridgeCommandDoneReceived"/>.</summary>
        void RequestHealthCheck(string? requestId);
        void Cancel();
    }
}
