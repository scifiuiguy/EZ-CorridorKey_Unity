# EZ-CorridorKey Unity

[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-black?logo=unity)](https://unity.com)
[![UPM](https://img.shields.io/badge/distribution-UPM-blue)](https://docs.unity3d.com/Manual/upm-ui.html)
[![License](https://img.shields.io/badge/license-CC%20BY--NC--SA%204.0-5a5a5a)](https://creativecommons.org/licenses/by-nc-sa/4.0/)

UPM package repository for the Unity port of EZ-CorridorKey workflows.

This package targets artists and technical users with a unified setup path:

- install package through Unity Package Manager
- run in-editor setup wizard for backend installation/configuration
- use the same wizard flow for both non-programmers and advanced users

**Cross-platform family:**
- Core backend: [CorridorKey-Source](https://github.com/nikopueringer/CorridorKey) by Niko Pueringer
- Desktop GUI reference: [EZ-CorridorKey](https://github.com/edenaion/EZ-CorridorKey) by Ed Zisk (edenaion)
- Unreal companion: `EZ-CorridorKey_Unreal` (sibling project)

---

## Contents

<details>
<summary><strong>THIS README</strong></summary>

- [Overview](#overview)
- [Installation](#installation)
- [Wizard Workflow](#wizard-workflow)
- [Advanced Mode](#advanced-mode)
- [Package Layout](#package-layout)
- [Roadmap](#roadmap)
- [Requirements](#requirements)
- [License and Attribution](#license-and-attribution)

</details>

<details>
<summary><strong>RELATED DOCUMENTATION</strong></summary>

- [EZ-CorridorKey README](../EZ-CorridorKey/README.md)
- [CorridorKey-Source README](../CorridorKey-Source/README.md)
- [CorridorKey LLM Handover](../CorridorKey-Source/docs/LLM_HANDOVER.md)

</details>

---

## Overview

EZ-CorridorKey Unity is a Unity Editor package that mirrors the core UX goals of EZ-CorridorKey:

- guided clip processing workflows
- queue-aware inference orchestration
- output inspection and export-friendly structure
- artist-first experience with technical escape hatches

Inference remains in the Python backend ecosystem; the Unity package owns UI, orchestration, and setup ergonomics.

---

## Installation

### UPM (recommended)

In Unity:

1. Open **Window > Package Manager**
2. Click **+ > Add package from git URL**
3. Paste:

```text
https://github.com/scifiuiguy/EZ-CorridorKey_Unity.git
```

4. Install package
5. Open the EZ-CorridorKey Unity setup window

### Contributor clone (optional)

```bash
git clone https://github.com/scifiuiguy/EZ-CorridorKey_Unity.git
```

No submodule is required for standard usage. Backend provisioning is handled by the package wizard.

---

## Wizard Workflow

The package is designed around a single setup wizard flow for everyone:

1. Detect platform and environment state
2. Choose backend install mode
3. Download/configure backend assets
4. Validate backend health
5. Persist settings and enable workflow UI

This keeps onboarding consistent for both artists and developers.

---

## Advanced Mode

For advanced users, the wizard supports a path override mode:

- **Use Existing Backend Path** -> point the package at a manually managed backend checkout/install

This is a directory path toggle, not a required submodule workflow.

### Backend bridge (manual until the wizard lands)

The Editor talks to EZ via `Editor/Backend/Python/unity_bridge.py` (stdio NDJSON). Save paths with **Tools > CorridorKey > Backend Settings…** (writes **EditorPrefs** on this machine — not Project Settings).

**Default install layout (wizard + empty EditorPrefs):** `CorridorKey/EZ-CorridorKey` under your user profile (e.g. Windows `C:\Users\<you>\CorridorKey\EZ-CorridorKey`), with Python at `.venv\Scripts\python.exe` (Windows) or `.venv/bin/python3` (macOS/Linux). The Backend Settings window shows these paths when nothing is saved yet; override if your checkout lives elsewhere.

| EditorPrefs key | Value |
| --- | --- |
| `CorridorKey.PythonExecutable` | Full path to EZ’s venv Python (on Windows, not a bare `python` or the Store stub under `WindowsApps`) |
| `CorridorKey.BackendWorkingDirectory` | EZ repo root (folder that contains `backend/`) |

Then open **Tools > CorridorKey > Open** for the main window and use **File > Run Backend Health Check** in that window to verify the bridge. Successful health runs `backend.ffmpeg_tools.discovery.validate_ffmpeg_install` inside EZ.

### Guided matting backend status

- `MatAnyone2` is integrated into the Unity bridge queue flow and has been validated in this workspace.
- `VideoMaMa` is integrated to the same bridge/runner pattern, but end-to-end validation is currently **untested locally** due to VideoMaMa model size (`~37 GB`) exceeding available disk budget on this machine.

---

## Package Layout

```text
EZ-CorridorKey_Unity/
├── package.json
├── Runtime/
├── Editor/
├── Documentation~/
├── Samples~/
└── README.md
```

---

## Roadmap

<details>
<summary><strong>0.0.1 - Foundation</strong></summary>

- Package scaffold and assembly layout
- Setup wizard skeleton
- Backend config persistence
- Initial test harness wiring

</details>

<details>
<summary><strong>0.1.x - Workflow Parity</strong></summary>

- Queue and run controls
- Parameter controls and preview flow
- Output browsing parity targets

</details>

<details>
<summary><strong>0.2.x - Hardening</strong></summary>

- Diagnostics and repair actions
- Resumable operations
- Better update/compatibility messaging

</details>

---

## Requirements

- Unity 6000.0+ (or validated LTS equivalent)
- CorridorKey-compatible backend environment
- Hardware matching CorridorKey backend recommendations for practical throughput

---

## License and Attribution

This Unity port builds on the CorridorKey ecosystem work by:

- Niko Pueringer / Corridor Digital (CorridorKey)
- Ed Zisk and contributors (EZ-CorridorKey desktop implementation)

Please respect upstream licenses and attribution requirements when redistributing derivatives.

