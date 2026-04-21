#nullable enable
using System;
using CorridorKey.Editor.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Wires INFERENCE rail controls (EZ <c>parameter_panel.py</c> INFERENCE group).
    /// Mirrors EZ <c>params_changed</c>: any control change invokes <see cref="NotifyInferenceParamsChanged"/>.
    /// </summary>
    public sealed class InferenceSectionController
    {
        readonly Label _despillLabel;
        readonly Label _refinerLabel;
        readonly DropdownField _colorDropdown;
        readonly Slider _despillSlider;
        readonly Toggle _despeckleToggle;
        readonly IntegerField _despecklePx;
        readonly Slider _refinerSlider;
        readonly Toggle _livePreview;

        bool _suppress;

        public event Action<bool>? InputColorSpaceChanged;

        /// <summary>EZ <c>ParameterPanel.params_changed</c> — inference rail only (not output/performance).</summary>
        public event Action? InferenceParamsChanged;

        public bool InputIsLinear { get; private set; }

        public InferenceSectionController(VisualElement parametersRail)
        {
            var root = parametersRail.Q<VisualElement>("parameters-inference-section")
                       ?? throw new System.ArgumentException("Missing parameters-inference-section.", nameof(parametersRail));

            _colorDropdown = root.Q<DropdownField>("parameters-inference-color-space")
                             ?? throw new System.ArgumentException("Missing parameters-inference-color-space.", nameof(parametersRail));

            _despillSlider = root.Q<Slider>("parameters-inference-despill-slider")
                             ?? throw new System.ArgumentException("Missing parameters-inference-despill-slider.", nameof(parametersRail));
            _despillLabel = root.Q<Label>("parameters-inference-despill-label")
                            ?? throw new System.ArgumentException("Missing parameters-inference-despill-label.", nameof(parametersRail));

            _despeckleToggle = root.Q<Toggle>("parameters-inference-despeckle-toggle")
                               ?? throw new System.ArgumentException("Missing parameters-inference-despeckle-toggle.", nameof(parametersRail));
            _despecklePx = root.Q<IntegerField>("parameters-inference-despeckle-px")
                           ?? throw new System.ArgumentException("Missing parameters-inference-despeckle-px.", nameof(parametersRail));

            _refinerSlider = root.Q<Slider>("parameters-inference-refiner-slider")
                             ?? throw new System.ArgumentException("Missing parameters-inference-refiner-slider.", nameof(parametersRail));
            _refinerLabel = root.Q<Label>("parameters-inference-refiner-label")
                            ?? throw new System.ArgumentException("Missing parameters-inference-refiner-label.", nameof(parametersRail));

            _livePreview = root.Q<Toggle>("parameters-inference-live-preview")
                           ?? throw new System.ArgumentException("Missing parameters-inference-live-preview.", nameof(parametersRail));

            _colorDropdown.RegisterValueChangedCallback(OnDropdownChanged);
            _despillSlider.RegisterValueChangedCallback(OnDespillChanged);
            _despeckleToggle.RegisterValueChangedCallback(OnDespeckleToggled);
            _despecklePx.RegisterValueChangedCallback(OnDespecklePxChanged);
            _refinerSlider.RegisterValueChangedCallback(OnRefinerChanged);
            _livePreview.RegisterValueChangedCallback(OnLivePreviewToggled);

            InputIsLinear = IsLinearSelection(_colorDropdown.value);
            SyncDespillLabel();
            SyncRefinerLabel();
        }

        public InferenceParamsPayload CaptureInferenceParamsPayload()
        {
            return new InferenceParamsPayload
            {
                InputIsLinear = InputIsLinear,
                DespillStrength = _despillSlider.value / 10f,
                AutoDespeckle = _despeckleToggle.value,
                DespeckleSize = _despecklePx.value,
                DespeckleDilation = 25,
                DespeckleBlur = 5,
                RefinerScale = _refinerSlider.value / 10f,
                SourcePassthrough = true,
                EdgeErodePx = null,
                EdgeBlurPx = null,
            };
        }

        public void ApplyInferenceParamsPayload(InferenceParamsPayload p, bool applyLivePreview, bool livePreview)
        {
            _suppress = true;
            try
            {
                _colorDropdown.value = p.InputIsLinear ? "Linear" : "sRGB";
                InputIsLinear = p.InputIsLinear;
                _despillSlider.value = Mathf.Clamp(p.DespillStrength * 10f, 0f, 10f);
                _despeckleToggle.value = p.AutoDespeckle;
                _despecklePx.value = Mathf.Max(1, p.DespeckleSize);
                _refinerSlider.value = Mathf.Clamp(p.RefinerScale * 10f, 0f, 30f);
                if (applyLivePreview)
                    _livePreview.value = livePreview;
                SyncDespillLabel();
                SyncRefinerLabel();
            }
            finally
            {
                _suppress = false;
            }
        }

        public bool LivePreviewEnabled => _livePreview.value;

        public void SetLivePreviewWithoutNotify(bool value)
        {
            _suppress = true;
            try
            {
                _livePreview.value = value;
            }
            finally
            {
                _suppress = false;
            }
        }

        public void SetInputIsLinearWithoutNotify(bool linear)
        {
            _suppress = true;
            try
            {
                _colorDropdown.value = linear ? "Linear" : "sRGB";
                InputIsLinear = linear;
            }
            finally
            {
                _suppress = false;
            }
        }

        /// <summary>EZ <c>ParameterPanel.params_changed</c> equivalent — call after any inference control updates.</summary>
        void NotifyInferenceParamsChanged()
        {
            if (_suppress)
                return;
            InferenceParamsChanged?.Invoke();
        }

        void OnDropdownChanged(ChangeEvent<string> evt)
        {
            InputIsLinear = IsLinearSelection(evt.newValue);
            if (_suppress)
                return;
            InputColorSpaceChanged?.Invoke(InputIsLinear);
            NotifyInferenceParamsChanged();
        }

        static bool IsLinearSelection(string? value) =>
            string.Equals((value ?? string.Empty).Trim(), "Linear", StringComparison.OrdinalIgnoreCase);

        void OnDespillChanged(ChangeEvent<float> evt)
        {
            SyncDespillLabel();
            NotifyInferenceParamsChanged();
        }

        void OnDespeckleToggled(ChangeEvent<bool> evt)
        {
            NotifyInferenceParamsChanged();
        }

        void OnDespecklePxChanged(ChangeEvent<int> evt)
        {
            NotifyInferenceParamsChanged();
        }

        void OnRefinerChanged(ChangeEvent<float> evt)
        {
            SyncRefinerLabel();
            NotifyInferenceParamsChanged();
        }

        void OnLivePreviewToggled(ChangeEvent<bool> evt)
        {
            NotifyInferenceParamsChanged();
        }

        void SyncDespillLabel()
        {
            var v = _despillSlider.value;
            _despillLabel.text = $"Despill: {v / 10f:0.0}";
        }

        void SyncRefinerLabel()
        {
            var v = _refinerSlider.value;
            _refinerLabel.text = $"Refiner: {v / 10f:0.0}";
        }
    }
}
