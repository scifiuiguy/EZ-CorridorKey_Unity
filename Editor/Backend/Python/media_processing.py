"""
Media processing commands for Unity ↔ EZ-CorridorKey stdio bridge.

Handles video frame extraction and clip management.
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
from datetime import datetime, timezone

try:
    from . import bridge_core
except ImportError:
    import bridge_core


def _probe_total_frames(input_path: str) -> int:
    ffprobe = shutil.which("ffprobe")
    if not ffprobe:
        return 0
    try:
        proc = subprocess.run(
            [
                ffprobe,
                "-v",
                "error",
                "-count_frames",
                "-select_streams",
                "v:0",
                "-show_entries",
                "stream=nb_read_frames,nb_frames",
                "-of",
                "default=nokey=1:noprint_wrappers=1",
                input_path,
            ],
            capture_output=True,
            text=True,
            timeout=20,
            check=False,
        )
        vals = [ln.strip() for ln in (proc.stdout or "").splitlines() if ln.strip().isdigit()]
        if not vals:
            return 0
        return max(int(v) for v in vals)
    except Exception:
        return 0


def _write_clip_json(clip_dir: str, source_path: str, frames_dir: str, frame_count: int) -> None:
    clip_name = os.path.basename(clip_dir.rstrip("\\/"))
    payload = {
        "clip_name": clip_name,
        "source_path": source_path,
        "frames_dir": frames_dir,
        "frame_count": frame_count,
        "status": "frames_extracted",
        "updated_utc": datetime.now(timezone.utc).isoformat(),
    }
    clip_json = os.path.join(clip_dir, "clip.json")
    with open(clip_json, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)


def _update_clip_json_with_alpha(clip_dir: str, alpha_dir: str, alpha_count: int, status: str) -> None:
    clip_json = os.path.join(clip_dir, "clip.json")
    payload: dict = {}
    if os.path.isfile(clip_json):
        try:
            with open(clip_json, "r", encoding="utf-8") as f:
                loaded = json.load(f)
                if isinstance(loaded, dict):
                    payload = loaded
        except Exception:
            payload = {}
    payload["alpha_hint_dir"] = alpha_dir
    payload["alpha_hint_count"] = alpha_count
    payload["status"] = status
    payload["updated_utc"] = datetime.now(timezone.utc).isoformat()
    with open(clip_json, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)


def _run_media_extract_frames(request_id: str, input_path: str, output_dir: str, overwrite: bool) -> None:
    cmd_name = "media.extract_frames"
    try:
        input_path = os.path.abspath((input_path or "").strip())
        output_dir = os.path.abspath((output_dir or "").strip())
        if not input_path or not output_dir:
            msg = "input_path and output_dir are required"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isfile(input_path):
            msg = f"input file not found: {input_path}"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        os.makedirs(output_dir, exist_ok=True)
        existing = [n for n in os.listdir(output_dir) if n.lower().endswith(".png")]
        if existing and not overwrite:
            msg = f"output has existing frames ({len(existing)}); set overwrite=true to replace"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        ffmpeg = shutil.which("ffmpeg")
        if not ffmpeg:
            msg = "ffmpeg not found on PATH"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        total = _probe_total_frames(input_path)
        out_pattern = os.path.join(output_dir, "%06d.png")
        args = [ffmpeg, "-hide_banner", "-nostdin", "-loglevel", "error", "-nostats"]
        args.append("-y" if overwrite else "-n")
        args.extend(["-i", input_path, out_pattern])

        bridge_core._emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": f"extract frames: input={input_path}; output={output_dir}; overwrite={overwrite}",
            }
        )

        # Keep extraction execution simple and deterministic: one run, one result.
        run = subprocess.run(
            args,
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )
        code = run.returncode or 0
        final_count = len([n for n in os.listdir(output_dir) if n.lower().endswith(".png")])
        bridge_core._emit({"type": "progress", "current": final_count, "total": total, "phase": "extract_frames"})

        if code != 0:
            stderr = (run.stderr or "").strip()
            msg = stderr.splitlines()[-1] if stderr else f"ffmpeg failed (exit {code})"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        clip_dir = os.path.dirname(output_dir.rstrip("\\/"))
        _write_clip_json(clip_dir, input_path, output_dir, final_count)
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "extract_frames",
                "ok": True,
                "summary": f"wrote {final_count} frames to {output_dir}",
            }
        )
        bridge_core._emit_done(cmd_name, request_id, ok=True, summary=f"frames={final_count}")
    except Exception as exc:  # noqa: BLE001
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))