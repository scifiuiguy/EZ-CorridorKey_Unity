"""
Core infrastructure for Unity ↔ EZ-CorridorKey stdio bridge.

Shared utilities for NDJSON protocol, logging, and file operations.
"""
from __future__ import annotations

import json
import os

BRIDGE_VERSION = "unity_bridge_birefnet_v4"


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