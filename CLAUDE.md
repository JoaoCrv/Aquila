# Aquila — Claude Context

## Project
Desktop hardware monitor for Windows. WPF + .NET 9 + LibreHardwareMonitor.
MVVM with CommunityToolkit.Mvvm, DI via Microsoft.Extensions.Hosting, WPF-UI component kit.

- Repo: https://github.com/JoaoCrv/Aquila
- License: MPL 2.0

## Stack
| Layer | Technology | Version |
|---|---|---|
| UI | WPF + WPF-UI (Lepo.co) | 4.0.2 |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| Hardware | LibreHardwareMonitorLib | 0.9.4 |
| Charts | LiveChartsCore.SkiaSharpView.WPF | — |
| DI/Host | Microsoft.Extensions.Hosting | 9.0.1 |
| Auto-update | Velopack | 0.0.1298 |

## Session Rules
- Builds are done manually by the user (`Ctrl+Shift+B` in VS). If build output is needed to diagnose errors, ask the user to paste it.
- No `git diff` without a specific file path — generates excessive output.
- No `&&` in PowerShell — not valid. Use separate commands or `;`.
- No long-output terminal commands (>20 lines output).
- Commits via user's VS Source Control or external PowerShell, not through the terminal here.
- Start a new chat when the conversation gets long to avoid context bloat.

## Architecture
`App.xaml.cs` is the composition root (Generic Host + DI). All services and ViewModels are singletons.
Views own their ViewModel via constructor injection; `DataContext = this`; XAML binds via `ViewModel.PropertyName`.
`HardwareMonitorService` polls via `DispatcherTimer` (1 s) and fires `DataUpdated`.

The current LHM coupling will be replaced by an Anti-Corruption Layer — `IHardwareReader` interface backed by `LhmHardwareAdapter`. This is Phase X, currently in progress.

## Pending Work

### Active — Phase X: Hardware API Refactor
**Branch:** `refactor/hardware-semantics-foundation`

- [ ] X.1 Update LHM → 0.9.6 in `Aquila.csproj`
- [ ] X.2 Domain snapshot records in `Models/HardwareSnapshots.cs` (no LHM imports)
- [ ] X.3 `IHardwareReader` interface in `Services/IHardwareReader.cs`
- [ ] X.4 `LhmHardwareAdapter : IHardwareReader, IDisposable` — absorbs `HardwareMonitorService` + `SensorLocator`
- [ ] X.5 Register in DI, remove `HardwareMonitorService`
- [ ] X.6 Rewrite `DashboardViewModel` to consume snapshots (removes `SensorLocator`, `_prev*` cache, `NotifySensorReferences`)
- [ ] X.7 Update `DashboardPage.xaml` bindings
- [ ] X.8 Update `ExplorerViewModel` to use `IHardwareReader.RawTree`
- [ ] X.9 Delete `Services/HardwareMonitorService.cs`, `Helpers/SensorLocator.cs`
- [ ] X.10 Update `ApplicationHostService` (`Start` / `Dispose`)

### Backlog (rough priority)
- **2.6** Multi-network adapter support
- **3.5** Storage sparklines (unblocked after Phase X)
- **4.13** Fan card: Control % + dynamic Maximum (resolved by X.4)
- **4.14** Typography tokens in theme system
- **Phase 5** Settings & persistence (JSON file, theme, window pos, polling interval, card visibility)
- **Phase 6** Logging — `Microsoft.Extensions.Logging` + file sink
- **Phase 7** System tray, minimize to tray, auto-launch
- **Phase 9** Release pipeline automation (build.ps1, GitHub Actions)
- **Phase 12** Performance & memory (per-tick allocation reduction, minimize-throttle)
- **Phase 13** Full provider architecture (multi-source aggregator — supersedes Phase X)
- **Phase 11** Desktop widgets — depends on Phase 13

## Reference Docs
- Full roadmap with all completed items: `docs/ROADMAP.md`
- Architecture detail & session history: `docs/PROJECT_CONTEXT.md`
- Open GitHub issues: https://github.com/JoaoCrv/Aquila/issues
