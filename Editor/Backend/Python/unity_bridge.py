"""
Unity ↔ EZ-CorridorKey stdio bridge (NDJSON).

Run with EZ repo as working directory and the same Python interpreter EZ uses
(e.g. .venv\\Scripts\\python.exe) so `import backend` resolves.

Protocol (stdin, one JSON object per line):
  {"cmd":"health"}
  {"cmd":"shutdown"}

Protocol (stdout, one JSON object per line, flush after each line):
  {"type":"log","level":"INFO|WARNING|ERROR","message":"...","logger":"optional"}
  {"type":"health","ok":true|false,"summary":"..."}
  {"type":"done","cmd":"health"|"shutdown"}
"""
from __future__ import annotations

import json
import sys
import traceback


def _emit(obj: dict) -> None:
    # Compact JSON: Unity JsonUtility is picky about some whitespace-heavy payloads.
    print(json.dumps(obj, ensure_ascii=False, separators=(",", ":")), flush=True)


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


def main() -> None:
    _emit(
        {
            "type": "log",
            "level": "INFO",
            "message": "unity_bridge started (stdio NDJSON).",
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

        cmd = msg.get("cmd")
        if cmd == "health":
            _cmd_health()
            _emit({"type": "done", "cmd": "health"})
        elif cmd == "shutdown":
            _emit(
                {
                    "type": "log",
                    "level": "INFO",
                    "message": "shutdown received.",
                    "logger": "unity_bridge",
                }
            )
            _emit({"type": "done", "cmd": "shutdown"})
            break
        else:
            _emit(
                {
                    "type": "log",
                    "level": "WARNING",
                    "message": f"Unknown cmd: {cmd!r}",
                    "logger": "unity_bridge",
                }
            )


if __name__ == "__main__":
    main()
