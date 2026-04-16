# A/B Scrubber Implementation Plan (Unity)

This plan defines the A/B scrubber (wipe) implementation for `EZ-CorridorKey_Unity`, aligned with EZ behavior where useful and intentionally improved where requested.

## Goals

- Add an interactive A/B wipe overlay to the Unity viewer workflow.
- Keep implementation modular so it can land as a focused commit/work chunk.
- Preserve parity intent with EZ while fixing known usability issues.

## Confirmed Requirements

- A/B mode toggles from the shared top chrome `A/B` button.
- Scrubber supports:
  - drag-to-slide
  - drag-to-rotate
  - line + handle visual overlay in viewer
- Rotation pivot is the **scrub line midpoint** (not frame center).
- Rotation is **free** by default (no sticky vertical/horizontal clamps).
- Holding `Shift` enables **snap to nearest axis** (vertical/horizontal only while held).
- Rotate affordance uses `rotation-icon.png` in:
  - `Editor/UI/Images/rotation-icon.png`
- Rotation cursor/icon should orient dynamically so its up-vector faces the scrub-line center point.
- Scrub handle should visually match EZ: small square badge with `A` on one side and `B` on the other.

## UX Behavior Details

- Distinct interaction zones:
  - center handle: move midpoint
  - rotate zone around handle/line: rotate
- Cursor signaling:
  - move zone uses move/slide cursor
  - rotate zone uses rotation affordance (custom icon path above)
- Discoverability:
  - tooltip/hint text indicates: drag to move, drag around handle to rotate, hold Shift to snap.

## Technical Plan

1. **State Model**
   - Add scrubber state object:
     - `enabled`
     - `midpointNormalized` (x, y)
     - `angleDeg`
     - `leftSourceMode`, `rightSourceMode`
   - Initialize to midpoint `(0.5, 0.5)` and default angle.

2. **UI Overlay Layer**
   - Add overlay container to viewer surface with:
     - line element
     - center handle badge (`A|B`)
     - rotate affordance element
   - Register pointer handlers for move/rotate gestures.

3. **Interaction Math**
   - Move updates midpoint in normalized coordinates.
   - Rotate computes angle around midpoint.
   - Normalize angle continuously (no hard clamp).
   - On `Shift`, snap to nearest axis orientation.

4. **Cursor + Icon**
   - Load `rotation-icon.png` from package assets.
   - Render/rotate affordance so icon orientation points toward midpoint.
   - Integrate cursor fallback when custom dynamic cursor is unavailable in current editor context.

5. **Frame Composition Path**
   - Prototype with CPU-side A/B compositing path suitable for parity iteration.
   - Respect existing decode/request-coalescing patterns and avoid duplicate requests while dragging.

6. **Styling**
   - Add USS classes for scrubber line, handle, hover, active, and snap states.
   - Keep dark-theme/EZ-adjacent aesthetics.

7. **Validation**
   - Validate against sample project data in:
     - `F:\CorridorKey\R\Projects\260415_062207_greenscreen-test-02`
     - `...\clips\greenscreen-test-02\Output`
   - Confirm behavior across INPUT/ALPHA/FG/MATTE/COMP/PROC comparisons.

## Implementation Phasing

- **Phase 1 (now):** overlay scaffold + interaction model + midpoint pivot + Shift snap + EZ-style `A|B` handle.
- **Phase 2:** dynamic custom rotation cursor polish + CPU compose wiring hardening + UX polish.
- **Phase 3:** optional enhancements (horizontal/diagonal presets, keyboard nudging, persistence polish).
