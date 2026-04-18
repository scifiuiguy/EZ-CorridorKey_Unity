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


def _debug(message: str) -> None:
    print(f"gvm_hint_runner: {message}", file=sys.stderr, flush=True)


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


def _detect_device() -> str:
    device = "cpu"
    try:
        import torch

        if torch.cuda.is_available():
            device = "cuda"
        elif hasattr(torch.backends, "mps") and torch.backends.mps.is_available():
            device = "mps"
    except Exception:
        device = "cpu"
    return device


def main() -> int:
    if len(sys.argv) < 6:
        print(
            "usage: gvm_hint_runner.py <clip_root> <frames_dir> <alpha_dir> <result_json> <status_json>",
            file=sys.stderr,
        )
        return 2

    clip_root, frames_dir, alpha_dir, result_json, status_json = sys.argv[1:6]
    _debug(f"args: clip_root={clip_root!r}, frames_dir={frames_dir!r}, alpha_dir={alpha_dir!r}, result_json={result_json!r}, status_json={status_json!r}")
    clip_root = os.path.abspath(clip_root)
    frames_dir = os.path.abspath(frames_dir)
    alpha_dir = os.path.abspath(alpha_dir)
    result_json = os.path.abspath(result_json)
    status_json = os.path.abspath(status_json)

    _debug(f"resolved: clip_root={clip_root!r}, frames_dir={frames_dir!r}, alpha_dir={alpha_dir!r}, result_json={result_json!r}, status_json={status_json!r}")

    if not os.path.isdir(clip_root):
        _write_result(result_json, False, f"clip_root not found: {clip_root}")
        return 2
    if not os.path.isdir(frames_dir):
        _write_result(result_json, False, f"frames_dir not found: {frames_dir}")
        return 2

    frame_files = [
        n
        for n in os.listdir(frames_dir)
        if n.lower().endswith((".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp"))
    ]
    if not frame_files:
        _write_result(result_json, False, f"frames_dir has no image frames: {frames_dir}")
        return 2

    os.makedirs(alpha_dir, exist_ok=True)

    _write_status(status_json, "startup", "preparing GVM runner", total=len(frame_files))
    device = _detect_device()
    _debug(f"detected device={device}")
    _write_status(status_json, "init", f"detected device={device}", total=len(frame_files))
    if device == "cpu":
        _write_result(result_json, False, "GVM requires GPU; detected device=cpu")
        return 2

    try:
        _write_status(status_json, "import", "importing GVM modules", total=len(frame_files))
        _debug("importing gvm_core.wrapper")
        from gvm_core.wrapper import GVMProcessor

        _write_status(status_json, "init", "initializing GVM processor", total=len(frame_files))
        _debug("initializing GVMProcessor")
        gvm = GVMProcessor(device=device)
        _debug("GVMProcessor initialized")

        def _on_progress(batch_idx: int, total_batches: int) -> None:
            _write_status(
                status_json,
                "inference",
                f"processing batch {batch_idx + 1}/{total_batches}",
                current=batch_idx + 1,
                total=total_batches,
            )

        _write_status(status_json, "inference", "starting GVM inference", total=len(frame_files))
        _debug("starting GVM process_sequence")
        gvm.process_sequence(
            input_path=frames_dir,
            output_dir=clip_root,
            num_frames_per_batch=1,
            decode_chunk_size=1,
            denoise_steps=1,
            mode="matte",
            write_video=False,
            direct_output_dir=alpha_dir,
            progress_callback=_on_progress,
        )
        _debug("GVM process_sequence returned")
    except Exception as exc:  # noqa: BLE001
        traceback.print_exc(file=sys.stderr)
        _write_result(result_json, False, str(exc))
        return 1

    alpha_count = len([n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")])
    _write_result(result_json, True, f"GVM wrote {alpha_count} alpha hint frame(s) to {alpha_dir}", alpha_count=alpha_count)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

