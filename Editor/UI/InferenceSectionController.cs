#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Wires INFERENCE rail controls (EZ <c>parameter_panel.py</c> INFERENCE group). Placeholder handlers log to the Console for verification.
    /// </summary>
    public sealed class InferenceSectionController
    {
        readonly Label _despillLabel;
        readonly Label _refinerLabel;

        public InferenceSectionController(VisualElement parametersRail)
        {
            var root = parametersRail.Q<VisualElement>("parameters-inference-section")
                       ?? throw new System.ArgumentException("Missing parameters-inference-section.", nameof(parametersRail));

            var colorDropdown = root.Q<DropdownField>("parameters-inference-color-space")
                                ?? throw new System.ArgumentException("Missing parameters-inference-color-space.", nameof(parametersRail));

            var despillSlider = root.Q<Slider>("parameters-inference-despill-slider")
                                ?? throw new System.ArgumentException("Missing parameters-inference-despill-slider.", nameof(parametersRail));
            _despillLabel = root.Q<Label>("parameters-inference-despill-label")
                          ?? throw new System.ArgumentException("Missing parameters-inference-despill-label.", nameof(parametersRail));

            var despeckleToggle = root.Q<Toggle>("parameters-inference-despeckle-toggle")
                                  ?? throw new System.ArgumentException("Missing parameters-inference-despeckle-toggle.", nameof(parametersRail));
            var despecklePx = root.Q<IntegerField>("parameters-inference-despeckle-px")
                              ?? throw new System.ArgumentException("Missing parameters-inference-despeckle-px.", nameof(parametersRail));

            var refinerSlider = root.Q<Slider>("parameters-inference-refiner-slider")
                                ?? throw new System.ArgumentException("Missing parameters-inference-refiner-slider.", nameof(parametersRail));
            _refinerLabel = root.Q<Label>("parameters-inference-refiner-label")
                          ?? throw new System.ArgumentException("Missing parameters-inference-refiner-label.", nameof(parametersRail));

            var livePreview = root.Q<Toggle>("parameters-inference-live-preview")
                              ?? throw new System.ArgumentException("Missing parameters-inference-live-preview.", nameof(parametersRail));

            colorDropdown.RegisterValueChangedCallback(OnDropdownChanged);
            despillSlider.RegisterValueChangedCallback(OnDespillChanged);
            despeckleToggle.RegisterValueChangedCallback(OnDespeckleToggled);
            despecklePx.RegisterValueChangedCallback(OnDespecklePxChanged);
            refinerSlider.RegisterValueChangedCallback(OnRefinerChanged);
            livePreview.RegisterValueChangedCallback(OnLivePreviewToggled);
        }

        void OnDropdownChanged(ChangeEvent<string> evt)
        {
            Debug.Log($"[CorridorKey] Inference color space: {evt.previousValue} → {evt.newValue}");
        }

        void OnDespillChanged(ChangeEvent<float> evt)
        {
            var v = evt.newValue;
            _despillLabel.text = $"Despill: {v / 10f:0.0}";
            Debug.Log($"[CorridorKey] Inference despill slider: {evt.previousValue:0.###} → {v:0.###} (strength {v / 10f:0.00})");
        }

        void OnDespeckleToggled(ChangeEvent<bool> evt)
        {
            Debug.Log($"[CorridorKey] Inference despeckle enabled: {evt.previousValue} → {evt.newValue}");
        }

        void OnDespecklePxChanged(ChangeEvent<int> evt)
        {
            Debug.Log($"[CorridorKey] Inference despeckle size (px): {evt.previousValue} → {evt.newValue}");
        }

        void OnRefinerChanged(ChangeEvent<float> evt)
        {
            var v = evt.newValue;
            _refinerLabel.text = $"Refiner: {v / 10f:0.0}";
            Debug.Log($"[CorridorKey] Inference refiner slider: {evt.previousValue:0.###} → {v:0.###} (scale {v / 10f:0.00})");
        }

        void OnLivePreviewToggled(ChangeEvent<bool> evt)
        {
            Debug.Log($"[CorridorKey] Inference live preview: {evt.previousValue} → {evt.newValue}");
        }
    }
}
