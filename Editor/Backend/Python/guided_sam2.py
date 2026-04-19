"""SAM2 dense mask track for Unity bridge (EZ parity with guided run_sam2_track)."""
from __future__ import annotations

import json
import os
import shutil
import traceback

import cv2
import numpy as np

try:
    from . import bridge_core
except ImportError:
    import bridge_core

_image_exts = {
    ".png",
    ".jpg",
    ".jpeg",
    ".exr",
    ".tif",
    ".tiff",
    ".bmp",
    ".webp",
}


def _emit_status(msg: str) -> None:
    bridge_core._emit(
        {
            "type": "log",
            "level": "INFO",
            "logger": "unity_bridge",
            "message": f"SAM2 track: {msg}",
        }
    )


def _sorted_frame_basenames(frames_dir: str) -> list[str]:
    names = [
        n
        for n in os.listdir(frames_dir)
        if os.path.isfile(os.path.join(frames_dir, n))
        and os.path.splitext(n)[1].lower() in _image_exts
    ]
    names.sort(key=lambda n: n.lower())
    return names


def _load_frame_rgb(path: str) -> np.ndarray | None:
    """Load plate frame as uint8 RGB (H, W, 3) for SAM2 / PIL."""
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img is None:
        return None
    if img.ndim == 2:
        gray = img
        if np.issubdtype(gray.dtype, np.floating):
            gray = np.clip(gray, 0.0, None)
            mx = float(gray.max()) if gray.size else 0.0
            if mx > 1.0 + 1e-6:
                gray = gray / (mx + 1e-8)
            gray_u8 = (np.clip(gray, 0.0, 1.0) * 255.0).astype(np.uint8)
        else:
            gray_u8 = np.clip(gray, 0, 255).astype(np.uint8)
        rgb = cv2.cvtColor(gray_u8, cv2.COLOR_GRAY2RGB)
    elif img.ndim == 3 and img.shape[2] == 4:
        bgr = cv2.cvtColor(img, cv2.COLOR_BGRA2BGR)
        rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    elif img.ndim == 3 and img.shape[2] == 3:
        rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    else:
        return None
    if np.issubdtype(rgb.dtype, np.floating):
        rgb = np.clip(rgb, 0.0, None)
        mx = float(rgb.max()) if rgb.size else 0.0
        if mx > 1.0 + 1e-6:
            rgb = rgb / (mx + 1e-8)
        rgb = (np.clip(rgb, 0.0, 1.0) * 255.0).astype(np.uint8)
    elif rgb.dtype != np.uint8:
        rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    return rgb


def _detect_device() -> str:
    try:
        import torch

        if torch.cuda.is_available():
            return "cuda"
        if hasattr(torch.backends, "mps") and torch.backends.mps.is_available():
            return "mps"
    except Exception:
        pass
    return "cpu"


