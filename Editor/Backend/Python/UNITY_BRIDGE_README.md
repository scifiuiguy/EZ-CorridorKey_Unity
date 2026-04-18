# Unity Bridge Architecture

The Unity Bridge provides a stdio-based communication layer between Unity Editor and the EZ-CorridorKey Python backend, enabling ML-powered features like alpha generation and model management within the Unity environment.

## Architecture Overview

```
Unity Editor (C#)
    ↓ stdio
Python Bridge Process
    ├── unity_bridge.py (main entry point, command routing)
    ├── bridge_core.py (core communication primitives)
    ├── alpha_generation.py (alpha/matte generation commands)
    └── model_management.py (ML model download/management commands)
        ↓ dynamic imports
    EZ-CorridorKey Reference Implementation (R/)
```

## Core Components

### unity_bridge.py
- **Main Entry Point**: Handles stdio communication with Unity
- **Command Dispatch**: Routes incoming commands to appropriate handlers
- **Thread Management**: Runs long-running operations in background threads
- **Error Handling**: Provides structured error reporting to Unity

### bridge_core.py
- **Communication Primitives**: JSON message serialization/deserialization
- **Event Emission**: Standardized logging and result reporting
- **Protocol Constants**: Message type definitions and validation

### alpha_generation.py
- **Alpha Generation**: Handles foreground/background separation
- **Inference Pipeline**: Orchestrates ML model execution
- **Result Processing**: Formats outputs for Unity consumption

### model_management.py
- **Model Downloads**: GVM, SAM2, VideoMaMa model acquisition
- **Status Checking**: Verifies model installation and availability
- **Dynamic Imports**: Leverages EZ-CorridorKey's setup_models.py

## Communication Protocol

### Message Format
All communication uses JSON messages over stdio:

```json
{
  "cmd": "command.name",
  "request_id": "unique_request_id",
  "param1": "value1",
  "param2": "value2"
}
```

### Response Types
- **log**: Debug/info/warning messages
- **error**: Error conditions
- **diag_result**: Diagnostic command results
- **done**: Command completion status

### Command Categories

#### Alpha Generation Commands
- `alpha.generate`: Generate alpha matte from video frame
- `alpha.batch_generate`: Process multiple frames
- `alpha.get_status`: Check generation progress

#### Model Management Commands
- `model.download_gvm`: Download GVM segmentation model
- `model.download_sam2`: Download SAM2 tracking model
- `model.download_videomama`: Download VideoMaMa inference model
- `model.check_status`: Verify all models are installed
- `model.is_installed`: Check specific model availability

## Adding New Commands

### 1. Create Handler Function
Add a function in the appropriate module (e.g., `alpha_generation.py`):

```python
def _run_my_new_command(request_id: str, param1: str) -> None:
    cmd_name = "my.new_command"
    try:
        # Your implementation here
        result = do_something(param1)
        bridge_core._emit_done(cmd_name, request_id, ok=True, summary="Success")
    except Exception as exc:
        bridge_core._emit({"type": "error", "message": str(exc)})
        bridge_core._emit_done(cmd_name, request_id, ok=False, summary=str(exc))
```

### 2. Register in Dispatch
Add to the dispatch table in `unity_bridge.py`:

```python
if cmd == "my.new_command":
    param1 = msg.get("param1") or ""
    threading.Thread(target=my_module._run_my_new_command, args=(rid, param1), daemon=True).start()
    return True
```

### 3. Update Imports
Ensure the module is imported in `unity_bridge.py`:

```python
try:
    from . import my_module
except ImportError:
    import my_module
```

## Dependencies

### Python Requirements
- Python 3.8+
- Dynamic imports from EZ-CorridorKey reference implementation
- ML frameworks (PyTorch, etc.) via EZ-CorridorKey

### File Structure Requirements
```
F:\CorridorKey\
├── EZ-CorridorKey_Unity\Editor\Backend\Python\  # This bridge
└── R\scripts\setup_models.py                    # Reference implementation
```

## Error Handling

### Structured Error Reporting
All errors are reported with:
- Error message
- Command completion status
- Request ID correlation

### Thread Safety
Long-running operations use daemon threads to prevent blocking Unity Editor.

### Recovery
Failed operations can be retried by Unity without restarting the bridge process.

## Development Notes

### Testing
Test commands via stdio:
```bash
echo '{"cmd": "model.check_status", "request_id": "test123"}' | python unity_bridge.py
```

### Debugging
Enable verbose logging by modifying `bridge_core._emit()` calls.

### Performance
- Model downloads run in background threads
- Large data transfers use efficient JSON serialization
- Memory usage monitored via Python's built-in tools

## Integration with Unity

The bridge is launched by Unity's C# code and communicates via:
- Standard input (commands from Unity)
- Standard output (responses to Unity)
- Standard error (fatal errors only)

Unity parses JSON responses and updates UI accordingly.</content>
<parameter name="filePath">f:\CorridorKey\EZ-CorridorKey_Unity\Editor\Backend\Python\UNITY_BRIDGE_README.md