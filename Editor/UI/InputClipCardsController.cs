#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.UI
{
    /// <summary>INPUT tray clip cards (EZ IO tray parity baseline): discover clips, render cards, and emit selection.</summary>
    public sealed class InputClipCardsController
    {
        readonly VisualElement _inputSideRoot;
        readonly Label _header;
        readonly VisualElement _cardsRoot;
        readonly List<ClipCardItem> _items = new();
        string? _selectedClipRoot;

        sealed class ClipCardItem
        {
            public string ClipRoot = string.Empty;
            public string ClipName = string.Empty;
            public int FrameCount;
            public VisualElement? Card;
            public Image? Thumbnail;
        }

        public event Action<string>? ClipSelected;
        public event Action? SelectionCleared;

        public InputClipCardsController(VisualElement mainBodyRoot)
        {
            _inputSideRoot = mainBodyRoot.Q<VisualElement>("io-tray-input")
                             ?? throw new ArgumentException("Missing io-tray-input.", nameof(mainBodyRoot));
            _header = _inputSideRoot.Q<Label>("io-tray-input-title")
                      ?? throw new ArgumentException("Missing io-tray-input-title.", nameof(mainBodyRoot));
            _cardsRoot = _inputSideRoot.Q<VisualElement>("io-tray-input-cards")
                         ?? throw new ArgumentException("Missing io-tray-input-cards.", nameof(mainBodyRoot));
        }

        public void RefreshFromWorkspace(string workspaceRoot)
        {
            _cardsRoot.Clear();
            _items.Clear();

            var clipsDir = Path.Combine(workspaceRoot, "clips");
            if (!Directory.Exists(clipsDir))
            {
                _header.text = "INPUT (0)";
                _selectedClipRoot = null;
                SelectionCleared?.Invoke();
                return;
            }

            var clipDirs = Directory.GetDirectories(clipsDir);
            Array.Sort(clipDirs, StringComparer.OrdinalIgnoreCase);
            foreach (var clipRoot in clipDirs)
            {
                var clipName = Path.GetFileName(clipRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(clipName))
                    continue;
                var framesDir = Path.Combine(clipRoot, "Frames");
                var frameCount = 0;
                if (ClipPlateFramePaths.TryCollectSortedPlateFrames(framesDir, out var framePaths))
                    frameCount = framePaths.Count;

                var card = BuildCardElement(clipName, frameCount, framePaths: frameCount > 0 ? framePaths : null);
                var item = new ClipCardItem
                {
                    ClipRoot = clipRoot,
                    ClipName = clipName,
                    FrameCount = frameCount,
                    Card = card,
                    Thumbnail = card.Q<Image>("io-input-card-thumb")!,
                };
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    SetSelected(clipRoot);
                    ClipSelected?.Invoke(clipRoot);
                });
                card.RegisterCallback<ContextClickEvent>(_ =>
                {
                    SetSelected(clipRoot);
                    ShowContextMenuForClip(clipRoot);
                });

                _items.Add(item);
                _cardsRoot.Add(card);
            }

            _header.text = $"INPUT ({_items.Count})";
            if (_items.Count == 0)
            {
                _selectedClipRoot = null;
                SelectionCleared?.Invoke();
                return;
            }

            var selectedStillExists = !string.IsNullOrEmpty(_selectedClipRoot)
                                      && _items.Exists(i => string.Equals(i.ClipRoot, _selectedClipRoot, StringComparison.OrdinalIgnoreCase));
            if (!selectedStillExists)
            {
                var first = _items[0].ClipRoot;
                SetSelected(first);
                ClipSelected?.Invoke(first);
                return;
            }

            SetSelected(_selectedClipRoot);
        }

        public void SetSelected(string? clipRoot)
        {
            _selectedClipRoot = clipRoot;
            foreach (var item in _items)
            {
                var selected = string.Equals(item.ClipRoot, clipRoot, StringComparison.OrdinalIgnoreCase);
                item.Card.style.borderTopColor = selected ? new Color(1f, 0.95f, 0.1f, 1f) : new Color(0.18f, 0.17f, 0.1f, 1f);
                item.Card.style.borderRightColor = selected ? new Color(1f, 0.95f, 0.1f, 1f) : new Color(0.18f, 0.17f, 0.1f, 1f);
                item.Card.style.borderBottomColor = selected ? new Color(1f, 0.95f, 0.1f, 1f) : new Color(0.18f, 0.17f, 0.1f, 1f);
                item.Card.style.borderLeftColor = selected ? new Color(1f, 0.95f, 0.1f, 1f) : new Color(0.18f, 0.17f, 0.1f, 1f);
                item.Card.style.backgroundColor = selected ? new Color(0.14f, 0.14f, 0.09f, 1f) : new Color(0.1f, 0.1f, 0.07f, 1f);
            }
        }

        void ShowContextMenuForClip(string clipRoot)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename"), false, () => RenameClip(clipRoot));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Clip Folder"), false, () => RevealPath(clipRoot));

            var framesDir = Path.Combine(clipRoot, "Frames");
            if (Directory.Exists(framesDir))
                menu.AddItem(new GUIContent("Open Frames Folder"), false, () => RevealPath(framesDir));
            else
                menu.AddDisabledItem(new GUIContent("Open Frames Folder"));

            var alphaHintDir = Path.Combine(clipRoot, "AlphaHint");
            if (Directory.Exists(alphaHintDir))
                menu.AddItem(new GUIContent("Open Alpha Folder"), false, () => RevealPath(alphaHintDir));
            else
                menu.AddDisabledItem(new GUIContent("Open Alpha Folder"));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Clear Alpha"), false, () => ClearAlpha(clipRoot));
            menu.AddItem(new GUIContent("Clear All"), false, () => ClearAll(clipRoot));
            menu.AddItem(new GUIContent("Set Output Directory"), false, () => SetOutputDirectory(clipRoot));
            menu.AddItem(new GUIContent("Remove"), false, () => RemoveClip(clipRoot));
            menu.ShowAsContext();
        }

        void RefreshCurrentWorkspace()
        {
            if (!CorridorKeyWorkspacePaths.TryGetDefaultWorkspaceRoot(out var workspaceRoot))
                return;
            RefreshFromWorkspace(workspaceRoot);
            if (!string.IsNullOrEmpty(_selectedClipRoot))
                SetSelected(_selectedClipRoot);
        }

        static void RevealPath(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path))
                return;
            EditorUtility.RevealInFinder(path);
        }

        void RenameClip(string clipRoot)
        {
            var oldName = Path.GetFileName(clipRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(oldName))
                return;
            var parent = Path.GetDirectoryName(clipRoot);
            if (string.IsNullOrEmpty(parent))
                return;

            var chosen = EditorUtility.SaveFilePanel(
                "Rename Clip",
                parent,
                oldName,
                string.Empty);
            if (string.IsNullOrEmpty(chosen))
                return;
            var typed = Path.GetFileName(chosen);
            if (string.IsNullOrWhiteSpace(typed))
                return;
            typed = typed.Trim();
            if (string.Equals(typed, oldName, StringComparison.OrdinalIgnoreCase))
                return;

            var dst = Path.Combine(parent, typed);
            if (Directory.Exists(dst))
            {
                EditorUtility.DisplayDialog("Rename Clip", $"A clip named '{typed}' already exists.", "OK");
                return;
            }

            try
            {
                Directory.Move(clipRoot, dst);
                RefreshCurrentWorkspace();
                SetSelected(dst);
                ClipSelected?.Invoke(dst);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Rename Clip", $"Failed to rename clip:\n{e.Message}", "OK");
            }
        }

        void ClearAlpha(string clipRoot)
        {
            var alphaDir = Path.Combine(clipRoot, "AlphaHint");
            if (!Directory.Exists(alphaDir))
                return;
            if (!EditorUtility.DisplayDialog("Clear Alpha", "Remove all files under AlphaHint for this clip?", "Clear", "Cancel"))
                return;
            try
            {
                foreach (var f in Directory.GetFiles(alphaDir))
                    File.Delete(f);
                foreach (var d in Directory.GetDirectories(alphaDir))
                    Directory.Delete(d, recursive: true);
                RefreshCurrentWorkspace();
                SetSelected(clipRoot);
                ClipSelected?.Invoke(clipRoot);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Clear Alpha", $"Failed to clear AlphaHint:\n{e.Message}", "OK");
            }
        }

        void ClearAll(string clipRoot)
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear All",
                    "Remove generated data for this clip?\n\nThis clears AlphaHint and Output folders.",
                    "Clear",
                    "Cancel"))
                return;
            try
            {
                var alphaDir = Path.Combine(clipRoot, "AlphaHint");
                if (Directory.Exists(alphaDir))
                {
                    foreach (var f in Directory.GetFiles(alphaDir))
                        File.Delete(f);
                    foreach (var d in Directory.GetDirectories(alphaDir))
                        Directory.Delete(d, recursive: true);
                }

                var outputDir = Path.Combine(clipRoot, "Output");
                if (Directory.Exists(outputDir))
                {
                    foreach (var d in Directory.GetDirectories(outputDir))
                        Directory.Delete(d, recursive: true);
                    foreach (var f in Directory.GetFiles(outputDir))
                        File.Delete(f);
                }

                RefreshCurrentWorkspace();
                SetSelected(clipRoot);
                ClipSelected?.Invoke(clipRoot);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Clear All", $"Failed to clear generated data:\n{e.Message}", "OK");
            }
        }

        void SetOutputDirectory(string clipRoot)
        {
            var existing = ReadOutputOverride(clipRoot);
            var chosen = EditorUtility.OpenFolderPanel("Set Output Directory", string.IsNullOrEmpty(existing) ? clipRoot : existing, "");
            if (string.IsNullOrEmpty(chosen))
                return;
            try
            {
                WriteOutputOverride(clipRoot, chosen);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Set Output Directory", $"Failed to save output override:\n{e.Message}", "OK");
            }
        }

        static string ReadOutputOverride(string clipRoot)
        {
            var path = Path.Combine(clipRoot, "clip.json");
            if (!File.Exists(path))
                return string.Empty;
            try
            {
                var text = File.ReadAllText(path);
                const string key = "\"output_dir_override\"";
                var idx = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return string.Empty;
                var colon = text.IndexOf(':', idx);
                if (colon < 0)
                    return string.Empty;
                var q1 = text.IndexOf('"', colon + 1);
                if (q1 < 0)
                    return string.Empty;
                var q2 = text.IndexOf('"', q1 + 1);
                if (q2 < 0)
                    return string.Empty;
                return text.Substring(q1 + 1, q2 - q1 - 1);
            }
            catch
            {
                return string.Empty;
            }
        }

        static void WriteOutputOverride(string clipRoot, string outputDir)
        {
            var path = Path.Combine(clipRoot, "clip.json");
            var escaped = outputDir.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            string text;
            if (File.Exists(path))
            {
                text = File.ReadAllText(path);
                var key = "\"output_dir_override\"";
                var idx = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var colon = text.IndexOf(':', idx);
                    var q1 = text.IndexOf('"', colon + 1);
                    var q2 = text.IndexOf('"', q1 + 1);
                    if (colon > 0 && q1 > 0 && q2 > q1)
                    {
                        text = text.Substring(0, q1 + 1) + escaped + text.Substring(q2);
                    }
                    else
                    {
                        text = text.TrimEnd().TrimEnd('}');
                        text += $",\n  \"output_dir_override\": \"{escaped}\"\n}}\n";
                    }
                }
                else
                {
                    text = text.TrimEnd().TrimEnd('}');
                    if (!text.EndsWith("{", StringComparison.Ordinal))
                        text += ",";
                    text += $"\n  \"output_dir_override\": \"{escaped}\"\n}}\n";
                }
            }
            else
            {
                text = "{\n"
                       + $"  \"output_dir_override\": \"{escaped}\"\n"
                       + "}\n";
            }

            File.WriteAllText(path, text);
        }

        void RemoveClip(string clipRoot)
        {
            var clipName = Path.GetFileName(clipRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!EditorUtility.DisplayDialog(
                    "Remove Clip",
                    $"Remove clip '{clipName}' from this project?\n\nThis deletes the clip folder from disk.",
                    "Remove",
                    "Cancel"))
                return;

            try
            {
                Directory.Delete(clipRoot, recursive: true);
                _selectedClipRoot = null;
                RefreshCurrentWorkspace();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Remove Clip", $"Failed to remove clip:\n{e.Message}", "OK");
            }
        }

        static VisualElement BuildCardElement(string clipName, int frameCount, List<string>? framePaths)
        {
            var card = new VisualElement { name = "io-input-card" };
            card.style.width = 150f;
            card.style.height = 118f;
            card.style.marginRight = 6f;
            card.style.marginBottom = 6f;
            card.style.paddingLeft = 6f;
            card.style.paddingRight = 6f;
            card.style.paddingTop = 6f;
            card.style.paddingBottom = 6f;
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderTopColor = new Color(0.18f, 0.17f, 0.1f, 1f);
            card.style.borderRightColor = new Color(0.18f, 0.17f, 0.1f, 1f);
            card.style.borderBottomColor = new Color(0.18f, 0.17f, 0.1f, 1f);
            card.style.borderLeftColor = new Color(0.18f, 0.17f, 0.1f, 1f);
            card.style.backgroundColor = new Color(0.1f, 0.1f, 0.07f, 1f);
            card.style.flexDirection = FlexDirection.Column;

            var thumb = new Image { name = "io-input-card-thumb" };
            thumb.style.height = 68f;
            thumb.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            thumb.style.marginBottom = 4f;
            thumb.pickingMode = PickingMode.Ignore;
            if (framePaths is { Count: > 0 })
            {
                var tex = TextureFileLoader.LoadReadableFromFile(framePaths[0]);
                if (tex != null)
                    thumb.image = tex;
            }

            var name = new Label(clipName);
            name.style.fontSize = 10;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.whiteSpace = WhiteSpace.NoWrap;
            name.style.textOverflow = TextOverflow.Ellipsis;
            name.pickingMode = PickingMode.Ignore;

            var info = new Label($"{frameCount} frames");
            info.style.fontSize = 9;
            info.style.color = new Color(0.6f, 0.6f, 0.53f, 1f);
            info.pickingMode = PickingMode.Ignore;

            card.Add(thumb);
            card.Add(name);
            card.Add(info);
            return card;
        }
    }
}

