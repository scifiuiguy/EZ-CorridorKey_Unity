using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// EZ <c>ui/main_window.py</c> structural parity: header → top block (queue sidebar beside viewer+params
    /// only; queue bottom aligns with I/O tray top) → full-width I/O tray → status bar.
    /// Queue matches <c>queue_panel.py</c> (left tab strip; height = viewer block, not tray/status).
    /// INPUT/OUTPUT split uses a draggable divider; I/O tray left width tracks INPUT in pixels
    /// (<see cref="HorizontalViewerIoSplitController"/>), matching EZ <c>_sync_io_divider</c>.
    /// <c>io-tray-queue-spacer</c> matches <c>queue-sidebar</c> width so the tray aligns with the viewer row (EZ uses an overlay queue).
    /// </summary>
    public static class CorridorKeyWindowLayout
    {
        /// <summary>EZ parameters column width (content only; tab is extra).</summary>
        public const float ParametersRailWidthPx = 280f;

        /// <summary>Unity-only vertical tab — same width as <see cref="QueueTabWidthPx"/>.</summary>
        public const float ParametersTabWidthPx = 24f;

        public const float ParametersExpandedWidthPx = ParametersTabWidthPx + ParametersRailWidthPx;

        /// <summary>EZ <c>_TAB_W</c> — always-visible click strip.</summary>
        public const float QueueTabWidthPx = 24f;

        /// <summary>EZ <c>_CONTENT_W</c>.</summary>
        public const float QueueContentWidthPx = 216f;

        /// <summary>EZ <c>_EXPANDED_W</c> = tab + content.</summary>
        public const float QueueExpandedWidthPx = QueueTabWidthPx + QueueContentWidthPx;

        /// <summary>Horizontal strip under INPUTS/EXPORTS with IN/EX tab toggles.</summary>
        public const float IoTrayFilesBarHeightPx = 22f;

        /// <summary>Tray height when INPUT/EXPORT row is hidden (Files bar + tray padding only).</summary>
        public const float IoTrayCollapsedHeightPx =
            6f + 6f + 2f + IoTrayFilesBarHeightPx + 4f;

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
        /// Column: viewer+params row (full width) with queue overlaid on the left (EZ overlay parity) → I/O tray → status bar.
        /// Queue does not steal flex width from parameters; <see cref="HorizontalViewerIoSplitController"/> pads viewer column.
        /// <see cref="VerticalViewerIoTraySplitController"/> sits between the viewer block and the I/O tray.
        /// </summary>
        static VisualElement BuildWorkspaceColumn()
        {
            var column = new VisualElement { name = "workspace-column" };
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.minHeight = 0;
            column.style.minWidth = 0;

            var topBlock = new VisualElement { name = "viewer-params-block-row" };
            topBlock.style.position = Position.Relative;
            topBlock.style.flexDirection = FlexDirection.Row;
            topBlock.style.flexGrow = 1;
            topBlock.style.flexShrink = 1;
            topBlock.style.minHeight = VerticalViewerIoTraySplitController.MinViewerBlockHeightPx;
            topBlock.style.alignItems = Align.Stretch;
            topBlock.style.marginBottom = 0f;

            // Main row is full width; queue is added after and positioned absolute so it overlays (parameters stay fixed at right edge).
            topBlock.Add(BuildMainWorkRow());
            topBlock.Add(BuildQueueSidebar());

            var ioTrayDivider = new VisualElement { name = "io-tray-split-divider" };
            ioTrayDivider.style.height = VerticalViewerIoTraySplitController.DividerHeightPx;
            ioTrayDivider.style.flexShrink = 0;
            ioTrayDivider.style.flexGrow = 0;
            ioTrayDivider.style.alignSelf = Align.Stretch;
            ioTrayDivider.style.backgroundColor = new Color(0.18f, 0.18f, 0.17f, 1f);
            ioTrayDivider.style.borderTopWidth = 1f;
            ioTrayDivider.style.borderBottomWidth = 1f;
            ioTrayDivider.style.borderTopColor = new Color(0.38f, 0.38f, 0.36f);
            ioTrayDivider.style.borderBottomColor = new Color(0.38f, 0.38f, 0.36f);
            ioTrayDivider.pickingMode = PickingMode.Position;

            column.Add(topBlock);
            column.Add(ioTrayDivider);
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

            var titleRow = new VisualElement { name = "queue-title-row" };
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.marginBottom = 4f;

            var title = new Label("Queue");
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            var clearBtn = new Button { text = "CLEAR" };
            clearBtn.name = "queue-clear-button";
            clearBtn.style.height = 20f;
            clearBtn.style.fontSize = 9;
            clearBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            clearBtn.style.paddingLeft = 6f;
            clearBtn.style.paddingRight = 6f;
            clearBtn.tooltip = "Clear all queue cards";

            titleRow.Add(title);
            titleRow.Add(clearBtn);

            var scroll = new ScrollView { name = "queue-scroll" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;

            var placeholder = new Label("Job list binds here (EZ queue panel parity).");
            placeholder.name = "queue-placeholder";
            placeholder.style.fontSize = 10;
            placeholder.style.whiteSpace = WhiteSpace.Normal;
            placeholder.style.color = new Color(0.55f, 0.55f, 0.5f);
            scroll.Add(placeholder);

            content.Add(titleRow);
            content.Add(scroll);

            sidebar.Add(tab);
            sidebar.Add(content);

            sidebar.style.width = QueueTabWidthPx;
            content.style.display = DisplayStyle.None;

            sidebar.style.position = Position.Absolute;
            sidebar.style.left = 0f;
            sidebar.style.top = 0f;
            sidebar.style.bottom = 0f;

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
            // Must be shrinkable so parameters expansion can reclaim space from viewers.
            row.style.flexShrink = 1;
            row.style.minWidth = 0;
            row.style.minHeight = 180f;

            var viewerColumn = new VisualElement { name = "viewer-column" };
            viewerColumn.style.flexDirection = FlexDirection.Column;
            viewerColumn.style.flexGrow = 1;
            viewerColumn.style.flexShrink = 1;
            // Allow shrinking when parameters expand; pane min widths come from viewer panes + split controller.
            viewerColumn.style.minWidth = 0f;
            viewerColumn.style.minHeight = 0;

            var dualHost = new VisualElement { name = "dual-viewer-host" };
            dualHost.style.flexDirection = FlexDirection.Row;
            dualHost.style.flexGrow = 1;
            dualHost.style.minHeight = 120f;
            dualHost.style.minWidth = 0;
            dualHost.style.alignItems = Align.Stretch;

            var inputCol = CreateViewerPane("viewer-input", "INPUT");
            var divider = new VisualElement { name = "viewer-split-divider" };
            divider.style.width = HorizontalViewerIoSplitController.DividerWidthPx;
            divider.style.flexShrink = 0;
            divider.style.flexGrow = 0;
            divider.style.alignSelf = Align.Stretch;
            divider.style.backgroundColor = new Color(0.18f, 0.18f, 0.17f, 1f);
            divider.style.borderLeftWidth = 1f;
            divider.style.borderRightWidth = 1f;
            divider.style.borderLeftColor = new Color(0.38f, 0.38f, 0.36f);
            divider.style.borderRightColor = new Color(0.38f, 0.38f, 0.36f);
            divider.pickingMode = PickingMode.Position;

            var outputCol = CreateViewerPane("viewer-output", "OUTPUT");

            dualHost.Add(inputCol);
            dualHost.Add(divider);
            dualHost.Add(outputCol);

            viewerColumn.Add(BuildDualViewerChromeBar());
            viewerColumn.Add(dualHost);
            viewerColumn.Add(BuildViewerPlayheadStrip());

            var parametersShell = new VisualElement { name = "parameters-rail-shell" };
            parametersShell.style.flexDirection = FlexDirection.Row;
            parametersShell.style.flexShrink = 0;
            parametersShell.style.flexGrow = 0;
            parametersShell.style.alignItems = Align.Stretch;
            parametersShell.style.overflow = Overflow.Hidden;
            parametersShell.style.marginLeft = 8f;
            parametersShell.style.width = ParametersExpandedWidthPx;

            var parametersTab = new VisualElement { name = "parameters-tab" };
            parametersTab.style.width = ParametersTabWidthPx;
            parametersTab.style.flexShrink = 0;
            parametersTab.style.flexGrow = 0;
            parametersTab.style.flexDirection = FlexDirection.Column;
            parametersTab.style.justifyContent = Justify.Center;
            parametersTab.style.alignItems = Align.Center;
            parametersTab.style.backgroundColor = new Color(0.1f, 0.1f, 0.11f, 1f);
            parametersTab.style.borderLeftWidth = 1f;
            parametersTab.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f);

            foreach (var ch in "ALPHA")
            {
                var letter = new Label(ch.ToString());
                letter.pickingMode = PickingMode.Ignore;
                letter.style.fontSize = 11;
                letter.style.unityFontStyleAndWeight = FontStyle.Bold;
                letter.style.color = new Color(0.72f, 0.72f, 0.68f);
                letter.style.unityTextAlign = TextAnchor.MiddleCenter;
                parametersTab.Add(letter);
            }

            var parametersRail = new VisualElement { name = "parameters-rail" };
            parametersRail.style.width = ParametersRailWidthPx;
            parametersRail.style.flexShrink = 0;
            parametersRail.style.flexGrow = 0;
            parametersRail.style.minHeight = 0;
            parametersRail.style.paddingLeft = 8f;
            parametersRail.style.paddingRight = 8f;
            parametersRail.style.paddingTop = 4f;
            parametersRail.style.paddingBottom = 4f;
            parametersRail.style.borderLeftWidth = 1f;
            parametersRail.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f);
            parametersRail.style.flexDirection = FlexDirection.Column;

            var parametersTitle = new Label("Alpha Generation Parameters");
            parametersTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            parametersTitle.style.fontSize = 11;
            parametersTitle.style.marginBottom = 4f;
            parametersTitle.style.flexShrink = 0;

            var scroll = new ScrollView { name = "parameters-scroll" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;

            BuildParametersRailScrollContent(scroll);

            parametersRail.Add(parametersTitle);
            parametersRail.Add(scroll);

            // Content first, tab last — tab sits on the far right (queue tab sits on the far left of its strip).
            parametersShell.Add(parametersRail);
            parametersShell.Add(parametersTab);

            row.Add(viewerColumn);
            row.Add(parametersShell);

            return row;
        }

        /// <summary>
        /// Unity parameters rail: Auto vs Guided (see <c>ALPHA_GENERATION_UI_IMPROVEMENTS.md</c>), Advanced GVM/BiRefNet,
        /// Guided Draw (Track mask → MatAnyone vs VideoMaMa) or Import.
        /// </summary>
        static void BuildParametersRailScrollContent(ScrollView scroll)
        {
            var root = new VisualElement { name = "parameters-rail-root" };
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            root.style.paddingBottom = 8f;

            var modeRow = new VisualElement { name = "parameters-mode-row" };
            modeRow.style.flexDirection = FlexDirection.Row;
            modeRow.style.alignItems = Align.Center;
            modeRow.style.flexShrink = 0;
            modeRow.style.marginBottom = 8f;

            var modeAutoBtn = new Button { text = "Auto" };
            modeAutoBtn.name = "parameters-toggle-auto";
            modeAutoBtn.tooltip = "Automatic alpha generation — no painting or import.";
            modeAutoBtn.AddToClassList(EzChromeButtonClass);
            modeAutoBtn.AddToClassList(EzChromeButtonActiveClass);
            StyleParamChromeToggleButton(modeAutoBtn, marginLeft: 0f);

            var modeGuidedBtn = new Button { text = "Guided" };
            modeGuidedBtn.name = "parameters-toggle-guided";
            modeGuidedBtn.tooltip = "Guided workflow — draw masks or import alpha.";
            modeGuidedBtn.AddToClassList(EzChromeButtonClass);
            StyleParamChromeToggleButton(modeGuidedBtn, marginLeft: 0f);

            modeRow.Add(modeAutoBtn);
            modeRow.Add(CreateParametersOrLabel("parameters-mode-or"));
            modeRow.Add(modeGuidedBtn);

            // —— Auto ——
            var autoPanel = new VisualElement { name = "parameters-auto-panel" };
            autoPanel.style.flexDirection = FlexDirection.Column;
            autoPanel.style.flexShrink = 0;

            var autoSummary = new Label(
                "The alpha key will be generated automatically when you run (no painting or import required).");
            autoSummary.name = "parameters-auto-summary";
            autoSummary.style.whiteSpace = WhiteSpace.Normal;
            autoSummary.style.fontSize = 10;
            autoSummary.style.color = new Color(0.63f, 0.62f, 0.56f, 1f);
            autoSummary.style.marginBottom = 8f;

            var advancedToggle = new Button { text = "Advanced \u25B6" };
            advancedToggle.name = "parameters-auto-advanced-toggle";
            advancedToggle.AddToClassList("corridor-key-param-advanced-toggle");
            advancedToggle.style.alignSelf = Align.FlexStart;
            advancedToggle.style.marginBottom = 4f;
            advancedToggle.tooltip = "Show or hide GVM and BiRefNet options (collapsed by default).";

            var advancedBody = new VisualElement { name = "parameters-auto-advanced-body" };
            advancedBody.style.flexDirection = FlexDirection.Column;
            advancedBody.style.display = DisplayStyle.None;
            advancedBody.style.paddingLeft = 4f;
            advancedBody.style.paddingTop = 4f;
            advancedBody.style.paddingBottom = 4f;
            advancedBody.style.borderLeftWidth = 2f;
            advancedBody.style.borderLeftColor = new Color(0.35f, 0.34f, 0.2f, 1f);
            advancedBody.style.marginBottom = 4f;

            var gvmBtn = new Button { text = "GVM AUTO" };
            gvmBtn.name = "parameters-gvm-btn";
            gvmBtn.SetEnabled(true);
            gvmBtn.tooltip =
                "Auto-generate alpha hint for the entire clip.\n"
                + "Uses GVM to predict foreground/background separation.\n"
                + "Available when clip is in RAW state (frames extracted).";
            gvmBtn.style.marginBottom = 6f;
            StyleParamRailCtaButton(gvmBtn);

            var birefRow = new VisualElement { name = "parameters-birefnet-row" };
            birefRow.style.flexDirection = FlexDirection.Row;
            birefRow.style.alignItems = Align.Center;
            birefRow.style.marginBottom = 2f;

            var birefBtn = new Button { text = "BIREFNET" };
            birefBtn.name = "parameters-birefnet-btn";
            birefBtn.SetEnabled(true);
            birefBtn.tooltip =
                "Auto-generate alpha hint using BiRefNet.\n"
                + "Fully automatic — no painting or annotation needed.\n"
                + "Downloads the selected model variant on first use.\n\n"
                + "Matting: Best for hair/transparency detail (recommended).\n"
                + "Portrait: Optimized for human close-ups.\n"
                + "General: Balanced foreground/background separation.\n"
                + "HR variants: For 2K/4K footage (uses more VRAM).";
            birefBtn.style.flexGrow = 1;
            birefBtn.style.marginRight = 4f;
            StyleParamRailCtaButton(birefBtn);

            var birefModel = new DropdownField(
                string.Empty,
                BiRefNetModelOptions.CreateChoicesList(),
                BiRefNetModelOptions.DefaultDisplayName);
            birefModel.name = "parameters-birefnet-model";
            birefModel.style.flexGrow = 1;
            birefModel.style.minWidth = 120f;
            birefModel.SetEnabled(true);
            birefModel.tooltip = "BiRefNet model variant — changes take effect on next run (EZ <c>BIREFNET_MODELS</c>).";

            birefRow.Add(birefBtn);
            birefRow.Add(birefModel);

            advancedBody.Add(gvmBtn);
            advancedBody.Add(birefRow);

            autoPanel.Add(autoSummary);
            autoPanel.Add(advancedToggle);
            autoPanel.Add(advancedBody);

            // —— Guided ——
            var guidedPanel = new VisualElement { name = "parameters-guided-panel" };
            guidedPanel.style.flexDirection = FlexDirection.Column;
            guidedPanel.style.flexShrink = 0;
            guidedPanel.style.display = DisplayStyle.None;

            var drawImportRow = new VisualElement { name = "parameters-draw-import-row" };
            drawImportRow.style.flexDirection = FlexDirection.Row;
            drawImportRow.style.alignItems = Align.Center;
            drawImportRow.style.flexShrink = 0;
            drawImportRow.style.marginBottom = 8f;

            var drawToggleBtn = new Button { text = "Draw" };
            drawToggleBtn.name = "parameters-toggle-draw";
            drawToggleBtn.tooltip = "Paint prompts and run track / video matting.";
            drawToggleBtn.AddToClassList(EzChromeButtonClass);
            drawToggleBtn.AddToClassList(EzChromeButtonActiveClass);
            StyleParamChromeToggleButton(drawToggleBtn, marginLeft: 0f);

            var importToggleBtn = new Button { text = "Import" };
            importToggleBtn.name = "parameters-toggle-import";
            importToggleBtn.tooltip = "Import an alpha sequence or video file.";
            importToggleBtn.AddToClassList(EzChromeButtonClass);
            StyleParamChromeToggleButton(importToggleBtn, marginLeft: 0f);

            drawImportRow.Add(drawToggleBtn);
            drawImportRow.Add(CreateParametersOrLabel("parameters-draw-import-or"));
            drawImportRow.Add(importToggleBtn);

            var drawPanel = new VisualElement { name = "parameters-guided-draw" };
            drawPanel.style.flexDirection = FlexDirection.Column;

            var step1 = new Label("Step 1 — Track mask");
            step1.style.unityFontStyleAndWeight = FontStyle.Bold;
            step1.style.fontSize = 10;
            step1.style.color = new Color(0.72f, 0.7f, 0.62f, 1f);
            step1.style.marginTop = 8f;
            step1.style.marginBottom = 4f;

            var trackBtn = new Button { text = "TRACK MASK" };
            trackBtn.name = "parameters-track-mask-btn";
            trackBtn.SetEnabled(true);
            trackBtn.tooltip =
                "Use SAM2 to turn painted prompts into a dense mask track.\n"
                + "Required before running MatAnyone2 or VideoMaMa.\n\n"
                + "HOW TO USE:\n"
                + "1. Press 1 to select the GREEN brush (foreground — subject to keep)\n"
                + "2. Press 2 to select the RED brush (background — area to remove)\n"
                + "3. Paint strokes on the left viewer over your footage\n"
                + "4. Click TRACK MASK to preview SAM2 on the painted frame\n"
                + "5. If the preview looks right, confirm to propagate across all frames";
            trackBtn.style.marginBottom = 6f;
            StyleParamRailCtaButton(trackBtn);

            var strokeHint = new Label("Paint subject with 1, background with 2");
            strokeHint.style.fontSize = 9;
            strokeHint.style.color = new Color(0.55f, 0.54f, 0.48f, 1f);
            strokeHint.style.marginBottom = 8f;
            strokeHint.style.whiteSpace = WhiteSpace.Normal;

            var step2 = new Label("Step 2 — Video matting");
            step2.style.unityFontStyleAndWeight = FontStyle.Bold;
            step2.style.fontSize = 10;
            step2.style.color = new Color(0.72f, 0.7f, 0.62f, 1f);
            step2.style.marginTop = 10f;
            step2.style.marginBottom = 4f;

            var mattingEngineRow = new VisualElement { name = "parameters-matting-engine-row" };
            mattingEngineRow.style.flexDirection = FlexDirection.Row;
            mattingEngineRow.style.alignItems = Align.Center;
            mattingEngineRow.style.flexShrink = 0;
            mattingEngineRow.style.marginBottom = 6f;

            var mattingEngineMatBtn = new Button { text = "MatAnyone2" };
            mattingEngineMatBtn.name = "parameters-toggle-matanyone";
            mattingEngineMatBtn.tooltip =
                "Choose MatAnyone2 for step 2 video matting.\n\n"
                + "Recommendation: Prefer MatAnyone2 for people/subjects with fast motion,\n"
                + "fine hair detail, or when you need stronger temporal coherence.\n\n"
                + "Generate alpha hints using MatAnyone2 video matting.\n"
                + "Requires paint strokes on the FIRST FRAME (frame 1).\n\n"
                + "1. Navigate to frame 1 (the very first frame)\n"
                + "2. Paint foreground (hotkey 1) and background (hotkey 2)\n"
                + "3. Click Track Mask to generate dense masks with SAM2\n"
                + "4. Run MatAnyone2 to generate temporally coherent AlphaHint";
            mattingEngineMatBtn.AddToClassList(EzChromeButtonClass);
            mattingEngineMatBtn.AddToClassList(EzChromeButtonActiveClass);
            StyleParamChromeToggleButton(mattingEngineMatBtn, marginLeft: 0f);

            var mattingEngineVmBtn = new Button { text = "VideoMaMa" };
            mattingEngineVmBtn.name = "parameters-toggle-videomama";
            mattingEngineVmBtn.tooltip =
                "Choose VideoMaMa for step 2 video matting.\n\n"
                + "Recommendation: Try VideoMaMa for simpler shots or when you want a\n"
                + "lighter/faster first pass before refining difficult edges.\n\n"
                + "Generate alpha hints from a dense VideoMaMa mask track.\n\n"
                + "1. Paint sparse foreground/background prompts\n"
                + "2. Click Track Mask to generate dense masks with SAM2\n"
                + "3. Run VideoMaMa to generate AlphaHint";
            mattingEngineVmBtn.AddToClassList(EzChromeButtonClass);
            StyleParamChromeToggleButton(mattingEngineVmBtn, marginLeft: 2f);

            mattingEngineRow.Add(mattingEngineMatBtn);
            mattingEngineRow.Add(mattingEngineVmBtn);

            var mattingHint = new Label("Requires paint strokes on frame 1")
            {
                name = "parameters-guided-matting-hint",
            };
            mattingHint.style.fontSize = 9;
            mattingHint.style.color = new Color(0.55f, 0.54f, 0.48f, 1f);
            mattingHint.style.marginBottom = 4f;
            mattingHint.style.whiteSpace = WhiteSpace.Normal;

            drawPanel.Add(step1);
            drawPanel.Add(trackBtn);
            drawPanel.Add(strokeHint);
            drawPanel.Add(step2);
            drawPanel.Add(mattingEngineRow);
            drawPanel.Add(mattingHint);

            var importPanel = new VisualElement { name = "parameters-guided-import" };
            importPanel.style.flexDirection = FlexDirection.Column;
            importPanel.style.display = DisplayStyle.None;

            var importHint = new Label(
                "Import an alpha sequence or video; files are copied into the clip's AlphaHint folder.");
            importHint.style.whiteSpace = WhiteSpace.Normal;
            importHint.style.fontSize = 10;
            importHint.style.color = new Color(0.63f, 0.62f, 0.56f, 1f);
            importHint.style.marginBottom = 8f;

            var importBtn = new Button { text = "IMPORT ALPHA" };
            importBtn.name = "parameters-import-alpha-btn";
            importBtn.SetEnabled(true);
            importBtn.tooltip =
                "Import alpha hints from an image folder or video file.\n"
                + "Supports: PNG/JPG/TIF/EXR sequences, or MOV/MP4/ProRes video.\n"
                + "White = foreground, black = background.\n"
                + "Files are copied into the clip's AlphaHint/ folder\n"
                + "and the clip advances to READY state for inference.";
            StyleParamRailCtaButton(importBtn);

            importPanel.Add(importHint);
            importPanel.Add(importBtn);

            guidedPanel.Add(drawImportRow);
            guidedPanel.Add(drawPanel);
            guidedPanel.Add(importPanel);

            root.Add(modeRow);
            root.Add(autoPanel);
            root.Add(guidedPanel);

            scroll.Add(root);
        }

        static void StyleParamRailCtaButton(Button btn)
        {
            btn.style.minHeight = 22f;
            btn.style.fontSize = 10;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        static void StyleParamChromeToggleButton(Button btn, float marginLeft)
        {
            btn.style.minWidth = 56f;
            btn.style.height = 24f;
            btn.style.fontSize = 10;
            btn.style.marginLeft = marginLeft;
        }

        static Label CreateParametersOrLabel(string name)
        {
            var or = new Label("OR");
            or.name = name;
            or.pickingMode = PickingMode.Ignore;
            or.style.fontSize = 10;
            or.style.unityFontStyleAndWeight = FontStyle.Bold;
            or.style.color = new Color(0.5f, 0.49f, 0.44f, 1f);
            or.style.marginLeft = 6f;
            or.style.marginRight = 6f;
            or.style.flexShrink = 0;
            return or;
        }

        /// <summary>
        /// EZ <c>dual_viewer.py</c> bottom row: shared <c>FrameScrubber</c> + zoom readout (coverage placeholder + slider + transport).
        /// </summary>
        static VisualElement BuildViewerPlayheadStrip()
        {
            var strip = new VisualElement { name = "viewer-playhead-strip" };
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.flexShrink = 0;
            strip.style.alignItems = Align.Center;
            strip.style.minHeight = 48f;
            strip.style.paddingLeft = 8f;
            strip.style.paddingRight = 8f;
            strip.style.paddingTop = 0f;
            strip.style.paddingBottom = 0f;
            strip.style.backgroundColor = new Color(14f / 255f, 13f / 255f, 0f, 1f);
            strip.style.borderTopWidth = 1f;
            strip.style.borderTopColor = new Color(42f / 255f, 41f / 255f, 16f / 255f, 1f);

            var frameLabel = new Label("0 / 0");
            frameLabel.name = "viewer-playhead-frame-label";
            frameLabel.style.width = 90f;
            frameLabel.style.flexShrink = 0;
            frameLabel.style.fontSize = 11;
            frameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            frameLabel.style.color = new Color(128f / 255f, 128f / 255f, 112f / 255f, 1f);
            frameLabel.pickingMode = PickingMode.Ignore;

            var startBtn = new Button { text = "\u25C0\u25C0" };
            startBtn.name = "viewer-playhead-btn-start";
            startBtn.tooltip = "Go to first frame";
            startBtn.AddToClassList("corridor-key-playhead-transport-btn");
            startBtn.style.width = 28f;

            var prevBtn = new Button { text = "\u25C0" };
            prevBtn.name = "viewer-playhead-btn-prev";
            prevBtn.tooltip = "Previous frame";
            prevBtn.AddToClassList("corridor-key-playhead-transport-btn");
            prevBtn.style.width = 24f;

            var playBtn = new Button { text = "\u25B6" };
            playBtn.name = "viewer-playhead-btn-play";
            playBtn.tooltip = "Play / Pause (Space)";
            playBtn.AddToClassList("corridor-key-playhead-transport-btn");
            playBtn.style.width = 24f;

            var center = new VisualElement { name = "viewer-playhead-center" };
            center.style.flexGrow = 1;
            center.style.flexShrink = 1;
            center.style.minWidth = 40f;
            center.style.flexDirection = FlexDirection.Column;
            center.style.justifyContent = Justify.FlexStart;
            center.style.marginLeft = 4f;
            center.style.marginRight = 4f;

            var coverage = new VisualElement { name = "viewer-playhead-coverage" };
            coverage.AddToClassList("viewer-playhead-coverage-placeholder");
            coverage.style.height = 8f;
            coverage.style.marginBottom = 2f;
            coverage.style.flexShrink = 0;
            coverage.style.backgroundColor = new Color(26f / 255f, 25f / 255f, 0f, 1f);
            coverage.tooltip =
                "Coverage bar — shows which frames have been processed.\n"
                + "Green lane: painted frames (brush strokes).\n"
                + "White lane: alpha hint coverage.\n"
                + "Yellow lane: inference output coverage.\n\n"
                + "(Binds to clip state; EZ <c>CoverageBar</c>.)";

            var slider = new Slider(0f, 0f, SliderDirection.Horizontal);
            slider.name = "viewer-playhead-slider";
            slider.AddToClassList("corridor-key-playhead-slider");
            slider.style.flexGrow = 1;
            slider.style.minHeight = 22f;
            slider.style.flexShrink = 1;
            slider.tooltip = "Scrub through frames. Scroll wheel or Left/Right to step.";
            slider.SetEnabled(false);

            center.Add(coverage);
            center.Add(slider);

            var nextBtn = new Button { text = "\u25B6" };
            nextBtn.name = "viewer-playhead-btn-next";
            nextBtn.tooltip = "Next frame";
            nextBtn.AddToClassList("corridor-key-playhead-transport-btn");
            nextBtn.style.width = 24f;

            var endBtn = new Button { text = "\u25B6\u25B6" };
            endBtn.name = "viewer-playhead-btn-end";
            endBtn.tooltip = "Go to last frame";
            endBtn.AddToClassList("corridor-key-playhead-transport-btn");
            endBtn.style.width = 28f;

            var zoomLabel = new Label("100%");
            zoomLabel.name = "viewer-playhead-zoom-label";
            zoomLabel.style.width = 50f;
            zoomLabel.style.flexShrink = 0;
            zoomLabel.style.fontSize = 10;
            zoomLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            zoomLabel.style.color = new Color(128f / 255f, 128f / 255f, 112f / 255f, 1f);
            zoomLabel.pickingMode = PickingMode.Ignore;
            zoomLabel.tooltip = "Viewer zoom (binds to preview; EZ output viewer primary).";

            strip.Add(frameLabel);
            strip.Add(startBtn);
            strip.Add(prevBtn);
            strip.Add(playBtn);
            strip.Add(center);
            strip.Add(nextBtn);
            strip.Add(endBtn);
            strip.Add(zoomLabel);
            return strip;
        }

        /// <summary>
        /// Single shared top bar above the dual viewer — EZ <c>PreviewViewport</c> top bar + <c>ViewModeBar</c>
        /// (one bar spans both panes; output viewer uses view modes, input is INPUT-locked in EZ).
        /// Tooltips from <c>ui/widgets/view_mode_bar.py</c> and <c>PreviewViewport.add_ab_button</c>.
        /// </summary>
        static VisualElement BuildDualViewerChromeBar()
        {
            var chrome = new VisualElement { name = "dual-viewer-chrome" };
            chrome.style.flexDirection = FlexDirection.Row;
            chrome.style.alignItems = Align.Center;
            chrome.style.flexShrink = 0;
            chrome.style.minWidth = 0f;
            chrome.style.minHeight = 30f;
            chrome.style.paddingLeft = 8f;
            chrome.style.paddingRight = 8f;
            chrome.style.paddingTop = 2f;
            chrome.style.paddingBottom = 2f;
            chrome.style.backgroundColor = new Color(14f / 255f, 13f / 255f, 0f, 1f);

            var frameLabel = new Label("Frame — / —");
            frameLabel.name = "viewer-shared-frame-label";
            frameLabel.style.fontSize = 10;
            frameLabel.style.flexShrink = 0;
            frameLabel.style.marginRight = 8f;
            frameLabel.style.color = new Color(0.75f, 0.75f, 0.7f, 1f);
            frameLabel.pickingMode = PickingMode.Ignore;

            var statusLabel = new Label("Ready");
            statusLabel.name = "viewer-shared-status-label";
            statusLabel.style.fontSize = 10;
            statusLabel.style.flexGrow = 1;
            statusLabel.style.flexShrink = 1;
            statusLabel.style.minWidth = 0f;
            statusLabel.style.whiteSpace = WhiteSpace.NoWrap;
            statusLabel.style.textOverflow = TextOverflow.Ellipsis;
            statusLabel.style.color = new Color(0.55f, 0.55f, 0.5f, 1f);
            statusLabel.pickingMode = PickingMode.Ignore;

            var abButton = new Button { text = "A/B" };
            abButton.name = "viewer-ab-button";
            abButton.tooltip = TooltipAbWipe;
            abButton.style.width = 50f;
            abButton.style.height = 24f;
            abButton.style.marginRight = 0f;
            abButton.AddToClassList(EzChromeButtonClass);

            var abDivider = new VisualElement { name = "viewer-ab-divider" };
            abDivider.pickingMode = PickingMode.Ignore;
            abDivider.style.width = 1f;
            abDivider.style.height = 18f;
            abDivider.style.flexShrink = 0;
            abDivider.style.flexGrow = 0;
            abDivider.style.marginLeft = 8f;
            abDivider.style.marginRight = 8f;
            abDivider.style.backgroundColor = new Color(42f / 255f, 41f / 255f, 16f / 255f, 1f);

            var modeBar = new VisualElement { name = "viewer-view-mode-bar" };
            modeBar.style.flexDirection = FlexDirection.Row;
            modeBar.style.alignItems = Align.Center;
            modeBar.style.flexShrink = 0;

            for (var i = 0; i < ViewModeChromeSpecs.Length; i++)
            {
                var spec = ViewModeChromeSpecs[i];
                var modeBtn = new Button { text = spec.Label };
                modeBtn.name = $"viewer-view-mode-{spec.Id}";
                modeBtn.tooltip = spec.Tooltip;
                modeBtn.style.minWidth = 50f;
                modeBtn.style.height = 24f;
                modeBtn.style.marginLeft = i > 0 ? 2f : 0f;
                modeBtn.AddToClassList(EzChromeButtonClass);
                if (spec.IsDefaultSelected)
                    modeBtn.AddToClassList(EzChromeButtonActiveClass);
                modeBar.Add(modeBtn);
            }

            chrome.Add(frameLabel);
            chrome.Add(statusLabel);
            chrome.Add(abButton);
            chrome.Add(abDivider);
            chrome.Add(modeBar);
            return chrome;
        }

        /// <summary>USS <see cref="CorridorKeyUxmlPaths.LoadCorridorKeyStyleSheet"/> — EZ <c>view_mode_bar._button_style</c> / A/B.</summary>
        internal const string EzChromeButtonClass = "corridor-key-ez-chrome-btn";

        internal const string EzChromeButtonActiveClass = "corridor-key-ez-chrome-btn--active";

        /// <summary>Exclusive selection for parameter toggle rows (same USS as dual-viewer view modes).</summary>
        internal static void SetEzChromeToggleExclusive(Button selected, Button other)
        {
            selected.AddToClassList(EzChromeButtonActiveClass);
            other.RemoveFromClassList(EzChromeButtonActiveClass);
        }

        /// <summary>Labels, element ids, tooltips from EZ <c>view_mode_bar.py</c>; default COMP per EZ.</summary>
        internal static readonly ViewModeChromeSpec[] ViewModeChromeSpecs =
        {
            new ViewModeChromeSpec("input", "INPUT", false,
                "Original input footage (unprocessed)\n\nHotkey: F1"),
            new ViewModeChromeSpec("mask", "MASK", false,
                "Tracked mask — SAM2 segmentation output.\n"
                + "White = foreground, black = background.\n"
                + "This is the binary mask before MatAnyone2/VideoMaMa refinement.\n\n"
                + "Hotkey: F2"),
            new ViewModeChromeSpec("alpha", "ALPHA", false,
                "Alpha hint — generated by GVM, VideoMaMa, or MatAnyone2.\n"
                + "White = foreground, black = background.\n"
                + "This is the pre-inference guide used by CorridorKey.\n\n"
                + "Hotkey: F3"),
            new ViewModeChromeSpec("fg", "FG", false,
                "Foreground — subject with green spill removed.\n"
                + "Colors may look shifted; this is the despilled intermediate.\n\n"
                + "Hotkey: F4"),
            new ViewModeChromeSpec("matte", "MATTE", false,
                "Alpha matte — white = opaque, black = transparent.\n"
                + "Shows the AI's confidence in foreground vs background.\n\n"
                + "Hotkey: F5"),
            new ViewModeChromeSpec("comp", "COMP", true,
                "Composite — final keyed result over checkerboard.\n"
                + "Best preview of key quality with faithful colors.\n\n"
                + "Hotkey: F6"),
            new ViewModeChromeSpec("proc", "PROC", false,
                "Processed — production RGBA (straight, linear).\n"
                + "For Resolve, Premiere, and compositing tools.\n"
                + "Preview composites the stored image over black.\n"
                + "Final compositing should happen in your compositor of choice.\n\n"
                + "Hotkey: F7"),
        };

        const string TooltipAbWipe =
            "Toggle A/B wipe comparison (hotkey: A)\n\n"
            + "Overlays input (A) and current output (B) in one viewer\n"
            + "with a diagonal divider line.\n\n"
            + "Drag the center handle to slide the line.\n"
            + "Drag above or below the handle to rotate the angle.\n"
            + "Scroll wheel to slide the line (Shift+scroll for fine-grain).\n"
            + "Middle-click the line to reset to default.";

        internal readonly struct ViewModeChromeSpec
        {
            public readonly string Id;
            public readonly string Label;
            public readonly bool IsDefaultSelected;
            public readonly string Tooltip;

            public ViewModeChromeSpec(string id, string label, bool isDefaultSelected, string tooltip)
            {
                Id = id;
                Label = label;
                IsDefaultSelected = isDefaultSelected;
                Tooltip = tooltip;
            }
        }

        static VisualElement CreateViewerPane(string paneName, string title)
        {
            var col = new VisualElement { name = paneName };
            col.style.flexGrow = 0;
            col.style.flexShrink = 0;
            col.style.flexDirection = FlexDirection.Column;
            col.style.minWidth = HorizontalViewerIoSplitController.MinPaneWidthPx;

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
            tray.style.flexGrow = 0;
            tray.style.flexDirection = FlexDirection.Column;
            tray.style.height = 150f;
            tray.style.minHeight = VerticalViewerIoTraySplitController.MinIoTrayHeightPx;
            tray.style.marginBottom = 6f;
            tray.style.paddingTop = 6f;
            tray.style.paddingBottom = 6f;
            tray.style.backgroundColor = new Color(0.16f, 0.16f, 0.15f, 0.9f);
            tray.style.borderTopWidth = 1f;
            tray.style.borderBottomWidth = 1f;
            tray.style.borderLeftWidth = 1f;
            tray.style.borderRightWidth = 1f;
            tray.style.borderTopColor = new Color(0.3f, 0.3f, 0.28f);
            tray.style.borderBottomColor = new Color(0.3f, 0.3f, 0.28f);
            tray.style.borderLeftColor = new Color(0.3f, 0.3f, 0.28f);
            tray.style.borderRightColor = new Color(0.3f, 0.3f, 0.28f);

            var trayRow = new VisualElement { name = "io-tray-row" };
            trayRow.style.flexDirection = FlexDirection.Row;
            trayRow.style.flexGrow = 1;
            trayRow.style.minHeight = 56f;
            trayRow.style.alignItems = Align.Stretch;

            // Matches queue sidebar width so INPUT/EXPORTS line up with dual viewers (EZ uses overlay queue; we use flex).
            var queueSpacer = new VisualElement { name = "io-tray-queue-spacer" };
            queueSpacer.style.flexShrink = 0;
            queueSpacer.style.width = 0f;

            var splitRow = new VisualElement { name = "io-tray-split-row" };
            splitRow.style.flexDirection = FlexDirection.Row;
            splitRow.style.flexGrow = 1;
            splitRow.style.minWidth = 0;
            splitRow.style.alignItems = Align.Stretch;

            var inputSide = new VisualElement { name = "io-tray-input" };
            inputSide.style.flexShrink = 0;
            inputSide.style.flexDirection = FlexDirection.Column;
            inputSide.style.minWidth = 0f;

            var inputHeaderRow = new VisualElement { name = "io-tray-input-header-row" };
            inputHeaderRow.style.flexDirection = FlexDirection.Row;
            inputHeaderRow.style.alignItems = Align.Center;
            inputHeaderRow.style.justifyContent = Justify.SpaceBetween;
            inputHeaderRow.style.marginBottom = 2f;
            inputHeaderRow.style.flexShrink = 0;
            inputHeaderRow.style.minWidth = 0f;

            var inputTitle = new Label("INPUT (0)  —  import / clip list");
            inputTitle.name = "io-tray-input-title";
            inputTitle.style.fontSize = 11;
            inputTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            inputTitle.style.flexGrow = 1;
            inputTitle.style.flexShrink = 1;
            inputTitle.style.minWidth = 0f;
            inputTitle.style.whiteSpace = WhiteSpace.NoWrap;
            inputTitle.style.textOverflow = TextOverflow.Ellipsis;
            inputTitle.pickingMode = PickingMode.Ignore;

            var ioTrayInputActions = new VisualElement { name = "io-tray-input-header-actions" };
            ioTrayInputActions.style.flexDirection = FlexDirection.Row;
            ioTrayInputActions.style.alignItems = Align.Center;
            ioTrayInputActions.style.flexShrink = 0;

            var resetIo = new Button { text = "RESET IO" };
            resetIo.name = "io-tray-input-reset-io";
            resetIo.AddToClassList("corridor-key-io-tray-btn");
            resetIo.tooltip = "Clear INPUT / I-O tray bindings (EZ parity).";

            var addBtn = new Button { text = "ADD" };
            addBtn.name = "io-tray-input-add";
            addBtn.AddToClassList("corridor-key-io-tray-btn");
            addBtn.style.marginLeft = 6f;
            addBtn.tooltip = "Add clip or import (EZ parity).";

            ioTrayInputActions.Add(resetIo);
            ioTrayInputActions.Add(addBtn);
            inputHeaderRow.Add(inputTitle);
            inputHeaderRow.Add(ioTrayInputActions);
            inputSide.Add(inputHeaderRow);

            var exportSide = new VisualElement { name = "io-tray-exports" };
            exportSide.style.flexGrow = 1;
            exportSide.style.flexShrink = 0;
            exportSide.style.flexDirection = FlexDirection.Column;
            exportSide.style.minWidth = 0;
            // Visible seam vs INPUT (backgrounds are similar); aligns with OUTPUT column edge when split is synced.
            exportSide.style.borderLeftWidth = 2f;
            exportSide.style.borderLeftColor = new Color(0.62f, 0.58f, 0.38f, 1f);
            exportSide.style.paddingLeft = 6f;
            var exportTitle = new Label("EXPORTS (0)");
            exportTitle.style.fontSize = 11;
            exportTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            exportSide.Add(exportTitle);

            splitRow.Add(inputSide);
            splitRow.Add(exportSide);
            trayRow.Add(queueSpacer);
            trayRow.Add(splitRow);
            tray.Add(trayRow);

            var filesBar = new VisualElement { name = "io-files-bar" };
            filesBar.style.flexDirection = FlexDirection.Row;
            filesBar.style.flexShrink = 0;
            filesBar.style.flexGrow = 0;
            filesBar.style.height = IoTrayFilesBarHeightPx;
            filesBar.style.alignItems = Align.Stretch;
            filesBar.style.marginTop = 2f;
            // Full-bar fill so io-files-bar-queue-spacer (transparent) doesn't show the lighter io-tray surface behind it.
            filesBar.style.backgroundColor = new Color(0.12f, 0.12f, 0.11f, 1f);
            filesBar.style.borderTopWidth = 1f;
            filesBar.style.borderTopColor = new Color(0.28f, 0.28f, 0.26f);

            var filesQueueSpacer = new VisualElement { name = "io-files-bar-queue-spacer" };
            filesQueueSpacer.style.flexShrink = 0;
            filesQueueSpacer.style.width = 0f;

            var filesRest = new VisualElement { name = "io-files-bar-rest" };
            filesRest.style.flexDirection = FlexDirection.Row;
            filesRest.style.flexGrow = 1;
            filesRest.style.minWidth = 0;
            filesRest.style.alignItems = Align.Stretch;
            filesRest.style.justifyContent = Justify.Center;
            filesRest.style.backgroundColor = new Color(0.12f, 0.12f, 0.11f, 1f);
            filesRest.pickingMode = PickingMode.Position;

            var filesTitle = new Label("FILES");
            filesTitle.style.fontSize = 10;
            filesTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            filesTitle.style.color = new Color(0.62f, 0.62f, 0.58f);
            filesTitle.pickingMode = PickingMode.Ignore;
            filesRest.Add(filesTitle);

            filesBar.Add(filesQueueSpacer);
            filesBar.Add(filesRest);
            tray.Add(filesBar);

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
            left.style.minWidth = 0f;
            left.style.alignItems = Align.Center;

            // EZ order: progress bar first (left), then status copy; secondary grows to fill before RUN SELECTED.
            var inferenceLoad = new ProgressBar { title = " " };
            inferenceLoad.name = "status-inference-loading";
            inferenceLoad.value = 0f;
            inferenceLoad.lowValue = 0f;
            inferenceLoad.highValue = 100f;
            inferenceLoad.AddToClassList("corridor-key-status-progress");
            inferenceLoad.style.flexShrink = 0;
            inferenceLoad.style.minWidth = 40f;
            inferenceLoad.style.maxWidth = 120f;
            inferenceLoad.style.width = 120f;
            inferenceLoad.style.height = 5f;
            inferenceLoad.style.marginRight = 10f;
            inferenceLoad.tooltip = "Inference / queue progress (binds to backend).";

            var inferencePrimary = new Label("Ready");
            inferencePrimary.name = "status-inference-primary";
            inferencePrimary.style.fontSize = 11;
            inferencePrimary.style.unityFontStyleAndWeight = FontStyle.Bold;
            inferencePrimary.style.color = new Color(0.78f, 0.78f, 0.72f, 1f);
            inferencePrimary.style.flexShrink = 0;
            inferencePrimary.style.marginRight = 8f;

            var inferenceSecondary = new Label("Inference: idle — no job selected.");
            inferenceSecondary.name = "status-inference-secondary";
            inferenceSecondary.style.fontSize = 10;
            inferenceSecondary.style.color = new Color(0.58f, 0.58f, 0.52f, 1f);
            inferenceSecondary.style.flexGrow = 1;
            inferenceSecondary.style.flexShrink = 1;
            inferenceSecondary.style.minWidth = 0f;
            inferenceSecondary.style.whiteSpace = WhiteSpace.NoWrap;
            inferenceSecondary.style.textOverflow = TextOverflow.Ellipsis;

            left.Add(inferenceLoad);
            left.Add(inferencePrimary);
            left.Add(inferenceSecondary);

            var right = new VisualElement { name = "status-bar-right" };
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;
            right.style.flexShrink = 0;

            var runSelected = new Button { text = "RUN SELECTED" };
            runSelected.name = "status-run-selected";
            runSelected.AddToClassList("corridor-key-run-selected-btn");
            runSelected.tooltip = "Run the selected queue job or clip (binds to backend orchestration).";
            right.Add(runSelected);

            bar.Add(left);
            bar.Add(right);

            return bar;
        }
    }
}
