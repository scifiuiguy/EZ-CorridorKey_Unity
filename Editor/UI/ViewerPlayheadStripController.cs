#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// EZ <c>ui/widgets/dual_viewer.py</c> shared bottom strip: <c>FrameScrubber</c> + zoom label.
    /// Stem index is 0-based; frame label shows 1-based current / total (matches EZ <c>_update_label</c>).
    /// </summary>
    public sealed class ViewerPlayheadStripController
    {
        const float PlaybackIntervalMs = 333f;
        const string PlayIcon = "\u25B6";
        const string PauseIcon = "\u275A\u275A";

        readonly VisualElement _strip;
        readonly Label _frameLabel;
        readonly Label? _chromeFrameLabel;
        readonly Slider _slider;
        readonly Button _startBtn;
        readonly Button _prevBtn;
        readonly Button _playBtn;
        readonly Button _nextBtn;
        readonly Button _endBtn;

        int _totalFrames;
        bool _playing;
        bool _suppressSlider;
        IVisualElementScheduledItem? _playbackSchedule;

        public event Action<int>? FrameChanged;

        public ViewerPlayheadStripController(VisualElement root)
        {
            _strip = root.Q<VisualElement>("viewer-playhead-strip")
                     ?? throw new InvalidOperationException("Missing viewer-playhead-strip.");
            _frameLabel = root.Q<Label>("viewer-playhead-frame-label")
                          ?? throw new InvalidOperationException("Missing viewer-playhead-frame-label.");
            _chromeFrameLabel = root.Q<Label>("viewer-shared-frame-label");
            _slider = root.Q<Slider>("viewer-playhead-slider")
                      ?? throw new InvalidOperationException("Missing viewer-playhead-slider.");
            _startBtn = root.Q<Button>("viewer-playhead-btn-start")
                        ?? throw new InvalidOperationException("Missing viewer-playhead-btn-start.");
            _prevBtn = root.Q<Button>("viewer-playhead-btn-prev")
                        ?? throw new InvalidOperationException("Missing viewer-playhead-btn-prev.");
            _playBtn = root.Q<Button>("viewer-playhead-btn-play")
                        ?? throw new InvalidOperationException("Missing viewer-playhead-btn-play.");
            _nextBtn = root.Q<Button>("viewer-playhead-btn-next")
                        ?? throw new InvalidOperationException("Missing viewer-playhead-btn-next.");
            _endBtn = root.Q<Button>("viewer-playhead-btn-end")
                        ?? throw new InvalidOperationException("Missing viewer-playhead-btn-end.");

            _slider.RegisterValueChangedCallback(OnSliderChanged);
            _startBtn.clicked += GoStart;
            _prevBtn.clicked += StepBack;
            _playBtn.clicked += TogglePlayback;
            _nextBtn.clicked += StepForward;
            _endBtn.clicked += GoEnd;

            SetFrameCount(0);
        }

        /// <summary>Number of frames (stems) in the clip; 0 means no media / disabled scrubber.</summary>
        public void SetFrameCount(int totalFrames)
        {
            _totalFrames = Mathf.Max(0, totalFrames);
            StopPlayback();

            if (_totalFrames <= 0)
            {
                _suppressSlider = true;
                _slider.lowValue = 0f;
                _slider.highValue = 0f;
                _slider.value = 0f;
                _suppressSlider = false;
                _slider.SetEnabled(false);
                SetTransportEnabled(false);
            }
            else
            {
                _suppressSlider = true;
                _slider.lowValue = 0f;
                _slider.highValue = _totalFrames - 1;
                _slider.value = Mathf.Clamp(Mathf.RoundToInt(_slider.value), 0, _totalFrames - 1);
                _suppressSlider = false;
                _slider.SetEnabled(true);
                SetTransportEnabled(true);
            }

            RefreshLabels();
        }

        public int CurrentStemIndex => Mathf.Clamp(Mathf.RoundToInt(_slider.value), 0, Mathf.Max(0, _totalFrames - 1));

        /// <summary>Programmatic scrub (e.g. backend); does not change <see cref="SetFrameCount"/>.</summary>
        public void SetStemIndex(int stemIndex, bool notify = true)
        {
            if (_totalFrames <= 0)
                return;
            var v = Mathf.Clamp(stemIndex, 0, _totalFrames - 1);
            _suppressSlider = true;
            _slider.value = v;
            _suppressSlider = false;
            RefreshLabels();
            if (notify)
                FrameChanged?.Invoke(v);
        }

        void OnSliderChanged(ChangeEvent<float> evt)
        {
            if (_suppressSlider)
                return;
            RefreshLabels();
            FrameChanged?.Invoke(CurrentStemIndex);
        }

        void RefreshLabels()
        {
            if (_totalFrames <= 0)
            {
                _frameLabel.text = "0 / 0";
                if (_chromeFrameLabel != null)
                    _chromeFrameLabel.text = "Frame — / —";
                return;
            }

            var current = Mathf.RoundToInt(_slider.value) + 1;
            _frameLabel.text = $"{current} / {_totalFrames}";
            if (_chromeFrameLabel != null)
                _chromeFrameLabel.text = $"Frame {current} / {_totalFrames}";
        }

        void SetTransportEnabled(bool enabled)
        {
            _startBtn.SetEnabled(enabled);
            _prevBtn.SetEnabled(enabled);
            _playBtn.SetEnabled(enabled);
            _nextBtn.SetEnabled(enabled);
            _endBtn.SetEnabled(enabled);
        }

        void GoStart()
        {
            SetStemIndex(0);
        }

        void StepBack()
        {
            SetStemIndex(CurrentStemIndex - 1);
        }

        void StepForward()
        {
            SetStemIndex(CurrentStemIndex + 1);
        }

        void GoEnd()
        {
            if (_totalFrames <= 0)
                return;
            SetStemIndex(_totalFrames - 1);
        }

        void TogglePlayback()
        {
            if (_playing)
                StopPlayback();
            else
                StartPlayback();
        }

        void StartPlayback()
        {
            if (_totalFrames <= 1)
                return;
            _playing = true;
            _playBtn.text = PauseIcon;
            _playBtn.tooltip = "Pause (Space)";
            _playbackSchedule?.Pause();
            _playbackSchedule = _strip.schedule.Execute(PlaybackTick).Every((long)PlaybackIntervalMs);
        }

        void StopPlayback()
        {
            if (!_playing && _playbackSchedule == null)
            {
                _playBtn.text = PlayIcon;
                _playBtn.tooltip = "Play / Pause (Space)";
                return;
            }

            _playing = false;
            _playbackSchedule?.Pause();
            _playbackSchedule = null;
            _playBtn.text = PlayIcon;
            _playBtn.tooltip = "Play / Pause (Space)";
        }

        void PlaybackTick()
        {
            if (!_playing || _totalFrames <= 1)
                return;
            var v = CurrentStemIndex;
            if (v >= _totalFrames - 1)
            {
                StopPlayback();
                return;
            }

            SetStemIndex(v + 1);
        }
    }
}
