"""
Unity ↔ EZ-CorridorKey stdio bridge (NDJSON).

Run with EZ repo as working directory and the same Python interpreter EZ uses
(e.g. .venv\\Scripts\\python.exe) so `import backend` resolves.

Protocol (stdin, one JSON object per line):
  {"cmd":"health"}  optional: "request_id":"..."
  {"cmd":"shutdown"}
  {"cmd":"diag.python","request_id":"..."}
  {"cmd":"diag.imports","request_id":"..."}
  {"cmd":"diag.ffmpeg_version","request_id":"..."}
  {"cmd":"diag.file_exists","request_id":"...","path":"C:/abs/path"}
  {"cmd":"diag.birefnet","request_id":"...","usage":"Matting"}  # fast checkpoint report (no model load)

Protocol (stdout, one JSON object per line, flush after each line):
  {"type":"log",...}
  {"type":"health","ok":true|false,"summary":"..."}
  {"type":"diag_result","request_id":"...","diag":"python","ok":true,"summary":"..."}
  {"type":"done","cmd":"health|diag.python|...","request_id":"...","ok":true}

Phase 1 diagnostics run in worker threads so stdin keeps accepting commands.
No inference or rendering — imports and subprocess checks only.
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import threading
import traceback
from datetime import datetime, timezone

try:
    from . import bridge_core
except ImportError:
    import bridge_core

try:
    from . import diagnostics
except ImportError:
    import diagnostics

try:
    from . import media_processing
except ImportError:
    import media_processing

try:
    from . import alpha_generation
except ImportError:
    import alpha_generation

try:
    from . import model_management
except ImportError:
    import model_management


def _dispatch(msg: dict) -> bool:
    """Return False when the stdin loop should exit (shutdown)."""
    cmd = msg.get("cmd")
    rid = (msg.get("request_id") or "").strip()
    bridge_core._emit(
        {
            "type": "log",
            "level": "DEBUG",
            "logger": "unity_bridge",
            "message": f"[_dispatch] cmd={cmd!r} request_id={rid!r}",
        }
    )

    if cmd == "health":
        diagnostics._cmd_health()
        bridge_core._emit_done("health", rid)
        return True

    if cmd == "shutdown":
        bridge_core._emit(
            {
                "type": "log",
                "level": "INFO",
                "message": "shutdown received.",
                "logger": "unity_bridge",
            }
        )
        bridge_core._emit_done("shutdown", rid)
        return False

    if cmd == "diag.python":
        threading.Thread(target=diagnostics._run_diag_python, args=(rid,), daemon=True).start()
        return True
    if cmd == "diag.imports":
        threading.Thread(target=diagnostics._run_diag_imports, args=(rid,), daemon=True).start()
        return True
    if cmd == "diag.ffmpeg_version":
        threading.Thread(target=diagnostics._run_diag_ffmpeg_version, args=(rid,), daemon=True).start()
        return True
    if cmd == "diag.file_exists":
        path = msg.get("path") or ""
        threading.Thread(target=diagnostics._run_diag_file_exists, args=(rid, path), daemon=True).start()
        return True
    if cmd == "diag.birefnet":
        usage = msg.get("usage") or "Matting"
        # Run on the stdin thread so NDJSON lines flush before the next read (daemon thread ordering confused Unity).
        diagnostics._run_diag_birefnet(rid, usage)
        return True
    if cmd == "media.extract_frames":
        input_path = msg.get("input_path") or ""
        output_dir = msg.get("output_dir") or ""
        overwrite = bool(msg.get("overwrite", False))
        threading.Thread(
            target=media_processing._run_media_extract_frames,
            args=(rid, input_path, output_dir, overwrite),
            daemon=True,
        ).start()
        return True
    if cmd == "alpha.gvm_hint":
        clip_root = msg.get("clip_root") or ""
        frames_dir = msg.get("frames_dir") or ""
        overwrite = bool(msg.get("overwrite", False))
        bridge_core._emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": f"[DISPATCH] alpha.gvm_hint request_id={rid} clip_root={clip_root!r} frames_dir={frames_dir!r} overwrite={overwrite}",
            }
        )
        threading.Thread(
            target=alpha_generation._run_alpha_gvm_hint,
            args=(rid, clip_root, frames_dir, overwrite),
            daemon=True,
        ).start()
        bridge_core._emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": f"[DISPATCH] alpha.gvm_hint thread spawned for request_id={rid}",
            }
        )
        return True
    if cmd == "alpha.birefnet_hint":
        clip_root = msg.get("clip_root") or ""
        frames_dir = msg.get("frames_dir") or ""
        usage = msg.get("usage") or "Matting"
        overwrite = bool(msg.get("overwrite", False))
        threading.Thread(
            target=alpha_generation._run_alpha_birefnet_hint,
            args=(rid, clip_root, frames_dir, usage, overwrite),
            daemon=True,
        ).start()
        return True
    if cmd == "model.download_gvm":
        threading.Thread(target=model_management._run_model_download_gvm, args=(rid,), daemon=True).start()
        return True
    if cmd == "model.download_sam2":
        model_name = msg.get("model_name") or "base-plus"
        threading.Thread(target=model_management._run_model_download_sam2, args=(rid, model_name), daemon=True).start()
        return True
    if cmd == "model.download_videomama":
        threading.Thread(target=model_management._run_model_download_videomama, args=(rid,), daemon=True).start()
        return True
    if cmd == "model.check_status":
        threading.Thread(target=model_management._run_model_check_status, args=(rid,), daemon=True).start()
        return True
    if cmd == "model.is_installed":
        model_name = msg.get("model_name") or ""
        threading.Thread(target=model_management._run_model_is_installed, args=(rid, model_name), daemon=True).start()
        return True

    bridge_core._emit(
        {
            "type": "log",
            "level": "WARNING",
            "message": f"Unknown cmd: {cmd!r}",
            "logger": "unity_bridge",
        }
    )
    return True


def main() -> None:
    bridge_core._emit(
        {
            "type": "log",
            "level": "INFO",
            "message": f"unity_bridge started (stdio NDJSON) [{bridge_core.BRIDGE_VERSION}] file={__file__}",
            "logger": "unity_bridge",
        }
    )

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        bridge_core._emit(
            {
                "type": "log",
                "level": "DEBUG",
                "logger": "unity_bridge",
                "message": f"[stdin] received line: {line[:200]!r}",
            }
        )
        try:
            msg = json.loads(line)
        except json.JSONDecodeError as exc:
            bridge_core._emit(
                {
                    "type": "log",
                    "level": "WARNING",
                    "message": f"Invalid JSON on stdin: {exc}: {line!r}",
                    "logger": "unity_bridge",
                }
            )
            continue

        if not _dispatch(msg):
            break


if __name__ == "__main__":
    main()
