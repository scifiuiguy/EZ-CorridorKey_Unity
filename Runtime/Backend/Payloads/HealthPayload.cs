namespace CorridorKey.Backend.Payloads
{
    /// <summary>
    /// Result of a backend health / capability probe.
    /// </summary>
    public sealed class HealthPayload
    {
        public HealthPayload(bool ok, string summary)
        {
            Ok = ok;
            Summary = summary;
        }

        public bool Ok { get; }
        public string Summary { get; }
    }
}
