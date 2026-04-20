"""
Model management commands for Unity ↔ EZ-CorridorKey stdio bridge.

Handles downloading and checking status of ML models (GVM, SAM2, VideoMaMa).
"""
from __future__ import annotations

import importlib.util
import io
import os
import shutil
import subprocess
import sys
import threading
import time
from contextlib import redirect_stdout
from pathlib import Path
from typing import TextIO

try:
    from . import bridge_core
except ImportError:
    import bridge_core


def _all_setup_models_script_paths() -> list[str]:
    """Ordered candidates for setup_models.py.

    - Walk parents of this file so we still find ``scripts/setup_models.py`` when process ``cwd`` is wrong.
    - Under a shared parent, only ``R/scripts`` is considered (Unity package + wizard: single runtime tree).
    - Then ``CORRIDORKEY_EZ_ROOT`` and ``cwd`` (must contain the script + pyproject).
    """
    paths: list[str] = []
    seen: set[str] = set()

    def add(p: str) -> None:
        ap = os.path.abspath(p)
        if ap not in seen:
            seen.add(ap)
            paths.append(ap)

    # Sibling R folder under a workspace parent, or scripts/ when cwd is already the R tree.
    rel_checks = (
        ("R", "scripts", "setup_models.py"),
        ("scripts", "setup_models.py"),
    )

    d = os.path.dirname(os.path.abspath(__file__))
    for _ in range(18):
        for parts in rel_checks:
            add(os.path.join(d, *parts))
        parent = os.path.dirname(d)
        if parent == d:
            break
        d = parent

    roots: list[str] = []
    env_root = (os.environ.get("CORRIDORKEY_EZ_ROOT") or "").strip()
    if env_root:
        roots.append(os.path.abspath(env_root))
    roots.append(os.path.abspath(os.getcwd()))
    for root in roots:
        for parts in rel_checks:
            add(os.path.join(root, *parts))
    return paths


def _load_setup_models_module():
    """Load setup_models like EZ's setup wizard (dynamic import from checkout tree)."""
    tried = _all_setup_models_script_paths()
    for script_path in tried:
        if not os.path.isfile(script_path):
            continue
        spec = importlib.util.spec_from_file_location("corridorkey_setup_models", script_path)
        if spec is None or spec.loader is None:
            continue
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod
    cwd = os.path.abspath(os.getcwd())
    raise RuntimeError(
        "setup_models.py not found. Set Backend Working Directory or CORRIDORKEY_EZ_ROOT to the full "
        "R app tree — folder that contains scripts/setup_models.py "
        f"and pyproject.toml. cwd={cwd!r} tried={tried!r}"
    )


def _call_setup_models_with_captured_prints(fn, /, *args, **kwargs):
    """``scripts/setup_models.py`` uses ``print()``; bridge stdout must stay NDJSON-only."""
    buf = io.StringIO()
    with redirect_stdout(buf):
        result = fn(*args, **kwargs)
    text = buf.getvalue()
    if text.strip():
        bridge_core._emit_log_lines(text.strip(), logger="unity_bridge")
    return result


def _ez_repo_root_from_setup_models(setup_models) -> str:
    """Parent of ``scripts/`` — the directory used as ``cwd`` for ``pip install -e .[tracker]``."""
    return str(Path(setup_models.__file__).resolve().parents[1])


def _is_corridorkey_editable_install_root(repo_root: str) -> bool:
    """True if ``repo_root`` looks like a full R app tree with optional ``[tracker]`` in pyproject."""
    path = os.path.join(repo_root, "pyproject.toml")
    if not os.path.isfile(path):
        return False
    try:
        text = Path(path).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return False
    if 'name = "corridorkey"' not in text and "name='corridorkey'" not in text:
        return False
    return "[project.optional-dependencies]" in text and "tracker" in text


def _tracker_install_timeout_s() -> int:
    """Wall-clock cap for ``uv``/``pip`` tracker install subprocess (watchdog kill). Override with env."""
    raw = (os.environ.get("CORRIDORKEY_TRACKER_INSTALL_TIMEOUT_S") or "").strip()
    if not raw:
        return 3600
    try:
        v = int(raw)
    except ValueError:
        return 3600
    return max(600, min(v, 48 * 3600))


