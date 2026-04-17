#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Parameters rail: ALPHA / INFERENCE / OUTPUT / PERFORMANCE section folds, Auto vs Guided, Advanced fold,
    /// Guided Draw vs Import, step-2 MatAnyone2 vs VideoMaMa toggles
    /// (EZ chrome styling via <see cref="CorridorKeyWindowLayout.SetEzChromeToggleExclusive"/>).
    /// </summary>
    public sealed class ParametersRailController
    {
        /// <summary>Collapsed: chevron right (more to reveal). Expanded: chevron down (section open).</summary>
        const string AdvancedCollapsed = "Advanced \u25B6";

        const string AdvancedExpanded = "Advanced \u25BC";

        const string AlphaCollapsed = "ALPHA \u25B6";

        const string AlphaExpanded = "ALPHA \u25BC";

        const string InferenceCollapsed = "INFERENCE \u25B6";

        const string InferenceExpanded = "INFERENCE \u25BC";

        const string OutputCollapsed = "OUTPUT \u25B6";

        const string OutputExpanded = "OUTPUT \u25BC";

        const string PerformanceCollapsed = "PERFORMANCE \u25B6";

        const string PerformanceExpanded = "PERFORMANCE \u25BC";

        readonly Button _modeAutoBtn;
        readonly Button _modeGuidedBtn;
        readonly VisualElement _autoPanel;
        readonly VisualElement _guidedPanel;
        readonly Button _advancedToggle;
        readonly VisualElement _advancedBody;
        readonly Button _drawToggleBtn;
        readonly Button _importToggleBtn;
        readonly VisualElement _drawPanel;
        readonly VisualElement _importPanel;
        readonly Button _gvmAutoBtn;
        readonly Button _birefNetBtn;
        readonly DropdownField _birefNetDropdown;
        readonly Button _trackMaskBtn;
        readonly Button _mattingEngineMatBtn;
        readonly Button _mattingEngineVmBtn;
        readonly Button _importAlphaBtn;

        readonly Button _alphaSectionToggle;
        readonly VisualElement _alphaSectionBody;
        readonly Button _inferenceSectionToggle;
        readonly VisualElement _inferenceSectionBody;
        readonly Button _outputSectionToggle;
        readonly VisualElement _outputSectionBody;
        readonly Button _performanceSectionToggle;
        readonly VisualElement _performanceSectionBody;

        bool _advancedExpanded;
        bool _alphaExpanded;
        bool _inferenceExpanded;
        bool _outputExpanded;
        bool _performanceExpanded;

        public ParametersRailController(VisualElement root)
        {
            _modeAutoBtn = root.Q<Button>("parameters-toggle-auto")
                           ?? throw new System.InvalidOperationException("parameters-toggle-auto");
            _modeGuidedBtn = root.Q<Button>("parameters-toggle-guided")
                             ?? throw new System.InvalidOperationException("parameters-toggle-guided");
            _autoPanel = root.Q<VisualElement>("parameters-auto-panel")
                         ?? throw new System.InvalidOperationException("parameters-auto-panel");
            _guidedPanel = root.Q<VisualElement>("parameters-guided-panel")
                           ?? throw new System.InvalidOperationException("parameters-guided-panel");
            _advancedToggle = root.Q<Button>("parameters-auto-advanced-toggle")
                              ?? throw new System.InvalidOperationException("parameters-auto-advanced-toggle");
            _advancedBody = root.Q<VisualElement>("parameters-auto-advanced-body")
                             ?? throw new System.InvalidOperationException("parameters-auto-advanced-body");
            _drawToggleBtn = root.Q<Button>("parameters-toggle-draw")
                             ?? throw new System.InvalidOperationException("parameters-toggle-draw");
            _importToggleBtn = root.Q<Button>("parameters-toggle-import")
                               ?? throw new System.InvalidOperationException("parameters-toggle-import");
            _drawPanel = root.Q<VisualElement>("parameters-guided-draw")
                         ?? throw new System.InvalidOperationException("parameters-guided-draw");
            _importPanel = root.Q<VisualElement>("parameters-guided-import")
                           ?? throw new System.InvalidOperationException("parameters-guided-import");
            _gvmAutoBtn = root.Q<Button>("parameters-gvm-btn")
                          ?? throw new System.InvalidOperationException("parameters-gvm-btn");
            _birefNetBtn = root.Q<Button>("parameters-birefnet-btn")
                           ?? throw new System.InvalidOperationException("parameters-birefnet-btn");
            _birefNetDropdown = root.Q<DropdownField>("parameters-birefnet-model")
                                ?? throw new System.InvalidOperationException("parameters-birefnet-model");
            _trackMaskBtn = root.Q<Button>("parameters-track-mask-btn")
                            ?? throw new System.InvalidOperationException("parameters-track-mask-btn");
            _mattingEngineMatBtn = root.Q<Button>("parameters-toggle-matanyone")
                                   ?? throw new System.InvalidOperationException("parameters-toggle-matanyone");
            _mattingEngineVmBtn = root.Q<Button>("parameters-toggle-videomama")
                                  ?? throw new System.InvalidOperationException("parameters-toggle-videomama");
            _importAlphaBtn = root.Q<Button>("parameters-import-alpha-btn")
                              ?? throw new System.InvalidOperationException("parameters-import-alpha-btn");
            _alphaSectionToggle = root.Q<Button>("parameters-alpha-section-toggle")
                                  ?? throw new System.InvalidOperationException("parameters-alpha-section-toggle");
            _alphaSectionBody = root.Q<VisualElement>("parameters-alpha-body")
                                ?? throw new System.InvalidOperationException("parameters-alpha-body");
            _inferenceSectionToggle = root.Q<Button>("parameters-inference-section-toggle")
                                      ?? throw new System.InvalidOperationException("parameters-inference-section-toggle");
            _inferenceSectionBody = root.Q<VisualElement>("parameters-inference-body")
                                    ?? throw new System.InvalidOperationException("parameters-inference-body");
            _outputSectionToggle = root.Q<Button>("parameters-output-section-toggle")
                                   ?? throw new System.InvalidOperationException("parameters-output-section-toggle");
            _outputSectionBody = root.Q<VisualElement>("parameters-output-body")
                                 ?? throw new System.InvalidOperationException("parameters-output-body");
            _performanceSectionToggle = root.Q<Button>("parameters-performance-section-toggle")
                                        ?? throw new System.InvalidOperationException("parameters-performance-section-toggle");
            _performanceSectionBody = root.Q<VisualElement>("parameters-performance-body")
                                      ?? throw new System.InvalidOperationException("parameters-performance-body");

            _modeAutoBtn.clicked += OnModeAutoClicked;
            _modeGuidedBtn.clicked += OnModeGuidedClicked;
            _drawToggleBtn.clicked += OnDrawClicked;
            _importToggleBtn.clicked += OnImportClicked;
            _gvmAutoBtn.clicked += OnGvmAutoClicked;
            _birefNetBtn.clicked += OnBiRefNetClicked;
            _birefNetDropdown.RegisterValueChangedCallback(OnBiRefNetDropdownChanged);
            _trackMaskBtn.clicked += OnTrackMaskClicked;
            _mattingEngineMatBtn.clicked += OnMattingMatAnyoneClicked;
            _mattingEngineVmBtn.clicked += OnMattingVideoMaMaClicked;
            _importAlphaBtn.clicked += OnImportAlphaClicked;
            _advancedToggle.clicked += OnAdvancedClicked;
            _alphaSectionToggle.clicked += OnAlphaSectionClicked;
            _inferenceSectionToggle.clicked += OnInferenceSectionClicked;
            _outputSectionToggle.clicked += OnOutputSectionClicked;
            _performanceSectionToggle.clicked += OnPerformanceSectionClicked;

            ApplyTopMode();
            ApplyDrawImport();
        }

        void OnModeAutoClicked()
        {
            CorridorKeyWindowLayout.SetEzChromeToggleExclusive(_modeAutoBtn, _modeGuidedBtn);
            ApplyTopMode();
        }

        void OnModeGuidedClicked()
        {
            CorridorKeyWindowLayout.SetEzChromeToggleExclusive(_modeGuidedBtn, _modeAutoBtn);
            ApplyTopMode();
        }

        void OnDrawClicked()
        {
            CorridorKeyWindowLayout.SetEzChromeToggleExclusive(_drawToggleBtn, _importToggleBtn);
            ApplyDrawImport();
        }

        void OnImportClicked()
        {
            CorridorKeyWindowLayout.SetEzChromeToggleExclusive(_importToggleBtn, _drawToggleBtn);
            ApplyDrawImport();
        }

        void OnMattingMatAnyoneClicked()
        {
            CorridorKeyWindowLayout.SetEzChromeToggleExclusive(_mattingEngineMatBtn, _mattingEngineVmBtn);
            Debug.Log("[CorridorKey] MATANYONE2 selected.");
        }

        void OnMattingVideoMaMaClicked()
        {
            CorridorKeyWindowLayout.SetEzChromeToggleExclusive(_mattingEngineVmBtn, _mattingEngineMatBtn);
            Debug.Log("[CorridorKey] VIDEOMAMA selected.");
        }

        void OnGvmAutoClicked()
        {
            Debug.Log("[CorridorKey] GVM AUTO clicked.");
        }

        void OnBiRefNetClicked()
        {
            Debug.Log($"[CorridorKey] BIREFNET clicked. Model: {_birefNetDropdown.value}");
        }

        void OnBiRefNetDropdownChanged(ChangeEvent<string> evt)
        {
            Debug.Log($"[CorridorKey] OnBiRefNetDropdownChanged: {evt.newValue}");
        }

        void OnTrackMaskClicked()
        {
            Debug.Log("[CorridorKey] TRACK MASK clicked.");
        }

        void OnImportAlphaClicked()
        {
            Debug.Log("[CorridorKey] IMPORT ALPHA clicked.");
        }

        void OnAdvancedClicked()
        {
            _advancedExpanded = !_advancedExpanded;
            _advancedBody.style.display = _advancedExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _advancedToggle.text = _advancedExpanded ? AdvancedExpanded : AdvancedCollapsed;
        }

        void OnAlphaSectionClicked()
        {
            _alphaExpanded = !_alphaExpanded;
            _alphaSectionBody.style.display = _alphaExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _alphaSectionToggle.text = _alphaExpanded ? AlphaExpanded : AlphaCollapsed;
        }

        void OnInferenceSectionClicked()
        {
            _inferenceExpanded = !_inferenceExpanded;
            _inferenceSectionBody.style.display = _inferenceExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _inferenceSectionToggle.text = _inferenceExpanded ? InferenceExpanded : InferenceCollapsed;
        }

        void OnOutputSectionClicked()
        {
            _outputExpanded = !_outputExpanded;
            _outputSectionBody.style.display = _outputExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _outputSectionToggle.text = _outputExpanded ? OutputExpanded : OutputCollapsed;
        }

        void OnPerformanceSectionClicked()
        {
            _performanceExpanded = !_performanceExpanded;
            _performanceSectionBody.style.display = _performanceExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _performanceSectionToggle.text = _performanceExpanded ? PerformanceExpanded : PerformanceCollapsed;
        }

        bool IsGuidedMode() =>
            _modeGuidedBtn.ClassListContains(CorridorKeyWindowLayout.EzChromeButtonActiveClass);

        bool IsImportPath() =>
            _importToggleBtn.ClassListContains(CorridorKeyWindowLayout.EzChromeButtonActiveClass);

        void ApplyTopMode()
        {
            var guided = IsGuidedMode();
            _autoPanel.style.display = guided ? DisplayStyle.None : DisplayStyle.Flex;
            _guidedPanel.style.display = guided ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ApplyDrawImport()
        {
            var import = IsImportPath();
            _drawPanel.style.display = import ? DisplayStyle.None : DisplayStyle.Flex;
            _importPanel.style.display = import ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
