"""
Model management commands for Unity ↔ EZ-CorridorKey stdio bridge.

Handles downloading and checking status of ML models (GVM, SAM2, VideoMaMa).
"""
from __future__ import annotations

import importlib.util
import io
import os
from contextlib import redirect_stdout

try:
    from . import bridge_core
except ImportError:
    import bridge_core


def _run_model_download_gvm(request_id: str) -> None:
    cmd_name = "model.download_gvm"
    try:
        # Import setup_models dynamically like EZ UI does
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)

        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "Downloading GVM model..."})
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
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)

        bridge_core._emit({"type": "log", "level": "INFO", "logger": "unity_bridge", "message": "Downloading VideoMaMa model..."})
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
        script_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "R", "scripts", "setup_models.py")
        if not os.path.isfile(script_path):
            raise RuntimeError(f"setup_models.py not found at {script_path}")
        spec = importlib.util.spec_from_file_location("setup_models", script_path)
        setup_models = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(setup_models)

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
        # Import setup_models dynamically like EZ UI does
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