def _tracker_install_verbose_flags() -> tuple[list[str], list[str]]:
    """Extra argv tokens: (uv_flags_after ``install``, pip_flags_after ``pip``).

    Env ``CORRIDORKEY_TRACKER_INSTALL_VERBOSE`` (default ``0``): 0=none, 1=``-v``, 2=``-vv`` (``uv`` and ``pip``).
    """
    raw = (os.environ.get("CORRIDORKEY_TRACKER_INSTALL_VERBOSE") or "0").strip().lower()
    try:
        n = int(raw) if raw.isdigit() else 2
    except ValueError:
        n = 2
    n = max(0, min(n, 2))
    if n == 0:
        return [], []
    if n == 1:
        return ["-v"], ["-v"]
    return ["-vv"], ["-vv"]


def _tracker_install_session_log_path(ez_root: str, request_id: str) -> str:
    """Plain-text capture path (one file per ``model.prepare_sam2_track`` ``request_id``)."""
    safe = "".join(c for c in request_id if c.isalnum())[:48] or "norid"
    d = os.path.join(ez_root, ".bridge_tmp", "tracker_install_logs")
    return os.path.join(d, f"tracker_install_{safe}.log")


def _emit_tracker_install_env_diagnostics(
    request_id: str, ez_root: str, session_log: TextIO | None = None
) -> None:
    """One-shot context so hangs are reproducible outside Unity (paths, versions)."""
    uv_path = shutil.which("uv") or ""
    pip_ver = ""
    try:
        r = subprocess.run(
            [sys.executable, "-m", "pip", "--version"],
            cwd=ez_root,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=60,
        )
        pip_ver = (r.stdout or r.stderr or "").strip()
    except Exception as exc:
        pip_ver = f"(pip --version failed: {exc})"
    uv_ver = ""
    if uv_path:
        try:
            r = subprocess.run(
                [uv_path, "--version"],
                cwd=ez_root,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=60,
            )
            uv_ver = (r.stdout or r.stderr or "").strip()
        except Exception as exc:
            uv_ver = f"(uv --version failed: {exc})"
    msg = (
        f"tracker_install env: cwd={ez_root!r}\n"
        f"sys.executable={sys.executable!r}\n"
        f"sys.version={sys.version.splitlines()[0]!r}\n"
        f"uv which={uv_path!r}\n"
        f"uv version={uv_ver!r}\n"
        f"pip version={pip_ver!r}\n"
        f"timeout_s={_tracker_install_timeout_s()} (set CORRIDORKEY_TRACKER_INSTALL_TIMEOUT_S to change)\n"
        f"verbosity level from CORRIDORKEY_TRACKER_INSTALL_VERBOSE (0-2, default 0)"
    )
    bridge_core._emit_log_lines(msg, logger="unity_bridge", request_id=request_id, level="INFO")
    if session_log is not None:
        try:
            session_log.write("# env diagnostics\n" + msg + "\n")
            session_log.flush()
        except Exception:
            pass


def _emit_subprocess_stdout_tail(
    label: str,
    lines: list[str],
    request_id: str,
    *,
    reason: str,
    max_lines: int = 60,
    max_chars: int = 14000,
) -> None:
    if not lines:
        bridge_core._emit(
            {
                "type": "log",
                "level": "WARNING",
                "logger": "unity_bridge",
                "message": f"{label}: {reason} (no subprocess stdout captured yet)",
                "request_id": request_id,
            }
        )
        return
    tail_lines = lines[-max_lines:]
    text = "\n".join(tail_lines)
    if len(text) > max_chars:
        text = text[-max_chars:]
    header = f"{label}: {reason} - tail of merged stdout/stderr ({len(tail_lines)} line(s)):\n"
    bridge_core._emit_log_lines(header + text, logger="unity_bridge", request_id=request_id, level="WARNING")


def _emit_setup_progress(
    request_id: str,
    current: int,
    total: int = 100,
    *,
    phase: str = "sam2_setup",
    detail: str | None = None,
) -> None:
    bridge_core._emit(
        {
            "type": "progress",
            "request_id": request_id,
            "current": max(0, current),
            "total": max(1, total),
            "phase": phase,
            "detail": detail or phase,
        }
    )


