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

            var rawMessage = payload.Message ?? string.Empty;
            var msg = string.IsNullOrEmpty(payload.LoggerName)
                ? $"{Prefix} {rawMessage}"
                : $"{Prefix}[{payload.LoggerName}] {rawMessage}";

            var level = (payload.Level ?? string.Empty).ToUpperInvariant();
            if (level.Contains("ERROR") || level.Contains("CRITICAL"))
            {
                // python.stderr mixes warnings, traceback frames, and true terminal exceptions.
                // Classify more carefully so Unity users don't see benign warnings as hard errors.
                if (string.Equals(payload.LoggerName, "python.stderr", StringComparison.OrdinalIgnoreCase))
                {
                    if (LooksLikePythonBlankStderr(rawMessage))
                    {
                        Debug.Log(msg);
                        return;
                    }

                    if (LooksLikePythonWarning(payload.Message))
                    {
                        Debug.LogWarning(msg);
                        return;
                    }

                    if (LooksLikePythonTracebackFrame(payload.Message))
                    {
                        Debug.Log(msg);
                        return;
                    }
                }

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

        static bool LooksLikePythonBlankStderr(string message)
        {
            if (string.IsNullOrEmpty(message))
                return true;
            for (var i = 0; i < message.Length; i++)
            {
                var ch = message[i];
                if (char.IsWhiteSpace(ch))
                    continue;
                if (char.IsControl(ch))
                    continue;
                return false;
            }
            return true;
        }

        static bool LooksLikePythonWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            return message.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("UserWarning", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("DeprecationWarning", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("Skipping the post-processing step due to the error above", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool LooksLikePythonTracebackFrame(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // Traceback frame lines usually look like:
            //   File "...", line N, in ...
            //   some_code(...)
            var trimmed = message.TrimStart();
            if (trimmed.StartsWith("File \"", StringComparison.Ordinal))
                return true;
            if (trimmed.StartsWith("Traceback (most recent call last):", StringComparison.Ordinal))
                return true;
            if (trimmed.StartsWith("at ", StringComparison.Ordinal))
                return true;
            if (message.StartsWith("  ", StringComparison.Ordinal))
                return true;
            if (string.IsNullOrWhiteSpace(message))
                return true;
            return false;
        }
    }
}
