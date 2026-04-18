#nullable enable
using CorridorKey.Editor.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>References on a queue card for live updates from <see cref="QueueJobVm"/>.</summary>
    public sealed class QueueJobCardHandle
    {
        public QueueJobCardHandle(QueueJobVm vm, ProgressBar progress, Label statusLabel)
        {
            Vm = vm;
            Progress = progress;
            StatusLabel = statusLabel;
        }

        public QueueJobVm Vm { get; }

        public ProgressBar Progress { get; }

        public Label StatusLabel { get; }
    }

    /// <summary>
    /// Factory for queue job cards (type / clip / progress / status + dismiss).
    /// </summary>
    public static class QueueJobCardFactory
    {
        /// <summary>Attaches <see cref="QueueJobCardHandle"/> to <paramref name="card"/>.userData.</summary>
        public static VisualElement Create(QueueJobVm vm, System.Action<VisualElement>? onRemove = null)
        {
            var card = new VisualElement { name = "queue-job-card" };
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.FlexStart;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.paddingLeft = 8f;
            card.style.paddingRight = 8f;
            card.style.paddingTop = 6f;
            card.style.paddingBottom = 6f;
            card.style.marginBottom = 6f;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftColor = new Color(0.2f, 0.19f, 0.11f, 1f);
            card.style.borderRightColor = new Color(0.2f, 0.19f, 0.11f, 1f);
            card.style.borderTopColor = new Color(0.2f, 0.19f, 0.11f, 1f);
            card.style.borderBottomColor = new Color(0.2f, 0.19f, 0.11f, 1f);
            card.style.backgroundColor = new Color(0.09f, 0.085f, 0.02f, 1f);

            var left = new VisualElement { name = "queue-job-card-left" };
            left.style.flexDirection = FlexDirection.Column;
            left.style.flexGrow = 1;
            left.style.flexShrink = 1;
            left.style.minWidth = 0f;

            var type = new Label(vm.TypeDisplay) { name = "queue-job-type" };
            type.style.unityFontStyleAndWeight = FontStyle.Bold;
            type.style.fontSize = 10;
            type.style.color = new Color(0.89f, 0.85f, 0.48f, 1f);
            type.style.marginBottom = 2f;

            var file = new Label(vm.ClipFileLabel) { name = "queue-job-file" };
            file.style.fontSize = 10;
            file.style.color = new Color(0.77f, 0.76f, 0.67f, 1f);
            file.style.whiteSpace = WhiteSpace.NoWrap;
            file.style.textOverflow = TextOverflow.Ellipsis;
            file.style.marginBottom = 4f;

            var progress = new ProgressBar { name = "queue-job-progress", title = " " };
            progress.AddToClassList("corridor-key-queue-job-progress");
            progress.style.height = 16f;
            progress.style.marginBottom = 4f;
            progress.lowValue = 0f;
            progress.highValue = 100f;

            var status = new Label(FormatStatusLine(vm)) { name = "queue-job-status" };
            status.style.fontSize = 9;
            status.style.color = new Color(0.62f, 0.62f, 0.56f, 1f);
            status.style.whiteSpace = WhiteSpace.NoWrap;
            status.style.textOverflow = TextOverflow.Ellipsis;

            left.Add(type);
            left.Add(file);
            left.Add(progress);
            left.Add(status);

            var handle = new QueueJobCardHandle(vm, progress, status);
            card.userData = handle;

            var removeBtn = new Button { text = "X" };
            removeBtn.name = "queue-job-remove";
            removeBtn.tooltip = "Remove this job card";
            removeBtn.style.width = 22f;
            removeBtn.style.height = 22f;
            removeBtn.style.marginLeft = 8f;
            removeBtn.style.fontSize = 10;
            removeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            removeBtn.style.backgroundColor = new Color(0.14f, 0.13f, 0.06f, 1f);
            removeBtn.style.color = new Color(0.86f, 0.84f, 0.72f, 1f);
            removeBtn.style.borderLeftWidth = 1f;
            removeBtn.style.borderRightWidth = 1f;
            removeBtn.style.borderTopWidth = 1f;
            removeBtn.style.borderBottomWidth = 1f;
            removeBtn.style.borderLeftColor = new Color(0.24f, 0.23f, 0.12f, 1f);
            removeBtn.style.borderRightColor = new Color(0.24f, 0.23f, 0.12f, 1f);
            removeBtn.style.borderTopColor = new Color(0.24f, 0.23f, 0.12f, 1f);
            removeBtn.style.borderBottomColor = new Color(0.24f, 0.23f, 0.12f, 1f);
            removeBtn.clicked += () =>
            {
                Debug.Log($"[CorridorKey] Queue card remove clicked: {vm.ClipFileLabel}");
                onRemove?.Invoke(card);
            };

            card.Add(left);
            card.Add(removeBtn);

            Apply(card, vm);
            return card;
        }

        /// <summary>Updates progress bar and status label from <paramref name="vm"/> (card must be from <see cref="Create"/>).</summary>
        public static void Apply(VisualElement card, QueueJobVm vm)
        {
            if (card.userData is not QueueJobCardHandle h)
                return;
            Apply(h, vm);
        }

        public static void Apply(QueueJobCardHandle handle, QueueJobVm vm)
        {
            var bar = handle.Progress;
            var statusLabel = handle.StatusLabel;

            if (vm.TotalFrames > 0)
            {
                bar.lowValue = 0f;
                bar.highValue = 100f;
                var t = Mathf.Clamp01(vm.TotalFrames > 0 ? (float)vm.CurrentFrame / vm.TotalFrames : 0f);
                bar.value = t * 100f;
                bar.title = $"{vm.CurrentFrame} / {vm.TotalFrames}";
            }
            else
            {
                var indeterminate = vm.Status is QueueJobStatus.Queued or QueueJobStatus.Running;
                bar.lowValue = 0f;
                bar.highValue = 100f;
                bar.value = indeterminate ? 0f : (vm.Status == QueueJobStatus.Succeeded ? 100f : 0f);
                bar.title = indeterminate ? "Processing…" : "";
            }

            statusLabel.text = FormatStatusLine(vm);
        }

        static string FormatStatusLine(QueueJobVm vm)
        {
            if (!string.IsNullOrEmpty(vm.Detail))
                return vm.Detail;
            return vm.Status switch
            {
                QueueJobStatus.Queued => "Queued",
                QueueJobStatus.Running => "Running",
                QueueJobStatus.Succeeded => "DONE",
                QueueJobStatus.Failed => "Failed",
                QueueJobStatus.Cancelled => "Cancelled",
                _ => "",
            };
        }
    }
}
