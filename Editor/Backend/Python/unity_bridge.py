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

BRIDGE_VERSION = "unity_bridge_birefnet_v3"


def _emit(obj: dict) -> None:
    print(json.dumps(obj, ensure_ascii=False, separators=(",", ":")), flush=True)


def _emit_log_lines(text: str, logger: str = "unity_bridge", max_line_len: int = 3500) -> None:
    """Emit multi-line text as separate log NDJSON lines (Unity JsonUtility is fragile on huge single fields)."""
    if not text:
        _emit({"type": "log", "level": "INFO", "logger": logger, "message": "(empty)"})
        return
    for raw_line in text.splitlines():
        remaining = raw_line
        while remaining:
            chunk = remaining[:max_line_len]
            remaining = remaining[max_line_len:]
            _emit({"type": "log", "level": "INFO", "logger": logger, "message": chunk})


def _emit_done(cmd: str, request_id: str, ok: bool = True, summary: str | None = None) -> None:
    d: dict = {"type": "done", "cmd": cmd, "ok": ok}
    if request_id:
        d["request_id"] = request_id
    if summary is not None:
        d["summary"] = summary
    _emit(d)


def _read_text_file_tail(path: str, max_bytes: int = 12000) -> str:
    """Last ~max_bytes of a text file (for subprocess stderr logs)."""
    try:
        with open(path, "rb") as f:
            f.seek(0, os.SEEK_END)
            sz = f.tell()
            if sz <= 0:
                return ""
            if sz <= max_bytes:
                f.seek(0)
            else:
                f.seek(sz - max_bytes)
            return f.read().decode("utf-8", errors="replace")
    except OSError:
        return ""


def _birefnet_append_stderr(base: str, stderr_path: str) -> str:
    if stderr_path and os.path.isfile(stderr_path):
        tail = _read_text_file_tail(stderr_path).strip()
        if tail:
            return f"{base}\n--- birefnet stderr (tail; full log: {stderr_path}) ---\n{tail}"
    return base


def _cmd_health() -> None:
    try:
        from backend.ffmpeg_tools.discovery import validate_ffmpeg_install

        result = validate_ffmpeg_install(require_probe=True)
        if not result.ok:
            _emit(
                {
                    "type": "health",
                    "ok": False,
                    "summary": result.message,
                }
            )
            return

        py = f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}"
        _emit(
            {
                "type": "health",
                "ok": True,
                "summary": f"Python {py}; {result.message}",
            }
        )
    except Exception as exc:  # noqa: BLE001 — bridge must never crash the process
        tb = traceback.format_exc()
        _emit(
            {
                "type": "log",
                "level": "ERROR",
                "message": f"health check failed: {exc}\n{tb}",
                "logger": "unity_bridge",
            }
        )
        _emit(
            {
                "type": "health",
                "ok": False,
                "summary": str(exc),
            }
        )


def _run_diag_python(request_id: str) -> None:
    try:
        summary = (
            f"version={sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}; "
            f"executable={sys.executable}; cwd={os.getcwd()}"
        )
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "python",
                "ok": True,
                "summary": summary,
            }
        )
        _emit_done("diag.python", request_id, ok=True)
    except Exception as exc:  # noqa: BLE001
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "python",
                "ok": False,
                "summary": str(exc),
            }
        )
        _emit_done("diag.python", request_id, ok=False, summary=str(exc))


def _run_diag_imports(request_id: str) -> None:
    try:
        import importlib

        importlib.import_module("backend")
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "imports",
                "ok": True,
                "summary": "import backend: ok",
            }
        )
        _emit_done("diag.imports", request_id, ok=True)
    except Exception as exc:  # noqa: BLE001
        tb = traceback.format_exc()
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "imports",
                "ok": False,
                "summary": f"{exc}\n{tb}",
            }
        )
        _emit_done("diag.imports", request_id, ok=False, summary=str(exc))