def _clear_stale_sam2_modules() -> None:
    """After ``pip install``, drop failed/partial ``sam2`` entries so ``import sam2`` retries."""
    import importlib

    for name in list(sys.modules):
        if name == "sam2" or name.startswith("sam2."):
            del sys.modules[name]
    importlib.invalidate_caches()


TRACKER_REQUIREMENT = "sam-2 @ git+https://github.com/facebookresearch/sam2.git"


def _run_subprocess_streaming_with_heartbeat(
    argv: list[str],
    *,
    cwd: str,
    label: str,
    request_id: str,
    heartbeat_s: float = 15.0,
    timeout_s: int = 3600,
    session_log: TextIO | None = None,
    stream_to_bridge: bool = True,
    progress_range: tuple[int, int] | None = None,
) -> tuple[int, str]:
    """Run subprocess: stream stdout to NDJSON logs, emit heartbeat if no exit yet (uv/pip can be silent)."""
    argv_preview = repr(argv)
    if len(argv_preview) > 4000:
        argv_preview = argv_preview[:4000] + "... (truncated)"
    if session_log is not None:
        try:
            session_log.write(f"\n### {label} launch pid-will-follow cwd={cwd!r}\n{argv_preview}\n")
            session_log.flush()
        except Exception:
            pass
    bridge_core._emit(
        {
            "type": "log",
            "level": "INFO",
            "logger": "unity_bridge",
            "message": f"{label}: launching subprocess argv={argv_preview} cwd={cwd!r} watchdog_timeout_s={timeout_s}",
            "request_id": request_id,
        }
    )
    proc = subprocess.Popen(
        argv,
        cwd=cwd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if session_log is not None:
        try:
            session_log.write(f"### {label} child pid={proc.pid}\n")
            session_log.flush()
        except Exception:
            pass
    collected: list[str] = []
    lock = threading.Lock()

    def _reader() -> None:
        if proc.stdout is None:
            return
        try:
            for line in proc.stdout:
                s = line.rstrip("\r\n")
                if not s:
                    continue
                with lock:
                    collected.append(s)
                    if session_log is not None:
                        try:
                            session_log.write(f"{label}\t{s}\n")
                            session_log.flush()
                        except Exception:
                            pass
                if stream_to_bridge:
                    bridge_core._emit(
                        {
                            "type": "log",
                            "level": "INFO",
                            "logger": "unity_bridge",
                            "message": f"{label}: {s}",
                            "request_id": request_id,
                        }
                    )
        except Exception:
            pass

    th = threading.Thread(target=_reader, name=f"bridge-{label}", daemon=True)
    th.start()
    t0 = time.monotonic()
    rc: int | None = None
    killed_for_timeout = False
    try:
        while True:
            try:
                rc = proc.wait(timeout=heartbeat_s)
                break
            except subprocess.TimeoutExpired:
                elapsed = int(time.monotonic() - t0)
                if elapsed >= timeout_s:
                    with lock:
                        snap = list(collected)
                    _emit_subprocess_stdout_tail(
                        label,
                        snap,
                        request_id,
                        reason=f"watchdog: no exit after {timeout_s}s (set CORRIDORKEY_TRACKER_INSTALL_TIMEOUT_S to raise this cap)",
                    )
                    bridge_core._emit(
                        {
                            "type": "log",
                            "level": "WARNING",
                            "logger": "unity_bridge",
                            "message": (
                                f"{label}: killing pid={proc.pid} after {timeout_s}s. "
                                "Re-run the same argv in a terminal under the same cwd to reproduce."
                            ),
                            "request_id": request_id,
                        }
                    )
                    proc.kill()
                    killed_for_timeout = True
                    if session_log is not None:
                        try:
                            session_log.write(
                                f"\n### {label} watchdog killed pid={proc.pid} after {timeout_s}s\n"
                            )
                            session_log.flush()
                        except Exception:
                            pass
                    try:
                        proc.wait(timeout=60)
                    except Exception:
                        pass
                    rc = -1
                    break
                bridge_core._emit(
                    {
                        "type": "log",
                        "level": "INFO",
                        "logger": "unity_bridge",
                        "message": f"{label}: still running ({elapsed}s, pid={proc.pid})",
                        "request_id": request_id,
                    }
                )
                if progress_range is not None:
                    lo, hi = progress_range
                    span = max(1, hi - lo)
                    # Smooth heartbeat progress for long-running subprocesses without parsing verbose child output.
                    pct = min(0.95, elapsed / 120.0)
                    cur = lo + int(span * pct)
                    _emit_setup_progress(
                        request_id,
                        min(max(lo, cur), max(lo, hi - 1)),
                        detail=f"{label} running ({elapsed}s)",
                    )
    finally:
        th.join(timeout=120)
    if rc is None:
        rc = -1
    lvl = "INFO" if rc == 0 else "WARNING"
    bridge_core._emit(
        {
            "type": "log",
            "level": lvl,
            "logger": "unity_bridge",
            "message": (
                f"{label}: finished with exit code {rc} (cwd={cwd!r}"
                + (", killed_by_watchdog=True" if killed_for_timeout else "")
                + ")"
            ),
            "request_id": request_id,
        }
    )
    with lock:
        blob = "\n".join(collected)
    if progress_range is not None and rc == 0:
        _emit_setup_progress(request_id, progress_range[1], detail=f"{label} complete")
    if session_log is not None:
        try:
            session_log.write(
                f"\n### {label} finished exit_code={rc} killed_by_watchdog={killed_for_timeout} cwd={cwd!r}\n"
            )
            session_log.flush()
        except Exception:
            pass
    return rc, blob


def _install_sam2_tracker_editable(ez_root: str, request_id: str) -> tuple[bool, str]:
    """Install only SAM2 tracker dependency into the active bridge venv."""
    pyproject = os.path.join(ez_root, "pyproject.toml")
    if not os.path.isfile(pyproject):
        return False, f"Cannot install tracker: missing pyproject.toml under {ez_root!r}"
    if not _is_corridorkey_editable_install_root(ez_root):
        return (
            False,
            f"Install root {ez_root!r} is not a full CorridorKey R tree (need pyproject.toml with "
            "[project.optional-dependencies]). Run the wizard / 1-install on R; point Unity's Python "
            "at R\\.venv when needed.",
        )

    extra = (
        os.environ.get("CORRIDORKEY_PIP_EXTRA_INDEX_URL")
        or os.environ.get("PIP_EXTRA_INDEX_URL")
        or ""
    ).strip()
    _emit_setup_progress(request_id, 5, detail="Preparing SAM2 setup...")

    log_path = _tracker_install_session_log_path(ez_root, request_id)
    session_log: TextIO | None = None
    try:
        os.makedirs(os.path.dirname(log_path), exist_ok=True)
        session_log = open(log_path, "w", encoding="utf-8", errors="replace", buffering=1)
        session_log.write(
            "# CorridorKey: full merged stdout/stderr for tracker install\n"
            f"# request_id={request_id}\n"
            "# Each line: LABEL<TAB>one subprocess stdout/stderr line\n\n"
        )
        session_log.flush()
        bridge_core._emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": f"Tracker install full subprocess log: {os.path.abspath(log_path)}",
                "request_id": request_id,
            }
        )
    except OSError as exc:
        bridge_core._emit(
            {
                "type": "log",
                "level": "WARNING",
                "logger": "unity_bridge",
                "message": f"Could not open tracker install log file {log_path!r}: {exc}",
                "request_id": request_id,
            }
        )
        session_log = None

    try:
        _emit_tracker_install_env_diagnostics(request_id, ez_root, session_log)
        uv_flags, pip_flags = _tracker_install_verbose_flags()
        install_timeout = _tracker_install_timeout_s()

        uv = shutil.which("uv")
        if uv:
            argv = [
                uv,
                "pip",
                "install",
                *uv_flags,
                "--python",
                sys.executable,
                "--torch-backend=auto",
                TRACKER_REQUIREMENT,
            ]
            bridge_core._emit(
                {
                    "type": "log",
                    "level": "INFO",
                    "logger": "unity_bridge",
                    "message": f"Installing SAM2 tracker via uv (cwd={ez_root})...",
                    "request_id": request_id,
                }
            )
            _emit_setup_progress(request_id, 15, detail="Installing tracker dependency (uv)...")
            code, _out = _run_subprocess_streaming_with_heartbeat(
                argv,
                cwd=ez_root,
                label="uv[tracker]",
                request_id=request_id,
                timeout_s=install_timeout,
                session_log=session_log,
                stream_to_bridge=False,
                progress_range=(15, 70),
            )
            if code == 0:
                _clear_stale_sam2_modules()
                return True, ""
            if _out.strip():
                _emit_subprocess_stdout_tail(
                    "uv[tracker]",
                    _out.splitlines(),
                    request_id,
                    reason=f"uv finished with exit code {code} (see lines above for streamed output; tail repeat below)",
                    max_lines=40,
                )
            bridge_core._emit(
                {
                    "type": "log",
                    "level": "WARNING",
                    "logger": "unity_bridge",
                    "message": f"uv pip install tracker dep failed (exit {code}); falling back to python -m pip...",
                    "request_id": request_id,
                }
            )

        if extra:
            argv = [
                sys.executable,
                "-m",
                "pip",
                *pip_flags,
                "install",
                "--extra-index-url",
                extra,
                TRACKER_REQUIREMENT,
            ]
        else:
            argv = [sys.executable, "-m", "pip", *pip_flags, "install", TRACKER_REQUIREMENT]
        bridge_core._emit(
            {
                "type": "log",
                "level": "INFO",
                "logger": "unity_bridge",
                "message": f"Installing SAM2 tracker via pip (cwd={ez_root})...",
                "request_id": request_id,
            }
        )
        _emit_setup_progress(request_id, 20, detail="Installing tracker dependency (pip)...")
        code, _out = _run_subprocess_streaming_with_heartbeat(
            argv,
            cwd=ez_root,
            label="pip[tracker]",
            request_id=request_id,
            timeout_s=install_timeout,
            session_log=session_log,
            stream_to_bridge=False,
            progress_range=(20, 70),
        )
        _clear_stale_sam2_modules()
        if code != 0:
            if _out.strip():
                _emit_subprocess_stdout_tail(
                    "pip[tracker]",
                    _out.splitlines(),
                    request_id,
                    reason=f"pip finished with exit code {code}",
                    max_lines=50,
                )
            return False, f"pip install tracker dependency failed with exit code {code}"
        return True, ""
    finally:
        if session_log is not None:
            try:
                session_log.write("\n# _install_sam2_tracker_editable finished (file closed)\n")
                session_log.flush()
                session_log.close()
            except Exception:
                pass


