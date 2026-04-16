using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Factory for queue job cards (type/file/status on left + dismiss button on right).
    /// </summary>
    public static class QueueJobCardFactory
    {
        public static VisualElement Create(
            string typeText,
            string fileText,
            string statusText,
            System.Action<VisualElement>? onRemove = null)
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

            var type = new Label(typeText) { name = "queue-job-type" };
            type.style.unityFontStyleAndWeight = FontStyle.Bold;
            type.style.fontSize = 10;
            type.style.color = new Color(0.89f, 0.85f, 0.48f, 1f);
            type.style.marginBottom = 2f;

            var file = new Label(fileText) { name = "queue-job-file" };
            file.style.fontSize = 10;
            file.style.color = new Color(0.77f, 0.76f, 0.67f, 1f);
            file.style.whiteSpace = WhiteSpace.NoWrap;
            file.style.textOverflow = TextOverflow.Ellipsis;
            file.style.marginBottom = 2f;

            var status = new Label(statusText) { name = "queue-job-status" };
            status.style.fontSize = 9;
            status.style.color = new Color(0.62f, 0.62f, 0.56f, 1f);
            status.style.whiteSpace = WhiteSpace.NoWrap;
            status.style.textOverflow = TextOverflow.Ellipsis;

            left.Add(type);
            left.Add(file);
            left.Add(status);

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
                Debug.Log($"[CorridorKey] Queue card remove clicked: {fileText}");
                onRemove?.Invoke(card);
            };

            card.Add(left);
            card.Add(removeBtn);
            return card;
        }
    }
}