def _run_diag_ffmpeg_version(request_id: str) -> None:
    try:
        ffmpeg = shutil.which("ffmpeg")
        if not ffmpeg:
            _emit(
                {
                    "type": "diag_result",
                    "request_id": request_id,
                    "diag": "ffmpeg_version",
                    "ok": False,
                    "summary": "ffmpeg not found on PATH",
                }
            )
            _emit_done("diag.ffmpeg_version", request_id, ok=False, summary="ffmpeg not found on PATH")
            return
        proc = subprocess.run(
            [ffmpeg, "-version"],
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        out = (proc.stdout or proc.stderr or "").strip()
        first = out.splitlines()[0] if out else "(no output)"
        ok = proc.returncode == 0
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "ffmpeg_version",
                "ok": ok,
                "summary": f"{ffmpeg} -> {first}",
            }
        )
        _emit_done("diag.ffmpeg_version", request_id, ok=ok, summary=first if not ok else None)
    except Exception as exc:  # noqa: BLE001
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "ffmpeg_version",
                "ok": False,
                "summary": str(exc),
            }
        )
        _emit_done("diag.ffmpeg_version", request_id, ok=False, summary=str(exc))


def _run_diag_file_exists(request_id: str, path: str) -> None:
    try:
        path = (path or "").strip()
        if not path:
            _emit(
                {
                    "type": "diag_result",
                    "request_id": request_id,
                    "diag": "file_exists",
                    "ok": False,
                    "summary": "missing path",
                }
            )
            _emit_done("diag.file_exists", request_id, ok=False, summary="missing path")
            return
        exists = os.path.isfile(path)
        try:
            size = os.path.getsize(path) if exists else 0
        except OSError:
            size = -1
        summary = f"path={path!r}; isfile={exists}; size={size}"
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "file_exists",
                "ok": exists,
                "summary": summary,
            }
        )
        _emit_done("diag.file_exists", request_id, ok=exists, summary=summary if not exists else None)
    except Exception as exc:  # noqa: BLE001
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "file_exists",
                "ok": False,
                "summary": str(exc),
            }
        )
        _emit_done("diag.file_exists", request_id, ok=False, summary=str(exc))


