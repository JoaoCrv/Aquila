# Aquila — Project Context

> Auto-generated context file for AI-assisted development.
> Last updated based on branch `Home_Explorer`, version **1.0.2**.

---

## 0. AI Assistant — Session Rules

> Rules established to avoid terminal lockups and wasted context budget.

- **No `run_build`** — builds are done manually with `Ctrl+Shift+B` in Visual Studio. If there are errors, the user pastes them into the chat.
- **No `git diff`** without a specific file path — generates excessive output that blocks the terminal.
- **No long-output terminal commands** — anything that may produce more than ~20 lines of output should be avoided.
- **No `&&` in PowerShell** — it is not a valid statement separator. Use separate commands or `git -C <path> <command>`.
- **Commits** are done via the user's external PowerShell or Visual Studio Source Control panel, not through the Copilot terminal.
- **Build verification** — after code changes, ask the user to build with `Ctrl+Shift+B` and report any errors.
- **Chat length** — long chats accumulate context and cause the terminal to hang on permission prompts. Start a new chat when the conversation becomes long, and re-read this file and `docs/ROADMAP.md` to restore context.

---

## 1. What Is Aquila?

Aquila is an **open-source desktop hardware monitoring application** built for Windows.
Its primary use case is displaying real-time system metrics (CPU, GPU, RAM, Network, Storage) on a secondary screen in a clean, modern UI.

