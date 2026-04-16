using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// EZ <c>ui/main_window.py</c> structural parity: header → top block (queue sidebar beside viewer+params
    /// only; queue bottom aligns with I/O tray top) → full-width I/O tray → status bar.
    /// Queue matches <c>queue_panel.py</c> (left tab strip; height = viewer block, not tray/status).
    /// </summary>
    public static class CorridorKeyWindowLayout
    {
        public const float ParametersRailWidthPx = 280f;

        /// <summary>EZ <c>_TAB_W</c> — always-visible click strip.</summary>
        public const float QueueTabWidthPx = 24f;

        /// <summary>EZ <c>_CONTENT_W</c>.</summary>
        public const float QueueContentWidthPx = 216f;

        /// <summary>EZ <c>_EXPANDED_W</c> = tab + content.</summary>
        public const float QueueExpandedWidthPx = QueueTabWidthPx + QueueContentWidthPx;

        /// <summary>Root column under the menu bar (flex-grow, fills the window).</summary>
        public static VisualElement BuildMainBodyColumn()
        {
            var column = new VisualElement { name = "main-body-column" };
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.minHeight = 0;
            column.style.minWidth = 0;

            column.Add(BuildHeaderStrip());
            column.Add(BuildWorkspaceColumn());

            return column;
        }

        /// <summary>
        /// Column: [queue | viewer+params] row (flex-grow, ~EZ splitter top) → I/O tray → status bar.
        /// Queue height stops at the INPUT / I/O tray top (EZ <c>_position_queue_panel</c>).
        /// </summary>
        static VisualElement BuildWorkspaceColumn()
        {
            var column = new VisualElement { name = "workspace-column" };
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.minHeight = 0;
            column.style.minWidth = 0;

            var topBlock = new VisualElement { name = "viewer-params-block-row" };
            topBlock.style.flexDirection = FlexDirection.Row;
            topBlock.style.flexGrow = 1;
            topBlock.style.flexShrink = 0;
            topBlock.style.minHeight = 0;
            topBlock.style.alignItems = Align.Stretch;
            topBlock.style.marginBottom = 6f;

            topBlock.Add(BuildQueueSidebar());
            topBlock.Add(BuildMainWorkRow());

            column.Add(topBlock);
            column.Add(BuildIoTray());
            column.Add(BuildStatusBar());

            return column;
        }

        static VisualElement BuildQueueSidebar()
        {
            var sidebar = new VisualElement { name = "queue-sidebar" };
            sidebar.style.flexDirection = FlexDirection.Row;
            sidebar.style.flexShrink = 0;
            sidebar.style.flexGrow = 0;
            sidebar.style.alignItems = Align.Stretch;
            sidebar.style.overflow = Overflow.Hidden;
            sidebar.style.borderRightWidth = 1f;
            sidebar.style.borderRightColor = new Color(0.22f, 0.21f, 0.12f);
            sidebar.style.backgroundColor = new Color(0.055f, 0.051f, 0f, 1f);

            var tab = new VisualElement { name = "queue-tab" };
            tab.style.width = QueueTabWidthPx;
            tab.style.flexShrink = 0;
            tab.style.flexGrow = 0;
            tab.style.flexDirection = FlexDirection.Column;
            tab.style.justifyContent = Justify.Center;
            tab.style.alignItems = Align.Center;
            tab.style.backgroundColor = new Color(0.08f, 0.075f, 0.02f, 1f);

            foreach (var ch in "QUEUE")
            {
                var letter = new Label(ch.ToString());
                letter.pickingMode = PickingMode.Ignore;
                letter.style.fontSize = 11;
                letter.style.unityFontStyleAndWeight = FontStyle.Bold;
                letter.style.color = new Color(0.75f, 0.74f, 0.65f);
                letter.style.unityTextAlign = TextAnchor.MiddleCenter;
                tab.Add(letter);
            }

            var content = new VisualElement { name = "queue-content" };
            content.style.width = QueueContentWidthPx;
            content.style.flexShrink = 0;
            content.style.flexGrow = 0;
            content.style.minHeight = 0;
            content.style.flexDirection = FlexDirection.Column;
            content.style.paddingLeft = 6f;
            content.style.paddingRight = 4f;
            content.style.paddingTop = 6f;
            content.style.paddingBottom = 6f;
            content.style.backgroundColor = new Color(0.055f, 0.051f, 0f, 1f);

            var title = new Label("Queue");
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4f;

            var scroll = new ScrollView { name = "queue-scroll" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;

            var placeholder = new Label("Job list binds here (EZ queue panel parity).");
            placeholder.name = "queue-placeholder";
            placeholder.style.fontSize = 10;
            placeholder.style.whiteSpace = WhiteSpace.Normal;
            placeholder.style.color = new Color(0.55f, 0.55f, 0.5f);
            scroll.Add(placeholder);

            content.Add(title);
            content.Add(scroll);

            sidebar.Add(tab);
            sidebar.Add(content);

            sidebar.style.width = QueueTabWidthPx;
            content.style.display = DisplayStyle.None;

            return sidebar;
        }

        static VisualElement BuildHeaderStrip()
        {
            var row = new VisualElement { name = "header-strip" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingBottom = 6;
            row.style.marginBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);

            var brand = new Label("EZ CorridorKey");
            brand.name = "header-brand";
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.fontSize = 13;
            brand.style.flexGrow = 0;

            var gpu = new Label("GPU | VRAM — / —");
            gpu.name = "header-gpu-placeholder";
            gpu.style.fontSize = 11;
            gpu.style.color = new Color(0.65f, 0.65f, 0.6f);

            row.Add(brand);
            row.Add(gpu);

            return row;
        }

        static VisualElement BuildMainWorkRow()
        {
            var row = new VisualElement { name = "main-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow = 1;
            row.style.flexShrink = 0;
            row.style.minWidth = 0;
            row.style.minHeight = 180f;

            var viewerColumn = new VisualElement { name = "viewer-column" };
            viewerColumn.style.flexDirection = FlexDirection.Column;
            viewerColumn.style.flexGrow = 1;
            viewerColumn.style.minWidth = 200f;
            viewerColumn.style.minHeight = 0;

            var dualRow = new VisualElement { name = "dual-viewer-row" };
            dualRow.style.flexDirection = FlexDirection.Row;
            dualRow.style.flexGrow = 1;
            dualRow.style.minHeight = 120f;

            dualRow.Add(CreateViewerPane("viewer-input", "INPUT"));
            dualRow.Add(CreateViewerPane("viewer-output", "OUTPUT"));

            viewerColumn.Add(dualRow);

            var parametersRail = new VisualElement { name = "parameters-rail" };
            parametersRail.style.width = ParametersRailWidthPx;
            parametersRail.style.flexShrink = 0;
            parametersRail.style.flexGrow = 0;
            parametersRail.style.minHeight = 0;
            parametersRail.style.marginLeft = 8f;
            parametersRail.style.paddingLeft = 8f;
            parametersRail.style.paddingRight = 8f;
            parametersRail.style.paddingTop = 4f;
            parametersRail.style.paddingBottom = 4f;
            parametersRail.style.borderLeftWidth = 1f;
            parametersRail.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f);
            parametersRail.style.flexDirection = FlexDirection.Column;

            var parametersTitle = new Label("Parameters");
            parametersTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            parametersTitle.style.fontSize = 11;
            parametersTitle.style.marginBottom = 4f;
            parametersTitle.style.flexShrink = 0;

            var scroll = new ScrollView { name = "parameters-scroll" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;

            var parametersPlaceholder = new Label(
                "Inference and alpha controls will bind here (EZ parameter panel parity).");
            parametersPlaceholder.name = "parameters-placeholder";
            parametersPlaceholder.style.whiteSpace = WhiteSpace.Normal;
            parametersPlaceholder.style.fontSize = 11;
            parametersPlaceholder.style.color = new Color(0.7f, 0.7f, 0.65f);
            scroll.Add(parametersPlaceholder);

            parametersRail.Add(parametersTitle);
            parametersRail.Add(scroll);

            row.Add(viewerColumn);
            row.Add(parametersRail);

            return row;
        }

        static VisualElement CreateViewerPane(string paneName, string title)
        {
            var col = new VisualElement { name = paneName };
            col.style.flexGrow = 1;
            col.style.flexBasis = 0;
            col.style.flexDirection = FlexDirection.Column;
            col.style.minWidth = 60f;
            col.style.marginRight = 4f;

            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 11;
            header.style.marginBottom = 4f;
            header.style.flexShrink = 0;

            var surface = new VisualElement { name = $"{paneName}-surface" };
            surface.style.flexGrow = 1;
            surface.style.minHeight = 80f;
            surface.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            surface.style.borderTopWidth = 1f;
            surface.style.borderBottomWidth = 1f;
            surface.style.borderLeftWidth = 1f;
            surface.style.borderRightWidth = 1f;
            surface.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f);
            surface.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);
            surface.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f);
            surface.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f);
            surface.style.alignItems = Align.Center;
            surface.style.justifyContent = Justify.Center;

            var hint = new Label("Viewer");
            hint.style.fontSize = 10;
            hint.style.color = new Color(0.45f, 0.45f, 0.42f);
            surface.Add(hint);

            col.Add(header);
            col.Add(surface);

            return col;
        }

        static VisualElement BuildIoTray()
        {
            var tray = new VisualElement { name = "io-tray" };
            tray.style.flexShrink = 0;
            tray.style.flexDirection = FlexDirection.Row;
            tray.style.minHeight = 72f;
            tray.style.marginBottom = 6f;
            tray.style.paddingTop = 6f;
            tray.style.paddingBottom = 6f;
            tray.style.paddingLeft = 8f;
            tray.style.paddingRight = 8f;
            tray.style.backgroundColor = new Color(0.16f, 0.16f, 0.15f, 0.9f);
            tray.style.borderTopWidth = 1f;
            tray.style.borderBottomWidth = 1f;
            tray.style.borderLeftWidth = 1f;
            tray.style.borderRightWidth = 1f;
            tray.style.borderTopColor = new Color(0.3f, 0.3f, 0.28f);
            tray.style.borderBottomColor = new Color(0.3f, 0.3f, 0.28f);
            tray.style.borderLeftColor = new Color(0.3f, 0.3f, 0.28f);
            tray.style.borderRightColor = new Color(0.3f, 0.3f, 0.28f);

            var inputSide = new VisualElement { name = "io-tray-input" };
            inputSide.style.flexGrow = 1;
            inputSide.style.flexDirection = FlexDirection.Column;
            var inputTitle = new Label("INPUT (0)  —  import / clip list");
            inputTitle.style.fontSize = 11;
            inputTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            inputSide.Add(inputTitle);

            var exportSide = new VisualElement { name = "io-tray-exports" };
            exportSide.style.flexGrow = 1;
            exportSide.style.flexDirection = FlexDirection.Column;
            exportSide.style.marginLeft = 12f;
            var exportTitle = new Label("EXPORTS (0)");
            exportTitle.style.fontSize = 11;
            exportTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            exportSide.Add(exportTitle);

            tray.Add(inputSide);
            tray.Add(exportSide);

            return tray;
        }

        static VisualElement BuildStatusBar()
        {
            var bar = new VisualElement { name = "status-bar" };
            bar.style.flexShrink = 0;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.paddingTop = 6f;
            bar.style.paddingBottom = 6f;
            bar.style.paddingLeft = 4f;
            bar.style.paddingRight = 4f;
            bar.style.borderTopWidth = 1f;
            bar.style.borderTopColor = new Color(0.35f, 0.35f, 0.35f);

            var left = new VisualElement { name = "status-bar-left" };
            left.style.flexDirection = FlexDirection.Row;
            left.style.flexGrow = 1;
            var progress = new Label("Ready");
            progress.name = "status-progress";
            progress.style.fontSize = 11;
            progress.style.color = new Color(0.75f, 0.75f, 0.7f);
            left.Add(progress);

            var right = new VisualElement { name = "status-bar-right" };
            right.style.flexDirection = FlexDirection.Row;
            var runHint = new Label("RUN / STOP — (placeholder)");
            runHint.name = "status-run-placeholder";
            runHint.style.fontSize = 11;
            runHint.style.color = new Color(0.6f, 0.6f, 0.55f);
            right.Add(runHint);

            bar.Add(left);
            bar.Add(right);

            return bar;
        }
    }
}
