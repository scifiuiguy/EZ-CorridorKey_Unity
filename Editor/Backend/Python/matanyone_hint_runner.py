from __future__ import annotations

import json
import os
import sys
import traceback
from datetime import datetime, timezone


def _write_result(path: str, ok: bool, message: str, alpha_count: int = 0) -> None:
    payload = {
        "ok": bool(ok),
        "message": message,
        "alpha_count": int(alpha_count),
        "updated_utc": datetime.now(timezone.utc).isoformat(),
    }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)


def _write_status(
    path: str,
    stage: str,
    detail: str = "",
    current: int | None = None,
    total: int | None = None,
) -> None:
    payload: dict = {
        "stage": stage,
        "detail": detail,
        "updated_utc": datetime.now(timezone.utc).isoformat(),
    }
    if current is not None:
        payload["current"] = int(current)
    if total is not None:
        payload["total"] = int(total)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)


def _log(path: str, message: str) -> None:
    try:
        with open(path, "a", encoding="utf-8", buffering=1) as f:
            f.write(message + "\n")
    except Exception:
        pass


def _detect_device() -> str:
    try:
        import torch
        if torch.cuda.is_available():
            return "cuda"
    except Exception:
        pass
    return "cpu"


def main() -> int:
    if len(sys.argv) < 7:
        print(
            "usage: matanyone_hint_runner.py <clip_root> <frames_dir> <alpha_dir> <result_json> <status_json> <session_log>",
            file=sys.stderr,
        )
        return 2

    clip_root, frames_dir, alpha_dir, result_json, status_json, session_log = sys.argv[1:7]
    clip_root = os.path.abspath(clip_root)
    frames_dir = os.path.abspath(frames_dir)
    alpha_dir = os.path.abspath(alpha_dir)
    result_json = os.path.abspath(result_json)
    status_json = os.path.abspath(status_json)
    session_log = os.path.abspath(session_log)

    _log(session_log, f"[RUNNER] pid={os.getpid()}")
    _log(session_log, f"[ARGS] clip_root={clip_root}")
    _log(session_log, f"[ARGS] frames_dir={frames_dir}")
    _log(session_log, f"[ARGS] alpha_dir={alpha_dir}")

    if not os.path.isdir(clip_root):
        _write_result(result_json, False, f"clip_root not found: {clip_root}")
        return 2
    if not os.path.isdir(frames_dir):
        _write_result(result_json, False, f"frames_dir not found: {frames_dir}")
        return 2

    mask_dir = os.path.join(clip_root, "VideoMamaMaskHint")
    if not os.path.isdir(mask_dir):
        _write_result(result_json, False, f"VideoMamaMaskHint not found: {mask_dir}. Run TRACK MASK first.")
        return 2

    image_exts = (".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp")
    frame_files = sorted([n for n in os.listdir(frames_dir) if n.lower().endswith(image_exts)], key=str.lower)
    if not frame_files:
        _write_result(result_json, False, f"frames_dir has no supported image frames: {frames_dir}")
        return 2
    total = len(frame_files)

    _write_status(status_json, "import_cv2", "importing cv2...", total=total)
    _log(session_log, "[PHASE] import_cv2_begin")
    import cv2
    _log(session_log, "[PHASE] import_cv2_done")

    _write_status(status_json, "import_torch", "importing torch...", total=total)
    _log(session_log, "[PHASE] import_torch_begin")
    import torch  # noqa: F401
    _log(session_log, "[PHASE] import_torch_done")

    device = _detect_device()
    _write_status(status_json, "device", f"detected device={device}", total=total)
    if device != "cuda":
        _write_result(result_json, False, "MatAnyone2 requires a CUDA GPU, but CUDA is not available.")
        return 2

    try:
        if os.path.isdir(alpha_dir):
            for n in os.listdir(alpha_dir):
                if n.lower().endswith(".png"):
                    os.remove(os.path.join(alpha_dir, n))
        os.makedirs(alpha_dir, exist_ok=True)
    except Exception:
        pass

    _write_status(status_json, "load_frames", f"loading {total} frame(s) from disk...", total=total)
    input_frames = []
    frame_stems = []
    for i, frame_name in enumerate(frame_files):
        frame_path = os.path.join(frames_dir, frame_name)
        frame = cv2.imread(frame_path, cv2.IMREAD_COLOR)
        if frame is None:
            continue
        input_frames.append(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        frame_stems.append(os.path.splitext(frame_name)[0])
        if i % 10 == 0:
            _write_status(status_json, "load_frames", f"loaded {i + 1}/{total} frame(s)", current=i + 1, total=total)
    if not input_frames:
        _write_result(result_json, False, f"Could not load any frames from: {frames_dir}")
        return 2

    _write_status(status_json, "load_mask", "resolving first-frame mask...", total=total)
    first_stem = frame_stems[0]
    first_mask_path = None
    for ext in image_exts:
        candidate = os.path.join(mask_dir, first_stem + ext)
        if os.path.isfile(candidate):
            first_mask_path = candidate
            break
    if first_mask_path is None:
        mask_files = sorted([n for n in os.listdir(mask_dir) if n.lower().endswith(image_exts)], key=str.lower)
        if mask_files:
            first_mask_path = os.path.join(mask_dir, mask_files[0])
    if first_mask_path is None:
        _write_result(result_json, False, f"VideoMamaMaskHint has no mask images: {mask_dir}")
        return 2

    mask_frame = cv2.imread(first_mask_path, cv2.IMREAD_GRAYSCALE)
    if mask_frame is None:
        _write_result(result_json, False, f"Failed reading first-frame mask: {first_mask_path}")
        return 2
    _, mask_frame = cv2.threshold(mask_frame, 10, 255, cv2.THRESH_BINARY)
    h, w = input_frames[0].shape[:2]
    if mask_frame.shape[:2] != (h, w):
        mask_frame = cv2.resize(mask_frame, (w, h), interpolation=cv2.INTER_NEAREST)

    _write_status(status_json, "import_wrapper", "importing MatAnyone2 wrapper...", total=total)
    from modules.MatAnyone2Module.wrapper import MatAnyone2Processor

    clip_name = os.path.basename(clip_root.rstrip("\\/")) or "clip"

    def _on_progress(_clip_name: str, current: int, total_frames: int) -> None:
        _write_status(
            status_json,
            "inference",
            f"{int(current)}/{int(total_frames)} alpha PNG(s) written",
            current=int(current),
            total=int(total_frames),
        )

    def _on_status(msg: str) -> None:
        _write_status(status_json, "status", msg, total=total)
        _log(session_log, f"[STATUS] {msg}")

    try:
        _write_status(status_json, "run", "starting MatAnyone2 inference...", total=total)
        processor = MatAnyone2Processor(device="cuda")
        written = processor.process_frames(
            input_frames=input_frames,
            mask_frame=mask_frame,
            output_dir=alpha_dir,
            frame_names=frame_stems,
            progress_callback=_on_progress,
            on_status=_on_status,
            clip_name=clip_name,
        )
    except Exception as exc:  # noqa: BLE001
        traceback.print_exc(file=sys.stderr)
        _write_result(result_json, False, str(exc))
        return 1

    alpha_count = len([n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]) if os.path.isdir(alpha_dir) else 0
    _write_result(
        result_json,
        True,
        f"MatAnyone2 wrote {written} alpha hint frame(s) to {alpha_dir}",
        alpha_count=alpha_count,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