def _run_guided_sam2_track(request_id: str, clip_root: str, frames_dir: str) -> None:
    cmd_name = "guided.sam2_track"
    try:
        clip_root = os.path.abspath((clip_root or "").strip())
        frames_dir = os.path.abspath((frames_dir or "").strip())

        if not clip_root or not frames_dir:
            msg = "clip_root and frames_dir are required"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isdir(clip_root):
            msg = f"clip_root not found: {clip_root}"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isdir(frames_dir):
            msg = f"frames_dir not found: {frames_dir}"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        basenames = _sorted_frame_basenames(frames_dir)
        if not basenames:
            msg = f"frames_dir has no image frames: {frames_dir}"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        frame_paths = [os.path.join(frames_dir, n) for n in basenames]
        named_frames: list[tuple[str, np.ndarray]] = []
        for fname, fpath in zip(basenames, frame_paths):
            rgb = _load_frame_rgb(fpath)
            if rgb is None:
                msg = f"Unreadable frame: {fpath}"
                bridge_core._emit({"type": "error", "message": msg})
                bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
                return
            named_frames.append((fname, rgb))

        start_index = 0
        allowed_indices = list(range(start_index, start_index + len(named_frames)))

        from backend.annotation_prompts import load_annotation_prompt_frames
        from backend.clip_state import MASK_TRACK_MANIFEST

        prompt_frames = load_annotation_prompt_frames(
            clip_root,
            allowed_indices=allowed_indices,
        )
        if not prompt_frames:
            msg = "No usable annotations for SAM2 tracking (annotations.json / strokes)"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        from sam2_tracker import PromptFrame, SAM2NotInstalledError, SAM2Tracker

        local_prompts = [
            PromptFrame(
                frame_index=p.frame_index - start_index,
                positive_points=list(p.positive_points),
                negative_points=list(p.negative_points),
                box=p.box,
            )
            for p in prompt_frames
        ]
        if not any(
            pr.positive_points or pr.box is not None for pr in local_prompts
        ):
            msg = "SAM2 tracking requires at least one non-empty foreground prompt"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        device = _detect_device()
        _emit_status(f"device={device}; loading SAM2…")

        tracker = SAM2Tracker(
            device=device,
            vos_optimized=False,
            offload_video_to_cpu=device.startswith("cuda"),
            offload_state_to_cpu=False,
        )

        last_emit = (-1, -1)

        def on_progress(cur: int, tot: int) -> None:
            nonlocal last_emit
            if tot <= 0:
                return
            step = max(1, tot // 20)
            if cur == tot or cur == 1 or cur - last_emit[0] >= step:
                last_emit = (cur, tot)
                bridge_core._emit(
                    {
                        "type": "progress",
                        "request_id": request_id,
                        "current": cur,
                        "total": tot,
                        "phase": "sam2_track",
                        "detail": f"{cur}/{tot}",
                    }
                )

        tracker.prepare(on_progress=on_progress, on_status=_emit_status)

        _emit_status("Running SAM2 tracker…")
        try:
            masks = tracker.track_video(
                [frame for _, frame in named_frames],
                local_prompts,
                on_progress=on_progress,
                on_status=_emit_status,
                check_cancel=None,
            )
        except SAM2NotInstalledError as exc:
            bridge_core._emit({"type": "error", "message": str(exc)})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))
            return
        except ValueError as exc:
            bridge_core._emit({"type": "error", "message": str(exc)})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))
            return

        mask_dir = os.path.join(clip_root, "VideoMamaMaskHint")
        os.makedirs(mask_dir, exist_ok=True)
        for fname in os.listdir(mask_dir):
            if fname.lower().endswith((".png", ".jpg", ".jpeg")):
                try:
                    os.remove(os.path.join(mask_dir, fname))
                except OSError:
                    pass

        stems: list[str] = []
        for (fname, _), mask in zip(named_frames, masks):
            stem = os.path.splitext(fname)[0]
            stems.append(stem)
            out_path = os.path.join(mask_dir, f"{stem}.png")
            if not cv2.imwrite(out_path, mask):
                msg = f"Failed to write mask: {out_path}"
                bridge_core._emit({"type": "error", "message": msg})
                bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
                return

        manifest_path = os.path.join(clip_root, MASK_TRACK_MANIFEST)
        payload = {
            "source": "sam2",
            "frame_stems": stems,
            "model_id": getattr(tracker, "model_id", None),
        }
        with open(manifest_path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=2)

        alpha_dir = os.path.join(clip_root, "AlphaHint")
        if os.path.isdir(alpha_dir):
            shutil.rmtree(alpha_dir, ignore_errors=True)

        summary = f"SAM2 track complete: {len(stems)} masks → VideoMamaMaskHint"
        _emit_status(summary)
        bridge_core._emit_done(cmd_name, request_id, ok=True, summary=summary)
    except Exception as exc:
        tb = traceback.format_exc()
        bridge_core._emit_log_lines(tb, logger="unity_bridge")
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))
