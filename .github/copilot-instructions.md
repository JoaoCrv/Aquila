# Aquila Project Guidelines

## Project Shape

- `Aquila` is a Windows-only WPF hardware monitoring app targeting `net9.0-windows10.0.19041`.
- Core libraries already in use: `WPF-UI`, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.Hosting`, `LibreHardwareMonitorLib`, and `Velopack`.

## Architecture

- Preserve the existing **MVVM + DI + service layer** structure wired in `App.xaml.cs`.
- Keep hardware polling, OS interop, and LibreHardwareMonitor access inside `Services/`; keep `ViewModels` focused on UI state and commands.
- Views/pages typically receive their ViewModel through constructor injection, expose it as `ViewModel`, and bind through `DataContext = this`.
- Reuse shared pieces from `Helpers/`, `Extensions/`, `Controls/`, `Styles/`, and `Themes/` before adding new abstractions.

## Repo-Specific Conventions

- In ViewModels, prefer `partial` classes with `[ObservableProperty]` and `[RelayCommand]` from `CommunityToolkit.Mvvm`.
- Avoid hardcoding machine-specific sensor identifiers in UI code. Prefer extending `Helpers/SensorLocator.cs` and keep missing-sensor behavior null-safe.
- Follow the existing naming/folder patterns: `*ViewModel`, `*Page`, `*Converter`, plus `Services/`, `Helpers/`, and `Models/`.
- If a class subscribes to events, timers, or theme changes, add cleanup via `IDisposable` to match the current lifecycle pattern.

## Build and Validation

- Prefer user-driven verification with `Ctrl+Shift+B` in Visual Studio after code changes.
- Avoid long/noisy terminal commands in this repo, and never use `&&` in PowerShell.
- There are currently **no automated tests** in the workspace, so provide clear manual verification steps when needed.
- Release packaging is handled by `build.ps1` and `Velopack`; only change that flow when the task is explicitly about packaging/releases.

## Docs to Consult

- See `docs/PROJECT_CONTEXT.md` for architecture, stack details, and AI workflow constraints.
- See `docs/ROADMAP.md` for feature planning, known issues, and previously fixed bugs.
- See `README.md` for the high-level project overview.
