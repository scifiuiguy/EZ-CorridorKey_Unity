#nullable enable

namespace CorridorKey.Backend.Payloads
{
    /// <summary>
    /// Emitted when the bridge finishes handling one stdin command (health, shutdown, or diag.*).
    /// </summary>
    public sealed class BridgeCommandDonePayload
    {
        public BridgeCommandDonePayload(string cmd, string requestId, bool ok, string? summary)
        {
            Cmd = cmd;
            RequestId = requestId;
            Ok = ok;
            Summary = summary;
        }

        public string Cmd { get; }
        public string RequestId { get; }
        public bool Ok { get; }
        public string? Summary { get; }
    }
}
