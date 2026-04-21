#nullable enable
using System;
using System.IO;
using UnityEngine;

namespace CorridorKey.Editor.Session
{
    /// <summary>EZ <c>session_mixin.py</c> — atomic write of <c>.corridorkey_session.json</c>.</summary>
    public static class CorridorKeySessionPersistence
    {
        public const string SessionFileName = ".corridorkey_session.json";

        public static string? GetSessionPath(string workspaceRootAbsolute)
        {
            if (string.IsNullOrEmpty(workspaceRootAbsolute))
                return null;
            return Path.Combine(workspaceRootAbsolute, SessionFileName);
        }

        public static bool TryRead(string path, out CorridorKeySessionPayload payload)
        {
            payload = new CorridorKeySessionPayload();
            if (!File.Exists(path))
                return false;
            try
            {
                var json = File.ReadAllText(path);
                if (!CorridorKeySessionJsonIo.TryDeserialize(json, out payload))
                    return false;
                if (payload.Version > CorridorKeySessionPayload.SupportedVersion)
                    Debug.LogWarning(
                        $"[CorridorKey] Session version {payload.Version} is newer than supported {CorridorKeySessionPayload.SupportedVersion}.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CorridorKey] Failed to load session: {e.Message}");
                return false;
            }
        }

        public static void Write(string path, CorridorKeySessionPayload payload)
        {
            var tmp = path + ".tmp";
            try
            {
                var json = CorridorKeySessionJsonIo.Serialize(payload);
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CorridorKey] Failed to save session: {e.Message}");
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
