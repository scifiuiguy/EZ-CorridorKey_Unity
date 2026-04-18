#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CorridorKey.Editor.UI.Annotations
{
    /// <summary>EZ <c>annotations.json</c> stroke (image pixels, y increasing downward like Qt / UITK).</summary>
    [Serializable]
    public sealed class AnnotationStrokeData
    {
        public List<float> px = new();
        public List<float> py = new();
        public string brush_type = "fg";
        public float radius = 15f;

        public static AnnotationStrokeData Create(string brushType, float radius)
        {
            return new AnnotationStrokeData { brush_type = brushType, radius = radius };
        }
    }
}