def _run_diag_birefnet(request_id: str, usage: str) -> None:
    _emit(
        {
            "type": "log",
            "level": "INFO",
            "logger": "unity_bridge",
            "message": (
                "diag.birefnet: imports and checkpoint paths use process cwd (set this to your EZ/R root in Unity). "
                f"cwd={os.getcwd()}"
            ),
        }
    )
    try:
        from birefnet_checkpoint_diagnostics import format_full_report

        ok, report = format_full_report(usage or "Matting")
        # Full report as log lines — putting multi-KB strings in diag_result breaks Unity JsonUtility (empty type, silent drop).
        _emit_log_lines(report, logger="diag.birefnet")
        short_summary = (
            f"ok={ok}; full checkpoint report above ({len(report)} chars in {report.count(chr(10)) + 1} lines)"
        )
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "birefnet",
                "ok": ok,
                "summary": short_summary,
            }
        )
        _emit_done(
            "diag.birefnet",
            request_id,
            ok=ok,
            summary=None if ok else "checkpoint or torch issue — see diag.birefnet logs above",
        )
    except Exception as exc:  # noqa: BLE001
        tb = traceback.format_exc()
        _emit_log_lines(f"{exc}\n{tb}", logger="diag.birefnet")
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "birefnet",
                "ok": False,
                "summary": f"exception: {exc}",
            }
        )
        _emit_done("diag.birefnet", request_id, ok=False, summary=str(exc))


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
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isfile(input_path):
            msg = f"input file not found: {input_path}"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        os.makedirs(output_dir, exist_ok=True)
        existing = [n for n in os.listdir(output_dir) if n.lower().endswith(".png")]
        if existing and not overwrite:
            msg = f"output has existing frames ({len(existing)}); set overwrite=true to replace"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        ffmpeg = shutil.which("ffmpeg")
        if not ffmpeg:
            msg = "ffmpeg not found on PATH"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        total = _probe_total_frames(input_path)
        out_pattern = os.path.join(output_dir, "%06d.png")
        args = [ffmpeg, "-hide_banner", "-nostdin", "-loglevel", "error", "-nostats"]
        args.append("-y" if overwrite else "-n")
        args.extend(["-i", input_path, out_pattern])

        _emit(
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
        _emit({"type": "progress", "current": final_count, "total": total, "phase": "extract_frames"})

        if code != 0:
            stderr = (run.stderr or "").strip()
            msg = stderr.splitlines()[-1] if stderr else f"ffmpeg failed (exit {code})"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        clip_dir = os.path.dirname(output_dir.rstrip("\\/"))
        _write_clip_json(clip_dir, input_path, output_dir, final_count)
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "extract_frames",
                "ok": True,
                "summary": f"wrote {final_count} frames to {output_dir}",
            }
        )
        _emit_done(cmd_name, request_id, ok=True, summary=f"frames={final_count}")
    except Exception as exc:  # noqa: BLE001
        _emit({"type": "error", "message": str(exc)})
        _emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_alpha_gvm_hint(
    request_id: str,
    clip_root: str,
    frames_dir: str,
    overwrite: bool,
) -> None:
    cmd_name = "alpha.gvm_hint"
    try:
        clip_root = os.path.abspath((clip_root or "").strip())
        frames_dir = os.path.abspath((frames_dir or "").strip())
        if not clip_root or not frames_dir:
            msg = "clip_root and frames_dir are required"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isdir(clip_root):
            msg = f"clip_root not found: {clip_root}"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isdir(frames_dir):
            msg = f"frames_dir not found: {frames_dir}"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        frame_files = [n for n in os.listdir(frames_dir) if n.lower().endswith((".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp"))]
        if not frame_files:
            msg = f"frames_dir has no image frames: {frames_dir}"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        alpha_dir = os.path.join(clip_root, "AlphaHint")
        if os.path.isdir(alpha_dir):
            existing_alpha = [n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]
            if existing_alpha and not overwrite:
                msg = f"AlphaHint already has {len(existing_alpha)} PNGs; set overwrite=true to replace"
                _emit({"type": "error", "message": msg})
                _emit_done(cmd_name, request_id, ok=False, summary=msg)
                return
            if existing_alpha and overwrite:
                shutil.rmtree(alpha_dir, ignore_errors=True)
        os.makedirs(alpha_dir, exist_ok=True)

        _emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": f"run_gvm: clip_root={clip_root}; frames_dir={frames_dir}; overwrite={overwrite}",
            }
        )

        total_hint = max(1, len(frame_files))
        bridge_tmp_dir = os.path.join(clip_root, ".bridge_tmp")
        os.makedirs(bridge_tmp_dir, exist_ok=True)
        result_path = os.path.join(bridge_tmp_dir, f"corridorkey_gvm_result_{request_id}.json")
        runner_path = os.path.join(os.path.dirname(__file__), "gvm_hint_runner.py")
        try:
            if os.path.exists(result_path):
                os.remove(result_path)
        except Exception:
            pass
        if not os.path.isfile(runner_path):
            raise RuntimeError(f"runner script not found: {runner_path}")
        _emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "gvm stage: start runner subprocess"})
        proc = subprocess.Popen(
            [
                sys.executable,
                "-u",
                runner_path,
                clip_root,
                frames_dir,
                alpha_dir,
                result_path,
            ],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            cwd=os.getcwd(),
        )
        _emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"gvm runner pid={proc.pid} script={runner_path}"})
        waited = 0.0
        heartbeat_s = 5.0
        timeout_s = 300.0
        last_count = -1
        while True:
            curr = len([n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]) if os.path.isdir(alpha_dir) else 0
            if curr != last_count:
                last_count = curr
                _emit({"type": "progress", "current": curr, "total": total_hint, "phase": "gvm_hint"})

            if os.path.isfile(result_path):
                with open(result_path, "r", encoding="utf-8") as rf:
                    result = json.load(rf)
                if not result.get("ok", False):
                    raise RuntimeError(str(result.get("message") or "gvm runner failed"))
                alpha_count = int(result.get("alpha_count", curr))
                _emit({"type": "progress", "current": alpha_count, "total": total_hint, "phase": "gvm_hint"})
                _emit(
                    {
                        "type": "diag_result",
                        "request_id": request_id,
                        "diag": "gvm_hint",
                        "ok": True,
                        "summary": str(result.get("message") or f"GVM wrote {alpha_count} alpha hint frame(s)"),
                    }
                )
                break

            if proc.poll() is not None:
                code = proc.returncode or 0
                if code != 0:
                    if os.path.isfile(result_path):
                        with open(result_path, "r", encoding="utf-8") as rf:
                            result = json.load(rf)
                        raise RuntimeError(str(result.get("message") or f"gvm runner exited with code {code}"))
                    raise RuntimeError(f"gvm runner exited with code {code} without result file")
                spin = 0
                while spin < 10 and not os.path.isfile(result_path):
                    threading.Event().wait(0.2)
                    spin += 1
                if os.path.isfile(result_path):
                    break
                raise RuntimeError("gvm runner exited successfully but result file not found")

            threading.Event().wait(heartbeat_s)
            waited += heartbeat_s
            _emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"gvm running... {int(waited)}s elapsed"})
            _emit({"type": "progress", "current": curr, "total": total_hint, "phase": "gvm_hint_loading_v4"})
            if waited >= timeout_s:
                proc.kill()
                raise RuntimeError(f"run_gvm timed out after {int(timeout_s)}s")

        _emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": "gvm run completed; collecting outputs",
            }
        )
        alpha_files = [n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]
        alpha_count = len(alpha_files)
        _update_clip_json_with_alpha(clip_root, alpha_dir, alpha_count, status="gvm_hint_generated")
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "gvm_hint",
                "ok": True,
                "summary": f"GVM wrote {alpha_count} alpha hint frame(s) to {alpha_dir}",
            }
        )
        _emit_done(cmd_name, request_id, ok=True, summary=f"alpha_hint_frames={alpha_count}")
    except Exception as exc:  # noqa: BLE001
        _emit({"type": "error", "message": str(exc)})
        _emit_done(cmd_name, request_id, ok=False, summary=str(exc))
    finally:
        # Best-effort cleanup of bridge temp artifacts.
        try:
            if 'result_path' in locals() and os.path.isfile(result_path):
                os.remove(result_path)
        except Exception:
            pass
        try:
            if 'bridge_tmp_dir' in locals() and os.path.isdir(bridge_tmp_dir) and not os.listdir(bridge_tmp_dir):
                os.rmdir(bridge_tmp_dir)
        except Exception:
            pass


