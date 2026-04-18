#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CorridorKey.Editor.UI.Annotations
{
    /// <summary>Load/save <c>annotations.json</c> next to EZ (list of points per stroke, fg/bg, radius). No System.Text.Json dependency.</summary>
    public static class AnnotationJsonIo
    {
        public const string FileName = "annotations.json";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static Dictionary<int, List<AnnotationStrokeData>> Load(string clipRoot)
        {
            var dict = new Dictionary<int, List<AnnotationStrokeData>>();
            if (string.IsNullOrEmpty(clipRoot))
                return dict;
            var path = Path.Combine(clipRoot, FileName);
            if (!File.Exists(path))
                return dict;

            try
            {
                var json = File.ReadAllText(path);
                ParseInto(json, dict);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CorridorKey] annotations.json load failed: {e.Message}");
            }

            return dict;
        }

        public static void Save(string clipRoot, IReadOnlyDictionary<int, List<AnnotationStrokeData>> byStem)
        {
            if (string.IsNullOrEmpty(clipRoot))
                return;

            var path = Path.Combine(clipRoot, FileName);
            try
            {
                var sb = new StringBuilder();
                sb.Append('{');
                var first = true;
                foreach (var kv in byStem)
                {
                    if (kv.Value == null || kv.Value.Count == 0)
                        continue;
                    if (!first)
                        sb.Append(',');
                    first = false;
                    sb.Append('"');
                    sb.Append(kv.Key.ToString(Inv));
                    sb.Append("\":");
                    AppendStrokeArray(sb, kv.Value);
                }

                sb.Append('}');
                var text = sb.ToString();
                if (text == "{}")
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    return;
                }

                File.WriteAllText(path, text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CorridorKey] annotations.json save failed: {e.Message}");
            }
        }

        static void AppendStrokeArray(StringBuilder sb, List<AnnotationStrokeData> strokes)
        {
            sb.Append('[');
            for (var si = 0; si < strokes.Count; si++)
            {
                if (si > 0)
                    sb.Append(',');
                var s = strokes[si];
                sb.Append("{\"brush_type\":\"");
                sb.Append(EscapeJsonString(s.brush_type ?? "fg"));
                sb.Append("\",\"radius\":");
                sb.Append(s.radius.ToString(Inv));
                sb.Append(",\"points\":[");
                for (var pi = 0; pi < s.px.Count; pi++)
                {
                    if (pi > 0)
                        sb.Append(',');
                    sb.Append('[');
                    sb.Append(s.px[pi].ToString(Inv));
                    sb.Append(',');
                    sb.Append(s.py[pi].ToString(Inv));
                    sb.Append(']');
                }

                sb.Append("]}");
            }

            sb.Append(']');
        }

        static string EscapeJsonString(string s)
        {
            return s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        /// <summary>EZ-format object: { "0": [ { strokes } ], ... }</summary>
        static void ParseInto(string raw, Dictionary<int, List<AnnotationStrokeData>> dict)
        {
            var s = raw.Trim();
            if (s.Length < 2 || s[0] != '{' || s[^1] != '}')
                return;
            var i = 1;
            SkipWs(s, ref i);
            while (i < s.Length)
            {
                if (s[i] == '}')
                    break;
                if (!ReadQuotedStem(s, ref i, out var stemIdx))
                    break;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    break;
                i++;
                SkipWs(s, ref i);
                if (!ReadStrokeList(s, ref i, out var list))
                    break;
                if (list.Count > 0)
                    dict[stemIdx] = list;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    SkipWs(s, ref i);
                }
            }
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        static bool ReadQuotedStem(string s, ref int i, out int stemIdx)
        {
            stemIdx = 0;
            if (i >= s.Length || s[i] != '"')
                return false;
            i++;
            var start = i;
            while (i < s.Length && s[i] != '"')
                i++;
            if (i >= s.Length)
                return false;
            var key = s.Substring(start, i - start);
            i++;
            if (!int.TryParse(key, NumberStyles.Integer, Inv, out stemIdx))
                return false;
            return true;
        }

        static bool ReadStrokeList(string s, ref int i, out List<AnnotationStrokeData> list)
        {
            list = new List<AnnotationStrokeData>();
            if (i >= s.Length || s[i] != '[')
                return false;
            i++;
            SkipWs(s, ref i);
            while (i < s.Length && s[i] != ']')
            {
                if (!ReadStrokeObject(s, ref i, out var stroke))
                    return false;
                if (stroke.px.Count > 0)
                    list.Add(stroke);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    SkipWs(s, ref i);
                }
            }

            if (i < s.Length && s[i] == ']')
                i++;
            return true;
        }

        static bool ReadStrokeObject(string s, ref int i, out AnnotationStrokeData stroke)
        {
            stroke = new AnnotationStrokeData();
            if (i >= s.Length || s[i] != '{')
                return false;
            i++;
            SkipWs(s, ref i);
            while (i < s.Length && s[i] != '}')
            {
                if (!ReadPropertyName(s, ref i, out var prop))
                    return false;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    return false;
                i++;
                SkipWs(s, ref i);
                if (prop == "brush_type")
                {
                    if (!ReadJsonString(s, ref i, out var bt))
                        return false;
                    stroke.brush_type = bt;
                }
                else if (prop == "radius")
                {
                    if (!ReadFloat(s, ref i, out var r))
                        return false;
                    stroke.radius = r;
                }
                else if (prop == "points")
                {
                    if (!ReadPointsArray(s, ref i, stroke))
                        return false;
                }
                else
                    return false;

                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    SkipWs(s, ref i);
                }
            }

            if (i < s.Length && s[i] == '}')
                i++;
            return true;
        }

        static bool ReadPropertyName(string s, ref int i, out string name)
        {
            name = "";
            return ReadJsonString(s, ref i, out name);
        }

        static bool ReadJsonString(string s, ref int i, out string value)
        {
            value = "";
            if (i >= s.Length || s[i] != '"')
                return false;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                var c = s[i++];
                if (c == '"')
                    break;
                if (c == '\\' && i < s.Length)
                {
                    var n = s[i++];
                    if (n == '"' || n == '\\' || n == '/')
                        sb.Append(n);
                    else if (n == 'n')
                        sb.Append('\n');
                    else if (n == 'r')
                        sb.Append('\r');
                    else if (n == 't')
                        sb.Append('\t');
                    else
                        sb.Append(n);
                }
                else
                    sb.Append(c);
            }

            value = sb.ToString();
            return true;
        }

        static bool ReadFloat(string s, ref int i, out float v)
        {
            v = 0f;
            var start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
                i++;
            if (i == start)
                return false;
            var span = s.Substring(start, i - start);
            return float.TryParse(span, NumberStyles.Float, Inv, out v);
        }

        static bool ReadPointsArray(string s, ref int i, AnnotationStrokeData stroke)
        {
            if (i >= s.Length || s[i] != '[')
                return false;
            i++;
            SkipWs(s, ref i);
            while (i < s.Length && s[i] != ']')
            {
                if (i >= s.Length || s[i] != '[')
                    return false;
                i++;
                SkipWs(s, ref i);
                if (!ReadFloat(s, ref i, out var x))
                    return false;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ',')
                    return false;
                i++;
                SkipWs(s, ref i);
                if (!ReadFloat(s, ref i, out var y))
                    return false;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ']')
                    return false;
                i++;
                stroke.px.Add(x);
                stroke.py.Add(y);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    SkipWs(s, ref i);
                }
            }

            if (i < s.Length && s[i] == ']')
                i++;
            return true;
        }

    }
}
