#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Parameters rail: Auto vs Guided, Advanced fold, Guided Draw vs Import, step-2 MatAnyone2 vs VideoMaMa toggles
    /// (EZ chrome styling via <see cref="CorridorKeyWindowLayout.SetEzChromeToggleExclusive"/>).
    /// </summary>
    public sealed class ParametersRailController
    {
        /// <summary>Collapsed: chevron right (more to reveal). Expanded: chevron down (section open).</summary>
        const string AdvancedCollapsed = "Advanced \u25B6";

        const string AdvancedExpanded = "Advanced \u25BC";

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

        bool _advancedExpanded;

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
