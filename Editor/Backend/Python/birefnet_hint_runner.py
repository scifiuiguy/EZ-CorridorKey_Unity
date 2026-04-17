from __future__ import annotations

import sys
import json
import os
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
    """Status file for unity_bridge (download/load before first PNG)."""
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


def _load_input_frames(frames_dir: str) -> tuple[list, list]:
    import cv2
    import numpy as np

    names = sorted(
        [
            n
            for n in os.listdir(frames_dir)
            if n.lower().endswith((".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp"))
        ]
    )
    if not names:
        return [], []

    frames = []
    stems = []
    for name in names:
        path = os.path.join(frames_dir, name)
        img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
        if img is None:
            continue

        if img.ndim == 2:
            img = cv2.cvtColor(img, cv2.COLOR_GRAY2RGB)
        elif img.ndim == 3 and img.shape[2] >= 3:
            img = cv2.cvtColor(img[:, :, :3], cv2.COLOR_BGR2RGB)
        else:
            continue

        if img.dtype != np.uint8:
            # Normalize non-8-bit inputs to uint8 for BiRefNet.
            img = img.astype(np.float32)
            maxv = float(img.max()) if img.size > 0 else 1.0
            if maxv <= 1.0:
                img = img * 255.0
            img = np.clip(img, 0.0, 255.0).astype(np.uint8)

        frames.append(img)
        stems.append(os.path.splitext(name)[0])

    return frames, stems


def main() -> int:
    if len(sys.argv) < 7:
        print(
            "usage: birefnet_hint_runner.py <clip_root> <frames_dir> <alpha_dir> "
            "<result_json> <status_json> <usage>",
            file=sys.stderr,
        )
        return 2

    clip_root, frames_dir, alpha_dir, result_json, status_json, usage = sys.argv[1:7]
    usage = usage.strip() or "Matting"
    clip_root = os.path.abspath(clip_root)
    frames_dir = os.path.abspath(frames_dir)
    alpha_dir = os.path.abspath(alpha_dir)
    result_json = os.path.abspath(result_json)
    status_json = os.path.abspath(status_json)

    print(
        f"birefnet_hint_runner pid={os.getpid()} clip_root={clip_root} frames_dir={frames_dir} alpha_dir={alpha_dir}",
        file=sys.stderr,
        flush=True,
    )

    if os.path.normcase(os.path.normpath(frames_dir)) == os.path.normcase(
        os.path.normpath(alpha_dir)
    ):
        _write_result(
            result_json,
            False,
            "frames_dir and alpha_dir resolve to the same path; check bridge argv order.",
        )
        return 2

    if not os.path.isdir(clip_root):
        _write_result(result_json, False, f"clip_root not found: {clip_root}")
        return 2
    if not os.path.isdir(frames_dir):
        _write_result(result_json, False, f"frames_dir not found: {frames_dir}")
        return 2

    _write_status(status_json, "startup", "loading frame files from disk")
    frames, frame_stems = _load_input_frames(frames_dir)
    if not frames:
        _write_result(result_json, False, f"frames_dir has no readable image frames: {frames_dir}")
        return 2

    os.makedirs(alpha_dir, exist_ok=True)
    n_frames = len(frames)
    _write_status(status_json, "frames_ready", f"loaded {n_frames} frame(s); preparing model", total=n_frames)

    device = _detect_device()
    print(f"birefnet_hint_runner: device={device!r}", file=sys.stderr, flush=True)
    if device == "cpu":
        _write_result(result_json, False, "BiRefNet requires GPU; detected device=cpu")
        return 2

    def _on_status(msg: str) -> None:
        # Download / load phases from BiRefNetProcessor._ensure_loaded
        _write_status(status_json, "model", msg, total=n_frames)

    def _on_progress(_clip_name: str, cur: int, tot: int) -> None:
        _write_status(
            status_json,
            "inference",
            f"frame {cur}/{tot}",
            current=cur,
            total=tot,
        )

    try:
        _write_status(
            status_json,
            "import",
            "importing BiRefNet modules (torch/transformers; download only if checkpoint folder is empty)",
            total=n_frames,
        )
        from birefnet_checkpoint_diagnostics import abort_message_if_checkpoint_corrupt, resolve_paths

        try:
            _, _repo_name, local_dir, repo_id = resolve_paths(usage)
        except ValueError as exc:
            _write_result(result_json, False, str(exc))
            print(str(exc), file=sys.stderr)
            return 1

        bad = abort_message_if_checkpoint_corrupt(local_dir, repo_id)
        if bad:
            print(bad, file=sys.stderr)
            _write_result(result_json, False, bad)
            return 1

        from modules.BiRefNetModule import wrapper as _biref_wrapper

        _ck_dir = os.path.join(os.path.dirname(_biref_wrapper.__file__), "checkpoints", _repo_name)
        if not os.path.isdir(_ck_dir):
            _need_hf_snapshot = True
        else:
            _need_hf_snapshot = not any(
                f.endswith((".safetensors", ".bin"))
                for f in os.listdir(_ck_dir)
                if os.path.isfile(os.path.join(_ck_dir, f))
            )
        _write_status(
            status_json,
            "preflight",
            f"snapshot_download will run: {_need_hf_snapshot} | checkpoint dir={_ck_dir}",
            total=n_frames,
        )

        # When the snapshot is already on disk, force offline Hub behaviour so runs do not block on network.
        if not _need_hf_snapshot:
            os.environ["HF_HUB_OFFLINE"] = "1"

        from modules.BiRefNetModule.wrapper import BiRefNetProcessor

        _write_status(status_json, "init", f"BiRefNet variant={usage!r}", total=n_frames)

        try:
            import torch as _torch

            print(
                f"birefnet_hint_runner: torch={_torch.__version__} cuda={_torch.cuda.is_available()}",
                file=sys.stderr,
                flush=True,
            )
        except Exception:
            pass

        processor = BiRefNetProcessor(device=device, usage=usage)

        written = processor.process_frames(
            input_frames=frames,
            output_dir=alpha_dir,
            frame_names=frame_stems,
            progress_callback=_on_progress,
            on_status=_on_status,
            cancel_check=None,
            clip_name=os.path.basename(clip_root),
        )
    except Exception as exc:  # noqa: BLE001
        traceback.print_exc(file=sys.stderr)
        _write_result(result_json, False, str(exc))
        return 1

    png_names = [n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")] if os.path.isdir(alpha_dir) else []
    alpha_count = len(png_names)
    tmp_peer = alpha_dir + "._birefnet_tmp"
    tmp_left = (
        len([n for n in os.listdir(tmp_peer) if n.lower().endswith(".png")])
        if os.path.isdir(tmp_peer)
        else 0
    )

    if written != alpha_count:
        msg = (
            f"Inconsistent output: process_frames reported {written} frame(s) but found {alpha_count} .png in {alpha_dir!r}. "
            f"tmp peer leftover png: {tmp_left} in {tmp_peer!r}. "
            "Stale corridorkey_birefnet_result_*.json can show wrong ok if the bridge was killed before cleanup; always correlate request_id + updated_utc."
        )
        print(msg, file=sys.stderr, flush=True)
        _write_result(result_json, False, msg, alpha_count=alpha_count)
        return 1

    _write_result(result_json, True, f"BiRefNet wrote {alpha_count} alpha hint frame(s) to {alpha_dir}", alpha_count=alpha_count)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
