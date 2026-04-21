#nullable enable
using System;
using CorridorKey.Editor.Session;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Wires OUTPUT and PERFORMANCE rail controls (EZ <c>parameter_panel.py</c>). Parallel frames use <see cref="EditorPrefs"/> (EZ <c>QSettings</c>).
    /// </summary>
    public sealed class OutputPerformanceSectionController
    {
        readonly Toggle _fgToggle;
        readonly DropdownField _fgFormat;
        readonly Toggle _matteToggle;
        readonly DropdownField _matteFormat;
        readonly Toggle _compToggle;
        readonly DropdownField _compFormat;
        readonly Toggle _procToggle;
        readonly DropdownField _procFormat;
        readonly IntegerField _parallelFrames;

        bool _suppress;

        public event Action? SessionStateChanged;

        public OutputPerformanceSectionController(VisualElement parametersRail)
        {
            var outputRoot = parametersRail.Q<VisualElement>("parameters-output-section")
                             ?? throw new System.ArgumentException("Missing parameters-output-section.", nameof(parametersRail));

            _fgToggle = outputRoot.Q<Toggle>("parameters-output-fg-toggle")
                        ?? throw new System.ArgumentException("Missing parameters-output-fg-toggle.", nameof(parametersRail));
            _fgFormat = outputRoot.Q<DropdownField>("parameters-output-fg-format")
                        ?? throw new System.ArgumentException("Missing parameters-output-fg-format.", nameof(parametersRail));
            _matteToggle = outputRoot.Q<Toggle>("parameters-output-matte-toggle")
                           ?? throw new System.ArgumentException("Missing parameters-output-matte-toggle.", nameof(parametersRail));
            _matteFormat = outputRoot.Q<DropdownField>("parameters-output-matte-format")
                           ?? throw new System.ArgumentException("Missing parameters-output-matte-format.", nameof(parametersRail));
            _compToggle = outputRoot.Q<Toggle>("parameters-output-comp-toggle")
                          ?? throw new System.ArgumentException("Missing parameters-output-comp-toggle.", nameof(parametersRail));
            _compFormat = outputRoot.Q<DropdownField>("parameters-output-comp-format")
                          ?? throw new System.ArgumentException("Missing parameters-output-comp-format.", nameof(parametersRail));
            _procToggle = outputRoot.Q<Toggle>("parameters-output-processed-toggle")
                          ?? throw new System.ArgumentException("Missing parameters-output-processed-toggle.", nameof(parametersRail));
            _procFormat = outputRoot.Q<DropdownField>("parameters-output-processed-format")
                          ?? throw new System.ArgumentException("Missing parameters-output-processed-format.", nameof(parametersRail));

            _fgToggle.RegisterValueChangedCallback(_ => OnOutputChanged());
            _fgFormat.RegisterValueChangedCallback(_ => OnOutputChanged());
            _matteToggle.RegisterValueChangedCallback(_ => OnOutputChanged());
            _matteFormat.RegisterValueChangedCallback(_ => OnOutputChanged());
            _compToggle.RegisterValueChangedCallback(_ => OnOutputChanged());
            _compFormat.RegisterValueChangedCallback(_ => OnOutputChanged());
            _procToggle.RegisterValueChangedCallback(_ => OnOutputChanged());
            _procFormat.RegisterValueChangedCallback(_ => OnOutputChanged());

            var perfRoot = parametersRail.Q<VisualElement>("parameters-performance-section")
                           ?? throw new System.ArgumentException("Missing parameters-performance-section.", nameof(parametersRail));
            _parallelFrames = perfRoot.Q<IntegerField>("parameters-performance-parallel-frames")
                              ?? throw new System.ArgumentException("Missing parameters-performance-parallel-frames.", nameof(parametersRail));
            _parallelFrames.value = EditorPrefs.GetInt(EditorPrefsKeys.ParallelFrames, 1);
            _parallelFrames.RegisterValueChangedCallback(OnParallelFramesChanged);
        }

        public OutputConfigPayload CaptureOutputConfigPayload()
        {
            return new OutputConfigPayload
            {
                FgEnabled = _fgToggle.value,
                FgFormat = _fgFormat.value ?? "exr",
                MatteEnabled = _matteToggle.value,
                MatteFormat = _matteFormat.value ?? "exr",
                CompEnabled = _compToggle.value,
                CompFormat = _compFormat.value ?? "png",
                ProcessedEnabled = _procToggle.value,
                ProcessedFormat = _procFormat.value ?? "exr",
                ExrCompression = "dwab",
            };
        }

        public void ApplyOutputConfigPayload(OutputConfigPayload p)
        {
            _suppress = true;
            try
            {
                _fgToggle.value = p.FgEnabled;
                SetDropdownFormat(_fgFormat, p.FgFormat);
                _matteToggle.value = p.MatteEnabled;
                SetDropdownFormat(_matteFormat, p.MatteFormat);
                _compToggle.value = p.CompEnabled;
                SetDropdownFormat(_compFormat, p.CompFormat);
                _procToggle.value = p.ProcessedEnabled;
                SetDropdownFormat(_procFormat, p.ProcessedFormat);
            }
            finally
            {
                _suppress = false;
            }
        }

        static void SetDropdownFormat(DropdownField field, string format)
        {
            if (string.IsNullOrEmpty(format))
                return;
            var choices = field.choices;
            if (choices == null)
                return;
            for (var i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i], format, System.StringComparison.OrdinalIgnoreCase))
                {
                    field.index = i;
                    return;
                }
            }
        }

        void OnOutputChanged()
        {
            if (_suppress)
                return;
            SessionStateChanged?.Invoke();
        }

        void OnParallelFramesChanged(ChangeEvent<int> evt)
        {
            var clamped = Mathf.Clamp(evt.newValue, 1, 64);
            if (clamped != evt.newValue)
                _parallelFrames.value = clamped;
            EditorPrefs.SetInt(EditorPrefsKeys.ParallelFrames, clamped);
            if (!_suppress)
                SessionStateChanged?.Invoke();
        }
    }
}
