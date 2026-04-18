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


def _run_model_download_gvm(request_id: str) -> None:
    cmd_name = "model.download_gvm"
    try:
        # Import setup_models dynamically like EZ UI does
        import importlib.util
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)
        
        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"Downloading GVM model..."})
        ok = setup_models.download_model("gvm")
        summary = "GVM model downloaded successfully" if ok else "GVM model download failed"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "download_gvm", "ok": ok, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=ok, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_download_sam2(request_id: str, model_name: str = "base-plus") -> None:
    cmd_name = "model.download_sam2"
    try:
        # Import setup_models dynamically like EZ UI does
        import importlib.util
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)
        
        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"Downloading SAM2 {model_name} model..."})
        ok = setup_models.download_sam2_model(model_name)
        summary = f"SAM2 {model_name} model downloaded successfully" if ok else f"SAM2 {model_name} model download failed"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "download_sam2", "ok": ok, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=ok, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_download_videomama(request_id: str) -> None:
    cmd_name = "model.download_videomama"
    try:
        # Import setup_models dynamically like EZ UI does
        import importlib.util
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)
        
        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"Downloading VideoMaMa model..."})
        ok = setup_models.download_model("videomama")
        summary = "VideoMaMa model downloaded successfully" if ok else "VideoMaMa model download failed"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "download_videomama", "ok": ok, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=ok, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_check_status(request_id: str) -> None:
    cmd_name = "model.check_status"
    try:
        # Import setup_models dynamically like EZ UI does
        import importlib.util
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)
        
        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "Checking model status..."})
        # Capture the output of check_all
        import io
        from contextlib import redirect_stdout
        f = io.StringIO()
        with redirect_stdout(f):
            setup_models.check_all()
        output = f.getvalue()
        bridge_core._emit_log_lines(output, logger="model_status")
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "model_status", "ok": True, "summary": "Model status check completed"})
        bridge_core._emit_done(cmd_name, request_id, ok=True)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_is_installed(request_id: str, model_name: str) -> None:
    cmd_name = "model.is_installed"
    try:
        # Import setup_models dynamically like EZ UI does
        import importlib.util
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)
        
        if model_name == "sam2":
            # For SAM2, check if base-plus is installed
            installed = setup_models.is_sam2_installed("base-plus")
        else:
            installed = setup_models.is_installed(model_name)
        
        summary = f"{model_name} is {'installed' if installed else 'not installed'}"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "is_installed", "ok": installed, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=installed, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


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