def _run_alpha_birefnet_hint(
    request_id: str,
    clip_root: str,
    frames_dir: str,
    usage: str,
    overwrite: bool,
) -> None:
    cmd_name = "alpha.birefnet_hint"
    stderr_path = ""
    try:
        clip_root = os.path.abspath((clip_root or "").strip())
        frames_dir = os.path.abspath((frames_dir or "").strip())
        usage = (usage or "Matting").strip() or "Matting"
        if not clip_root or not frames_dir:
            msg = "clip_root and frames_dir are required"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isdir(clip_root):
            msg = f"clip_root not found: {clip_root}"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        if not os.path.isdir(frames_dir):
            msg = f"frames_dir not found: {frames_dir}"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return
        frame_files = [n for n in os.listdir(frames_dir) if n.lower().endswith((".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp"))]
        if not frame_files:
            msg = f"frames_dir has no image frames: {frames_dir}"
            _emit({"type": "error", "message": msg})
            _emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        alpha_dir = os.path.join(clip_root, "AlphaHint")
        if os.path.isdir(alpha_dir):
            existing_alpha = [n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]
            if existing_alpha and not overwrite:
                msg = f"AlphaHint already has {len(existing_alpha)} PNGs; set overwrite=true to replace"
                _emit({"type": "error", "message": msg})
                _emit_done(cmd_name, request_id, ok=False, summary=msg)
                return
            if existing_alpha and overwrite:
                shutil.rmtree(alpha_dir, ignore_errors=True)
        os.makedirs(alpha_dir, exist_ok=True)

        _emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": (
                    f"run_birefnet request_id={request_id}: "
                    f"clip_root={clip_root}; frames_dir={frames_dir}; alpha_dir={alpha_dir}; "
                    f"image_files_in_frames_dir={len(frame_files)}; usage={usage}; overwrite={overwrite}"
                ),
            }
        )

        total_hint = max(1, len(frame_files))
        bridge_tmp_dir = os.path.join(clip_root, ".bridge_tmp")
        os.makedirs(bridge_tmp_dir, exist_ok=True)
        result_path = os.path.join(bridge_tmp_dir, f"corridorkey_birefnet_result_{request_id}.json")
        status_path = os.path.join(bridge_tmp_dir, f"corridorkey_birefnet_status_{request_id}.json")
        stderr_path = os.path.join(bridge_tmp_dir, f"corridorkey_birefnet_stderr_{request_id}.log")
        try:
            with open(stderr_path, "w", encoding="utf-8", buffering=1) as _sf:
                _sf.write(f"unity_bridge: birefnet stderr (request_id={request_id})\n")
        except Exception:
            stderr_path = ""
        runner_path = os.path.join(os.path.dirname(__file__), "birefnet_hint_runner.py")
        try:
            if os.path.exists(result_path):
                os.remove(result_path)
            if os.path.exists(status_path):
                os.remove(status_path)
        except Exception:
            pass
        if not os.path.isfile(runner_path):
            raise RuntimeError(f"runner script not found: {runner_path}")
        _emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "birefnet stage: start runner subprocess"})
        # stderr=PIPE + background pump (reliable flush on Windows vs open file handle). Child stdin must not inherit the bridge pipe.
        use_stderr_pipe = bool(stderr_path)
        popen_kw: dict = {
            "stdout": subprocess.DEVNULL,
            "stdin": subprocess.DEVNULL,
            "stderr": subprocess.PIPE if use_stderr_pipe else subprocess.DEVNULL,
            "text": True,
            "encoding": "utf-8",
            "errors": "replace",
            "bufsize": 1,
            "cwd": os.getcwd(),
        }
        if sys.platform == "win32":
            popen_kw["creationflags"] = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        proc = subprocess.Popen(
            [
                sys.executable,
                "-u",
                runner_path,
                clip_root,
                frames_dir,
                alpha_dir,
                result_path,
                status_path,
                usage,
            ],
            **popen_kw,
        )

        if stderr_path:
            try:
                with open(stderr_path, "a", encoding="utf-8", buffering=1) as _sf:
                    _sf.write(f"unity_bridge: child pid={proc.pid} {sys.executable!r}\n")
            except Exception:
                pass

        if use_stderr_pipe and proc.stderr is not None:

            def _pump_birefnet_stderr() -> None:
                try:
                    with open(stderr_path, "a", encoding="utf-8", buffering=1) as out:
                        for line in proc.stderr:
                            out.write(line)
                            out.flush()
                except Exception as exc:
                    try:
                        with open(stderr_path, "a", encoding="utf-8", errors="replace") as out:
                            out.write(f"\nunity_bridge: stderr pump exception: {exc!r}\n")
                            out.flush()
                    except Exception:
                        pass
                finally:
                    try:
                        proc.stderr.close()
                    except Exception:
                        pass

            threading.Thread(target=_pump_birefnet_stderr, daemon=True, name="birefnet-stderr-pump").start()
        _emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": (
                    f"birefnet runner pid={proc.pid} script={runner_path}; stderr -> {stderr_path}"
                    if stderr_path
                    else f"birefnet runner pid={proc.pid} script={runner_path}"
                ),
            }
        )
        waited = 0.0
        poll_s = 1.0
        heartbeat_log_s = 20.0
        last_status_emit = ""
        last_progress_emit = (-1, -1, "", "")
        last_non_inference_progress = ""
        last_pulse_at = 0.0
        timeout_s = 900.0
        last_count = -1
        since_status_log = 0.0
        while True:
            curr = len([n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]) if os.path.isdir(alpha_dir) else 0
            if curr != last_count:
                last_count = curr
                _emit(
                    {
                        "type": "progress",
                        "request_id": request_id,
                        "current": curr,
                        "total": total_hint,
                        "phase": "birefnet_hint",
                        "detail": f"{curr}/{total_hint} alpha PNG(s) on disk",
                    }
                )

            # Runner status (download / load long before first PNG — avoids stuck 0/60 with no explanation).
            if os.path.isfile(status_path):
                try:
                    with open(status_path, "r", encoding="utf-8") as sf:
                        st = json.load(sf)
                    if isinstance(st, dict):
                        stage = str(st.get("stage") or "")
                        detail = str(st.get("detail") or "")
                        key = f"{stage}|{detail}"
                        if key != last_status_emit:
                            last_status_emit = key
                            _emit(
                                {
                                    "type": "log",
                                    "level": "INFO",
                                    "logger": "unity_bridge",
                                    "message": f"BiRefNet [{stage}]: {detail}" if detail else f"BiRefNet [{stage}]",
                                }
                            )
                        sc = st.get("current")
                        stt = st.get("total")
                        if isinstance(sc, int) and isinstance(stt, int) and stt > 0:
                            inf_detail = detail or f"frame {sc}/{stt}"
                            if (sc, stt, stage, inf_detail) != last_progress_emit:
                                last_progress_emit = (sc, stt, stage, inf_detail)
                                _emit(
                                    {
                                        "type": "progress",
                                        "request_id": request_id,
                                        "current": sc,
                                        "total": stt,
                                        "phase": f"birefnet_{stage}",
                                        "detail": inf_detail,
                                    }
                                )
                        elif stage and stage != "inference":
                            nk = f"{stage}|{detail}"
                            if nk != last_non_inference_progress:
                                last_non_inference_progress = nk
                                _emit(
                                    {
                                        "type": "progress",
                                        "request_id": request_id,
                                        "current": curr,
                                        "total": total_hint,
                                        "phase": f"birefnet_{stage}",
                                        "detail": detail or stage,
                                    }
                                )
                except Exception:
                    pass

            if os.path.isfile(result_path):
                with open(result_path, "r", encoding="utf-8") as rf:
                    result = json.load(rf)
                if not result.get("ok", False):
                    raise RuntimeError(
                        _birefnet_append_stderr(
                            str(result.get("message") or "birefnet runner failed"),
                            stderr_path,
                        )
                    )
                alpha_count = int(result.get("alpha_count", curr))
                _emit(
                    {
                        "type": "progress",
                        "request_id": request_id,
                        "current": alpha_count,
                        "total": total_hint,
                        "phase": "birefnet_hint",
                        "detail": f"{alpha_count}/{total_hint} alpha PNG(s) on disk",
                    }
                )
                _emit(
                    {
                        "type": "diag_result",
                        "request_id": request_id,
                        "diag": "birefnet_hint",
                        "ok": True,
                        "summary": str(result.get("message") or f"BiRefNet wrote {alpha_count} alpha hint frame(s)"),
                    }
                )
                break

            if proc.poll() is not None:
                code = proc.returncode or 0
                if code != 0:
                    if os.path.isfile(result_path):
                        with open(result_path, "r", encoding="utf-8") as rf:
                            result = json.load(rf)
                        raise RuntimeError(
                            _birefnet_append_stderr(
                                str(result.get("message") or f"birefnet runner exited with code {code}"),
                                stderr_path,
                            )
                        )
                    raise RuntimeError(
                        _birefnet_append_stderr(
                            f"birefnet runner exited with code {code} without result file",
                            stderr_path,
                        )
                    )
                spin = 0
                while spin < 10 and not os.path.isfile(result_path):
                    threading.Event().wait(0.2)
                    spin += 1
                if os.path.isfile(result_path):
                    break
                raise RuntimeError(
                    _birefnet_append_stderr(
                        "birefnet runner exited successfully but result file not found",
                        stderr_path,
                    )
                )

            threading.Event().wait(poll_s)
            waited += poll_s
            since_status_log += poll_s
            # Granular UI feedback: PNG count stays 0 until the first frame completes (cold subprocess: import + load + first forward).
            if waited >= 2.0 and (waited - last_pulse_at) >= 2.0:
                skip_warmup_pulse = False
                if os.path.isfile(status_path):
                    try:
                        with open(status_path, "r", encoding="utf-8") as sf:
                            pst = json.load(sf)
                        if isinstance(pst, dict) and str(pst.get("stage") or "") == "inference":
                            skip_warmup_pulse = True
                    except Exception:
                        pass
                if not skip_warmup_pulse:
                    last_pulse_at = waited
                    pulse_detail = ""
                    pulse_phase = "birefnet_warmup"
                    if os.path.isfile(status_path):
                        try:
                            with open(status_path, "r", encoding="utf-8") as sf:
                                pst = json.load(sf)
                            if isinstance(pst, dict):
                                stg = str(pst.get("stage") or "?")
                                det = str(pst.get("detail") or "")
                                pulse_phase = f"birefnet_{stg}"
                                pulse_detail = det if det else f"stage={stg}"
                        except Exception:
                            pass
                    if not pulse_detail:
                        pulse_detail = "waiting for runner (imports / startup)"
                    _emit(
                        {
                            "type": "progress",
                            "request_id": request_id,
                            "current": curr,
                            "total": total_hint,
                            "phase": pulse_phase,
                            "detail": f"{pulse_detail} ({int(waited)}s; PNG count updates after first frame)",
                        }
                    )
            if since_status_log >= heartbeat_log_s:
                since_status_log = 0.0
                _emit(
                    {
                        "type": "log",
                        "level": "INFO",
                        "logger": "unity_bridge",
                        "message": (
                            f"birefnet still running {int(waited)}s (no/few PNGs yet); first run may download weights — see {stderr_path}"
                            if stderr_path
                            else f"birefnet still running {int(waited)}s (no/few PNGs yet)"
                        ),
                    }
                )
            if waited >= timeout_s:
                proc.kill()
                raise RuntimeError(
                    _birefnet_append_stderr(
                        f"run_birefnet timed out after {int(timeout_s)}s (check stderr log and run diag.birefnet)",
                        stderr_path,
                    )
                )

        _emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": "birefnet run completed; collecting outputs",
            }
        )
        alpha_files = [n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]
        alpha_count = len(alpha_files)
        _update_clip_json_with_alpha(clip_root, alpha_dir, alpha_count, status="birefnet_hint_generated")
        _emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "birefnet_hint",
                "ok": True,
                "summary": f"BiRefNet wrote {alpha_count} alpha hint frame(s) to {alpha_dir}",
            }
        )
        _emit_done(cmd_name, request_id, ok=True, summary=f"alpha_hint_frames={alpha_count}")
    except Exception as exc:  # noqa: BLE001
        _emit({"type": "error", "message": str(exc)})
        _emit_done(cmd_name, request_id, ok=False, summary=str(exc))
    finally:
        # Child stderr uses PIPE + pump; nothing to close here.
        # Best-effort cleanup of bridge temp artifacts.
        try:
            if 'result_path' in locals() and os.path.isfile(result_path):
                os.remove(result_path)
        except Exception:
            pass
        try:
            if 'status_path' in locals() and os.path.isfile(status_path):
                os.remove(status_path)
        except Exception:
            pass
        try:
            if 'bridge_tmp_dir' in locals() and os.path.isdir(bridge_tmp_dir) and not os.listdir(bridge_tmp_dir):
                os.rmdir(bridge_tmp_dir)
        except Exception:
            pass


