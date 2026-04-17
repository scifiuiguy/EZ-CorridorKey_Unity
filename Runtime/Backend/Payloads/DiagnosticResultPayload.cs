#nullable enable

namespace CorridorKey.Backend.Payloads
{
    /// <summary>
    /// Phase 1 bridge: result of a <c>diag.*</c> command from <c>unity_bridge.py</c>.
    /// </summary>
    public sealed class DiagnosticResultPayload
    {
        public DiagnosticResultPayload(string requestId, string diag, bool ok, string summary)
        {
            RequestId = requestId;
            Diag = diag;
            Ok = ok;
            Summary = summary;
        }

        public string RequestId { get; }
        public string Diag { get; }
        public bool Ok { get; }
        public string Summary { get; }
    }
}
