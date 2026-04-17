"""Fast BiRefNet checkpoint inspection — no model load, no inference.

Use from unity_bridge (diag.birefnet) or birefnet_hint_runner to fail fast when
the local snapshot under modules/BiRefNetModule/checkpoints/<repo>/ is incomplete
(e.g. partial copy, missing config.json, deleted safetensors).

Registry paths match modules/BiRefNetModule/wrapper.py (do not import wrapper here:
that module pulls in cv2/torch at import time).
"""
from __future__ import annotations

import os
from typing import Tuple

# Display name → HuggingFace repo folder under ZhengPeng7/ (keep in sync with wrapper.py)
BIREFNET_MODELS: dict[str, str] = {
    "Matting": "BiRefNet-matting",
    "Matting HR": "BiRefNet_HR-Matting",
    "Matting Lite": "BiRefNet_lite-matting",
    "Matting Dynamic": "BiRefNet_dynamic-matting",
    "General": "BiRefNet",
    "General HR": "BiRefNet_HR",
    "General Lite": "BiRefNet_lite",
    "General Lite 2K": "BiRefNet_lite-2K",
    "General Dynamic": "BiRefNet_dynamic",
    "General 512": "BiRefNet_512x512",
    "Portrait": "BiRefNet-portrait",
    "DIS": "BiRefNet-DIS5K",
    "HRSOD": "BiRefNet-HRSOD",
    "COD": "BiRefNet-COD",
    "DIS TR_TEs": "BiRefNet-DIS5K-TR_TEs",
    "General Legacy": "BiRefNet-legacy",
}
DEFAULT_MODEL = "Matting"


def _checkpoint_base_dir() -> str:
    """Same rule as wrapper.BiRefNetProcessor: <cwd>/modules/BiRefNetModule/checkpoints"""
    return os.path.abspath(
        os.path.join(os.getcwd(), "modules", "BiRefNetModule", "checkpoints")
    )


def resolve_paths(usage: str) -> tuple[str, str, str, str]:
    """Return (display_usage, repo_folder_name, local_checkpoint_dir, hf_repo_id).

    Uses `os.getcwd()` — must match the EZ/R backend working directory (same as the bridge).
    """
    u = (usage or "").strip() or DEFAULT_MODEL
    repo_name = BIREFNET_MODELS.get(u)
    if repo_name is None:
        raise ValueError(
            f"Unknown BiRefNet usage {u!r}. Options: {', '.join(sorted(BIREFNET_MODELS.keys()))}"
        )
    local_dir = os.path.join(_checkpoint_base_dir(), repo_name)
    repo_id = f"ZhengPeng7/{repo_name}"
    return u, repo_name, os.path.abspath(local_dir), repo_id


def _scan_files(local_dir: str) -> tuple[list[str], int]:
    if not os.path.isdir(local_dir):
        return [], 0
    names: list[str] = []
    total = 0
    try:
        for fn in sorted(os.listdir(local_dir)):
            fp = os.path.join(local_dir, fn)
            if os.path.isfile(fp):
                names.append(fn)
                try:
                    total += os.path.getsize(fp)
                except OSError:
                    pass
    except OSError:
        return [], 0
    return names, total


def classify_checkpoint_files(filenames: list[str]) -> dict[str, bool | int]:
    lowered = {n.lower(): n for n in filenames}
    has_config = "config.json" in lowered
    has_weights = any(
        n.endswith(".safetensors") or n.endswith(".bin") for n in filenames
    )
    return {
        "has_config": has_config,
        "has_weights": has_weights,
        "file_count": len(filenames),
    }


def abort_message_if_checkpoint_corrupt(local_dir: str, repo_id: str) -> str | None:
    """If the checkpoint folder exists but cannot work with transformers, return reason.

    Returns None when:
    - directory does not exist (first run will snapshot_download), or
    - directory looks complete enough to try loading.
    """
    if not os.path.isdir(local_dir):
        return None
    names, _ = _scan_files(local_dir)
    if not names:
        return None
    cc = classify_checkpoint_files(names)
    if cc["has_weights"] and cc["has_config"]:
        return None

    parts = [
        f"BiRefNet checkpoint folder exists but looks INCOMPLETE (load will fail or hang).",
        f"Path: {local_dir}",
        f"HuggingFace repo: {repo_id}",
        f"Files found: {cc['file_count']}",
    ]
    if not cc["has_weights"]:
        parts.append("Missing: no *.safetensors or *.bin weight file.")
    if not cc["has_config"]:
        parts.append("Missing: config.json (required for transformers).")
    parts.append(
        "Fix: delete this folder and let the runner re-download, "
        "or restore it from a full Hugging Face snapshot (huggingface-cli download, etc.)."
    )
    return "\n".join(parts)


def format_full_report(usage: str) -> Tuple[bool, str]:
    """Return (looks_ok, multiline report). `looks_ok` is a heuristic (disk + torch only)."""
    lines: list[str] = []
    ok = True
    u, repo_name, local_dir, repo_id = resolve_paths(usage)
    lines.append(f"usage={u!r}  ->  folder={repo_name!r}  ->  hf={repo_id}")
    lines.append(f"cwd={os.getcwd()}")
    mod_parent = os.path.join(os.getcwd(), "modules", "BiRefNetModule")
    if not os.path.isdir(mod_parent):
        ok = False
        lines.append(
            "ERROR: modules/BiRefNetModule not found under cwd.\n"
            f"  Expected: {mod_parent}\n"
            "  Set Unity backend “Working Directory” to your EZ/R root (the tree that contains modules/)."
        )
    lines.append(f"Checkpoint path:\n  {local_dir}")

    names, nbytes = _scan_files(local_dir)
    if not os.path.isdir(local_dir):
        lines.append("State: directory does not exist yet (first run will download from Hugging Face).")
        # Not an error — nothing is corrupted yet.
    elif not names:
        lines.append("State: directory EXISTS but is empty — stale or failed extract; safe to delete and retry.")
        ok = False
    else:
        cc = classify_checkpoint_files(names)
        mib = nbytes / (1024.0 * 1024.0)
        lines.append(f"State: {cc['file_count']} file(s), ~{mib:.1f} MiB total")
        if not cc["has_weights"]:
            lines.append("PROBLEM: no *.safetensors or *.bin — weights missing.")
            ok = False
        if not cc["has_config"]:
            lines.append("PROBLEM: no config.json — transformers load will fail.")
            ok = False
        show = names if len(names) <= 40 else names[:40] + [f"... ({len(names) - 40} more)"]
        lines.append("Files:\n  " + "\n  ".join(show))

    abort = abort_message_if_checkpoint_corrupt(local_dir, repo_id)
    if abort:
        ok = False
        lines.append("")
        lines.append("--- abort_if_running ---")
        lines.append(abort)

    lines.append("")
    lines.append("--- Python / GPU ---")
    try:
        import torch

        lines.append(f"torch: {torch.__version__}")
        lines.append(f"cuda available: {torch.cuda.is_available()}")
        if torch.cuda.is_available():
            lines.append(f"cuda device: {torch.cuda.get_device_name(0)}")
        mps = getattr(torch.backends, "mps", None)
        if mps is not None:
            lines.append(f"mps available: {mps.is_available()}")
    except Exception as exc:  # noqa: BLE001
        lines.append(f"torch: NOT importable ({exc})")
        ok = False

    return ok, "\n".join(lines)