def _run_alpha_gvm_hint(
    request_id: str,
    clip_root: str,
    frames_dir: str,
    overwrite: bool,
) -> None:
    cmd_name = "alpha.gvm_hint"
    success = False
    stderr_path = ""
    stderr_file = None
    try:
        # Log immediately to a temp file to capture early errors
        clip_root_raw = clip_root
        frames_dir_raw = frames_dir
        clip_root = os.path.abspath((clip_root or "").strip())
        frames_dir = os.path.abspath((frames_dir or "").strip())
        
        # Create stderr early so we can log the startup sequence
        bridge_tmp_dir_early = os.path.join(clip_root, ".bridge_tmp") if clip_root else ""
        if bridge_tmp_dir_early:
            try:
                os.makedirs(bridge_tmp_dir_early, exist_ok=True)
                stderr_path = os.path.join(bridge_tmp_dir_early, f"corridorkey_gvm_stderr_{request_id}.log")
                stderr_file = open(stderr_path, "w", encoding="utf-8", buffering=1)
                stderr_file.write(f"[START] GVM hint request_id={request_id}\n")
                stderr_file.write(f"[ARGS] clip_root_raw={clip_root_raw!r}\n")
                stderr_file.write(f"[ARGS] frames_dir_raw={frames_dir_raw!r}\n")
                stderr_file.write(f"[ARGS] overwrite={overwrite}\n")
                stderr_file.write(f"[RESOLVED] clip_root={clip_root}\n")
                stderr_file.write(f"[RESOLVED] frames_dir={frames_dir}\n")
                stderr_file.flush()
            except Exception as e:
                bridge_core._emit({"type": "log", "level": "WARNING", "logger": "unity_bridge", "message": f"GVM: could not open early stderr log: {e}"})
        
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
        frame_files = [n for n in os.listdir(frames_dir) if n.lower().endswith((".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp"))]
        if not frame_files:
            msg = f"frames_dir has no image frames: {frames_dir}"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        alpha_dir = os.path.join(clip_root, "AlphaHint")
        if os.path.isdir(alpha_dir):
            existing_alpha = [n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]
            if existing_alpha and not overwrite:
                msg = f"AlphaHint already has {len(existing_alpha)} PNGs; set overwrite=true to replace"
                bridge_core._emit({"type": "error", "message": msg})
                bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
                return
            if existing_alpha and overwrite:
                shutil.rmtree(alpha_dir, ignore_errors=True)
        os.makedirs(alpha_dir, exist_ok=True)

        bridge_core._emit(
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
        
        # stderr_path already set at function start, reuse it
        if not stderr_path:
            stderr_path = os.path.join(bridge_tmp_dir, f"corridorkey_gvm_stderr_{request_id}.log")
        
        result_path = os.path.join(bridge_tmp_dir, f"corridorkey_gvm_result_{request_id}.json")
        status_path = os.path.join(bridge_tmp_dir, f"corridorkey_gvm_status_{request_id}.json")
        runner_path = os.path.join(os.path.dirname(__file__), "gvm_hint_runner.py")
        
        # Close the early stderr file so the pump thread can open it
        if stderr_file:
            stderr_file.write(f"[INFO] closing early file handle for pump thread\n")
            stderr_file.flush()
            stderr_file.close()
            stderr_file = None
        
        try:
            if os.path.exists(result_path):
                os.remove(result_path)
            if os.path.exists(status_path):
                os.remove(status_path)
            # Don't delete stderr_path - we need it for diagnostics
        except Exception:
            pass
        if not os.path.isfile(runner_path):
            raise RuntimeError(f"runner script not found: {runner_path}")
        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "gvm stage: start runner subprocess"})
        popen_kw: dict = {
            "stdout": subprocess.DEVNULL,
            "stdin": subprocess.DEVNULL,
            "stderr": subprocess.PIPE,
            "text": True,
            "encoding": "utf-8",
            "errors": "replace",
            "bufsize": 1,
            "cwd": os.getcwd(),
        }
        if sys.platform == "win32":
            popen_kw["creationflags"] = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        
        # Log subprocess startup details to stderr before launching
        try:
            with open(stderr_path, "a", encoding="utf-8", buffering=1) as log:
                log.write(f"[INFO] about to launch subprocess\n")
                log.write(f"[INFO] runner_path={runner_path}\n")
                log.write(f"[INFO] sys.executable={sys.executable}\n")
                log.write(f"[INFO] cwd={os.getcwd()}\n")
                log.write(f"[INFO] args: clip_root={clip_root}\n")
                log.write(f"[INFO] args: frames_dir={frames_dir}\n")
                log.write(f"[INFO] args: alpha_dir={alpha_dir}\n")
                log.write(f"[INFO] args: result_path={result_path}\n")
                log.write(f"[INFO] args: status_path={status_path}\n")
                log.flush()
        except Exception as e:
            bridge_core._emit({"type": "log", "level": "WARNING", "logger": "unity_bridge", "message": f"GVM: could not log subprocess startup: {e}"})
        
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
            ],
            **popen_kw,
        )
        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"gvm runner pid={proc.pid} script={runner_path}; stderr -> {stderr_path}"})

        def _pump_gvm_stderr() -> None:
            try:
                with open(stderr_path, "a", encoding="utf-8", buffering=1) as out:
                    out.write(f"[INFO] unity_bridge: gvm stderr pump started (request_id={request_id})\n")
                    if proc.stderr is not None:
                        for line in proc.stderr:
                            out.write(line)
                            out.flush()
                    out.write(f"[INFO] stderr pump: subprocess stderr closed\n")
                    out.flush()
            except Exception as exc:
                try:
                    with open(stderr_path, "a", encoding="utf-8", errors="replace") as out:
                        out.write(f"[ERROR] unity_bridge: stderr pump exception: {exc!r}\n")
                        import traceback
                        out.write(f"{traceback.format_exc()}\n")
                        out.flush()
                except Exception:
                    pass
            finally:
                try:
                    if proc.stderr is not None:
                        proc.stderr.close()
                except Exception:
                    pass

        threading.Thread(target=_pump_gvm_stderr, daemon=True, name="gvm-stderr-pump").start()

        waited = 0.0
        poll_s = 1.0
        heartbeat_log_s = 20.0
        last_status_emit = ""
        last_progress_emit = (-1, -1, "")
        since_status_log = 0.0
        last_count = -1
        timeout_s = 900.0  # DISABLED: GPU inference is too slow on limited VRAM
        while True:
            curr = len([n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]) if os.path.isdir(alpha_dir) else 0
            if curr != last_count:
                last_count = curr
                bridge_core._emit({"type": "progress", "request_id": request_id, "current": curr, "total": total_hint, "phase": "gvm_hint", "detail": f"{curr}/{total_hint} alpha PNG(s) on disk"})

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
                            bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"GVM [{stage}]: {detail}" if detail else f"GVM [{stage}]"})
                        sc = st.get("current")
                        stt = st.get("total")
                        if isinstance(sc, int) and isinstance(stt, int) and stt > 0:
                            if (sc, stt, stage) != last_progress_emit:
                                last_progress_emit = (sc, stt, stage)
                                bridge_core._emit({"type": "progress", "request_id": request_id, "current": sc, "total": stt, "phase": f"gvm_{stage}", "detail": detail or f"frame {sc}/{stt}"})
                except json.JSONDecodeError as exc:
                    bridge_core._emit({"type": "log", "level": "WARNING", "logger": "unity_bridge", "message": f"GVM status JSON invalid: {exc}; path={status_path}"})
                except Exception as exc:
                    bridge_core._emit({"type": "log", "level": "WARNING", "logger": "unity_bridge", "message": f"GVM status read failed: {exc}; path={status_path}"})

            if os.path.isfile(result_path):
                with open(result_path, "r", encoding="utf-8") as rf:
                    result = json.load(rf)
                if not result.get("ok", False):
                    raise RuntimeError(str(result.get("message") or "gvm runner failed"))
                alpha_count = int(result.get("alpha_count", curr))
                bridge_core._emit({"type": "progress", "request_id": request_id, "current": alpha_count, "total": total_hint, "phase": "gvm_hint", "detail": f"{alpha_count}/{total_hint} alpha PNG(s) on disk"})
                bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "gvm_hint", "ok": True, "summary": str(result.get("message") or f"GVM wrote {alpha_count} alpha hint frame(s)")})
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

            threading.Event().wait(poll_s)
            waited += poll_s
            since_status_log += poll_s
            if since_status_log >= heartbeat_log_s:
                since_status_log = 0.0
                bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"GVM still running {int(waited)}s (no/few PNGs yet); check stderr log {stderr_path}"})
            # DISABLED: timeout commented out for slow GPU with limited VRAM
            # if waited >= timeout_s:
            #     proc.kill()
            #     raise RuntimeError(f"run_gvm timed out after {int(timeout_s)}s")

        bridge_core._emit(
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
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "gvm_hint",
                "ok": True,
                "summary": f"GVM wrote {alpha_count} alpha hint frame(s) to {alpha_dir}",
            }
        )
        bridge_core._emit_done(cmd_name, request_id, ok=True, summary=f"alpha_hint_frames={alpha_count}")
        success = True
    except Exception as exc:  # noqa: BLE001
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))
    finally:
        # Preserve temp artifacts on failure for post-mortem debugging.
        # Always keep stderr for diagnostics.
        if success:
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
        else:
            if 'bridge_tmp_dir' in locals() and os.path.isdir(bridge_tmp_dir):
                bridge_core._emit(
                    {
                        "type": "log",
                        "level": "INFO",
                        "logger": "unity_bridge",
                        "message": f"GVM failure artifacts preserved in {bridge_tmp_dir}",
                    }
                )
        
        # Close any open stderr file handle
        if stderr_file:
            try:
                stderr_file.close()
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
        frame_files = [n for n in os.listdir(frames_dir) if n.lower().endswith((".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp"))]
        if not frame_files:
            msg = f"frames_dir has no image frames: {frames_dir}"
            bridge_core._emit({"type": "error", "message": msg})
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
            return

        alpha_dir = os.path.join(clip_root, "AlphaHint")
        if os.path.isdir(alpha_dir):
            existing_alpha = [n for n in os.listdir(alpha_dir) if n.lower().endswith(".png")]
            if existing_alpha and not overwrite:
                msg = f"AlphaHint already has {len(existing_alpha)} PNGs; set overwrite=true to replace"
                bridge_core._emit({"type": "error", "message": msg})
                bridge_core._emit_done(cmd_name, request_id, ok=False, summary=msg)
                return
            if existing_alpha and overwrite:
                shutil.rmtree(alpha_dir, ignore_errors=True)
        os.makedirs(alpha_dir, exist_ok=True)

        bridge_core._emit(
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
        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "birefnet stage: start runner subprocess"})
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
        bridge_core._emit(
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
                bridge_core._emit(
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
                            bridge_core._emit(
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
                                bridge_core._emit(
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
                                bridge_core._emit(
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
                        bridge_core._birefnet_append_stderr(
                            str(result.get("message") or "birefnet runner failed"),
                            stderr_path,
                        )
                    )
                alpha_count = int(result.get("alpha_count", curr))
                bridge_core._emit(
                    {
                        "type": "progress",
                        "request_id": request_id,
                        "current": alpha_count,
                        "total": total_hint,
                        "phase": "birefnet_hint",
                        "detail": f"{alpha_count}/{total_hint} alpha PNG(s) on disk",
                    }
                )
                bridge_core._emit(
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
                            bridge_core._birefnet_append_stderr(
                                str(result.get("message") or f"birefnet runner exited with code {code}"),
                                stderr_path,
                            )
                        )
                    raise RuntimeError(
                        bridge_core._birefnet_append_stderr(
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
                    bridge_core._birefnet_append_stderr(
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
                    bridge_core._emit(
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
                bridge_core._emit(
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
                    bridge_core._birefnet_append_stderr(
                        f"run_birefnet timed out after {int(timeout_s)}s (check stderr log and run diag.birefnet)",
                        stderr_path,
                    )
                )

        bridge_core._emit(
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
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "birefnet_hint",
                "ok": True,
                "summary": f"BiRefNet wrote {alpha_count} alpha hint frame(s) to {alpha_dir}",
            }
        )
        bridge_core._emit_done(cmd_name, request_id, ok=True, summary=f"alpha_hint_frames={alpha_count}")
    except Exception as exc:  # noqa: BLE001
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))
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
            target=_run_media_extract_frames,
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
            target=_run_alpha_gvm_hint,
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
            target=_run_alpha_birefnet_hint,
            args=(rid, clip_root, frames_dir, usage, overwrite),
            daemon=True,
        ).start()
        return True
    if cmd == "model.download_gvm":
        threading.Thread(target=_run_model_download_gvm, args=(rid,), daemon=True).start()
        return True
    if cmd == "model.download_sam2":
        model_name = msg.get("model_name") or "base-plus"
        threading.Thread(target=_run_model_download_sam2, args=(rid, model_name), daemon=True).start()
        return True
    if cmd == "model.download_videomama":
        threading.Thread(target=_run_model_download_videomama, args=(rid,), daemon=True).start()
        return True
    if cmd == "model.check_status":
        threading.Thread(target=_run_model_check_status, args=(rid,), daemon=True).start()
        return True
    if cmd == "model.is_installed":
        model_name = msg.get("model_name") or ""
        threading.Thread(target=_run_model_is_installed, args=(rid, model_name), daemon=True).start()
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
