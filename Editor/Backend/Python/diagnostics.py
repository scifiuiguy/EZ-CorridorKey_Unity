"""
Diagnostic commands for Unity ↔ EZ-CorridorKey stdio bridge.

Handles health checks, Python diagnostics, import validation, FFmpeg checks,
file existence tests, and BiRefNet diagnostics.
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import traceback

try:
    from . import bridge_core
except ImportError:
    import bridge_core


def _cmd_health() -> None:
    try:
        from backend.ffmpeg_tools.discovery import validate_ffmpeg_install

        result = validate_ffmpeg_install(require_probe=True)
        if not result.ok:
            bridge_core._emit(
                {
                    "type": "health",
                    "ok": False,
                    "summary": result.message,
                }
            )
            return

        py = f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}"
        bridge_core._emit(
            {
                "type": "health",
                "ok": True,
                "summary": f"Python {py}; {result.message}",
            }
        )
    except Exception as exc:  # noqa: BLE001 — bridge must never crash the process
        tb = traceback.format_exc()
        bridge_core._emit(
            {
                "type": "log",
                "level": "ERROR",
                "message": f"health check failed: {exc}\n{tb}",
                "logger": "unity_bridge",
            }
        )
        bridge_core._emit(
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
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "python",
                "ok": True,
                "summary": summary,
            }
        )
        bridge_core._emit_done("diag.python", request_id, ok=True)
    except Exception as exc:  # noqa: BLE001
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "python",
                "ok": False,
                "summary": str(exc),
            }
        )
        bridge_core._emit_done("diag.python", request_id, ok=False, summary=str(exc))


def _run_diag_imports(request_id: str) -> None:
    try:
        import importlib

        importlib.import_module("backend")
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "imports",
                "ok": True,
                "summary": "import backend: ok",
            }
        )
        bridge_core._emit_done("diag.imports", request_id, ok=True)
    except Exception as exc:  # noqa: BLE001
        tb = traceback.format_exc()
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "imports",
                "ok": False,
                "summary": f"{exc}\n{tb}",
            }
        )
        bridge_core._emit_done("diag.imports", request_id, ok=False, summary=str(exc))


def _run_diag_ffmpeg_version(request_id: str) -> None:
    try:
        ffmpeg = shutil.which("ffmpeg")
        if not ffmpeg:
            bridge_core._emit(
                {
                    "type": "diag_result",
                    "request_id": request_id,
                    "diag": "ffmpeg_version",
                    "ok": False,
                    "summary": "ffmpeg not found on PATH",
                }
            )
            bridge_core._emit_done("diag.ffmpeg_version", request_id, ok=False, summary="ffmpeg not found on PATH")
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
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "ffmpeg_version",
                "ok": ok,
                "summary": f"{ffmpeg} -> {first}",
            }
        )
        bridge_core._emit_done("diag.ffmpeg_version", request_id, ok=ok, summary=first if not ok else None)
    except Exception as exc:  # noqa: BLE001
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "ffmpeg_version",
                "ok": False,
                "summary": str(exc),
            }
        )
        bridge_core._emit_done("diag.ffmpeg_version", request_id, ok=False, summary=str(exc))


def _run_diag_file_exists(request_id: str, path: str) -> None:
    try:
        path = (path or "").strip()
        if not path:
            bridge_core._emit(
                {
                    "type": "diag_result",
                    "request_id": request_id,
                    "diag": "file_exists",
                    "ok": False,
                    "summary": "missing path",
                }
            )
            bridge_core._emit_done("diag.file_exists", request_id, ok=False, summary="missing path")
            return
        exists = os.path.isfile(path)
        try:
            size = os.path.getsize(path) if exists else 0
        except OSError:
            size = -1
        summary = f"path={path!r}; isfile={exists}; size={size}"
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "file_exists",
                "ok": exists,
                "summary": summary,
            }
        )
        bridge_core._emit_done("diag.file_exists", request_id, ok=exists, summary=summary if not exists else None)
    except Exception as exc:  # noqa: BLE001
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "file_exists",
                "ok": False,
                "summary": str(exc),
            }
        )
        bridge_core._emit_done("diag.file_exists", request_id, ok=False, summary=str(exc))


def _run_diag_birefnet(request_id: str, usage: str) -> None:
    bridge_core._emit(
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
        bridge_core._emit_log_lines(report, logger="diag.birefnet")
        short_summary = (
            f"ok={ok}; full checkpoint report above ({len(report)} chars in {report.count(chr(10)) + 1} lines)"
        )
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "birefnet",
                "ok": ok,
                "summary": short_summary,
            }
        )
        bridge_core._emit_done(
            "diag.birefnet",
            request_id,
            ok=ok,
            summary=None if ok else "checkpoint or torch issue — see diag.birefnet logs above",
        )
    except Exception as exc:  # noqa: BLE001
        tb = traceback.format_exc()
        bridge_core._emit_log_lines(f"{exc}\n{tb}", logger="diag.birefnet")
        bridge_core._emit(
            {
                "type": "diag_result",
                "request_id": request_id,
                "diag": "birefnet",
                "ok": False,
                "summary": f"exception: {exc}",
            }
        )
        bridge_core._emit_done("diag.birefnet", request_id, ok=False, summary=str(exc))