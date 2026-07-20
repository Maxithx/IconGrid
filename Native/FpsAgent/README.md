# Native FPS Agent

This folder contains the planned native FPS worker for IconGrid.

## Scope

- C++ owns ETW-based FPS capture only.
- C# continues to own launcher UI, gaming overlay UI, settings, persistence, and orchestration.
- The worker is intended to replace the current `PresentMon`/C# prototype path once integrated.

## Contract

The first worker slice writes a small JSON state file that C# can later consume:

```json
{
  "capturedAtUtc": "2026-07-18T15:04:05.123Z",
  "fpsStatus": "144",
  "targetPid": 12345,
  "targetProcessName": "PathOfExile_x64Steam.exe",
  "error": ""
}
```

## Runtime shape

Expected worker arguments:

- `--state-path <path>`
- `--parent-pid <pid>` optional

The worker:

- starts a real-time ETW session
- tracks the current foreground process
- ignores `IconGrid.exe`
- calculates FPS from DXGI, D3D9, and DxgKrnl present events
- writes the latest state to the provided JSON file

## Status

This is the native worker scaffold and first ETW slice.
The C# runtime has not been switched over to consume this worker yet.

## Build note

This worker is intentionally kept out of the main `IconGrid.sln` so the normal `dotnet build IconGrid.sln` flow for the WPF app does not break on machines where the Visual C++ targets are not available in the .NET CLI path.
