# Alpha generation UI — design notes (Unity port)

This document captures UX observations from the EZ-CorridorKey reference implementation and proposed improvements for the Unity package. It is **not** a commitment for v1; the first Unity pass should prioritize **functional parity** with EZ. Use this file when planning polish and onboarding.

---

## Audience and naming

Most VFX artists are fluent in **concepts** (greenscreen, roto, spill, holdout, alpha, premult) and tools they already use (Nuke, Resolve, After Effects). They are **not** assumed to recognize research model or repo names such as **GVM**, **BiRefNet**, **VideoMaMa**, **MatAnyone2**, or **SAM2** unless they follow ML matting papers or GitHub projects.

Exposing those names as primary labels is accurate for developers and power users but increases cognitive load for the majority. The underlying engines can stay the same; **labels and grouping** should speak in workflow language first, with technical names available on demand (tooltips, advanced panels, logs).

---

## What alpha generation actually is (user-facing wording)

In EZ, “alpha generation” produces an **alpha hint** sequence (`AlphaHint/`) so the clip can reach **READY** and the user can run **CorridorKey inference**. Short user-facing copy should avoid implying that GVM or BiRefNet *is* the final CorridorKey key by itself—they are **pre-pass** options that feed the same downstream inference step.

---

## Proposed high-level structure (target UX)

Present three **paths** at the top level, matching how artists think about the problem:

1. **Automatic** — “Generate alpha hint from the full clip; no painting.”  
   - Internally maps to one-click options such as **GVM** and/or **BiRefNet** (implementation parity with EZ).

2. **Guided (paint rough masks)** — “If automatic fails, paint rough foreground/background on key frames, then run tracking and a video matting step.”  
   - Internally maps to **SAM2 track** + **MatAnyone2 *or* VideoMaMa** (same backends as EZ).

3. **Import alpha** — “Bring mattes from another application.”  
   - Same as EZ **IMPORT ALPHA** (folder sequence or video).

This mirrors the EZ README’s Option A / B / C narrative but should be **visible in the UI hierarchy**, not only in documentation.

---

## Automatic path: BiRefNet variants

EZ exposes a **BiRefNet** button with a **dropdown of many model variants** (Matting, Portrait, General, HR, Lite, task-specific checkpoints, etc.). Advanced users benefit from switching variants when a shot misbehaves; most sessions should succeed with a **single sensible default** (EZ defaults to **Matting**).

**Unity direction:**

- Default: one primary action (e.g. “Generate alpha (automatic)”) with **Matting** or equivalent default behind the scenes.
- **Advanced / “Model…”** disclosure: reveal the full variant list (or a curated short list) with short plain-language hints (hair/detail, portrait, high resolution, faster/lightweight).
- Keep technical names (BiRefNet, Hugging Face repo IDs) in tooltips or logs, not as the only headline.

---

## Guided path: current EZ gaps (why this doc exists)

In EZ, the guided workflow is **sequential and conditional**, but the UI does not always read that way:

| Issue | Description |
|--------|-------------|
| **OR vs AND** | **Track Mask** is **required** before **MatAnyone2** or **VideoMaMa**. The latter two are **alternatives** (pick one matting backend after a good mask track), not three equal buttons like **GVM AUTO**. |
| **Visual parity** | **TRACK MASK**, **MATANYONE2**, and **VIDEOMAMA** are styled like peer actions; at a glance they can look like three more “one-click auto” options alongside **GVM AUTO**, rather than **step 1 → step 2 (choose A or B)**. |
| **Order** | The vertical order in the parameter panel does not fully encode the pipeline (track first; then one of two generators). New users must read tooltips or external docs to understand ordering. |
| **Frame 1 constraint** | MatAnyone2’s expectation (strokes on **frame 1**, then track, then run) is easy to miss if the user only skims the panel. |

**Unity direction:**

- Use a **wizard-style or stepped** sub-UI for guided mode: e.g. “1. Paint prompts → 2. Track masks → 3. Generate hint (MatAnyone2 *or* VideoMaMa)”.
- Visually group **MatAnyone2** and **VideoMaMa** under one parent (e.g. “Video matting (choose one)”) so the **OR** is obvious.
- Disable or hide step 3 until step 2 completes successfully; show clear status (“Mask track ready — choose generator”).
- Optionally collapse engine names under a single “Generate from track” label with an advanced dropdown for power users.

---

## Import path

EZ already behaves clearly: **IMPORT ALPHA** is a distinct third path. Preserve that separation in Unity; avoid burying import under automatic controls.

---

## Scope for the Unity package

- **First implementation pass:** Match EZ behavior and state machine (RAW → alpha hint → READY → inference) with acceptable labels; parity over perfection.
- **Follow-up:** Apply the structure and disclosure patterns above; improve onboarding strings; reduce reliance on model acronyms in primary UI.
- **Testing:** Use the same representative clips as EZ (clean greenscreen, difficult edges, guided shots) to validate that **guided** flows are discoverable without reading upstream repo names.

---

## Reference

- EZ-CorridorKey README — Quick Start “Generate Alpha Hint” (Options A/B/C): upstream repo [edenaion/EZ-CorridorKey](https://github.com/edenaion/EZ-CorridorKey).
- This package README: `README.md` in this folder.
