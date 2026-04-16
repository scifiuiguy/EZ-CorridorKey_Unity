using System;
using CorridorKey.Backend.Payloads;
using UnityEngine;

namespace CorridorKey.Editor.Backend
{
    /// <summary>
    /// Maps bridge <see cref="LogPayload"/> to Unity Console (<see cref="Debug"/>).
    /// Call from the main thread (same as other UI-facing backend events).
    /// </summary>
    public static class BackendLogForwarder
    {
        const string Prefix = "[CorridorKey]";

        public static void Forward(LogPayload payload)
        {
            if (payload == null)
                return;

            var msg = string.IsNullOrEmpty(payload.LoggerName)
                ? $"{Prefix} {payload.Message}"
                : $"{Prefix}[{payload.LoggerName}] {payload.Message}";

            var level = (payload.Level ?? string.Empty).ToUpperInvariant();
            if (level.Contains("ERROR") || level.Contains("CRITICAL"))
            {
                // Windows Store python.exe prints this to stderr; treat as setup guidance, not a hard engine failure.
                if (LooksLikeWindowsPythonStubStderr(payload.Message))
                {
                    Debug.LogWarning(msg);
                    return;
                }

                Debug.LogError(msg);
                return;
            }

            if (level.Contains("WARN"))
            {
                Debug.LogWarning(msg);
                return;
            }

            Debug.Log(msg);
        }

        static bool LooksLikeWindowsPythonStubStderr(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            if (message.IndexOf("Microsoft Store", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return message.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
