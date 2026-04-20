from __future__ import annotations

import json
import os
import sys
import traceback
from datetime import datetime, timezone
import numpy as np


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


def _videomama_checkpoints_ready(base_dir: str) -> bool:
    vm_root = os.path.join(base_dir, "VideoMaMaInferenceModule", "checkpoints")
    base_model = os.path.join(vm_root, "stable-video-diffusion-img2vid-xt")
    unet_model = os.path.join(vm_root, "VideoMaMa")
    return os.path.isdir(base_model) and os.path.isdir(unet_model)


def _ensure_videomama_models(base_dir: str, status_json: str, session_log: str, total: int) -> None:
    if _videomama_checkpoints_ready(base_dir):
        return

    _write_status(status_json, "model_setup", "VideoMaMa checkpoints missing; downloading...", total=total)
    _log(session_log, "[MODEL] VideoMaMa checkpoints missing. Running setup_models.download_model('videomama').")

    scripts_dir = os.path.join(base_dir, "scripts")
    if scripts_dir not in sys.path:
        sys.path.insert(0, scripts_dir)
    import setup_models  # type: ignore

    ok = bool(setup_models.download_model("videomama"))
    if not ok or not _videomama_checkpoints_ready(base_dir):
        raise RuntimeError(
            "VideoMaMa model download did not complete successfully. "
            "Expected checkpoints under VideoMaMaInferenceModule/checkpoints."
        )
    _write_status(status_json, "model_setup", "VideoMaMa checkpoints ready", total=total)
    _log(session_log, "[MODEL] VideoMaMa checkpoints ready.")


def main() -> int:
    if len(sys.argv) < 7:
        print(
            "usage: videomama_hint_runner.py <clip_root> <frames_dir> <alpha_dir> <result_json> <status_json> <session_log>",
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

    try:
        _write_status(status_json, "import_cv2", "importing cv2...", total=total)
        import cv2
        _write_status(status_json, "import_torch", "importing torch...", total=total)
        import torch  # noqa: F401

        device = _detect_device()
        _write_status(status_json, "device", f"detected device={device}", total=total)
        if device != "cuda":
            _write_result(result_json, False, "VideoMaMa requires a CUDA GPU, but CUDA is not available.")
            return 2

        _write_status(status_json, "load_frames", f"loading {total} frame(s) from disk...", total=total)
        input_frames = []
        input_stems = []
        for i, frame_name in enumerate(frame_files):
            frame_path = os.path.join(frames_dir, frame_name)
            frame = cv2.imread(frame_path, cv2.IMREAD_COLOR)
            if frame is None:
                continue
            input_frames.append(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            input_stems.append(os.path.splitext(frame_name)[0])
            if i % 10 == 0:
                _write_status(status_json, "load_frames", f"loaded {i + 1}/{total} frame(s)", current=i + 1, total=total)
        if not input_frames:
            _write_result(result_json, False, f"Could not load any frames from: {frames_dir}")
            return 2

        _write_status(status_json, "load_masks", "loading VideoMaMa mask frames...", total=total)
        mask_stems: dict[str, object] = {}
        mask_files = sorted([n for n in os.listdir(mask_dir) if n.lower().endswith(image_exts)], key=str.lower)
        for mf in mask_files:
            mpath = os.path.join(mask_dir, mf)
            m = cv2.imread(mpath, cv2.IMREAD_GRAYSCALE)
            if m is None:
                continue
            _, binary = cv2.threshold(m, 10, 255, cv2.THRESH_BINARY)
            mask_stems[os.path.splitext(mf)[0]] = binary

        h0, w0 = input_frames[0].shape[:2]
        mask_frames = []
        for stem in input_stems:
            if stem in mask_stems:
                mask = mask_stems[stem]
                if mask.shape[:2] != (h0, w0):
                    mask = cv2.resize(mask, (w0, h0), interpolation=cv2.INTER_NEAREST)
                mask_frames.append(mask)
            else:
                mask_frames.append(np.zeros((h0, w0), dtype=np.uint8))

        _write_status(status_json, "import_module", "importing VideoMaMa module...", total=total)
        base_dir = os.getcwd()
        vm_path = os.path.join(base_dir, "VideoMaMaInferenceModule")
        if vm_path not in sys.path:
            sys.path.insert(0, vm_path)
        from VideoMaMaInferenceModule.inference import load_videomama_model, run_inference

        _ensure_videomama_models(base_dir, status_json, session_log, total)

        try:
            if os.path.isdir(alpha_dir):
                for n in os.listdir(alpha_dir):
                    if n.lower().endswith(".png"):
                        os.remove(os.path.join(alpha_dir, n))
            os.makedirs(alpha_dir, exist_ok=True)
        except Exception:
            pass

        _write_status(status_json, "load_model", "loading VideoMaMa model...", total=total)
        pipeline = load_videomama_model(device="cuda")

        written = 0
        chunk_size = 16

        def _on_status(msg: str) -> None:
            _write_status(status_json, "inference_status", msg, current=written, total=total)
            _log(session_log, f"[STATUS] {msg}")

        _write_status(status_json, "run", "starting VideoMaMa inference...", total=total)
        for chunk_frames in run_inference(
            pipeline,
            input_frames,
            mask_frames,
            chunk_size=chunk_size,
            on_status=_on_status,
        ):
            for frame in chunk_frames:
                if written >= len(input_stems):
                    break
                out_path = os.path.join(alpha_dir, f"{input_stems[written]}.png")
                if not cv2.imwrite(out_path, cv2.cvtColor(frame, cv2.COLOR_RGB2BGR)):
                    raise RuntimeError(f"Failed to write alpha frame: {out_path}")
                written += 1
            _write_status(status_json, "inference", f"{written}/{total} alpha PNG(s) written", current=written, total=total)

        alpha_count = len([n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]) if os.path.isdir(alpha_dir) else 0
        _write_result(
            result_json,
            True,
            f"VideoMaMa wrote {alpha_count} alpha hint frame(s) to {alpha_dir}",
            alpha_count=alpha_count,
        )
        return 0
    except Exception as exc:  # noqa: BLE001
        traceback.print_exc(file=sys.stderr)
        _write_result(result_json, False, str(exc))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
