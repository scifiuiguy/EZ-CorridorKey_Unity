from __future__ import annotations

import json
import os
import sys
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
    if len(sys.argv) < 5:
        print(
            "usage: gvm_hint_runner.py <clip_root> <frames_dir> <alpha_dir> <result_json>",
            file=sys.stderr,
        )
        return 2

    clip_root, frames_dir, alpha_dir, result_json = sys.argv[1:5]
    clip_root = os.path.abspath(clip_root)
    frames_dir = os.path.abspath(frames_dir)
    alpha_dir = os.path.abspath(alpha_dir)
    result_json = os.path.abspath(result_json)

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

    device = _detect_device()
    if device == "cpu":
        _write_result(result_json, False, "GVM requires GPU; detected device=cpu")
        return 2

    try:

        from gvm_core.wrapper import GVMProcessor

        # Match EZ behavior: let gvm_core resolve local weights or HuggingFace fallback.
        gvm = GVMProcessor(device=device)
        gvm.process_sequence(
            input_path=frames_dir,
            output_dir=clip_root,
            num_frames_per_batch=1,
            decode_chunk_size=1,
            denoise_steps=1,
            mode="matte",
            write_video=False,
            direct_output_dir=alpha_dir,
            progress_callback=None,
        )
    except Exception as exc:  # noqa: BLE001
        _write_result(result_json, False, str(exc))
        return 1

    alpha_count = len([n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")])
    _write_result(result_json, True, f"GVM wrote {alpha_count} alpha hint frame(s) to {alpha_dir}", alpha_count=alpha_count)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

