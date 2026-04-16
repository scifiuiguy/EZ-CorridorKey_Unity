# Unity port scaffolding plan (EZ-CorridorKey → C# + UI Toolkit)

This document turns the high-level port strategy into a **checklist**: folder layout, core types, UI regions, backend bridge, and delivery phases. It assumes **v1 keeps inference and alpha generators in the existing Python stack** (EZ/CorridorKey); Unity owns **layout, state, orchestration, and UX**.

Companion docs: [`README.md`](README.md), [`ALPHA_GENERATION_UI_IMPROVEMENTS.md`](ALPHA_GENERATION_UI_IMPROVEMENTS.md).

---

## Assumptions

- **UPM package** with `Editor/` + `Runtime/` + `.asmdef` split (already started in this repo).
- **UI Toolkit** for the custom Editor window; IMGUI only if unavoidable (e.g. legacy integration).
- **Backend** is contacted via a thin **C# bridge** (subprocess with JSON lines, local HTTP, or named pipes—pick one transport and stick to it).
- **Parity target** is EZ-CorridorKey behavior and project layout on disk, not a rewrite of PyTorch in C#.

---

## Recommended folder tree

```
EZ-CorridorKey_Unity/
├── package.json
├── README.md
├── SCAFFOLDING.md
├── ALPHA_GENERATION_UI_IMPROVEMENTS.md
├── Runtime/
│   ├── CorridorKey.Runtime.asmdef
│   ├── Core/
│   │   ├── ClipState.cs                 # Extracting | Raw | Masked | Ready | Complete | Error
│   │   ├── ClipEntry.cs                 # name, paths, frame count, in/out, state
│   │   ├── ProjectContext.cs            # project root, clips root, session file path
│   │   ├── JobType.cs                   # Extract | Gvm | Birefnet | TrackMask | … | Inference
│   │   └── JobDescriptor.cs             # id, type, clip id, params handle
│   ├── Backend/
│   │   ├── IBackendClient.cs            # facade for all remote operations
│   │   ├── BackendOptions.cs            # python exe, working dir, timeout, env
│   │   ├── BackendEvent.cs              # discriminated union or event type + typed payloads
│   │   └── Payloads/                    # JSON models: health, progress, clip update, log line
│   └── Services/
│       └── ClipScanService.cs           # optional: C#-only directory scan for UI shell
├── Editor/
│   ├── CorridorKey.Editor.asmdef
│   ├── CorridorKeyWindow.cs             # [MenuItem] entry, root VisualElement host
│   ├── Backend/
│   │   ├── ProcessBackendClient.cs      # IBackendClient implementation v1
│   │   └── BackendLocator.cs            # resolve python / venv from wizard settings
│   ├── Settings/
│   │   └── CorridorKeySettings.cs     # EditorPrefs / SessionState keys for backend paths
│   ├── UI/
│   │   ├── UXML/
│   │   │   ├── CorridorKeyWindow.uxml
│   │   │   └── (optional) partials: QueuePanel.uxml, ViewerPanel.uxml, AlphaPanel.uxml
│   │   ├── USS/
│   │   │   └── CorridorKey.uss
│   │   └── Presenters/                 # bind views ↔ view models
│   │       ├── QueuePresenter.cs
│   │       ├── ViewerPresenter.cs      # scrubber, dual pane placeholders
│   │       ├── AlphaGenerationPresenter.cs
│   │       ├── InferencePresenter.cs
│   │       └── StatusBarPresenter.cs
│   ├── ViewModels/
│   │   ├── CorridorKeySessionVm.cs     # single place: selected clip, running job, log buffer
│   │   └── ClipRowVm.cs
│   └── Wizard/                         # when ready: setup UI for backend path
│       └── BackendSetupWizard.cs
```

Adjust names to match your namespaces (`CorridorKey.Editor`, `CorridorKey`).

---

## Core types (minimum viable)

| Type | Responsibility |
|------|----------------|
| `ClipState` | Mirror EZ `ClipEntry` lifecycle for gating buttons and channels. |
| `ClipEntry` | Paths to source video, frames folder, output dirs; optional in/out range. |
| `ProjectContext` | Root that contains `Projects/` or clip folders per EZ conventions. |
| `JobType` + `JobDescriptor` | What the backend is doing; drives status bar and cancel. |
| `IBackendClient` | All side effects: import, extract, alpha hint, inference, cancel, health. |
| `BackendEvent` stream | Progress %, phase text, stderr tail, structured errors, clip state updates. |

