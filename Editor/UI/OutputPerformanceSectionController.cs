#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Wires OUTPUT and PERFORMANCE rail controls (EZ <c>parameter_panel.py</c>). Placeholder handlers log to the Console.
    /// </summary>
    public sealed class OutputPerformanceSectionController
    {
        readonly IntegerField _parallelFrames;

        public OutputPerformanceSectionController(VisualElement parametersRail)
        {
            var outputRoot = parametersRail.Q<VisualElement>("parameters-output-section")
                             ?? throw new System.ArgumentException("Missing parameters-output-section.", nameof(parametersRail));

            var fgToggle = outputRoot.Q<Toggle>("parameters-output-fg-toggle")
                           ?? throw new System.ArgumentException("Missing parameters-output-fg-toggle.", nameof(parametersRail));
            var fgFormat = outputRoot.Q<DropdownField>("parameters-output-fg-format")
                           ?? throw new System.ArgumentException("Missing parameters-output-fg-format.", nameof(parametersRail));
            var matteToggle = outputRoot.Q<Toggle>("parameters-output-matte-toggle")
                              ?? throw new System.ArgumentException("Missing parameters-output-matte-toggle.", nameof(parametersRail));
            var matteFormat = outputRoot.Q<DropdownField>("parameters-output-matte-format")
                              ?? throw new System.ArgumentException("Missing parameters-output-matte-format.", nameof(parametersRail));
            var compToggle = outputRoot.Q<Toggle>("parameters-output-comp-toggle")
                             ?? throw new System.ArgumentException("Missing parameters-output-comp-toggle.", nameof(parametersRail));
            var compFormat = outputRoot.Q<DropdownField>("parameters-output-comp-format")
                             ?? throw new System.ArgumentException("Missing parameters-output-comp-format.", nameof(parametersRail));
            var procToggle = outputRoot.Q<Toggle>("parameters-output-processed-toggle")
                             ?? throw new System.ArgumentException("Missing parameters-output-processed-toggle.", nameof(parametersRail));
            var procFormat = outputRoot.Q<DropdownField>("parameters-output-processed-format")
                             ?? throw new System.ArgumentException("Missing parameters-output-processed-format.", nameof(parametersRail));

            fgToggle.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output FG enabled: {evt.previousValue} → {evt.newValue}"));
            fgFormat.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output FG format: {evt.previousValue} → {evt.newValue}"));
            matteToggle.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output Matte enabled: {evt.previousValue} → {evt.newValue}"));
            matteFormat.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output Matte format: {evt.previousValue} → {evt.newValue}"));
            compToggle.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output Comp enabled: {evt.previousValue} → {evt.newValue}"));
            compFormat.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output Comp format: {evt.previousValue} → {evt.newValue}"));
            procToggle.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output Processed enabled: {evt.previousValue} → {evt.newValue}"));
            procFormat.RegisterValueChangedCallback(evt =>
                Debug.Log($"[CorridorKey] Output Processed format: {evt.previousValue} → {evt.newValue}"));

            var perfRoot = parametersRail.Q<VisualElement>("parameters-performance-section")
                           ?? throw new System.ArgumentException("Missing parameters-performance-section.", nameof(parametersRail));
            _parallelFrames = perfRoot.Q<IntegerField>("parameters-performance-parallel-frames")
                              ?? throw new System.ArgumentException("Missing parameters-performance-parallel-frames.", nameof(parametersRail));
            _parallelFrames.RegisterValueChangedCallback(OnParallelFramesChanged);
        }

        void OnParallelFramesChanged(ChangeEvent<int> evt)
        {
            var clamped = Mathf.Clamp(evt.newValue, 1, 64);
            if (clamped != evt.newValue)
                _parallelFrames.value = clamped;
            Debug.Log($"[CorridorKey] Parallel frames: {evt.previousValue} → {clamped}");
        }
    }
}