def _run_model_prepare_sam2_track(request_id: str, model_name: str = "base-plus") -> None:
    """Ensure SAM2 Base+ weights + Meta ``sam2`` package (EZ ``[tracker]`` extra), like the setup wizard / installer."""
    cmd_name = "model.prepare_sam2_track"
    try:
        setup_models = _load_setup_models_module()
        ez_root = _ez_repo_root_from_setup_models(setup_models)
        _emit_setup_progress(request_id, 1, detail="Preparing SAM2 setup...")

        # If sam2 was uninstalled while the bridge process stayed alive, stale entries in sys.modules can
        # make import checks incorrectly pass. Clear before every tracker installed check.
        _clear_stale_sam2_modules()
        if not setup_models.tracker_dependency_installed():
            ok, err = _install_sam2_tracker_editable(ez_root, request_id)
            if not ok:
                bridge_core._emit_done(cmd_name, request_id, ok=False, summary=err)
                return
            if not setup_models.tracker_dependency_installed():
                bridge_core._emit_done(
                    cmd_name,
                    request_id,
                    ok=False,
                    summary="SAM2 Python package still not importable after install; check bridge venv is EZ .venv",
                )
                return
        else:
            _emit_setup_progress(request_id, 70, detail="Tracker dependency already installed")

        if not setup_models.is_sam2_installed(model_name):
            bridge_core._emit(
                {
                    "type": "log",
                    "level": "INFO",
                    "logger": "unity_bridge",
                    "message": f"Downloading SAM2 {model_name} weights...",
                }
            )
            _emit_setup_progress(request_id, 72, detail=f"Downloading SAM2 {model_name} weights...")
            done = threading.Event()
            box: dict[str, object] = {"ok": False}

            def _dl_worker() -> None:
                try:
                    box["ok"] = _call_setup_models_with_captured_prints(setup_models.download_sam2_model, model_name)
                finally:
                    done.set()

            threading.Thread(target=_dl_worker, name="sam2-weight-download", daemon=True).start()
            tick = 0
            while not done.wait(timeout=1.0):
                tick += 1
                cur = min(95, 72 + tick // 2)
                _emit_setup_progress(request_id, cur, detail=f"Downloading SAM2 {model_name} weights... ({tick}s)")
            dl_ok = bool(box.get("ok"))
            if not dl_ok:
                bridge_core._emit_done(
                    cmd_name,
                    request_id,
                    ok=False,
                    summary=f"SAM2 {model_name} weight download failed",
                )
                return

        if not setup_models.is_sam2_installed(model_name):
            bridge_core._emit_done(cmd_name, request_id, ok=False, summary="SAM2 weights still missing after download")
            return

        _emit_setup_progress(request_id, 100, detail="SAM2 setup complete")
        bridge_core._emit_done(
            cmd_name,
            request_id,
            ok=True,
            summary="SAM2 tracker package and weights ready",
        )
    except subprocess.TimeoutExpired:
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary="SAM2 setup timed out (pip/download > 3600s)")
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_download_gvm(request_id: str) -> None:
    cmd_name = "model.download_gvm"
    try:
        setup_models = _load_setup_models_module()

        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "Downloading GVM model..."})
        ok = _call_setup_models_with_captured_prints(setup_models.download_model, "gvm")
        summary = "GVM model downloaded successfully" if ok else "GVM model download failed"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "download_gvm", "ok": ok, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=ok, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_download_sam2(request_id: str, model_name: str = "base-plus") -> None:
    cmd_name = "model.download_sam2"
    try:
        setup_models = _load_setup_models_module()

        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": f"Downloading SAM2 {model_name} model..."})
        dl_ok = _call_setup_models_with_captured_prints(setup_models.download_sam2_model, model_name)
        ok = bool(dl_ok)
        summary = f"SAM2 {model_name} model downloaded successfully" if ok else f"SAM2 {model_name} model download failed"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "download_sam2", "ok": ok, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=ok, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_download_videomama(request_id: str) -> None:
    cmd_name = "model.download_videomama"
    try:
        setup_models = _load_setup_models_module()

        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "Downloading VideoMaMa model..."})
        ok = _call_setup_models_with_captured_prints(setup_models.download_model, "videomama")
        summary = "VideoMaMa model downloaded successfully" if ok else "VideoMaMa model download failed"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "download_videomama", "ok": ok, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=ok, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))


def _run_model_check_status(request_id: str) -> None:
    cmd_name = "model.check_status"
    try:
        setup_models = _load_setup_models_module()

        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "Checking model status..."})
        # Capture the output of check_all
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
        setup_models = _load_setup_models_module()

        if model_name == "sam2":
            # Weights alone are not enough; SAM2Tracker needs ``import sam2`` (EZ optional ``[tracker]`` extra).
            _clear_stale_sam2_modules()
            weights = setup_models.is_sam2_installed("base-plus")
            tracker_pkg = setup_models.tracker_dependency_installed()
            installed = bool(weights and tracker_pkg)
            if weights and not tracker_pkg:
                summary = (
                    "sam2: HuggingFace checkpoint is cached but the `sam2` Python package is missing "
                    "(install EZ optional tracker dependencies in the bridge venv)."
                )
            else:
                summary = f"{model_name} is {'installed' if installed else 'not installed'}"
        else:
            installed = setup_models.is_installed(model_name)
            summary = f"{model_name} is {'installed' if installed else 'not installed'}"
        bridge_core._emit({"type": "diag_result", "request_id": request_id, "diag": "is_installed", "ok": installed, "summary": summary})
        bridge_core._emit_done(cmd_name, request_id, ok=installed, summary=summary)
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))