#nullable enable
using System.Collections.Generic;

namespace CorridorKey.Editor.Session
{
    /// <summary>
    /// Maps to EZ <c>.corridorkey_session.json</c> / <c>session_mixin._build_session_data</c> (snake_case in file).
    /// Serialized by <see cref="CorridorKeySessionJsonIo"/> (PascalCase in C#).
    /// </summary>
    public sealed class CorridorKeySessionPayload
    {
        public const int SupportedVersion = 1;

        public int Version { get; set; } = SupportedVersion;

        public InferenceParamsPayload Params { get; set; } = new();

        public OutputConfigPayload OutputConfig { get; set; } = new();

        public bool LivePreview { get; set; } = true;

        public SessionGeometryPayload? Geometry { get; set; }

        public List<int>? SplitterSizes { get; set; }

        public List<int>? VsplitterSizes { get; set; }

        public string? WorkspacePath { get; set; }

        public string? SelectedClip { get; set; }

        public Dictionary<string, bool>? ClipInputIsLinear { get; set; }
    }

    public sealed class SessionGeometryPayload
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }

    /// <summary>EZ <c>InferenceParams</c> / <c>backend.service.core</c> field names.</summary>
    public sealed class InferenceParamsPayload
    {
        public bool InputIsLinear { get; set; }

        public float DespillStrength { get; set; } = 0.5f;

        public bool AutoDespeckle { get; set; } = true;

        public int DespeckleSize { get; set; } = 400;

        public int DespeckleDilation { get; set; } = 25;

        public int DespeckleBlur { get; set; } = 5;

        public float RefinerScale { get; set; } = 1f;

        public bool SourcePassthrough { get; set; } = true;

        public int? EdgeErodePx { get; set; }

        public int? EdgeBlurPx { get; set; }
    }

    /// <summary>EZ <c>OutputConfig</c>.</summary>
    public sealed class OutputConfigPayload
    {
        public bool FgEnabled { get; set; } = true;

        public string FgFormat { get; set; } = "exr";

        public bool MatteEnabled { get; set; } = true;

        public string MatteFormat { get; set; } = "exr";

        public bool CompEnabled { get; set; } = true;

        public string CompFormat { get; set; } = "png";

        public bool ProcessedEnabled { get; set; } = true;

        public string ProcessedFormat { get; set; } = "exr";

        public string ExrCompression { get; set; } = "dwab";
    }
}
