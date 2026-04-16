#nullable enable

namespace CorridorKey.Backend.Payloads
{
    /// <summary>
    /// Log line forwarded from the Python bridge for <see cref="BackendLogForwarder"/> (Editor).
    /// </summary>
    public sealed class LogPayload
    {
        public LogPayload(string level, string message, string? loggerName = null)
        {
            Level = level;
            Message = message;
            LoggerName = loggerName;
        }

        public string Level { get; }
        public string Message { get; }
        public string? LoggerName { get; }
    }
}
