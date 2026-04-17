using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Wires dual-viewer top chrome: A/B wipe toggle (EZ <c>PreviewViewport.add_ab_button</c>) and
    /// exclusive view mode buttons (EZ <c>ViewModeBar</c>). Styling uses USS
    /// <c>.corridor-key-ez-chrome-btn</c> / <c>--active</c>.
    /// </summary>
    public sealed class DualViewerChromeController
    {
        public event System.Action<bool>? AbToggled;
        public event System.Action<bool>? AbRendererModeToggled;

        readonly Button _abButton;
        readonly Button _abRendererButton;
        readonly Button[] _modeButtons;
        readonly string[] _modeIds;

        bool _abWipeOn;
        bool _gpuPreviewOn = true;
        int _selectedModeIndex = -1;

        public DualViewerChromeController(VisualElement root)
        {
            _abButton = root.Q<Button>("viewer-ab-button");
            _abRendererButton = root.Q<Button>("viewer-ab-renderer-button");
            var modeBar = root.Q<VisualElement>("viewer-view-mode-bar");
            var specs = CorridorKeyWindowLayout.ViewModeChromeSpecs;
            if (modeBar == null)
            {
                _modeButtons = System.Array.Empty<Button>();
                _modeIds = System.Array.Empty<string>();
            }
            else
            {
                _modeButtons = new Button[specs.Length];
                _modeIds = new string[specs.Length];
                for (var i = 0; i < specs.Length; i++)
                {
                    _modeIds[i] = specs[i].Id;
                    _modeButtons[i] = modeBar.Q<Button>($"viewer-view-mode-{specs[i].Id}");
                }
            }

            if (_abButton != null)
                _abButton.RegisterCallback<ClickEvent>(OnAbClicked);
            if (_abRendererButton != null)
                _abRendererButton.RegisterCallback<ClickEvent>(OnAbRendererClicked);

            for (var i = 0; i < _modeButtons.Length; i++)
            {
                var index = i;
                var btn = _modeButtons[i];
                if (btn == null)
                    continue;
                btn.RegisterCallback<ClickEvent>(_ => SelectViewMode(index));
            }

            for (var i = 0; i < _modeButtons.Length; i++)
            {
                if (_modeButtons[i] != null &&
                    _modeButtons[i].ClassListContains(CorridorKeyWindowLayout.EzChromeButtonActiveClass))
                {
                    _selectedModeIndex = i;
                    break;
                }
            }

            SetAbRendererVisual(_gpuPreviewOn);
        }

        /// <summary>Selects a view mode by <see cref="CorridorKeyWindowLayout.ViewModeChromeSpecs"/> id (e.g. <c>alpha</c>, <c>comp</c>).</summary>
        public void SelectViewModeById(string modeId)
        {
            if (string.IsNullOrEmpty(modeId))
                return;
            for (var i = 0; i < _modeIds.Length; i++)
            {
                if (!string.Equals(_modeIds[i], modeId, System.StringComparison.Ordinal))
                    continue;
                if (_modeButtons[i] == null)
                    return;
                SelectViewMode(i);
                return;
            }
        }

        void OnAbClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            _abWipeOn = !_abWipeOn;
            SetAbVisual(_abWipeOn);
            AbToggled?.Invoke(_abWipeOn);
            Debug.Log(_abWipeOn
                ? "[CorridorKey] A/B wipe: ON"
                : "[CorridorKey] A/B wipe: OFF");
        }

        void OnAbRendererClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            _gpuPreviewOn = !_gpuPreviewOn;
            SetAbRendererVisual(_gpuPreviewOn);
            AbRendererModeToggled?.Invoke(_gpuPreviewOn);
            Debug.Log(_gpuPreviewOn
                ? "[CorridorKey] A/B preview renderer: GPU"
                : "[CorridorKey] A/B preview renderer: CPU");
        }

        void SelectViewMode(int index)
        {
            if (index < 0 || index >= _modeButtons.Length || _modeButtons[index] == null)
                return;

            if (_selectedModeIndex == index)
                return;

            if (_selectedModeIndex >= 0 && _selectedModeIndex < _modeButtons.Length && _modeButtons[_selectedModeIndex] != null)
                SetModeActive(_selectedModeIndex, false);

            _selectedModeIndex = index;
            SetModeActive(index, true);
            Debug.Log($"[CorridorKey] View mode: {_modeIds[index]}");
        }

        void SetAbVisual(bool on)
        {
            if (_abButton == null)
                return;
            if (on)
                _abButton.AddToClassList(CorridorKeyWindowLayout.EzChromeButtonActiveClass);
            else
                _abButton.RemoveFromClassList(CorridorKeyWindowLayout.EzChromeButtonActiveClass);
        }

        void SetAbRendererVisual(bool gpuOn)
        {
            if (_abRendererButton == null)
                return;

            _abRendererButton.text = gpuOn ? "GPU" : "CPU";
            if (gpuOn)
                _abRendererButton.AddToClassList(CorridorKeyWindowLayout.EzChromeButtonActiveClass);
            else
                _abRendererButton.RemoveFromClassList(CorridorKeyWindowLayout.EzChromeButtonActiveClass);
        }

        void SetModeActive(int index, bool active)
        {
            var btn = _modeButtons[index];
            if (btn == null)
                return;
            if (active)
                btn.AddToClassList(CorridorKeyWindowLayout.EzChromeButtonActiveClass);
            else
                btn.RemoveFromClassList(CorridorKeyWindowLayout.EzChromeButtonActiveClass);
        }
    }
}
