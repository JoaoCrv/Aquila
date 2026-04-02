---
applyTo: "Services/**/*.cs,Helpers/SensorLocator.cs,Models/**/*.cs,App.xaml.cs"
description: "Use when editing Aquila services, sensor lookup helpers, hardware models, or DI registration. Keep LibreHardwareMonitor and OS polling logic inside the service layer."
---

# Aquila Service Boundaries Instructions

## Keep the architecture intact

- Preserve the current **MVVM + DI + service layer** structure centered on `App.xaml.cs`.
- Keep hardware polling, Windows interop, PDH/native calls, and LibreHardwareMonitor access inside `Services/`.
- Keep `ViewModels/` focused on UI state, derived display values, and commands; do not move raw hardware access into page code-behind or ViewModels.

## Sensor access rules

- Do not hardcode machine-specific sensor identifiers in UI code.
- Prefer extending `Helpers/SensorLocator.cs` for reusable sensor discovery and fallback logic.
- Keep sensor lookup null-safe because hardware availability varies by machine, vendor, and startup timing.

## Service and model patterns

- Follow the existing singleton-style service registration in `App.xaml.cs` and the hosted startup flow in `Services/ApplicationHostService.cs`.
- If a service subscribes to events, owns timers, or opens native/hardware resources, implement `IDisposable` and clean up explicitly.
- Keep `Models/` simple and UI-friendly; use them to transport hardware data rather than embedding WPF-specific behavior in services.

## Scope changes carefully

- Reuse existing helpers and models before adding a new abstraction.
- For incremental work, prefer small extensions to `HardwareMonitorService`, `SensorLocator`, or the relevant model instead of introducing a parallel data path.
- Large architecture changes related to the Anti-Corruption Layer or `IHardwareReader` should only be made when the task explicitly targets the Phase X refactor in `docs/ROADMAP.md`.

## Validation and workflow

- This repo currently relies on manual verification rather than automated tests.
- Prefer concise validation steps the user can run with `Ctrl+Shift+B` in Visual Studio.
- Avoid long/noisy terminal commands in this workspace, and never use `&&` in PowerShell.
