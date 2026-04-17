#nullable enable
using System.IO;
using UnityEngine;

namespace CorridorKey.Editor.UI
{
    /// <summary>Load PNG/JPG bytes into a readable <see cref="Texture2D"/> (Editor previews, bridge outputs).</summary>
    public static class TextureFileLoader
    {
        public static Texture2D? LoadReadableFromFile(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return null;

            var bytes = File.ReadAllBytes(absolutePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = Path.GetFileName(absolutePath)
            };

            return texture.LoadImage(bytes, markNonReadable: false) ? texture : null;
        }
    }
}
