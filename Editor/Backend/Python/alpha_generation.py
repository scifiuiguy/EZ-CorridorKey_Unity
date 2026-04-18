"""
Alpha generation commands for Unity ↔ EZ-CorridorKey stdio bridge.

Handles GVM and BiRefNet alpha matte generation.
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import threading

try:
    from . import bridge_core
except ImportError:
    import bridge_core

try:
    from . import media_processing
except ImportError:
    import media_processing


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
        media_processing._update_clip_json_with_alpha(clip_root, alpha_dir, alpha_count, status="gvm_hint_generated")
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
        media_processing._update_clip_json_with_alpha(clip_root, alpha_dir, alpha_count, status="birefnet_hint_generated")
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