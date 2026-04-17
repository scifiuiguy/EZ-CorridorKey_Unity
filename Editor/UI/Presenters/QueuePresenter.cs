#nullable enable
using System;
using System.Collections.Generic;
using CorridorKey.Backend;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor.ViewModels;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI.Presenters
{
    /// <summary>
    /// EZ <c>queue_panel.py</c> parity: presenter for the queue — scroll list, bridge <c>progress</c> / <c>done</c> for
    /// registered jobs (<see cref="QueueJobVm.JobId"/> equals bridge <c>request_id</c>).
    /// </summary>
    public sealed class QueuePresenter : IDisposable
    {
        readonly IBackendClient _backend;
        readonly ScrollView? _scroll;
        readonly Dictionary<string, VisualElement> _cardByJobId = new();
        readonly Dictionary<string, QueueJobVm> _jobsByRequestId = new();

        public QueuePresenter(VisualElement root, IBackendClient backend)
        {
            _backend = backend;
            Root = root;
            _scroll = root.Q<ScrollView>("queue-scroll");
            RemoveBindPlaceholder();
            _backend.ProgressReceived += OnBackendProgress;
            _backend.BridgeCommandDoneReceived += OnBridgeCommandDone;
        }

        public VisualElement Root { get; }

        /// <summary>Fired after a new card is added (e.g. expand the QUEUE drawer).</summary>
        public event Action<QueueJobVm>? OnNewQueueCardCreated;

        public void Dispose()
        {
            _backend.ProgressReceived -= OnBackendProgress;
            _backend.BridgeCommandDoneReceived -= OnBridgeCommandDone;
        }

        void RemoveBindPlaceholder()
        {
            var placeholder = _scroll?.Q<Label>("queue-placeholder");
            placeholder?.RemoveFromHierarchy();
        }

        /// <summary>Appends a card and tracks <paramref name="vm"/> for bridge lines carrying matching <c>request_id</c>.</summary>
        public bool Enqueue(QueueJobVm vm)
        {
            if (_scroll == null)
                return false;

            var id = vm.JobId;
            _jobsByRequestId[id] = vm;

            var card = QueueJobCardFactory.Create(vm, el =>
            {
                _cardByJobId.Remove(id);
                _jobsByRequestId.Remove(id);
                el.RemoveFromHierarchy();
            });
            _cardByJobId[id] = card;
            _scroll.Add(card);
            _scroll.ScrollTo(card);

            OnNewQueueCardCreated?.Invoke(vm);
            return true;
        }

        /// <summary>Pre-bridge failures (validation, send) after <see cref="Enqueue"/>.</summary>
        public void FailJob(QueueJobVm vm, string detail)
        {
            if (!_jobsByRequestId.ContainsKey(vm.JobId))
                return;
            vm.Status = QueueJobStatus.Failed;
            vm.Detail = detail;
            Refresh(vm);
        }

        /// <summary>Re-bind a card after mutating <paramref name="vm"/> in place (same <see cref="QueueJobVm.JobId"/>).</summary>
        public void Refresh(QueueJobVm vm)
        {
            if (!_cardByJobId.TryGetValue(vm.JobId, out var card))
                return;
            QueueJobCardFactory.Apply(card, vm);
        }

        public void Clear()
        {
            _jobsByRequestId.Clear();
            _cardByJobId.Clear();
            _scroll?.Clear();
        }

        void OnBackendProgress(ProgressPayload p)
        {
            if (string.IsNullOrEmpty(p.RequestId))
                return;
            if (!_jobsByRequestId.TryGetValue(p.RequestId, out var vm))
                return;

            vm.CurrentFrame = Math.Max(0, p.Current);
            vm.TotalFrames = Math.Max(0, p.Total);
            vm.Status = QueueJobStatus.Running;
            if (!string.IsNullOrEmpty(p.Detail))
                vm.Detail = p.Detail;
            else if (!string.IsNullOrEmpty(p.Phase))
                vm.Detail = p.Phase;
            Refresh(vm);
        }

        void OnBridgeCommandDone(BridgeCommandDonePayload p)
        {
            if (string.IsNullOrEmpty(p.RequestId))
                return;
            if (!_jobsByRequestId.TryGetValue(p.RequestId, out var vm))
                return;

            if (!p.Ok)
            {
                vm.Status = QueueJobStatus.Failed;
                vm.Detail = string.IsNullOrEmpty(p.Summary) ? "Failed" : p.Summary;
            }
            else
            {
                vm.Status = QueueJobStatus.Succeeded;
                vm.Detail = null;
                if (vm.TotalFrames > 0)
                    vm.CurrentFrame = vm.TotalFrames;
            }

            Refresh(vm);
        }
    }
}