- **License:** MPL 2.0
- **Author:** [@JoaoCrv](https://github.com/JoaoCrv)
- **Repository:** <https://github.com/JoaoCrv/Aquila>
- **Branch:** `Home_Explorer`
- **GitHub CLI:** ? Available — use `gh` commands for issue management
- **Open Issues:** 4 ([view all](https://github.com/JoaoCrv/Aquila/issues))
- **Labels:** `bug`, `enhancement`, `documentation`, `good first issue`, `help wanted`, `question`

---

## 2. Technology Stack

| Layer            | Technology                                                                              | Version  |
| ---------------- | --------------------------------------------------------------------------------------- | -------- |
| Runtime          | .NET 9 (Windows Desktop)                                                                | net9.0   |
| UI Framework     | WPF                                                                                     | —        |
| UI Component Kit | [WPF-UI (Lepo.co)](https://github.com/lepoco/wpfui)                                    | 4.0.2    |
| MVVM Toolkit     | CommunityToolkit.Mvvm                                                                   | 8.4.0    |
| DI / Hosting     | Microsoft.Extensions.Hosting                                                            | 9.0.1    |
| DI (WPF-UI)      | WPF-UI.DependencyInjection (page provider)                                              | 4.0.2    |
| Hardware Data    | [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.4    |
| Charts           | LiveChartsCore.SkiaSharpView.WPF                                                        | —        |
| Auto-Update      | [Velopack](https://velopack.io/)                                                        | 0.0.1298 |
| Versioning       | [Versionize](https://github.com/versionize/versionize) (global .NET tool)              | 2.4.0    |

---

## 3. Architecture Overview

```
???????????????????????????????????????????????????????????
?  App.xaml.cs  (Composition Root)                        ?
?  ??? Microsoft.Extensions.Hosting (Generic Host)        ?
?  ??? DI container registrations                         ?
?  ??? Velopack auto-update bootstrap                     ?
???????????????????????????????????????????????????????????
                         ?
          ???????????????????????????????
          ?              ?              ?
   ???????????????? ??????????? ????????????????
   ?  Services    ? ? Models  ? ?  ViewModels  ?
   ???????????????? ??????????? ????????????????
         ?               ?              ?
         ?  ??????????????              ?
         ?  ?                           ?
   ???????????????????          ????????????????
   ? HardwareMonitor ????????????  Dashboard   ?
   ?    Service      ?          ?  ViewModel   ?
   ? (DispatcherTimer?          ????????????????
   ?  1s polling)    ?          ?  Explorer    ?
   ???????????????????          ?  ViewModel   ?
            ?                   ????????????????
            ?                           ?
   ???????????????????          ????????????????
   ?  ComputerData   ?          ?    Views     ?
   ?  (Model root)   ?          ? (XAML Pages) ?
   ?  ? HardwareList ?          ? DataContext =?
   ?  ? SensorIndex  ?          ?   this       ?
   ???????????????????          ? ViewModel =  ?
                                ?   injected   ?
                                ????????????????
```

### Pattern: MVVM with Service Layer

- **Views** own their `ViewModel` via constructor injection and expose it as a property.
- `DataContext = this` on each page; XAML binds via `ViewModel.PropertyName`.
- **ViewModels** use `ObservableObject` (CommunityToolkit.Mvvm) with `[ObservableProperty]` and `[RelayCommand]` source generators.
- **Services** are registered as singletons in the DI container.

---

## 4. Key Services

### 4.1 `HardwareMonitorService`
- Wraps `LibreHardwareMonitor.Hardware.Computer`.
- Opens the computer with all categories enabled (CPU, GPU, Memory, Motherboard, Storage, Network).
- Uses a `DispatcherTimer` (1 second interval) to poll sensor data on the UI thread.
- Builds and maintains a `ComputerData` model:
  - `HardwareList` — `ObservableCollection<DataHardware>` (hierarchical, for Explorer).
  - `SensorIndex` — `Dictionary<string, DataSensor>` (flat lookup by identifier, for Dashboard widgets).
- Fires `DataUpdated` event each tick so ViewModels can refresh.
- Implements `IDisposable` — `Computer.Close()` and timer stop on disposal.
- Also reads Windows memory extras via `PerformanceCounter` (Page Reads/sec, Page Writes/sec) and `GetPerformanceInfo` P/Invoke (Cache Bytes).

### 4.2 `ApplicationHostService`
- `IHostedService` — calls `HardwareMonitorService.StartMonitoring()` and opens the main window on `StartAsync`. Calls `HardwareMonitorService.Dispose()` on `StopAsync`.

### 4.3 `UiService`
- Tiny observable singleton with `IsLoading` flag, used for a global loading overlay.

---

## 5. Data Model

```
ComputerData
??? HardwareList : ObservableCollection<DataHardware>
?   ??? DataHardware
?       ??? Identifier, Name, HardwareType
?       ??? Sensors : ObservableCollection<DataSensor>
??? SensorIndex : Dictionary<string, DataSensor>
    ??? DataSensor : ObservableObject
        ??? Index, Identifier, Name, SensorType, Unit
        ??? Value, Min, Max  (observable via [ObservableProperty])
```

---

## 6. Pages & Navigation

| Page           | ViewModel          | Status        | Description                                              |
| -------------- | ------------------ | ------------- | -------------------------------------------------------- |
| DashboardPage  | DashboardViewModel | ? Functional  | Cards with real-time CPU/GPU/RAM/Network stats + charts. |
| ExplorerPage   | ExplorerViewModel  | ? Functional  | Grouped tree of all sensors (all hardware).              |
| StoragePage    | —                  | ?? Placeholder | Static text only. No ViewModel.                          |
| AboutPage      | AboutViewModel     | ? Functional  | Shows assembly version.                                  |
| SettingsPage   | SettingsViewModel  | ? Functional  | Theme toggle (Light/Dark) with WPF-UI.                   |

Navigation is handled by **WPF-UI's `NavigationView`** with `LeftFluent` pane mode, Mica backdrop, and a breadcrumb header.

All pages implement `INavigableView<TViewModel>`. `ExplorerPage` and `SettingsPage` also implement `INavigationAware`.

---

## 7. How Each Package Is Used

### LibreHardwareMonitorLib 0.9.4
- `Computer` class — opened with all subsystems enabled.
- `IVisitor` pattern (`UpdateVisitor`) — traverses and updates hardware.
- `IHardware.Sensors` and `SubHardware` — iterated to build the flat + hierarchical model.
- `SensorType` enum — used for grouping and unit mapping.

### WPF-UI 4.0.2
- `FluentWindow` — main window with `Mica` backdrop, round corners, extends into title bar.
- `NavigationView` (LeftFluent) — sidebar navigation with icons (`SymbolRegular`).
- `TitleBar` — custom title bar with icon.
- `BreadcrumbBar` — page breadcrumbs.
- `SnackbarPresenter` — snackbar slot (not yet used).
- `Card` / `CardExpander` — dashboard and explorer widget containers.
- `ProgressRing` — global loading overlay.
- `ThemesDictionary` / `ControlsDictionary` — resource dictionaries for theming.
- `ApplicationThemeManager` / `SystemThemeWatcher` — runtime theme switching.

### WPF-UI.DependencyInjection 4.0.2
- `AddNavigationViewPageProvider()` — auto-resolves pages from DI for `NavigationView`.

### CommunityToolkit.Mvvm 8.4.0
- `ObservableObject` — base class for all ViewModels and `DataSensor`.
- `[ObservableProperty]` — source-generated bindable properties.
- `[RelayCommand]` — source-generated `ICommand` implementations.
- Used via `global using` in `Usings.cs`.

### Microsoft.Extensions.Hosting 9.0.1
- `Host.CreateDefaultBuilder()` — application host with DI, configuration.
- `IHostedService` — `ApplicationHostService` lifecycle.
- `IServiceProvider` — static access via `App.Services`.

### LiveChartsCore.SkiaSharpView.WPF
- `CartesianChart` — sparkline charts in Dashboard cards (CPU usage history, GPU load history, per-GPU sparklines).
- `LineSeries<double>` with `ObservableCollection<double>` ring buffers (60 points = 60 seconds).
- Theme-aware via `ApplicationThemeManager.Changed` — series colors update on Light/Dark switch.

### Velopack 0.0.1298
- `VelopackApp.Build().Run()` — early startup hook in `App` constructor.
- `UpdateManager` + `GithubSource` — checks GitHub releases for updates, downloads, and applies with restart.
- **Velopack CLI (`vpk`)** — used in `build.ps1` to package the app into an installer (`vpk pack`).

### Versionize 2.4.0 (global .NET tool)
- Installed globally (`dotnet tool install -g versionize`).
- Follows [Conventional Commits](https://www.conventionalcommits.org/) — commit messages like `feat:`, `fix:`, `refactor:`, `BREAKING CHANGE:` drive automatic versioning.
- Running `versionize` bumps the `<Version>` in `Aquila.csproj`, generates/appends `CHANGELOG.md`, and creates a git tag.
- Current version: **1.0.2** (set in `Aquila.csproj`).

### Release Pipeline (`build.ps1`) — git-ignored
Current manual release script. Steps:
1. `dotnet publish -c Release -r win-x64` — self-contained publish.
2. Validates publish directory and PFX certificate exist.
3. Prompts for version number manually (`Read-Host`).
4. `vpk pack` — packages with Velopack CLI, signs with code-signing certificate (`aquila-cert.pfx`, also git-ignored).
5. Signing uses SHA-256 with Comodo timestamp server.

**Known issues with the current script:**
- Version is entered manually — should be read from `Aquila.csproj` (Versionize already bumps it).
- PFX password is hardcoded in plain text — should use a secure vault or environment variable.
- No integration with Versionize — the two tools run independently.
- No automatic GitHub Release creation — releases must be uploaded manually.

---

## 8. Project Structure

```
Aquila/
??? App.xaml / App.xaml.cs          # Composition root, DI, Velopack
??? Aquila.csproj                   # Project file (.NET 9, WPF)
??? Usings.cs                       # Global usings (System, CommunityToolkit.Mvvm)
??? AssemblyInfo.cs                 # WPF ThemeInfo
??? app.manifest                    # UAC (asInvoker), DPI awareness
??? CHANGELOG.md                    # Auto-generated by Versionize
?
??? Assets/
?   ??? icon.ico
?   ??? Images/icon.png
?
??? docs/                           # Public documentation (tracked by git)
?   ??? ROADMAP.md                  # Development roadmap with GitHub issue links
?   ??? PROJECT_CONTEXT.md          # Architecture, stack, and project overview (this file)
?
??? drafts/                         # Private working files (git-ignored)
?   ??? DEVELOPMENT_INSTRUCTIONS.md # Coding conventions and AI assistant context
?
??? Helpers/
?   ??? AccentBrushProvider.cs      # Theme-aware accent brush singleton (ObservableObject)
?   ??? EnumToBooleanConverter.cs   # IValueConverter for radio buttons ? enum
?   ??? HardwareTypeToIconConverter.cs  # HardwareType ? SymbolRegular icon
?   ??? NullToCollapsedConverter.cs # null ? Collapsed visibility converter
?   ??? ProgressBarWidthConverter.cs    # Multi-value converter for rounded progress bar template
?   ??? SensorLocator.cs            # Static helper for hardware/sensor discovery (no hardcoded IDs)
?   ??? ThroughputConverter.cs      # float B/s ? human-readable KB/s / MB/s
?
??? Extensions/
?   ??? TaskExtensions.cs           # SafeFireAndForget async helper
?
??? Models/
?   ??? HardwareMonitorModel.cs     # DataSensor, DataHardware, ComputerData (ACTIVE)
?
??? Services/
?   ??? HardwareMonitorService.cs   # Core hardware polling service (IDisposable)
?   ??? ApplicationHostService.cs   # IHostedService startup
?   ??? UiService.cs                # Global loading state
?
??? ViewModels/
?   ??? Windows/
?   ?   ??? MainWindowViewModel.cs  # Nav items, title, IsLoading relay
?   ??? Pages/
?       ??? DashboardViewModel.cs   # Multi-GPU, charts, per-core bars, RAM extras, Network
?       ??? ExplorerViewModel.cs    # Grouped hardware list builder (async, with loading)
?       ??? SettingsViewModel.cs    # Theme switching (INavigationAware)
?       ??? AboutViewModel.cs       # Assembly version display
?
??? Views/
?   ??? Windows/
?   ?   ??? MainWindow.xaml(.cs)    # FluentWindow + NavigationView
?   ??? Pages/
?       ??? DashboardPage.xaml(.cs) # 3-column layout: CPU+GPU / RAM+Network / Power+Temps+Fans
?       ??? ExplorerPage.xaml(.cs)  # CardExpander per hardware, sensor ListBox per group
?       ??? StoragePage.xaml(.cs)   # Placeholder (no ViewModel)
?       ??? SettingsPage.xaml(.cs)  # Theme toggle
?       ??? AboutPage.xaml(.cs)     # About info
?
??? Styles/
?   ??? PageStyles.xaml             # BasePageStyle + semantic accent brushes (Light/Dark variants)
?
??? .github/                        # Issue templates, setup docs
```

---

## 9. Key Helpers Detail

### `SensorLocator` (`Helpers/SensorLocator.cs`)
Static class that centralises all sensor discovery. No hardcoded identifiers — uses name-pattern matching with fallbacks.

Key methods:
- `CpuTemperature`, `CpuLoad`, `CpuPower`, `CpuCoreSensors`, `CpuFan`
- `AllGpus`, `GpuTemperatureFor`, `GpuLoadFor`, `GpuClockFor`, `GpuPowerFor`, `GpuVramUsedFor`, `GpuVramTotalFor`, `GpuFanFor`
- `MemoryLoad`, `MemoryUsed`, `MemoryAvailable`, `MemoryPower`, `VirtualMemoryUsed`, `VirtualMemoryAvailable`
- `NetworkUploadSpeed`, `NetworkDownloadSpeed`, `NetworkDataUploaded`, `NetworkDataDownloaded`
- `SystemTemperatures`, `MotherboardFans`

### `AccentBrushProvider` (`Helpers/AccentBrushProvider.cs`)
Singleton `ObservableObject` exposed as `{StaticResource AccentBrushes}` in `App.xaml`. Exposes theme-aware `Brush` properties (`CpuAccent`, `GpuAccent`, `RamAccent`, `TempAccent`, `GpuTempAccent`, `PowerAccent`, `GaugeBackground`). Automatically refreshes on `ApplicationThemeManager.Changed`.

Brush keys are defined in `Styles/PageStyles.xaml` as `SolidColorBrush` resources with `.Dark` / `.Light` suffixes.

---

## 10. Known Issues & Technical Debt

> Items marked ? are resolved in the current codebase. Items marked ?? are open.
> For the full prioritised list, see `docs/ROADMAP.md`.

| # | Issue | Status |
|---|-------|--------|
| 1 | Hardcoded sensor identifiers in `DashboardViewModel` | ? Resolved — `SensorLocator` used throughout |
| 2 | Legacy model files (`SensorInfo.cs`, `hardwareModel.cs`) | ? Resolved — removed from project |
| 3 | `AppConfig.cs` unused placeholder | ? Resolved — removed from project |
| 4 | `Translations.cs` empty placeholder | ? Resolved — removed from project |
| 5 | `StoragePage` no ViewModel, no data | ?? Open — see Roadmap Phase 3 |
| 6 | `MainWindow.xaml.cs` duplicate `GetNavigation()` / `SetServiceProvider` throwing | ? Resolved — `SetServiceProvider` is a no-op, no duplicates |
| 7 | Dashboard XAML "CPU Speed" card duplicated in GPU section | ? Resolved |
| 8 | No `IDisposable` on `HardwareMonitorService` | ? Resolved — `Dispose()` implemented and called on app exit |
| 9 | No logging framework | ?? Open — see Roadmap Phase 6 |
| 10 | `OnPropertyChanged(nameof(_effectiveCpuClock))` wrong backing field name | ? Resolved — `[ObservableProperty]` handles notification correctly |
| 11 | Network sensors missing in `DashboardViewModel` | ? Resolved — `NetworkUploadSpeedSensor` etc. implemented via `SensorLocator` |
| 12 | `OnPropertyChanged` on sensor properties called every tick unnecessarily | ?? Open — see Roadmap 0.14 |
| 13 | Dashboard XAML 400+ lines of repetitive markup | ?? Open — see Roadmap 4.8 |
| 14 | `DashboardViewModel` never unsubscribes from `ApplicationThemeManager.Changed` | ?? Open — see Roadmap 0.7 |
| 15 | `ExplorerPage` content clipped when many sections expanded (no ScrollViewer) | ?? Open — see Roadmap 0.4 |
| 16 | `ProgressBarWidthConverter` silently returns 0 for `float` inputs | ?? Open — see Roadmap 0.15 |
