using System.Collections.Generic;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Display names for BiRefNet variants — EZ parity: <c>modules/BiRefNetModule/wrapper.py</c> <c>BIREFNET_MODELS</c> keys (order preserved).
    /// </summary>
    public static class BiRefNetModelOptions
    {
        public const string DefaultDisplayName = "Matting";

        /// <summary>Keys of <c>BIREFNET_MODELS</c> in EZ (same order as the Python dict).</summary>
        public static readonly string[] DisplayNames =
        {
            "Matting",
            "Matting HR",
            "Matting Lite",
            "Matting Dynamic",
            "General",
            "General HR",
            "General Lite",
            "General Lite 2K",
            "General Dynamic",
            "General 512",
            "Portrait",
            "DIS",
            "HRSOD",
            "COD",
            "DIS TR_TEs",
            "General Legacy",
        };

        public static List<string> CreateChoicesList() => new List<string>(DisplayNames);
    }
}