def _dispatch(msg: dict) -> bool:
    """Return False when the stdin loop should exit (shutdown)."""
    cmd = msg.get("cmd")
    rid = (msg.get("request_id") or "").strip()

    if cmd == "health":
        _cmd_health()
        _emit_done("health", rid)
        return True

    if cmd == "shutdown":
        _emit(
            {
                "type": "log",
                "level": "INFO",
                "message": "shutdown received.",
                "logger": "unity_bridge",
            }
        )
        _emit_done("shutdown", rid)
        return False

    if cmd == "diag.python":
        threading.Thread(target=_run_diag_python, args=(rid,), daemon=True).start()
        return True
    if cmd == "diag.imports":
        threading.Thread(target=_run_diag_imports, args=(rid,), daemon=True).start()
        return True
    if cmd == "diag.ffmpeg_version":
        threading.Thread(target=_run_diag_ffmpeg_version, args=(rid,), daemon=True).start()
        return True
    if cmd == "diag.file_exists":
        path = msg.get("path") or ""
        threading.Thread(target=_run_diag_file_exists, args=(rid, path), daemon=True).start()
        return True
    if cmd == "diag.birefnet":
        usage = msg.get("usage") or "Matting"
        # Run on the stdin thread so NDJSON lines flush before the next read (daemon thread ordering confused Unity).
        _run_diag_birefnet(rid, usage)
        return True
    if cmd == "media.extract_frames":
        input_path = msg.get("input_path") or ""
        output_dir = msg.get("output_dir") or ""
        overwrite = bool(msg.get("overwrite", False))
        threading.Thread(
            target=_run_media_extract_frames,
            args=(rid, input_path, output_dir, overwrite),
            daemon=True,
        ).start()
        return True
    if cmd == "alpha.gvm_hint":
        clip_root = msg.get("clip_root") or ""
        frames_dir = msg.get("frames_dir") or ""
        overwrite = bool(msg.get("overwrite", False))
        threading.Thread(
            target=_run_alpha_gvm_hint,
            args=(rid, clip_root, frames_dir, overwrite),
            daemon=True,
        ).start()
        return True
    if cmd == "alpha.birefnet_hint":
        clip_root = msg.get("clip_root") or ""
        frames_dir = msg.get("frames_dir") or ""
        usage = msg.get("usage") or "Matting"
        overwrite = bool(msg.get("overwrite", False))
        threading.Thread(
            target=_run_alpha_birefnet_hint,
            args=(rid, clip_root, frames_dir, usage, overwrite),
            daemon=True,
        ).start()
        return True

    _emit(
        {
            "type": "log",
            "level": "WARNING",
            "message": f"Unknown cmd: {cmd!r}",
            "logger": "unity_bridge",
        }
    )
    return True


def main() -> None:
    _emit(
        {
            "type": "log",
            "level": "INFO",
            "message": f"unity_bridge started (stdio NDJSON) [{BRIDGE_VERSION}] file={__file__}",
            "logger": "unity_bridge",
        }
    )

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except json.JSONDecodeError as exc:
            _emit(
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