Keep **one** session/view-model object (`CorridorKeySessionVm`) that presenters subscribe to so UXML code-behind stays thin.

---

## Mapping EZ Python source to this scaffold (for EZ contributors)

Unity does **not** recreate most EZ files line-for-line in C#. The **Python backend remains EZ** (see README); C# mirrors **roles** (UI shell, state, bridge). Use this table to find the **nearest equivalent** when reading or reviewing PRs.

### Expectations: 1:1 files vs conceptual parity

| Expectation | Reality |
|-------------|---------|
| One EZ `.py` file → one C# file | Rare. Qt `MainWindow` + mixins map to **one window + several presenters + one session VM**. |
| All `backend/service/` logic reimplemented in C# | **No** for v1. It stays Python; C# exposes `IBackendClient` + payload types. |
| EZ developers can still navigate | **Yes**, if you use this table and **file-header parity comments** (below). |

### EZ repository → proposed C# location

Paths are relative to the **EZ-CorridorKey** repo root (same layout as [edenaion/EZ-CorridorKey](https://github.com/edenaion/EZ-CorridorKey)).

| EZ (Python) | Nearest C# scaffold | Notes |
|-------------|---------------------|--------|
| `backend/clip_state.py` (`ClipState`, `ClipEntry`, transitions) | `Runtime/Core/ClipState.cs`, `ClipEntry.cs` | Mirror enum and fields needed for UI gating; disk layout rules stay documented in comments pointing back to Python. |
| `backend/clip_scanner.py`, project scan helpers | `Runtime/Services/ClipScanService.cs` (optional), `ProjectContext.cs` | C# may only **enumerate** projects/clips for the shell until the bridge is live; full semantics match EZ scanner. |
| `backend/project.py` (paths, session, in/out) | `ProjectContext.cs`, settings payload types | Keep path conventions identical to EZ on disk. |
| `backend/service/core.py` (`CorridorKeyService` + mixins) | `Runtime/Backend/IBackendClient.cs` + `Backend/*` payload types | **Not a port**—the **contract** Unity uses to talk to Python. Implementation remains EZ. |
| `backend/service/*.py` (frame ops, inference, pipelines) | Same bridge payload types + opaque JSON params where needed | Add typed C# payloads as you stabilize the protocol. |
| `backend/ffmpeg_tools/**` | No dedicated C# tree in v1 | Extraction/stitching runs inside Python; Unity triggers jobs via `IBackendClient`. |
| `ui/main_window.py` | `Editor/CorridorKeyWindow.cs` | Menu host + root `VisualElement`; no single mixin file—see presenters. |
| `ui/main_window_mixins/*.py` (clip, inference, export, worker, …) | `CorridorKeySessionVm.cs` + focused **Presenters** | Logic splits by **feature** (queue vs inference vs export), not by EZ’s mixin file boundaries. |
| `ui/models/clip_model.py` | `ClipRowVm.cs`, queue binding in `QueuePresenter.cs` | List selection + signals → C# events / callbacks. |
| `ui/workers/gpu_job_worker.py`, job snapshots | `JobType.cs`, `JobDescriptor.cs`, `BackendEvent.cs`, session VM | Job IDs, progress, cancel: same **concept**; threading model differs (Unity main thread + async). |
| `ui/workers/extract_worker.py` | Extract calls on `IBackendClient` + progress in `StatusBarPresenter` | |
| `ui/widgets/parameter_panel.py` | `AlphaGenerationPresenter.cs`, `InferencePresenter.cs`, output section | EZ’s single large panel → **multiple** presenters; optional shared base class. |
| `ui/widgets/status_bar.py` | `StatusBarPresenter.cs` | Run / resume / extraction mode text, progress. |
| Dual viewer / scrubber widgets under `ui/` | `ViewerPresenter.cs` + UXML | Bind to textures or proxy images per phase. |
| `ui/app.py` (QApplication, global setup) | Unity `Editor` entry + `CorridorKeyWindow` | No Qt; Editor lifecycle replaces `QApplication`. |
| Wizard / first-run backend setup | `Editor/Wizard/BackendSetupWizard.cs`, `BackendLocator.cs`, `CorridorKeySettings.cs` | Installs or points at **EZ** checkout + venv (see README), not “Niko-only” unless advanced override. |

### Python bridge script (expected EZ-side anchor)

| Responsibility | Typical EZ location |
|----------------|---------------------|
| Entry invoked by Unity (`python …`) | New small module, e.g. `scripts/unity_bridge.py`, or extend existing CLI under `scripts/` |
| Calls into `CorridorKeyService` | `backend/service/core.py` |

Document the chosen script path in `IBackendClient` XML comments or `Backend/README.md` once added.

### Conventions for discoverability

1. **File headers** (C#): one line `// EZ parity: backend/clip_state.py (ClipState, ClipEntry)` where non-obvious.
2. **Folder readme** (optional): `Editor/UI/README.md` listing presenter ↔ `parameter_panel.py` sections.
3. **Grep-friendly names**: e.g. `GpuJob` or `InferenceJob` in C# can align with EZ `JobType` / worker naming where it does not conflict with Unity APIs.

### What has no C# equivalent by design

| EZ area | Why |
|---------|-----|
| PyTorch models, CUDA, `torch.compile` | Stay in Python process. |
| Full FFmpeg CLI construction | `backend/ffmpeg_tools/` |
| SAM2 / GVM / BiRefNet / VideoMaMa / MatAnyone2 Python modules | Same; Unity only sends **commands and params**. |

---

## UI Toolkit layout (regions to match EZ)

Map EZ’s main areas to named root elements in `CorridorKeyWindow.uxml`:

1. **Brand / menu bar** — optional in v1; project name + backend status is enough.
2. **Queue / I-O tray** — `ListView` or `ScrollView` of clips; selection changes `ClipEntry`.
3. **Dual viewer** — two hosts (`VisualElement` + `Image` or `RenderTexture` binding); **v1** can show placeholders or load preview PNGs from disk if backend writes them.
4. **Parameter stack** — collapsible sections:
   - Alpha generation (auto / guided / import — see `ALPHA_GENERATION_UI_IMPROVEMENTS.md`)
   - Inference (color space, despill, refiner, etc., as you add parity)
   - Output
5. **Status bar** — progress, primary action button text (`RUN EXTRACTION` vs `RUN INFERENCE` vs `RUN SELECTED`), cancel.

Use **presenter classes** per region; avoid one 2000-line `CorridorKeyWindow`.

---

## Backend bridge contract

Define a **small, versioned protocol** (JSON) for:

- `health` — Python version, torch CUDA, ffmpeg path, CorridorKey model present.
- `import_clip` / `extract` — clip id, paths.
- `run_alpha` — mode + engine-specific params (opaque JSON blob acceptable at first).
- `run_inference` — params + frame range.
- `cancel` — job id.
- **Streaming**: `log_line`, `progress`, `clip_state`, `job_done`, `error`.

The Python side can be a **thin adapter script** in EZ or a dedicated `unity_bridge.py` that calls existing `CorridorKeyService` APIs—implementation detail, but the C# interface should stay stable.

---

## Logging: EZ debug console vs Unity Editor Console

EZ’s **F12 debug console** is a **Qt window** wired to the same Python logging stack as the rest of the app. We **do not** recreate that UI in UI Toolkit for v1.

### What we do instead

Forward backend log lines into the **Unity Editor Console** (`Window > General > Console`) so developers get a **familiar, filterable** log with severity styling (info / warning / error), without maintaining a second custom console window.

### How lines reach C# (not “polling the EZ console”)

Unity is **not** scraping or polling EZ’s on-screen console widget. Typical options:

| Source | Mechanism | Notes |
|--------|-----------|--------|
| **Bridge stream** | Python bridge emits JSON payloads such as `{ "type": "log", "level": "...", "logger": "...", "message": "..." }` on stdout or a socket | Same process that runs jobs; `IBackendClient` read loop parses each line and maps to `Debug.Log*` (see below). **Event-driven** (read blocks or async read), not a busy poll. |
| **Log file tail** (optional) | EZ already writes files under `logs/backend/` (see EZ README). Unity can **tail** new bytes when the bridge is not the only writer, or for debugging “what EZ wrote when Unity wasn’t attached.” | Use `FileSystemWatcher` + incremental read, or periodic **low-frequency** tail—still not polling the Qt console. |

Prefer **bridge-embedded log payloads** for low latency and a single channel; add file tail only if needed.

### Mapping to Unity Console

| Python / EZ log level (concept) | Unity API | Console appearance |
|---------------------------------|------------|----------------------|
| DEBUG, INFO | `Debug.Log` | White default log |
| WARNING | `Debug.LogWarning` | Yellow warning |
| ERROR, CRITICAL | `Debug.LogError` | Red error |

Implementation detail (choose one and stick to it):

- **Prefix** every line with a fixed tag, e.g. `[CorridorKey]`, so users can filter the Console search field.
- Optionally include the original logger name: `[CorridorKey][backend.ffmpeg_tools.extraction] …`.

Rich-text color in the Unity Console is limited; **severity mapping** (`Log` / `LogWarning` / `LogError`) is the reliable way to get “color-coded” behavior most Unity devs expect.

### Code placement

- Add a small **`BackendLogForwarder`** (or private methods on `ProcessBackendClient`) that converts each parsed `log` payload into the correct `Debug.Log*` call on the **main thread** (same rules as other UI-facing backend events).
- `IBackendClient` can expose an optional **`LogReceived`** event if you want the custom Editor window to show a **short** recent tail; full history stays in the Unity Console.

### Non-goals

- **No** full replica of EZ’s F12 panel (filters, level combo, custom styling) in UXML for the first pass.
- **No** busy-wait loop “constantly checking” anything—use **streaming reads** or **event-driven** file tail.

---

## Phased delivery

| Phase | Goal | Exit criteria |
|-------|------|----------------|
| **A** | Editor window + empty layout + open folder | UXML loads; no crash. |
| **B** | `IBackendClient` + health check + settings | Status shows OK/fail from Python. |
| **C** | Clip list from disk + select clip | Matches EZ folder layout or documented subset. |
| **D** | Extract frames job + progress | Clip reaches `Raw`; logs visible. |
| **E** | One alpha path (e.g. BiRefNet default only) | Clip reaches `Ready`. |
| **F** | Inference run | Clip reaches `Complete`; outputs path shown. |
| **G** | Remaining alpha modes + parity params | Feature matrix vs EZ. |
| **H** | UX polish | `ALPHA_GENERATION_UI_IMPROVEMENTS.md` guided grouping, shortcuts. |

---

## Testing harness

- Consume this package from a **local test Unity project** via `file:` in `Packages/manifest.json` (see main README).
- **Edit Mode tests** (optional): JSON payload parse, `ClipState` transitions, path normalization.
- **Manual script**: same short clip as EZ (RAW → alpha → READY → inference).

---

## Explicit non-goals for early phases

- Native CUDA inference in Unity.
- Full EXR viewer in-editor (use proxies or external viewer until needed).
- Pixel-perfect USS clone of EZ before state machine works.
- Pixel-perfect clone of EZ’s **F12** Qt debug console; use **Unity Console** + log forwarding instead (see [Logging: EZ debug console vs Unity Editor Console](#logging-ez-debug-console-vs-unity-editor-console)).

---

## File checklist (create as you implement)

- [x] `Runtime/Core/ClipState.cs`
- [x] `Runtime/Core/ClipEntry.cs`
- [x] `Runtime/Core/ProjectContext.cs`
- [x] `Runtime/Core/JobType.cs`
- [x] `Runtime/Core/JobDescriptor.cs`
- [x] `Runtime/Backend/IBackendClient.cs`
- [x] `Runtime/Backend/BackendOptions.cs`
- [x] `Runtime/Backend/BackendEvent.cs`
- [x] `Runtime/Backend/Payloads/*.cs`
- [x] `Runtime/Services/ClipScanService.cs`
- [x] `Editor/Backend/ProcessBackendClient.cs` (stub — wire transport next)
- [x] `Editor/Backend/BackendLocator.cs`
- [x] `Editor/Backend/BackendLogForwarder.cs`
- [x] `Editor/CorridorKeyWindow.cs` + `Editor/UI/UXML/CorridorKeyWindow.uxml` + `Editor/UI/USS/CorridorKey.uss`
- [x] `Editor/UI/CorridorKeyUxmlPaths.cs`
- [x] `Editor/ViewModels/CorridorKeySessionVm.cs` + `ClipRowVm.cs`
- [x] Presenters: `QueuePresenter`, `StatusBarPresenter`, `AlphaGenerationPresenter`, `InferencePresenter`, `ViewerPresenter`
- [x] `Editor/Settings/CorridorKeySettings.cs`
- [x] `Editor/Wizard/BackendSetupWizard.cs` (placeholder)
- [x] Bridge Python script + NDJSON stdio protocol (see [Backend bridge contract](#backend-bridge-contract)) — `Editor/Backend/Python/unity_bridge.py` + `ProcessBackendClient`

Update this checklist as files land.
