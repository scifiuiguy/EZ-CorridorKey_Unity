#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CorridorKey.Editor.Session
{
    /// <summary>EZ <c>.corridorkey_session.json</c> read/write — no Newtonsoft (Unity/package optional).</summary>
    public static class CorridorKeySessionJsonIo
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Serialize(CorridorKeySessionPayload p)
        {
            var sb = new StringBuilder(2048);
            sb.Append("{\n");
            AppendInt(sb, "version", p.Version, indent: 1, first: true);
            sb.Append(",\n");
            sb.Append("  \"params\": ");
            AppendInferenceParams(sb, p.Params, indent: 1);
            sb.Append(",\n");
            sb.Append("  \"output_config\": ");
            AppendOutputConfig(sb, p.OutputConfig, indent: 1);
            sb.Append(",\n");
            AppendBool(sb, "live_preview", p.LivePreview, indent: 1);
            if (p.Geometry != null)
            {
                sb.Append(",\n");
                sb.Append("  \"geometry\": ");
                AppendGeometry(sb, p.Geometry, indent: 1);
            }

            if (p.SplitterSizes is { Count: > 0 } ss)
            {
                sb.Append(",\n");
                AppendIntArray(sb, "splitter_sizes", ss, indent: 1);
            }

            if (p.VsplitterSizes is { Count: > 0 } vs)
            {
                sb.Append(",\n");
                AppendIntArray(sb, "vsplitter_sizes", vs, indent: 1);
            }

            if (p.WorkspacePath != null)
            {
                sb.Append(",\n");
                AppendString(sb, "workspace_path", p.WorkspacePath, indent: 1);
            }

            if (p.SelectedClip != null)
            {
                sb.Append(",\n");
                AppendString(sb, "selected_clip", p.SelectedClip, indent: 1);
            }

            if (p.ClipInputIsLinear is { Count: > 0 } d)
            {
                sb.Append(",\n");
                AppendClipLinear(sb, d, indent: 1);
            }

            sb.Append("\n}");
            return sb.ToString();
        }

        public static bool TryDeserialize(string json, out CorridorKeySessionPayload payload)
        {
            payload = new CorridorKeySessionPayload();
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                var i = 0;
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != '{')
                    return false;
                var root = ParseObject(json, ref i);
                if (root == null)
                    return false;
                MapPayload(root, payload);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void MapPayload(Dictionary<string, object?> root, CorridorKeySessionPayload p)
        {
            if (TryGetInt(root, "version", out var v))
                p.Version = v;

            if (root.TryGetValue("params", out var prm) && prm is Dictionary<string, object?> pdict)
                MapInferenceParams(pdict, p.Params);

            if (root.TryGetValue("output_config", out var oc) && oc is Dictionary<string, object?> ocdict)
                MapOutputConfig(ocdict, p.OutputConfig);

            if (root.TryGetValue("live_preview", out var lp) && lp is bool lb)
                p.LivePreview = lb;

            if (root.TryGetValue("geometry", out var geo) && geo is Dictionary<string, object?> gdict)
            {
                p.Geometry = new SessionGeometryPayload();
                if (TryGetInt(gdict, "x", out var gx)) p.Geometry.X = gx;
                if (TryGetInt(gdict, "y", out var gy)) p.Geometry.Y = gy;
                if (TryGetInt(gdict, "width", out var gw)) p.Geometry.Width = gw;
                if (TryGetInt(gdict, "height", out var gh)) p.Geometry.Height = gh;
            }

            if (root.TryGetValue("splitter_sizes", out var sp) && sp is List<object?> spl)
                p.SplitterSizes = MapIntList(spl);

            if (root.TryGetValue("vsplitter_sizes", out var vp) && vp is List<object?> vpl)
                p.VsplitterSizes = MapIntList(vpl);

            if (root.TryGetValue("workspace_path", out var wp) && wp is string ws)
                p.WorkspacePath = ws;

            if (root.TryGetValue("selected_clip", out var sc) && sc is string ssc)
                p.SelectedClip = ssc;

            if (root.TryGetValue("clip_input_is_linear", out var cl) && cl is Dictionary<string, object?> cld)
            {
                p.ClipInputIsLinear = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in cld)
                {
                    if (kv.Value is bool b)
                        p.ClipInputIsLinear[kv.Key] = b;
                }
            }
        }

        static List<int>? MapIntList(List<object?> src)
        {
            var list = new List<int>(src.Count);
            foreach (var o in src)
            {
                if (TryCoerceInt(o, out var x))
                    list.Add(x);
            }

            return list.Count > 0 ? list : null;
        }

        static void MapInferenceParams(Dictionary<string, object?> d, InferenceParamsPayload t)
        {
            if (TryGetBool(d, "input_is_linear", out var il))
                t.InputIsLinear = il;
            if (TryGetFloat(d, "despill_strength", out var ds))
                t.DespillStrength = ds;
            if (TryGetBool(d, "auto_despeckle", out var ad))
                t.AutoDespeckle = ad;
            if (TryGetInt(d, "despeckle_size", out var dz))
                t.DespeckleSize = dz;
            if (TryGetInt(d, "despeckle_dilation", out var dd))
                t.DespeckleDilation = dd;
            if (TryGetInt(d, "despeckle_blur", out var db))
                t.DespeckleBlur = db;
            if (TryGetFloat(d, "refiner_scale", out var rs))
                t.RefinerScale = rs;
            if (TryGetBool(d, "source_passthrough", out var sp))
                t.SourcePassthrough = sp;
            if (d.TryGetValue("edge_erode_px", out var ee))
                t.EdgeErodePx = ee == null ? null : TryCoerceInt(ee, out var ei) ? ei : null;
            if (d.TryGetValue("edge_blur_px", out var eb))
                t.EdgeBlurPx = eb == null ? null : TryCoerceInt(eb, out var ebi) ? ebi : null;
        }

        static void MapOutputConfig(Dictionary<string, object?> d, OutputConfigPayload t)
        {
            if (TryGetBool(d, "fg_enabled", out var x))
                t.FgEnabled = x;
            if (TryGetString(d, "fg_format", out var s))
                t.FgFormat = s;
            if (TryGetBool(d, "matte_enabled", out var m))
                t.MatteEnabled = m;
            if (TryGetString(d, "matte_format", out var ms))
                t.MatteFormat = ms;
            if (TryGetBool(d, "comp_enabled", out var c))
                t.CompEnabled = c;
            if (TryGetString(d, "comp_format", out var cs))
                t.CompFormat = cs;
            if (TryGetBool(d, "processed_enabled", out var pe))
                t.ProcessedEnabled = pe;
            if (TryGetString(d, "processed_format", out var ps))
                t.ProcessedFormat = ps;
            if (TryGetString(d, "exr_compression", out var ex))
                t.ExrCompression = ex;
        }

        static bool TryGetInt(Dictionary<string, object?> d, string key, out int v)
        {
            v = 0;
            if (!d.TryGetValue(key, out var o) || o == null)
                return false;
            return TryCoerceInt(o, out v);
        }

        static bool TryGetFloat(Dictionary<string, object?> d, string key, out float v)
        {
            v = 0f;
            if (!d.TryGetValue(key, out var o) || o == null)
                return false;
            return TryCoerceFloat(o, out v);
        }

        static bool TryGetBool(Dictionary<string, object?> d, string key, out bool v)
        {
            v = false;
            if (!d.TryGetValue(key, out var o) || o == null)
                return false;
            if (o is bool b)
            {
                v = b;
                return true;
            }

            if (o is string str && bool.TryParse(str, out var pb))
            {
                v = pb;
                return true;
            }

            return false;
        }

        static bool TryGetString(Dictionary<string, object?> d, string key, out string v)
        {
            v = "";
            if (!d.TryGetValue(key, out var o) || o == null)
                return false;
            if (o is string s)
            {
                v = s;
                return true;
            }

            v = o.ToString() ?? "";
            return true;
        }

        static bool TryCoerceInt(object? o, out int v)
        {
            v = 0;
            switch (o)
            {
                case int i:
                    v = i;
                    return true;
                case long l:
                    v = (int)l;
                    return true;
                case double d:
                    v = (int)d;
                    return true;
                case float f:
                    v = (int)f;
                    return true;
                default:
                    return int.TryParse(Convert.ToString(o, Inv), NumberStyles.Integer, Inv, out v);
            }
        }

        static bool TryCoerceFloat(object? o, out float v)
        {
            v = 0f;
            switch (o)
            {
                case float f:
                    v = f;
                    return true;
                case double d:
                    v = (float)d;
                    return true;
                case int i:
                    v = i;
                    return true;
                case long l:
                    v = l;
                    return true;
                default:
                    return float.TryParse(Convert.ToString(o, Inv), NumberStyles.Float, Inv, out v);
            }
        }

        #region Serialize helpers

        static void AppendInferenceParams(StringBuilder sb, InferenceParamsPayload p, int indent)
        {
            var pad = new string(' ', (indent + 1) * 2);
            sb.Append("{\n");
            sb.Append(pad); AppendBool(sb, "input_is_linear", p.InputIsLinear, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendFloat(sb, "despill_strength", p.DespillStrength, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendBool(sb, "auto_despeckle", p.AutoDespeckle, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendInt(sb, "despeckle_size", p.DespeckleSize, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendInt(sb, "despeckle_dilation", p.DespeckleDilation, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendInt(sb, "despeckle_blur", p.DespeckleBlur, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendFloat(sb, "refiner_scale", p.RefinerScale, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendBool(sb, "source_passthrough", p.SourcePassthrough, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); sb.Append("\"edge_erode_px\": "); AppendNullOrInt(sb, p.EdgeErodePx);
            sb.Append(",\n");
            sb.Append(pad); sb.Append("\"edge_blur_px\": "); AppendNullOrInt(sb, p.EdgeBlurPx);
            sb.Append("\n");
            sb.Append(new string(' ', indent * 2));
            sb.Append('}');
        }

        static void AppendNullOrInt(StringBuilder sb, int? v)
        {
            if (v == null)
                sb.Append("null");
            else
                sb.Append(v.Value.ToString(Inv));
        }

        static void AppendOutputConfig(StringBuilder sb, OutputConfigPayload p, int indent)
        {
            var pad = new string(' ', (indent + 1) * 2);
            sb.Append("{\n");
            sb.Append(pad); AppendBool(sb, "fg_enabled", p.FgEnabled, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendString(sb, "fg_format", p.FgFormat, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendBool(sb, "matte_enabled", p.MatteEnabled, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendString(sb, "matte_format", p.MatteFormat, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendBool(sb, "comp_enabled", p.CompEnabled, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendString(sb, "comp_format", p.CompFormat, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendBool(sb, "processed_enabled", p.ProcessedEnabled, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendString(sb, "processed_format", p.ProcessedFormat, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendString(sb, "exr_compression", p.ExrCompression, indent + 1);
            sb.Append("\n");
            sb.Append(new string(' ', indent * 2));
            sb.Append('}');
        }

        static void AppendGeometry(StringBuilder sb, SessionGeometryPayload g, int indent)
        {
            var pad = new string(' ', (indent + 1) * 2);
            sb.Append("{\n");
            sb.Append(pad); AppendInt(sb, "x", g.X, indent + 1, first: true);
            sb.Append(",\n");
            sb.Append(pad); AppendInt(sb, "y", g.Y, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendInt(sb, "width", g.Width, indent + 1);
            sb.Append(",\n");
            sb.Append(pad); AppendInt(sb, "height", g.Height, indent + 1);
            sb.Append("\n");
            sb.Append(new string(' ', indent * 2));
            sb.Append('}');
        }

        static void AppendClipLinear(StringBuilder sb, Dictionary<string, bool> d, int indent)
        {
            var pad = new string(' ', (indent + 1) * 2);
            sb.Append("  \"clip_input_is_linear\": {\n");
            var first = true;
            foreach (var kv in d)
            {
                if (!first)
                    sb.Append(",\n");
                first = false;
                sb.Append(pad);
                sb.Append('"');
                sb.Append(EscapeJsonString(kv.Key));
                sb.Append("\": ");
                sb.Append(kv.Value ? "true" : "false");
            }

            sb.Append("\n");
            sb.Append(new string(' ', indent * 2));
            sb.Append('}');
        }

        static void AppendIntArray(StringBuilder sb, string key, List<int> arr, int indent)
        {
            var pad = new string(' ', indent * 2);
            sb.Append(pad);
            sb.Append('"');
            sb.Append(key);
            sb.Append("\": [");
            for (var i = 0; i < arr.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(arr[i].ToString(Inv));
            }

            sb.Append(']');
        }

        static void AppendInt(StringBuilder sb, string key, int value, int indent, bool first = false)
        {
            var pad = new string(' ', indent * 2);
            sb.Append(pad);
            sb.Append('"');
            sb.Append(key);
            sb.Append("\": ");
            sb.Append(value.ToString(Inv));
        }

        static void AppendFloat(StringBuilder sb, string key, float value, int indent)
        {
            var pad = new string(' ', indent * 2);
            sb.Append(pad);
            sb.Append('"');
            sb.Append(key);
            sb.Append("\": ");
            sb.Append(value.ToString("R", Inv));
        }

        static void AppendBool(StringBuilder sb, string key, bool value, int indent)
        {
            var pad = new string(' ', indent * 2);
            sb.Append(pad);
            sb.Append('"');
            sb.Append(key);
            sb.Append("\": ");
            sb.Append(value ? "true" : "false");
        }

        static void AppendString(StringBuilder sb, string key, string value, int indent)
        {
            var pad = new string(' ', indent * 2);
            sb.Append(pad);
            sb.Append('"');
            sb.Append(key);
            sb.Append("\": \"");
            sb.Append(EscapeJsonString(value));
            sb.Append('"');
        }

        static string EscapeJsonString(string s) =>
            s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        #endregion

        #region JSON parse (subset — objects, arrays, strings, numbers, bool, null)

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        static Dictionary<string, object?>? ParseObject(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '{')
                return null;
            i++;
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            SkipWs(s, ref i);
            while (i < s.Length && s[i] != '}')
            {
                if (s[i] == ',')
                    i++;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"')
                    return null;
                var key = ReadJsonString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    return null;
                i++;
                SkipWs(s, ref i);
                var val = ParseValue(s, ref i);
                dict[key] = val;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (i < s.Length && s[i] == '}')
                    break;
            }

            if (i < s.Length && s[i] == '}')
                i++;
            return dict;
        }

        static List<object?> ParseArray(string s, ref int i)
        {
            var list = new List<object?>();
            if (i >= s.Length || s[i] != '[')
                return list;
            i++;
            SkipWs(s, ref i);
            while (i < s.Length && s[i] != ']')
            {
                list.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    SkipWs(s, ref i);
                }
            }

            if (i < s.Length && s[i] == ']')
                i++;
            return list;
        }

        static object? ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length)
                return null;
            var c = s[i];
            if (c == '{')
                return ParseObject(s, ref i);
            if (c == '[')
                return ParseArray(s, ref i);
            if (c == '"')
                return ReadJsonString(s, ref i);
            if (c == 't' && i + 3 < s.Length && s.Substring(i, 4) == "true")
            {
                i += 4;
                return true;
            }

            if (c == 'f' && i + 4 < s.Length && s.Substring(i, 5) == "false")
            {
                i += 5;
                return false;
            }

            if (c == 'n' && i + 3 < s.Length && s.Substring(i, 4) == "null")
            {
                i += 4;
                return null;
            }

            return ReadNumber(s, ref i);
        }

        static string ReadJsonString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
                return "";
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '"')
                {
                    i++;
                    return sb.ToString();
                }

                if (c == '\\' && i + 1 < s.Length)
                {
                    i++;
                    var e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u' when i + 4 <= s.Length:
                        {
                            var hex = s.Substring(i, 4);
                            if (int.TryParse(hex, NumberStyles.HexNumber, Inv, out var cp))
                            {
                                sb.Append((char)cp);
                                i += 4;
                            }

                            break;
                        }
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        static object ReadNumber(string s, ref int i)
        {
            var start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
                i++;
            while (i < s.Length && char.IsDigit(s[i]))
                i++;
            if (i < s.Length && s[i] == '.')
            {
                i++;
                while (i < s.Length && char.IsDigit(s[i]))
                    i++;
            }

            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '-' || s[i] == '+'))
                    i++;
                while (i < s.Length && char.IsDigit(s[i]))
                    i++;
            }

            var slice = s.Substring(start, i - start);
            if (double.TryParse(slice, NumberStyles.Float, Inv, out var d))
            {
                if (Math.Abs(d - Math.Round(d)) < 1e-9 && d >= int.MinValue && d <= int.MaxValue)
                    return (int)d;
                return d;
            }

            return 0;
        }

        #endregion
    }
}
